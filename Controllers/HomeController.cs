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
    private readonly ISqlStandardsService _standardsService;
    private readonly ILiveDatabaseInspectorService _liveInspectorService;
    private readonly IAiConfigurationService _aiConfigService;

    public HomeController(
        ILogger<HomeController> logger,
        ISqlEngineService sqlEngineService,
        ISqlTemplateService sqlTemplateService,
        ISqlStandardsService standardsService,
        ILiveDatabaseInspectorService liveInspectorService,
        IAiConfigurationService aiConfigService)
    {
        _logger = logger;
        _sqlEngineService = sqlEngineService;
        _sqlTemplateService = sqlTemplateService;
        _standardsService = standardsService;
        _liveInspectorService = liveInspectorService;
        _aiConfigService = aiConfigService;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult GetAiConfig()
    {
        var config = _aiConfigService.GetSettings();
        // Mask API keys for safe display if desired, or return direct for management
        return Json(config);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult UpdateAiConfig([FromBody] AiSettings config)
    {
        if (config == null)
            return BadRequest(new { Message = "Invalid AI configuration payload." });

        _aiConfigService.UpdateSettings(config);
        return Ok(new { Message = "AI configuration updated successfully.", ActiveProvider = config.Provider });
    }

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

    [HttpGet]
    public IActionResult GetCorporateStandards()
    {
        return Json(_standardsService.GetStandards());
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public IActionResult UpdateCorporateStandards([FromBody] CorporateSqlStandards standards)
    {
        if (standards == null)
            return BadRequest(new { Message = "Invalid standards payload." });

        _standardsService.UpdateStandards(standards);
        return Ok(new { Message = "Corporate SQL standards updated successfully." });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> InspectLiveDatabaseTables([FromBody] LiveDbInspectRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.ConnectionString))
            return BadRequest(new { Message = "Connection string is required." });

        try
        {
            var tables = await _liveInspectorService.GetTableNamesAsync(request.ConnectionString, cancellationToken);
            return Ok(new { Success = true, Tables = tables });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ExtractLiveTableDdl([FromBody] LiveDbInspectRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.ConnectionString) || string.IsNullOrWhiteSpace(request?.TableName))
            return BadRequest(new { Message = "Connection string and table name are required." });

        try
        {
            var ddl = await _liveInspectorService.GenerateTableDdlAsync(request.ConnectionString, request.TableName, cancellationToken);
            return Ok(new { Success = true, Ddl = ddl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Success = false, Message = ex.Message });
        }
    }

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
