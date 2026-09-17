using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.Dishes;

namespace TaneHesap.API.Controllers;

/// <summary>
/// Ürün/tabak boyu/reçete yönetimi ve otomatik tabak maliyeti hesaplama — ADMIN yönetir,
/// EMPLOYEE görüntüler. bkz. Proje Raporu bölüm 3.3, 3.11.
/// </summary>
[ApiController]
[Route("api/dishes")]
[Authorize(Roles = "Admin,Employee")]
public class DishesController : ControllerBase
{
    private readonly IDishService _dishService;
    private readonly ICurrentUserService _currentUserService;

    public DishesController(IDishService dishService, ICurrentUserService currentUserService)
    {
        _dishService = dishService;
        _currentUserService = currentUserService;
    }

    private Guid BusinessId => _currentUserService.BusinessId!.Value;

    [HttpGet]
    public async Task<ActionResult<List<DishDto>>> GetAll(CancellationToken ct)
        => Ok(await _dishService.GetAllAsync(BusinessId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DishDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _dishService.GetByIdAsync(BusinessId, id, ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DishDto>> Create([FromBody] CreateDishRequest request, CancellationToken ct)
    {
        var userId = _currentUserService.UserId!.Value;
        var created = await _dishService.CreateDishAsync(BusinessId, request, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("{dishId:guid}/sizes")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DishSizeDto>> AddSize(Guid dishId, [FromBody] CreateDishSizeRequest request, CancellationToken ct)
    {
        var userId = _currentUserService.UserId!.Value;
        var created = await _dishService.AddSizeAsync(BusinessId, dishId, request, userId, ct);
        return Ok(created);
    }

    [HttpPut("{dishId:guid}/sizes/{sizeId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DishSizeDto>> UpdateSize(Guid dishId, Guid sizeId, [FromBody] UpdateDishSizeRequest request, CancellationToken ct)
    {
        var userId = _currentUserService.UserId!.Value;
        return Ok(await _dishService.UpdateSizeAsync(BusinessId, dishId, sizeId, request, userId, ct));
    }
}
