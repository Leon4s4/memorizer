using System.ComponentModel;
using System.Text.Json;
using Memorizer.Self.Models;
using Memorizer.Self.Services;
using ModelContextProtocol.Server;

namespace Memorizer.Self.Tools;

[McpServerToolType]
public class MemoryTools
{
    private readonly MemoryService _memoryService;
    private readonly ILogger<MemoryTools> _logger;

    public MemoryTools(MemoryService memoryService, ILogger<MemoryTools> logger)
    {
        _memoryService = memoryService;
        _logger = logger;
    }

    [McpServerTool, Description("Store a new memory in the database, optionally creating a relationship to another memory. Use this to save reference material, how-to guides, coding standards, or any information you (the LLM) may want to refer to when completing tasks.")]
    public async Task<string> Store(
        [Description("The type of memory (e.g., 'conversation', 'document', 'reference', 'how-to', etc.). Use 'reference' or 'how-to' for reusable knowledge.")] string type,
        [Description("Plain text (markdown, code, prose, etc.) that you want to store and embed.")] string text,
        [Description("The source of the memory (e.g., 'user', 'system', 'LLM', etc.). Use 'LLM' if you are storing knowledge for your own future use.")] string source,
        [Description("Title for the memory. If not provided, will be auto-generated.")] string? title = null,
        [Description("Optional tags to categorize the memory. Use tags like 'coding-standard', 'unit-test', 'reference', 'how-to', etc. to make retrieval easier.")] string[]? tags = null,
        [Description("Confidence score for the memory (0.0 to 1.0)")] double confidence = 1.0,
        [Description("Optionally, the ID of a related memory. Use this to link related reference materials, how-tos, or examples.")] Guid? relatedTo = null,
        [Description("Optionally, the type of relationship to create (e.g., 'example-of', 'explains', 'related-to'). Use relationships to connect related knowledge.")] string? relationshipType = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogInformation("MCP Store: type={Type}, source={Source}, tags={Tags}", type, source, string.Join(",", tags ?? []));

            // Create JSON document for content
            var contentJson = JsonSerializer.SerializeToDocument(new Dictionary<string, string> { { "text", text } });

            var memory = await _memoryService.StoreMemory(type, contentJson, source, text, tags, confidence, title);

            // Create relationship if specified
            if (relatedTo.HasValue && !string.IsNullOrEmpty(relationshipType))
            {
                await _memoryService.CreateRelationship(memory.Id, relatedTo.Value, relationshipType);
            }

            var result = new
            {
                success = true,
                id = memory.Id,
                title = memory.Title,
                message = "Memory stored successfully"
            };

            return JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing memory via MCP");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, Description("Search for memories using semantic similarity. Returns the most relevant memories based on the query text.")]
    public async Task<string> Search(
        [Description("The search query text. This will be embedded and compared against stored memories.")] string query,
        [Description("Maximum number of results to return (default: 10)")] int limit = 10,
        [Description("Minimum similarity score threshold (0.0 to 1.0). Higher values return only more relevant results (default: 0.7)")] double minSimilarity = 0.7,
        [Description("Optional array of tags to filter results. Only memories with these tags will be returned.")] string[]? filterTags = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogInformation("MCP Search: query={Query}, limit={Limit}, minSimilarity={MinSimilarity}", query, limit, minSimilarity);

            var memories = await _memoryService.SearchMemories(query, limit, filterTags, minSimilarity);

            var results = memories.Select(m => new
            {
                id = m.Id,
                type = m.Type,
                title = m.Title,
                source = m.Source,
                content = m.Content,
                tags = m.Tags,
                confidence = m.Confidence,
                createdAt = m.CreatedAt,
                updatedAt = m.UpdatedAt
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                count = memories.Count,
                results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching memories via MCP");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, Description("Retrieve a specific memory by its unique ID.")]
    public async Task<string> Get(
        [Description("The unique identifier (GUID) of the memory to retrieve.")] Guid id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogInformation("MCP Get: id={Id}", id);

            var memory = await _memoryService.GetMemory(id);

            if (memory == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Memory not found" });
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                memory = new
                {
                    id = memory.Id,
                    type = memory.Type,
                    title = memory.Title,
                    source = memory.Source,
                    content = memory.Content,
                    tags = memory.Tags,
                    confidence = memory.Confidence,
                    createdAt = memory.CreatedAt,
                    updatedAt = memory.UpdatedAt
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting memory via MCP");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, Description("Retrieve multiple memories by their IDs in a single request.")]
    public async Task<string> GetMany(
        [Description("Array of memory IDs (GUIDs) to retrieve.")] Guid[] ids,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogInformation("MCP GetMany: count={Count}", ids.Length);

            var memories = await _memoryService.GetMemories(ids.ToList());

            var results = memories.Select(m => new
            {
                id = m.Id,
                type = m.Type,
                title = m.Title,
                source = m.Source,
                content = m.Content,
                tags = m.Tags,
                confidence = m.Confidence,
                createdAt = m.CreatedAt,
                updatedAt = m.UpdatedAt
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                count = memories.Count,
                results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting multiple memories via MCP");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, Description("Delete a memory from the database by its ID.")]
    public async Task<string> Delete(
        [Description("The unique identifier (GUID) of the memory to delete.")] Guid id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogInformation("MCP Delete: id={Id}", id);

            var deleted = await _memoryService.DeleteMemory(id);

            if (!deleted)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Memory not found" });
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "Memory deleted successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting memory via MCP");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, Description("Create a relationship between two memories. Use this to link related information, examples, explanations, etc.")]
    public async Task<string> CreateRelationship(
        [Description("The ID of the source memory (the 'from' side of the relationship).")] Guid fromId,
        [Description("The ID of the target memory (the 'to' side of the relationship).")] Guid toId,
        [Description("The type of relationship (e.g., 'example-of', 'explains', 'related-to', 'depends-on').")] string type,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogInformation("MCP CreateRelationship: from={FromId}, to={ToId}, type={Type}", fromId, toId, type);

            var relationship = await _memoryService.CreateRelationship(fromId, toId, type);

            return JsonSerializer.Serialize(new
            {
                success = true,
                relationship = new
                {
                    fromId = relationship.FromMemoryId,
                    toId = relationship.ToMemoryId,
                    type = relationship.Type.ToString(),
                    createdAt = relationship.CreatedAt
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating relationship via MCP");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}
