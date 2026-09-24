namespace TaneHesap.Application.DailyClosing;

/// <summary>
/// Gün sonu kapanışı: ADMIN'in girdiği gerçek gelir/tüketim ile satışlardan hesaplanan beklenen değerleri
/// karşılaştırıp fire/kayıp raporu üretir; stok farkı ve kasa sayım farkı bu adımda işlenir. bkz. bölüm 3.10.
/// </summary>
public interface IDailyClosingService
{
    /// <summary>Gerçek girişi kaydeder/günceller; stok farkını, kasa sayım farkını ve fire raporunu (yeniden) üretir.</summary>
    Task<DailyLossReportDto> SubmitActualEntryAsync(Guid businessId, SubmitDailyActualEntryRequest request, Guid enteredByUserId, CancellationToken ct = default);

    /// <summary>O gün için kapanış girişi varsa türetilmiş kayıtları (stok farkı, kasa farkı, rapor) güncel satışlara göre yeniden hesaplar; giriş yoksa false.</summary>
    Task<bool> RecalculateAsync(Guid businessId, DateOnly date, Guid userId, CancellationToken ct = default);

    Task<DailyActualEntryDto?> GetActualEntryByDateAsync(Guid businessId, DateOnly date, CancellationToken ct = default);

    Task<DailyLossReportDto?> GetLossReportByDateAsync(Guid businessId, DateOnly date, CancellationToken ct = default);

    Task<List<DailyLossReportDto>> GetLossReportsAsync(Guid businessId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}
