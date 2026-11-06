using NPUtil.Core.AI;
using NPUtil.Core.Extraction;
using NPUtil.Core.Hardware;
using NPUtil.Core.Logging;
using NPUtil.Core.Modules;
using NPUtil.Modules.SmartDataExtractor.Engine;
using NPUtil.Modules.SmartDataExtractor.UI;

namespace NPUtil.Modules.SmartDataExtractor;

public class SmartDataExtractorModule : IModule
{
    public string Id => "SmartDataExtractor";
    public string Name => "Smart Data Extractor";
    public string Description => "Extract structured tables, invoices, records and JSON from documents using local AI.";
    public string IconGlyph => "DocumentTable24";
    public int SortOrder => 3;
    public bool IsEnabled { get; set; } = true;

    private SmartDataExtractorEngine? _engine;
    private SmartDataExtractorMainView? _view;

    public SmartDataExtractorEngine? Engine => _engine;

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var logger = services.GetService(typeof(ILogger)) as ILogger;
        var hardware = services.GetService(typeof(IHardwareService)) as IHardwareService ?? new HardwareService();

        var documentExtractor = services.GetService(typeof(IDocumentExtractor)) as IDocumentExtractor ?? new DocumentExtractor();
        var modelManager = services.GetService(typeof(IModelManager)) as IModelManager ?? new ModelManager(hardware, logger);
        var inferenceEngine = services.GetService(typeof(IInferenceEngine)) as IInferenceEngine ?? new LocalInferenceEngine(modelManager, logger);

        _engine = new SmartDataExtractorEngine(
            documentExtractor,
            inferenceEngine,
            logger);

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public object CreateView()
    {
        if (_engine == null)
        {
            throw new InvalidOperationException("Module has not been initialized.");
        }

        _view ??= new SmartDataExtractorMainView(_engine);
        return _view;
    }
}
