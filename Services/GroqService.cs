using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SQLDatabaseScriptGenerator.Models;

namespace SQLDatabaseScriptGenerator.Services;

public class GroqService : IAiCompletionService
{
    private readonly HttpClient _httpClient;
    private readonly IAiConfigurationService _configService;
    private readonly ILogger<GroqService> _logger;

    public string ProviderName => "Groq";

    public GroqService(HttpClient httpClient, IAiConfigurationService configService, ILogger<GroqService> logger)
    {
        _httpClient = httpClient;
        _configService = configService;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
    }

    public Task<bool> IsServiceAvailableAsync(CancellationToken cancellationToken = default)
    {
        var settings = _configService.GetSettings();
        return Task.FromResult(!string.IsNullOrWhiteSpace(settings.GroqApiKey));
    }

    public async Task<string?> GenerateSqlCompletionAsync(string prompt, string systemPrompt, CancellationToken cancellationToken = default)
    {
        var settings = _configService.GetSettings();
        var apiKey = settings.GroqApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogInformation("Groq API key is not configured. Falling back to next provider.");
            return null;
        }

        try
        {
            var model = !string.IsNullOrWhiteSpace(settings.GroqModel) ? settings.GroqModel : "llama-3.3-70b-versatile";

            var payload = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = prompt }
                },
                temperature = 0.2,
                max_tokens = 8192
            };

            var json = JsonConvert.SerializeObject(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending SQL generation request to Groq API ({Model})...", model);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Groq API returned HTTP {StatusCode}: {ResponseBody}", response.StatusCode, responseBody);
                return null;
            }

            var jObj = JObject.Parse(responseBody);
            var text = jObj["choices"]?[0]?["message"]?["content"]?.ToString();

            return !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error communicating with Groq API: {Message}", ex.Message);
            return null;
        }
    }
}
