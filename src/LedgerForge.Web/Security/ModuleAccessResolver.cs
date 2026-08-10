using System.Security.Claims;

namespace LedgerForge.Web.Security;

public sealed record EffectiveModuleAccess(
    IReadOnlyList<SecurityRoleDefinition> Roles,
    IReadOnlyDictionary<LedgerForgeModule, ModuleAccessLevel> Modules);

public sealed class ModuleAccessResolver(
    SecurityAccessConfigurationStore store,
    ILogger<ModuleAccessResolver> logger)
{
    public async Task<EffectiveModuleAccess> ResolveAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var empty = Enum.GetValues<LedgerForgeModule>().ToDictionary(x => x, _ => ModuleAccessLevel.None);
        if (principal.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(principal.Identity.Name))
            return new([], empty);

        var configuration = await store.GetAsync(cancellationToken);
        if (configuration.Roles.Count == 0 || configuration.Assignments.Count == 0)
            return new([], empty);

        var identity = principal.Identity.Name;
        var matchingRoleIds = new HashSet<Guid>();
        foreach (var assignment in configuration.Assignments)
        {
            var matches = assignment.PrincipalType switch
            {
                SecurityPrincipalType.User => string.Equals(assignment.PrincipalName, identity, StringComparison.OrdinalIgnoreCase),
                SecurityPrincipalType.ActiveDirectoryGroup => IsInRole(principal, assignment.PrincipalName, identity),
                _ => false
            };
            if (matches) matchingRoleIds.Add(assignment.RoleId);
        }

        var roles = configuration.Roles.Where(x => matchingRoleIds.Contains(x.Id)).OrderBy(x => x.Name).ToArray();
        foreach (var role in roles)
        {
            foreach (var module in Enum.GetValues<LedgerForgeModule>())
            {
                var candidate = role.ModuleAccess.GetValueOrDefault(module, ModuleAccessLevel.None);
                empty[module] = ModuleAccess.Highest(empty[module], candidate);
            }
        }

        return new(roles, empty);
    }

    public async Task<bool> HasAccessAsync(
        ClaimsPrincipal principal,
        LedgerForgeModule module,
        ModuleAccessLevel minimum,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAsync(principal, cancellationToken);
        return ModuleAccess.Meets(access.Modules.GetValueOrDefault(module, ModuleAccessLevel.None), minimum);
    }

    public async Task<bool> HasAnyAccessAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var access = await ResolveAsync(principal, cancellationToken);
        return access.Modules.Values.Any(x => x >= ModuleAccessLevel.View);
    }

    private bool IsInRole(ClaimsPrincipal principal, string groupName, string identity)
    {
        try
        {
            return principal.IsInRole(groupName);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Directory group membership resolution failed for {DomainIdentity} while evaluating configurable module access.", identity);
            return false;
        }
    }
}
