using System.Collections.Concurrent;
using NPUtil.Core.Hardware;
using NPUtil.Core.Logging;
using NPUtil.Core.Services;
using NPUtil.Core.Settings;
using NPUtil.Modules.FileOrganizer.Classification;
using NPUtil.Modules.FileOrganizer.Extraction;
using NPUtil.Modules.FileOrganizer.FileOperations;
using NPUtil.Modules.FileOrganizer.Models;
using NPUtil.Modules.FileOrganizer.Monitoring;
using NPUtil.Modules.FileOrganizer.Search;

namespace NPUtil.Modules.FileOrganizer.Core;

public class ReviewItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string SuggestedCategory { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public double RawSimilarity { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public IReadOnlyList<string> Keywords { get; set; } = Array.Empty<string>();
    public IReadOnlyList<DestinationSuggestion> Alternatives { get; set; } = Array.Empty<DestinationSuggestion>();
    public ItemStatus Status { get; set; } = ItemStatus.PendingReview;
    public ExtractedMetadata? Metadata { get; set; }
    public ClassificationResult? Classification { get; set; }
}

public class OrganizerEngine : IDisposable
{
    private readonly FileContentExtractor _extractor;
    private readonly HybridClassifier _classifier;
    private readonly SafeFileMover _mover;
    private readonly UndoManager _undoManager;
    private readonly OperationHistoryDb _historyDb;
    private readonly FileSearchIndex _searchIndex;
    private readonly DownloadsWatcher _watcher;
    private readonly TaxonomyManager _taxonomyManager;
    private readonly UserCorrectionMemory _userMemory;
    private readonly IHardwareService _hardwareService;
    private readonly INotificationService _notificationService;
    private readonly SettingsManager _settingsManager;
    private readonly ILogger _logger;

    private readonly ConcurrentDictionary<string, ReviewItem> _reviewQueue = new();
    private bool _isDisposed;

    public event EventHandler? ReviewQueueChanged;
    public event EventHandler? StatusChanged;

    public OrganizerMode Mode { get; set; } = OrganizerMode.ReviewEverything;
    public double HighConfidenceThreshold { get; set; } = 0.75;
    public string WatchedFolder => _watcher.WatchedDirectory;
    public bool IsMonitoring => _watcher.IsMonitoring;

    public OrganizerEngine(
        FileContentExtractor extractor,
        HybridClassifier classifier,
        SafeFileMover mover,
        UndoManager undoManager,
        OperationHistoryDb historyDb,
        FileSearchIndex searchIndex,
        DownloadsWatcher watcher,
        TaxonomyManager taxonomyManager,
        UserCorrectionMemory userMemory,
        IHardwareService hardwareService,
        INotificationService notificationService,
        SettingsManager settingsManager,
        ILogger logger)
    {
        _extractor = extractor;
        _classifier = classifier;
        _mover = mover;
        _undoManager = undoManager;
        _historyDb = historyDb;
        _searchIndex = searchIndex;
        _watcher = watcher;
        _taxonomyManager = taxonomyManager;
        _userMemory = userMemory;
        _hardwareService = hardwareService;
        _notificationService = notificationService;
        _settingsManager = settingsManager;
        _logger = logger;

        _watcher.FileReadyToProcess += OnFileDiscovered;
    }

    public IReadOnlyList<ReviewItem> GetReviewQueue() =>
        _reviewQueue.Values.Where(v => v.Status == ItemStatus.PendingReview).ToList();

    public void StartMonitoring(string? customFolder = null)
    {
        _watcher.Start(customFolder);
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void StopMonitoring()
    {
        _watcher.Stop();
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void OnFileDiscovered(object? sender, FileDiscoveredEventArgs e)
    {
        try
        {
            await ProcessSingleFileAsync(e.FilePath);
        }
        catch (Exception ex)
        {
            _logger.Error("OrganizerEngine", $"Failed to process discovered file {e.FilePath}", ex);
        }
    }

    public async Task<ReviewItem?> ProcessSingleFileAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath)) return null;

        // Battery throttling check
        if (_settingsManager.Current.ThrottleOnBattery && _hardwareService.IsRunningOnBattery())
        {
            _logger.Info("OrganizerEngine", "Running on battery power: applying lightweight extraction profile.");
        }

        var metadata = await _extractor.ExtractAsync(filePath, ct);
        var classification = await _classifier.ClassifyAsync(metadata, ct);

        var reviewItem = new ReviewItem
        {
            FilePath = filePath,
            FileName = metadata.FileName,
            Extension = metadata.Extension,
            FileSizeBytes = metadata.FileSizeBytes,
            SuggestedCategory = classification.TargetRelativePath,
            Confidence = classification.Confidence,
            RawSimilarity = classification.RawSimilarityScore,
            Explanation = classification.Explanation,
            Keywords = classification.ExtractedKeywords,
            Alternatives = classification.AlternativeDestinations,
            Metadata = metadata,
            Classification = classification,
            Status = ItemStatus.PendingReview
        };

