using Microsoft.Data.Sqlite;
using NPUtil.Core.Logging;

namespace NPUtil.Modules.FileOrganizer.Classification;

public class EmbeddingCacheDb : IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger _logger;
    private SqliteConnection? _connection;
    private readonly object _lock = new();

    public EmbeddingCacheDb(ILogger logger, string? dbPath = null)
    {
        _logger = logger;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NPUtil", "Data");
        Directory.CreateDirectory(dir);
        var path = dbPath ?? Path.Combine(dir, "embeddings_cache.db");
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

                CREATE TABLE IF NOT EXISTS FileEmbeddings (
                    FileHash TEXT PRIMARY KEY,
                    Embedding BLOB NOT NULL,
                    Dimension INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS CategoryEmbeddings (
                    RelativePath TEXT PRIMARY KEY,
                    TextHash TEXT NOT NULL,
                    Embedding BLOB NOT NULL,
                    Dimension INTEGER NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );
            ";
            cmd.ExecuteNonQuery();
        }
    }

    public float[]? GetFileEmbedding(string fileHash)
    {
        lock (_lock)
        {
            if (_connection == null) return null;
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT Embedding, Dimension FROM FileEmbeddings WHERE FileHash = @hash";
            cmd.Parameters.AddWithValue("@hash", fileHash);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var bytes = (byte[])reader["Embedding"];
                var dim = reader.GetInt32(1);
                return DeserializeFloats(bytes, dim);
            }
            return null;
        }
    }

    public void SaveFileEmbedding(string fileHash, float[] embedding)
    {
        lock (_lock)
        {
            if (_connection == null) return;
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO FileEmbeddings (FileHash, Embedding, Dimension, CreatedAt)
                VALUES (@hash, @embedding, @dim, @date)
            ";
            cmd.Parameters.AddWithValue("@hash", fileHash);
            cmd.Parameters.AddWithValue("@embedding", SerializeFloats(embedding));
            cmd.Parameters.AddWithValue("@dim", embedding.Length);
            cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }
    }

    public float[]? GetCategoryEmbedding(string relativePath, string currentTextHash)
    {
        lock (_lock)
        {
            if (_connection == null) return null;
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT Embedding, Dimension FROM CategoryEmbeddings WHERE RelativePath = @path AND TextHash = @thash";
            cmd.Parameters.AddWithValue("@path", relativePath);
            cmd.Parameters.AddWithValue("@thash", currentTextHash);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var bytes = (byte[])reader["Embedding"];
                var dim = reader.GetInt32(1);
                return DeserializeFloats(bytes, dim);
            }
            return null;
        }
    }

    public void SaveCategoryEmbedding(string relativePath, string textHash, float[] embedding)
    {
        lock (_lock)
        {
            if (_connection == null) return;
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO CategoryEmbeddings (RelativePath, TextHash, Embedding, Dimension, UpdatedAt)
                VALUES (@path, @thash, @embedding, @dim, @date)
            ";
            cmd.Parameters.AddWithValue("@path", relativePath);
            cmd.Parameters.AddWithValue("@thash", textHash);
            cmd.Parameters.AddWithValue("@embedding", SerializeFloats(embedding));
            cmd.Parameters.AddWithValue("@dim", embedding.Length);
            cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }
    }

    private static byte[] SerializeFloats(float[] floats)
    {
        var bytes = new byte[floats.Length * sizeof(float)];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] DeserializeFloats(byte[] bytes, int dimension)
    {
        var floats = new float[dimension];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
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
