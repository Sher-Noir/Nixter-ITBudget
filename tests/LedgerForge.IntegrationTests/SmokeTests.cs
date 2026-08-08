using LedgerForge.Infrastructure.Persistence;
using Xunit;

namespace LedgerForge.IntegrationTests;

public sealed class SmokeTests
{
    [Fact]
    public void InfrastructureAssembly_UsesLedgerForgeIdentity()
    {
        Assert.StartsWith("LedgerForge.", typeof(LedgerForgeDbContext).Assembly.GetName().Name, StringComparison.Ordinal);
    }
}
