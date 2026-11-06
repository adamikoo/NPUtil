using NPUtil.Core.Hardware;
using NPUtil.Core.Logging;
using NPUtil.Modules.FileOrganizer.Classification;
using NPUtil.Modules.FileOrganizer.Core;
using NPUtil.Modules.FileOrganizer.Extraction;
using Xunit;

namespace NPUtil.Tests;

public class ClassificationTests : IDisposable
{
    private readonly string _sandboxDir;
    private readonly HybridClassifier _classifier;
    private readonly FileContentExtractor _extractor;
    private readonly TaxonomyManager _taxonomy;
    private readonly UserCorrectionMemory _userMemory;

    public ClassificationTests()
    {
        _sandboxDir = SandboxHelper.CreateTestSandbox("Classification");
        var logger = new AppLogger();
        var hardware = new HardwareService();
        var modelMgr = new ModelManager(logger, Path.Combine(_sandboxDir, "models"));
        var qnn = new QnnDetector(logger);
        var engine = new EmbeddingEngine(modelMgr, qnn, hardware, logger);
        var cacheDb = new EmbeddingCacheDb(logger, Path.Combine(_sandboxDir, "cache.db"));
        var rules = new RuleClassifier();
        _userMemory = new UserCorrectionMemory(logger, Path.Combine(_sandboxDir, "user_pref.db"));

        _taxonomy = new TaxonomyManager(Path.Combine(_sandboxDir, "taxonomy.json"));
        _classifier = new HybridClassifier(engine, cacheDb, rules, _userMemory, _taxonomy, logger);
        _extractor = new FileContentExtractor();
    }

    [Theory]
    [InlineData("Cambridge_C1_Advanced_Exam_Preparation.docx", "Cambridge English Language Assessment CAE exam handbook with practice tests.", "Education\\Languages\\English")]
    [InlineData("Faktura_2026_015_Webhosting.docx", "Faktura daňový doklad č. 2026/015 za webhosting a doménu. Celkem k úhradě 1500 Kč.", "Finance\\Invoices\\2026")]
    [InlineData("trading_strategy_mt5_eurusd.txt", "MetaTrader 5 algorithmic trading strategy indicator and backtest results for EURUSD.", "Finance\\Trading\\Strategies")]
    [InlineData("porsche_911_gt3_track.jpg", "Automobile supercar photo of Porsche 911 GT3 RS on race track.", "Media\\Images\\Cars")]
    [InlineData("vscode_setup_arm64.exe", "Visual Studio Code installer executable setup program for Windows 11 ARM64.", "Software\\Installers")]
    [InlineData("matematika_prednaska_linear_algebra.txt", "Matematika přednáška lineární algebra vektory matice determinanty.", "Education\\Mathematics")]
    public async Task ClassifyAsync_RealisticDocuments_AchievesCorrectDestinations(string fileName, string content, string expectedPrefix)
    {
        var file = Path.Combine(_sandboxDir, fileName);
        if (fileName.EndsWith(".docx"))
        {
            SandboxHelper.CreateMockDocx(_sandboxDir, fileName, content);
        }
        else
        {
            SandboxHelper.CreateTextFile(_sandboxDir, fileName, content);
        }

        var metadata = await _extractor.ExtractAsync(file);
        var result = await _classifier.ClassifyAsync(metadata);

        Assert.True(result.Confidence >= 0.65, $"Expected confidence >= 0.65, but got {result.Confidence:P0}");
        Assert.StartsWith(expectedPrefix, result.TargetRelativePath, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(result.Explanation));
    }

    [Fact]
    public async Task ClassifyAsync_ZpravodajDocument_ClassifiesIntoWorkNewsletters_NotFinance()
    {
        var file = SandboxHelper.CreateTextFile(_sandboxDir, "Zpravodaj_leden_2026.pdf",
            "Městský zpravodaj číslo 1/2026. Informace pro občany a zaměstnance, zprávy z jednání zastupitelstva a kulturní přehled.");

        var meta = await _extractor.ExtractAsync(file);
        var result = await _classifier.ClassifyAsync(meta);

        Assert.Equal(@"Work\Newsletters", result.TargetRelativePath);
        Assert.DoesNotContain("Finance", result.TargetRelativePath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Invoices", result.TargetRelativePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClassifyAsync_AmbiguousFile_DynamicallyCreatesNewCategory()
    {
        var file = SandboxHelper.CreateTextFile(_sandboxDir, "astronomy_telescope_observations.txt",
            "Astronomical observations of nebula stars galaxies redshift astrophysicist celestial catalog.");

        var meta = await _extractor.ExtractAsync(file);
        var result = await _classifier.ClassifyAsync(meta);

        Assert.Equal("DynamicCategory", result.ClassifierSource);
        Assert.False(string.IsNullOrWhiteSpace(result.TargetRelativePath));
        Assert.Contains("dynamicky vytvořena", result.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClassifyAsync_LearnsFromUserCorrection()
    {
        _userMemory.RecordCorrection(".specialdoc", @"Work\Projects");

        var file = SandboxHelper.CreateTextFile(_sandboxDir, "unusual_project.specialdoc", "Arbitrary content");
        var meta = await _extractor.ExtractAsync(file);

        var result = await _classifier.ClassifyAsync(meta);

        Assert.Equal(@"Work\Projects", result.TargetRelativePath);
        Assert.Equal("UserPreference", result.ClassifierSource);
    }

    public void Dispose()
    {
        _userMemory.Dispose();
        SandboxHelper.CleanUpSandbox(_sandboxDir);
    }
}
