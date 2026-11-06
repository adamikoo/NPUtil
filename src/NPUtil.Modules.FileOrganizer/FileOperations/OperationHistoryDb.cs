using Microsoft.Data.Sqlite;
using NPUtil.Core.Logging;
using NPUtil.Modules.FileOrganizer.Models;

namespace NPUtil.Modules.FileOrganizer.FileOperations;

public class OperationHistoryDb : IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger _logger;
    private SqliteConnection? _connection;
    private readonly object _lock = new();

    public OperationHistoryDb(ILogger logger, string? dbPath = null)
    {
        _logger = logger;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NPUtil", "Data");
        Directory.CreateDirectory(dir);
        var path = dbPath ?? Path.Combine(dir, "history.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        lock (_lock)
        {
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;

                CREATE TABLE IF NOT EXISTS Operations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    BatchId TEXT NOT NULL,
                    OriginalPath TEXT NOT NULL,
                    DestinationPath TEXT NOT NULL,
                    FileName TEXT NOT NULL,
                    FileSizeBytes INTEGER NOT NULL,
                    FileHash TEXT,
                    Category TEXT NOT NULL,
                    Confidence REAL NOT NULL,
                    Explanation TEXT NOT NULL,
                    Timestamp TEXT NOT NULL,
                    IsUndone INTEGER NOT NULL DEFAULT 0,
                    UndoTimestamp TEXT
                );

                CREATE INDEX IF NOT EXISTS IX_Operations_BatchId ON Operations(BatchId);
                CREATE INDEX IF NOT EXISTS IX_Operations_Timestamp ON Operations(Timestamp DESC);
                CREATE INDEX IF NOT EXISTS IX_Operations_IsUndone ON Operations(IsUndone);
            ";
            cmd.ExecuteNonQuery();
        }
    }

    public long RecordOperation(OrganizeOperationRecord record)
    {
        lock (_lock)
        {
            if (_connection == null) return -1;
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Operations 
                (BatchId, OriginalPath, DestinationPath, FileName, FileSizeBytes, FileHash, Category, Confidence, Explanation, Timestamp, IsUndone)
                VALUES 
                (@batch, @orig, @dest, @fn, @size, @hash, @cat, @conf, @exp, @time, 0);
                SELECT last_insert_rowid();
            ";
            cmd.Parameters.AddWithValue("@batch", record.BatchId);
            cmd.Parameters.AddWithValue("@orig", record.OriginalPath);
            cmd.Parameters.AddWithValue("@dest", record.DestinationPath);
            cmd.Parameters.AddWithValue("@fn", record.FileName);
            cmd.Parameters.AddWithValue("@size", record.FileSizeBytes);
            cmd.Parameters.AddWithValue("@hash", (object?)record.FileHash ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cat", record.Category);
            cmd.Parameters.AddWithValue("@conf", record.Confidence);
            cmd.Parameters.AddWithValue("@exp", record.Explanation);
            cmd.Parameters.AddWithValue("@time", record.Timestamp.ToString("o"));

            var result = cmd.ExecuteScalar();
            return Convert.ToInt64(result);
        }
    }

    public IReadOnlyList<OrganizeOperationRecord> GetRecentOperations(int limit = 100)
    {
        lock (_lock)
        {
            var list = new List<OrganizeOperationRecord>();
            if (_connection == null) return list;

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, BatchId, OriginalPath, DestinationPath, FileName, FileSizeBytes, FileHash,
                       Category, Confidence, Explanation, Timestamp, IsUndone, UndoTimestamp
                FROM Operations
                ORDER BY Id DESC
                LIMIT @limit;
            ";
            cmd.Parameters.AddWithValue("@limit", limit);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(ReadRecord(reader));
            }
            return list;
        }
    }

    public OrganizeOperationRecord? GetById(long id)
    {
        lock (_lock)
        {
            if (_connection == null) return null;
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, BatchId, OriginalPath, DestinationPath, FileName, FileSizeBytes, FileHash,
                       Category, Confidence, Explanation, Timestamp, IsUndone, UndoTimestamp
                FROM Operations WHERE Id = @id;
            ";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadRecord(reader) : null;
        }
    }

    public IReadOnlyList<OrganizeOperationRecord> GetByBatch(string batchId)
    {
        lock (_lock)
        {
            var list = new List<OrganizeOperationRecord>();
            if (_connection == null) return list;

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, BatchId, OriginalPath, DestinationPath, FileName, FileSizeBytes, FileHash,
                       Category, Confidence, Explanation, Timestamp, IsUndone, UndoTimestamp
                FROM Operations WHERE BatchId = @batch
                ORDER BY Id DESC;
            ";
            cmd.Parameters.AddWithValue("@batch", batchId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(ReadRecord(reader));
            }
            return list;
        }
    }

    public void MarkAsUndone(long id)
    {
        lock (_lock)
        {
            if (_connection == null) return;
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE Operations 
                SET IsUndone = 1, UndoTimestamp = @undoTime 
                WHERE Id = @id;
            ";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@undoTime", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }
    }

    public (int TotalMoved, int ActiveOrganized, int TotalUndone) GetCounts()
    {
        lock (_lock)
        {
            if (_connection == null) return (0, 0, 0);
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                SELECT 
                    COUNT(*) as Total,
                    SUM(CASE WHEN IsUndone = 0 THEN 1 ELSE 0 END) as Active,
                    SUM(CASE WHEN IsUndone = 1 THEN 1 ELSE 0 END) as Undone
                FROM Operations;
            ";
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var total = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader[0]);
                var active = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader[1]);
                var undone = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader[2]);
                return (total, active, undone);
            }
            return (0, 0, 0);
        }
    }

    private static OrganizeOperationRecord ReadRecord(SqliteDataReader reader)
    {
        return new OrganizeOperationRecord(
            Id: reader.GetInt64(0),
            BatchId: reader.GetString(1),
            OriginalPath: reader.GetString(2),
            DestinationPath: reader.GetString(3),
            FileName: reader.GetString(4),
            FileSizeBytes: reader.GetInt64(5),
            FileHash: reader.IsDBNull(6) ? null : reader.GetString(6),
            Category: reader.GetString(7),
            Confidence: reader.GetDouble(8),
            Explanation: reader.GetString(9),
            Timestamp: DateTime.Parse(reader.GetString(10)),
            IsUndone: reader.GetInt32(11) == 1,
            UndoTimestamp: reader.IsDBNull(12) ? null : DateTime.Parse(reader.GetString(12))
        );
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }
}
