using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Activity;
using TaneHesap.Application.Common.Interfaces;

namespace TaneHesap.API.Controllers;

/// <summary>İşlem geçmişi — kim, ne zaman, hangi gideri/satışı/ödemeyi ekledi/değiştirdi/sildi (okunabilir denetim kaydı). Sadece ADMIN.</summary>
[ApiController]
[Route("api/activity")]
[Authorize(Roles = "Admin")]
public class ActivityController : ControllerBase
{
    private readonly IActivityService _activityService;
    private readonly ICurrentUserService _currentUserService;

    public ActivityController(IActivityService activityService, ICurrentUserService currentUserService)
    {
        _activityService = activityService;
        _currentUserService = currentUserService;
    }

    private Guid BusinessId => _currentUserService.BusinessId!.Value;

    [HttpGet]
    public async Task<ActionResult<List<ActivityEntryDto>>> Get(
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, [FromQuery] Guid? userId, [FromQuery] string? entityName, CancellationToken ct)
        => Ok(await _activityService.GetAsync(BusinessId, new ActivityQuery(fromDate, toDate, userId, entityName), ct));

    [HttpGet("users")]
    public async Task<ActionResult<List<ActivityUserDto>>> GetUsers(CancellationToken ct)
        => Ok(await _activityService.GetUsersAsync(BusinessId, ct));

    [HttpGet("kinds")]
    public ActionResult<IReadOnlyList<ActivityKindDto>> GetKinds() => Ok(_activityService.GetKinds());
}
