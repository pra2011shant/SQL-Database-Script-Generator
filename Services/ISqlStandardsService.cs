using SQLDatabaseScriptGenerator.Models;

namespace SQLDatabaseScriptGenerator.Services;

public interface ISqlStandardsService
{
    CorporateSqlStandards GetStandards();
    void UpdateStandards(CorporateSqlStandards standards);
    string BuildStandardsContextPrompt();
}
