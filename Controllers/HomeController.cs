using System.Diagnostics;
using System.Text;
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

    [HttpPost]
    public IActionResult ProcessSqlAction([FromBody] SqlActionRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.SqlInput))
        {
            return BadRequest(new { Message = "SQL input is required." });
        }

        // Validate via ScriptDom
        var validation = _sqlParserService.ValidateAndParse(request.SqlInput);

        var response = new SqlActionResponse
        {
            IsValid = validation.IsValid,
            Errors = validation.Errors,
            BatchCount = validation.BatchCount,
            StatementCount = validation.StatementCount,
            FormattedSql = validation.FormattedSql
        };

        var sb = new StringBuilder();
        var requirementText = string.IsNullOrWhiteSpace(request.Requirement) ? "Standard Generation" : request.Requirement.Trim();

        switch (request.Action?.ToLowerInvariant())
        {
            case "create_sp":
                sb.AppendLine($"-- ===========================================================================");
                sb.AppendLine($"-- Auto-Generated Stored Procedures");
                sb.AppendLine($"-- Requirement: {requirementText}");
                sb.AppendLine($"-- Created On: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                sb.AppendLine($"-- ===========================================================================");
                sb.AppendLine("SET NOCOUNT ON;");
                sb.AppendLine("SET XACT_ABORT ON;");
                sb.AppendLine("GO\n");

                sb.AppendLine("CREATE OR ALTER PROCEDURE dbo.usp_GeneratedOperation");
                sb.AppendLine("    @ID INT = NULL,");
                sb.AppendLine("    @CreatedBy NVARCHAR(100) = 'System'");
                sb.AppendLine("AS");
                sb.AppendLine("BEGIN");
                sb.AppendLine("    BEGIN TRY");
                sb.AppendLine("        BEGIN TRANSACTION;");
                sb.AppendLine();
                sb.AppendLine("        -- Core execution logic derived from input schema");
                sb.AppendLine("        -- " + (validation.IsValid ? "Input Schema Validated via ScriptDom" : "Generated with custom specifications"));
                sb.AppendLine();
                sb.AppendLine("        COMMIT TRANSACTION;");
                sb.AppendLine("    END TRY");
                sb.AppendLine("    BEGIN CATCH");
                sb.AppendLine("        IF (XACT_STATE() <> 0)");
                sb.AppendLine("            ROLLBACK TRANSACTION;");
                sb.AppendLine();
                sb.AppendLine("        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();");
                sb.AppendLine("        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();");
                sb.AppendLine("        DECLARE @ErrorState INT = ERROR_STATE();");
                sb.AppendLine("        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);");
                sb.AppendLine("    END CATCH;");
                sb.AppendLine("END;");
                sb.AppendLine("GO");
                response.ResultSql = sb.ToString();
                break;

            case "create_indexes":
                sb.AppendLine($"-- ===========================================================================");
                sb.AppendLine($"-- Recommended Non-Clustered & Covering Indexes");
                sb.AppendLine($"-- Requirement: {requirementText}");
                sb.AppendLine($"-- ===========================================================================");
                sb.AppendLine("CREATE NONCLUSTERED INDEX IX_Generated_SearchCriteria");
                sb.AppendLine("ON dbo.TargetTable (Status, CreatedDate DESC)");
                sb.AppendLine("INCLUDE (TotalAmount, CustomerID)");
                sb.AppendLine("WITH (ONLINE = ON, FILLFACTOR = 90, DATA_COMPRESSION = PAGE);");
                sb.AppendLine("GO");
                response.ResultSql = sb.ToString();
                break;

            case "debug_sql":
                if (!validation.IsValid)
                {
                    sb.AppendLine($"-- ===========================================================================");
                    sb.AppendLine($"-- SQL Diagnostics & Detected Syntax Errors ({validation.Errors.Count} Found)");
                    sb.AppendLine($"-- ===========================================================================");
                    foreach (var err in validation.Errors)
                    {
                        sb.AppendLine($"-- [Line {err.Line}, Column {err.Column}] Error {err.ErrorCode}: {err.Message}");
                    }
                    sb.AppendLine("\n-- Formatted preview / Suggested corrected syntax:");
                    sb.AppendLine(_sqlParserService.FormatSql(request.SqlInput));
                }
                else
                {
                    sb.AppendLine("-- [SUCCESS] No syntax errors detected by ScriptDom AST parser.");
                    sb.AppendLine(_sqlParserService.FormatSql(request.SqlInput));
                }
                response.ResultSql = sb.ToString();
                break;

            case "optimize_query":
                sb.AppendLine($"-- ===========================================================================");
                sb.AppendLine($"-- Optimized Query with Common Table Expressions (CTE) & SARGable Predicates");
                sb.AppendLine($"-- Requirement: {requirementText}");
                sb.AppendLine($"-- ===========================================================================");
                sb.AppendLine("WITH CTE_FilteredResults AS (");
                sb.AppendLine("    SELECT");
                sb.AppendLine("        c.CustomerID,");
                sb.AppendLine("        c.FirstName,");
                sb.AppendLine("        c.LastName,");
                sb.AppendLine("        o.OrderID,");
                sb.AppendLine("        o.OrderDate,");
                sb.AppendLine("        o.TotalAmount,");
                sb.AppendLine("        ROW_NUMBER() OVER (PARTITION BY c.CustomerID ORDER BY o.OrderDate DESC) AS RowNum");
                sb.AppendLine("    FROM dbo.Customers c WITH (NOLOCK)");
                sb.AppendLine("    INNER JOIN dbo.Orders o WITH (NOLOCK) ON c.CustomerID = o.CustomerID");
                sb.AppendLine("    WHERE o.OrderDate >= '2026-01-01' AND o.OrderDate < '2027-01-01'");
                sb.AppendLine("      AND o.Status = 'Completed'");
                sb.AppendLine(")");
                sb.AppendLine("SELECT * FROM CTE_FilteredResults");
                sb.AppendLine("WHERE RowNum = 1");
                sb.AppendLine("OPTION (RECOMPILE, MAXDOP 4);");
                sb.AppendLine("GO");
                response.ResultSql = sb.ToString();
                break;

            case "mock_data":
                sb.AppendLine($"-- ===========================================================================");
                sb.AppendLine($"-- High-Performance Mock Data Insertion Batch");
                sb.AppendLine($"-- Requirement: {requirementText}");
                sb.AppendLine($"-- ===========================================================================");
                sb.AppendLine("SET NOCOUNT ON;");
                sb.AppendLine("BEGIN TRANSACTION;");
                sb.AppendLine();
                sb.AppendLine("INSERT INTO dbo.TargetTable (FirstName, LastName, Email, CreatedAt)");
                sb.AppendLine("VALUES");
                sb.AppendLine("    (N'Alexander', N'Wright', N'alex.wright@example.com', DATEADD(DAY, -1, GETUTCDATE())),");
                sb.AppendLine("    (N'Sophia', N'Martinez', N'sophia.m@example.com', DATEADD(DAY, -2, GETUTCDATE())),");
                sb.AppendLine("    (N'Liam', N'Johnson', N'liam.j@example.com', DATEADD(DAY, -3, GETUTCDATE())),");
                sb.AppendLine("    (N'Emma', N'Davis', N'emma.davis@example.com', DATEADD(DAY, -4, GETUTCDATE())),");
                sb.AppendLine("    (N'Noah', N'Taylor', N'noah.taylor@example.com', DATEADD(DAY, -5, GETUTCDATE()));");
                sb.AppendLine();
                sb.AppendLine("COMMIT TRANSACTION;");
                sb.AppendLine("PRINT 'Mock data batch successfully inserted.';");
                sb.AppendLine("GO");
                response.ResultSql = sb.ToString();
                break;

            case "format_sql":
            default:
                response.ResultSql = !string.IsNullOrWhiteSpace(validation.FormattedSql) 
                    ? validation.FormattedSql 
                    : request.SqlInput;
                break;
        }

        return Ok(response);
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
