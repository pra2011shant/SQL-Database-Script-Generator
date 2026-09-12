using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLDatabaseScriptGenerator.Models;

namespace SQLDatabaseScriptGenerator.Services;

public class SqlEngineService : ISqlEngineService
{
    private readonly IOllamaClientService _ollamaClient;
    private readonly ILogger<SqlEngineService> _logger;

    public SqlEngineService(IOllamaClientService ollamaClient, ILogger<SqlEngineService> logger)
    {
        _ollamaClient = ollamaClient;
        _logger = logger;
    }

    public SqlValidationResult ValidateSql(string sql)
    {
        var result = new SqlValidationResult();

        if (string.IsNullOrWhiteSpace(sql))
        {
            result.IsValid = false;
            result.Errors.Add(new SqlParseError
            {
                Line = 0,
                Column = 0,
                Message = "SQL script is empty or whitespace."
            });
            return result;
        }

        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql);
        var fragment = parser.Parse(reader, out IList<ParseError> errors);

        if (errors != null && errors.Count > 0)
        {
            result.IsValid = false;
            foreach (var error in errors)
            {
                result.Errors.Add(new SqlParseError
                {
                    Line = error.Line,
                    Column = error.Column,
                    Message = error.Message,
                    ErrorCode = error.Number
                });
            }
            return result;
        }

        result.IsValid = true;

        if (fragment is TSqlScript script)
        {
            result.BatchCount = script.Batches?.Count ?? 0;
            result.StatementCount = 0;
            if (script.Batches != null)
            {
                foreach (var batch in script.Batches)
                {
                    result.StatementCount += batch.Statements?.Count ?? 0;
                }
            }
        }

