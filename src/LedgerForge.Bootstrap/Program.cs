using LedgerForge.Application.Abstractions;
using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Infrastructure.Persistence.Auditing;
using LedgerForge.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;

const string PrimaryConnectionEnvironmentVariable = "LEDGERFORGE_CONNECTION_STRING";
const string AspNetConnectionEnvironmentVariable = "ConnectionStrings__LedgerForge";

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

file sealed class BootstrapAuditRequestContext : IAuditRequestContext
{
    public string Actor => "ledgerforge-setup";
    public string CorrelationId { get; } = $"setup-{Guid.NewGuid():N}";
    public string? RequestMethod => null;
    public string? RequestPath => null;
    public string? RemoteAddress => null;
    public string? UserAgent => "LedgerForge.Bootstrap";
}
