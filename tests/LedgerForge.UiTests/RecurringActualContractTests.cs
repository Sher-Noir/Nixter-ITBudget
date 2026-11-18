using Xunit;

namespace LedgerForge.UiTests;

public sealed class RecurringActualContractTests
{
    [Fact]
    public void RecurringActuals_HavePermissionedFormsAndNeverCountFutureCharges()
    {
        var view = Read("src", "LedgerForge.Web", "Views", "Budget", "Item.cshtml");
        var controller = Read("src", "LedgerForge.Web", "Controllers", "BudgetController.cs");
        var service = Read("src", "LedgerForge.Infrastructure", "Actuals", "RecurringActualService.cs");
        var program = Read("src", "LedgerForge.Web", "Program.cs");
        var db = Read("src", "LedgerForge.Infrastructure", "Persistence", "LedgerForgeDbContext.cs");
        var worker = Read("src", "LedgerForge.Web", "Services", "RecurringActualWorker.cs");

        Assert.Contains("id=\"recurring-actuals\"", view, StringComparison.Ordinal);
        Assert.Contains("Save recurring schedule", view, StringComparison.Ordinal);
        Assert.Contains("Save schedule changes", view, StringComparison.Ordinal);
        Assert.Contains("Pause future postings", view, StringComparison.Ordinal);
        Assert.Contains("Edit in Actuals", view, StringComparison.Ordinal);
        Assert.Contains("name=\"lastDayOfMonth\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"amount\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"startDate\"", view, StringComparison.Ordinal);
        Assert.Contains("name=\"endDate\"", view, StringComparison.Ordinal);
        Assert.Contains("Authorize(Policy = AuthorizationPolicies.PostActuals)", controller, StringComparison.Ordinal);
        Assert.Contains("CreateRecurringActual(", controller, StringComparison.Ordinal);
        Assert.Contains("UpdateRecurringActual(", controller, StringComparison.Ordinal);
        Assert.Contains("SetRecurringActualActive(", controller, StringComparison.Ordinal);
        Assert.Contains("PostDueAsync(", service, StringComparison.Ordinal);
        Assert.Contains("IsolationLevel.Serializable", service, StringComparison.Ordinal);
        Assert.Contains("x.BillingYear == year && x.BillingMonth == month", service, StringComparison.Ordinal);
        Assert.Contains("AddHostedService<RecurringActualWorker>()", program, StringComparison.Ordinal);
        Assert.Contains("RecurringActualOccurrences", db, StringComparison.Ordinal);
        Assert.Contains("PeriodicTimer(TimeSpan.FromHours(1))", worker, StringComparison.Ordinal);
    }

    private static string Read(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LedgerForge.slnx")))
                return File.ReadAllText(Path.Combine([directory.FullName, ..segments]));
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("LedgerForge repository root could not be located.");
    }
}
