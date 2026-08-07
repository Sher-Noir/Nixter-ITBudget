using Crch.ItBudget.Domain.Security;
using Crch.ItBudget.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Crch.ItBudget.Web.Security;

public sealed class ApplicationRoleAuthorizationHandler(
    ItBudgetDbContext dbContext,
    ILogger<ApplicationRoleAuthorizationHandler> logger)
    : AuthorizationHandler<ApplicationRoleRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApplicationRoleRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var domainIdentity = context.User.Identity.Name;
        if (string.IsNullOrWhiteSpace(domainIdentity))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var exceptions = await dbContext.Set<UserRoleException>()
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.DomainIdentity == domainIdentity &&
                (x.ExpiresAtUtc == null || x.ExpiresAtUtc > now))
            .ToListAsync();

        var deniedRoles = exceptions
            .Where(x => x.Effect == UserRoleExceptionEffect.Deny)
            .Select(x => x.Role)
            .ToHashSet();

        var grantedRoles = exceptions
            .Where(x => x.Effect == UserRoleExceptionEffect.Grant)
            .Select(x => x.Role)
            .Where(role => !deniedRoles.Contains(role))
            .ToHashSet();

        if (requirement.AllowedRoles.Any(grantedRoles.Contains))
        {
            context.Succeed(requirement);
            return;
        }

        var allowedRoles = requirement.AllowedRoles
            .Where(role => !deniedRoles.Contains(role))
            .ToArray();

        if (allowedRoles.Length == 0)
        {
            return;
        }

        var mappings = await dbContext.Set<AdGroupMapping>()
            .AsNoTracking()
            .Where(x => x.IsActive && allowedRoles.Contains(x.Role))
            .ToListAsync();

        foreach (var mapping in mappings)
        {
            try
            {
                if (context.User.IsInRole(mapping.GroupName))
                {
                    context.Succeed(requirement);
                    return;
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "AD group membership resolution failed for user {DomainIdentity}; authorization failed closed.",
                    domainIdentity);
                return;
            }
        }
    }
}
