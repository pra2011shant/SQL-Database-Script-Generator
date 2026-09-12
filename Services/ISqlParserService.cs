using SQLDatabaseScriptGenerator.Models;

namespace SQLDatabaseScriptGenerator.Services;

/// <summary>
/// Dedicated service for high-performance T-SQL AST parsing, syntax diagnostics, and script formatting.
/// </summary>
public interface ISqlParserService
{
    SqlValidationResult ValidateAndParse(string sqlScript);
    string FormatSql(string sqlScript);
    string? ExtractPrimaryTableName(string sqlScript);
    TableMetadata ExtractTableMetadata(string sqlScript);
}
