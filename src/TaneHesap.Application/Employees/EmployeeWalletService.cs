using TaneHesap.Application.Common;
using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.ExpenseTypes;
using TaneHesap.Application.Expenses;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Employees;

public class EmployeeWalletService : IEmployeeWalletService
{
    public const string PaymentExpenseTypeName = "Personel Ödemesi";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdentityService _identityService;
    private readonly IExpenseService _expenseService;
    private readonly IExpenseTypeCatalog _typeCatalog;

    public EmployeeWalletService(IUnitOfWork unitOfWork, IIdentityService identityService, IExpenseService expenseService, IExpenseTypeCatalog typeCatalog)
    {
        _unitOfWork = unitOfWork;
        _identityService = identityService;
        _expenseService = expenseService;
        _typeCatalog = typeCatalog;
    }

    public async Task<EmployeeWalletDto> GetWalletAsync(Guid businessId, Guid employeeUserId, CancellationToken ct = default)
    {
        var employee = await GetEmployeeAsync(businessId, employeeUserId);
        var profile = await _unitOfWork.Repository<EmployeeProfile>().ListAsync(p => p.BusinessId == businessId && p.UserId == employeeUserId, ct);
        var workLogs = await _unitOfWork.Repository<EmployeeWorkLog>().ListAsync(w => w.BusinessId == businessId && w.UserId == employeeUserId, ct);
        var payments = await _expenseService.GetListAsync(businessId, new ExpenseListFilter(null, null, null, null, employeeUserId), ct);

        var totalEarned = workLogs.Sum(w => w.Amount);
        var totalPaid = payments.Sum(p => p.Amount);

        return new EmployeeWalletDto(
            employee.UserId,
            employee.FullName,
            profile.FirstOrDefault()?.HourlyWage ?? 0,
            workLogs.Sum(w => w.Hours),
            totalEarned,
            totalPaid,
            totalEarned - totalPaid,
            workLogs.OrderByDescending(w => w.WorkDate).ThenByDescending(w => w.CreatedAtUtc).Select(ToDto).ToList(),
            payments.Select(p => new EmployeePaymentDto(p.Id, p.ExpenseDate, p.Amount, p.PaymentMethod, p.PaymentCardName, p.Description)).ToList());
    }

    public async Task<EmployeeWorkLogDto> AddWorkLogAsync(Guid businessId, Guid employeeUserId, CreateWorkLogRequest request, Guid userId, CancellationToken ct = default)
    {
        if (request.Hours <= 0 || request.Hours > 24)
        {
            throw new ValidationAppException("Çalışma saati 0 ile 24 arasında olmalı.");
        }

        await GetEmployeeAsync(businessId, employeeUserId);
        var profile = (await _unitOfWork.Repository<EmployeeProfile>().ListAsync(p => p.BusinessId == businessId && p.UserId == employeeUserId, ct)).FirstOrDefault();
        var hourlyWage = profile?.HourlyWage ?? 0;
        if (hourlyWage <= 0)
        {
            throw new ValidationAppException("Önce çalışanın saatlik ücretini tanımlayın (Çalışanlar → Düzenle).");
        }

        var workLog = new EmployeeWorkLog
        {
            BusinessId = businessId,
            UserId = employeeUserId,
            WorkDate = request.WorkDate,
            Hours = request.Hours,
            HourlyWage = hourlyWage,
            Amount = MoneyMath.Round(request.Hours * hourlyWage),
            Note = request.Note,
            CreatedByUserId = userId
        };

        await _unitOfWork.Repository<EmployeeWorkLog>().AddAsync(workLog, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return ToDto(workLog);
    }

    public async Task DeleteWorkLogAsync(Guid businessId, Guid employeeUserId, Guid workLogId, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<EmployeeWorkLog>();
        var workLog = await repo.GetByIdAsync(workLogId, ct);
        if (workLog is null || workLog.BusinessId != businessId || workLog.UserId != employeeUserId)
        {
            throw new NotFoundException(nameof(EmployeeWorkLog), workLogId);
        }

        repo.Remove(workLog);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<EmployeePaymentDto> PayAsync(Guid businessId, Guid employeeUserId, CreateEmployeePaymentRequest request, Guid userId, CancellationToken ct = default)
    {
        var employee = await GetEmployeeAsync(businessId, employeeUserId);
        var expenseType = await _typeCatalog.GetOrCreateAsync(businessId, PaymentExpenseTypeName, ExpenseCategory.Personnel, userId, ct);

        var expense = await _expenseService.CreateAsync(businessId, new CreateExpenseRequest(
            expenseType.Id, request.Amount, null, request.Date, request.PaymentMethod, request.PaymentCardId, employeeUserId,
            request.Note ?? $"{employee.FullName} — personel ödemesi"), userId, ct);

        return new EmployeePaymentDto(expense.Id, expense.ExpenseDate, expense.Amount, expense.PaymentMethod, expense.PaymentCardName, expense.Description);
    }

    private async Task<ApplicationUserInfo> GetEmployeeAsync(Guid businessId, Guid employeeUserId)
    {
        var user = await _identityService.GetByIdAsync(employeeUserId);
        if (user is null || user.BusinessId != businessId || user.Role != UserRole.Employee)
        {
            throw new NotFoundException("Employee", employeeUserId);
        }

        return user;
    }

    private static EmployeeWorkLogDto ToDto(EmployeeWorkLog w) => new(w.Id, w.WorkDate, w.Hours, w.HourlyWage, w.Amount, w.Note);
}
