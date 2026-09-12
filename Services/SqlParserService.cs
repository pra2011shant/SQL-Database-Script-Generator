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

    private static string GenerateScriptFromFragment(TSqlFragment fragment)
    {
        var generator = new Sql160ScriptGenerator(GeneratorOptions);
        generator.GenerateScript(fragment, out string formatted);
        return formatted ?? string.Empty;
    }
}
