namespace Memorizer.Self.Services;

public interface ILLMService
{
    Task<string> GenerateTitle(string content);
    Task InitializeAsync();
}
