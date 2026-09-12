using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLDatabaseScriptGenerator.Models;

namespace SQLDatabaseScriptGenerator.Services;

/// <summary>
/// High-performance implementation of ISqlParserService utilizing Microsoft ScriptDom.
/// Dynamic, generic, and supports both formal SQL and natural language prompt parsing.
/// </summary>
public class SqlParserService : ISqlParserService
{
    private static readonly SqlScriptGeneratorOptions GeneratorOptions = new()
    {
        KeywordCasing = KeywordCasing.Uppercase,
        IncludeSemicolons = true,
        AlignClauseBodies = true,
        MultilineSelectElementsList = false
    };

    private static readonly Regex TableNameRegex = new(
        @"CREATE\s+TABLE\s+(?:dbo\.)?\[?([a-zA-Z0-9_]+)\]?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ColumnDefRegex = new(
        @"\[?([a-zA-Z0-9_]+)\]?\s+([a-zA-Z0-9_\(\)]+)(?:\s+(IDENTITY(?:\([0-9,\s]+\))?))?(?:\s+(PRIMARY\s+KEY))?(?:\s+(NOT\s+NULL|NULL))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ValidSqlDataTypeRegex = new(
        @"^(INT|BIGINT|SMALLINT|TINYINT|BIT|DECIMAL(?:\(\d+(?:,\s*\d+)?\))?|NUMERIC(?:\(\d+(?:,\s*\d+)?\))?|MONEY|SMALLMONEY|FLOAT(?:\(\d+\))?|REAL|DATE|DATETIME|DATETIME2(?:\(\d+\))?|DATETIMEOFFSET(?:\(\d+\))?|TIME(?:\(\d+\))?|CHAR(?:\(\d+\))?|VARCHAR(?:\(\d+|MAX\))?|TEXT|NCHAR(?:\(\d+\))?|NVARCHAR(?:\(\d+|MAX\))?|NTEXT|BINARY(?:\(\d+\))?|VARBINARY(?:\(\d+|MAX\))?|IMAGE|UNIQUEIDENTIFIER|XML|ROWVERSION)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public SqlValidationResult ValidateAndParse(string sqlScript)
    {
        var result = new SqlValidationResult();

        if (string.IsNullOrWhiteSpace(sqlScript))
        {
            result.IsValid = false;
            result.Errors.Add(new SqlParseError
            {
                Line = 0,
                Column = 0,
                Message = "Input SQL script is empty."
            });
            return result;
        }

        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sqlScript);
        var fragment = parser.Parse(reader, out IList<ParseError> errors);

        if (errors != null && errors.Count > 0)
        {
            result.IsValid = false;
            foreach (var error in errors)
            {
                result.Errors.Add(new SqlParseError
                {
                    Line = error.Line,
                    Column = error.Column,
                    Message = error.Message,
                    ErrorCode = error.Number
                });
            }
            return result;
        }

        result.IsValid = true;

        if (fragment is TSqlScript script)
        {
            result.BatchCount = script.Batches?.Count ?? 0;
            result.StatementCount = 0;
            if (script.Batches != null)
            {
                foreach (var batch in script.Batches)
                {
                    result.StatementCount += batch.Statements?.Count ?? 0;
                }
            }
        }

        result.FormattedSql = GenerateScriptFromFragment(fragment);
        return result;
    }

    public string FormatSql(string sqlScript)
    {
        if (string.IsNullOrWhiteSpace(sqlScript))
            return string.Empty;

        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(sqlScript);
        var fragment = parser.Parse(reader, out IList<ParseError> errors);

        if (errors != null && errors.Count > 0)
            return sqlScript;

        return GenerateScriptFromFragment(fragment);
    }

    public string? ExtractPrimaryTableName(string sqlScript)
    {
        if (string.IsNullOrWhiteSpace(sqlScript))
            return null;

        var match = TableNameRegex.Match(sqlScript);
        return match.Success ? match.Groups[1].Value : null;
    }

