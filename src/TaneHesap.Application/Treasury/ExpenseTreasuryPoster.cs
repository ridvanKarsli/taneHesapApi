using TaneHesap.Application.Platforms;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Treasury;

public class ExpenseTreasuryPoster : IExpenseTreasuryPoster
{
    public const string SourceReferenceType = nameof(Expense);

    /// <summary>
    /// Satış gelirinden kesilen otomatik giderler (POS ve platform komisyonu): para zaten o gelirle hesaba girer,
    /// bu yüzden bakiye kontrolüne takılmaz (aksi halde satış yüklemesinin sırasına göre yanlışlıkla reddedilirdi).
    /// </summary>
    private static readonly HashSet<string> SaleDeductionSources = new()
    {
        CardFeeExpensePoster.SourceType,
        PlatformCommissionExpensePoster.SourceType
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ITreasuryFundsGuard _fundsGuard;

    public ExpenseTreasuryPoster(IUnitOfWork unitOfWork, ITreasuryFundsGuard fundsGuard)
    {
        _unitOfWork = unitOfWork;
        _fundsGuard = fundsGuard;
    }

    public async Task SyncAsync(Expense expense, CancellationToken ct = default)
    {
        var stale = await RemoveRowsAsync(expense.Id, ct);

        var account = ToAccount(expense.PaymentMethod);
        if (account is null)
        {
            return;
        }

        var row = new TreasuryTransaction
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
        };

        if (expense.SourceReferenceType is null || !SaleDeductionSources.Contains(expense.SourceReferenceType))
        {
            await _fundsGuard.EnsureAvailableAsync(expense.BusinessId, stale, new[] { row }, ct);
        }

        await _unitOfWork.Repository<TreasuryTransaction>().AddAsync(row, ct);
    }

    public async Task RemoveAsync(Guid expenseId, CancellationToken ct = default) => await RemoveRowsAsync(expenseId, ct);

    private async Task<List<TreasuryTransaction>> RemoveRowsAsync(Guid expenseId, CancellationToken ct)
    {
        var repo = _unitOfWork.Repository<TreasuryTransaction>();
        var stale = await repo.ListAsync(t => t.SourceReferenceType == SourceReferenceType && t.SourceReferenceId == expenseId, ct);
        foreach (var row in stale)
        {
            repo.Remove(row);
        }

        return stale;
    }

    private static TreasuryAccount? ToAccount(PaymentMethod? method) => method switch
    {
        PaymentMethod.Cash => TreasuryAccount.Cash,
        PaymentMethod.Bank => TreasuryAccount.Bank,
        PaymentMethod.Card => TreasuryAccount.CreditCard,
        _ => null
    };
}
