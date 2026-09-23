namespace TaneHesap.Application.Reports;

/// <summary>
/// Aylık rapor: tüm giderler ÷ satılan tabak = tabak başı genel maliyet; malzeme başına gelir verimliliği ve
/// önceki aya göre düşüş uyarısı. Ay bitince (arka plan görevi) rapor kalıcı özetle kapatılır ve ADMIN'lere
/// bildirim gönderilir. bkz. Proje Raporu bölüm 3.16.
/// </summary>
public interface IMonthlyReportService
{
    Task<MonthlyReportDto> GetAsync(Guid businessId, int year, int month, CancellationToken ct = default);

    /// <summary>Bitmiş ve henüz kapatılmamış her ay için (tüm işletmeler) rapor özetini kaydeder ve bildirir; kapatılan ay sayısını döner. Sistem kapsamında çağrılır.</summary>
    Task<int> CloseFinishedMonthsAsync(DateOnly today, CancellationToken ct = default);
}
