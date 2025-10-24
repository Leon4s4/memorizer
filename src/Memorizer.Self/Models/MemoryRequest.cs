using System.Text.Json;

namespace Memorizer.Self.Models;

public record CreateMemoryRequest(
    string Type,
    JsonDocument Content,
    string Source,
    string[]? Tags = null,
    double Confidence = 1.0,
    string? Title = null
);

public record SearchMemoryRequest(
    string Query,
    int Limit = 10,
    string[]? FilterTags = null,
    double MinSimilarity = 0.7
);

public record CreateRelationshipRequest(
    Guid FromMemoryId,
    Guid ToMemoryId,
    string Type
);
