using System.Text.Json;
using LedgerForge.Application.Abstractions;
using LedgerForge.Domain.Auditing;
using LedgerForge.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LedgerForge.Infrastructure.Persistence.Auditing;

public sealed class AuditSaveChangesInterceptor(IAuditRequestContext requestContext) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> OmittedProperties = new(StringComparer.Ordinal)
    {
        nameof(AuditableEntity.RowVersion),
        "RawDataJson"
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddAuditEvents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddAuditEvents(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditEvents(DbContext? context)
    {
        if (context is null) return;
        var entries = context.ChangeTracker.Entries().ToArray();

        foreach (var auditEntry in entries.Where(x => x.Entity is AuditEvent))
        {
            if (auditEntry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Audit events are immutable and cannot be modified or deleted.");
        }

        var auditable = entries
            .Where(x => x.Entity is AuditableEntity && x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToArray();
        if (auditable.Length == 0) return;

        var now = DateTimeOffset.UtcNow;
        var actor = string.IsNullOrWhiteSpace(requestContext.Actor) ? "system" : requestContext.Actor.Trim();
        var events = new List<AuditEvent>(auditable.Length);

        foreach (var entry in auditable)
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException($"Hard deletion is not permitted for auditable entity {entry.Metadata.ClrType.Name}. Archive, cancel, reject, close, or reverse the record instead.");

            Stamp(entry, actor, now);
            var entity = (AuditableEntity)entry.Entity;
            var action = ResolveAction(entry);
            var before = entry.State == EntityState.Modified ? SerializeSnapshot(entry, original: true, modifiedOnly: true) : null;
            var after = SerializeSnapshot(entry, original: false, modifiedOnly: entry.State == EntityState.Modified);

            events.Add(new AuditEvent(
                actor,
                entry.Metadata.ClrType.Name,
                entity.Id,
                action,
                before,
                after,
                requestContext.CorrelationId,
                now,
                requestContext.RequestMethod,
                requestContext.RequestPath,
                requestContext.RemoteAddress,
                requestContext.UserAgent));
        }

        context.Set<AuditEvent>().AddRange(events);
    }

    private static void Stamp(EntityEntry entry, string actor, DateTimeOffset now)
    {
        if (entry.State == EntityState.Added)
        {
            entry.Property(nameof(AuditableEntity.CreatedBy)).CurrentValue = actor;
            entry.Property(nameof(AuditableEntity.CreatedAtUtc)).CurrentValue = now;
        }
        else if (entry.State == EntityState.Modified)
        {
            entry.Property(nameof(AuditableEntity.ModifiedBy)).CurrentValue = actor;
            entry.Property(nameof(AuditableEntity.ModifiedAtUtc)).CurrentValue = now;
        }
    }

    private static AuditAction ResolveAction(EntityEntry entry)
    {
        if (entry.State == EntityState.Added) return AuditAction.Created;
        var archived = entry.Properties.FirstOrDefault(x => x.Metadata.Name == nameof(AuditableEntity.IsArchived));
        if (archived is not null && archived.IsModified && archived.OriginalValue is false && archived.CurrentValue is true)
            return AuditAction.Archived;
        return AuditAction.Updated;
    }

    private static string? SerializeSnapshot(EntityEntry entry, bool original, bool modifiedOnly)
    {
        var values = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in entry.Properties)
        {
            if (OmittedProperties.Contains(property.Metadata.Name)) continue;
            if (modifiedOnly && !property.IsModified) continue;
            var value = original ? property.OriginalValue : property.CurrentValue;
            values[property.Metadata.Name] = Normalize(value);
        }
        return values.Count == 0 ? null : JsonSerializer.Serialize(values, JsonOptions);
    }

    private static object? Normalize(object? value)
        => value switch
        {
            null => null,
            byte[] => "<binary>",
            string text when text.Length > 2000 => text[..2000] + "…",
            _ => value
        };
}
