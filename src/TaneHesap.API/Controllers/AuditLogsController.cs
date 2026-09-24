using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.AuditLogs;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Enums;

namespace TaneHesap.API.Controllers;

/// <summary>
/// Değiştirilemez (append-only) denetim kayıtları — kayıtlar EF Core interceptor'ı tarafından
/// otomatik üretilir (bkz. Infrastructure/Persistence/Interceptors/AuditSaveChangesInterceptor),
/// bu uç nokta sadece görüntüleme sağlar. ADMIN sadece kendi işletmesini, SUPER_ADMIN tüm
/// işletmeleri (veya dilerse tek bir işletmeyi) görebilir. bkz. Proje Raporu bölüm 3.6.
/// </summary>
[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = "SuperAdmin")] // Ham JSON denetim kaydı teknik görünümdür; işletme sahibi okunabilir "İşlem Geçmişi"ni kullanır (ActivityController).
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUserService;

    public AuditLogsController(IAuditLogService auditLogService, ICurrentUserService currentUserService)
    {
        _auditLogService = auditLogService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AuditLogDto>>> GetLogs(
        [FromQuery] Guid? businessId,
        [FromQuery] string? entityName,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken ct)
    {
        // ADMIN yalnızca kendi işletmesini görebilir; sorgudaki businessId parametresi göz ardı edilir.
        // SUPER_ADMIN dilerse tek bir işletmeye filtreleyebilir, dilerse tümünü (businessId = null) görür.
        var scopedBusinessId = _currentUserService.Role == UserRole.SuperAdmin
            ? businessId
            : _currentUserService.BusinessId;

        var query = new AuditLogQuery(scopedBusinessId, entityName, fromUtc, toUtc);
        return Ok(await _auditLogService.GetLogsAsync(query, ct));
    }
}
