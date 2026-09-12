using System.Text;
using System.Text.RegularExpressions;

namespace SQLDatabaseScriptGenerator.Services;

public class SqlTranspilerService : ISqlTranspilerService
{
    public string TranspileToPostgreSql(string tsql)
    {
        if (string.IsNullOrWhiteSpace(tsql)) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("-- ===========================================================================");
        sb.AppendLine("-- Transpiled Target: PostgreSQL (pgSQL 15+)");
        sb.AppendLine($"-- Generated On: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("-- ===========================================================================");
        sb.AppendLine();

        var sql = tsql;

        // Data Types
        sql = Regex.Replace(sql, @"\bNVARCHAR\s*\(MAX\)", "TEXT", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bNVARCHAR\s*\(([0-9]+)\)", "VARCHAR($1)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bNCHAR\s*\(([0-9]+)\)", "CHAR($1)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bDATETIME2(?:\([0-9]+\))?", "TIMESTAMPTZ", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bDATETIME\b", "TIMESTAMP", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bBIT\b", "BOOLEAN", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bROWVERSION\b", "BYTEA", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bUNIQUEIDENTIFIER\b", "UUID", RegexOptions.IgnoreCase);

        // Identity & Sequence
        sql = Regex.Replace(sql, @"\bINT\s+IDENTITY(?:\([0-9,\s]+\))?", "SERIAL", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bBIGINT\s+IDENTITY(?:\([0-9,\s]+\))?", "BIGSERIAL", RegexOptions.IgnoreCase);

        // Built-in Functions & Defaults
        sql = Regex.Replace(sql, @"\bGETUTCDATE\(\)", "NOW() AT TIME ZONE 'UTC'", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bSYSUTCDATETIME\(\)", "NOW() AT TIME ZONE 'UTC'", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bGETDATE\(\)", "NOW()", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bSYSTEM_USER\b", "CURRENT_USER", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bNEWID\(\)", "gen_random_uuid()", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bISNULL\(", "COALESCE(", RegexOptions.IgnoreCase);

        // T-SQL Clauses to strip
        sql = Regex.Replace(sql, @"\bSET\s+NOCOUNT\s+ON;?", "", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bSET\s+XACT_ABORT\s+ON;?", "", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bWITH\s*\(NOLOCK\)", "", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bWITH\s*\(DATA_COMPRESSION\s*=\s*PAGE\)", "", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bGO\b", ";", RegexOptions.IgnoreCase);

        // Square brackets to quotes/clean
        sql = Regex.Replace(sql, @"\[([a-zA-Z0-9_]+)\]", "\"$1\"");

        sb.Append(sql.Trim());
        return sb.ToString();
    }

    public string TranspileToMySql(string tsql)
    {
        if (string.IsNullOrWhiteSpace(tsql)) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("-- ===========================================================================");
        sb.AppendLine("-- Transpiled Target: MySQL (InnoDB 8.0+)");
        sb.AppendLine($"-- Generated On: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("-- ===========================================================================");
        sb.AppendLine();

        var sql = tsql;

        // Data Types
        sql = Regex.Replace(sql, @"\bNVARCHAR\s*\(MAX\)", "LONGTEXT", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bNVARCHAR\s*\(([0-9]+)\)", "VARCHAR($1)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bDATETIME2(?:\([0-9]+\))?", "DATETIME(6)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bBIT\b", "TINYINT(1)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bROWVERSION\b", "TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bUNIQUEIDENTIFIER\b", "VARCHAR(36)", RegexOptions.IgnoreCase);

        // Auto Increment
        sql = Regex.Replace(sql, @"\bIDENTITY(?:\([0-9,\s]+\))?", "AUTO_INCREMENT", RegexOptions.IgnoreCase);

        // Functions
        sql = Regex.Replace(sql, @"\bGETUTCDATE\(\)", "UTC_TIMESTAMP()", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bSYSUTCDATETIME\(\)", "UTC_TIMESTAMP(6)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bGETDATE\(\)", "NOW()", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bSYSTEM_USER\b", "USER()", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bNEWID\(\)", "UUID()", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bISNULL\(", "IFNULL(", RegexOptions.IgnoreCase);

        // Stripping T-SQL syntax
        sql = Regex.Replace(sql, @"\bSET\s+NOCOUNT\s+ON;?", "", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bSET\s+XACT_ABORT\s+ON;?", "", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bWITH\s*\(NOLOCK\)", "", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bGO\b", ";", RegexOptions.IgnoreCase);

        // Square brackets to backticks
        sql = Regex.Replace(sql, @"\[([a-zA-Z0-9_]+)\]", "`$1`");

        sb.Append(sql.Trim());
        return sb.ToString();
    }

    public string TranspileToOracle(string tsql)
    {
        if (string.IsNullOrWhiteSpace(tsql)) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("-- ===========================================================================");
        sb.AppendLine("-- Transpiled Target: Oracle Database (PL/SQL 19c/21c)");
        sb.AppendLine($"-- Generated On: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("-- ===========================================================================");
        sb.AppendLine();

        var sql = tsql;

        // Data Types
        sql = Regex.Replace(sql, @"\bNVARCHAR\s*\(MAX\)", "CLOB", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bNVARCHAR\s*\(([0-9]+)\)", "VARCHAR2($1 CHAR)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bVARCHAR\s*\(([0-9]+)\)", "VARCHAR2($1)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bDATETIME2(?:\([0-9]+\))?", "TIMESTAMP(6) WITH TIME ZONE", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bBIT\b", "NUMBER(1)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bROWVERSION\b", "RAW(8)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bUNIQUEIDENTIFIER\b", "RAW(16)", RegexOptions.IgnoreCase);

        // Identity
        sql = Regex.Replace(sql, @"\bIDENTITY(?:\([0-9,\s]+\))?", "GENERATED ALWAYS AS IDENTITY", RegexOptions.IgnoreCase);

        // Functions
        sql = Regex.Replace(sql, @"\bGETUTCDATE\(\)", "SYS_EXTRACT_UTC(SYSTIMESTAMP)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bSYSUTCDATETIME\(\)", "SYS_EXTRACT_UTC(SYSTIMESTAMP)", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bGETDATE\(\)", "SYSDATE", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bNEWID\(\)", "SYS_GUID()", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bISNULL\(", "NVL(", RegexOptions.IgnoreCase);

        // Strip T-SQL syntax
        sql = Regex.Replace(sql, @"\bSET\s+NOCOUNT\s+ON;?", "", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bSET\s+XACT_ABORT\s+ON;?", "", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bWITH\s*\(NOLOCK\)", "", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bGO\b", "/", RegexOptions.IgnoreCase);

        // Square brackets to quotes
        sql = Regex.Replace(sql, @"\[([a-zA-Z0-9_]+)\]", "\"$1\"");

        sb.Append(sql.Trim());
        return sb.ToString();
    }
}
