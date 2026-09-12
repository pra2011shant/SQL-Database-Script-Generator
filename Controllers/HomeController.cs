using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SQLDatabaseScriptGenerator.Models;
using SQLDatabaseScriptGenerator.Services;

namespace SQLDatabaseScriptGenerator.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ISqlEngineService _sqlEngineService;
    private readonly ISqlTemplateService _sqlTemplateService;

    public HomeController(
        ILogger<HomeController> logger, 
        ISqlEngineService sqlEngineService,
        ISqlTemplateService sqlTemplateService)
    {
        _logger = logger;
        _sqlEngineService = sqlEngineService;
        _sqlTemplateService = sqlTemplateService;
    }

    public IActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// Fetches sample schema templates securely from the backend service.
    /// </summary>
    [HttpGet]
    public IActionResult GetSampleTemplate(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { Message = "Template key is required." });

        var template = _sqlTemplateService.GetTemplateByKey(key);
        if (template == null)
            return NotFound(new { Message = "Template not found." });

        return Json(new { key = key, sql = template });
    }

    /// <summary>
    /// Asynchronous endpoint to process SQL scripts and return structured JSON response via AJAX/Fetch API.
    /// Routes requests through ISqlEngineService (ScriptDom parser + Ollama LLM / prompt engineering).
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ProcessSql([FromBody] ScriptRequestModel request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.SqlInput))
        {
            return BadRequest(new ScriptResponseModel
            {
                Success = false,
                IsValid = false,
                Message = "SQL script input is required."
            });
        }

        try
        {
            _logger.LogInformation("Processing SQL Action: {Action}", request.Action);
            var response = await _sqlEngineService.ProcessAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SQL request with action {Action}", request.Action);
            return StatusCode(500, new ScriptResponseModel
            {
                Success = false,
                IsValid = false,
                Message = $"An internal error occurred: {ex.Message}"
            });
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
