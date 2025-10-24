# Implementation Guide: Memorizer Self-Contained C#

This guide provides step-by-step instructions to complete the self-contained C# implementation.

## Prerequisites

- .NET 9 SDK installed
- Docker installed
- Basic understanding of C#, SQLite, and ML concepts
- Familiarity with the original Memorizer codebase

## Phase 1: Database Layer (Estimated: 8 hours)

### 1.1 Create SQLite Database Service

**File:** `src/Memorizer.Self/Data/SqliteVectorDb.cs`

```csharp
using Microsoft.Data.Sqlite;
using Memorizer.Self.Models;

namespace Memorizer.Self.Data;

public class SqliteVectorDb : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _dbPath;

    public SqliteVectorDb(string dbPath)
    {
        _dbPath = dbPath;
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        InitializeDatabase();
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
                FOREIGN KEY (from_memory_id) REFERENCES memories(id),
                FOREIGN KEY (to_memory_id) REFERENCES memories(id)
            );

            CREATE INDEX IF NOT EXISTS idx_memories_type ON memories(type);
            CREATE INDEX IF NOT EXISTS idx_memories_created_at ON memories(created_at);
            CREATE INDEX IF NOT EXISTS idx_relationships_from ON relationships(from_memory_id);
            CREATE INDEX IF NOT EXISTS idx_relationships_to ON relationships(to_memory_id);
        ";

        using var command = _connection.CreateCommand();
        command.CommandText = createTablesSql;
        command.ExecuteNonQuery();
    }

    // TODO: Implement methods
    // - Task<Guid> InsertMemory(Memory memory)
    // - Task<Memory?> GetMemory(Guid id)
    // - Task<List<Memory>> SearchBySimilarity(float[] queryEmbedding, int limit)
    // - Task<List<Memory>> GetMemoriesByIds(List<Guid> ids)
    // - Task DeleteMemory(Guid id)
    // - Task<Guid> CreateRelationship(MemoryRelationship relationship)
    // - Task<List<MemoryRelationship>> GetRelationships(Guid memoryId)

    // Helper: Convert float[] to BLOB
    private static byte[] EmbeddingToBytes(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    // Helper: Convert BLOB to float[]
    private static float[] BytesToEmbedding(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    // Helper: Cosine similarity
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

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
```

### 1.2 Implement Vector Search

The key challenge: SQLite doesn't have native vector indexing. We'll use brute-force search:

```csharp
public async Task<List<Memory>> SearchBySimilarity(
    float[] queryEmbedding,
    int limit,
    double minSimilarity = 0.7)
{
    var results = new List<(Memory memory, double similarity)>();

    using var command = _connection.CreateCommand();
    command.CommandText = "SELECT * FROM memories";

    using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var embeddingBytes = (byte[])reader["embedding"];
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
```

**Note:** For better performance with >10K memories, consider:
- Using sqlite-vec extension (requires native library)
- Pre-filtering by tags before similarity calculation
- Caching frequently accessed embeddings

## Phase 2: LLamaSharp Integration (Estimated: 12 hours)

### 2.1 Embedding Service

**File:** `src/Memorizer.Self/Services/LLamaEmbeddingService.cs`

```csharp
using LLama;
using LLama.Common;
using Memorizer.Self.Services;

namespace Memorizer.Self.Services;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbedding(string text);
    int EmbeddingDimensions { get; }
}

public class LLamaEmbeddingService : IEmbeddingService, IDisposable
{
    private readonly LLamaWeights _model;
    private readonly LLamaContext _context;
    private readonly LLamaEmbedder _embedder;

    public int EmbeddingDimensions { get; }

    public LLamaEmbeddingService(string modelPath)
    {
        var parameters = new ModelParams(modelPath)
        {
            ContextSize = 512,
            Seed = 1337,
            GpuLayerCount = 0, // CPU only
            Embeddings = true  // Enable embedding mode
        };

        _model = LLamaWeights.LoadFromFile(parameters);
        _context = _model.CreateContext(parameters);
        _embedder = new LLamaEmbedder(_model, parameters);

        EmbeddingDimensions = _model.EmbeddingSize;
    }

    public async Task<float[]> GenerateEmbedding(string text)
    {
        var embedding = await _embedder.GetEmbeddings(text);
        return embedding;
    }

    public void Dispose()
    {
        _embedder?.Dispose();
        _context?.Dispose();
        _model?.Dispose();
    }
}
```

