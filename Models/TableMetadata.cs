namespace SQLDatabaseScriptGenerator.Models;

public class ColumnMetadata
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = "NVARCHAR(100)";
    public bool IsIdentity { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsNullable { get; set; } = true;
    public string? DefaultValue { get; set; }
}

public class TableMetadata
{
    public string TableName { get; set; } = "TargetTable";
    public string Schema { get; set; } = "dbo";
    public string PrimaryKeyColumn { get; set; } = "ID";
    public List<ColumnMetadata> Columns { get; set; } = new();
}
