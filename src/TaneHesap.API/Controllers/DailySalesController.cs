using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.DailySales;

namespace TaneHesap.API.Controllers;

/// <summary>
/// Gün sonu Excel içe aktarımı (satış satırları) — sadece ADMIN yükler/görüntüler.
/// bkz. Proje Raporu bölüm 3.5, 3.10.
/// </summary>
[ApiController]
[Route("api/daily-sales")]
[Authorize(Roles = "Admin")]
public class DailySalesController : ControllerBase
{
    private readonly IDailySalesService _dailySalesService;
    private readonly ICurrentUserService _currentUserService;

    public DailySalesController(IDailySalesService dailySalesService, ICurrentUserService currentUserService)
    {
        _dailySalesService = dailySalesService;
        _currentUserService = currentUserService;
    }

    private Guid BusinessId => _currentUserService.BusinessId!.Value;
    private Guid UserId => _currentUserService.UserId!.Value;

    /// <summary>
    /// Ayrıştırılmış satış satırlarını içe aktarır. Excel şablonu netleşene kadar istemci (frontend)
    /// .xlsx dosyasını kendisi ayrıştırıp bu satırları gönderir; şablon netleşince burada
    /// ClosedXML/EPPlus ile doğrudan dosya kabul eden bir uç nokta eklenecektir.
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult<ImportDailySalesResult>> Import([FromBody] ImportDailySalesRequest request, CancellationToken ct)
        => Ok(await _dailySalesService.ImportAsync(BusinessId, request, UserId, ct));

    [HttpGet("by-date")]
    public async Task<ActionResult<List<DailySalesEntryDto>>> GetByDate([FromQuery] DateOnly date, CancellationToken ct)
        => Ok(await _dailySalesService.GetByDateAsync(BusinessId, date, ct));

    [HttpGet("expected-summary")]
    public async Task<ActionResult<ExpectedDaySummaryDto>> GetExpectedSummary([FromQuery] DateOnly date, CancellationToken ct)
        => Ok(await _dailySalesService.GetExpectedSummaryAsync(BusinessId, date, ct));

    [HttpDelete("entries/{id:guid}")]
    public async Task<IActionResult> DeleteEntry(Guid id, CancellationToken ct)
    {
        await _dailySalesService.DeleteEntryAsync(BusinessId, id, UserId, ct);
        return NoContent();
    }

    /// <summary>Bir günün tüm satışlarını siler (aynı dosya iki kez yüklendiyse düzeltmek için).</summary>
    [HttpDelete("by-date")]
    public async Task<ActionResult<int>> DeleteByDate([FromQuery] DateOnly date, CancellationToken ct)
        => Ok(await _dailySalesService.DeleteByDateAsync(BusinessId, date, UserId, ct));
}
