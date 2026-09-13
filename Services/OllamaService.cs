using System.IO;
using System.Text;
using Newtonsoft.Json;
using SQLDatabaseScriptGenerator.Models;

namespace SQLDatabaseScriptGenerator.Services;

public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly IAiConfigurationService _configService;
    private readonly ILogger<OllamaService> _logger;

    public string ProviderName => "Ollama";

    public OllamaService(HttpClient httpClient, IAiConfigurationService configService, ILogger<OllamaService> logger)
    {
        _httpClient = httpClient;
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// Checks if the local Ollama background service is running on http://localhost:11434.
    /// </summary>
    public async Task<bool> IsServiceAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = _configService.GetSettings();
            var baseUrl = string.IsNullOrWhiteSpace(settings.OllamaBaseUrl) ? "http://localhost:11434" : settings.OllamaBaseUrl.TrimEnd('/');
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2)); // Rapid heartbeat check

            using var response = await _httpClient.GetAsync($"{baseUrl}/api/tags", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Ollama health probe returned offline: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Dispatches prompt payload to /api/generate and deserializes the generated SQL script.
    /// </summary>
    public async Task<string?> GenerateSqlCompletionAsync(string prompt, string systemPrompt, CancellationToken cancellationToken = default)
    {
        var settings = _configService.GetSettings();
        if (!settings.EnableAiEnhancement)
        {
            _logger.LogInformation("AI enhancement is disabled in configuration. Skipping Ollama dispatch.");
            return null;
        }

        var baseUrl = string.IsNullOrWhiteSpace(settings.OllamaBaseUrl) ? "http://localhost:11434" : settings.OllamaBaseUrl.TrimEnd('/');

        try
        {
            var payload = new
            {
                model = !string.IsNullOrWhiteSpace(settings.Model) ? settings.Model : "codellama",
                prompt = prompt,
                system = systemPrompt,
                stream = false,
                options = new
                {
                    temperature = 0.2,
                    top_p = 0.9,
                    num_ctx = 4096
                }
            };

            var json = JsonConvert.SerializeObject(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending SQL generation prompt to Ollama ({Model}) at {BaseUrl}/api/generate",
                payload.model, baseUrl);

            using var response = await _httpClient.PostAsync($"{baseUrl}/api/generate", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ollama responded with non-success HTTP status code: {StatusCode}", response.StatusCode);
                return null;
            }

            using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var streamReader = new StreamReader(responseStream);
            using var jsonReader = new JsonTextReader(streamReader);

            var serializer = new JsonSerializer();
            var result = serializer.Deserialize<OllamaGenerateResponse>(jsonReader);

            var generatedText = result?.Response?.Trim();
            return !string.IsNullOrWhiteSpace(generatedText) ? generatedText : null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogInformation("Ollama local service is offline or unreachable at {BaseUrl} ({Message}). Fallback engine activated.",
                baseUrl, ex.Message);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning("Ollama request timed out after {Timeout}s ({Message}). Fallback engine activated.",
                settings.TimeoutSeconds, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error communicating with Ollama service.");
            return null;
        }
    }

    private class OllamaGenerateResponse
    {
        [JsonProperty("response")]
        public string? Response { get; set; }

        [JsonProperty("done")]
        public bool Done { get; set; }
    }
}
