namespace SQLDatabaseScriptGenerator.Services;

/// <summary>
/// Unified contract for AI completion providers (Gemini, Groq, Ollama).
/// </summary>
public interface IAiCompletionService
{
    string ProviderName { get; }
    Task<bool> IsServiceAvailableAsync(CancellationToken cancellationToken = default);
    Task<string?> GenerateSqlCompletionAsync(string prompt, string systemPrompt, CancellationToken cancellationToken = default);
}
