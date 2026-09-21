using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;

namespace TaneHesap.Application.Expenses;

public class ExpenseService : IExpenseService
{
    private readonly IUnitOfWork _unitOfWork;

    public ExpenseService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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

        var expenseTypes = await _unitOfWork.Repository<ExpenseType>().ListAsync(t => t.BusinessId == businessId, ct);
        var expenseTypeNames = expenseTypes.ToDictionary(t => t.Id, t => t.Name);

        return query
            .OrderByDescending(e => e.ExpenseDate)
            .Select(e => ToDto(e, expenseTypeNames.GetValueOrDefault(e.ExpenseTypeId, "-")))
            .ToList();
    }

    public async Task<ExpenseDto> CreateAsync(Guid businessId, CreateExpenseRequest request, Guid createdByUserId, CancellationToken ct = default)
    {
        var expenseType = await _unitOfWork.Repository<ExpenseType>().GetByIdAsync(request.ExpenseTypeId, ct);
        if (expenseType is null || expenseType.BusinessId != businessId)
        {
            throw new NotFoundException(nameof(ExpenseType), request.ExpenseTypeId);
        }

        var expense = new Expense
        {
            BusinessId = businessId,
            ExpenseTypeId = request.ExpenseTypeId,
            Amount = request.Amount,
            Quantity = request.Quantity,
            ExpenseDate = request.ExpenseDate,
            PaymentMethod = request.PaymentMethod,
            Description = request.Description,
            CreatedByUserId = createdByUserId
        };

        await _unitOfWork.Repository<Expense>().AddAsync(expense, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(expense, expenseType.Name);
    }

    private static ExpenseDto ToDto(Expense e, string expenseTypeName) => new(
        e.Id, e.ExpenseTypeId, expenseTypeName, e.Amount, e.Quantity, e.ExpenseDate,
        e.PaymentMethod, e.Description, e.CreatedByUserId, e.CreatedAtUtc);
}
