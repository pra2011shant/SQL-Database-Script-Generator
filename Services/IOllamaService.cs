namespace SQLDatabaseScriptGenerator.Services;

public interface IOllamaService
{
    Task<string?> GenerateSqlCompletionAsync(string prompt, string systemPrompt, CancellationToken cancellationToken = default);
    Task<bool> IsServiceAvailableAsync(CancellationToken cancellationToken = default);
}
