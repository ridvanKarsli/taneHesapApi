using TaneHesap.Domain.Common;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// Uygulama içi (in-app) bildirim — düşük stok, ödenmemiş düzenli gider, gün sonu fire uyarısı vb.
/// SignalR ile gerçek zamanlı ADMIN'e iletilir. bkz. Proje Raporu bölüm 3.13.
/// </summary>
public class Notification : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    /// <summary>Alıcı (her zaman bir ADMIN).</summary>
    public Guid RecipientUserId { get; set; }

    public NotificationType Type { get; set; }

    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }
}
