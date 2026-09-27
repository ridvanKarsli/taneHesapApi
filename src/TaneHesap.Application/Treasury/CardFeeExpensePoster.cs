using TaneHesap.Application.IncomeVerification;
using TaneHesap.Application.Common;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.DailySales;
using TaneHesap.Application.Expenses;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Treasury;

/// <summary>
/// Dükkan içi kart (POS) gelirinde (doğrulanmışsa gerçek, değilse beklenen) bankanın kestiği komisyonu (Business.CardFeePercentage, varsayılan %3)
/// gün başına otomatik bir gidere dönüştürür; ödeme şekli Bank olduğu için banka hesabından düşer ve raporlarda
/// gider olarak görünür. Satış geliri brüt yazılır (SalesTreasuryPoster), kesinti bu giderle netleşir.
/// </summary>
public class CardFeeExpensePoster : IIncomeDependentSideEffect
{
    public const string SourceType = "CardFee";
    private const string ExpenseTypeName = "POS Komisyonu";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAutoExpenseWriter _autoExpenses;

    public CardFeeExpensePoster(IUnitOfWork unitOfWork, IAutoExpenseWriter autoExpenses)
    {
        _unitOfWork = unitOfWork;
        _autoExpenses = autoExpenses;
    }

    public async Task ApplyAsync(Guid businessId, IReadOnlyCollection<DateOnly> saleDates, Guid userId, CancellationToken ct = default)
    {
        var dates = saleDates.Distinct().ToList();
        if (dates.Count == 0)
        {
            return;
        }

        var business = await _unitOfWork.Repository<Business>().GetByIdAsync(businessId, ct);
        var feePercent = business?.CardFeePercentage ?? 0m;
        var inStoreSales = await _unitOfWork.Repository<DailySalesEntry>()
            .ListAsync(e => e.BusinessId == businessId && e.Channel == SalesChannel.InStore && dates.Contains(e.SaleDate), ct);
        var verifications = await InStoreIncome.LoadAsync(_unitOfWork, businessId, dates.Min(), dates.Max(), ct);

        foreach (var date in dates)
        {
            var sourceId = DeterministicGuid.From(SourceType, businessId, date);
            var daySales = inStoreSales.Where(s => s.SaleDate == date).ToList();
            var cardIncome = InStoreIncome.Effective(daySales, verifications.GetValueOrDefault(date)).Card;
            var fee = MoneyMath.Round(cardIncome * feePercent / 100m);
            if (fee <= 0)
            {
                await _autoExpenses.RemoveAsync(businessId, SourceType, sourceId, ct);
                continue;
            }

            await _autoExpenses.UpsertAsync(new AutoExpenseSpec(
                businessId, SourceType, sourceId, ExpenseTypeName, ExpenseCategory.Other, fee, date, PaymentMethod.Bank, null,
                $"POS komisyonu %{feePercent:0.##} (otomatik, {date:dd.MM.yyyy})", userId), ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }
}