### 2.2 LLM Service for Title Generation

**File:** `src/Memorizer.Self/Services/LLamaService.cs`

```csharp
using LLama;
using LLama.Common;

namespace Memorizer.Self.Services;

public interface ILLMService
{
    Task<string> GenerateTitle(string content);
}

public class LLamaService : ILLMService, IDisposable
{
    private readonly LLamaWeights _model;
    private readonly LLamaContext _context;
    private readonly InteractiveExecutor _executor;

    public LLamaService(string modelPath)
    {
        var parameters = new ModelParams(modelPath)
        {
            ContextSize = 2048,
            Seed = 1337,
            GpuLayerCount = 0
        };

        _model = LLamaWeights.LoadFromFile(parameters);
        _context = _model.CreateContext(parameters);
        _executor = new InteractiveExecutor(_context);
    }

    public async Task<string> GenerateTitle(string content)
    {
        var prompt = $@"Generate a short, descriptive title (max 10 words) for this content:

{content.Substring(0, Math.Min(500, content.Length))}

Title:";

        var inferenceParams = new InferenceParams
        {
            MaxTokens = 20,
            Temperature = 0.7f,
            AntiPrompts = new[] { "\n", "." }
        };

        var result = new StringBuilder();
        await foreach (var text in _executor.InferAsync(prompt, inferenceParams))
        {
            result.Append(text);
        }

        return result.ToString().Trim();
    }

    public void Dispose()
    {
        _executor?.Dispose();
        _context?.Dispose();
        _model?.Dispose();
    }
}
```

## Phase 3: Business Logic (Estimated: 10 hours)

### 3.1 Memory Service

**File:** `src/Memorizer.Self/Services/MemoryService.cs`

```csharp
using Memorizer.Self.Data;
using Memorizer.Self.Models;

namespace Memorizer.Self.Services;

public class MemoryService
{
    private readonly SqliteVectorDb _db;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILLMService _llmService;

    public MemoryService(
        SqliteVectorDb db,
        IEmbeddingService embeddingService,
        ILLMService llmService)
    {
        _db = db;
        _embeddingService = embeddingService;
        _llmService = llmService;
    }

    public async Task<Memory> StoreMemory(Memory memory)
    {
        // Generate embeddings
        var contentEmbedding = await _embeddingService.GenerateEmbedding(memory.Text);

        // Generate metadata embedding (type + tags)
        var metadata = $"{memory.Type} {string.Join(" ", memory.Tags ?? Array.Empty<string>())}";
        var metadataEmbedding = await _embeddingService.GenerateEmbedding(metadata);

        // Generate title if not provided
        string title = memory.Title;
        if (string.IsNullOrEmpty(title))
        {
            title = await _llmService.GenerateTitle(memory.Text);
        }

        var newMemory = memory with
        {
            Id = Guid.NewGuid(),
            Embedding = contentEmbedding,
            EmbeddingMetadata = metadataEmbedding,
            Title = title,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _db.InsertMemory(newMemory);
        return newMemory;
    }

    public async Task<List<Memory>> SearchMemories(
        string query,
        int limit = 10,
        string[]? tags = null,
        double minSimilarity = 0.7)
    {
        var queryEmbedding = await _embeddingService.GenerateEmbedding(query);
        var results = await _db.SearchBySimilarity(queryEmbedding, limit * 2, minSimilarity);

        // Filter by tags if provided
        if (tags != null && tags.Length > 0)
        {
            results = results.Where(m =>
                m.Tags != null && m.Tags.Intersect(tags).Any()
            ).ToList();
        }

        return results.Take(limit).ToList();
    }

    // TODO: Implement
    // - Task<Memory?> GetMemory(Guid id)
    // - Task<List<Memory>> GetMemories(List<Guid> ids)
    // - Task DeleteMemory(Guid id)
    // - Task<MemoryRelationship> CreateRelationship(...)
}
```

## Phase 4: API & MCP (Estimated: 10 hours)

### 4.1 Program.cs Setup

**File:** `src/Memorizer.Self/Program.cs`

