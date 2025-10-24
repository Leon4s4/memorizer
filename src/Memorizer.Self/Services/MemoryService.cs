using Memorizer.Self.Data;
using Memorizer.Self.Models;
using System.Text.Json;

namespace Memorizer.Self.Services;

public class MemoryService
{
    private readonly SqliteVectorDb _db;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILLMService _llmService;
    private readonly ILogger<MemoryService> _logger;

    public MemoryService(
        SqliteVectorDb db,
        IEmbeddingService embeddingService,
        ILLMService llmService,
        ILogger<MemoryService> logger)
    {
        _db = db;
        _embeddingService = embeddingService;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<Memory> StoreMemory(
        string type,
        JsonDocument content,
        string source,
        string text,
        string[]? tags = null,
        double confidence = 1.0,
        string? title = null)
    {
        _logger.LogInformation("Storing new memory of type {Type}", type);

        // Generate content embedding
        var contentEmbedding = await _embeddingService.GenerateEmbedding(text);

        // Generate metadata embedding (type + tags for better filtering)
        var metadata = $"{type} {string.Join(" ", tags ?? Array.Empty<string>())}";
        var metadataEmbedding = await _embeddingService.GenerateEmbedding(metadata);

        // Generate title if not provided
        if (string.IsNullOrWhiteSpace(title))
        {
            try
            {
                title = await _llmService.GenerateTitle(text);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Title generation failed, using fallback");
                title = text.Length > 50 ? text.Substring(0, 47) + "..." : text;
            }
        }

        var memory = new Memory
        {
            Id = Guid.NewGuid(),
            Type = type,
            Content = content,
            Source = source,
            Text = text,
            Embedding = contentEmbedding,
            EmbeddingMetadata = metadataEmbedding,
            Tags = tags,
            Confidence = confidence,
            Title = title,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _db.InsertMemory(memory);
        _logger.LogInformation("Stored memory {MemoryId} with title: {Title}", memory.Id, title);

        return memory;
    }

    public async Task<List<Memory>> SearchMemories(
        string query,
        int limit = 10,
        string[]? filterTags = null,
        double minSimilarity = 0.7)
    {
        _logger.LogInformation("Searching memories with query: {Query}, limit: {Limit}", query, limit);

        var queryEmbedding = await _embeddingService.GenerateEmbedding(query);

        // Search using content embeddings first
        var results = await _db.SearchBySimilarity(
            queryEmbedding,
            limit: limit * 2, // Get more results for filtering
            minSimilarity: minSimilarity,
            filterTags: filterTags,
            useMetadataEmbedding: false);

        // If not enough results, try with metadata embeddings (type + tags)
        if (results.Count < limit / 2)
        {
            _logger.LogDebug("Not enough results with content search, trying metadata search");
            var metadataResults = await _db.SearchBySimilarity(
                queryEmbedding,
                limit: limit,
                minSimilarity: minSimilarity * 0.9, // Slightly lower threshold for metadata
                filterTags: filterTags,
                useMetadataEmbedding: true);

            // Merge results, avoiding duplicates
            var existingIds = new HashSet<Guid>(results.Select(m => m.Id));
            foreach (var metadataResult in metadataResults)
            {
                if (!existingIds.Contains(metadataResult.Id))
                {
                    results.Add(metadataResult);
                }
            }
        }

        // If still not enough, try with fallback threshold
        if (results.Count == 0 && minSimilarity > 0.5)
        {
            _logger.LogDebug("No results found, trying with fallback threshold");
            var fallbackThreshold = Math.Max(0.5, minSimilarity - 0.2);
            results = await _db.SearchBySimilarity(
                queryEmbedding,
                limit: limit,
                minSimilarity: fallbackThreshold,
                filterTags: filterTags);
        }

        // Load relationships for each result
        foreach (var memory in results)
        {
            memory.Relationships = await _db.GetRelationships(memory.Id);
        }

        var finalResults = results
            .OrderByDescending(m => m.Similarity)
            .Take(limit)
            .ToList();

        _logger.LogInformation("Search returned {Count} results", finalResults.Count);
        return finalResults;
    }

    public async Task<Memory?> GetMemory(Guid id)
    {
        var memory = await _db.GetMemory(id);
        if (memory != null)
        {
            memory.Relationships = await _db.GetRelationships(id);
        }
        return memory;
    }

    public async Task<List<Memory>> GetMemories(List<Guid> ids)
    {
        var memories = await _db.GetMemoriesByIds(ids);

        // Load relationships for each
        foreach (var memory in memories)
        {
            memory.Relationships = await _db.GetRelationships(memory.Id);
        }

        return memories;
    }

    public async Task<bool> DeleteMemory(Guid id)
    {
        _logger.LogInformation("Deleting memory {MemoryId}", id);
        return await _db.DeleteMemory(id);
    }

    public async Task<MemoryRelationship> CreateRelationship(
        Guid fromMemoryId,
        Guid toMemoryId,
        string type)
    {
        _logger.LogInformation("Creating relationship from {FromId} to {ToId} of type {Type}",
            fromMemoryId, toMemoryId, type);

        var relationship = new MemoryRelationship
        {
            Id = Guid.NewGuid(),
            FromMemoryId = fromMemoryId,
            ToMemoryId = toMemoryId,
            Type = type,
            CreatedAt = DateTime.UtcNow
        };

        await _db.CreateRelationship(relationship);
        return relationship;
    }

    public async Task<Dictionary<string, object>> GetStatistics()
    {
        return await _db.GetStatistics();
    }
}
