using NPUtil.Core.Logging;
using NPUtil.Modules.FileOrganizer.Models;

namespace NPUtil.Modules.FileOrganizer.FileOperations;

public class UndoResult
{
    public bool Success { get; set; }
    public string RestoredPath { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class UndoManager
{
    private readonly OperationHistoryDb _historyDb;
    private readonly ILogger _logger;

    public UndoManager(OperationHistoryDb historyDb, ILogger logger)
    {
        _historyDb = historyDb;
        _logger = logger;
    }

    public async Task<UndoResult> UndoOperationAsync(long operationId)
    {
        var record = _historyDb.GetById(operationId);
        if (record == null)
        {
            return new UndoResult { Success = false, ErrorMessage = $"Operation #{operationId} not found." };
        }

        if (record.IsUndone)
        {
            return new UndoResult { Success = false, ErrorMessage = $"Operation #{operationId} has already been undone." };
        }

        return await Task.Run(() => UndoSingleRecord(record));
    }

    public async Task<IReadOnlyList<UndoResult>> UndoBatchAsync(string batchId)
    {
        var records = _historyDb.GetByBatch(batchId).Where(r => !r.IsUndone).ToList();
        var results = new List<UndoResult>();

        foreach (var record in records)
        {
            var res = await Task.Run(() => UndoSingleRecord(record));
            results.Add(res);
        }

        return results;
    }

    private UndoResult UndoSingleRecord(OrganizeOperationRecord record)
    {
        if (!File.Exists(record.DestinationPath))
        {
            return new UndoResult
            {
                Success = false,
                ErrorMessage = $"Organized file not found at '{record.DestinationPath}' (it may have been deleted or moved manually)."
            };
        }

        var targetDir = Path.GetDirectoryName(record.OriginalPath);
        if (string.IsNullOrEmpty(targetDir))
        {
            return new UndoResult { Success = false, ErrorMessage = "Invalid original directory path." };
        }

        var targetPath = record.OriginalPath;

        // Collision safety during undo: never overwrite an existing file!
        if (File.Exists(targetPath))
        {
            targetPath = GenerateRestoredPath(targetDir, Path.GetFileName(record.OriginalPath));
        }

        try
        {
            Directory.CreateDirectory(targetDir);

            var fi = new FileInfo(record.DestinationPath);
            var creationTime = fi.CreationTimeUtc;
            var lastWriteTime = fi.LastWriteTimeUtc;

            File.Move(record.DestinationPath, targetPath);

            try
            {
                File.SetCreationTimeUtc(targetPath, creationTime);
                File.SetLastWriteTimeUtc(targetPath, lastWriteTime);
            }
            catch { /* metadata warning */ }

            _historyDb.MarkAsUndone(record.Id);
            _logger.Info("UndoManager", $"Undone operation #{record.Id}: Restored '{record.DestinationPath}' -> '{targetPath}'");

            return new UndoResult
            {
                Success = true,
                RestoredPath = targetPath
            };
        }
        catch (Exception ex)
        {
            _logger.Error("UndoManager", $"Failed to undo operation #{record.Id}", ex);
            return new UndoResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static string GenerateRestoredPath(string directory, string originalFileName)
    {
        var name = Path.GetFileNameWithoutExtension(originalFileName);
        var ext = Path.GetExtension(originalFileName);
        var counter = 1;

        while (true)
        {
            var candidate = Path.Combine(directory, $"{name} (restored {counter}){ext}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
            counter++;
        }
    }
}
