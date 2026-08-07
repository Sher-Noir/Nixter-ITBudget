using System.Text.RegularExpressions;
using LedgerForge.Application.Abstractions;
using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Infrastructure.Persistence.Auditing;
using LedgerForge.Infrastructure.Persistence.Seeding;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

const string PrimaryConnectionEnvironmentVariable = "LEDGERFORGE_CONNECTION_STRING";
const string AspNetConnectionEnvironmentVariable = "ConnectionStrings__LedgerForge";
const string ApplicationIdentityEnvironmentVariable = "LEDGERFORGE_DATABASE_APP_IDENTITY";

if (args.Length != 1 || args[0] is not ("initialize" or "verify"))
{
    Console.Error.WriteLine("Usage: LedgerForge.Bootstrap <initialize|verify>");
    Console.Error.WriteLine($"Set {PrimaryConnectionEnvironmentVariable} or {AspNetConnectionEnvironmentVariable} before running the command.");
    return 2;
}

var connectionString = Environment.GetEnvironmentVariable(PrimaryConnectionEnvironmentVariable);
if (string.IsNullOrWhiteSpace(connectionString))
    connectionString = Environment.GetEnvironmentVariable(AspNetConnectionEnvironmentVariable);

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("LedgerForge database connection configuration is required.");
    return 3;
}

try
{
    var auditContext = new BootstrapAuditRequestContext();
    var auditInterceptor = new AuditSaveChangesInterceptor(auditContext);
    var options = new DbContextOptionsBuilder<LedgerForgeDbContext>()
        .UseSqlServer(connectionString)
        .AddInterceptors(auditInterceptor)
        .Options;

    await using var dbContext = new LedgerForgeDbContext(options);

    if (args[0] == "initialize")
    {
        Console.WriteLine("Applying committed LedgerForge database migrations...");
        await dbContext.Database.MigrateAsync();

        Console.WriteLine("Initializing generic LedgerForge workflow lookups...");
        var initializer = new ManagedLookupInitializer(dbContext);
        await initializer.InitializeMissingAsync();

        var applicationIdentity = Environment.GetEnvironmentVariable(ApplicationIdentityEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(applicationIdentity))
        {
            Console.WriteLine("Provisioning the configured Windows application identity for LedgerForge data access...");
            await ProvisionApplicationIdentityAsync(connectionString, dbContext, applicationIdentity.Trim());
        }
    }

    if (!await dbContext.Database.CanConnectAsync())
    {
        Console.Error.WriteLine("LedgerForge database verification failed.");
        return 4;
    }

    var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToArray();
    if (pendingMigrations.Length != 0)
    {
        Console.Error.WriteLine($"LedgerForge database has {pendingMigrations.Length} pending migration(s).");
        return 5;
    }

    Console.WriteLine(args[0] == "initialize"
        ? "LedgerForge database initialization completed successfully."
        : "LedgerForge database verification completed successfully.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"LedgerForge database operation failed: {exception.GetType().Name}: {exception.Message}");
    return 10;
}

static async Task ProvisionApplicationIdentityAsync(
    string connectionString,
    LedgerForgeDbContext applicationDbContext,
    string applicationIdentity)
{
    if (!Regex.IsMatch(applicationIdentity, @"^[A-Za-z0-9_ .\\$@-]{1,256}$", RegexOptions.CultureInvariant))
        throw new InvalidOperationException("The configured Windows application identity contains unsupported characters.");

    var quotedIdentifier = "[" + applicationIdentity.Replace("]", "]]", StringComparison.Ordinal) + "]";
    var sqlLiteral = applicationIdentity.Replace("'", "''", StringComparison.Ordinal);

    var masterConnection = new SqlConnectionStringBuilder(connectionString)
    {
        InitialCatalog = "master"
    };
    var masterOptions = new DbContextOptionsBuilder<LedgerForgeDbContext>()
        .UseSqlServer(masterConnection.ConnectionString)
        .Options;

    await using (var masterDbContext = new LedgerForgeDbContext(masterOptions))
    {
        await masterDbContext.Database.ExecuteSqlRawAsync($"""
            IF SUSER_ID(N'{sqlLiteral}') IS NULL
                CREATE LOGIN {quotedIdentifier} FROM WINDOWS;
            """);
    }

    await applicationDbContext.Database.ExecuteSqlRawAsync($"""
        IF DATABASE_PRINCIPAL_ID(N'{sqlLiteral}') IS NULL
            CREATE USER {quotedIdentifier} FOR LOGIN {quotedIdentifier};

        IF NOT EXISTS (
            SELECT 1
            FROM sys.database_role_members drm
            JOIN sys.database_principals role_principal ON role_principal.principal_id = drm.role_principal_id
            JOIN sys.database_principals member_principal ON member_principal.principal_id = drm.member_principal_id
            WHERE role_principal.name = N'db_datareader' AND member_principal.name = N'{sqlLiteral}')
            ALTER ROLE [db_datareader] ADD MEMBER {quotedIdentifier};

        IF NOT EXISTS (
            SELECT 1
            FROM sys.database_role_members drm
            JOIN sys.database_principals role_principal ON role_principal.principal_id = drm.role_principal_id
            JOIN sys.database_principals member_principal ON member_principal.principal_id = drm.member_principal_id
            WHERE role_principal.name = N'db_datawriter' AND member_principal.name = N'{sqlLiteral}')
            ALTER ROLE [db_datawriter] ADD MEMBER {quotedIdentifier};
        """);
}

file sealed class BootstrapAuditRequestContext : IAuditRequestContext
{
    public string Actor => "ledgerforge-setup";
    public string CorrelationId { get; } = $"setup-{Guid.NewGuid():N}";
    public string? RequestMethod => null;
    public string? RequestPath => null;
    public string? RemoteAddress => null;
    public string? UserAgent => "LedgerForge.Bootstrap";
}
