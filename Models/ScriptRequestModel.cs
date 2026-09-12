using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace SQLDatabaseScriptGenerator.Models;

/// <summary>
/// Data Transfer Object for client SQL generation and processing requests.
/// </summary>
public class ScriptRequestModel
{
    [Required(ErrorMessage = "SQL input is required.")]
    [JsonProperty("sqlInput")]
    [JsonPropertyName("sqlInput")]
    public string SqlInput { get; set; } = string.Empty;

    [Required(ErrorMessage = "Target action must be specified.")]
    [JsonProperty("action")]
    [JsonPropertyName("action")]
    public string Action { get; set; } = "create_sp";

    [JsonProperty("requirement")]
    [JsonPropertyName("requirement")]
    public string? Requirement { get; set; }

    [JsonProperty("databaseEngine")]
    [JsonPropertyName("databaseEngine")]
    public string DatabaseEngine { get; set; } = "SqlServer2022";

    [JsonProperty("includeTryCatch")]
    [JsonPropertyName("includeTryCatch")]
    public bool IncludeTryCatch { get; set; } = true;

    [JsonProperty("includeTransactions")]
    [JsonPropertyName("includeTransactions")]
    public bool IncludeTransactions { get; set; } = true;

    [JsonProperty("includeComments")]
    [JsonPropertyName("includeComments")]
    public bool IncludeComments { get; set; } = true;

    [JsonProperty("usePageCompression")]
    [JsonPropertyName("usePageCompression")]
    public bool UsePageCompression { get; set; } = false;
}
