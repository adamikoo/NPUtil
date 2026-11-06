using NPUtil.Core.AI;
using NPUtil.Core.Context;
using NPUtil.Core.Extraction;
using NPUtil.Core.Hardware;
using NPUtil.Core.Logging;
using NPUtil.Core.Modules;
using NPUtil.Core.Translation;
using NPUtil.Modules.ContextAssistant.Core;
using NPUtil.Modules.ContextAssistant.UI;

namespace NPUtil.Modules.ContextAssistant;

public class ContextAssistantModule : IModule
{
    public string Id => "ContextAssistant";
    public string Name => "Context Assistant";
    public string Description => "Intelligent workflow assistant that acts on selected text, active application and documents.";
    public string IconGlyph => "Bot24";
    public int SortOrder => 2;
    public bool IsEnabled { get; set; } = true;

    private ContextAssistantEngine? _engine;
    private ContextAssistantMainView? _view;

    public ContextAssistantEngine? Engine => _engine;

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var logger = services.GetService(typeof(ILogger)) as ILogger;
        var hardware = services.GetService(typeof(IHardwareService)) as IHardwareService ?? new HardwareService();

        var captureService = services.GetService(typeof(IContextCaptureService)) as IContextCaptureService ?? new ContextCaptureService(logger);
        var modelManager = services.GetService(typeof(IModelManager)) as IModelManager ?? new ModelManager(hardware, logger);
        var inferenceEngine = services.GetService(typeof(IInferenceEngine)) as IInferenceEngine ?? new LocalInferenceEngine(modelManager, logger);
        var translationService = services.GetService(typeof(ITranslationService)) as ITranslationService ?? new LocalTranslationService(logger);
        var documentExtractor = services.GetService(typeof(IDocumentExtractor)) as IDocumentExtractor ?? new DocumentExtractor();
        var historyDb = new ContextAssistantHistoryDb(logger: logger);

        _engine = new ContextAssistantEngine(
            captureService,
            inferenceEngine,
            translationService,
            documentExtractor,
            historyDb,
            logger);

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _engine?.HistoryDb.ClearHistory(); // or keep history
        return Task.CompletedTask;
    }

    public object CreateView()
    {
        if (_engine == null)
        {
            throw new InvalidOperationException("Module has not been initialized.");
        }

        _view ??= new ContextAssistantMainView(_engine);
        return _view;
    }
}
