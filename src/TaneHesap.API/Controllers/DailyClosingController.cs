using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.DailyClosing;

namespace TaneHesap.API.Controllers;

/// <summary>
/// Gün sonu kapanışı: ADMIN'in gerçekleşen gelir/tüketim girişi ve otomatik üretilen fire/kayıp
/// raporu — sadece ADMIN. bkz. Proje Raporu bölüm 3.10.
/// </summary>
[ApiController]
[Route("api/daily-closing")]
[Authorize(Roles = "Admin")]
public class DailyClosingController : ControllerBase
{
    private readonly IDailyClosingService _dailyClosingService;
    private readonly ICurrentUserService _currentUserService;

    public DailyClosingController(IDailyClosingService dailyClosingService, ICurrentUserService currentUserService)
    {
        _dailyClosingService = dailyClosingService;
        _currentUserService = currentUserService;
    }

    private Guid BusinessId => _currentUserService.BusinessId!.Value;
    private Guid UserId => _currentUserService.UserId!.Value;

    [HttpPost("actual-entry")]
    public async Task<ActionResult<DailyLossReportDto>> SubmitActualEntry([FromBody] SubmitDailyActualEntryRequest request, CancellationToken ct)
        => Ok(await _dailyClosingService.SubmitActualEntryAsync(BusinessId, request, UserId, ct));

    [HttpGet("actual-entry")]
    public async Task<ActionResult<DailyActualEntryDto?>> GetActualEntry([FromQuery] DateOnly date, CancellationToken ct)
        => Ok(await _dailyClosingService.GetActualEntryByDateAsync(BusinessId, date, ct));

    [HttpGet("loss-report")]
    public async Task<ActionResult<DailyLossReportDto?>> GetLossReport([FromQuery] DateOnly date, CancellationToken ct)
        => Ok(await _dailyClosingService.GetLossReportByDateAsync(BusinessId, date, ct));

    [HttpGet("loss-reports")]
    public async Task<ActionResult<List<DailyLossReportDto>>> GetLossReports([FromQuery] DateOnly fromDate, [FromQuery] DateOnly toDate, CancellationToken ct)
        => Ok(await _dailyClosingService.GetLossReportsAsync(BusinessId, fromDate, toDate, ct));
}
