using Crch.ItBudget.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Crch.ItBudget.Infrastructure.Security;

public sealed record AdGroupMappingSummary(
    Guid Id,
    ApplicationRole Role,
    string GroupName,
    string? Description,
    bool IsActive);

public sealed class SecurityAdministrationService(ItBudgetDbContext dbContext)
{
    public async Task<IReadOnlyList<AdGroupMappingSummary>> ListMappingsAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.AdGroupMappings
            .AsNoTracking()
            .OrderBy(x => x.Role)
            .ThenBy(x => x.GroupName)
            .Select(x => new AdGroupMappingSummary(
                x.Id,
                x.Role,
                x.GroupName,
                x.Description,
                x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task AddOrActivateMappingAsync(
        ApplicationRole role,
        string groupName,
        string? description,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        if (string.IsNullOrWhiteSpace(groupName)) throw new ArgumentException("AD group name is required.", nameof(groupName));

        groupName = groupName.Trim();
        if (groupName.Length > 256) throw new ArgumentException("AD group name cannot exceed 256 characters.", nameof(groupName));

        var existing = await dbContext.AdGroupMappings
            .SingleOrDefaultAsync(
                x => x.Role == role && x.GroupName == groupName,
                cancellationToken);

        if (existing is null)
        {
            dbContext.AdGroupMappings.Add(new AdGroupMapping(role, groupName, description));
        }
        else
        {
            existing.Activate();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetMappingActiveAsync(
        Guid mappingId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (mappingId == Guid.Empty) throw new ArgumentException("Mapping ID is required.", nameof(mappingId));

        var mapping = await dbContext.AdGroupMappings
            .SingleOrDefaultAsync(x => x.Id == mappingId, cancellationToken)
            ?? throw new KeyNotFoundException("AD group mapping was not found.");

        if (isActive) mapping.Activate();
        else mapping.Deactivate();

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
