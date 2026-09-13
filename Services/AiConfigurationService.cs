using Microsoft.Extensions.Options;
using SQLDatabaseScriptGenerator.Models;

namespace SQLDatabaseScriptGenerator.Services;

public interface IAiConfigurationService
{
    AiSettings GetSettings();
    void UpdateSettings(AiSettings settings);
}

public class AiConfigurationService : IAiConfigurationService
{
    private readonly AiSettings _settings;
    private readonly object _lock = new();

    public AiConfigurationService(IOptions<AiSettings> initialSettings)
    {
        _settings = initialSettings.Value ?? new AiSettings();
    }

    public AiSettings GetSettings()
    {
        lock (_lock)
        {
            return new AiSettings
            {
                Provider = _settings.Provider,
                GeminiApiKey = _settings.GeminiApiKey,
                GeminiModel = _settings.GeminiModel,
                GroqApiKey = _settings.GroqApiKey,
                GroqModel = _settings.GroqModel,
                OllamaBaseUrl = _settings.OllamaBaseUrl,
                Model = _settings.Model,
                TimeoutSeconds = _settings.TimeoutSeconds,
                EnableAiEnhancement = _settings.EnableAiEnhancement,
                FallbackToLocalGenerator = _settings.FallbackToLocalGenerator
            };
        }
    }

    public void UpdateSettings(AiSettings newSettings)
    {
        if (newSettings == null) return;
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(newSettings.Provider)) _settings.Provider = newSettings.Provider;
            if (newSettings.GeminiApiKey != null) _settings.GeminiApiKey = newSettings.GeminiApiKey;
            if (!string.IsNullOrWhiteSpace(newSettings.GeminiModel)) _settings.GeminiModel = newSettings.GeminiModel;
            if (newSettings.GroqApiKey != null) _settings.GroqApiKey = newSettings.GroqApiKey;
            if (!string.IsNullOrWhiteSpace(newSettings.GroqModel)) _settings.GroqModel = newSettings.GroqModel;
            if (!string.IsNullOrWhiteSpace(newSettings.OllamaBaseUrl)) _settings.OllamaBaseUrl = newSettings.OllamaBaseUrl;
            if (!string.IsNullOrWhiteSpace(newSettings.Model)) _settings.Model = newSettings.Model;
            _settings.TimeoutSeconds = newSettings.TimeoutSeconds;
            _settings.EnableAiEnhancement = newSettings.EnableAiEnhancement;
            _settings.FallbackToLocalGenerator = newSettings.FallbackToLocalGenerator;
        }
    }
}
