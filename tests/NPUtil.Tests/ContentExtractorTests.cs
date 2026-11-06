using NPUtil.Modules.FileOrganizer.Extraction;
using Xunit;

namespace NPUtil.Tests;

public class ContentExtractorTests : IDisposable
{
    private readonly string _sandboxDir;
    private readonly FileContentExtractor _extractor;

    public ContentExtractorTests()
    {
        _sandboxDir = SandboxHelper.CreateTestSandbox("Extraction");
        _extractor = new FileContentExtractor();
    }

    [Fact]
    public async Task ExtractAsync_PlainText_ExtractsCleanTextAndKeywords()
    {
        var content = "Faktura daňový doklad č. 2026-001 za webové služby a programování webu. Dodavatel Jan Novák.";
        var file = SandboxHelper.CreateTextFile(_sandboxDir, "faktura_2026_001.txt", content);

        var meta = await _extractor.ExtractAsync(file);

        Assert.Equal("faktura_2026_001.txt", meta.FileName);
        Assert.Equal(".txt", meta.Extension);
        Assert.Contains("Faktura daňový doklad", meta.ExtractedText);
        Assert.Contains(meta.Keywords, k => k.Equals("faktura", StringComparison.OrdinalIgnoreCase) || k.Equals("doklad", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExtractAsync_Docx_ExtractsDocumentXmlContent()
    {
        var body = "Cambridge C1 Advanced English examination preparation guide and vocabulary exercises.";
        var file = SandboxHelper.CreateMockDocx(_sandboxDir, "cambridge_c1_prep.docx", body);

        var meta = await _extractor.ExtractAsync(file);

        Assert.Equal(".docx", meta.Extension);
        Assert.Contains("Cambridge C1 Advanced", meta.ExtractedText);
        Assert.Contains(meta.Keywords, k => k.Equals("cambridge", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExtractAsync_Xlsx_ExtractsSharedStrings()
    {
        var text = "Bankovní výpis účtu AirBank transakce leden 2026";
        var file = SandboxHelper.CreateMockXlsx(_sandboxDir, "vypis_airbank_2026.xlsx", text);

        var meta = await _extractor.ExtractAsync(file);

        Assert.Equal(".xlsx", meta.Extension);
        Assert.Contains("AirBank", meta.ExtractedText);
    }

    public void Dispose()
    {
        SandboxHelper.CleanUpSandbox(_sandboxDir);
    }
}
