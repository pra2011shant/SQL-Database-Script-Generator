namespace SQLDatabaseScriptGenerator.Services;

public interface ISqlTranspilerService
{
    string TranspileToPostgreSql(string tsql);
    string TranspileToMySql(string tsql);
    string TranspileToOracle(string tsql);
}
