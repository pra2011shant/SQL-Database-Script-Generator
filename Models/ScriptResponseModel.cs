using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace SQLDatabaseScriptGenerator.Models;

/// <summary>
/// Data Transfer Object for returning generated SQL scripts, diagnostics, metrics, and multi-engine transpilations.
/// </summary>
public class ScriptResponseModel
{
    [JsonProperty("success")]
    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;

    [JsonProperty("isValid")]
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; } = true;

    [JsonProperty("resultSql")]
    [JsonPropertyName("resultSql")]
    public string ResultSql { get; set; } = string.Empty;

    [JsonProperty("correctedScript")]
    [JsonPropertyName("correctedScript")]
    public string CorrectedScript { get; set; } = string.Empty;

    [JsonProperty("optimizedScript")]
    [JsonPropertyName("optimizedScript")]
    public string OptimizedScript { get; set; } = string.Empty;

    [JsonProperty("explanation")]
    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = string.Empty;

    [JsonProperty("diagnosis")]
    [JsonPropertyName("diagnosis")]
    public string Diagnosis { get; set; } = string.Empty;

    [JsonProperty("formattedSql")]
    [JsonPropertyName("formattedSql")]
    public string FormattedSql { get; set; } = string.Empty;

    [JsonProperty("mermaidErDiagram")]
    [JsonPropertyName("mermaidErDiagram")]
    public string MermaidErDiagram { get; set; } = string.Empty;

    [JsonProperty("postgresSql")]
    [JsonPropertyName("postgresSql")]
    public string PostgresSql { get; set; } = string.Empty;

    [JsonProperty("mySql")]
    [JsonPropertyName("mySql")]
    public string MySql { get; set; } = string.Empty;

    [JsonProperty("oracleSql")]
    [JsonPropertyName("oracleSql")]
    public string OracleSql { get; set; } = string.Empty;

    [JsonProperty("batchCount")]
    [JsonPropertyName("batchCount")]
    public int BatchCount { get; set; }

    [JsonProperty("statementCount")]
    [JsonPropertyName("statementCount")]
    public int StatementCount { get; set; }

    [JsonProperty("errors")]
    [JsonPropertyName("errors")]
    public List<SqlParseError> Errors { get; set; } = new();

    [JsonProperty("executionTimeMs")]
    [JsonPropertyName("executionTimeMs")]
    public double ExecutionTimeMs { get; set; }

    [JsonProperty("message")]
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("recommendations")]
    [JsonPropertyName("recommendations")]
    public List<string> Recommendations { get; set; } = new();
}
