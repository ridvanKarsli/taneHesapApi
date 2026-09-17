namespace TaneHesap.Application.Common.Interfaces;

/// <summary>
/// Anlık (gerçek zamanlı) bildirim iletimi soyutlaması. Application katmanı SignalR gibi somut bir
/// iletim teknolojisine bağımlı olmaz (Dependency Inversion); implementasyon API katmanındadır
/// (composition root — bkz. Hubs/NotificationsHub, Services/SignalRRealtimeNotifier).
/// bkz. Proje Raporu bölüm 3.13, 8.
/// </summary>
public interface IRealtimeNotifier
{
    /// <summary>Belirli bir kullanıcıya (her zaman bir ADMIN) anlık bildirim gönderir. Alıcı bağlı değilse sessizce yok sayılır.</summary>
    Task NotifyUserAsync(Guid userId, NotificationPushDto notification, CancellationToken ct = default);
}

/// <summary>SignalR üzerinden istemciye gönderilen anlık bildirim payload'u.</summary>
public record NotificationPushDto(Guid Id, string Type, string Message, DateTime CreatedAtUtc);