```csharp
using Memorizer.Self.Data;
using Memorizer.Self.Services;
using ModelContextProtocol.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var modelsPath = builder.Configuration["ModelsPath"] ?? "/app/models";
var dataPath = builder.Configuration["DataPath"] ?? "/app/data";
var dbPath = Path.Combine(dataPath, "memorizer.db");

// Ensure directories exist
Directory.CreateDirectory(dataPath);

// Services
builder.Services.AddSingleton(sp => new SqliteVectorDb(dbPath));
builder.Services.AddSingleton<IEmbeddingService>(sp =>
    new LLamaEmbeddingService(Path.Combine(modelsPath, "all-MiniLM-L6-v2.gguf")));
builder.Services.AddSingleton<ILLMService>(sp =>
    new LLamaService(Path.Combine(modelsPath, "tinyllama-1.1b-chat.gguf")));
builder.Services.AddScoped<MemoryService>();

// MCP
builder.Services.AddMcp();

// API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapMcp("/mcp");
app.MapHealthChecks("/healthz");

app.Run();
```

### 4.2 API Endpoints

Create controllers for:
- `POST /api/memories` - Store memory
- `POST /api/search` - Search memories
- `GET /api/memories/{id}` - Get memory
- `DELETE /api/memories/{id}` - Delete memory
- `POST /api/relationships` - Create relationship

## Phase 5: Docker (Estimated: 6 hours)

### 5.1 Dockerfile

**File:** `memorizer-self/Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Download models during build (requires internet)
WORKDIR /models
RUN apt-get update && apt-get install -y wget && \
    wget https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/ggml-model-q4_0.gguf \
      -O all-MiniLM-L6-v2.gguf && \
    wget https://huggingface.co/TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF/resolve/main/tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf \
      -O tinyllama-1.1b-chat.gguf

# Build application
WORKDIR /src
COPY src/Memorizer.Self/ .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0

WORKDIR /app

# Copy models
COPY --from=build /models /app/models

# Copy application
COPY --from=build /app/publish .

# Create data directory
RUN mkdir -p /app/data

EXPOSE 8000
EXPOSE 8501

ENTRYPOINT ["dotnet", "Memorizer.Self.dll"]
```

### 5.2 docker-compose.yml

```yaml
version: '3.8'

services:
  memorizer-self:
    build:
      context: .
      dockerfile: Dockerfile
    image: memorizer-self:latest
    container_name: memorizer-self
    restart: unless-stopped
    ports:
      - "9000:8000"  # API
    volumes:
      - memorizer-self-data:/app/data
    environment:
      - ASPNETCORE_URLS=http://+:8000
      - ModelsPath=/app/models
      - DataPath=/app/data

volumes:
  memorizer-self-data:
```

## Testing Checklist

- [ ] Database initialization works
- [ ] Embeddings generate correctly
- [ ] LLM title generation works
- [ ] Vector search returns relevant results
- [ ] API endpoints respond correctly
- [ ] MCP server integrates with Claude Desktop
- [ ] Docker image builds successfully
- [ ] Air-gapped deployment works (no internet at runtime)
- [ ] Data persists across container restarts

## Performance Optimization

1. **Caching:**
   - Cache LLamaSharp models in memory (singleton)
   - Cache frequently accessed memories

2. **Batch Processing:**
   - Process multiple embeddings in batch
   - Use Akka.NET actors for background title generation

3. **Database Optimization:**
   - Add indexes on frequently queried columns
   - Consider sqlite-vec for >10K memories
   - Use connection pooling

## Estimated Total Time

- Phase 1 (Database): 8 hours
- Phase 2 (LLamaSharp): 12 hours
- Phase 3 (Business Logic): 10 hours
- Phase 4 (API & MCP): 10 hours
- Phase 5 (Docker): 6 hours
- Testing & Debugging: 8 hours

**Total: 54 hours** (approximately 1-2 weeks)

## Next Steps

1. Start with Phase 1: Implement `SqliteVectorDb.cs`
2. Write unit tests as you go
3. Test each phase before moving to next
4. Reference the Python implementation for business logic
5. Reference the original C# implementation for API structure

## Resources

- [LLamaSharp Documentation](https://github.com/SciSharp/LLamaSharp)
- [SQLite Documentation](https://www.sqlite.org/docs.html)
- [Model Context Protocol](https://modelcontextprotocol.io/)
- [.NET 9 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/overview)

---

Good luck with the implementation! The Python version is already production-ready if you need a working solution immediately.
