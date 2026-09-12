using System.IO;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLDatabaseScriptGenerator.Models;

namespace SQLDatabaseScriptGenerator.Services;

/// <summary>
/// High-performance implementation of ISqlParserService utilizing Microsoft ScriptDom.
/// Thread-safe and optimized to minimize memory allocations.
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
            // Detect natural language keywords if no CREATE TABLE statement was found
            DetectEntityFromPrompt(sqlScript, meta);
        }

        // Parse column definitions if valid SQL structure
        using var reader = new StringReader(sqlScript);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim().TrimEnd(',');
            if (trimmed.StartsWith("--") || trimmed.StartsWith("/*") || trimmed.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("CONSTRAINT", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith(")", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            var colMatch = ColumnDefRegex.Match(trimmed);
            if (colMatch.Success)
            {
                var colName = colMatch.Groups[1].Value;
                var dataType = colMatch.Groups[2].Value.ToUpperInvariant();

                // Ensure data type is a recognized SQL type and not natural language words
                if (!ValidSqlDataTypeRegex.IsMatch(dataType))
                {
                    continue;
                }

                // Skip constraint keywords if captured
                if (colName.Equals("PRIMARY", StringComparison.OrdinalIgnoreCase) ||
                    colName.Equals("CONSTRAINT", StringComparison.OrdinalIgnoreCase) ||
                    colName.Equals("FOREIGN", StringComparison.OrdinalIgnoreCase) ||
                    colName.Equals("CHECK", StringComparison.OrdinalIgnoreCase) ||
                    colName.Equals("UNIQUE", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var isIdentity = trimmed.Contains("IDENTITY", StringComparison.OrdinalIgnoreCase);
                var isPk = trimmed.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) || colName.Equals(meta.PrimaryKeyColumn, StringComparison.OrdinalIgnoreCase);

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

        // Fallback rich domain attributes if no valid SQL columns were extracted
        if (meta.Columns.Count == 0)
        {
            PopulateDomainColumns(meta, sqlScript);
        }

        return meta;
    }

    private static void DetectEntityFromPrompt(string text, TableMetadata meta)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("teacher") || lower.Contains("faculty") || lower.Contains("professor") || lower.Contains("instructor"))
        {
            meta.TableName = "Teachers";
            meta.PrimaryKeyColumn = "TeacherID";
        }
        else if (lower.Contains("student") || lower.Contains("pupil"))
        {
            meta.TableName = "Students";
            meta.PrimaryKeyColumn = "StudentID";
        }
        else if (lower.Contains("employee") || lower.Contains("staff") || lower.Contains("worker"))
        {
            meta.TableName = "Employees";
            meta.PrimaryKeyColumn = "EmployeeID";
        }
        else if (lower.Contains("product") || lower.Contains("item") || lower.Contains("goods"))
        {
            meta.TableName = "Products";
            meta.PrimaryKeyColumn = "ProductID";
        }
        else if (lower.Contains("customer") || lower.Contains("client") || lower.Contains("buyer"))
        {
            meta.TableName = "Customers";
            meta.PrimaryKeyColumn = "CustomerID";
        }
        else if (lower.Contains("order") || lower.Contains("invoice"))
        {
            meta.TableName = "Orders";
            meta.PrimaryKeyColumn = "OrderID";
        }
        else
        {
            meta.TableName = "TargetTable";
            meta.PrimaryKeyColumn = "TargetTableID";
        }
    }

    private static void PopulateDomainColumns(TableMetadata meta, string text)
    {
        var lower = text.ToLowerInvariant();

        meta.Columns.Add(new ColumnMetadata { Name = meta.PrimaryKeyColumn, DataType = "INT", IsPrimaryKey = true, IsIdentity = true, IsNullable = false });

        if (meta.TableName.Equals("Teachers", StringComparison.OrdinalIgnoreCase))
        {
            meta.Columns.Add(new ColumnMetadata { Name = "FirstName", DataType = "NVARCHAR(50)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "LastName", DataType = "NVARCHAR(50)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "Email", DataType = "NVARCHAR(100)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "PhoneNumber", DataType = "NVARCHAR(20)", IsNullable = true });
            meta.Columns.Add(new ColumnMetadata { Name = "SubjectSpecialization", DataType = "NVARCHAR(100)", IsNullable = true });
            meta.Columns.Add(new ColumnMetadata { Name = "AddressLine1", DataType = "NVARCHAR(200)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "AddressLine2", DataType = "NVARCHAR(200)", IsNullable = true });
            meta.Columns.Add(new ColumnMetadata { Name = "City", DataType = "NVARCHAR(100)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "State", DataType = "NVARCHAR(100)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "PostalCode", DataType = "NVARCHAR(20)", IsNullable = true });
            meta.Columns.Add(new ColumnMetadata { Name = "Country", DataType = "NVARCHAR(100)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "HireDate", DataType = "DATE", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "Salary", DataType = "DECIMAL(18,2)", IsNullable = false });
        }
        else if (meta.TableName.Equals("Students", StringComparison.OrdinalIgnoreCase))
        {
            meta.Columns.Add(new ColumnMetadata { Name = "FirstName", DataType = "NVARCHAR(50)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "LastName", DataType = "NVARCHAR(50)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "DateOfBirth", DataType = "DATE", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "Email", DataType = "NVARCHAR(100)", IsNullable = true });
            meta.Columns.Add(new ColumnMetadata { Name = "PhoneNumber", DataType = "NVARCHAR(20)", IsNullable = true });
            meta.Columns.Add(new ColumnMetadata { Name = "AddressLine1", DataType = "NVARCHAR(200)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "City", DataType = "NVARCHAR(100)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "State", DataType = "NVARCHAR(100)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "PostalCode", DataType = "NVARCHAR(20)", IsNullable = true });
            meta.Columns.Add(new ColumnMetadata { Name = "EnrollmentDate", DataType = "DATE", IsNullable = false });
        }
        else if (meta.TableName.Equals("Employees", StringComparison.OrdinalIgnoreCase))
        {
            meta.Columns.Add(new ColumnMetadata { Name = "FirstName", DataType = "NVARCHAR(50)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "LastName", DataType = "NVARCHAR(50)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "Email", DataType = "NVARCHAR(100)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "Department", DataType = "NVARCHAR(50)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "JobTitle", DataType = "NVARCHAR(100)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "AddressLine1", DataType = "NVARCHAR(200)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "City", DataType = "NVARCHAR(100)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "State", DataType = "NVARCHAR(100)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "PostalCode", DataType = "NVARCHAR(20)", IsNullable = true });
            meta.Columns.Add(new ColumnMetadata { Name = "HireDate", DataType = "DATE", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "Salary", DataType = "DECIMAL(18,2)", IsNullable = false });
        }
        else if (meta.TableName.Equals("Customers", StringComparison.OrdinalIgnoreCase))
        {
            meta.Columns.Add(new ColumnMetadata { Name = "FirstName", DataType = "NVARCHAR(50)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "LastName", DataType = "NVARCHAR(50)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "Email", DataType = "NVARCHAR(100)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "PhoneNumber", DataType = "NVARCHAR(20)", IsNullable = true });
            meta.Columns.Add(new ColumnMetadata { Name = "AddressLine1", DataType = "NVARCHAR(200)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "City", DataType = "NVARCHAR(100)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "State", DataType = "NVARCHAR(100)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "PostalCode", DataType = "NVARCHAR(20)", IsNullable = true });
            meta.Columns.Add(new ColumnMetadata { Name = "Country", DataType = "NVARCHAR(100)", IsNullable = false });
        }
        else if (lower.Contains("address"))
        {
            meta.Columns.Add(new ColumnMetadata { Name = "AddressLine1", DataType = "NVARCHAR(200)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "AddressLine2", DataType = "NVARCHAR(200)", IsNullable = true });
            meta.Columns.Add(new ColumnMetadata { Name = "City", DataType = "NVARCHAR(100)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "StateProvince", DataType = "NVARCHAR(100)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "PostalCode", DataType = "NVARCHAR(20)", IsNullable = true });
            meta.Columns.Add(new ColumnMetadata { Name = "Country", DataType = "NVARCHAR(100)", IsNullable = false });
        }
        else
        {
            meta.Columns.Add(new ColumnMetadata { Name = "Code", DataType = "NVARCHAR(50)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "Name", DataType = "NVARCHAR(150)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "Description", DataType = "NVARCHAR(500)", IsNullable = true });
            meta.Columns.Add(new ColumnMetadata { Name = "Amount", DataType = "DECIMAL(18,2)", IsNullable = false });
            meta.Columns.Add(new ColumnMetadata { Name = "Status", DataType = "VARCHAR(30)", IsNullable = false });
        }
    }

    private static string GenerateScriptFromFragment(TSqlFragment fragment)
    {
        var generator = new Sql160ScriptGenerator(GeneratorOptions);
        generator.GenerateScript(fragment, out string formatted);
        return formatted ?? string.Empty;
    }
}
