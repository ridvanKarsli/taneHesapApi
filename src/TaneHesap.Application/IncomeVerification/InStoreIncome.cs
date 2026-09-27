using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.IncomeVerification;

/// <summary>Bir günün dükkân içi geliri: nakit (kasaya) ve kart (banka hesabına).</summary>
public readonly record struct DayIncome(decimal Cash, decimal Card)
{
    public decimal Total => Cash + Card;

    public static DayIncome operator +(DayIncome a, DayIncome b) => new(a.Cash + b.Cash, a.Card + b.Card);

    public static DayIncome operator -(DayIncome a, DayIncome b) => new(a.Cash - b.Cash, a.Card - b.Card);
}

/// <summary>
/// Dükkân içi gelirin tek kuralı: beklenen = o günün Kasa Excel satışları (nakit/kart); gün doğrulanmışsa
/// etkin gelir gerçek tutarlardır. Kasa Excel'i olmayan (ya da sonradan silinen) günde doğrulama yok sayılır —
/// satışsız bir gün kasaya gelir yazdıramaz. Kasa, kart komisyonu ve raporlar bu sınıfı kullanır ki üçü hep tutarlı olsun.
/// </summary>
public static class InStoreIncome
{
    public static DayIncome Expected(IEnumerable<DailySalesEntry> daySales)
    {
        var inStore = daySales.Where(s => s.Channel == SalesChannel.InStore).ToList();
        return new DayIncome(
            inStore.Where(s => s.PaymentMethod == PaymentMethod.Cash).Sum(s => s.TotalAmount),
            inStore.Where(s => s.PaymentMethod == PaymentMethod.Card).Sum(s => s.TotalAmount));
    }

    public static bool HasInStoreSales(IEnumerable<DailySalesEntry> daySales)
        => daySales.Any(s => s.Channel == SalesChannel.InStore);

    /// <summary>Doğrulama o gün için geçerli mi (doğrulama var ve Kasa Excel'i yüklü).</summary>
    public static bool IsApplied(IEnumerable<DailySalesEntry> daySales, DailyIncomeVerification? verification)
        => verification is not null && HasInStoreSales(daySales);

    public static DayIncome Effective(IReadOnlyCollection<DailySalesEntry> daySales, DailyIncomeVerification? verification)
        => IsApplied(daySales, verification)
            ? new DayIncome(verification!.ActualCash, verification.ActualCard)
            : Expected(daySales);

    /// <summary>Gerçek − beklenen (doğrulanmamış günde sıfır).</summary>
    public static DayIncome Difference(IReadOnlyCollection<DailySalesEntry> daySales, DailyIncomeVerification? verification)
        => Effective(daySales, verification) - Expected(daySales);

    /// <summary>
    /// Aralıktaki doğrulanmış günlerin toplam farkı (gerçek − beklenen). Raporlar Excel satış toplamına bunu ekler;
    /// böylece rapordaki gelir kasaya/bankaya yazılan gelirle aynıdır.
    /// </summary>
    public static async Task<DayIncome> TotalDifferenceAsync(IUnitOfWork unitOfWork, Guid businessId,
        IEnumerable<DailySalesEntry> salesInRange, DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        var verifications = await LoadAsync(unitOfWork, businessId, fromDate, toDate, ct);
        if (verifications.Count == 0)
        {
            return default;
        }

        var salesByDate = salesInRange.ToLookup(s => s.SaleDate);
        return verifications.Values.Aggregate(default(DayIncome),
            (total, v) => total + Difference(salesByDate[v.VerificationDate].ToList(), v));
    }

    /// <summary>Tarih aralığındaki doğrulamaları güne göre yükler.</summary>
    public static async Task<Dictionary<DateOnly, DailyIncomeVerification>> LoadAsync(
        IUnitOfWork unitOfWork, Guid businessId, DateOnly fromDate, DateOnly toDate, CancellationToken ct)
        => (await unitOfWork.Repository<DailyIncomeVerification>().ListAsync(v =>
                v.BusinessId == businessId && v.VerificationDate >= fromDate && v.VerificationDate <= toDate, ct))
            .ToDictionary(v => v.VerificationDate);
}
