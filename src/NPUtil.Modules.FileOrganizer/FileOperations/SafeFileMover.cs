using System.Security.Cryptography;
using NPUtil.Core.Logging;
using NPUtil.Modules.FileOrganizer.Models;

namespace NPUtil.Modules.FileOrganizer.FileOperations;

public class MoveResult
{
    public bool Success { get; set; }
    public string FinalDestinationPath { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public bool HadCollision { get; set; }
    public OrganizeOperationRecord? OperationRecord { get; set; }
}

public class SafeFileMover
{
    private readonly OperationHistoryDb _historyDb;
    private readonly ILogger _logger;

    public SafeFileMover(OperationHistoryDb historyDb, ILogger logger)
    {
        _historyDb = historyDb;
        _logger = logger;
    }

    public async Task<MoveResult> MoveFileSafelyAsync(
        string sourcePath,
        string targetBaseDirectory,
        string categoryRelativePath,
        ClassificationResult classification,
        string batchId,
        bool isDryRun = false,
        CancellationToken ct = default)
    {
        if (!File.Exists(sourcePath))
        {
            return new MoveResult { Success = false, ErrorMessage = $"Source file does not exist: {sourcePath}" };
        }

        var fileInfo = new FileInfo(sourcePath);

        // Do not move hidden or system files
        if (fileInfo.Attributes.HasFlag(FileAttributes.Hidden) || fileInfo.Attributes.HasFlag(FileAttributes.System))
        {
            return new MoveResult { Success = false, ErrorMessage = "Hidden or system file skipped for safety." };
        }

        // Do not process symbolic links or reparse points
        if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return new MoveResult { Success = false, ErrorMessage = "Symbolic link or reparse point skipped for safety." };
        }

        var destinationDir = Path.Combine(targetBaseDirectory, categoryRelativePath);
        var originalFileName = fileInfo.Name;
        var destinationPath = Path.Combine(destinationDir, originalFileName);

        var hadCollision = false;
        var finalDestPath = destinationPath;

        // Collision Resolution: Suffix safely without overwriting
        if (File.Exists(finalDestPath))
        {
            hadCollision = true;
            finalDestPath = GenerateCollisionFreePath(destinationDir, originalFileName);
        }

        var fileHash = string.Empty;
        try
        {
            fileHash = await Task.Run(() => ComputeFileHash(sourcePath), ct);
        }
        catch (Exception ex)
        {
            _logger.Warning("SafeFileMover", $"Could not compute file hash for {sourcePath}", ex);
        }

        if (isDryRun)
        {
            _logger.Info("SafeFileMover", $"[DRY-RUN] Would move '{sourcePath}' -> '{finalDestPath}'");
            return new MoveResult
            {
                Success = true,
                FinalDestinationPath = finalDestPath,
                HadCollision = hadCollision
            };
        }

        // Check if file is locked
        if (!IsFileReady(sourcePath))
        {
            return new MoveResult
            {
                Success = false,
                ErrorMessage = "File is locked by another process (possibly still downloading)."
            };
        }

        try
        {
            Directory.CreateDirectory(destinationDir);

            // Capture timestamps before moving to ensure preservation
            var creationTime = fileInfo.CreationTimeUtc;
            var lastWriteTime = fileInfo.LastWriteTimeUtc;
            var lastAccessTime = fileInfo.LastAccessTimeUtc;

            File.Move(sourcePath, finalDestPath);

            // Restore timestamps
            try
            {
                File.SetCreationTimeUtc(finalDestPath, creationTime);
                File.SetLastWriteTimeUtc(finalDestPath, lastWriteTime);
                File.SetLastAccessTimeUtc(finalDestPath, lastAccessTime);
            }
            catch { /* non-fatal metadata warning */ }

            var record = new OrganizeOperationRecord(
                Id: 0,
                BatchId: batchId,
                OriginalPath: sourcePath,
                DestinationPath: finalDestPath,
                FileName: Path.GetFileName(finalDestPath),
                FileSizeBytes: fileInfo.Length,
                FileHash: fileHash,
                Category: categoryRelativePath,
                Confidence: classification.Confidence,
                Explanation: classification.Explanation,
                Timestamp: DateTime.Now,
                IsUndone: false
            );

            var id = _historyDb.RecordOperation(record);
            var savedRecord = record with { Id = id };

            _logger.Info("SafeFileMover", $"Moved '{sourcePath}' -> '{finalDestPath}' (Batch: {batchId})");

            return new MoveResult
            {
                Success = true,
                FinalDestinationPath = finalDestPath,
                HadCollision = hadCollision,
                OperationRecord = savedRecord
            };
        }
        catch (Exception ex)
        {
            _logger.Error("SafeFileMover", $"Failed to move file '{sourcePath}' to '{finalDestPath}'", ex);
            return new MoveResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static string GenerateCollisionFreePath(string directory, string originalFileName)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
        var ext = Path.GetExtension(originalFileName);
        var counter = 1;

        while (true)
        {
            var candidateName = $"{nameWithoutExt} ({counter}){ext}";
            var candidatePath = Path.Combine(directory, candidateName);
            if (!File.Exists(candidatePath))
            {
                return candidatePath;
            }
            counter++;
        }
    }

    public static bool IsFileReady(string filename)
    {
        try
        {
            using var inputStream = File.Open(filename, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return inputStream.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeFileHash(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(fs);
        return Convert.ToHexString(hash);
    }
}
