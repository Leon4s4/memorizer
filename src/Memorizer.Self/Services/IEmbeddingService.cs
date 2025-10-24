namespace Memorizer.Self.Services;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbedding(string text);
    int EmbeddingDimensions { get; }
    Task InitializeAsync();
}
