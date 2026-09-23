using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.ExpenseTypes;
using TaneHesap.Application.Treasury;
using TaneHesap.Domain.Entities;

namespace TaneHesap.Application.Expenses;

public class AutoExpenseWriter : IAutoExpenseWriter
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExpenseTypeCatalog _typeCatalog;
    private readonly IExpenseTreasuryPoster _treasuryPoster;

    public AutoExpenseWriter(IUnitOfWork unitOfWork, IExpenseTypeCatalog typeCatalog, IExpenseTreasuryPoster treasuryPoster)
    {
        _unitOfWork = unitOfWork;
        _typeCatalog = typeCatalog;
        _treasuryPoster = treasuryPoster;
    }

    public async Task<Expense> UpsertAsync(AutoExpenseSpec spec, CancellationToken ct = default)
    {
        var expenseType = await _typeCatalog.GetOrCreateAsync(spec.BusinessId, spec.ExpenseTypeName, spec.Category, spec.UserId, ct);
        var repo = _unitOfWork.Repository<Expense>();
        var expense = (await repo.ListAsync(e => e.BusinessId == spec.BusinessId
            && e.SourceReferenceType == spec.SourceType && e.SourceReferenceId == spec.SourceId, ct)).FirstOrDefault();

        if (expense is null)
        {
            expense = new Expense
            {
                BusinessId = spec.BusinessId,
                SourceReferenceType = spec.SourceType,
                SourceReferenceId = spec.SourceId,
                CreatedByUserId = spec.UserId
            };
            await repo.AddAsync(expense, ct);
        }
        else
        {
            expense.UpdatedByUserId = spec.UserId;
            expense.UpdatedAtUtc = DateTime.UtcNow;
            repo.Update(expense);
        }

        expense.ExpenseTypeId = expenseType.Id;
        expense.ExpenseType = expenseType;
        expense.Amount = spec.Amount;
        expense.ExpenseDate = spec.Date;
        expense.PaymentMethod = spec.PaymentMethod;
        expense.PaymentCardId = spec.PaymentCardId;
        expense.Description = spec.Description;

        await _treasuryPoster.SyncAsync(expense, ct);
        return expense;
    }

    public async Task RemoveAsync(Guid businessId, string sourceType, Guid sourceId, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<Expense>();
        foreach (var expense in await repo.ListAsync(e => e.BusinessId == businessId && e.SourceReferenceType == sourceType && e.SourceReferenceId == sourceId, ct))
        {
            await _treasuryPoster.RemoveAsync(expense.Id, ct);
            repo.Remove(expense);
        }
    }
}
