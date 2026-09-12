namespace SQLDatabaseScriptGenerator.Services;

public interface ILiveDatabaseInspectorService
{
    Task<List<string>> GetTableNamesAsync(string connectionString, CancellationToken cancellationToken = default);
    Task<string> GenerateTableDdlAsync(string connectionString, string tableName, CancellationToken cancellationToken = default);
}
