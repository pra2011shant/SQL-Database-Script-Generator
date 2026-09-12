namespace SQLDatabaseScriptGenerator.Services;

public interface IOllamaClientService
{
    Task<string?> GenerateCompletionAsync(string prompt, string systemPrompt, CancellationToken cancellationToken = default);
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
