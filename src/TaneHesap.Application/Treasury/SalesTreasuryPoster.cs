using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.DailySales;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Treasury;

/// <summary>
/// Gün sonu satışlarının parasını kasaya yazar (gün bazında idempotent): nakit satışlar nakit kasasına;
/// dükkan içi kart satışları kart kasasına brüt olarak + bankanın kestiği komisyon (Business.CardFeePercentage,
/// varsayılan %3) ayrı bir CardFee hareketiyle; paket servis platformundan gelen kart ödemeleri platformun
/// hakedişi olduğundan banka komisyonu kesilmeden kart kasasına (platform komisyonu zaten ayrı bir giderdir).
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
        if (saleDates.Count == 0)
        {
            return;
        }

        var business = await _unitOfWork.Repository<Business>().GetByIdAsync(businessId, ct);
        var feeRate = (business?.CardFeePercentage ?? 0m) / 100m;

        var repo = _unitOfWork.Repository<TreasuryTransaction>();
        var sales = await _unitOfWork.Repository<DailySalesEntry>()
            .ListAsync(e => e.BusinessId == businessId && saleDates.Contains(e.SaleDate), ct);

        foreach (var date in saleDates.Distinct())
        {
            foreach (var stale in await repo.ListAsync(t => t.BusinessId == businessId && t.SourceReferenceType == SourceReferenceType && t.TransactionDate == date, ct))
            {
                repo.Remove(stale);
            }

            var daySales = sales.Where(s => s.SaleDate == date).ToList();
            var cash = daySales.Where(s => s.PaymentMethod == PaymentMethod.Cash).Sum(s => s.TotalAmount);
            var inStoreCard = daySales.Where(s => s.PaymentMethod == PaymentMethod.Card && s.Channel == SalesChannel.InStore).Sum(s => s.TotalAmount);
            var platformCard = daySales.Where(s => s.PaymentMethod == PaymentMethod.Card && s.Channel == SalesChannel.Platform).Sum(s => s.TotalAmount);
            var fee = Math.Round(inStoreCard * feeRate, 2);

            await AddAsync(businessId, TreasuryAccount.Cash, cash, TreasuryTransactionKind.SalesRevenue, date, "Nakit satışlar", userId, ct);
            await AddAsync(businessId, TreasuryAccount.Bank, inStoreCard + platformCard, TreasuryTransactionKind.SalesRevenue, date, "Kart satışları (brüt)", userId, ct);
            await AddAsync(businessId, TreasuryAccount.Bank, -fee, TreasuryTransactionKind.CardFee, date, $"Kart komisyonu (%{business?.CardFeePercentage ?? 0m:0.##})", userId, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task AddAsync(Guid businessId, TreasuryAccount account, decimal amount, TreasuryTransactionKind kind, DateOnly date, string description, Guid userId, CancellationToken ct)
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
            Kind = kind,
            TransactionDate = date,
            Description = description,
            SourceReferenceType = SourceReferenceType,
            CreatedByUserId = userId
        }, ct);
    }
}
