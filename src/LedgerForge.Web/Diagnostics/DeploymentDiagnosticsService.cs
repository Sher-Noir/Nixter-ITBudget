using System.Runtime.InteropServices;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Web.Diagnostics;

public sealed record DeploymentDiagnosticCheck(string Name, bool Success, string Detail);

public sealed record DeploymentDiagnosticsSnapshot(
    string EnvironmentName,
    string Framework,
    string OperatingSystem,
    string ApplicationVersion,
    IReadOnlyList<DeploymentDiagnosticCheck> Checks)
{
    public bool IsHealthy => Checks.All(x => x.Success);
}

public sealed class DeploymentDiagnosticsService(
    LedgerForgeDbContext dbContext,
    IWebHostEnvironment environment,
    IConfiguration configuration)
{
    public async Task<DeploymentDiagnosticsSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<DeploymentDiagnosticCheck>();

        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            checks.Add(new("SQL Server connectivity", canConnect, canConnect ? "LedgerForge can connect to its configured SQL Server database." : "LedgerForge cannot connect to its configured SQL Server database."));
        }
        catch (Exception exception)
        {
            checks.Add(new("SQL Server connectivity", false, $"Database connection failed: {exception.GetType().Name}: {exception.Message}"));
        }

        try
        {
            var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
            checks.Add(new(
                "Database migrations",
                pending.Length == 0,
                pending.Length == 0 ? "No EF Core migrations are pending." : $"{pending.Length} migration(s) are pending: {string.Join(", ", pending)}"));
        }
        catch (Exception exception)
        {
            checks.Add(new("Database migrations", false, $"Migration state could not be read: {exception.GetType().Name}: {exception.Message}"));
        }

        var documentPath = ResolveDocumentPath();
        try
        {
            var webRoot = Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
            var root = Path.GetFullPath(documentPath);
            var webPrefix = webRoot.EndsWith(Path.DirectorySeparatorChar) ? webRoot : webRoot + Path.DirectorySeparatorChar;
            var outsideWebRoot = !root.Equals(webRoot, StringComparison.OrdinalIgnoreCase) && !root.StartsWith(webPrefix, StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(root);
            var probe = Path.Combine(root, $".ledgerforge-diagnostic-{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(probe, "LedgerForge storage diagnostic", cancellationToken);
            File.Delete(probe);
            checks.Add(new(
                "Document storage",
                outsideWebRoot,
                outsideWebRoot ? $"Writable non-web-root storage: {root}" : $"Storage is writable but incorrectly located beneath the public web root: {root}"));
        }
        catch (Exception exception)
        {
            checks.Add(new("Document storage", false, $"Document storage check failed for {documentPath}: {exception.GetType().Name}: {exception.Message}"));
        }

        var httpsRedirect = configuration.GetValue("Deployment:HttpsRedirection", true);
        checks.Add(new(
            "HTTPS redirect policy",
            environment.IsDevelopment() || httpsRedirect || string.Equals(configuration["Deployment:Mode"], "PreviewHttp", StringComparison.OrdinalIgnoreCase),
            httpsRedirect
                ? "HTTPS redirection is enabled."
                : "HTTPS redirection is disabled; this is acceptable only for the localhost/evaluation preview binding."));

        var connection = configuration.GetConnectionString("LedgerForge");
        checks.Add(new(
            "Connection configuration",
            !string.IsNullOrWhiteSpace(connection),
            string.IsNullOrWhiteSpace(connection) ? "Connection string 'LedgerForge' is missing." : "Connection string 'LedgerForge' is configured (value hidden)."));

        return new(
            environment.EnvironmentName,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription,
            typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
            checks);
    }

    private string ResolveDocumentPath()
    {
        var configured = configuration["Documents:StoragePath"];
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "Documents")
            : Environment.ExpandEnvironmentVariables(configured.Trim());
    }
}
