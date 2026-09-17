using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.Stock;

namespace TaneHesap.API.Controllers;

/// <summary>
/// Malzeme stok hareketleri (manuel düzeltme, fire vb.) — ADMIN kaydeder, ADMIN+EMPLOYEE görüntüler.
/// Alış kaynaklı hareketler Suppliers modülü, satış tüketimi ise gün sonu modülü üzerinden otomatik
/// oluşturulur. bkz. Proje Raporu bölüm 3.9.
/// </summary>
[ApiController]
[Route("api/stock-movements")]
[Authorize(Roles = "Admin,Employee")]
public class StockMovementsController : ControllerBase
{
    private readonly IStockMovementService _stockMovementService;
    private readonly ICurrentUserService _currentUserService;

    public StockMovementsController(IStockMovementService stockMovementService, ICurrentUserService currentUserService)
    {
        _stockMovementService = stockMovementService;
        _currentUserService = currentUserService;
    }

    private Guid BusinessId => _currentUserService.BusinessId!.Value;

    [HttpGet]
    public async Task<ActionResult<List<StockMovementDto>>> GetAll([FromQuery] Guid? ingredientId, CancellationToken ct)
        => Ok(await _stockMovementService.GetAllAsync(BusinessId, ingredientId, ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<StockMovementDto>> Create([FromBody] CreateStockMovementRequest request, CancellationToken ct)
    {
        var userId = _currentUserService.UserId!.Value;
        var created = await _stockMovementService.CreateAsync(BusinessId, request, userId, ct);
        return Ok(created);
    }
}
