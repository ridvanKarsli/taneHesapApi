using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.DailySales;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Treasury;

/// <summary>
/// Gün sonu satışlarının parasını kasaya yazar (gün bazında idempotent): nakit satışlar nakit kasasına,
/// kart satışları (dükkan içi POS ve platform hakedişi) brüt olarak kart kasasına. Bankanın POS kesintisi
/// ayrı bir otomatik giderdir (CardFeeExpensePoster), platform komisyonu da öyle (PlatformCommissionExpensePoster).
/// bkz. Proje Raporu bölüm 3.15.
/// </summary>
public class SalesTreasuryPoster : IDailySalesSideEffect
{
    public const string SourceReferenceType = "DailySales";

    private readonly IUnitOfWork _unitOfWork;

    public SalesTreasuryPoster(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task ApplyAsync(Guid businessId, IReadOnlyCollection<DateOnly> saleDates, Guid userId, CancellationToken ct = default)
    {
        var dates = saleDates.Distinct().ToList();
        if (dates.Count == 0)
        {
            return;
        }

        var repo = _unitOfWork.Repository<TreasuryTransaction>();
        var sales = await _unitOfWork.Repository<DailySalesEntry>()
            .ListAsync(e => e.BusinessId == businessId && dates.Contains(e.SaleDate), ct);

        foreach (var date in dates)
        {
            foreach (var stale in await repo.ListAsync(t => t.BusinessId == businessId && t.SourceReferenceType == SourceReferenceType && t.TransactionDate == date, ct))
            {
                repo.Remove(stale);
            }

            var daySales = sales.Where(s => s.SaleDate == date).ToList();
            await AddAsync(businessId, TreasuryAccount.Cash, daySales.Where(s => s.PaymentMethod == PaymentMethod.Cash).Sum(s => s.TotalAmount), date, "Nakit satışlar", userId, ct);
            await AddAsync(businessId, TreasuryAccount.Bank, daySales.Where(s => s.PaymentMethod == PaymentMethod.Card).Sum(s => s.TotalAmount), date, "Kart satışları (brüt)", userId, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task AddAsync(Guid businessId, TreasuryAccount account, decimal amount, DateOnly date, string description, Guid userId, CancellationToken ct)
    {
        if (amount == 0)
        {
            return;
        }

        await _unitOfWork.Repository<TreasuryTransaction>().AddAsync(new TreasuryTransaction
        {
            BusinessId = businessId,
            Account = account,
            Amount = amount,
            Kind = TreasuryTransactionKind.SalesRevenue,
            TransactionDate = date,
            Description = description,
            SourceReferenceType = SourceReferenceType,
            CreatedByUserId = userId
        }, ct);
    }
}
