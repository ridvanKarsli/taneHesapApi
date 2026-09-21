using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Employees;

public class EmployeeService : IEmployeeService
{
    private readonly IIdentityService _identityService;

    public EmployeeService(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<List<EmployeeDto>> GetAllAsync(Guid businessId, CancellationToken ct = default)
    {
        var employees = await _identityService.GetEmployeesByBusinessAsync(businessId);
        return employees.Select(e => new EmployeeDto(e.UserId, e.Username, e.FullName, e.IsActive)).ToList();
    }

    public async Task<EmployeeDto> CreateAsync(Guid businessId, CreateEmployeeRequest request, CancellationToken ct = default)
    {
        var result = await _identityService.CreateEmployeeAsync(businessId, request.Username, request.Password, request.FullName);
        if (!result.Succeeded || result.UserId is null)
        {
            throw new ConflictAppException(string.Join("; ", result.Errors));
        }

        return new EmployeeDto(result.UserId.Value, request.Username, request.FullName, true);
    }

    public async Task DeleteAsync(Guid businessId, Guid employeeId, CancellationToken ct = default)
    {
        if (!await _identityService.DeleteUserAsync(employeeId, businessId, UserRole.Employee))
        {
            throw new NotFoundException("Employee", employeeId);
        }
    }

    public async Task<EmployeeDto> UpdateAsync(Guid businessId, Guid employeeId, UpdateEmployeeRequest request, CancellationToken ct = default)
    {
        var updated = await _identityService.UpdateUserAsync(
            employeeId, businessId, UserRole.Employee, request.FullName, request.Username, request.Password);
        if (!updated)
        {
            throw new NotFoundException("Employee", employeeId);
        }

        await _identityService.SetActiveAsync(employeeId, request.IsActive);

        var user = await _identityService.GetByIdAsync(employeeId)
            ?? throw new NotFoundException("Employee", employeeId);

        return new EmployeeDto(user.UserId, user.Username, user.FullName, user.IsActive);
    }
}
