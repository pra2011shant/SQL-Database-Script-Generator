using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SQLDatabaseScriptGenerator.Models;

namespace SQLDatabaseScriptGenerator.Services;

public class OllamaClientService : IOllamaClientService
{
    private readonly HttpClient _httpClient;
    private readonly AiSettings _aiSettings;
    private readonly ILogger<OllamaClientService> _logger;

    public OllamaClientService(HttpClient httpClient, IOptions<AiSettings> aiSettings, ILogger<OllamaClientService> logger)
    {
        _httpClient = httpClient;
        _aiSettings = aiSettings.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_aiSettings.OllamaBaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_aiSettings.OllamaBaseUrl.TrimEnd('/') + "/");
        }
        _httpClient.Timeout = TimeSpan.FromSeconds(_aiSettings.TimeoutSeconds > 0 ? _aiSettings.TimeoutSeconds : 30);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> GenerateCompletionAsync(string prompt, string systemPrompt, CancellationToken cancellationToken = default)
    {
        if (!_aiSettings.EnableAiEnhancement)
            return null;

        try
        {
            var payload = new
            {
                model = _aiSettings.Model ?? "codellama",
                prompt = prompt,
                system = systemPrompt,
                stream = false,
                options = new
                {
                    temperature = 0.2,
                    top_p = 0.9
                }
            };

            var jsonContent = new StringContent(
                JsonConvert.SerializeObject(payload),
                Encoding.UTF8,
                "application/json");

            _logger.LogInformation("Invoking Ollama model {Model} at {BaseUrl}", _aiSettings.Model, _httpClient.BaseAddress);

            using var response = await _httpClient.PostAsync("api/generate", jsonContent, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ollama returned non-success status: {StatusCode}", response.StatusCode);
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var resultObj = JsonConvert.DeserializeAnonymousType(responseBody, new { response = "" });

            return resultObj?.response?.Trim();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogInformation("Ollama local service not reachable ({Message}). Utilizing ScriptDom local generator.", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to communicate with LLM service.");
            return null;
        }
    }
}
