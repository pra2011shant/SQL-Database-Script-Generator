using System.Text;
using SQLDatabaseScriptGenerator.Models;

namespace SQLDatabaseScriptGenerator.Services;

public class SqlStandardsService : ISqlStandardsService
{
    private CorporateSqlStandards _standards = new();
    private readonly object _lock = new();

    public CorporateSqlStandards GetStandards()
    {
        lock (_lock)
        {
            return _standards;
        }
    }

    public void UpdateStandards(CorporateSqlStandards standards)
    {
        if (standards == null) return;
        lock (_lock)
        {
            _standards = standards;
        }
    }

    public string BuildStandardsContextPrompt()
    {
        var s = GetStandards();
        var sb = new StringBuilder();
        sb.AppendLine("CORPORATE CODING STANDARDS (RAG Context Policy):");
        sb.AppendLine($"- Stored Procedure Prefix: {s.SpPrefix}");
        sb.AppendLine($"- Index Prefix: {s.IndexPrefix}");
        sb.AppendLine($"- Primary Key Prefix: {s.PkPrefix}");
        sb.AppendLine($"- Foreign Key Prefix: {s.FkPrefix}");
        sb.AppendLine($"- Audit Columns: [{s.CreatedAtColumn}], [{s.CreatedByColumn}], [{s.ModifiedAtColumn}], [{s.ModifiedByColumn}]");
        sb.AppendLine($"- Soft Delete Column: [{s.SoftDeleteColumn}] (BIT default 0)");
        sb.AppendLine($"- Optimistic Concurrency: {(s.EnableOptimisticConcurrency ? "Include [RowVersion] ROWVERSION" : "Disabled")}");
        sb.AppendLine($"- Enforcement: SET NOCOUNT ON, SET XACT_ABORT ON, explicit transactions with TRY...CATCH.");
        return sb.ToString();
    }
}
