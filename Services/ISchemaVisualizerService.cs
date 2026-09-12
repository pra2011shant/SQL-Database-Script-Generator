namespace SQLDatabaseScriptGenerator.Services;

public interface ISchemaVisualizerService
{
    string GenerateMermaidErDiagram(string sqlScript);
}
