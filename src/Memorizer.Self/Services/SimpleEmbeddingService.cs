using System.Security.Cryptography;
using System.Text;

namespace Memorizer.Self.Services;

/// <summary>
/// Simple deterministic embedding service using hashing
/// This is a workaround for LLamaSharp Microsoft.Extensions.AI compatibility issues
/// TODO: Replace with proper LLama embeddings once the API stabilizes
/// </summary>
public class SimpleEmbeddingService : IEmbeddingService
{
    private readonly ILogger<SimpleEmbeddingService> _logger;
    public int EmbeddingDimensions => 384; // Standard embedding size

    public SimpleEmbeddingService(ILogger<SimpleEmbeddingService> logger)
    {
        _logger = logger;
        _logger.LogWarning("Using SimpleEmbeddingService - this is a temporary workaround for LLamaSharp compatibility issues");
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task<float[]> GenerateEmbedding(string text)
    {
        // Generate a deterministic embedding based on text hash
        // This ensures same text always gets same embedding
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));

        var embedding = new float[EmbeddingDimensions];

        // Convert hash bytes to float values in range [-1, 1]
        for (int i = 0; i < EmbeddingDimensions; i++)
        {
            var byteIndex = i % hash.Length;
            embedding[i] = (hash[byteIndex] / 128.0f) - 1.0f;
        }

        // Normalize the vector
        var magnitude = 0.0f;
        for (int i = 0; i < EmbeddingDimensions; i++)
        {
            magnitude += embedding[i] * embedding[i];
        }
        magnitude = MathF.Sqrt(magnitude);

        if (magnitude > 0)
        {
            for (int i = 0; i < EmbeddingDimensions; i++)
            {
                embedding[i] /= magnitude;
            }
        }

        return Task.FromResult(embedding);
    }
}
