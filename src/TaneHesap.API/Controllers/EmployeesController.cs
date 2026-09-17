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
    private readonly ICurrentUserService _currentUserService;

    public EmployeesController(IEmployeeService employeeService, ICurrentUserService currentUserService)
    {
        _employeeService = employeeService;
        _currentUserService = currentUserService;
    }

    private Guid BusinessId => _currentUserService.BusinessId!.Value;

    [HttpGet]
    public async Task<ActionResult<List<EmployeeDto>>> GetAll(CancellationToken ct)
        => Ok(await _employeeService.GetAllAsync(BusinessId, ct));

    [HttpPost]
    public async Task<ActionResult<EmployeeDto>> Create([FromBody] CreateEmployeeRequest request, CancellationToken ct)
        => Ok(await _employeeService.CreateAsync(BusinessId, request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> Update(Guid id, [FromBody] UpdateEmployeeRequest request, CancellationToken ct)
        => Ok(await _employeeService.UpdateAsync(BusinessId, id, request, ct));
}
