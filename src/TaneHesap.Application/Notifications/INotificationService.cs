using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Notifications;

/// <summary>
/// Uygulama içi (in-app) bildirimler — düşük stok, ödenmemiş düzenli gider, gün sonu fire uyarısı vb.
/// Alıcı her zaman ilgili işletmenin ADMIN kullanıcılarıdır. bkz. Proje Raporu bölüm 3.13.
/// </summary>
public interface INotificationService
{
    /// <summary>Belirli bir kullanıcıya (ADMIN) gelen bildirimleri döner.</summary>
    Task<List<NotificationDto>> GetForUserAsync(Guid businessId, Guid userId, bool unreadOnly, CancellationToken ct = default);

    Task<NotificationDto> MarkAsReadAsync(Guid businessId, Guid userId, Guid notificationId, CancellationToken ct = default);

    /// <summary>İşletmenin tüm aktif ADMIN kullanıcılarına aynı bildirimi oluşturur.</summary>
    Task NotifyAdminsAsync(Guid businessId, NotificationType type, string message, CancellationToken ct = default);

    /// <summary>Düşük stok bildirimi — StockMovements/Suppliers/gün sonu modülleri tarafından çağrılır.</summary>
    Task NotifyLowStockAsync(Guid businessId, string ingredientName, decimal currentQuantity, decimal threshold, CancellationToken ct = default);
}