    public TableMetadata ExtractTableMetadata(string sqlScript)
    {
        var meta = new TableMetadata();
        if (string.IsNullOrWhiteSpace(sqlScript))
            return meta;

        var tableMatch = TableNameRegex.Match(sqlScript);
        if (tableMatch.Success)
        {
            meta.TableName = tableMatch.Groups[1].Value;
            meta.PrimaryKeyColumn = $"{meta.TableName}ID";
        }
        else
        {
            // Dynamically extract table entity name from natural language prompt
            meta.TableName = ExtractTableNameFromNaturalPrompt(sqlScript);
            meta.PrimaryKeyColumn = $"{meta.TableName}ID";
        }

        // Parse column definitions dynamically from SQL text
        using var reader = new StringReader(sqlScript);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim().TrimEnd(',');
            if (trimmed.StartsWith("--") || trimmed.StartsWith("/*") || 
                trimmed.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase) || 
                trimmed.StartsWith("CONSTRAINT", StringComparison.OrdinalIgnoreCase) || 
                trimmed.StartsWith(")", StringComparison.OrdinalIgnoreCase) || 
                string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            var colMatch = ColumnDefRegex.Match(trimmed);
            if (colMatch.Success)
            {
                var colName = colMatch.Groups[1].Value;
                var dataType = colMatch.Groups[2].Value.ToUpperInvariant();

                // Validate that extracted dataType is a legitimate SQL data type
                if (!ValidSqlDataTypeRegex.IsMatch(dataType))
                {
                    continue;
                }

                // Filter out SQL constraint identifiers
                if (colName.Equals("PRIMARY", StringComparison.OrdinalIgnoreCase) ||
                    colName.Equals("CONSTRAINT", StringComparison.OrdinalIgnoreCase) ||
                    colName.Equals("FOREIGN", StringComparison.OrdinalIgnoreCase) ||
                    colName.Equals("CHECK", StringComparison.OrdinalIgnoreCase) ||
                    colName.Equals("UNIQUE", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var isIdentity = trimmed.Contains("IDENTITY", StringComparison.OrdinalIgnoreCase);
                var isPk = trimmed.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) || 
                           colName.Equals(meta.PrimaryKeyColumn, StringComparison.OrdinalIgnoreCase);

                if (isPk)
                {
                    meta.PrimaryKeyColumn = colName;
                }

                meta.Columns.Add(new ColumnMetadata
                {
                    Name = colName,
                    DataType = dataType,
                    IsIdentity = isIdentity,
                    IsPrimaryKey = isPk,
                    IsNullable = !trimmed.Contains("NOT NULL", StringComparison.OrdinalIgnoreCase)
                });
            }
        }

        // Dynamic attribute extraction from natural language if no formal SQL columns were matched
        if (meta.Columns.Count == 0)
        {
            ExtractDynamicColumnsFromPrompt(sqlScript, meta);
        }

        return meta;
    }

    private static string ExtractTableNameFromNaturalPrompt(string text)
    {
        // Pattern 1: "table name [rhega/hoga/is/as] <Name>" or "table named <Name>" or "table <Name>"
        var matches1 = Regex.Matches(text, @"table\s+(?:name\s+(?:rhega|hoga|rakhna|is|as|be)?|named|of|for)?\s*([a-zA-Z0-9_]+)", RegexOptions.IgnoreCase);
        foreach (Match m in matches1)
        {
            var val = m.Groups[1].Value;
            if (IsValidIdentifier(val)) return FormatIdentifierName(val);
        }

        // Pattern 2: "<Name> table name [rhega/hoga]" (e.g. "mstschool table name rhegaa")
        var matches2 = Regex.Matches(text, @"([a-zA-Z0-9_]+)\s+table\s+name", RegexOptions.IgnoreCase);
        foreach (Match m in matches2)
        {
            var val = m.Groups[1].Value;
            if (IsValidIdentifier(val)) return FormatIdentifierName(val);
        }

        // Pattern 3: "create [a/an] <Name> table"
        var matches3 = Regex.Matches(text, @"(?:create|make|generate|build|crete)\s+(?:a\s+|an\s+)?([a-zA-Z0-9_]+)\s+table", RegexOptions.IgnoreCase);
        foreach (Match m in matches3)
        {
            var val = m.Groups[1].Value;
            if (IsValidIdentifier(val)) return FormatIdentifierName(val);
        }

        // Pattern 4: "<Name> [ka/ki/ke/ko] [ek] table" (Hindi / Hinglish)
        var matches4 = Regex.Matches(text, @"([a-zA-Z0-9_]+)\s+(?:ka|ki|ke|ko)\s+(?:ek\s+)?(?:table|schema)", RegexOptions.IgnoreCase);
        foreach (Match m in matches4)
        {
            var val = m.Groups[1].Value;
            if (IsValidIdentifier(val)) return FormatIdentifierName(val);
        }

        // Pattern 5: "<Name> table"
        var matches5 = Regex.Matches(text, @"([a-zA-Z0-9_]+)\s+table", RegexOptions.IgnoreCase);
        foreach (Match m in matches5)
        {
            var val = m.Groups[1].Value;
            if (IsValidIdentifier(val)) return FormatIdentifierName(val);
        }

        return "TargetTable";
    }

