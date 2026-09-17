using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.Notifications;

namespace TaneHesap.API.Controllers;

/// <summary>
/// ADMIN'e gelen uygulama içi bildirimler (düşük stok, düzenli gider hatırlatması, gün sonu fire
/// uyarısı vb.). bkz. Proje Raporu bölüm 3.13.
/// </summary>
[ApiController]
[Route("api/notifications")]
[Authorize(Roles = "Admin")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserService _currentUserService;

    public NotificationsController(INotificationService notificationService, ICurrentUserService currentUserService)
    {
        _notificationService = notificationService;
        _currentUserService = currentUserService;
    }

    private Guid BusinessId => _currentUserService.BusinessId!.Value;
    private Guid UserId => _currentUserService.UserId!.Value;

    [HttpGet]
    public async Task<ActionResult<List<NotificationDto>>> GetAll([FromQuery] bool unreadOnly, CancellationToken ct)
        => Ok(await _notificationService.GetForUserAsync(BusinessId, UserId, unreadOnly, ct));

    [HttpPost("{id:guid}/mark-read")]
    public async Task<ActionResult<NotificationDto>> MarkAsRead(Guid id, CancellationToken ct)
        => Ok(await _notificationService.MarkAsReadAsync(BusinessId, UserId, id, ct));
}
