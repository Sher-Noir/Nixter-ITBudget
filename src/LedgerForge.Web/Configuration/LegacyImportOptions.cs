namespace LedgerForge.Web.Configuration;

public sealed class LegacyImportOptions
{
    public const string SectionName = "LegacyImport";
    public int? ExpectedItemCount { get; set; }
    public decimal? ExpectedPlannedTotal { get; set; }
    public string? PriorityNeedLevel { get; set; }
    public int? ExpectedPriorityNeedLevelCount { get; set; }
}
