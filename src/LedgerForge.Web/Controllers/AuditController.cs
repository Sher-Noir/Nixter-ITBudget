using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Web.Models.Auditing;
using LedgerForge.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LedgerForge.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ViewAudit)]
[Route("audit")]
public sealed class AuditController(LedgerForgeDbContext dbContext) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? actor,
        string? entityType,
        Guid? entityId,
        string? correlationId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 500);
        var query = dbContext.AuditEvents.AsNoTracking().AsQueryable();

        actor = Normalize(actor);
        entityType = Normalize(entityType);
        correlationId = Normalize(correlationId);
        if (actor is not null) query = query.Where(x => x.Actor.Contains(actor));
        if (entityType is not null) query = query.Where(x => x.EntityType.Contains(entityType));
        if (entityId is not null) query = query.Where(x => x.EntityId == entityId);
        if (correlationId is not null) query = query.Where(x => x.CorrelationId == correlationId);
        if (fromUtc is not null) query = query.Where(x => x.OccurredAtUtc >= fromUtc.Value);
        if (toUtc is not null) query = query.Where(x => x.OccurredAtUtc <= toUtc.Value);

        var events = await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .Select(x => new AuditEventViewModel(
                x.Id,
                x.OccurredAtUtc,
                x.Actor,
                x.EntityType,
                x.EntityId,
                x.Action,
                x.CorrelationId,
                x.RequestMethod,
                x.RequestPath,
                x.RemoteAddress,
                x.UserAgent,
                x.BeforeJson,
                x.AfterJson))
            .ToListAsync(cancellationToken);

        return View(new AuditIndexViewModel(
            events,
            actor,
            entityType,
            entityId,
            correlationId,
            fromUtc,
            toUtc,
            take));
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
