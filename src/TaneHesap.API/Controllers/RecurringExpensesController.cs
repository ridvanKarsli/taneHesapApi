using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.RecurringExpenses;

namespace TaneHesap.API.Controllers;

/// <summary>
/// Kira, elektrik gibi düzenli giderler — sadece ADMIN yönetir. bkz. Proje Raporu bölüm 3.8.
/// </summary>
[ApiController]
[Route("api/recurring-expenses")]
[Authorize(Roles = "Admin")]
public class RecurringExpensesController : ControllerBase
{
    private readonly IRecurringExpenseService _recurringExpenseService;
    private readonly ICurrentUserService _currentUserService;

    public RecurringExpensesController(IRecurringExpenseService recurringExpenseService, ICurrentUserService currentUserService)
    {
        _recurringExpenseService = recurringExpenseService;
        _currentUserService = currentUserService;
    }

    private Guid BusinessId => _currentUserService.BusinessId!.Value;
    private Guid UserId => _currentUserService.UserId!.Value;

    [HttpGet]
    public async Task<ActionResult<List<RecurringExpenseDto>>> GetAll(CancellationToken ct)
        => Ok(await _recurringExpenseService.GetAllAsync(BusinessId, ct));

    [HttpGet("due-for-reminder")]
    public async Task<ActionResult<List<RecurringExpenseDto>>> GetDueForReminder(CancellationToken ct)
        => Ok(await _recurringExpenseService.GetDueForReminderAsync(BusinessId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecurringExpenseDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _recurringExpenseService.GetByIdAsync(BusinessId, id, ct));

    [HttpPost]
    public async Task<ActionResult<RecurringExpenseDto>> Create([FromBody] CreateRecurringExpenseRequest request, CancellationToken ct)
    {
        var created = await _recurringExpenseService.CreateAsync(BusinessId, request, UserId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RecurringExpenseDto>> Update(Guid id, [FromBody] UpdateRecurringExpenseRequest request, CancellationToken ct)
        => Ok(await _recurringExpenseService.UpdateAsync(BusinessId, id, request, UserId, ct));

    [HttpPost("{id:guid}/mark-period-paid")]
    public async Task<ActionResult<RecurringExpenseDto>> MarkPeriodPaid(Guid id, [FromBody] MarkPeriodPaidRequest request, CancellationToken ct)
        => Ok(await _recurringExpenseService.MarkPeriodPaidAsync(BusinessId, id, request, UserId, ct));
}
