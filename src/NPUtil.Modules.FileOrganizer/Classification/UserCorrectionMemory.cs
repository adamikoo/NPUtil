using Microsoft.Data.Sqlite;
using NPUtil.Core.Logging;

namespace NPUtil.Modules.FileOrganizer.Classification;

public class UserCorrectionMemory : IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger _logger;
    private SqliteConnection? _connection;
    private readonly object _lock = new();

    public UserCorrectionMemory(ILogger logger, string? dbPath = null)
    {
        _logger = logger;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NPUtil", "Data");
        Directory.CreateDirectory(dir);
        var path = dbPath ?? Path.Combine(dir, "user_preferences.db");
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

                CREATE TABLE IF NOT EXISTS UserCorrections (
                    PatternKey TEXT NOT NULL,
                    PreferredCategory TEXT NOT NULL,
                    Count INTEGER NOT NULL DEFAULT 1,
                    LastUsedAt TEXT NOT NULL,
                    PRIMARY KEY (PatternKey, PreferredCategory)
                );
            ";
            cmd.ExecuteNonQuery();
        }
    }

    public void RecordCorrection(string patternKey, string preferredCategory)
    {
        if (string.IsNullOrWhiteSpace(patternKey) || string.IsNullOrWhiteSpace(preferredCategory)) return;

        lock (_lock)
        {
            if (_connection == null) return;
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO UserCorrections (PatternKey, PreferredCategory, Count, LastUsedAt)
                VALUES (@key, @cat, 1, @date)
                ON CONFLICT(PatternKey, PreferredCategory) DO UPDATE SET
                    Count = Count + 1,
                    LastUsedAt = @date;
            ";
            cmd.Parameters.AddWithValue("@key", patternKey.ToLowerInvariant().Trim());
            cmd.Parameters.AddWithValue("@cat", preferredCategory.Trim());
            cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();

            _logger.Info("UserCorrectionMemory", $"Recorded user preference: '{patternKey}' -> '{preferredCategory}'");
        }
    }

    public string? GetPreferredCategory(IEnumerable<string> patternKeys)
    {
        lock (_lock)
        {
            if (_connection == null) return null;

            foreach (var key in patternKeys)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT PreferredCategory, Count 
                    FROM UserCorrections 
                    WHERE PatternKey = @key 
                    ORDER BY Count DESC, LastUsedAt DESC 
                    LIMIT 1;
                ";
                cmd.Parameters.AddWithValue("@key", key.ToLowerInvariant().Trim());

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    var count = reader.GetInt32(1);
                    if (count >= 1)
                    {
                        return reader.GetString(0);
                    }
                }
            }

            return null;
        }
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
