using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using NPUtil.Core.Logging;
using NPUtil.Modules.FileOrganizer.FileOperations;

namespace NPUtil.Modules.FileOrganizer.Monitoring;

public class FileDiscoveredEventArgs : EventArgs
{
    public string FilePath { get; }
    public FileDiscoveredEventArgs(string filePath) => FilePath = filePath;
}

public class DownloadsWatcher : IDisposable
{
    private static readonly string[] IncompleteExtensions = [".crdownload", ".part", ".tmp", ".download", ".partial"];
    private static readonly Guid DownloadsFolderId = new("374DE290-123F-4565-9164-39C4925E467B");

    private readonly ILogger _logger;
    private FileSystemWatcher? _watcher;
    private readonly ConcurrentDictionary<string, byte> _processingFiles = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private bool _isDisposed;

    public event EventHandler<FileDiscoveredEventArgs>? FileReadyToProcess;
    public string WatchedDirectory { get; private set; } = string.Empty;
    public bool IsMonitoring { get; private set; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
    private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr pszPath);

    public DownloadsWatcher(ILogger logger)
    {
        _logger = logger;
        WatchedDirectory = ResolveWindowsDownloadsPath();
    }

    public static string ResolveWindowsDownloadsPath()
    {
        try
        {
            var hr = SHGetKnownFolderPath(DownloadsFolderId, 0, IntPtr.Zero, out var pPath);
            if (hr == 0 && pPath != IntPtr.Zero)
            {
                var path = Marshal.PtrToStringUni(pPath);
                Marshal.FreeCoTaskMem(pPath);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    return path;
                }
            }
        }
        catch { /* fallback */ }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var fallback = Path.Combine(userProfile, "Downloads");
        if (Directory.Exists(fallback)) return fallback;

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    public void Start(string? customFolder = null)
    {
        Stop();

        WatchedDirectory = !string.IsNullOrWhiteSpace(customFolder) && Directory.Exists(customFolder)
            ? customFolder
            : ResolveWindowsDownloadsPath();

        _cts = new CancellationTokenSource();

        try
        {
            _watcher = new FileSystemWatcher(WatchedDirectory)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                IncludeSubdirectories = false, // Critical: Avoid infinite sorting loop with organized subfolders!
                EnableRaisingEvents = true
            };

            _watcher.Created += OnFileEvent;
            _watcher.Changed += OnFileEvent;
            _watcher.Renamed += OnFileRenamed;

            IsMonitoring = true;
            _logger.Info("DownloadsWatcher", $"Started monitoring: {WatchedDirectory}");
        }
        catch (Exception ex)
        {
            _logger.Error("DownloadsWatcher", $"Failed to start watching directory: {WatchedDirectory}", ex);
            IsMonitoring = false;
        }
    }

    public void Stop()
    {
        IsMonitoring = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnFileEvent;
            _watcher.Changed -= OnFileEvent;
            _watcher.Renamed -= OnFileRenamed;
            _watcher.Dispose();
            _watcher = null;
        }

        _logger.Info("DownloadsWatcher", "Stopped monitoring.");
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType is WatcherChangeTypes.Created or WatcherChangeTypes.Changed)
        {
            QueueForStabilityCheck(e.FullPath);
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        QueueForStabilityCheck(e.FullPath);
    }

    public void QueueForStabilityCheck(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        // Skip incomplete browser downloads and temp files
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var fileName = Path.GetFileName(filePath);
        if (IncompleteExtensions.Contains(ext) || fileName.StartsWith("~$") || fileName.StartsWith(".~"))
        {
            return;
        }

        if (!_processingFiles.TryAdd(filePath, 0))
        {
            return; // Already being verified
        }

        var token = _cts?.Token ?? CancellationToken.None;
        Task.Run(async () =>
        {
            try
            {
                if (await WaitForFileStabilityAsync(filePath, token))
                {
                    _logger.Info("DownloadsWatcher", $"File stabilized and ready: {Path.GetFileName(filePath)}");
                    FileReadyToProcess?.Invoke(this, new FileDiscoveredEventArgs(filePath));
                }
            }
            finally
            {
                _processingFiles.TryRemove(filePath, out _);
            }
        }, token);
    }

    private async Task<bool> WaitForFileStabilityAsync(string filePath, CancellationToken ct)
    {
        var attempts = 0;
        const int maxAttempts = 30; // Max 30 seconds
        long lastSize = -1;

        while (attempts < maxAttempts && !ct.IsCancellationRequested)
        {
            if (!File.Exists(filePath)) return false;

            try
            {
                var fi = new FileInfo(filePath);
                var currentSize = fi.Length;

                // File must have size > 0 and size must be unchanged across two checks
                if (currentSize > 0 && currentSize == lastSize)
                {
                    // Check if file is unlocked and can be read
                    if (SafeFileMover.IsFileReady(filePath))
                    {
                        return true;
                    }
                }

                lastSize = currentSize;
            }
            catch
            {
                // File locked by writer
            }

            attempts++;
            await Task.Delay(1000, ct);
        }

        return false;
    }

    /// <summary>
    /// Performs an explicit reconciliation scan of the top-level watched folder.
    /// Finds any files not currently inside subdirectories.
    /// </summary>
    public IReadOnlyList<string> ScanTopLevelFiles()
    {
        if (!Directory.Exists(WatchedDirectory)) return Array.Empty<string>();

        try
        {
            var files = Directory.GetFiles(WatchedDirectory, "*.*", SearchOption.TopDirectoryOnly);
            return files.Where(f =>
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                var name = Path.GetFileName(f);
                return !IncompleteExtensions.Contains(ext) &&
                       !name.StartsWith("~$") &&
                       !name.StartsWith(".~") &&
                       !File.GetAttributes(f).HasFlag(FileAttributes.Hidden) &&
                       !File.GetAttributes(f).HasFlag(FileAttributes.System);
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.Error("DownloadsWatcher", "Error during reconciliation scan", ex);
            return Array.Empty<string>();
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
    }
}
