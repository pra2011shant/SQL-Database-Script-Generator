using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SQLDatabaseScriptGenerator.Models;

namespace SQLDatabaseScriptGenerator.Services;

public class GeminiService : IAiCompletionService
{
    private readonly HttpClient _httpClient;
    private readonly IAiConfigurationService _configService;
    private readonly ILogger<GeminiService> _logger;

    public string ProviderName => "Gemini";

    public GeminiService(HttpClient httpClient, IAiConfigurationService configService, ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _configService = configService;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
    }

    public Task<bool> IsServiceAvailableAsync(CancellationToken cancellationToken = default)
    {
        var settings = _configService.GetSettings();
        return Task.FromResult(!string.IsNullOrWhiteSpace(settings.GeminiApiKey));
    }

    public async Task<string?> GenerateSqlCompletionAsync(string prompt, string systemPrompt, CancellationToken cancellationToken = default)
    {
        var settings = _configService.GetSettings();
        var apiKey = settings.GeminiApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogInformation("Gemini API key is not configured. Falling back to next provider.");
            return null;
        }

        try
        {
            var model = !string.IsNullOrWhiteSpace(settings.GeminiModel) ? settings.GeminiModel : "gemini-1.5-flash";
            var requestUri = $"models/{model}:generateContent?key={apiKey}";

            var payload = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = prompt } }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.2,
                    topP = 0.9,
                    maxOutputTokens = 8192
                }
            };

            var json = JsonConvert.SerializeObject(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending SQL generation request to Google Gemini API ({Model})...", model);

            using var response = await _httpClient.PostAsync(requestUri, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini API returned HTTP {StatusCode}: {ResponseBody}", response.StatusCode, responseBody);
                return null;
            }

            var jObj = JObject.Parse(responseBody);
            var text = jObj["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

            return !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error communicating with Google Gemini API: {Message}", ex.Message);
            return null;
        }
    }
}
