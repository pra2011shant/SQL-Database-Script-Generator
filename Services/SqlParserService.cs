using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLDatabaseScriptGenerator.Models;

namespace SQLDatabaseScriptGenerator.Services;

public class SqlParserService : ISqlParserService
{
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
                Message = "SQL script is empty or whitespace."
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

        result.FormattedSql = FormatFragment(fragment);
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
            return sqlScript; // Return unformatted if invalid

        return FormatFragment(fragment);
    }

    private static string FormatFragment(TSqlFragment fragment)
    {
        var generator = new Sql160ScriptGenerator(new SqlScriptGeneratorOptions
        {
            KeywordCasing = KeywordCasing.Uppercase,
            IncludeSemicolons = true,
            AlignClauseBodies = true
        });

        generator.GenerateScript(fragment, out string formatted);
        return formatted ?? string.Empty;
    }
}
