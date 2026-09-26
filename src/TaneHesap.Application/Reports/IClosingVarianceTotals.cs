namespace TaneHesap.Application.Reports;

/// <summary>
/// Eski gün sonu sayımlarının kasaya yazdığı farkların (sayılan − satışlardan beklenen gelir) toplamı. Sayım modülü
/// kaldırıldı (kapanış = günün Excel'lerini yüklemek); geçmiş fark kayıtları kasada durduğu için kâr da onları içerir.
/// </summary>
public interface IClosingVarianceTotals
{
    Task<decimal> SumAsync(Guid businessId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}
