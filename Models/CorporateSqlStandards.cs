namespace SQLDatabaseScriptGenerator.Models;

public class CorporateSqlStandards
{
    public string SpPrefix { get; set; } = "usp_";
    public string IndexPrefix { get; set; } = "IX_";
    public string PkPrefix { get; set; } = "PK_";
    public string FkPrefix { get; set; } = "FK_";
    public string DefaultConstraintPrefix { get; set; } = "DF_";
    public string CheckConstraintPrefix { get; set; } = "CK_";

    public string CreatedAtColumn { get; set; } = "CreatedAtUtc";
    public string CreatedByColumn { get; set; } = "CreatedBy";
    public string ModifiedAtColumn { get; set; } = "ModifiedAtUtc";
    public string ModifiedByColumn { get; set; } = "ModifiedBy";
    public string SoftDeleteColumn { get; set; } = "IsDeleted";
    public bool EnableOptimisticConcurrency { get; set; } = true;
    public bool EnforceExplicitTransactions { get; set; } = true;
    public bool EnforceTryCatch { get; set; } = true;
    public bool EnforceSetNoCount { get; set; } = true;
}