    private static void ExtractDynamicColumnsFromPrompt(string text, TableMetadata meta)
    {
        meta.Columns.Add(new ColumnMetadata { Name = meta.PrimaryKeyColumn, DataType = "INT", IsPrimaryKey = true, IsIdentity = true, IsNullable = false });

        var lower = text.ToLowerInvariant();
        var addedCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void TryAdd(string colName, string dataType, bool nullable)
        {
            if (addedCols.Add(colName))
            {
                meta.Columns.Add(new ColumnMetadata
                {
                    Name = colName,
                    DataType = dataType,
                    IsNullable = nullable
                });
            }
        }

        // 1. Extract explicitly mentioned custom columns / snake_case or comma-separated tokens (e.g. vill_code,dis_code)
        var explicitMatches = Regex.Matches(text, @"(?:[a-zA-Z0-9_]+_[a-zA-Z0-9_]+|[a-zA-Z0-9_]+(?:\s*,\s*[a-zA-Z0-9_]+)+)");
        foreach (Match match in explicitMatches)
        {
            var parts = match.Value.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var cleanPart = part.Trim().Trim('[', ']', '(', ')', ';', ':');
                if (cleanPart.Length > 1 && IsValidIdentifier(cleanPart) && !cleanPart.Equals(meta.TableName, StringComparison.OrdinalIgnoreCase))
                {
                    var inferredType = InferDataTypeFromColumnName(cleanPart);
                    TryAdd(cleanPart, inferredType, false);
                }
            }
        }

        // 2. Generic keyword attribute matching across languages
        if (lower.Contains("school") || lower.Contains("college") || lower.Contains("institute"))
        {
            TryAdd("SchoolName", "NVARCHAR(150)", false);
            TryAdd("SchoolCode", "VARCHAR(50)", false);
            TryAdd("PrincipalName", "NVARCHAR(100)", true);
            TryAdd("AffiliationNumber", "VARCHAR(50)", true);
        }

        if (lower.Contains("name") || lower.Contains("naam"))
        {
            if (!addedCols.Contains("SchoolName"))
            {
                TryAdd("FirstName", "NVARCHAR(50)", false);
                TryAdd("LastName", "NVARCHAR(50)", false);
            }
        }

        if (lower.Contains("roll") || lower.Contains("rollno") || lower.Contains("roll_no"))
        {
            TryAdd("RollNumber", "INT", false);
        }

        if (lower.Contains("email") || lower.Contains("mail"))
        {
            TryAdd("Email", "NVARCHAR(100)", false);
        }

        if (lower.Contains("phone") || lower.Contains("mobile") || lower.Contains("contact"))
        {
            TryAdd("PhoneNumber", "VARCHAR(20)", true);
        }

        if (lower.Contains("address") || lower.Contains("pata") || lower.Contains("location"))
        {
            TryAdd("AddressLine1", "NVARCHAR(200)", false);
            TryAdd("AddressLine2", "NVARCHAR(200)", true);
            TryAdd("City", "NVARCHAR(100)", false);
            TryAdd("State", "NVARCHAR(100)", false);
            TryAdd("PostalCode", "VARCHAR(20)", true);
            TryAdd("Country", "NVARCHAR(100)", false);
        }

