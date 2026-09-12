namespace SQLDatabaseScriptGenerator.Models;

public class SqlActionRequest
{
    public string SqlInput { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Requirement { get; set; }
}
