namespace SQLDatabaseScriptGenerator.Models;

public class SqlActionResponse
{
    public bool IsValid { get; set; }
    public string ResultSql { get; set; } = string.Empty;
    public string FormattedSql { get; set; } = string.Empty;
    public int BatchCount { get; set; }
    public int StatementCount { get; set; }
    public List<SqlParseError> Errors { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}
