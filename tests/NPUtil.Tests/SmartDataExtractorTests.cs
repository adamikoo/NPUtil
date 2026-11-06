using System.Text;
using NPUtil.Core.AI;
using NPUtil.Core.Extraction;
using NPUtil.Core.Hardware;
using NPUtil.Modules.SmartDataExtractor.Engine;
using NPUtil.Modules.SmartDataExtractor.Export;
using NPUtil.Modules.SmartDataExtractor.Models;

namespace NPUtil.Tests;

public class SmartDataExtractorTests
{
    private readonly SmartDataExtractorEngine _engine;
    private readonly string _tempDir;

    public SmartDataExtractorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "NPUtil_ExtractorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var hardware = new HardwareService();
        var modelManager = new ModelManager(hardware);
        var inference = new LocalInferenceEngine(modelManager);
        var extractor = new DocumentExtractor();

        _engine = new SmartDataExtractorEngine(extractor, inference);
    }

    [Fact]
    public async Task ExtractCsv_ExtractsTableHeadersAndRowsCorrectly()
    {
        var csvPath = Path.Combine(_tempDir, "sample.csv");
        File.WriteAllText(csvPath, "Product,Quantity,Price\nLaptop,5,25000\nMouse,20,500\nKeyboard,10,1200", Encoding.UTF8);

        var res = await _engine.ExtractAsync(csvPath, ExtractionCategory.TablesAndData);

        Assert.True(res.Success);
        Assert.Equal(3, res.TableHeaders.Count);
        Assert.Equal("Product", res.TableHeaders[0]);
        Assert.Equal(3, res.TableRows.Count);
        Assert.Equal("Laptop", res.TableRows[0][0]);
    }

    [Fact]
    public async Task ExtractInvoice_IdentifiesVendorAndTotal()
    {
        var invoicePath = Path.Combine(_tempDir, "invoice.txt");
        var text = @"Faktura č. 2026001
Dodavatel: TechCorp Solutions s.r.o.
Odběratel: Jan Novák
Datum vystavení: 15.03.2026
Datum splatnosti: 29.03.2026
Celkem k úhradě: 14 500,00 Kč
Položky:
Konzultace IT 10 1450 Kč";

        File.WriteAllText(invoicePath, text, Encoding.UTF8);

        var res = await _engine.ExtractAsync(invoicePath, ExtractionCategory.InvoicesAndReceipts);

        Assert.True(res.Success);
        Assert.NotNull(res.Invoice);
        Assert.Contains("TechCorp", res.Invoice.Vendor);
        Assert.Equal("2026001", res.Invoice.InvoiceNumber);
        Assert.Contains("14 500,00", res.Invoice.TotalAmount);
    }

    [Fact]
    public async Task ExtractDates_FindsDatesAndDeadlines()
    {
        var docPath = Path.Combine(_tempDir, "contract.txt");
        var text = @"Smlouva nabývá účinnosti dne 01.01.2026.
Termín dokončení projektu (deadline) je stanoven na 30.06.2026.
Závěrečná faktura se splatností do 15.07.2026.";

        File.WriteAllText(docPath, text, Encoding.UTF8);

        var res = await _engine.ExtractAsync(docPath, ExtractionCategory.DatesAndDeadlines);

        Assert.True(res.Success);
        Assert.True(res.DateRecords.Count >= 2);
        Assert.Contains(res.DateRecords, d => d.DateText == "30.06.2026" && d.RecordType == "Deadline");
    }

    [Fact]
    public async Task DataExporter_ExportsToCsvAndJsonSuccessfully()
    {
        var docPath = Path.Combine(_tempDir, "data.txt");
        File.WriteAllText(docPath, "Header1,Header2\nVal1,Val2\nVal3,Val4", Encoding.UTF8);

        var res = await _engine.ExtractAsync(docPath, ExtractionCategory.TablesAndData);

        var exportCsv = Path.Combine(_tempDir, "export.csv");
        var exportJson = Path.Combine(_tempDir, "export.json");
        var exportXlsx = Path.Combine(_tempDir, "export.xlsx");
        var exportDocx = Path.Combine(_tempDir, "export.docx");

        DataExporter.ExportToCsv(res, exportCsv);
        DataExporter.ExportToJson(res, exportJson);
        DataExporter.ExportToXlsx(res, exportXlsx);
        DataExporter.ExportToDocx(res, exportDocx);

        Assert.True(File.Exists(exportCsv));
        Assert.True(File.Exists(exportJson));
        Assert.True(File.Exists(exportXlsx));
        Assert.True(File.Exists(exportDocx));
        Assert.True(new FileInfo(exportXlsx).Length > 100);
        Assert.True(new FileInfo(exportDocx).Length > 100);
    }
}
