namespace TaneHesap.Application.Reports;

/// <summary>
/// Günlük/haftalık/aylık gelir-gider raporlaması — nakit/kart, kanal (dükkan içi/platform) ve
/// gider kategorisi bazında kırılım. bkz. Proje Raporu bölüm 3.14.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Verilen tarih aralığı için (günlük rapor: fromDate == toDate, haftalık/aylık: geniş aralık)
    /// gelir-gider özetini döner.
    /// </summary>
    Task<PeriodReportDto> GetPeriodReportAsync(Guid businessId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}
