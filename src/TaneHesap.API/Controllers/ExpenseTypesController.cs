using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.ExpenseTypes;

namespace TaneHesap.API.Controllers;

/// <summary>
/// Gider türü kataloğu — ADMIN oluşturur/günceller, EMPLOYEE sadece listeler.
/// bkz. Proje Raporu bölüm 3.2.
/// </summary>
[ApiController]
[Route("api/expense-types")]
[Authorize(Roles = "Admin,Employee")]
public class ExpenseTypesController : ControllerBase
{
    private readonly IExpenseTypeService _expenseTypeService;
    private readonly ICurrentUserService _currentUserService;

    public ExpenseTypesController(IExpenseTypeService expenseTypeService, ICurrentUserService currentUserService)
    {
        _expenseTypeService = expenseTypeService;
        _currentUserService = currentUserService;
    }

    private Guid BusinessId => _currentUserService.BusinessId!.Value;

    [HttpGet]
    public async Task<ActionResult<List<ExpenseTypeDto>>> GetAll(CancellationToken ct)
        => Ok(await _expenseTypeService.GetAllAsync(BusinessId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExpenseTypeDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _expenseTypeService.GetByIdAsync(BusinessId, id, ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ExpenseTypeDto>> Create([FromBody] CreateExpenseTypeRequest request, CancellationToken ct)
    {
        var userId = _currentUserService.UserId!.Value;
        var created = await _expenseTypeService.CreateAsync(BusinessId, request, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ExpenseTypeDto>> Update(Guid id, [FromBody] UpdateExpenseTypeRequest request, CancellationToken ct)
    {
        var userId = _currentUserService.UserId!.Value;
        return Ok(await _expenseTypeService.UpdateAsync(BusinessId, id, request, userId, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _expenseTypeService.DeleteAsync(BusinessId, id, ct);
        return NoContent();
    }
}
