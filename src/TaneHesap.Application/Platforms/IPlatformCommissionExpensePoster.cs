namespace TaneHesap.Application.Platforms;

/// <summary>
/// Paket servis platformu (Yemeksepeti, Getir vb.) üzerinden yapılan satışların komisyon tutarını
/// otomatik olarak bir Expense kaydına dönüştüren servisin soyutlaması. bkz. Proje Raporu bölüm 3.4.
/// DailySalesService'ten ayrı tutulur (Single Responsibility) — satış içe aktarımı ile komisyon/gider
/// dönüşümü birbirinden bağımsız iki sorumluluktur.
/// </summary>
public interface IPlatformCommissionExpensePoster
{
    /// <summary>
    /// Verilen tarihler için, işletmenin platform bazlı brüt satış tutarlarından komisyon hesaplar
    /// ve her platform+tarih için idempotent şekilde (varsa günceller, yoksa oluşturur) bir Expense
    /// kaydı oluşturur/günceller. Değişiklikleri kalıcı hale getirir (SaveChangesAsync çağırır).
    /// </summary>
    Task PostCommissionExpensesAsync(Guid businessId, IEnumerable<DateOnly> saleDates, Guid postedByUserId, CancellationToken ct = default);
}
