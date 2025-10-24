using LLama;
using LLama.Common;

namespace Memorizer.Self.Services;

public class LLamaEmbeddingService : IEmbeddingService, IDisposable
{
    private readonly ILogger<LLamaEmbeddingService> _logger;
    private readonly LLamaWeights _model;
    private readonly LLamaEmbedder _embedder;

    public int EmbeddingDimensions { get; private set; }

    public LLamaEmbeddingService(string modelPath, ILogger<LLamaEmbeddingService> logger)
    {
        _logger = logger;

        try
        {
            _logger.LogInformation("Initializing embedding model from {ModelPath}", modelPath);

            var parameters = new ModelParams(modelPath)
            {
                ContextSize = 512,
                GpuLayerCount = 0, // CPU only for maximum compatibility
                Embeddings = true  // Enable embedding mode
            };

            _model = LLamaWeights.LoadFromFile(parameters);
            _embedder = new LLamaEmbedder(_model, parameters);

            EmbeddingDimensions = _model.EmbeddingSize;

            _logger.LogInformation("Embedding model initialized. Embedding dimensions: {Dimensions}", EmbeddingDimensions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize embedding model");
            throw;
        }
    }

    public Task InitializeAsync()
    {
        // Already initialized in constructor
        return Task.CompletedTask;
    }

    public async Task<float[]> GenerateEmbedding(string text)
    {
        try
        {
            // Truncate text if too long to avoid context overflow
            var truncatedText = text.Length > 2000 ? text.Substring(0, 2000) : text;

            var embeddings = await Task.Run(() => _embedder.GetEmbeddings(truncatedText));

            // GetEmbeddings returns IReadOnlyList<float[]> in 0.18.0, we need the first element
            var embedding = embeddings.Count > 0 ? embeddings[0] : Array.Empty<float>();

            _logger.LogDebug("Generated embedding for text of length {Length}, embedding size: {Size}",
                text.Length, embedding.Length);

            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text of length {Length}", text.Length);
            throw;
        }
    }

    public void Dispose()
    {
        _embedder?.Dispose();
        _model?.Dispose();
        _logger.LogInformation("Embedding service disposed");
    }
}
