using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLDatabaseScriptGenerator.Models;

namespace SQLDatabaseScriptGenerator.Services;

public class SqlParserService : ISqlParserService
{
    public SqlValidationResult ValidateAndParse(string sqlScript)
    {
        var result = new SqlValidationResult();

        if (string.IsNullOrWhiteSpace(sqlScript))
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
        using var reader = new StringReader(sqlScript);
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

    public string FormatSql(string sqlScript)
    {
        if (string.IsNullOrWhiteSpace(sqlScript))
            return string.Empty;

        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sqlScript);
        var fragment = parser.Parse(reader, out IList<ParseError> errors);

        if (errors != null && errors.Count > 0)
            return sqlScript;

        return FormatFragment(fragment);
    }

    public async Task<ScriptResponseModel> ProcessSqlScriptAsync(ScriptRequestModel request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        return await Task.Run(() =>
        {
            var validation = ValidateAndParse(request.SqlInput);

            var response = new ScriptResponseModel
            {
                IsValid = validation.IsValid,
                Errors = validation.Errors,
                BatchCount = validation.BatchCount,
                StatementCount = validation.StatementCount,
                FormattedSql = validation.FormattedSql
            };

            var sb = new StringBuilder();
            var requirement = string.IsNullOrWhiteSpace(request.Requirement)
                ? "Standard T-SQL generation with best practices"
                : request.Requirement.Trim();

            // Extract table name if present in schema
            var tableName = ExtractTableName(request.SqlInput) ?? "TargetTable";

            switch (request.Action?.ToLowerInvariant())
            {
                case "create_sp":
                    GenerateStoredProcedures(sb, tableName, request, validation);
                    response.Recommendations.Add("Wrap DML operations in explicit transactions.");
                    response.Recommendations.Add("Use TRY...CATCH blocks and log errors with ERROR_MESSAGE().");
                    response.Recommendations.Add("Ensure SET NOCOUNT ON is declared to avoid unnecessary network traffic.");
                    break;

                case "create_indexes":
                    GenerateIndexes(sb, tableName, request);
                    response.Recommendations.Add("Place foreign key columns in non-clustered indexes.");
                    response.Recommendations.Add("Use INCLUDE columns for covering indexes to eliminate key lookups.");
                    response.Recommendations.Add("Consider PAGE data compression for large historical tables.");
                    break;

                case "debug_sql":
                    GenerateDebugReport(sb, request, validation);
                    if (!validation.IsValid)
                    {
                        response.Recommendations.Add("Review ScriptDom diagnostic line numbers to identify exact syntax issues.");
                        response.Recommendations.Add("Ensure all string literals use proper single quotes and brackets around reserved keywords.");
                    }
                    else
                    {
                        response.Recommendations.Add("AST parsing passed with 0 syntax errors.");
                    }
                    break;

                case "optimize_query":
                    GenerateOptimizedQuery(sb, tableName, requirement);
                    response.Recommendations.Add("Replace non-SARGable functions in WHERE clauses (e.g. YEAR(date) -> range check).");
                    response.Recommendations.Add("Use CTEs and Window Functions for deduplication and ranking.");
                    response.Recommendations.Add("Specify column lists rather than SELECT *.");
                    break;

                case "mock_data":
                    GenerateMockData(sb, tableName, requirement);
                    response.Recommendations.Add("Use multi-row VALUES clauses for bulk insertion performance.");
                    response.Recommendations.Add("Wrap large data seedings inside transactions.");
                    break;

                case "format_sql":
                default:
                    sb.Append(!string.IsNullOrWhiteSpace(validation.FormattedSql) ? validation.FormattedSql : request.SqlInput);
                    response.Recommendations.Add("Keywords formatted to UPPERCASE with semicolon termination.");
                    break;
            }

            stopwatch.Stop();
            response.ResultSql = sb.ToString();
            response.ExecutionTimeMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2);
            response.Message = validation.IsValid ? "SQL script processed successfully." : "Syntax issues detected in input SQL.";

            return response;
        }, cancellationToken);
    }

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

    private static void GenerateStoredProcedures(StringBuilder sb, string tableName, ScriptRequestModel req, SqlValidationResult validation)
    {
        sb.AppendLine($"-- ===========================================================================");
        sb.AppendLine($"-- Stored Procedures for: dbo.[{tableName}]");
        sb.AppendLine($"-- Requirement: {req.Requirement}");
        sb.AppendLine($"-- Generated On: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"-- Target Engine: {req.DatabaseEngine}");
        sb.AppendLine($"-- ===========================================================================");
        sb.AppendLine("SET NOCOUNT ON;");
        sb.AppendLine("SET XACT_ABORT ON;");
        sb.AppendLine("GO\n");

        // 1. GET By ID Procedure
        sb.AppendLine($"CREATE OR ALTER PROCEDURE dbo.usp_{tableName}_GetByID");
        sb.AppendLine($"    @ID INT");
        sb.AppendLine($"AS");
        sb.AppendLine($"BEGIN");
        sb.AppendLine($"    SET NOCOUNT ON;");
        sb.AppendLine($"    SELECT * FROM dbo.[{tableName}] WITH (NOLOCK)");
        sb.AppendLine($"    WHERE [{tableName}ID] = @ID;");
        sb.AppendLine($"END;");
        sb.AppendLine("GO\n");

        // 2. Search / List with Pagination Procedure
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
        sb.AppendLine($"    OFFSET @Offset ROWS");
        sb.AppendLine($"    FETCH NEXT @PageSize ROWS ONLY;");
        sb.AppendLine($"END;");
        sb.AppendLine("GO\n");

        // 3. Upsert / Save Procedure with Transaction & Try/Catch
        sb.AppendLine($"CREATE OR ALTER PROCEDURE dbo.usp_{tableName}_Save");
        sb.AppendLine($"    @ID INT = NULL OUTPUT,");
        sb.AppendLine($"    @UpdatedBy NVARCHAR(100) = 'System'");
        sb.AppendLine($"AS");
        sb.AppendLine($"BEGIN");
        sb.AppendLine($"    SET NOCOUNT ON;");
        sb.AppendLine($"    SET XACT_ABORT ON;");
        sb.AppendLine();
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
            sb.AppendLine("        IF (XACT_STATE() <> 0)");
            sb.AppendLine("            ROLLBACK TRANSACTION;");
            sb.AppendLine();
            sb.AppendLine("        DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();");
            sb.AppendLine("        DECLARE @ErrSeverity INT = ERROR_SEVERITY();");
            sb.AppendLine("        DECLARE @ErrState INT = ERROR_STATE();");
            sb.AppendLine("        RAISERROR(@ErrMsg, @ErrSeverity, @ErrState);");
            sb.AppendLine("    END CATCH;");
        }
        sb.AppendLine($"END;");
        sb.AppendLine("GO");
    }

    private static void GenerateIndexes(StringBuilder sb, string tableName, ScriptRequestModel req)
    {
        sb.AppendLine($"-- ===========================================================================");
        sb.AppendLine($"-- Index Recommendations for: dbo.[{tableName}]");
        sb.AppendLine($"-- ===========================================================================");
        sb.AppendLine();
        sb.AppendLine($"-- 1. Foreign Key Index");
        sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_{tableName}_CustomerID' AND object_id = OBJECT_ID('dbo.[{tableName}]'))");
        sb.AppendLine($"BEGIN");
        sb.AppendLine($"    CREATE NONCLUSTERED INDEX IX_{tableName}_CustomerID");
        sb.AppendLine($"    ON dbo.[{tableName}] (CustomerID)");
        sb.AppendLine($"    WITH (ONLINE = ON, FILLFACTOR = 90{(req.UsePageCompression ? ", DATA_COMPRESSION = PAGE" : "")});");
        sb.AppendLine($"END;");
        sb.AppendLine("GO\n");

        sb.AppendLine($"-- 2. Covering Composite Index for Search & Date Filtering");
        sb.AppendLine($"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_{tableName}_Status_CreatedAt' AND object_id = OBJECT_ID('dbo.[{tableName}]'))");
        sb.AppendLine($"BEGIN");
        sb.AppendLine($"    CREATE NONCLUSTERED INDEX IX_{tableName}_Status_CreatedAt");
        sb.AppendLine($"    ON dbo.[{tableName}] (Status, CreatedAt DESC)");
        sb.AppendLine($"    INCLUDE (TotalAmount)");
        sb.AppendLine($"    WITH (ONLINE = ON, FILLFACTOR = 85{(req.UsePageCompression ? ", DATA_COMPRESSION = PAGE" : "")});");
        sb.AppendLine($"END;");
        sb.AppendLine("GO");
    }

    private static void GenerateDebugReport(StringBuilder sb, ScriptRequestModel req, SqlValidationResult validation)
    {
        if (!validation.IsValid)
        {
            sb.AppendLine($"-- ===========================================================================");
            sb.AppendLine($"-- SCRIPT DOM DIAGNOSTIC REPORT: {validation.Errors.Count} SYNTAX ERROR(S)");
            sb.AppendLine($"-- ===========================================================================");
            foreach (var err in validation.Errors)
            {
                sb.AppendLine($"-- [Line {err.Line}, Column {err.Column}] T-SQL Error #{err.ErrorCode}: {err.Message}");
            }
            sb.AppendLine("\n-- Parsed or Formatted Output Attempt:");
        }
        else
        {
            sb.AppendLine($"-- ===========================================================================");
            sb.AppendLine($"-- SYNTAX VALIDATION: SUCCESS");
            sb.AppendLine($"-- Batches: {validation.BatchCount} | Statements: {validation.StatementCount}");
            sb.AppendLine($"-- ===========================================================================");
        }
        sb.AppendLine(validation.FormattedSql);
    }

    private static void GenerateOptimizedQuery(StringBuilder sb, string tableName, string requirement)
    {
        sb.AppendLine($"-- ===========================================================================");
        sb.AppendLine($"-- Optimized Query with Common Table Expressions (CTE) & SARGable Predicates");
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
    }

    private static void GenerateMockData(StringBuilder sb, string tableName, string requirement)
    {
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
    }
}
