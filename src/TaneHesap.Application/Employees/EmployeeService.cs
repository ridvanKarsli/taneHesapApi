using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Employees;

public class EmployeeService : IEmployeeService
{
    private readonly IIdentityService _identityService;
    private readonly IUnitOfWork _unitOfWork;

    public EmployeeService(IIdentityService identityService, IUnitOfWork unitOfWork)
    {
        _identityService = identityService;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<EmployeeDto>> GetAllAsync(Guid businessId, CancellationToken ct = default)
    {
        var employees = await _identityService.GetEmployeesByBusinessAsync(businessId);
        var wages = (await _unitOfWork.Repository<EmployeeProfile>().ListAsync(p => p.BusinessId == businessId, ct))
            .ToDictionary(p => p.UserId, p => p.HourlyWage);
        return employees.Select(e => ToDto(e, wages.GetValueOrDefault(e.UserId))).ToList();
    }

    public async Task<EmployeeDto> CreateAsync(Guid businessId, CreateEmployeeRequest request, CancellationToken ct = default)
    {
        EnsureValidWage(request.HourlyWage);
        var result = await _identityService.CreateEmployeeAsync(businessId, request.Username, request.Password, request.FullName);
        if (!result.Succeeded || result.UserId is null)
        {
            throw new ConflictAppException(string.Join("; ", result.Errors));
        }

        await UpsertWageAsync(businessId, result.UserId.Value, request.HourlyWage, ct);
        return new EmployeeDto(result.UserId.Value, request.Username, request.FullName, true, request.HourlyWage);
    }

    public async Task DeleteAsync(Guid businessId, Guid employeeId, CancellationToken ct = default)
    {
        if (!await _identityService.DeleteUserAsync(employeeId, businessId, UserRole.Employee))
        {
            throw new NotFoundException("Employee", employeeId);
        }
        // Profil ve çalışma kayıtları geçmiş (cüzdan/gider izlenebilirliği) olarak kalır.
    }

    public async Task<EmployeeDto> UpdateAsync(Guid businessId, Guid employeeId, UpdateEmployeeRequest request, CancellationToken ct = default)
    {
        EnsureValidWage(request.HourlyWage);
        var updated = await _identityService.UpdateUserAsync(
            employeeId, businessId, UserRole.Employee, request.FullName, request.Username, request.Password);
        if (!updated)
        {
            throw new NotFoundException("Employee", employeeId);
        }

        await _identityService.SetActiveAsync(employeeId, request.IsActive);
        await UpsertWageAsync(businessId, employeeId, request.HourlyWage, ct);

        var user = await _identityService.GetByIdAsync(employeeId)
            ?? throw new NotFoundException("Employee", employeeId);

        return ToDto(user, request.HourlyWage);
    }

    private async Task UpsertWageAsync(Guid businessId, Guid userId, decimal hourlyWage, CancellationToken ct)
    {
        var repo = _unitOfWork.Repository<EmployeeProfile>();
        var profile = (await repo.ListAsync(p => p.BusinessId == businessId && p.UserId == userId, ct)).FirstOrDefault();
        if (profile is null)
        {
            await repo.AddAsync(new EmployeeProfile { BusinessId = businessId, UserId = userId, HourlyWage = hourlyWage }, ct);
        }
        else if (profile.HourlyWage != hourlyWage)
        {
            profile.HourlyWage = hourlyWage;
            profile.UpdatedAtUtc = DateTime.UtcNow;
            repo.Update(profile);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static void EnsureValidWage(decimal hourlyWage)
    {
        if (hourlyWage < 0)
        {
            throw new ValidationAppException("Saatlik ücret negatif olamaz.");
        }
    }

    private static EmployeeDto ToDto(ApplicationUserInfo user, decimal hourlyWage)
        => new(user.UserId, user.Username, user.FullName, user.IsActive, hourlyWage);
}
