namespace TaneHesap.Application.DailySales;

/// <summary>
/// Gün sonu Excel içe aktarımı — satış satırlarını kaydeder ve reçeteye göre o günün beklenen
/// gelir/malzeme tüketimini hesaplar. Stok DÜŞÜMÜ burada YAPILMAZ; ADMIN'in gün sonu manuel
/// gerçek giriş onayına bağlıdır (bkz. DailyClosing modülü, Proje Raporu bölüm 3.10).
/// </summary>
public interface IDailySalesService
{
    /// <summary>
    /// Satış satırlarını doğrular (DishSize/Platform var mı vb.), geçerli olanları kaydeder ve
    /// bir ExcelImportLog oluşturur. Geçersiz satırlar atlanır ve hata listesinde döner.
    /// </summary>
    Task<ImportDailySalesResult> ImportAsync(Guid businessId, ImportDailySalesRequest request, Guid importedByUserId, CancellationToken ct = default);

    Task<List<DailySalesEntryDto>> GetByDateAsync(Guid businessId, DateOnly date, CancellationToken ct = default);

    /// <summary>O gün için sistemin (reçete × satış adedi) hesapladığı beklenen gelir ve malzeme tüketimi.</summary>
    Task<ExpectedDaySummaryDto> GetExpectedSummaryAsync(Guid businessId, DateOnly date, CancellationToken ct = default);
}
