using NPUtil.Core.Hardware;
using NPUtil.Core.Logging;
using NPUtil.Core.Services;
using NPUtil.Core.Settings;
using NPUtil.Modules.FileOrganizer.Classification;
using NPUtil.Modules.FileOrganizer.Core;
using NPUtil.Modules.FileOrganizer.Extraction;
using NPUtil.Modules.FileOrganizer.FileOperations;
using NPUtil.Modules.FileOrganizer.Models;
using NPUtil.Modules.FileOrganizer.Monitoring;
using NPUtil.Modules.FileOrganizer.Search;
using Xunit;

namespace NPUtil.Tests;

public class OrganizerEngineIntegrationTests : IDisposable
{
    private readonly string _sandboxDir;
    private readonly string _watchedFolder;
    private readonly OrganizerEngine _engine;
    private readonly TaxonomyManager _taxonomy;

    public OrganizerEngineIntegrationTests()
    {
        _sandboxDir = SandboxHelper.CreateTestSandbox("EngineIntegration");
        _watchedFolder = Path.Combine(_sandboxDir, "Downloads");
        Directory.CreateDirectory(_watchedFolder);

        var logger = new AppLogger();
        var hardware = new HardwareService();
        var notifications = new NotificationService();
        var settings = new SettingsManager(Path.Combine(_sandboxDir, "settings.json"));

        var modelMgr = new ModelManager(logger, Path.Combine(_sandboxDir, "models"));
        var qnn = new QnnDetector(logger);
        var embeddingEngine = new EmbeddingEngine(modelMgr, qnn, hardware, logger);
        var cacheDb = new EmbeddingCacheDb(logger, Path.Combine(_sandboxDir, "cache.db"));
        var rules = new RuleClassifier();
        var userMemory = new UserCorrectionMemory(logger, Path.Combine(_sandboxDir, "user_pref.db"));
        _taxonomy = new TaxonomyManager(Path.Combine(_sandboxDir, "taxonomy.json"));
        var classifier = new HybridClassifier(embeddingEngine, cacheDb, rules, userMemory, _taxonomy, logger);
        var extractor = new FileContentExtractor();
        var historyDb = new OperationHistoryDb(logger, Path.Combine(_sandboxDir, "history.db"));
        var mover = new SafeFileMover(historyDb, logger);
        var undo = new UndoManager(historyDb, logger);
        var searchIndex = new FileSearchIndex(embeddingEngine, logger, Path.Combine(_sandboxDir, "search.db"));
        var watcher = new DownloadsWatcher(logger);

        _engine = new OrganizerEngine(
            extractor,
            classifier,
            mover,
            undo,
            historyDb,
            searchIndex,
            watcher,
            _taxonomy,
            userMemory,
            hardware,
            notifications,
            settings,
            logger
        );

        _engine.StartMonitoring(_watchedFolder);
    }

    [Fact]
    public async Task ReconciliationScan_ReviewEverythingMode_PopulatesReviewQueue()
    {
        _engine.Mode = OrganizerMode.ReviewEverything;

        // Create test files in watched Downloads folder
        SandboxHelper.CreateTextFile(_watchedFolder, "Faktura_2026_099.txt", "Faktura daňový doklad č. 2026/099 celkem 5000 Kč");
        SandboxHelper.CreateMockDocx(_watchedFolder, "Cambridge_English_Exam.docx", "Cambridge C1 Advanced English sample tests.");

        var count = await _engine.RunReconciliationScanAsync();
        Assert.Equal(2, count);

        var queue = _engine.GetReviewQueue();
        Assert.Equal(2, queue.Count);

        // Files should NOT be moved yet in ReviewEverything mode
        Assert.True(File.Exists(Path.Combine(_watchedFolder, "Faktura_2026_099.txt")));
        Assert.True(File.Exists(Path.Combine(_watchedFolder, "Cambridge_English_Exam.docx")));

        // Approve all
        var approved = await _engine.ApproveAllPendingAsync();
        Assert.Equal(2, approved);

        // Review queue should now be empty
        Assert.Empty(_engine.GetReviewQueue());

        // Files should now be moved into respective categories
        Assert.True(File.Exists(Path.Combine(_watchedFolder, @"Finance\Invoices\2026", "Faktura_2026_099.txt")));
        Assert.True(File.Exists(Path.Combine(_watchedFolder, @"Education\Languages\English", "Cambridge_English_Exam.docx")));
    }

    [Fact]
    public async Task ReconciliationScan_IntelligentAutoMode_MovesHighConfidenceFilesImmediately()
    {
        _engine.Mode = OrganizerMode.IntelligentAuto;
        _engine.HighConfidenceThreshold = 0.70;

        SandboxHelper.CreateTextFile(_watchedFolder, "trading_mt5_strategy.txt", "MetaTrader 5 Expert Advisor EA trading strategy backtest.");

        await _engine.RunReconciliationScanAsync();

        // High confidence file should be auto-organized immediately
        Assert.Empty(_engine.GetReviewQueue());
        Assert.True(File.Exists(Path.Combine(_watchedFolder, @"Finance\Trading\Strategies", "trading_mt5_strategy.txt")));
    }

    public void Dispose()
    {
        _engine.Dispose();
        SandboxHelper.CleanUpSandbox(_sandboxDir);
    }
}
