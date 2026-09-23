namespace TaneHesap.Application.Activity;

/// <summary>
/// Denetim kaydının (AuditLog) okunabilir hali: kim, ne zaman, neyi ekledi/değiştirdi/sildi — tutar ve kısa
/// açıklamayla. ADMIN "İşlem Geçmişi" ekranında kullanıcıya/türe/tarihe göre süzer. bkz. Proje Raporu bölüm 3.6.
/// </summary>
public record ActivityEntryDto(
    Guid Id,
    DateTime TimestampUtc,
    Guid UserId,
    string UserName,
    string Action,
    string EntityName,
    string Kind,
    Guid EntityId,
    string Summary,
    decimal? Amount);

public record ActivityQuery(DateOnly? FromDate, DateOnly? ToDate, Guid? UserId, string? EntityName);

public record ActivityUserDto(Guid UserId, string FullName, string Role);

public record ActivityKindDto(string EntityName, string Label);
