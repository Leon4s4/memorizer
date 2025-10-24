using LLama;
using LLama.Common;
using System.Text;

namespace Memorizer.Self.Services;

public class LLamaService : ILLMService, IDisposable
{
    private readonly ILogger<LLamaService> _logger;
    private readonly LLamaWeights _model;
    private readonly LLamaContext _context;
    private readonly InteractiveExecutor _executor;

    public LLamaService(string modelPath, ILogger<LLamaService> logger)
    {
        _logger = logger;

        try
        {
            _logger.LogInformation("Initializing LLM model from {ModelPath}", modelPath);

            var parameters = new ModelParams(modelPath)
            {
                ContextSize = 2048,
                GpuLayerCount = 0 // CPU only
            };

            _model = LLamaWeights.LoadFromFile(parameters);
            _context = _model.CreateContext(parameters);
            _executor = new InteractiveExecutor(_context);

            _logger.LogInformation("LLM model initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize LLM model");
            throw;
        }
    }

    public Task InitializeAsync()
    {
        // Already initialized in constructor
        return Task.CompletedTask;
    }

    public async Task<string> GenerateTitle(string content)
    {
        try
        {
            // Truncate content for title generation
            var truncatedContent = content.Length > 500 ? content.Substring(0, 500) + "..." : content;

            var prompt = $@"Generate a short, descriptive title (maximum 10 words) for this content. Only output the title, nothing else.

Content: {truncatedContent}

Title:";

            var inferenceParams = new InferenceParams
            {
                MaxTokens = 30,
                AntiPrompts = new[] { "\n", "Content:", "Title:" }
            };

            var result = new StringBuilder();
            var tokenCount = 0;

            await foreach (var text in _executor.InferAsync(prompt, inferenceParams))
            {
                result.Append(text);
                tokenCount++;

                // Safety limit
                if (tokenCount > 30) break;
            }

            var title = result.ToString().Trim();

            // Clean up title - remove quotes, extra whitespace
            title = title.Replace("\"", "").Replace("'", "").Trim();

            // Fallback if generation failed
            if (string.IsNullOrWhiteSpace(title) || title.Length < 3)
            {
                title = content.Length > 50
                    ? content.Substring(0, 47) + "..."
                    : content;
            }

            _logger.LogDebug("Generated title: {Title}", title);
            return title;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate title, using fallback");
            // Fallback to truncated content
            return content.Length > 50 ? content.Substring(0, 47) + "..." : content;
        }
    }

    public void Dispose()
    {
        // InteractiveExecutor doesn't implement IDisposable in 0.19.0
        _context?.Dispose();
        _model?.Dispose();
        _logger.LogInformation("LLM service disposed");
    }
}
