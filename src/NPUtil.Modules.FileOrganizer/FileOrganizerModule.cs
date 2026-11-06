using NPUtil.Core.Hardware;
using NPUtil.Core.Logging;
using NPUtil.Core.Modules;
using NPUtil.Core.Services;
using NPUtil.Core.Settings;
using NPUtil.Modules.FileOrganizer.Classification;
using NPUtil.Modules.FileOrganizer.Core;
using NPUtil.Modules.FileOrganizer.Extraction;
using NPUtil.Modules.FileOrganizer.FileOperations;
using NPUtil.Modules.FileOrganizer.Monitoring;
using NPUtil.Modules.FileOrganizer.Search;

namespace NPUtil.Modules.FileOrganizer;

public class FileOrganizerModule : IModule
{
    public string Id => "file-organizer";
    public string Name => "File Organizer";
    public string Description => "Intelligent local AI file organizer with semantic taxonomy and safety undo.";
    public string IconGlyph => "\uE8B7"; // Folder icon in Segoe Fluent Icons / Segoe MDL2
    public int SortOrder => 1;
    public bool IsEnabled { get; set; } = true;

    public OrganizerEngine? Engine { get; private set; }
    public TaxonomyManager? Taxonomy { get; private set; }
    public UndoManager? Undo { get; private set; }
    public FileSearchIndex? SearchIndex { get; private set; }
    public ModelManager? ModelMgr { get; private set; }
    public IEmbeddingEngine? EmbeddingEng { get; private set; }
    public OperationHistoryDb? HistoryDb { get; private set; }

    private Func<object>? _viewFactory;

    public void RegisterViewFactory(Func<object> viewFactory)
    {
        _viewFactory = viewFactory;
    }

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var logger = (ILogger?)services.GetService(typeof(ILogger)) ?? new AppLogger();
        var hardware = (IHardwareService?)services.GetService(typeof(IHardwareService)) ?? new HardwareService();
        var notifications = (INotificationService?)services.GetService(typeof(INotificationService)) ?? new NotificationService();
        var settings = (SettingsManager?)services.GetService(typeof(SettingsManager)) ?? new SettingsManager();

        ModelMgr = new ModelManager(logger);
        var qnnDetector = new QnnDetector(logger);
        EmbeddingEng = new EmbeddingEngine(ModelMgr, qnnDetector, hardware, logger);

        var cacheDb = new EmbeddingCacheDb(logger);
        var ruleClassifier = new RuleClassifier();
        var userMemory = new UserCorrectionMemory(logger);
        Taxonomy = new TaxonomyManager();

        var classifier = new HybridClassifier(EmbeddingEng, cacheDb, ruleClassifier, userMemory, Taxonomy, logger);
        var extractor = new FileContentExtractor();

        HistoryDb = new OperationHistoryDb(logger);
        var mover = new SafeFileMover(HistoryDb, logger);
        Undo = new UndoManager(HistoryDb, logger);
        SearchIndex = new FileSearchIndex(EmbeddingEng, logger);
        var watcher = new DownloadsWatcher(logger);

        Engine = new OrganizerEngine(
            extractor,
            classifier,
            mover,
            Undo,
            HistoryDb,
            SearchIndex,
            watcher,
            Taxonomy,
            userMemory,
            hardware,
            notifications,
            settings,
            logger
        );

        logger.Info("FileOrganizerModule", "File Organizer module initialized successfully.");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        Engine?.Dispose();
        (EmbeddingEng as IDisposable)?.Dispose();
        return Task.CompletedTask;
    }

    public object CreateView()
    {
        if (_viewFactory != null)
        {
            return _viewFactory();
        }
        return "File Organizer View";
    }
}
