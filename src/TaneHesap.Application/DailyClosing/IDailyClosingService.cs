namespace TaneHesap.Application.DailyClosing;

/// <summary>
/// Gün sonu kapanışı: ADMIN'in girdiği gerçek gelir/tüketim ile DailySales (Excel) modülünün
/// hesapladığı beklenen değerleri karşılaştırıp fire/kayıp raporu üretir; stok düşümü de bu adımda
/// kesinleşir. bkz. Proje Raporu bölüm 3.10.
/// </summary>
public interface IDailyClosingService
{
    /// <summary>Gerçek girişi kaydeder/günceller, stoğu düşer (yeniden gönderimde önce geri alır) ve o günün fire raporunu (yeniden) üretir.</summary>
    Task<DailyLossReportDto> SubmitActualEntryAsync(Guid businessId, SubmitDailyActualEntryRequest request, Guid enteredByUserId, CancellationToken ct = default);

    Task<DailyActualEntryDto?> GetActualEntryByDateAsync(Guid businessId, DateOnly date, CancellationToken ct = default);

    Task<DailyLossReportDto?> GetLossReportByDateAsync(Guid businessId, DateOnly date, CancellationToken ct = default);

    Task<List<DailyLossReportDto>> GetLossReportsAsync(Guid businessId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}
