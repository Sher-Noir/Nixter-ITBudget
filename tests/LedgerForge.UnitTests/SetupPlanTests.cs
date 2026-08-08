using System.Text.Json;
using LedgerForge.Setup.Engine;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class SetupPlanTests
{
    [Fact]
    public void ValidSetupPlan_RendersFailClosedAdministratorConfiguration()
    {
        var plan = CreatePlan();

        var json = DeploymentConfigurationRenderer.RenderProductionSettings(plan);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("Example Organization", root.GetProperty("Branding").GetProperty("OrganizationName").GetString());
        Assert.Equal(@"D:\LedgerForgeData\Documents", root.GetProperty("Documents").GetProperty("StoragePath").GetString());
        Assert.False(root.GetProperty("Deployment").GetProperty("HttpsRedirection").GetBoolean());
        var groups = root.GetProperty("Security").GetProperty("AdGroups");
        Assert.Equal(@"EXAMPLE\LedgerForge Admins", groups.GetProperty("SystemAdministrator")[0].GetString());
        Assert.Equal(0, groups.GetProperty("BudgetAdministrator").GetArrayLength());
        Assert.Equal(0, groups.GetProperty("ReadOnly").GetArrayLength());
    }

    [Fact]
    public void OptionalAdministratorGroup_CanRemainEmptyForFirstUserBootstrap()
    {
        var plan = CreatePlan() with { SystemAdministratorGroup = null };

        var json = DeploymentConfigurationRenderer.RenderProductionSettings(plan);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(
            0,
            document.RootElement.GetProperty("Security").GetProperty("AdGroups").GetProperty("SystemAdministrator").GetArrayLength());
    }

    [Fact]
    public void ConnectionString_UsesIntegratedSecurityAndEscapesValues()
    {
        var plan = CreatePlan() with { SqlServer = @".\SQLEXPRESS", DatabaseName = "LedgerForge Preview" };

        var connection = DeploymentConfigurationRenderer.CreateConnectionString(plan);

        Assert.Contains("Server=", connection, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Database=", connection, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Integrated Security=True", connection, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Encrypt=True", connection, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TrustServerCertificate=True", connection, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidPlan_IsRejectedBeforeConfigurationIsWritten()
    {
        var plan = CreatePlan() with
        {
            SiteName = "bad/site",
            HttpPort = 70000,
            InitialAdministratorIdentity = ""
        };

        var errors = SetupPlanValidator.Validate(plan);

        Assert.Contains(errors, x => x.Contains("Site name", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, x => x.Contains("HTTP port", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, x => x.Contains("administrator", StringComparison.OrdinalIgnoreCase));
        Assert.Throws<ArgumentException>(() => DeploymentConfigurationRenderer.RenderProductionSettings(plan));
    }

    [Fact]
    public void ApplicationPoolIdentity_IsDeterministic()
    {
        Assert.Equal(@"IIS APPPOOL\LedgerForge", CreatePlan().ApplicationPoolIdentity);
    }

    private static SetupPlan CreatePlan()
        => new(
            "Example Organization",
            "LedgerForge",
            @"C:\Program Files\LedgerForge",
            @"D:\LedgerForgeData\Documents",
            @".\SQLEXPRESS",
            "LedgerForge",
            @"EXAMPLE\setup-admin",
            @"EXAMPLE\LedgerForge Admins",
            8080);
}
