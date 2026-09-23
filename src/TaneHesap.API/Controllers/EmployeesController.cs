using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.Employees;

namespace TaneHesap.API.Controllers;

/// <summary>
/// ADMIN'in kendi işletmesine çalışan (EMPLOYEE) ekleyip yönetmesi.
/// bkz. Proje Raporu bölüm 3.7.
/// </summary>
[ApiController]
[Route("api/employees")]
[Authorize(Roles = "Admin")]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;
    private readonly IEmployeeWalletService _walletService;
    private readonly ICurrentUserService _currentUserService;

    public EmployeesController(IEmployeeService employeeService, IEmployeeWalletService walletService, ICurrentUserService currentUserService)
    {
        _employeeService = employeeService;
        _walletService = walletService;
        _currentUserService = currentUserService;
    }

    private Guid BusinessId => _currentUserService.BusinessId!.Value;
    private Guid UserId => _currentUserService.UserId!.Value;

    [HttpGet]
    public async Task<ActionResult<List<EmployeeDto>>> GetAll(CancellationToken ct)
        => Ok(await _employeeService.GetAllAsync(BusinessId, ct));

    [HttpPost]
    public async Task<ActionResult<EmployeeDto>> Create([FromBody] CreateEmployeeRequest request, CancellationToken ct)
        => Ok(await _employeeService.CreateAsync(BusinessId, request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> Update(Guid id, [FromBody] UpdateEmployeeRequest request, CancellationToken ct)
        => Ok(await _employeeService.UpdateAsync(BusinessId, id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _employeeService.DeleteAsync(BusinessId, id, ct);
        return NoContent();
    }

    // --- Cüzdan: saatlik ücret × çalışma saati hak edişi, ödemeler (bkz. Proje Raporu bölüm 3.7) ---

    [HttpGet("{id:guid}/wallet")]
    public async Task<ActionResult<EmployeeWalletDto>> GetWallet(Guid id, CancellationToken ct)
        => Ok(await _walletService.GetWalletAsync(BusinessId, id, ct));

    [HttpPost("{id:guid}/work-logs")]
    public async Task<ActionResult<EmployeeWorkLogDto>> AddWorkLog(Guid id, [FromBody] CreateWorkLogRequest request, CancellationToken ct)
        => Ok(await _walletService.AddWorkLogAsync(BusinessId, id, request, UserId, ct));

    [HttpDelete("{id:guid}/work-logs/{workLogId:guid}")]
    public async Task<IActionResult> DeleteWorkLog(Guid id, Guid workLogId, CancellationToken ct)
    {
        await _walletService.DeleteWorkLogAsync(BusinessId, id, workLogId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/payments")]
    public async Task<ActionResult<EmployeePaymentDto>> Pay(Guid id, [FromBody] CreateEmployeePaymentRequest request, CancellationToken ct)
        => Ok(await _walletService.PayAsync(BusinessId, id, request, UserId, ct));
}
