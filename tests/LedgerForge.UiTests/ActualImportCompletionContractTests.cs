using Xunit;

namespace LedgerForge.UiTests;

public sealed class ActualImportCompletionContractTests
{
    [Fact]
    public void ActualCsvImport_UsesHashBackedDuplicateProtection()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "LedgerForge.Infrastructure", "Actuals", "ActualCsvImportService.cs"));
        var integration = File.ReadAllText(Path.Combine(root, "tests", "LedgerForge.IntegrationTests", "ActualCsvImportIntegrationTests.cs"));

        Assert.Contains("SHA256.HashData", service, StringComparison.Ordinal);
        Assert.Contains("seenReferences", service, StringComparison.Ordinal);
        Assert.Contains("previously posted import reference(s)", service, StringComparison.Ordinal);
        Assert.Contains("ActualTransactionKind.Import", service, StringComparison.Ordinal);
        Assert.Contains("duplicateStream", integration, StringComparison.Ordinal);
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