        if (lower.Contains("marks") || lower.Contains("score") || lower.Contains("grade"))
        {
            TryAdd("Marks", "DECIMAL(5,2)", true);
            TryAdd("Grade", "VARCHAR(5)", true);
        }

        if (lower.Contains("salary") || lower.Contains("price") || lower.Contains("amount") || lower.Contains("fee") || lower.Contains("cost"))
        {
            TryAdd("Amount", "DECIMAL(18,2)", false);
        }

        if (lower.Contains("dob") || lower.Contains("birth"))
        {
            TryAdd("DateOfBirth", "DATE", false);
        }

        if (lower.Contains("dept") || lower.Contains("department"))
        {
            TryAdd("Department", "NVARCHAR(100)", true);
        }

        if (lower.Contains("gender") || lower.Contains("sex"))
        {
            TryAdd("Gender", "VARCHAR(10)", true);
        }

        // If no attributes matched, add clean generic columns
        if (meta.Columns.Count == 1)
        {
            TryAdd("Code", "NVARCHAR(50)", false);
            TryAdd("Name", "NVARCHAR(150)", false);
            TryAdd("Description", "NVARCHAR(500)", true);
            TryAdd("Status", "VARCHAR(30)", false);
        }
    }

    private static string InferDataTypeFromColumnName(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.EndsWith("_id") || lower.EndsWith("id") || lower.EndsWith("_no") || lower.EndsWith("_num") || lower.EndsWith("_count") || lower.EndsWith("_age") || lower.Equals("rollno"))
            return "INT";
        if (lower.EndsWith("_code") || lower.EndsWith("code") || lower.EndsWith("_status") || lower.EndsWith("_type") || lower.EndsWith("_key"))
            return "VARCHAR(50)";
        if (lower.EndsWith("_amount") || lower.EndsWith("_amt") || lower.EndsWith("_price") || lower.EndsWith("_salary") || lower.EndsWith("_fee") || lower.EndsWith("_tax") || lower.EndsWith("_rate") || lower.EndsWith("_cost") || lower.EndsWith("_marks"))
            return "DECIMAL(18,2)";
        if (lower.EndsWith("_date") || lower.EndsWith("_dt") || lower.EndsWith("_dob") || lower.EndsWith("_time"))
            return "DATE";
        if (lower.StartsWith("is_") || lower.StartsWith("has_") || lower.EndsWith("_flag") || lower.EndsWith("_active") || lower.EndsWith("_status_bit"))
            return "BIT";
        if (lower.EndsWith("_desc") || lower.EndsWith("_description") || lower.EndsWith("_remarks") || lower.EndsWith("_note") || lower.EndsWith("_details") || lower.EndsWith("_address"))
            return "NVARCHAR(500)";
        return "NVARCHAR(100)";
    }

    private static bool IsValidIdentifier(string word)
    {
        var ignoreWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "ek", "please", "hii", "hello", "new", "simple", "basic", "custom", "bnao", "bnaoo", "krna", "or", "and", "me", "esme", "ye", "column", "columns", "cahiye", "jo", "usme", "ka", "ki", "ke", "ko", "name", "naam", "rhega", "rhegaa", "hoga", "rakhna", "table", "schema", "mujhe"
        };
        return !string.IsNullOrWhiteSpace(word) && !ignoreWords.Contains(word) && word.Length > 1;
    }

    private static string FormatIdentifierName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "TargetTable";
        var clean = Regex.Replace(raw, @"[^a-zA-Z0-9_]", "");
        if (string.IsNullOrEmpty(clean)) return "TargetTable";
        return clean;
    }

    private static string GenerateScriptFromFragment(TSqlFragment fragment)
    {
        var generator = new Sql160ScriptGenerator(GeneratorOptions);
        generator.GenerateScript(fragment, out string formatted);
        return formatted ?? string.Empty;
    }
}
