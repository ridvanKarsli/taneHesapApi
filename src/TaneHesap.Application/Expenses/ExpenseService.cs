using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.Treasury;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Expenses;

public class ExpenseService : IExpenseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdentityService _identityService;
    private readonly IExpenseTreasuryPoster _treasuryPoster;
    private readonly IPaymentCardResolver _cardResolver;

    public ExpenseService(IUnitOfWork unitOfWork, IIdentityService identityService, IExpenseTreasuryPoster treasuryPoster, IPaymentCardResolver cardResolver)
    {
        _unitOfWork = unitOfWork;
        _identityService = identityService;
        _treasuryPoster = treasuryPoster;
        _cardResolver = cardResolver;
    }

    public async Task<List<ExpenseDto>> GetListAsync(Guid businessId, ExpenseListFilter filter, CancellationToken ct = default)
    {
        var expenses = await _unitOfWork.Repository<Expense>().ListAsync(e => e.BusinessId == businessId, ct);

        var query = expenses.AsEnumerable();

        if (filter.FromDate.HasValue)
            query = query.Where(e => e.ExpenseDate >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(e => e.ExpenseDate <= filter.ToDate.Value);

        if (filter.ExpenseTypeId.HasValue)
            query = query.Where(e => e.ExpenseTypeId == filter.ExpenseTypeId.Value);

        if (filter.CreatedByUserId.HasValue)
            query = query.Where(e => e.CreatedByUserId == filter.CreatedByUserId.Value);

        if (filter.EmployeeUserId.HasValue)
            query = query.Where(e => e.EmployeeUserId == filter.EmployeeUserId.Value);

        var lookups = await LoadLookupsAsync(businessId, ct);

        return query
            .OrderByDescending(e => e.ExpenseDate)
            .Select(e => ToDto(e, lookups))
            .ToList();
    }

    public async Task<ExpenseDto> CreateAsync(Guid businessId, CreateExpenseRequest request, Guid createdByUserId, CancellationToken ct = default)
    {
        var expenseType = await GetExpenseTypeAsync(businessId, request.ExpenseTypeId, ct);
        var expense = new Expense { BusinessId = businessId, CreatedByUserId = createdByUserId };
        await ApplyAsync(expense, expenseType, request.Amount, request.Quantity, request.ExpenseDate,
            request.PaymentMethod, request.PaymentCardId, request.EmployeeUserId, request.Description, ct);

        await _unitOfWork.Repository<Expense>().AddAsync(expense, ct);
        await _treasuryPoster.SyncAsync(expense, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(expense, await LoadLookupsAsync(businessId, ct));
    }

    public async Task<ExpenseDto> UpdateAsync(Guid businessId, Guid id, UpdateExpenseRequest request, Guid updatedByUserId, Guid? onlyCreatedBy, CancellationToken ct = default)
    {
        var expense = await GetEditableAsync(businessId, id, onlyCreatedBy, ct);
        var expenseType = await GetExpenseTypeAsync(businessId, request.ExpenseTypeId, ct);
        await ApplyAsync(expense, expenseType, request.Amount, request.Quantity, request.ExpenseDate,
            request.PaymentMethod, request.PaymentCardId, request.EmployeeUserId, request.Description, ct);
        expense.UpdatedByUserId = updatedByUserId;
        expense.UpdatedAtUtc = DateTime.UtcNow;

        _unitOfWork.Repository<Expense>().Update(expense);
        await _treasuryPoster.SyncAsync(expense, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(expense, await LoadLookupsAsync(businessId, ct));
    }

    public async Task DeleteAsync(Guid businessId, Guid id, Guid? onlyCreatedBy, CancellationToken ct = default)
    {
        var expense = await GetEditableAsync(businessId, id, onlyCreatedBy, ct);
        await _treasuryPoster.RemoveAsync(expense.Id, ct);
        _unitOfWork.Repository<Expense>().Remove(expense);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Ortak alan ataması ve iş kuralları (kart / çalışan tutarlılığı) — oluşturma ve güncelleme aynı yolu kullanır.</summary>
    private async Task ApplyAsync(Expense expense, ExpenseType expenseType, decimal amount, decimal? quantity, DateOnly date,
        PaymentMethod? paymentMethod, Guid? paymentCardId, Guid? employeeUserId, string? description, CancellationToken ct)
    {
        if (amount <= 0)
        {
            throw new ValidationAppException("Gider tutarı 0'dan büyük olmalı.");
        }

        expense.ExpenseTypeId = expenseType.Id;
        expense.Amount = amount;
        expense.Quantity = quantity;
        expense.ExpenseDate = date;
        expense.PaymentMethod = paymentMethod;
        expense.PaymentCardId = await _cardResolver.ResolveAsync(expense.BusinessId, paymentMethod, paymentCardId, ct);
        expense.EmployeeUserId = await ResolveEmployeeAsync(expense.BusinessId, expenseType, employeeUserId, ct);
        expense.Description = description;
    }


    private async Task<Guid?> ResolveEmployeeAsync(Guid businessId, ExpenseType expenseType, Guid? employeeUserId, CancellationToken ct)
    {
        if (expenseType.Category != ExpenseCategory.Personnel)
        {
            return null;
        }

        if (employeeUserId is null)
        {
            return null; // Personel kategorisinde çalışan seçmek zorunlu değil (örn. genel personel gideri).
        }

        var user = await _identityService.GetByIdAsync(employeeUserId.Value);
        if (user is null || user.BusinessId != businessId || user.Role != UserRole.Employee)
        {
            throw new NotFoundException("Employee", employeeUserId.Value);
        }

        return user.UserId;
    }

    private async Task<ExpenseType> GetExpenseTypeAsync(Guid businessId, Guid expenseTypeId, CancellationToken ct)
    {
        var expenseType = await _unitOfWork.Repository<ExpenseType>().GetByIdAsync(expenseTypeId, ct);
        if (expenseType is null || expenseType.BusinessId != businessId)
        {
            throw new NotFoundException(nameof(ExpenseType), expenseTypeId);
        }

        return expenseType;
    }

    private async Task<Expense> GetEditableAsync(Guid businessId, Guid id, Guid? onlyCreatedBy, CancellationToken ct)
    {
        var expense = await _unitOfWork.Repository<Expense>().GetByIdAsync(id, ct);
        if (expense is null || expense.BusinessId != businessId
            || (onlyCreatedBy.HasValue && expense.CreatedByUserId != onlyCreatedBy.Value))
        {
            throw new NotFoundException(nameof(Expense), id);
        }

        if (expense.IsAutomatic)
        {
            throw new ConflictAppException("Bu gider sistem tarafından otomatik oluşturuldu (satış, düzenli gider veya tedarikçi ödemesinden). Kaynağındaki kaydı düzenleyin; burada değiştirilemez.");
        }

        return expense;
    }

    private async Task<Lookups> LoadLookupsAsync(Guid businessId, CancellationToken ct)
    {
        var types = (await _unitOfWork.Repository<ExpenseType>().ListAsync(t => t.BusinessId == businessId, ct)).ToDictionary(t => t.Id);
        var cards = (await _unitOfWork.Repository<PaymentCard>().ListAsync(c => c.BusinessId == businessId, ct)).ToDictionary(c => c.Id, c => c.Name);
        var employees = (await _identityService.GetEmployeesByBusinessAsync(businessId)).ToDictionary(e => e.UserId, e => e.FullName);
        return new Lookups(types, cards, employees);
    }

    private sealed record Lookups(
        IReadOnlyDictionary<Guid, ExpenseType> Types,
        IReadOnlyDictionary<Guid, string> CardNames,
        IReadOnlyDictionary<Guid, string> EmployeeNames);

    private static ExpenseDto ToDto(Expense e, Lookups lookups)
    {
        var type = lookups.Types.GetValueOrDefault(e.ExpenseTypeId);
        return new ExpenseDto(
            e.Id, e.ExpenseTypeId, type?.Name ?? "-", type?.Category ?? ExpenseCategory.Other,
            e.Amount, e.Quantity, e.ExpenseDate, e.PaymentMethod,
            e.PaymentCardId, e.PaymentCardId.HasValue ? lookups.CardNames.GetValueOrDefault(e.PaymentCardId.Value) : null,
            e.EmployeeUserId, e.EmployeeUserId.HasValue ? lookups.EmployeeNames.GetValueOrDefault(e.EmployeeUserId.Value) : null,
            e.Description, e.SourceReferenceType, e.CreatedByUserId, e.CreatedAtUtc);
    }
}
