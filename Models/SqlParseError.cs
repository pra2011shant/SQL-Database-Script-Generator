namespace SQLDatabaseScriptGenerator.Models;

public class SqlParseError
{
    public int Line { get; set; }
    public int Column { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ErrorCode { get; set; }
}
