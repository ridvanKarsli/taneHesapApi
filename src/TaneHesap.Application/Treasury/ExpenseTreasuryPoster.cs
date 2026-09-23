using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Treasury;

public class ExpenseTreasuryPoster : IExpenseTreasuryPoster
{
    public const string SourceReferenceType = nameof(Expense);

    private readonly IUnitOfWork _unitOfWork;

    public ExpenseTreasuryPoster(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task SyncAsync(Expense expense, CancellationToken ct = default)
    {
        await RemoveAsync(expense.Id, ct);

        var account = ToAccount(expense.PaymentMethod);
        if (account is null)
        {
            return;
        }

        await _unitOfWork.Repository<TreasuryTransaction>().AddAsync(new TreasuryTransaction
        {
            BusinessId = expense.BusinessId,
            Account = account.Value,
            PaymentCardId = account == TreasuryAccount.CreditCard ? expense.PaymentCardId : null,
            Amount = -expense.Amount,
            Kind = TreasuryTransactionKind.Expense,
            TransactionDate = expense.ExpenseDate,
            Description = expense.Description,
            SourceReferenceType = SourceReferenceType,
            SourceReferenceId = expense.Id,
            CreatedByUserId = expense.UpdatedByUserId ?? expense.CreatedByUserId
        }, ct);
    }

    public async Task RemoveAsync(Guid expenseId, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<TreasuryTransaction>();
        foreach (var stale in await repo.ListAsync(t => t.SourceReferenceType == SourceReferenceType && t.SourceReferenceId == expenseId, ct))
        {
            repo.Remove(stale);
        }
    }

    private static TreasuryAccount? ToAccount(PaymentMethod? method) => method switch
    {
        PaymentMethod.Cash => TreasuryAccount.Cash,
        PaymentMethod.Bank => TreasuryAccount.Bank,
        PaymentMethod.Card => TreasuryAccount.CreditCard,
        _ => null
    };
}
