namespace TaneHesap.Application.DailySales;

/// <summary>
/// Gün sonu satış verisi değişince (içe aktarma / silme) etkilenen günler için türetilmiş kayıtları
/// idempotent şekilde yeniden hesaplayan iş kuralı: platform komisyonu gideri, reçeteye göre stok
/// düşümü, kasaya satış geliri. DailySalesService bu arayüzün tüm kayıtlı implementasyonlarını sırayla
/// çağırır — yeni bir türetilmiş kayıt eklemek yeni bir sınıf + DI kaydıdır (Open/Closed).
/// Her implementasyon kendi değişikliklerini kalıcı hale getirir (SaveChangesAsync çağırır).
/// </summary>
public interface IDailySalesSideEffect
{
    Task ApplyAsync(Guid businessId, IReadOnlyCollection<DateOnly> saleDates, Guid userId, CancellationToken ct = default);
}
