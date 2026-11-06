using System.Security.Cryptography;
using System.Text;
using NPUtil.Core.Logging;
using NPUtil.Modules.FileOrganizer.FileOperations;
using NPUtil.Modules.FileOrganizer.Models;
using Xunit;

namespace NPUtil.Tests;

public class FileSafetyAndUndoTests : IDisposable
{
    private readonly string _sandboxDir;
    private readonly string _targetBaseDir;
    private readonly OperationHistoryDb _historyDb;
    private readonly SafeFileMover _mover;
    private readonly UndoManager _undoManager;
    private readonly ILogger _logger;

    public FileSafetyAndUndoTests()
    {
        _sandboxDir = SandboxHelper.CreateTestSandbox("Safety");
        _targetBaseDir = Path.Combine(_sandboxDir, "TargetOrganized");
        Directory.CreateDirectory(_targetBaseDir);

        _logger = new AppLogger();
        _historyDb = new OperationHistoryDb(_logger, Path.Combine(_sandboxDir, "history.db"));
        _mover = new SafeFileMover(_historyDb, _logger);
        _undoManager = new UndoManager(_historyDb, _logger);
    }

    [Fact]
    public async Task MoveFileSafely_PreservesFileIntegrityAndHash()
    {
        var content = "Critical document content with special characters: Příliš žluťoučký kůň úpěl ďábelské ódy.";
        var file = SandboxHelper.CreateTextFile(_sandboxDir, "important_contract.txt", content);
        var originalHash = ComputeSha256(file);

        var classification = new ClassificationResult(
            @"Work\Documents", 0.95, 0.95, "Work contract", Array.Empty<string>(), Array.Empty<DestinationSuggestion>(), "Rule");

        var moveResult = await _mover.MoveFileSafelyAsync(file, _targetBaseDir, @"Work\Documents", classification, "Batch-1");

        Assert.True(moveResult.Success);
        Assert.False(File.Exists(file), "Source file should have been moved.");
        Assert.True(File.Exists(moveResult.FinalDestinationPath), "Destination file should exist.");

        var finalHash = ComputeSha256(moveResult.FinalDestinationPath);
        Assert.Equal(originalHash, finalHash);
    }

    [Fact]
    public async Task MoveFileSafely_DuplicateFileName_DoesNotOverwrite_AppendsCollisionSuffix()
    {
        var content1 = "First file content AAAAA";
        var content2 = "Second file content BBBBB (Different)";

        var file1 = SandboxHelper.CreateTextFile(_sandboxDir, "invoice.pdf", content1);

        var classification = new ClassificationResult(
            @"Finance\Invoices", 0.92, 0.92, "Invoice", Array.Empty<string>(), Array.Empty<DestinationSuggestion>(), "Rule");

        // Move first file
        var res1 = await _mover.MoveFileSafelyAsync(file1, _targetBaseDir, @"Finance\Invoices", classification, "Batch-1");
        Assert.True(res1.Success);

        // Create second file with identical original name in source
        var file2 = SandboxHelper.CreateTextFile(_sandboxDir, "invoice.pdf", content2);

        // Move second file
        var res2 = await _mover.MoveFileSafelyAsync(file2, _targetBaseDir, @"Finance\Invoices", classification, "Batch-1");

        Assert.True(res2.Success);
        Assert.True(res2.HadCollision);
        Assert.EndsWith("invoice (1).pdf", res2.FinalDestinationPath);

        // Verify both files exist with original contents intact
        Assert.Equal(content1, File.ReadAllText(res1.FinalDestinationPath));
        Assert.Equal(content2, File.ReadAllText(res2.FinalDestinationPath));
    }

    [Fact]
    public async Task UndoOperation_RestoresFileToOriginalLocation()
    {
        var content = "Restore test document content";
        var file = SandboxHelper.CreateTextFile(_sandboxDir, "to_restore.txt", content);
        var originalPath = file;

        var classification = new ClassificationResult(
            @"Education\School", 0.88, 0.88, "School assignment", Array.Empty<string>(), Array.Empty<DestinationSuggestion>(), "Rule");

        var moveResult = await _mover.MoveFileSafelyAsync(file, _targetBaseDir, @"Education\School", classification, "Batch-Undo-1");
        Assert.True(moveResult.Success);

        // Execute Undo
        var undoResult = await _undoManager.UndoOperationAsync(moveResult.OperationRecord!.Id);

        Assert.True(undoResult.Success, $"Undo failed: {undoResult.ErrorMessage}");
        Assert.True(File.Exists(originalPath), "File should be restored at original location.");
        Assert.Equal(content, File.ReadAllText(originalPath));
        Assert.False(File.Exists(moveResult.FinalDestinationPath), "Organized destination file should no longer exist.");
    }

    [Fact]
    public async Task UndoBatch_RestoresAllFiles()
    {
        var batchId = "Batch-Multi-" + Guid.NewGuid().ToString("N");

        var file1 = SandboxHelper.CreateTextFile(_sandboxDir, "batch_file_1.txt", "Content 1");
        var file2 = SandboxHelper.CreateTextFile(_sandboxDir, "batch_file_2.txt", "Content 2");

        var class1 = new ClassificationResult(@"Education\School", 0.85, 0.85, "School", Array.Empty<string>(), Array.Empty<DestinationSuggestion>(), "Rule");
        var class2 = new ClassificationResult(@"Programming\Source Code", 0.90, 0.90, "Code", Array.Empty<string>(), Array.Empty<DestinationSuggestion>(), "Rule");

        await _mover.MoveFileSafelyAsync(file1, _targetBaseDir, @"Education\School", class1, batchId);
        await _mover.MoveFileSafelyAsync(file2, _targetBaseDir, @"Programming\Source Code", class2, batchId);

        var undoResults = await _undoManager.UndoBatchAsync(batchId);

        Assert.Equal(2, undoResults.Count);
        Assert.All(undoResults, r => Assert.True(r.Success));
        Assert.True(File.Exists(file1));
        Assert.True(File.Exists(file2));
    }

    private static string ComputeSha256(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(fs));
    }

    public void Dispose()
    {
        _historyDb.Dispose();
        SandboxHelper.CleanUpSandbox(_sandboxDir);
    }
}
