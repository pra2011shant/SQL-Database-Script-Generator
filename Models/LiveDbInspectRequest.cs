namespace SQLDatabaseScriptGenerator.Models;

public class LiveDbInspectRequest
{
    public string ConnectionString { get; set; } = string.Empty;
    public string? TableName { get; set; }
}
