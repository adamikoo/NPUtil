using NPUtil.Core.Hardware;
using NPUtil.Core.Logging;
using NPUtil.Modules.FileOrganizer.Classification;
using NPUtil.Modules.FileOrganizer.Models;
using NPUtil.Modules.FileOrganizer.Search;
using Xunit;

namespace NPUtil.Tests;

public class SearchAndCrashRecoveryTests : IDisposable
{
    private readonly string _sandboxDir;
    private readonly FileSearchIndex _searchIndex;
    private readonly IEmbeddingEngine _embeddingEngine;
    private readonly ILogger _logger;

    public SearchAndCrashRecoveryTests()
    {
        _sandboxDir = SandboxHelper.CreateTestSandbox("SearchRecovery");
        _logger = new AppLogger();
        var hardware = new HardwareService();
        var modelMgr = new ModelManager(_logger, Path.Combine(_sandboxDir, "models"));
        var qnn = new QnnDetector(_logger);
        _embeddingEngine = new EmbeddingEngine(modelMgr, qnn, hardware, _logger);
        _searchIndex = new FileSearchIndex(_embeddingEngine, _logger, Path.Combine(_sandboxDir, "search.db"));
    }

    [Fact]
    public async Task SearchAsync_FtsAndSemantic_FindsRelevantDocuments()
    {
        var meta1 = new ExtractedMetadata(
            FilePath: @"C:\Downloads\c1_handbook.pdf",
            FileName: "c1_handbook.pdf",
            Extension: ".pdf",
            FileSizeBytes: 1024,
            CreatedTime: DateTime.Now,
            LastModifiedTime: DateTime.Now,
            MimeType: "application/pdf",
            ExtractedText: "Cambridge Assessment English Certificate in Advanced English examination guidelines for teachers.",
            Keywords: ["cambridge", "english", "advanced", "certificate"],
            AdditionalProperties: new Dictionary<string, string>()
        );

        var meta2 = new ExtractedMetadata(
            FilePath: @"C:\Downloads\invoice_2026.pdf",
            FileName: "invoice_2026.pdf",
            Extension: ".pdf",
            FileSizeBytes: 2048,
            CreatedTime: DateTime.Now,
            LastModifiedTime: DateTime.Now,
            MimeType: "application/pdf",
            ExtractedText: "Faktura za poradenské a daňové služby. Celkem s DPH 12000 CZK.",
            Keywords: ["faktura", "dph", "sluzby"],
            AdditionalProperties: new Dictionary<string, string>()
        );

        await _searchIndex.IndexFileAsync(meta1, @"Education\Languages\English");
        await _searchIndex.IndexFileAsync(meta2, @"Finance\Invoices\2026");

        // Search for concept "Cambridge English"
        var results = await _searchIndex.SearchAsync("Cambridge English");

        Assert.NotEmpty(results);
        var top = results[0];
        Assert.Equal("c1_handbook.pdf", top.FileName);
        Assert.Equal(@"Education\Languages\English", top.Category);
    }

    [Fact]
    public async Task SearchIndex_ReopensCleanlyAcrossRestart()
    {
        var dbPath = Path.Combine(_sandboxDir, "crash_test.db");
        using (var idx1 = new FileSearchIndex(_embeddingEngine, _logger, dbPath))
        {
            var meta = new ExtractedMetadata(
                FilePath: @"C:\Downloads\app_test.cs",
                FileName: "app_test.cs",
                Extension: ".cs",
                FileSizeBytes: 500,
                CreatedTime: DateTime.Now,
                LastModifiedTime: DateTime.Now,
                MimeType: "text/x-csharp",
                ExtractedText: "namespace NPUtil; public class Program { }",
                Keywords: ["nputil", "program"],
                AdditionalProperties: new Dictionary<string, string>()
            );
            await idx1.IndexFileAsync(meta, @"Programming\Source Code");
            Assert.Equal(1, idx1.GetIndexedCount());
        }

        // Re-open without crash or corruption
        using (var idx2 = new FileSearchIndex(_embeddingEngine, _logger, dbPath))
        {
            Assert.Equal(1, idx2.GetIndexedCount());
            var res = await idx2.SearchAsync("NPUtil");
            Assert.Single(res);
            Assert.Equal("app_test.cs", res[0].FileName);
        }
    }

    public void Dispose()
    {
        _searchIndex.Dispose();
        (_embeddingEngine as IDisposable)?.Dispose();
        SandboxHelper.CleanUpSandbox(_sandboxDir);
    }
}
