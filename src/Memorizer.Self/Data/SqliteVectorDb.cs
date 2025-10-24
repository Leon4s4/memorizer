using Microsoft.Data.Sqlite;
using Memorizer.Self.Models;
using System.Text.Json;

namespace Memorizer.Self.Data;

public class SqliteVectorDb : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _dbPath;
    private readonly ILogger<SqliteVectorDb> _logger;

    public SqliteVectorDb(string dbPath, ILogger<SqliteVectorDb> logger)
    {
        _dbPath = dbPath;
        _logger = logger;
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        InitializeDatabase();
        _logger.LogInformation("SQLite database initialized at {DbPath}", dbPath);
    }

    private void InitializeDatabase()
    {
        var createTablesSql = @"
            CREATE TABLE IF NOT EXISTS memories (
                id TEXT PRIMARY KEY,
                type TEXT NOT NULL,
                content TEXT NOT NULL,
                source TEXT NOT NULL,
                embedding BLOB NOT NULL,
                embedding_metadata BLOB,
                tags TEXT,
                confidence REAL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                title TEXT,
                text TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS relationships (
                id TEXT PRIMARY KEY,
                from_memory_id TEXT NOT NULL,
                to_memory_id TEXT NOT NULL,
                type TEXT NOT NULL,
                created_at TEXT NOT NULL,
                FOREIGN KEY (from_memory_id) REFERENCES memories(id) ON DELETE CASCADE,
                FOREIGN KEY (to_memory_id) REFERENCES memories(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_memories_type ON memories(type);
            CREATE INDEX IF NOT EXISTS idx_memories_created_at ON memories(created_at);
            CREATE INDEX IF NOT EXISTS idx_memories_tags ON memories(tags);
            CREATE INDEX IF NOT EXISTS idx_relationships_from ON relationships(from_memory_id);
            CREATE INDEX IF NOT EXISTS idx_relationships_to ON relationships(to_memory_id);
        ";

        using var command = _connection.CreateCommand();
        command.CommandText = createTablesSql;
        command.ExecuteNonQuery();
    }

    public async Task<Guid> InsertMemory(Memory memory)
    {
        var sql = @"
            INSERT INTO memories (id, type, content, source, embedding, embedding_metadata, tags, confidence, created_at, updated_at, title, text)
            VALUES (@id, @type, @content, @source, @embedding, @embedding_metadata, @tags, @confidence, @created_at, @updated_at, @title, @text)
        ";

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@id", memory.Id.ToString());
        command.Parameters.AddWithValue("@type", memory.Type);
        command.Parameters.AddWithValue("@content", memory.Content.RootElement.GetRawText());
        command.Parameters.AddWithValue("@source", memory.Source);
        command.Parameters.AddWithValue("@embedding", EmbeddingToBytes(memory.Embedding));
        command.Parameters.AddWithValue("@embedding_metadata", memory.EmbeddingMetadata != null ? (object)EmbeddingToBytes(memory.EmbeddingMetadata) : DBNull.Value);
        command.Parameters.AddWithValue("@tags", memory.Tags != null ? (object)string.Join(",", memory.Tags) : DBNull.Value);
        command.Parameters.AddWithValue("@confidence", memory.Confidence);
        command.Parameters.AddWithValue("@created_at", memory.CreatedAt.ToString("o"));
        command.Parameters.AddWithValue("@updated_at", memory.UpdatedAt.ToString("o"));
        command.Parameters.AddWithValue("@title", (object?)memory.Title ?? DBNull.Value);
        command.Parameters.AddWithValue("@text", memory.Text);

        await command.ExecuteNonQueryAsync();
        _logger.LogDebug("Inserted memory {MemoryId}", memory.Id);
        return memory.Id;
    }

    public async Task<Memory?> GetMemory(Guid id)
    {
        var sql = "SELECT * FROM memories WHERE id = @id";
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@id", id.ToString());

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapReaderToMemory(reader);
        }

        return null;
    }

    public async Task<List<Memory>> GetMemoriesByIds(List<Guid> ids)
    {
        if (ids.Count == 0) return new List<Memory>();

        var placeholders = string.Join(",", ids.Select((_, i) => $"@id{i}"));
        var sql = $"SELECT * FROM memories WHERE id IN ({placeholders})";

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        for (int i = 0; i < ids.Count; i++)
        {
            command.Parameters.AddWithValue($"@id{i}", ids[i].ToString());
        }

        var memories = new List<Memory>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            memories.Add(MapReaderToMemory(reader));
        }

        return memories;
    }

    public async Task<List<Memory>> SearchBySimilarity(
        float[] queryEmbedding,
        int limit,
        double minSimilarity = 0.7,
        string[]? filterTags = null,
        bool useMetadataEmbedding = false)
    {
        var results = new List<(Memory memory, double similarity)>();

        // Build SQL query with optional tag filtering
        var sql = "SELECT * FROM memories";
        if (filterTags != null && filterTags.Length > 0)
        {
            var tagConditions = string.Join(" OR ", filterTags.Select((_, i) => $"tags LIKE @tag{i}"));
            sql += $" WHERE ({tagConditions})";
        }

        using var command = _connection.CreateCommand();
        command.CommandText = sql;

        if (filterTags != null && filterTags.Length > 0)
        {
            for (int i = 0; i < filterTags.Length; i++)
            {
                command.Parameters.AddWithValue($"@tag{i}", $"%{filterTags[i]}%");
            }
        }

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            // Get the appropriate embedding (content or metadata)
            var embeddingBytes = useMetadataEmbedding && !reader.IsDBNull(reader.GetOrdinal("embedding_metadata"))
                ? (byte[])reader["embedding_metadata"]
                : (byte[])reader["embedding"];

            var embedding = BytesToEmbedding(embeddingBytes);
            var similarity = CosineSimilarity(queryEmbedding, embedding);

            if (similarity >= minSimilarity)
            {
                var memory = MapReaderToMemory(reader);
                memory.Similarity = similarity;
                results.Add((memory, similarity));
            }
        }

        return results
            .OrderByDescending(r => r.similarity)
            .Take(limit)
            .Select(r => r.memory)
            .ToList();
    }

    public async Task<bool> DeleteMemory(Guid id)
    {
        var sql = "DELETE FROM memories WHERE id = @id";
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@id", id.ToString());

        var rowsAffected = await command.ExecuteNonQueryAsync();
        _logger.LogDebug("Deleted memory {MemoryId}, rows affected: {RowsAffected}", id, rowsAffected);
        return rowsAffected > 0;
    }

    public async Task<Guid> CreateRelationship(MemoryRelationship relationship)
    {
        var sql = @"
            INSERT INTO relationships (id, from_memory_id, to_memory_id, type, created_at)
            VALUES (@id, @from_memory_id, @to_memory_id, @type, @created_at)
        ";

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@id", relationship.Id.ToString());
        command.Parameters.AddWithValue("@from_memory_id", relationship.FromMemoryId.ToString());
        command.Parameters.AddWithValue("@to_memory_id", relationship.ToMemoryId.ToString());
        command.Parameters.AddWithValue("@type", relationship.Type);
        command.Parameters.AddWithValue("@created_at", relationship.CreatedAt.ToString("o"));

        await command.ExecuteNonQueryAsync();
        _logger.LogDebug("Created relationship {RelationshipId}", relationship.Id);
        return relationship.Id;
    }

    public async Task<List<MemoryRelationship>> GetRelationships(Guid memoryId)
    {
        var sql = @"
            SELECT r.*, m.title as related_memory_title, m.type as related_memory_type
            FROM relationships r
            LEFT JOIN memories m ON m.id = r.to_memory_id
            WHERE r.from_memory_id = @memory_id
            UNION
            SELECT r.*, m.title as related_memory_title, m.type as related_memory_type
            FROM relationships r
            LEFT JOIN memories m ON m.id = r.from_memory_id
            WHERE r.to_memory_id = @memory_id
        ";

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@memory_id", memoryId.ToString());

        var relationships = new List<MemoryRelationship>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            relationships.Add(new MemoryRelationship
            {
                Id = Guid.Parse(reader.GetString(0)),
                FromMemoryId = Guid.Parse(reader.GetString(1)),
                ToMemoryId = Guid.Parse(reader.GetString(2)),
                Type = reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                RelatedMemoryTitle = reader.IsDBNull(5) ? null : reader.GetString(5),
                RelatedMemoryType = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }

        return relationships;
    }

    public async Task<Dictionary<string, object>> GetStatistics()
    {
        var stats = new Dictionary<string, object>();

        using var countCmd = _connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM memories";
        stats["total_memories"] = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

        using var typesCmd = _connection.CreateCommand();
        typesCmd.CommandText = "SELECT type, COUNT(*) as count FROM memories GROUP BY type";
        var typeStats = new Dictionary<string, int>();
        using var reader = await typesCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            typeStats[reader.GetString(0)] = reader.GetInt32(1);
        }
        stats["by_type"] = typeStats;

        using var relationshipsCmd = _connection.CreateCommand();
        relationshipsCmd.CommandText = "SELECT COUNT(*) FROM relationships";
        stats["total_relationships"] = Convert.ToInt32(await relationshipsCmd.ExecuteScalarAsync());

        return stats;
    }

    // Helper methods
    private static byte[] EmbeddingToBytes(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] BytesToEmbedding(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have same dimension");

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denominator == 0 ? 0 : dotProduct / denominator;
    }

    private Memory MapReaderToMemory(SqliteDataReader reader)
    {
        var tagsStr = reader.IsDBNull(reader.GetOrdinal("tags")) ? null : reader.GetString(reader.GetOrdinal("tags"));
        var tags = tagsStr?.Split(',', StringSplitOptions.RemoveEmptyEntries);

        var embeddingMetadataBytes = reader.IsDBNull(reader.GetOrdinal("embedding_metadata"))
            ? null
            : (byte[])reader["embedding_metadata"];

        return new Memory
        {
            Id = Guid.Parse(reader.GetString(reader.GetOrdinal("id"))),
            Type = reader.GetString(reader.GetOrdinal("type")),
            Content = JsonDocument.Parse(reader.GetString(reader.GetOrdinal("content"))),
            Source = reader.GetString(reader.GetOrdinal("source")),
            Embedding = BytesToEmbedding((byte[])reader["embedding"]),
            EmbeddingMetadata = embeddingMetadataBytes != null ? BytesToEmbedding(embeddingMetadataBytes) : null,
            Tags = tags,
            Confidence = reader.GetDouble(reader.GetOrdinal("confidence")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
            UpdatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("updated_at"))),
            Title = reader.IsDBNull(reader.GetOrdinal("title")) ? null : reader.GetString(reader.GetOrdinal("title")),
            Text = reader.GetString(reader.GetOrdinal("text"))
        };
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
        _logger.LogDebug("SQLite connection disposed");
    }
}