        result.FormattedSql = FormatFragment(fragment);
        return result;
    }

    public string FormatSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return string.Empty;

        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sql);
        var fragment = parser.Parse(reader, out IList<ParseError> errors);

        if (errors != null && errors.Count > 0)
            return sql;

        return FormatFragment(fragment);
    }

    public async Task<ScriptResponseModel> ProcessAsync(ScriptRequestModel request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // 1. Local Parser: Deep AST syntax check & metrics via ScriptDom
        var validation = ValidateSql(request.SqlInput);

        var response = new ScriptResponseModel
        {
            IsValid = validation.IsValid,
            Errors = validation.Errors,
            BatchCount = validation.BatchCount,
            StatementCount = validation.StatementCount,
            FormattedSql = validation.FormattedSql
        };

        var tableName = ExtractTableName(request.SqlInput) ?? "TargetTable";
        var requirement = string.IsNullOrWhiteSpace(request.Requirement)
            ? "Follow modern T-SQL production standards."
            : request.Requirement.Trim();

        // 2. Switch-Case Routing Logic & AI Prompt Engineering
        string? generatedSql = null;
        string actionKey = request.Action?.ToLowerInvariant() ?? "create_sp";

        switch (actionKey)
        {
            case "create_sp":
                generatedSql = await HandleCreateStoredProceduresAsync(request, tableName, requirement, validation, cancellationToken);
                response.Recommendations.Add("Explicit transactions with XACT_ABORT ON prevent partial batch failures.");
                response.Recommendations.Add("TRY...CATCH logs error severity, state, and message accurately.");
                response.Recommendations.Add("OFFSET-FETCH provides optimized server-side pagination over legacy ROW_NUMBER.");
                break;

            case "create_indexes":
                generatedSql = await HandleCreateIndexesAsync(request, tableName, requirement, cancellationToken);
                response.Recommendations.Add("Non-clustered indexes on foreign keys significantly reduce join overhead.");
                response.Recommendations.Add("INCLUDE columns create covering indexes that eliminate Key Lookups.");
                if (request.UsePageCompression) response.Recommendations.Add("PAGE data compression reduces disk footprint and I/O reads.");
                break;

            case "debug_sql":
                generatedSql = await HandleDebugSqlAsync(request, requirement, validation, cancellationToken);
                if (!validation.IsValid)
                {
                    response.Recommendations.Add("Check line and column numbers indicated in the ScriptDom Diagnostics panel.");
                    response.Recommendations.Add("Ensure proper closing quotes, parenthesis, and T-SQL keywords.");
                }
                else
                {
                    response.Recommendations.Add("ScriptDom parser verified 0 syntax errors in the provided script.");
                }
                break;

            case "optimize_query":
                generatedSql = await HandleOptimizeQueryAsync(request, tableName, requirement, cancellationToken);
                response.Recommendations.Add("Avoid non-SARGable scalar functions in WHERE predicates.");
                response.Recommendations.Add("Use Common Table Expressions (CTE) and Window Functions for deduplication and ranking.");
                response.Recommendations.Add("Add OPTION (RECOMPILE) or query hints only after analyzing execution plans.");
                break;

            case "mock_data":
                generatedSql = await HandleMockDataAsync(request, tableName, requirement, cancellationToken);
                response.Recommendations.Add("Batch INSERT statements using multi-row VALUES minimize transaction log overhead.");
                response.Recommendations.Add("Keep mock transactions small and wrapped in BEGIN/COMMIT blocks.");
                break;

            case "format_sql":
            default:
                generatedSql = !string.IsNullOrWhiteSpace(validation.FormattedSql) ? validation.FormattedSql : request.SqlInput;
                response.Recommendations.Add("Script formatted with UPPERCASE keywords, structured indentation, and semicolons.");
                break;
        }

        stopwatch.Stop();
        response.ResultSql = CleanAiOutput(generatedSql ?? string.Empty);
        response.ExecutionTimeMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2);
        response.Message = validation.IsValid ? "SQL script processed successfully." : "Processed with syntax warnings.";

        return response;
    }

    #region Action Handlers (AI Prompting + Local Fallback)

    private async Task<string> HandleCreateStoredProceduresAsync(
        ScriptRequestModel req, string tableName, string requirement, SqlValidationResult validation, CancellationToken ct)
    {
        string systemPrompt = "You are a Principal Microsoft SQL Server Database Architect. " +
            "Generate production-grade T-SQL stored procedures following strict best practices (SET NOCOUNT ON, SET XACT_ABORT ON, TRY/CATCH, Transactions, comments). " +
            "Return ONLY executable T-SQL code inside a ```sql codeblock without conversation.";

        string userPrompt = $"Generate full CRUD stored procedures for table [{tableName}] based on schema:\n\n{req.SqlInput}\n\n" +
            $"Requirements: {requirement}\n" +
            $"Options: IncludeTryCatch={req.IncludeTryCatch}, IncludeTransactions={req.IncludeTransactions}, IncludeComments={req.IncludeComments}.";

        var aiResult = await _ollamaClient.GenerateCompletionAsync(userPrompt, systemPrompt, ct);
        if (!string.IsNullOrWhiteSpace(aiResult))
            return aiResult;

        // Local Deterministic Generator Fallback
        var sb = new StringBuilder();
        sb.AppendLine($"-- ===========================================================================");
        sb.AppendLine($"-- Stored Procedures for: dbo.[{tableName}]");
        sb.AppendLine($"-- Requirement: {requirement}");
        sb.AppendLine($"-- Generated On: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"-- ===========================================================================");
        sb.AppendLine("SET NOCOUNT ON;");
        sb.AppendLine("SET XACT_ABORT ON;");
        sb.AppendLine("GO\n");

        sb.AppendLine($"CREATE OR ALTER PROCEDURE dbo.usp_{tableName}_GetByID");
        sb.AppendLine($"    @ID INT");
        sb.AppendLine($"AS");
        sb.AppendLine($"BEGIN");
        sb.AppendLine($"    SET NOCOUNT ON;");
        sb.AppendLine($"    SELECT * FROM dbo.[{tableName}] WITH (NOLOCK) WHERE [{tableName}ID] = @ID;");
        sb.AppendLine($"END;");
        sb.AppendLine("GO\n");

        sb.AppendLine($"CREATE OR ALTER PROCEDURE dbo.usp_{tableName}_Search");
        sb.AppendLine($"    @SearchTerm NVARCHAR(100) = NULL,");
        sb.AppendLine($"    @PageIndex INT = 1,");
        sb.AppendLine($"    @PageSize INT = 20");
        sb.AppendLine($"AS");
        sb.AppendLine($"BEGIN");
        sb.AppendLine($"    SET NOCOUNT ON;");
        sb.AppendLine($"    DECLARE @Offset INT = (@PageIndex - 1) * @PageSize;");
        sb.AppendLine();
        sb.AppendLine($"    SELECT *, COUNT(*) OVER() AS TotalCount");
        sb.AppendLine($"    FROM dbo.[{tableName}] WITH (NOLOCK)");
        sb.AppendLine($"    ORDER BY [{tableName}ID] DESC");
        sb.AppendLine($"    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;");
        sb.AppendLine($"END;");
        sb.AppendLine("GO\n");

        sb.AppendLine($"CREATE OR ALTER PROCEDURE dbo.usp_{tableName}_Save");
        sb.AppendLine($"    @ID INT = NULL OUTPUT,");
        sb.AppendLine($"    @UpdatedBy NVARCHAR(100) = 'System'");
        sb.AppendLine($"AS");
        sb.AppendLine($"BEGIN");
        sb.AppendLine($"    SET NOCOUNT ON;");
        sb.AppendLine($"    SET XACT_ABORT ON;");
        if (req.IncludeTryCatch) sb.AppendLine("    BEGIN TRY");
        if (req.IncludeTransactions) sb.AppendLine("        BEGIN TRANSACTION;");
        sb.AppendLine();
        sb.AppendLine($"        IF (@ID IS NULL OR @ID = 0)");
        sb.AppendLine($"        BEGIN");
        sb.AppendLine($"            -- INSERT Operation");
        sb.AppendLine($"            -- INSERT INTO dbo.[{tableName}] (...) VALUES (...);");
        sb.AppendLine($"            SET @ID = SCOPE_IDENTITY();");
        sb.AppendLine($"        END");
        sb.AppendLine($"        ELSE");
        sb.AppendLine($"        BEGIN");
        sb.AppendLine($"            -- UPDATE Operation");
        sb.AppendLine($"            -- UPDATE dbo.[{tableName}] SET ModifiedDate = GETUTCDATE() WHERE [{tableName}ID] = @ID;");
        sb.AppendLine($"            PRINT 'Record updated successfully.';");
        sb.AppendLine($"        END");
        sb.AppendLine();
        if (req.IncludeTransactions) sb.AppendLine("        COMMIT TRANSACTION;");
        if (req.IncludeTryCatch)
        {
            sb.AppendLine("    END TRY");
            sb.AppendLine("    BEGIN CATCH");
            sb.AppendLine("        IF (XACT_STATE() <> 0) ROLLBACK TRANSACTION;");
            sb.AppendLine("        DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();");
            sb.AppendLine("        DECLARE @ErrSeverity INT = ERROR_SEVERITY();");
            sb.AppendLine("        DECLARE @ErrState INT = ERROR_STATE();");
            sb.AppendLine("        RAISERROR(@ErrMsg, @ErrSeverity, @ErrState);");
            sb.AppendLine("    END CATCH;");
        }
        sb.AppendLine($"END;");
        sb.AppendLine("GO");

        return sb.ToString();
    }

    private async Task<string> HandleCreateIndexesAsync(
        ScriptRequestModel req, string tableName, string requirement, CancellationToken ct)
    {
        string systemPrompt = "You are an expert T-SQL Query Optimization & Database Indexing Specialist. " +
            "Analyze table schemas or queries and write optimized indexes (Clustered, Non-Clustered, Covering with INCLUDE, and Filtered). " +
            "Return ONLY executable T-SQL code.";

        string userPrompt = $"Recommend optimal indexes for table [{tableName}] or schema:\n\n{req.SqlInput}\n\nRequirements: {requirement}";

        var aiResult = await _ollamaClient.GenerateCompletionAsync(userPrompt, systemPrompt, ct);
        if (!string.IsNullOrWhiteSpace(aiResult))
            return aiResult;

        // Local Fallback
        var sb = new StringBuilder();
        sb.AppendLine($"-- ===========================================================================");
        sb.AppendLine($"-- Index Recommendations for: dbo.[{tableName}]");
        sb.AppendLine($"-- Requirement: {requirement}");
        sb.AppendLine($"-- ===========================================================================");
        sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_{tableName}_CustomerID' AND object_id = OBJECT_ID('dbo.[{tableName}]'))");
        sb.AppendLine($"BEGIN");
        sb.AppendLine($"    CREATE NONCLUSTERED INDEX IX_{tableName}_CustomerID");
        sb.AppendLine($"    ON dbo.[{tableName}] (CustomerID)");
        sb.AppendLine($"    WITH (ONLINE = ON, FILLFACTOR = 90{(req.UsePageCompression ? ", DATA_COMPRESSION = PAGE" : "")});");
        sb.AppendLine($"END;");
        sb.AppendLine("GO\n");

        sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_{tableName}_Status_CreatedAt' AND object_id = OBJECT_ID('dbo.[{tableName}]'))");
        sb.AppendLine($"BEGIN");
        sb.AppendLine($"    CREATE NONCLUSTERED INDEX IX_{tableName}_Status_CreatedAt");
        sb.AppendLine($"    ON dbo.[{tableName}] (Status, CreatedAt DESC)");
        sb.AppendLine($"    INCLUDE (TotalAmount)");
        sb.AppendLine($"    WITH (ONLINE = ON, FILLFACTOR = 85{(req.UsePageCompression ? ", DATA_COMPRESSION = PAGE" : "")});");
        sb.AppendLine($"END;");
        sb.AppendLine("GO");

        return sb.ToString();
    }

    private async Task<string> HandleDebugSqlAsync(
        ScriptRequestModel req, string requirement, SqlValidationResult validation, CancellationToken ct)
    {
        var errorDiagnostics = new StringBuilder();
        if (!validation.IsValid && validation.Errors.Count > 0)
        {
            errorDiagnostics.AppendLine("ScriptDom Syntax Errors:");
            foreach (var err in validation.Errors)
            {
                errorDiagnostics.AppendLine($"- Line {err.Line}, Column {err.Column}: {err.Message} (Error #{err.ErrorCode})");
            }
        }

        string systemPrompt = "You are a T-SQL Debugger and Syntax Specialist. " +
            "Analyze the flawed or buggy SQL script, fix all syntax and semantic issues, and return the corrected, formatted T-SQL. " +
            "Include short comment headers explaining what was fixed.";

        string userPrompt = $"Flawed SQL:\n```sql\n{req.SqlInput}\n```\n\n{errorDiagnostics}\nRequirements: {requirement}";

        var aiResult = await _ollamaClient.GenerateCompletionAsync(userPrompt, systemPrompt, ct);
        if (!string.IsNullOrWhiteSpace(aiResult))
            return aiResult;

        // Local Fallback
        var sb = new StringBuilder();
        if (!validation.IsValid)
        {
            sb.AppendLine($"-- ===========================================================================");
            sb.AppendLine($"-- SCRIPT DOM DIAGNOSTICS: {validation.Errors.Count} ERROR(S) FOUND");
            sb.AppendLine($"-- ===========================================================================");
            foreach (var err in validation.Errors)
            {
                sb.AppendLine($"-- [Line {err.Line}, Column {err.Column}] {err.Message}");
            }
            sb.AppendLine("\n-- Parsed Script Preview / Formatting Attempt:");
        }
        else
        {
            sb.AppendLine("-- [SUCCESS] ScriptDom validated 0 syntax errors in the query.");
        }
        sb.AppendLine(validation.FormattedSql);
        return sb.ToString();
    }

    private async Task<string> HandleOptimizeQueryAsync(
        ScriptRequestModel req, string tableName, string requirement, CancellationToken ct)
    {
        string systemPrompt = "You are a Microsoft SQL Server Performance Tuning Guru. " +
            "Rewrite unoptimized queries using CTEs, Window Functions, SARGable predicates, and optimal JOINs. " +
            "Return ONLY executable T-SQL code with brief explanatory comments.";

        string userPrompt = $"Optimize the following query:\n\n{req.SqlInput}\n\nRequirements: {requirement}";

        var aiResult = await _ollamaClient.GenerateCompletionAsync(userPrompt, systemPrompt, ct);
        if (!string.IsNullOrWhiteSpace(aiResult))
            return aiResult;

        // Local Fallback
        var sb = new StringBuilder();
        sb.AppendLine($"-- ===========================================================================");
        sb.AppendLine($"-- Optimized Query with CTE & SARGable Predicates");
        sb.AppendLine($"-- Requirement: {requirement}");
        sb.AppendLine($"-- ===========================================================================");
        sb.AppendLine("WITH CTE_RankedItems AS (");
        sb.AppendLine("    SELECT");
        sb.AppendLine("        t.CustomerID,");
        sb.AppendLine("        t.FirstName,");
        sb.AppendLine("        t.LastName,");
        sb.AppendLine("        o.OrderID,");
        sb.AppendLine("        o.OrderDate,");
        sb.AppendLine("        o.TotalAmount,");
        sb.AppendLine("        ROW_NUMBER() OVER (PARTITION BY t.CustomerID ORDER BY o.OrderDate DESC) AS RowSeq");
        sb.AppendLine($"    FROM dbo.[{tableName}] t WITH (NOLOCK)");
        sb.AppendLine("    INNER JOIN dbo.Orders o WITH (NOLOCK) ON t.CustomerID = o.CustomerID");
        sb.AppendLine("    WHERE o.OrderDate >= '2026-01-01' AND o.OrderDate < '2027-01-01'");
        sb.AppendLine("      AND o.Status = 'Completed'");
        sb.AppendLine(")");
        sb.AppendLine("SELECT CustomerID, FirstName, LastName, OrderID, OrderDate, TotalAmount");
        sb.AppendLine("FROM CTE_RankedItems");
        sb.AppendLine("WHERE RowSeq = 1");
        sb.AppendLine("OPTION (RECOMPILE, MAXDOP 4);");
        sb.AppendLine("GO");
        return sb.ToString();
    }

    private async Task<string> HandleMockDataAsync(
        ScriptRequestModel req, string tableName, string requirement, CancellationToken ct)
    {
        string systemPrompt = "You are a Database Test Data Generator. " +
            "Generate realistic, production-like mock data INSERT statements matching the columns and data types in the provided table schema. " +
            "Return executable T-SQL batches inside BEGIN TRANSACTION / COMMIT TRANSACTION.";

        string userPrompt = $"Generate 10 realistic mock data rows for table [{tableName}] with schema:\n\n{req.SqlInput}\n\nRequirements: {requirement}";

        var aiResult = await _ollamaClient.GenerateCompletionAsync(userPrompt, systemPrompt, ct);
        if (!string.IsNullOrWhiteSpace(aiResult))
            return aiResult;

        // Local Fallback
        var sb = new StringBuilder();
        sb.AppendLine($"-- ===========================================================================");
        sb.AppendLine($"-- Batch Mock Data Insertion for: dbo.[{tableName}]");
        sb.AppendLine($"-- Requirement: {requirement}");
        sb.AppendLine($"-- ===========================================================================");
        sb.AppendLine("SET NOCOUNT ON;");
        sb.AppendLine("BEGIN TRANSACTION;");
        sb.AppendLine();
        sb.AppendLine($"INSERT INTO dbo.[{tableName}] (FirstName, LastName, Email, CreatedAt)");
        sb.AppendLine("VALUES");
        sb.AppendLine("    (N'Liam', N'Smith', N'liam.smith@example.com', DATEADD(DAY, -1, GETUTCDATE())),");
        sb.AppendLine("    (N'Olivia', N'Johnson', N'olivia.j@example.com', DATEADD(DAY, -2, GETUTCDATE())),");
        sb.AppendLine("    (N'Noah', N'Williams', N'noah.w@example.com', DATEADD(DAY, -3, GETUTCDATE())),");
        sb.AppendLine("    (N'Emma', N'Brown', N'emma.brown@example.com', DATEADD(DAY, -4, GETUTCDATE())),");
        sb.AppendLine("    (N'James', N'Jones', N'james.jones@example.com', DATEADD(DAY, -5, GETUTCDATE()));");
        sb.AppendLine();
        sb.AppendLine("COMMIT TRANSACTION;");
        sb.AppendLine("PRINT 'Mock data batch executed successfully.';");
        sb.AppendLine("GO");
        return sb.ToString();
    }

    #endregion

    #region Helpers

    private static string FormatFragment(TSqlFragment fragment)
    {
        var generator = new Sql160ScriptGenerator(new SqlScriptGeneratorOptions
        {
            KeywordCasing = KeywordCasing.Uppercase,
            IncludeSemicolons = true,
            AlignClauseBodies = true
        });

        generator.GenerateScript(fragment, out string formatted);
        return formatted ?? string.Empty;
    }

    private static string? ExtractTableName(string sql)
    {
        var match = Regex.Match(sql, @"CREATE\s+TABLE\s+(?:dbo\.)?\[?([a-zA-Z0-9_]+)\]?", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string CleanAiOutput(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        // Strip ```sql and ``` markdown wrappers if returned by LLM
        var cleaned = Regex.Replace(raw, @"^```sql\s*", "", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"^```\s*", "", RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"\s*```$", "", RegexOptions.Multiline);
        return cleaned.Trim();
    }

    #endregion
}
