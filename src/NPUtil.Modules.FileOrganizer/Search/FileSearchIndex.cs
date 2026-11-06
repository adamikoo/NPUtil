using Microsoft.Data.Sqlite;
using NPUtil.Core.Logging;
using NPUtil.Modules.FileOrganizer.Classification;
using NPUtil.Modules.FileOrganizer.Models;

namespace NPUtil.Modules.FileOrganizer.Search;

public record SearchResultItem(
    string FilePath,
    string FileName,
    string Extension,
    string Category,
    long FileSizeBytes,
    DateTime ModifiedDate,
    string Snippet,
    double RelevanceScore
);

public class FileSearchIndex : IDisposable
{
    private readonly string _connectionString;
    private readonly IEmbeddingEngine _embeddingEngine;
    private readonly ILogger _logger;
    private SqliteConnection? _connection;
    private readonly object _lock = new();

    public FileSearchIndex(IEmbeddingEngine embeddingEngine, ILogger logger, string? dbPath = null)
    {
        _embeddingEngine = embeddingEngine;
        _logger = logger;

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NPUtil", "Data");
        Directory.CreateDirectory(dir);
        var path = dbPath ?? Path.Combine(dir, "search_index.db");
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

                CREATE TABLE IF NOT EXISTS FileRecords (
                    FilePath TEXT PRIMARY KEY,
                    FileName TEXT NOT NULL,
                    Extension TEXT NOT NULL,
                    Category TEXT NOT NULL,
                    FileSizeBytes INTEGER NOT NULL,
                    ModifiedDate TEXT NOT NULL,
                    IndexedDate TEXT NOT NULL,
                    ContentSnippet TEXT,
                    Keywords TEXT,
                    Embedding BLOB
                );

                CREATE VIRTUAL TABLE IF NOT EXISTS FileFts USING fts5(
                    FilePath UNINDEXED,
                    FileName,
                    Extension,
                    Category,
                    ContentSnippet,
                    Keywords,
                    tokenize='unicode61 remove_diacritics 2'
                );
            ";
            cmd.ExecuteNonQuery();
        }
    }

    public async Task IndexFileAsync(ExtractedMetadata metadata, string category, CancellationToken ct = default)
    {
        var snippet = metadata.ExtractedText.Length > 1000
            ? metadata.ExtractedText[..1000]
            : metadata.ExtractedText;

        var keywordsStr = string.Join(" ", metadata.Keywords);
        var embedding = await _embeddingEngine.GenerateEmbeddingAsync(metadata.FileName + " " + snippet, ct);

        lock (_lock)
        {
            if (_connection == null) return;
            using var tx = _connection.BeginTransaction();
            try
            {
                // Upsert to FileRecords
                using var cmdRecord = _connection.CreateCommand();
                cmdRecord.Transaction = tx;
                cmdRecord.CommandText = @"
                    INSERT OR REPLACE INTO FileRecords 
                    (FilePath, FileName, Extension, Category, FileSizeBytes, ModifiedDate, IndexedDate, ContentSnippet, Keywords, Embedding)
                    VALUES 
                    (@path, @fn, @ext, @cat, @size, @mdate, @idate, @snip, @kw, @emb);
                ";
                cmdRecord.Parameters.AddWithValue("@path", metadata.FilePath);
                cmdRecord.Parameters.AddWithValue("@fn", metadata.FileName);
                cmdRecord.Parameters.AddWithValue("@ext", metadata.Extension);
                cmdRecord.Parameters.AddWithValue("@cat", category);
                cmdRecord.Parameters.AddWithValue("@size", metadata.FileSizeBytes);
                cmdRecord.Parameters.AddWithValue("@mdate", metadata.LastModifiedTime.ToString("o"));
                cmdRecord.Parameters.AddWithValue("@idate", DateTime.UtcNow.ToString("o"));
                cmdRecord.Parameters.AddWithValue("@snip", snippet);
                cmdRecord.Parameters.AddWithValue("@kw", keywordsStr);

                var embBytes = new byte[embedding.Length * sizeof(float)];
                Buffer.BlockCopy(embedding, 0, embBytes, 0, embBytes.Length);
                cmdRecord.Parameters.AddWithValue("@emb", embBytes);
                cmdRecord.ExecuteNonQuery();

                // Delete old FTS entry and insert new
                using var cmdFtsDel = _connection.CreateCommand();
                cmdFtsDel.Transaction = tx;
                cmdFtsDel.CommandText = "DELETE FROM FileFts WHERE FilePath = @path;";
                cmdFtsDel.Parameters.AddWithValue("@path", metadata.FilePath);
                cmdFtsDel.ExecuteNonQuery();

                using var cmdFtsIns = _connection.CreateCommand();
                cmdFtsIns.Transaction = tx;
                cmdFtsIns.CommandText = @"
                    INSERT INTO FileFts (FilePath, FileName, Extension, Category, ContentSnippet, Keywords)
                    VALUES (@path, @fn, @ext, @cat, @snip, @kw);
                ";
                cmdFtsIns.Parameters.AddWithValue("@path", metadata.FilePath);
                cmdFtsIns.Parameters.AddWithValue("@fn", metadata.FileName);
                cmdFtsIns.Parameters.AddWithValue("@ext", metadata.Extension);
                cmdFtsIns.Parameters.AddWithValue("@cat", category);
                cmdFtsIns.Parameters.AddWithValue("@snip", snippet);
                cmdFtsIns.Parameters.AddWithValue("@kw", keywordsStr);
                cmdFtsIns.ExecuteNonQuery();

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.Warning("FileSearchIndex", $"Failed to index file {metadata.FilePath}", ex);
            }
        }
    }

    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(
        string query,
        string? categoryFilter = null,
        string? extensionFilter = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return GetRecentFiles(categoryFilter, extensionFilter, limit);
        }

        var results = new Dictionary<string, SearchResultItem>(StringComparer.OrdinalIgnoreCase);

        // 1. Full-Text Search via FTS5
        lock (_lock)
        {
            if (_connection == null) return Array.Empty<SearchResultItem>();

            try
            {
                // Sanitize query for FTS5 (escape special symbols)
                var sanitized = CleanFtsQuery(query);
                if (!string.IsNullOrWhiteSpace(sanitized))
                {
                    using var cmd = _connection.CreateCommand();
                    cmd.CommandText = @"
                        SELECT f.FilePath, f.FileName, f.Extension, f.Category, f.FileSizeBytes, f.ModifiedDate, f.ContentSnippet, bm.rank
                        FROM FileFts fts
                        JOIN FileRecords f ON fts.FilePath = f.FilePath
                        WHERE FileFts MATCH @q
                        LIMIT @limit;
                    ";
                    cmd.Parameters.AddWithValue("@q", sanitized);
                    cmd.Parameters.AddWithValue("@limit", limit);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var path = reader.GetString(0);
                        var item = new SearchResultItem(
                            FilePath: path,
                            FileName: reader.GetString(1),
                            Extension: reader.GetString(2),
                            Category: reader.GetString(3),
                            FileSizeBytes: reader.GetInt64(4),
                            ModifiedDate: DateTime.Parse(reader.GetString(5)),
                            Snippet: reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                            RelevanceScore: 1.0
                        );
                        results[path] = item;
                    }
                }
            }
            catch { /* FTS query syntax fallback */ }
        }

        // 2. Semantic Search using Embedding Cosine Similarity
        var queryEmbedding = await _embeddingEngine.GenerateEmbeddingAsync(query, ct);

        lock (_lock)
        {
            if (_connection == null) return results.Values.ToList();

            using var cmdAll = _connection.CreateCommand();
            cmdAll.CommandText = "SELECT FilePath, FileName, Extension, Category, FileSizeBytes, ModifiedDate, ContentSnippet, Embedding FROM FileRecords;";
            using var reader = cmdAll.ExecuteReader();

            while (reader.Read())
            {
                var path = reader.GetString(0);
                if (reader.IsDBNull(7)) continue;

                var embBytes = (byte[])reader["Embedding"];
                var dim = embBytes.Length / sizeof(float);
                var emb = new float[dim];
                Buffer.BlockCopy(embBytes, 0, emb, 0, embBytes.Length);

                var sim = EmbeddingEngine.CosineSimilarity(queryEmbedding, emb);
                if (sim >= 0.30f)
                {
                    if (results.TryGetValue(path, out var existing))
                    {
                        // Boost score
                        results[path] = existing with { RelevanceScore = existing.RelevanceScore + (sim * 1.5) };
                    }
                    else
                    {
                        results[path] = new SearchResultItem(
                            FilePath: path,
                            FileName: reader.GetString(1),
                            Extension: reader.GetString(2),
                            Category: reader.GetString(3),
                            FileSizeBytes: reader.GetInt64(4),
                            ModifiedDate: DateTime.Parse(reader.GetString(5)),
                            Snippet: reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                            RelevanceScore: sim
                        );
                    }
                }
            }
        }

        var list = results.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(categoryFilter))
        {
            list = list.Where(r => r.Category.Contains(categoryFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(extensionFilter))
        {
            list = list.Where(r => r.Extension.Equals(extensionFilter, StringComparison.OrdinalIgnoreCase));
        }

        return list.OrderByDescending(r => r.RelevanceScore).Take(limit).ToList();
    }

    private IReadOnlyList<SearchResultItem> GetRecentFiles(string? categoryFilter, string? extensionFilter, int limit)
    {
        lock (_lock)
        {
            var list = new List<SearchResultItem>();
            if (_connection == null) return list;

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                SELECT FilePath, FileName, Extension, Category, FileSizeBytes, ModifiedDate, ContentSnippet
                FROM FileRecords
                ORDER BY ModifiedDate DESC
                LIMIT @limit;
            ";
            cmd.Parameters.AddWithValue("@limit", limit);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var cat = reader.GetString(3);
                var ext = reader.GetString(2);

                if (!string.IsNullOrEmpty(categoryFilter) && !cat.Contains(categoryFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrEmpty(extensionFilter) && !ext.Equals(extensionFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                list.Add(new SearchResultItem(
                    FilePath: reader.GetString(0),
                    FileName: reader.GetString(1),
                    Extension: ext,
                    Category: cat,
                    FileSizeBytes: reader.GetInt64(4),
                    ModifiedDate: DateTime.Parse(reader.GetString(5)),
                    Snippet: reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    RelevanceScore: 0.5
                ));
            }
            return list;
        }
    }

    private static string CleanFtsQuery(string raw)
    {
        var terms = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                       .Select(t => t.Trim('"', '*', ':', '^', '~'))
                       .Where(t => t.Length > 1);
        return string.Join(" OR ", terms.Select(t => $"\"{t}\"*"));
    }

    public int GetIndexedCount()
    {
        lock (_lock)
        {
            if (_connection == null) return 0;
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM FileRecords;";
            var res = cmd.ExecuteScalar();
            return res != null ? Convert.ToInt32(res) : 0;
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
