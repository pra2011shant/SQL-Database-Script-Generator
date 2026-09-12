using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;

namespace SQLDatabaseScriptGenerator.Services;

public class LiveDatabaseInspectorService : ILiveDatabaseInspectorService
{
    private readonly ILogger<LiveDatabaseInspectorService> _logger;

    public LiveDatabaseInspectorService(ILogger<LiveDatabaseInspectorService> logger)
    {
        _logger = logger;
    }

    public async Task<List<string>> GetTableNamesAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var tables = new List<string>();

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                ConnectTimeout = 8,
                ApplicationIntent = ApplicationIntent.ReadOnly
            };

            using var conn = new SqlConnection(builder.ConnectionString);
            await conn.OpenAsync(cancellationToken);

            const string sql = @"
                SELECT TABLE_SCHEMA + '.' + TABLE_NAME AS FullTableName
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_SCHEMA, TABLE_NAME;";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add(reader.GetString(0));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to inspect live tables: {Message}", ex.Message);
            throw;
        }

        return tables;
    }

    public async Task<string> GenerateTableDdlAsync(string connectionString, string tableName, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                ConnectTimeout = 8,
                ApplicationIntent = ApplicationIntent.ReadOnly
            };

            using var conn = new SqlConnection(builder.ConnectionString);
            await conn.OpenAsync(cancellationToken);

            var cleanName = tableName.Contains('.') ? tableName.Split('.')[1] : tableName;
            var schemaName = tableName.Contains('.') ? tableName.Split('.')[0] : "dbo";

            const string colSql = @"
                SELECT 
                    COLUMN_NAME, 
                    DATA_TYPE, 
                    CHARACTER_MAXIMUM_LENGTH, 
                    NUMERIC_PRECISION, 
                    NUMERIC_SCALE, 
                    IS_NULLABLE,
                    COLUMN_DEFAULT
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Table
                ORDER BY ORDINAL_POSITION;";

            using var cmd = new SqlCommand(colSql, conn);
            cmd.Parameters.AddWithValue("@Schema", schemaName);
            cmd.Parameters.AddWithValue("@Table", cleanName);

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            sb.AppendLine($"-- Live Extracted DDL for: [{schemaName}].[{cleanName}]");
            sb.AppendLine($"CREATE TABLE [{schemaName}].[{cleanName}] (");

            var first = true;
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!first) sb.AppendLine(",");
                first = false;

                var col = reader.GetString(0);
                var type = reader.GetString(1).ToUpperInvariant();
                var maxLen = reader.IsDBNull(2) ? -1 : reader.GetInt32(2);
                var isNull = reader.GetString(5).Equals("YES", StringComparison.OrdinalIgnoreCase);

                var typeStr = type;
                if (type.Contains("CHAR") || type.Contains("BINARY"))
                {
                    typeStr += (maxLen == -1) ? "(MAX)" : $"({maxLen})";
                }
                else if (type.Equals("DECIMAL") || type.Equals("NUMERIC"))
                {
                    var prec = reader.IsDBNull(3) ? 18 : Convert.ToInt32(reader[3]);
                    var scale = reader.IsDBNull(4) ? 2 : Convert.ToInt32(reader[4]);
                    typeStr += $"({prec},{scale})";
                }

                sb.Append($"    [{col}] {typeStr} {(isNull ? "NULL" : "NOT NULL")}");
            }

            sb.AppendLine();
            sb.AppendLine(");");
            sb.AppendLine("GO");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate live table DDL.");
            throw;
        }

        return sb.ToString();
    }
}
