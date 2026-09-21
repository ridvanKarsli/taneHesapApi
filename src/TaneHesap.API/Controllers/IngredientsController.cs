using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.Ingredients;

namespace TaneHesap.API.Controllers;

/// <summary>
/// Malzeme kataloğu — ADMIN yönetir, EMPLOYEE sadece görür (reçete/stok ekranlarında kullanılır).
/// bkz. Proje Raporu bölüm 3.3, 3.9.
/// </summary>
[ApiController]
[Route("api/ingredients")]
[Authorize(Roles = "Admin,Employee")]
public class IngredientsController : ControllerBase
{
    private readonly IIngredientService _ingredientService;
    private readonly ICurrentUserService _currentUserService;

    public IngredientsController(IIngredientService ingredientService, ICurrentUserService currentUserService)
    {
        _ingredientService = ingredientService;
        _currentUserService = currentUserService;
    }

    private Guid BusinessId => _currentUserService.BusinessId!.Value;

    [HttpGet]
    public async Task<ActionResult<List<IngredientDto>>> GetAll(CancellationToken ct)
        => Ok(await _ingredientService.GetAllAsync(BusinessId, ct));

    [HttpGet("below-threshold")]
    public async Task<ActionResult<List<IngredientDto>>> GetBelowThreshold(CancellationToken ct)
        => Ok(await _ingredientService.GetBelowThresholdAsync(BusinessId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<IngredientDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _ingredientService.GetByIdAsync(BusinessId, id, ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IngredientDto>> Create([FromBody] CreateIngredientRequest request, CancellationToken ct)
    {
        var userId = _currentUserService.UserId!.Value;
        var created = await _ingredientService.CreateAsync(BusinessId, request, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IngredientDto>> Update(Guid id, [FromBody] UpdateIngredientRequest request, CancellationToken ct)
    {
        var userId = _currentUserService.UserId!.Value;
        return Ok(await _ingredientService.UpdateAsync(BusinessId, id, request, userId, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _ingredientService.DeleteAsync(BusinessId, id, ct);
        return NoContent();
    }
}
