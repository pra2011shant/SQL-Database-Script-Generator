using SQLDatabaseScriptGenerator.Models;

namespace SQLDatabaseScriptGenerator.Services;

/// <summary>
/// Master AI Orchestrator that intelligently dispatches completion requests to the configured
/// provider (Google Gemini, Groq, Ollama) with multi-tier fallback resilience.
/// </summary>
public class AiOrchestratorService : IAiCompletionService
{
    private readonly GeminiService _geminiService;
    private readonly GroqService _groqService;
    private readonly IOllamaService _ollamaService;
    private readonly IAiConfigurationService _configService;
    private readonly ILogger<AiOrchestratorService> _logger;

    public string ProviderName => _configService.GetSettings().Provider;

    public AiOrchestratorService(
        GeminiService geminiService,
        GroqService groqService,
        IOllamaService ollamaService,
        IAiConfigurationService configService,
        ILogger<AiOrchestratorService> logger)
    {
        _geminiService = geminiService;
        _groqService = groqService;
        _ollamaService = ollamaService;
        _configService = configService;
        _logger = logger;
    }

    public async Task<bool> IsServiceAvailableAsync(CancellationToken cancellationToken = default)
    {
        var settings = _configService.GetSettings();
        var active = (settings.Provider ?? "Gemini").ToLowerInvariant();
        return active switch
        {
            "gemini" => await _geminiService.IsServiceAvailableAsync(cancellationToken),
            "groq" => await _groqService.IsServiceAvailableAsync(cancellationToken),
            "ollama" => await _ollamaService.IsServiceAvailableAsync(cancellationToken),
            "offline" => false,
            _ => await _geminiService.IsServiceAvailableAsync(cancellationToken)
        };
    }

    public async Task<string?> GenerateSqlCompletionAsync(string prompt, string systemPrompt, CancellationToken cancellationToken = default)
    {
        var settings = _configService.GetSettings();
        if (!settings.EnableAiEnhancement)
        {
            _logger.LogInformation("AI enhancement is disabled globally.");
            return null;
        }

        var provider = (settings.Provider ?? "Gemini").Trim().ToLowerInvariant();

        // 1. If explicit "offline", skip AI
        if (provider == "offline")
        {
            _logger.LogInformation("AI Provider is set to Offline (ScriptDom AST only).");
            return null;
        }

        // 2. Try Primary Configured Provider
        string? result = null;
        if (provider == "gemini")
        {
            result = await _geminiService.GenerateSqlCompletionAsync(prompt, systemPrompt, cancellationToken);
            if (!string.IsNullOrWhiteSpace(result)) return result;
        }
        else if (provider == "groq")
        {
            result = await _groqService.GenerateSqlCompletionAsync(prompt, systemPrompt, cancellationToken);
            if (!string.IsNullOrWhiteSpace(result)) return result;
        }
        else if (provider == "ollama")
        {
            result = await _ollamaService.GenerateSqlCompletionAsync(prompt, systemPrompt, cancellationToken);
            if (!string.IsNullOrWhiteSpace(result)) return result;
        }

        // 3. Multi-tier Fallback chain if primary failed or was not configured
        if (settings.FallbackToLocalGenerator)
        {
            // If primary was Gemini but failed, try Groq if key exists
            if (provider != "groq" && !string.IsNullOrWhiteSpace(settings.GroqApiKey))
            {
                _logger.LogInformation("Attempting fallback to Groq AI...");
                result = await _groqService.GenerateSqlCompletionAsync(prompt, systemPrompt, cancellationToken);
                if (!string.IsNullOrWhiteSpace(result)) return result;
            }

            // If primary was Groq but failed, try Gemini if key exists
            if (provider != "gemini" && !string.IsNullOrWhiteSpace(settings.GeminiApiKey))
            {
                _logger.LogInformation("Attempting fallback to Google Gemini AI...");
                result = await _geminiService.GenerateSqlCompletionAsync(prompt, systemPrompt, cancellationToken);
                if (!string.IsNullOrWhiteSpace(result)) return result;
            }

            // Try local Ollama if available
            if (provider != "ollama")
            {
                _logger.LogInformation("Attempting fallback to local Ollama service...");
                result = await _ollamaService.GenerateSqlCompletionAsync(prompt, systemPrompt, cancellationToken);
                if (!string.IsNullOrWhiteSpace(result)) return result;
            }
        }

        _logger.LogInformation("All AI providers exhausted. Activating ScriptDom deterministic AST fallback engine.");
        return null;
    }
}
