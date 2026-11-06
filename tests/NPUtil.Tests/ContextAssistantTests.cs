using NPUtil.Core.AI;
using NPUtil.Core.Context;
using NPUtil.Core.Extraction;
using NPUtil.Core.Hardware;
using NPUtil.Core.Translation;
using NPUtil.Modules.ContextAssistant.Core;

namespace NPUtil.Tests;

public class ContextAssistantTests
{
    private readonly ContextAssistantEngine _engine;
    private readonly ContextAssistantHistoryDb _historyDb;

    public ContextAssistantTests()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "NPUtil_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "test_assistant.db");

        var hardware = new HardwareService();
        var modelManager = new ModelManager(hardware);
        var inference = new LocalInferenceEngine(modelManager);
        var translation = new LocalTranslationService();
        var extractor = new DocumentExtractor();
        _historyDb = new ContextAssistantHistoryDb(dbPath);
        var capture = new ContextCaptureService();

        _engine = new ContextAssistantEngine(capture, inference, translation, extractor, _historyDb);
    }

    [Fact]
    public async Task InferenceEngine_SummarizesTextCorrectly()
    {
        var text = @"The Snapdragon X Elite is a system on a chip designed by Qualcomm for Windows on ARM laptops. 
It features a 12-core Oryon CPU running up to 3.8 GHz. The integrated Hexagon NPU delivers up to 45 TOPS of AI performance. 
It supports multiple external 4K monitors and high bandwidth LPDDR5X memory. Battery life on Qualcomm laptops is significantly enhanced compared to traditional x86 architecture.";

        var res = await _engine.ExecuteActionAsync(ContextActionType.Summarize, text);

        Assert.True(res.Success);
        Assert.NotEmpty(res.OutputText);
        Assert.Contains("Summary:", res.OutputText);
    }

    [Fact]
    public async Task InferenceEngine_ExplainsCodeCorrectly()
    {
        var code = @"public class OrderProcessor {
    public decimal CalculateTotal(List<OrderItem> items) {
        return items.Sum(i => i.Price * i.Quantity);
    }
}";

        var res = await _engine.ExecuteActionAsync(ContextActionType.Explain, code, activeApp: "code");

        Assert.True(res.Success);
        Assert.Contains("C# / .NET", res.OutputText);
        Assert.Contains("CalculateTotal", res.OutputText);
    }

    [Fact]
    public async Task InferenceEngine_ExtractsKeyPointsCorrectly()
    {
        var text = @"* First milestone completed on time.
* Budget allocated for Q4 is $50,000.
* Team meeting scheduled for Monday at 10:00 AM.
* Deployment to production requires VP sign-off.";

        var res = await _engine.ExecuteActionAsync(ContextActionType.ExtractPoints, text);

        Assert.True(res.Success);
        Assert.Contains("First milestone completed", res.OutputText);
        Assert.Contains("Budget allocated", res.OutputText);
    }

    [Fact]
    public void ContextAssistant_HistoryDbPersistsRecords()
    {
        _historyDb.AddEntry("VSCode", "Explain function", "def foo(): pass", "Python function definition");
        var entries = _historyDb.GetRecentEntries(10);

        Assert.NotEmpty(entries);
        Assert.Contains(entries, e => e.ActiveApp == "VSCode" && e.UserPrompt == "Explain function");

        _historyDb.ClearHistory();
        var empty = _historyDb.GetRecentEntries(10);
        Assert.Empty(empty);
    }

    [Fact]
    public async Task InferenceEngine_AnswersWhatCanYouSeeOnExceptionLog()
    {
        var exceptionLog = @"[2026-11-06 16:21:32] [DispatcherUnhandledException] System.NullReferenceException: Object reference not set to an instance of an object.
   at NPUtil.Modules.UniversalOcr.UI.UniversalOcrMainView.TxtOriginalText_TextChanged(Object sender, TextChangedEventArgs e) in C:\Users\Asus\Downloads\NPUtil\src\NPUtil.Modules.UniversalOcr\UI\UniversalOcrMainView.xaml.cs:line 155
   at System.Windows.RoutedEventArgs.InvokeHandler(Delegate handler, Object target)";

        var res = await _engine.ExecuteActionAsync(ContextActionType.Custom, exceptionLog, customInstruction: "what can you see");

        Assert.True(res.Success);
        Assert.Contains("NullReferenceException", res.OutputText);
        Assert.Contains("UniversalOcrMainView.xaml.cs", res.OutputText);
        Assert.Contains("TxtOriginalText_TextChanged", res.OutputText);
        Assert.Contains("155", res.OutputText);
    }

    [Fact]
    public async Task InferenceEngine_AnswersHowToFixOnExceptionLog()
    {
        var exceptionLog = @"System.NullReferenceException: Object reference not set to an instance of an object.
   at NPUtil.Modules.UniversalOcr.UI.UniversalOcrMainView.TxtOriginalText_TextChanged(Object sender, TextChangedEventArgs e) in C:\Users\Asus\Downloads\NPUtil\src\NPUtil.Modules.UniversalOcr\UI\UniversalOcrMainView.xaml.cs:line 155";

        var res = await _engine.ExecuteActionAsync(ContextActionType.Custom, exceptionLog, customInstruction: "how to fix this");

        Assert.True(res.Success);
        Assert.Contains("Resolution & Fix", res.OutputText);
        Assert.Contains("null-check", res.OutputText);
    }

    [Fact]
    public void SettingsManager_PersistsLensHotkey()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"settings_{Guid.NewGuid():N}.json");
        var sm = new NPUtil.Core.Settings.SettingsManager(tempFile);

        Assert.Equal("Ctrl+Alt+L", sm.Current.LensHotkey);

        sm.Update(s => s.LensHotkey = "Ctrl+Shift+S");
        Assert.Equal("Ctrl+Shift+S", sm.Current.LensHotkey);

        var sm2 = new NPUtil.Core.Settings.SettingsManager(tempFile);
        Assert.Equal("Ctrl+Shift+S", sm2.Current.LensHotkey);
    }
}
