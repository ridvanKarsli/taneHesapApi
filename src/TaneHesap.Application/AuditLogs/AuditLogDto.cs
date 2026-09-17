namespace TaneHesap.Application.AuditLogs;

/// <summary>Değiştirilemez (append-only) denetim kaydı görünümü. bkz. Proje Raporu bölüm 3.6.</summary>
public record AuditLogDto(
    Guid Id,
    Guid? BusinessId,
    Guid UserId,
    string ActionType,
    string EntityName,
    Guid EntityId,
    string? OldValuesJson,
    string? NewValuesJson,
    DateTime TimestampUtc);

/// <summary>
/// businessId null ise (yalnızca SUPER_ADMIN kullanabilir) tüm işletmeler dahil edilir; ADMIN için
/// controller katmanı bu alanı her zaman kendi işletmesiyle doldurur.
/// </summary>
public record AuditLogQuery(Guid? BusinessId, string? EntityName, DateTime? FromUtc, DateTime? ToUtc);
