using Microsoft.Data.Sqlite;
using NPUtil.Core.Logging;

namespace NPUtil.Core.Context;

public record AssistantHistoryRecord
{
    public long Id { get; init; }
    public DateTime Timestamp { get; init; }
    public string ActiveApp { get; init; } = string.Empty;
    public string UserPrompt { get; init; } = string.Empty;
    public string CapturedContext { get; init; } = string.Empty;
    public string Response { get; init; } = string.Empty;
}

public class ContextAssistantHistoryDb
{
    private readonly string _connectionString;
    private readonly ILogger? _logger;

    public ContextAssistantHistoryDb(string? dbPath = null, ILogger? logger = null)
    {
        _logger = logger;
        if (string.IsNullOrEmpty(dbPath))
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NPUtil");
            Directory.CreateDirectory(dir);
            dbPath = Path.Combine(dir, "context_assistant.db");
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS AssistantHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp TEXT NOT NULL,
                ActiveApp TEXT NOT NULL,
                UserPrompt TEXT NOT NULL,
                CapturedContext TEXT NOT NULL,
                Response TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_assistant_time ON AssistantHistory(Timestamp DESC);
        ";
        cmd.ExecuteNonQuery();
    }

    public void AddEntry(string activeApp, string prompt, string context, string response)
    {
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO AssistantHistory (Timestamp, ActiveApp, UserPrompt, CapturedContext, Response)
                VALUES ($time, $app, $prompt, $context, $resp);
            ";
            cmd.Parameters.AddWithValue("$time", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("$app", activeApp);
            cmd.Parameters.AddWithValue("$prompt", prompt);
            cmd.Parameters.AddWithValue("$context", context);
            cmd.Parameters.AddWithValue("$resp", response);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger?.Error("AssistantHistoryDb", $"Failed to save history: {ex.Message}", ex);
        }
    }

    public IReadOnlyList<AssistantHistoryRecord> GetRecentEntries(int limit = 50)
    {
        var list = new List<AssistantHistoryRecord>();
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Timestamp, ActiveApp, UserPrompt, CapturedContext, Response FROM AssistantHistory ORDER BY Id DESC LIMIT $limit;";
            cmd.Parameters.AddWithValue("$limit", limit);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new AssistantHistoryRecord
                {
                    Id = reader.GetInt64(0),
                    Timestamp = DateTime.Parse(reader.GetString(1)),
                    ActiveApp = reader.GetString(2),
                    UserPrompt = reader.GetString(3),
                    CapturedContext = reader.GetString(4),
                    Response = reader.GetString(5)
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.Error("AssistantHistoryDb", $"Failed to fetch history: {ex.Message}", ex);
        }

        return list;
    }

    public void ClearHistory()
    {
        try
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM AssistantHistory;";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger?.Error("AssistantHistoryDb", $"Failed to clear history: {ex.Message}", ex);
        }
    }
}
