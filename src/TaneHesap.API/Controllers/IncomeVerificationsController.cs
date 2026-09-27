using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.IncomeVerification;

namespace TaneHesap.API.Controllers;

/// <summary>
/// Gün sonu gelir doğrulaması (ADMIN): Kasa Excel'inden beklenen dükkân içi nakit/kart geliri ile kasadan sayılan
/// ve POS'tan okunan gerçek tutarlar. Kaydedilince kasa ve banka hesabına gerçek tutarlar yazılır.
/// </summary>
[ApiController]
[Route("api/income-verifications")]
[Authorize(Roles = "Admin")]
public class IncomeVerificationsController : ControllerBase
{
    private readonly IIncomeVerificationService _service;
    private readonly ICurrentUserService _currentUserService;

    public IncomeVerificationsController(IIncomeVerificationService service, ICurrentUserService currentUserService)
    {
        _service = service;
        _currentUserService = currentUserService;
    }

    private Guid BusinessId => _currentUserService.BusinessId!.Value;
    private Guid UserId => _currentUserService.UserId!.Value;

    [HttpGet("day")]
    public async Task<ActionResult<IncomeVerificationDayDto>> GetDay([FromQuery] DateOnly date, CancellationToken ct)
        => Ok(await _service.GetDayAsync(BusinessId, date, ct));

    /// <summary>Aralıktaki doğrulanmış günler (fark geçmişi), en yeni önce.</summary>
    [HttpGet]
    public async Task<ActionResult<List<IncomeVerificationDayDto>>> GetVerified([FromQuery] DateOnly fromDate, [FromQuery] DateOnly toDate, CancellationToken ct)
        => Ok(await _service.GetVerifiedAsync(BusinessId, fromDate, toDate, ct));

    [HttpPut("{date}")]
    public async Task<ActionResult<IncomeVerificationDayDto>> Save(DateOnly date, [FromBody] SaveIncomeVerificationRequest request, CancellationToken ct)
        => Ok(await _service.SaveAsync(BusinessId, date, request, UserId, ct));

    [HttpDelete("{date}")]
    public async Task<IActionResult> Delete(DateOnly date, CancellationToken ct)
    {
        await _service.DeleteAsync(BusinessId, date, UserId, ct);
        return NoContent();
    }
}
