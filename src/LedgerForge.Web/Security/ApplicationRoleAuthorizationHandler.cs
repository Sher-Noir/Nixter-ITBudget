using LedgerForge.Domain.Security;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Web.Security;

public sealed class ApplicationRoleAuthorizationHandler(
    LedgerForgeDbContext dbContext,
    IConfiguration configuration,
    ModuleAccessResolver moduleAccessResolver,
    ILogger<ApplicationRoleAuthorizationHandler> logger)
    : AuthorizationHandler<ApplicationRoleRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ApplicationRoleRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        var domainIdentity = context.User.Identity.Name;
        if (string.IsNullOrWhiteSpace(domainIdentity)) return;

        // Configurable Role -> Module -> Access Level grants are evaluated first.
        // Legacy database/config mappings remain a bootstrap/recovery compatibility path.
        if (requirement.Module is LedgerForgeModule module)
        {
            if (await moduleAccessResolver.HasAccessAsync(context.User, module, requirement.MinimumAccess))
            {
                context.Succeed(requirement);
                return;
            }
        }
        else if (await moduleAccessResolver.HasAnyAccessAsync(context.User))
        {
            context.Succeed(requirement);
            return;
        }

        var requestedRoles = requirement.AllowedRoles.ToArray();
        List<UserRoleException> exceptions;
        List<AdGroupMapping> databaseMappings;
        try
        {
            var now = DateTimeOffset.UtcNow;
            exceptions = await dbContext.UserRoleExceptions.AsNoTracking()
                .Where(x => x.IsActive && x.DomainIdentity == domainIdentity && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > now))
                .ToListAsync();
            databaseMappings = await dbContext.AdGroupMappings.AsNoTracking()
                .Where(x => x.IsActive && requestedRoles.Contains(x.Role))
                .ToListAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Application role configuration could not be read for user {DomainIdentity}; authorization failed closed.", domainIdentity);
            return;
        }

        var deniedRoles = exceptions.Where(x => x.Effect == UserRoleExceptionEffect.Deny).Select(x => x.Role).ToHashSet();
        var grantedRoles = exceptions.Where(x => x.Effect == UserRoleExceptionEffect.Grant).Select(x => x.Role).Where(role => !deniedRoles.Contains(role)).ToHashSet();
        if (requestedRoles.Any(grantedRoles.Contains))
        {
            context.Succeed(requirement);
            return;
        }

        var allowedRoles = requestedRoles.Where(role => !deniedRoles.Contains(role)).ToHashSet();
        if (allowedRoles.Count == 0) return;

        var candidateGroups = databaseMappings.Where(x => allowedRoles.Contains(x.Role)).Select(x => (x.Role, x.GroupName)).ToList();
        foreach (var role in allowedRoles)
        {
            var section = configuration.GetSection($"Security:AdGroups:{role}");
            var configuredValues = section.GetChildren().Select(child => child.Value).Append(section.Value).Where(value => !string.IsNullOrWhiteSpace(value));
            foreach (var groupName in configuredValues) candidateGroups.Add((role, groupName!.Trim()));
        }

        foreach (var candidate in candidateGroups.Distinct())
        {
            try
            {
                if (context.User.IsInRole(candidate.GroupName))
                {
                    context.Succeed(requirement);
                    return;
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Directory group membership resolution failed for user {DomainIdentity}; authorization failed closed.", domainIdentity);
                return;
            }
        }
    }
}
