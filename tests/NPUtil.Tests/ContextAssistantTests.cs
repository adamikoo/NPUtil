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
}
