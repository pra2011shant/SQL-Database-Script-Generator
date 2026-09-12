using System.Text;
using System.Text.RegularExpressions;

namespace SQLDatabaseScriptGenerator.Services;

public class SchemaVisualizerService : ISchemaVisualizerService
{
    private static readonly Regex TableBlockRegex = new(
        @"CREATE\s+TABLE\s+(?:dbo\.)?\[?([a-zA-Z0-9_]+)\]?\s*\(([\s\S]*?)\)(?:;|\s*GO)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ColumnLineRegex = new(
        @"^\s*\[?([a-zA-Z0-9_]+)\]?\s+([a-zA-Z0-9_\(\)]+)(?:\s+IDENTITY(?:\([0-9,\s]+\))?)?(?:\s+(PRIMARY\s+KEY))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex FkRegex = new(
        @"FOREIGN\s+KEY\s*(?:\(\[?([a-zA-Z0-9_]+)\]?\))?\s*REFERENCES\s+(?:dbo\.)?\[?([a-zA-Z0-9_]+)\]?\s*(?:\(\[?([a-zA-Z0-9_]+)\]?\))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string GenerateMermaidErDiagram(string sqlScript)
    {
        if (string.IsNullOrWhiteSpace(sqlScript))
        {
            return "erDiagram\n    TARGET_TABLE {\n        int ID PK\n        string Name\n    }";
        }

        var sb = new StringBuilder();
        sb.AppendLine("erDiagram");

        var matches = TableBlockRegex.Matches(sqlScript);
        var relationships = new List<string>();

        if (matches.Count == 0)
        {
            sb.AppendLine("    CUSTOMERS ||--o{ ORDERS : places");
            sb.AppendLine("    CUSTOMERS {");
            sb.AppendLine("        int CustomerID PK");
            sb.AppendLine("        string FirstName");
            sb.AppendLine("        string Email");
            sb.AppendLine("    }");
            sb.AppendLine("    ORDERS {");
            sb.AppendLine("        int OrderID PK");
            sb.AppendLine("        int CustomerID FK");
            sb.AppendLine("        decimal TotalAmount");
            sb.AppendLine("    }");
            return sb.ToString();
        }

        foreach (Match tableMatch in matches)
        {
            var tableName = tableMatch.Groups[1].Value.ToUpperInvariant();
            var tableBody = tableMatch.Groups[2].Value;

            sb.AppendLine($"    {tableName} {{");

            using var reader = new StringReader(tableBody);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var trimmed = line.Trim().TrimEnd(',');
                if (trimmed.StartsWith("CONSTRAINT", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("--") || string.IsNullOrWhiteSpace(trimmed))
                {
                    // Check for FK constraints in table-level constraints
                    var fkMatch = FkRegex.Match(trimmed);
                    if (fkMatch.Success)
                    {
                        var targetTable = fkMatch.Groups[2].Value.ToUpperInvariant();
                        relationships.Add($"    {targetTable} ||--o{{ {tableName} : references");
                    }
                    continue;
                }

                // Check for inline FK
                var inlineFk = FkRegex.Match(trimmed);
                if (inlineFk.Success)
                {
                    var targetTable = inlineFk.Groups[2].Value.ToUpperInvariant();
                    relationships.Add($"    {targetTable} ||--o{{ {tableName} : references");
                }

                var colMatch = ColumnLineRegex.Match(trimmed);
                if (colMatch.Success)
                {
                    var colName = colMatch.Groups[1].Value;
                    var dataType = CleanType(colMatch.Groups[2].Value);
                    var isPk = trimmed.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) || colName.EndsWith("ID", StringComparison.OrdinalIgnoreCase) && !trimmed.Contains("FOREIGN", StringComparison.OrdinalIgnoreCase);
                    var isFk = trimmed.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("REFERENCES", StringComparison.OrdinalIgnoreCase);

                    var keyMarker = isPk ? "PK" : (isFk ? "FK" : "");
                    sb.AppendLine($"        {dataType} {colName} {keyMarker}".TrimEnd());
                }
            }

            sb.AppendLine("    }");
        }

        // Add relationships at the top
        if (relationships.Count > 0)
        {
            foreach (var rel in relationships.Distinct())
            {
                sb.Insert(sb.ToString().IndexOf('\n') + 1, rel + "\n");
            }
        }

        return sb.ToString();
    }

    private static string CleanType(string raw)
    {
        var upper = raw.ToUpperInvariant();
        if (upper.Contains("INT")) return "int";
        if (upper.Contains("VARCHAR") || upper.Contains("TEXT") || upper.Contains("CHAR")) return "string";
        if (upper.Contains("DECIMAL") || upper.Contains("NUMERIC") || upper.Contains("MONEY") || upper.Contains("FLOAT")) return "decimal";
        if (upper.Contains("DATE") || upper.Contains("TIME")) return "datetime";
        if (upper.Contains("BIT") || upper.Contains("BOOL")) return "boolean";
        if (upper.Contains("UUID") || upper.Contains("UNIQUEIDENTIFIER")) return "uuid";
        return "string";
    }
}
