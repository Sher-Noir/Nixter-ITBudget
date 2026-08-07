using LedgerForge.Setup.Engine;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class SetupPlanDocumentTests
{
    [Fact]
    public void SetupPlanDocument_RoundTripsWithoutCredentials()
    {
        var plan = new SetupPlan(
            "Example Organization",
            "LedgerForge",
            @"C:\Program Files\LedgerForge",
            @"C:\ProgramData\LedgerForge\Documents",
            @".\SQLEXPRESS",
            "LedgerForge",
            @"EXAMPLE\setup-admin",
            @"EXAMPLE\LedgerForge Admins",
            8080,
            "localhost");

        var json = SetupPlanDocument.Create(plan).ToJson();
        var roundTrip = SetupPlanDocument.Parse(json);

        Assert.Equal(SetupPlanDocument.CurrentSchemaVersion, roundTrip.SchemaVersion);
        Assert.Equal(plan, roundTrip.Plan);
        Assert.DoesNotContain("Password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetupPlanDocument_RejectsUnsupportedSchemaVersion()
    {
        var json = """
            {
              "SchemaVersion": 99,
              "Plan": {
                "OrganizationName": "Example Organization",
                "SiteName": "LedgerForge",
                "InstallPath": "C:\\Program Files\\LedgerForge",
                "DocumentsPath": "C:\\ProgramData\\LedgerForge\\Documents",
                "SqlServer": ".\\SQLEXPRESS",
                "DatabaseName": "LedgerForge",
                "InitialAdministratorIdentity": "EXAMPLE\\setup-admin",
                "SystemAdministratorGroup": null,
                "HttpPort": 8080,
                "HostName": "localhost"
              }
            }
            """;

        Assert.Throws<InvalidDataException>(() => SetupPlanDocument.Parse(json));
    }
}
