using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LedgerForge.Setup.Engine;

public sealed record SetupPlan(
    string OrganizationName,
    string SiteName,
    string InstallPath,
    string DocumentsPath,
    string SqlServer,
    string DatabaseName,
    string InitialAdministratorIdentity,
    string? SystemAdministratorGroup,
    int HttpPort,
    string? HostName = null)
{
    public string ApplicationPoolName => SiteName;
    public string ApplicationPoolIdentity => $"IIS APPPOOL\\{ApplicationPoolName}";
}

public static class SetupPlanValidator
{
    private static readonly Regex SiteNamePattern = new(@"^[A-Za-z0-9_.-]{1,64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex DatabaseNamePattern = new(@"^[A-Za-z0-9_. -]{1,128}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> Validate(SetupPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var errors = new List<string>();

        Required(plan.OrganizationName, 200, "Organization name", errors);
        if (!SiteNamePattern.IsMatch(plan.SiteName ?? string.Empty))
            errors.Add("Site name must contain only letters, numbers, dot, underscore, or hyphen and be at most 64 characters.");
        Required(plan.InstallPath, 1024, "Install path", errors);
        Required(plan.DocumentsPath, 1024, "Document storage path", errors);
        Required(plan.SqlServer, 256, "SQL Server", errors);
        if (!DatabaseNamePattern.IsMatch(plan.DatabaseName ?? string.Empty))
            errors.Add("Database name contains unsupported characters or is longer than 128 characters.");
        Required(plan.InitialAdministratorIdentity, 256, "Initial administrator Windows identity", errors);
        if (!string.IsNullOrWhiteSpace(plan.SystemAdministratorGroup) && plan.SystemAdministratorGroup.Trim().Length > 256)
            errors.Add("System Administrator Windows group cannot exceed 256 characters.");

        if (plan.HttpPort is < 1 or > 65535)
            errors.Add("HTTP port must be between 1 and 65535.");

        if (!string.IsNullOrWhiteSpace(plan.HostName) && plan.HostName.Trim().Length > 253)
            errors.Add("Host name cannot exceed 253 characters.");

        if (!string.IsNullOrWhiteSpace(plan.InstallPath) &&
            !string.IsNullOrWhiteSpace(plan.DocumentsPath) &&
            IsSamePath(plan.InstallPath, plan.DocumentsPath))
            errors.Add("Document storage must use a separate directory from the application installation path.");

        return errors;
    }

    private static void Required(string? value, int maxLength, string label, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{label} is required.");
            return;
        }

        if (value.Trim().Length > maxLength)
            errors.Add($"{label} cannot exceed {maxLength} characters.");
    }

    private static bool IsSamePath(string left, string right)
        => string.Equals(
            left.Trim().TrimEnd('\\', '/'),
            right.Trim().TrimEnd('\\', '/'),
            StringComparison.OrdinalIgnoreCase);
}

public static class DeploymentConfigurationRenderer
{
    public static string CreateConnectionString(SetupPlan plan)
    {
        EnsureValid(plan);
        var builder = new DbConnectionStringBuilder
        {
            ["Server"] = plan.SqlServer.Trim(),
            ["Database"] = plan.DatabaseName.Trim(),
            ["Integrated Security"] = true,
            ["Encrypt"] = true,
            ["TrustServerCertificate"] = true,
            ["MultipleActiveResultSets"] = true
        };
        return builder.ConnectionString;
    }

    public static string RenderProductionSettings(SetupPlan plan)
    {
        EnsureValid(plan);
        var administratorGroups = string.IsNullOrWhiteSpace(plan.SystemAdministratorGroup)
            ? Array.Empty<string>()
            : new[] { plan.SystemAdministratorGroup.Trim() };
        var settings = new
        {
            Branding = new
            {
                ProductName = "LedgerForge",
                OrganizationName = plan.OrganizationName.Trim(),
                ApplicationTitle = "Budget Management",
                SupportText = "Contact your LedgerForge administrator for assistance.",
                FooterText = "LedgerForge — free and open-source budget management.",
                TimeZone = "UTC",
                DefaultFiscalYearLabel = "Current FY",
                DefaultTheme = "system"
            },
            ConnectionStrings = new
            {
                LedgerForge = CreateConnectionString(plan)
            },
            Documents = new
            {
                StoragePath = plan.DocumentsPath.Trim(),
                MaxFileSizeBytes = 26214400
            },
            Imports = new
            {
                MaxFileSizeBytes = 26214400
            },
            Security = new
            {
                AdGroups = new Dictionary<string, string[]>
                {
                    ["SystemAdministrator"] = administratorGroups,
                    ["BudgetAdministrator"] = [],
                    ["BudgetEditor"] = [],
                    ["Approver"] = [],
                    ["ReadOnly"] = [],
                    ["Auditor"] = []
                }
            }
        };

        return JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
    }

    private static void EnsureValid(SetupPlan plan)
    {
        var errors = SetupPlanValidator.Validate(plan);
        if (errors.Count != 0)
            throw new ArgumentException(string.Join(Environment.NewLine, errors), nameof(plan));
    }
}
