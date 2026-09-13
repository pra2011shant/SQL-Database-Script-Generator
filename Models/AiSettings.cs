namespace SQLDatabaseScriptGenerator.Models;

public class AiSettings
{
    public const string SectionName = "AiSettings";

    public string Provider { get; set; } = "Gemini"; // "Gemini", "Groq", "Ollama", "Offline"
    
    // Google Gemini Settings
    public string? GeminiApiKey { get; set; }
    public string GeminiModel { get; set; } = "gemini-1.5-flash";
    
    // Groq Settings
    public string? GroqApiKey { get; set; }
    public string GroqModel { get; set; } = "llama-3.3-70b-versatile";

    // Ollama Settings
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "codellama";

    // General
    public int TimeoutSeconds { get; set; } = 30;
    public bool EnableAiEnhancement { get; set; } = true;
    public bool FallbackToLocalGenerator { get; set; } = true;
}
