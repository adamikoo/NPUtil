using NPUtil.Core.Logging;
using NPUtil.Core.Modules;
using NPUtil.Core.OCR;
using NPUtil.Core.Translation;
using NPUtil.Modules.UniversalOcr.Core;
using NPUtil.Modules.UniversalOcr.UI;

namespace NPUtil.Modules.UniversalOcr;

public class UniversalOcrModule : IModule
{
    public string Id => "UniversalOcr";
    public string Name => "Universal OCR & Translator";
    public string Description => "Extract text from screen regions and images with instant bilingual translation.";
    public string IconGlyph => "Scan24";
    public int SortOrder => 4;
    public bool IsEnabled { get; set; } = true;

    private UniversalOcrEngine? _engine;
    private UniversalOcrMainView? _view;

    public UniversalOcrEngine? Engine => _engine;

    public Action<string>? OnSendToAssistant { get; set; }
    public Action<string>? OnSendToExtractor { get; set; }

    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var logger = services.GetService(typeof(ILogger)) as ILogger;
        var ocrService = services.GetService(typeof(IOcrService)) as IOcrService ?? new WindowsOcrService(logger);
        var translationService = services.GetService(typeof(ITranslationService)) as ITranslationService ?? new LocalTranslationService(logger);

        _engine = new UniversalOcrEngine(
            ocrService,
            translationService,
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

        _view ??= new UniversalOcrMainView(_engine, OnSendToAssistant, OnSendToExtractor);
        return _view;
    }
}
