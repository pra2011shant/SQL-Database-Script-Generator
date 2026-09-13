namespace SQLDatabaseScriptGenerator.Services;

/// <summary>
/// Dedicated contract for AI completion providers (Groq Cloud Llama-3.3-70B).
/// </summary>
public interface IAiCompletionService
{
    string ProviderName { get; }
    Task<bool> IsServiceAvailableAsync(CancellationToken cancellationToken = default);
    Task<string?> GenerateSqlCompletionAsync(string prompt, string systemPrompt, CancellationToken cancellationToken = default);
}
