namespace SQLDatabaseScriptGenerator.Models;

public class AiSettings
{
    public const string SectionName = "AiSettings";

    public string Provider { get; set; } = "Groq"; // "Groq", "Offline"
    
    // Groq Cloud Settings
    public string? GroqApiKey { get; set; }
    public string GroqModel { get; set; } = "llama-3.3-70b-versatile";

    // General & Fallback
    public int TimeoutSeconds { get; set; } = 30;
    public bool EnableAiEnhancement { get; set; } = true;
    public bool FallbackToLocalGenerator { get; set; } = true;
}
