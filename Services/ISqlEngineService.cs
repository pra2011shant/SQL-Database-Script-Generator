using SQLDatabaseScriptGenerator.Models;

namespace SQLDatabaseScriptGenerator.Services;

public interface ISqlEngineService
{
    Task<ScriptResponseModel> ProcessAsync(ScriptRequestModel request, CancellationToken cancellationToken = default);
    SqlValidationResult ValidateSql(string sql);
    string FormatSql(string sql);
}
