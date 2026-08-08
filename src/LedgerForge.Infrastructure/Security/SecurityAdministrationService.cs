using LedgerForge.Domain.Security;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Infrastructure.Security;

public sealed record AdGroupMappingSummary(Guid Id, ApplicationRole Role, string GroupName, string? Description, bool IsActive);
public sealed record UserRoleExceptionSummary(Guid Id, string DomainIdentity, ApplicationRole Role, UserRoleExceptionEffect Effect, string Reason, bool IsActive, DateTimeOffset? ExpiresAtUtc);

public sealed class SecurityAdministrationService(LedgerForgeDbContext dbContext)
{
    public async Task<IReadOnlyList<AdGroupMappingSummary>> ListMappingsAsync(CancellationToken cancellationToken = default)
        => await dbContext.AdGroupMappings.AsNoTracking().OrderBy(x => x.Role).ThenBy(x => x.GroupName)
            .Select(x => new AdGroupMappingSummary(x.Id, x.Role, x.GroupName, x.Description, x.IsActive)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UserRoleExceptionSummary>> ListUserExceptionsAsync(CancellationToken cancellationToken = default)
        => await dbContext.UserRoleExceptions.AsNoTracking().OrderByDescending(x => x.IsActive).ThenBy(x => x.DomainIdentity).ThenBy(x => x.Role)
            .Select(x => new UserRoleExceptionSummary(x.Id, x.DomainIdentity, x.Role, x.Effect, x.Reason, x.IsActive, x.ExpiresAtUtc)).ToListAsync(cancellationToken);

    public async Task AddOrActivateMappingAsync(ApplicationRole role, string groupName, string? description, CancellationToken cancellationToken = default)
    {
        ValidateRole(role);
        groupName = ValidateGroupName(groupName);

        var existing = await dbContext.AdGroupMappings.SingleOrDefaultAsync(x => x.Role == role && x.GroupName == groupName, cancellationToken);
        if (existing is null)
        {
            dbContext.AdGroupMappings.Add(new AdGroupMapping(role, groupName, description));
        }
        else
        {
            existing.Update(groupName, description);
            existing.Activate();
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateMappingAsync(Guid mappingId, string groupName, string? description, bool isActive, CancellationToken cancellationToken = default)
    {
        if (mappingId == Guid.Empty) throw new ArgumentException("Mapping ID is required.", nameof(mappingId));
        groupName = ValidateGroupName(groupName);
        var mapping = await dbContext.AdGroupMappings.SingleOrDefaultAsync(x => x.Id == mappingId, cancellationToken)
            ?? throw new KeyNotFoundException("AD group mapping was not found.");
        if (await dbContext.AdGroupMappings.AnyAsync(x => x.Id != mappingId && x.Role == mapping.Role && x.GroupName == groupName, cancellationToken))
            throw new InvalidOperationException("That directory group is already mapped to this LedgerForge role.");
        mapping.Update(groupName, description);
        if (isActive) mapping.Activate(); else mapping.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetMappingActiveAsync(Guid mappingId, bool isActive, CancellationToken cancellationToken = default)
    {
        if (mappingId == Guid.Empty) throw new ArgumentException("Mapping ID is required.", nameof(mappingId));
        var mapping = await dbContext.AdGroupMappings.SingleOrDefaultAsync(x => x.Id == mappingId, cancellationToken)
            ?? throw new KeyNotFoundException("AD group mapping was not found.");
        if (isActive) mapping.Activate(); else mapping.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteMappingAsync(Guid mappingId, CancellationToken cancellationToken = default)
    {
        if (mappingId == Guid.Empty) throw new ArgumentException("Mapping ID is required.", nameof(mappingId));
        var mapping = await dbContext.AdGroupMappings.SingleOrDefaultAsync(x => x.Id == mappingId, cancellationToken)
            ?? throw new KeyNotFoundException("AD group mapping was not found.");
        dbContext.AdGroupMappings.Remove(mapping);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddOrUpdateUserExceptionAsync(string domainIdentity, ApplicationRole role, UserRoleExceptionEffect effect, string reason, CancellationToken cancellationToken = default)
    {
        ValidateRole(role);
        ValidateEffect(effect);
        domainIdentity = ValidateIdentity(domainIdentity);
        reason = ValidateReason(reason);

        var existing = await dbContext.UserRoleExceptions.SingleOrDefaultAsync(x => x.DomainIdentity == domainIdentity && x.Role == role && x.IsActive, cancellationToken);
        if (existing is null) dbContext.UserRoleExceptions.Add(new UserRoleException(domainIdentity, role, effect, reason));
        else existing.Update(effect, reason);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateUserExceptionAsync(Guid exceptionId, UserRoleExceptionEffect effect, string reason, bool isActive, CancellationToken cancellationToken = default)
    {
        if (exceptionId == Guid.Empty) throw new ArgumentException("Exception ID is required.", nameof(exceptionId));
        ValidateEffect(effect);
        reason = ValidateReason(reason);
        var exception = await dbContext.UserRoleExceptions.SingleOrDefaultAsync(x => x.Id == exceptionId, cancellationToken)
            ?? throw new KeyNotFoundException("User role exception was not found.");
        exception.Update(effect, reason, exception.ExpiresAtUtc);
        if (!isActive) exception.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateUserExceptionAsync(Guid exceptionId, CancellationToken cancellationToken = default)
    {
        if (exceptionId == Guid.Empty) throw new ArgumentException("Exception ID is required.", nameof(exceptionId));
        var exception = await dbContext.UserRoleExceptions.SingleOrDefaultAsync(x => x.Id == exceptionId, cancellationToken)
            ?? throw new KeyNotFoundException("User role exception was not found.");
        exception.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteUserExceptionAsync(Guid exceptionId, CancellationToken cancellationToken = default)
    {
        if (exceptionId == Guid.Empty) throw new ArgumentException("Exception ID is required.", nameof(exceptionId));
        var exception = await dbContext.UserRoleExceptions.SingleOrDefaultAsync(x => x.Id == exceptionId, cancellationToken)
            ?? throw new KeyNotFoundException("User role exception was not found.");
        dbContext.UserRoleExceptions.Remove(exception);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateRole(ApplicationRole role)
    {
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
    }

    private static void ValidateEffect(UserRoleExceptionEffect effect)
    {
        if (!Enum.IsDefined(effect)) throw new ArgumentOutOfRangeException(nameof(effect));
    }

    private static string ValidateGroupName(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) throw new ArgumentException("AD group name is required.", nameof(groupName));
        var normalized = groupName.Trim();
        if (normalized.Length > 256) throw new ArgumentException("AD group name cannot exceed 256 characters.", nameof(groupName));
        return normalized;
    }

    private static string ValidateIdentity(string domainIdentity)
    {
        if (string.IsNullOrWhiteSpace(domainIdentity)) throw new ArgumentException("Domain identity is required.", nameof(domainIdentity));
        var normalized = domainIdentity.Trim();
        if (normalized.Length > 256) throw new ArgumentException("Domain identity cannot exceed 256 characters.", nameof(domainIdentity));
        return normalized;
    }

    private static string ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.", nameof(reason));
        var normalized = reason.Trim();
        if (normalized.Length > 2000) throw new ArgumentException("Reason cannot exceed 2000 characters.", nameof(reason));
        return normalized;
    }
}
