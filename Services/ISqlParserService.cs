using SQLDatabaseScriptGenerator.Models;

namespace SQLDatabaseScriptGenerator.Services;

public interface ISqlParserService
{
    SqlValidationResult ValidateAndParse(string sqlScript);
    string FormatSql(string sqlScript);
}
