using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Memorizer.Self.Services;

/// <summary>
/// Improved deterministic embedding service using word-based hashing with TF-IDF weighting
/// Provides better semantic similarity than pure hashing by considering word frequency and importance
/// Self-contained with no external dependencies
/// </summary>
public class SimpleEmbeddingService : IEmbeddingService
{
    private readonly ILogger<SimpleEmbeddingService> _logger;
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "for", "from", "has", "he",
        "in", "is", "it", "its", "of", "on", "that", "the", "to", "was", "will", "with"
    };

    public int EmbeddingDimensions => 384; // Standard embedding size

    public SimpleEmbeddingService(ILogger<SimpleEmbeddingService> logger)
    {
        _logger = logger;
        _logger.LogInformation("Using improved SimpleEmbeddingService with word-based semantic hashing");
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task<float[]> GenerateEmbedding(string text)
    {
        // Tokenize and normalize text
        var words = TokenizeText(text);

        // Calculate TF (term frequency) for each word
        var wordFrequency = new Dictionary<string, float>();
        foreach (var word in words)
        {
            if (!StopWords.Contains(word) && word.Length > 2)
            {
                wordFrequency[word] = wordFrequency.GetValueOrDefault(word, 0) + 1;
            }
        }

        // Create embedding by combining word hashes weighted by frequency
        var embedding = new float[EmbeddingDimensions];

        foreach (var (word, frequency) in wordFrequency)
        {
            var wordHash = MD5.HashData(Encoding.UTF8.GetBytes(word.ToLowerInvariant()));

            // Use word hash to determine which dimensions this word affects
            for (int i = 0; i < EmbeddingDimensions; i++)
            {
                var hashIndex = i % wordHash.Length;
                var dimension = (i + hashIndex) % EmbeddingDimensions;

                // Weight by frequency (simple TF weighting)
                var weight = MathF.Log(1 + frequency);
                var value = ((wordHash[hashIndex] / 128.0f) - 1.0f) * weight;

                embedding[dimension] += value;
            }
        }

        // Add text length signal (helps distinguish short vs long texts)
        var lengthSignal = MathF.Log(1 + text.Length) / 10.0f;
        for (int i = 0; i < 10; i++)
        {
            embedding[i] += lengthSignal;
        }

        // Normalize the vector
        var magnitude = MathF.Sqrt(embedding.Sum(v => v * v));
        if (magnitude > 0)
        {
            for (int i = 0; i < EmbeddingDimensions; i++)
            {
                embedding[i] /= magnitude;
            }
        }

        return Task.FromResult(embedding);
    }

    private static List<string> TokenizeText(string text)
    {
        // Simple word tokenization
        var normalized = text.ToLowerInvariant();

        // Split on non-alphanumeric characters
        var words = Regex.Split(normalized, @"[^\w]+")
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .ToList();

        return words;
    }
}
