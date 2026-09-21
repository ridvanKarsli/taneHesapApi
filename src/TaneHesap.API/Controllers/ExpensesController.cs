using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.Expenses;
using TaneHesap.Domain.Enums;

namespace TaneHesap.API.Controllers;

/// <summary>
/// Gider girişi — hem ADMIN hem EMPLOYEE kullanabilir. bkz. Proje Raporu bölüm 3.1.
/// </summary>
[ApiController]
[Route("api/expenses")]
[Authorize(Roles = "Admin,Employee")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ICurrentUserService _currentUserService;

    public ExpensesController(IExpenseService expenseService, ICurrentUserService currentUserService)
    {
        _expenseService = expenseService;
        _currentUserService = currentUserService;
    }

    private Guid BusinessId => _currentUserService.BusinessId!.Value;

    [HttpGet]
    public async Task<ActionResult<List<ExpenseDto>>> GetList(
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, [FromQuery] Guid? expenseTypeId, CancellationToken ct)
    {
        // EMPLOYEE sadece kendi girdiği giderleri görür; ADMIN işletmenin tümünü (bkz. Proje Raporu bölüm 2).
        var onlyCreatedBy = _currentUserService.Role == UserRole.Employee ? _currentUserService.UserId : null;
        var filter = new ExpenseListFilter(fromDate, toDate, expenseTypeId, onlyCreatedBy);
        return Ok(await _expenseService.GetListAsync(BusinessId, filter, ct));
    }

    [HttpPost]
    public async Task<ActionResult<ExpenseDto>> Create([FromBody] CreateExpenseRequest request, CancellationToken ct)
    {
        var userId = _currentUserService.UserId!.Value;
        var created = await _expenseService.CreateAsync(BusinessId, request, userId, ct);
        return Ok(created);
    }
}
