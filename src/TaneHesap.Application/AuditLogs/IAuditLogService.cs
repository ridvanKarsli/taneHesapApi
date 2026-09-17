namespace TaneHesap.Application.AuditLogs;

/// <summary>
/// Salt okunur denetim kaydı sorgulama — kayıtlar EF Core interceptor tarafından (Infrastructure
/// katmanında) otomatik oluşturulur, bu servis sadece görüntüleme sağlar. bkz. Proje Raporu bölüm 3.6.
/// </summary>
public interface IAuditLogService
{
    Task<List<AuditLogDto>> GetLogsAsync(AuditLogQuery query, CancellationToken ct = default);
}
