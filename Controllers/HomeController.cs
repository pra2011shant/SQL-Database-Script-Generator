using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SQLDatabaseScriptGenerator.Models;
using SQLDatabaseScriptGenerator.Services;

namespace SQLDatabaseScriptGenerator.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ISqlParserService _sqlParserService;

    public HomeController(ILogger<HomeController> logger, ISqlParserService sqlParserService)
    {
        _logger = logger;
        _sqlParserService = sqlParserService;
    }

    public IActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// Asynchronous endpoint to process SQL scripts and return structured JSON response via AJAX/Fetch API.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken] // Optional or handled via header, we'll allow standard API calls as well
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
            var response = await _sqlParserService.ProcessSqlScriptAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SQL request.");
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
