using Microsoft.AspNetCore.SignalR;
using TaneHesap.API.Hubs;
using TaneHesap.Application.Common.Interfaces;

namespace TaneHesap.API.Services;

/// <summary>
/// IRealtimeNotifier'ın SignalR üzerinden implementasyonu. Bilinçli olarak API katmanında tutulur
/// (composition root) — Application katmanı SignalR'ı bilmez (Dependency Inversion), Infrastructure
/// katmanı da bu HTTP/host bağımlı detaydan habersiz kalır. bkz. Proje Raporu bölüm 3.13, 8.
/// </summary>
public class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationsHub> _hubContext;

    public SignalRRealtimeNotifier(IHubContext<NotificationsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyUserAsync(Guid userId, NotificationPushDto notification, CancellationToken ct = default)
        => _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", notification, ct);
}
