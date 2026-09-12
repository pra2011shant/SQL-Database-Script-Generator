namespace SQLDatabaseScriptGenerator.Services;

public interface ISqlTemplateService
{
    Dictionary<string, string> GetAllTemplates();
    string? GetTemplateByKey(string key);
}
