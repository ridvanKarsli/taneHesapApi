using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Common;
using TaneHesap.Domain.Entities;

namespace TaneHesap.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Her SaveChanges çağrısında değişen entity'ler için otomatik olarak (append-only) AuditLog
/// kaydı üretir. Servis katmanının audit logging'i bilmesine gerek kalmaz — tek sorumluluk
/// (Single Responsibility) burada toplanır ve yeni bir entity eklendiğinde otomatik olarak
/// denetime dahil olur (Open/Closed). bkz. Proje Raporu bölüm 3.6.
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    /// <summary>
    /// Denetim kapsamı dışında tutulan entity'ler: AuditLog'un kendisi (sonsuz döngü), Notification
    /// (sistem tarafından üretilir, kullanıcı eylemi değildir) ve RefreshToken (yüksek frekanslı,
    /// iş açısından anlamlı olmayan güvenlik detayı).
    /// </summary>
    private static readonly HashSet<Type> ExcludedTypes = new()
    {
        typeof(AuditLog),
        typeof(Notification),
        typeof(RefreshToken)
    };

    private readonly ICurrentUserService _currentUserService;

    public AuditSaveChangesInterceptor(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AppendAuditLogs(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        AppendAuditLogs(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void AppendAuditLogs(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var entries = context.ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => !ExcludedTypes.Contains(e.Entity.GetType()))
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        var userId = _currentUserService.UserId ?? Guid.Empty;
        var businessId = _currentUserService.BusinessId;

        foreach (var entry in entries)
        {
            var (oldValuesJson, newValuesJson) = CaptureValues(entry);

            context.Set<AuditLog>().Add(new AuditLog
            {
                BusinessId = businessId,
                UserId = userId,
                ActionType = entry.State.ToString(),
                EntityName = entry.Entity.GetType().Name,
                EntityId = entry.Entity.Id,
                OldValuesJson = oldValuesJson,
                NewValuesJson = newValuesJson,
                TimestampUtc = DateTime.UtcNow
            });
        }
    }

    private static (string? OldValuesJson, string? NewValuesJson) CaptureValues(EntityEntry<BaseEntity> entry) => entry.State switch
    {
        EntityState.Added => (null, Serialize(entry.CurrentValues)),
        EntityState.Deleted => (Serialize(entry.OriginalValues), null),
        EntityState.Modified => CaptureModifiedValues(entry),
        _ => (null, null)
    };

    /// <summary>Modified durumunda sadece GERÇEKTEN değişen alanları loglar — gereksiz büyüme olmaz.</summary>
    private static (string? OldValuesJson, string? NewValuesJson) CaptureModifiedValues(EntityEntry<BaseEntity> entry)
    {
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();

        foreach (var property in entry.Properties.Where(p => p.IsModified))
        {
            oldValues[property.Metadata.Name] = property.OriginalValue;
            newValues[property.Metadata.Name] = property.CurrentValue;
        }

        return (JsonSerializer.Serialize(oldValues), JsonSerializer.Serialize(newValues));
    }

    private static string Serialize(PropertyValues values)
    {
        var dict = values.Properties.ToDictionary(p => p.Name, p => values[p]);
        return JsonSerializer.Serialize(dict);
    }
}
