namespace SQLDatabaseScriptGenerator.Models;

public class AiSettings
{
    public const string SectionName = "AiSettings";

    public string Provider { get; set; } = "Ollama";
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "codellama";
    public int TimeoutSeconds { get; set; } = 30;
    public bool EnableAiEnhancement { get; set; } = true;
    public bool FallbackToLocalGenerator { get; set; } = true;
}