        // Determine action based on mode
        var shouldAutoMove = false;
        if (Mode == OrganizerMode.FullyAutomatic)
        {
            shouldAutoMove = true;
        }
        else if (Mode == OrganizerMode.IntelligentAuto && classification.Confidence >= HighConfidenceThreshold)
        {
            shouldAutoMove = true;
        }

        if (shouldAutoMove)
        {
            var batchId = "Auto-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var moveResult = await _mover.MoveFileSafelyAsync(filePath, _watcher.WatchedDirectory, classification.TargetRelativePath, classification, batchId, isDryRun: false, ct);
            if (moveResult.Success)
            {
                await _searchIndex.IndexFileAsync(metadata with { FilePath = moveResult.FinalDestinationPath }, classification.TargetRelativePath, ct);
                reviewItem.Status = ItemStatus.Organized;
                _notificationService.Notify("File Organized", $"{metadata.FileName} -> {classification.TargetRelativePath}", NotificationSeverity.Success);
                StatusChanged?.Invoke(this, EventArgs.Empty);
                return reviewItem;
            }
        }

        // Add to review queue
        _reviewQueue[reviewItem.Id] = reviewItem;
        _logger.Info("OrganizerEngine", $"Added to review queue: {reviewItem.FileName} -> {reviewItem.SuggestedCategory} ({reviewItem.Confidence:P0})");
        ReviewQueueChanged?.Invoke(this, EventArgs.Empty);
        StatusChanged?.Invoke(this, EventArgs.Empty);

        return reviewItem;
    }

    public async Task<int> RunReconciliationScanAsync(CancellationToken ct = default)
    {
        _logger.Info("OrganizerEngine", "Starting reconciliation scan of watched directory...");
        var files = _watcher.ScanTopLevelFiles();
        var processedCount = 0;

        foreach (var file in files)
        {
            if (ct.IsCancellationRequested) break;
            // Check if already in queue
            if (_reviewQueue.Values.Any(r => r.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase) && r.Status == ItemStatus.PendingReview))
            {
                continue;
            }

            var item = await ProcessSingleFileAsync(file, ct);
            if (item != null)
            {
                processedCount++;
            }
        }

        _logger.Info("OrganizerEngine", $"Reconciliation scan finished. Processed {processedCount} new files.");
        return processedCount;
    }

    public async Task<bool> ApproveItemAsync(string reviewItemId, string? customCategory = null)
    {
        if (!_reviewQueue.TryGetValue(reviewItemId, out var item)) return false;

        var targetCategory = customCategory ?? item.SuggestedCategory;
        var batchId = "Review-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");

        var moveResult = await _mover.MoveFileSafelyAsync(item.FilePath, _watcher.WatchedDirectory, targetCategory, item.Classification!, batchId);
        if (moveResult.Success)
        {
            item.Status = ItemStatus.Organized;
            _reviewQueue.TryRemove(reviewItemId, out _);

            // If user customized category, record learning
            if (customCategory != null && !customCategory.Equals(item.SuggestedCategory, StringComparison.OrdinalIgnoreCase))
            {
                _userMemory.RecordCorrection(item.Extension, targetCategory);
                if (item.Keywords.Count > 0)
                {
                    _userMemory.RecordCorrection(item.Keywords[0], targetCategory);
                }
            }
            else
            {
                // Reinforce approved preference
                _userMemory.RecordCorrection(item.Extension, targetCategory);
            }

            if (item.Metadata != null)
            {
                await _searchIndex.IndexFileAsync(item.Metadata with { FilePath = moveResult.FinalDestinationPath }, targetCategory);
            }

            ReviewQueueChanged?.Invoke(this, EventArgs.Empty);
            StatusChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        return false;
    }

    public void RejectItem(string reviewItemId)
    {
        if (_reviewQueue.TryRemove(reviewItemId, out var item))
        {
            item.Status = ItemStatus.Rejected;
            _logger.Info("OrganizerEngine", $"Item rejected from review queue: {item.FileName}");
            ReviewQueueChanged?.Invoke(this, EventArgs.Empty);
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task<int> ApproveAllPendingAsync()
    {
        var pending = GetReviewQueue();
        var approvedCount = 0;

        foreach (var item in pending)
        {
            if (await ApproveItemAsync(item.Id))
            {
                approvedCount++;
            }
        }

        return approvedCount;
    }

    public (int TotalMoved, int ActiveOrganized, int TotalUndone) GetOperationCounts() => _historyDb.GetCounts();
    public int GetIndexedFilesCount() => _searchIndex.GetIndexedCount();

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _watcher.FileReadyToProcess -= OnFileDiscovered;
        _watcher.Dispose();
        _searchIndex.Dispose();
        _historyDb.Dispose();
        _userMemory.Dispose();
    }
}
