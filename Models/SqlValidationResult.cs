namespace SQLDatabaseScriptGenerator.Models;

public class SqlValidationResult
{
    public bool IsValid { get; set; }
    public List<SqlParseError> Errors { get; set; } = new();
    public string FormattedSql { get; set; } = string.Empty;
    public int BatchCount { get; set; }
    public int StatementCount { get; set; }
}
