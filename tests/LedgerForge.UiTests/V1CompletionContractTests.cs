using Xunit;

namespace LedgerForge.UiTests;

public sealed class V1CompletionContractTests
{
    [Fact]
    public void ProcurementRevisionSurface_IsCompleteAndFailSafe()
    {
        var root = FindRepositoryRoot();
        var procurement = File.ReadAllText(Path.Combine(root, "src", "LedgerForge.Infrastructure", "Procurement", "ProcurementService.cs"));
        var lineController = File.ReadAllText(Path.Combine(root, "src", "LedgerForge.Web", "Controllers", "PurchaseOrderLinesController.cs"));
        var detail = File.ReadAllText(Path.Combine(root, "src", "LedgerForge.Web", "Views", "PurchaseOrders", "Details.cshtml"));
        var dbContext = File.ReadAllText(Path.Combine(root, "src", "LedgerForge.Infrastructure", "Persistence", "LedgerForgeDbContext.cs"));

        Assert.Contains("Create a change order before receiving or invoicing begins", procurement, StringComparison.Ordinal);
        Assert.Contains("received receiving/invoice activity after this revision was created", procurement, StringComparison.Ordinal);
        Assert.Contains("{lineId:guid}/update", lineController, StringComparison.Ordinal);
        Assert.Contains("{lineId:guid}/delete", lineController, StringComparison.Ordinal);
        Assert.Contains("budgetItemId ??= line.BudgetItemId", lineController, StringComparison.Ordinal);
        Assert.Contains("lines/@line.Id/update", detail, StringComparison.Ordinal);
        Assert.Contains("[State] <> 5 AND [State] <> 6", dbContext, StringComparison.Ordinal);

        var migrations = Directory.EnumerateFiles(
                Path.Combine(root, "src", "LedgerForge.Infrastructure", "Persistence", "Migrations"),
                "*_AllowChangeOrderRetry.cs")
            .ToArray();
        Assert.Single(migrations);
    }

    [Fact]
    public void ReleaseAndPublicHygiene_GuardrailsArePresent()
    {
        var root = FindRepositoryRoot();
        var release = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));
        var hygiene = File.ReadAllText(Path.Combine(root, ".github", "workflows", "public-hygiene.yml"));
        var gate = File.ReadAllText(Path.Combine(root, ".github", "workflows", "v1-release-gate.yml"));

        Assert.DoesNotContain("if: ${{ secrets.", release, StringComparison.Ordinal);
        Assert.Contains("No Authenticode signing certificate is configured", release, StringComparison.Ordinal);
        Assert.DoesNotContain(":!docs/PROJECT_HANDOFF.md", hygiene, StringComparison.Ordinal);
        Assert.Contains("v1-release-gate", gate, StringComparison.Ordinal);
        Assert.Contains("Integration tests against migrated SQL Server", gate, StringComparison.Ordinal);
        Assert.Contains("Publish embedded self-contained Setup", gate, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicDocumentation_UsesCurrentV1Contract()
    {
        var root = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var checklist = File.ReadAllText(Path.Combine(root, "docs", "architecture", "implementation-checklist.md"));
        var gettingStarted = File.ReadAllText(Path.Combine(root, "docs", "GETTING_STARTED.md"));

        Assert.Contains("docs/architecture/v1-completion.md", readme, StringComparison.Ordinal);
        Assert.Contains("superseded by", checklist, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Work Center", gettingStarted, StringComparison.Ordinal);
        Assert.DoesNotContain("Charles River Community" + " Health", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CR" + "CH", readme, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LedgerForge.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("LedgerForge repository root could not be located.");
    }
}
