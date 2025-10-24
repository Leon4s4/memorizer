using System.Text.Json;

namespace Memorizer.Self.Models;

public class Memory
{
    public Guid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public JsonDocument Content { get; init; } = JsonDocument.Parse("{}");
    public string Source { get; init; } = string.Empty;
    public float[] Embedding { get; init; } = Array.Empty<float>(); // Vector embedding
    public float[]? EmbeddingMetadata { get; init; } = null;
    public string[]? Tags { get; init; }
    public double Confidence { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string? Title { get; init; }
    public string Text { get; init; } = string.Empty;
    public double? Similarity { get; set; }
    public List<MemoryRelationship>? Relationships { get; set; }
}
