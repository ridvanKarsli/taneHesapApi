using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.Reports;

namespace TaneHesap.API.Controllers;

/// <summary>
/// Günlük/haftalık/aylık gelir-gider raporları — nakit/kart ve kanal bazlı kırılım.
/// fromDate/toDate aynı gün verilirse günlük, geniş aralık verilirse haftalık/aylık rapor olur.
/// Sadece ADMIN görür. bkz. Proje Raporu bölüm 3.14.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IMonthlyReportService _monthlyReportService;
    private readonly ICurrentUserService _currentUserService;

    public ReportsController(IReportService reportService, IMonthlyReportService monthlyReportService, ICurrentUserService currentUserService)
    {
        _reportService = reportService;
        _monthlyReportService = monthlyReportService;
        _currentUserService = currentUserService;
    }

    private Guid BusinessId => _currentUserService.BusinessId!.Value;

    [HttpGet("period")]
    public async Task<ActionResult<PeriodReportDto>> GetPeriodReport([FromQuery] DateOnly fromDate, [FromQuery] DateOnly toDate, CancellationToken ct)
        => Ok(await _reportService.GetPeriodReportAsync(BusinessId, fromDate, toDate, ct));

    /// <summary>Aylık rapor: tabak başı genel maliyet ve malzeme verimliliği uyarıları (bkz. Proje Raporu bölüm 3.16).</summary>
    [HttpGet("monthly")]
    public async Task<ActionResult<MonthlyReportDto>> GetMonthlyReport([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (month is < 1 or > 12 || year < 2000)
        {
            return BadRequest(new { error = "Geçerli bir yıl ve ay (1-12) girin." });
        }

        return Ok(await _monthlyReportService.GetAsync(BusinessId, year, month, ct));
    }
}
