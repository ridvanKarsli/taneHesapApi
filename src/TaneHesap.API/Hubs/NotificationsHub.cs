using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TaneHesap.API.Hubs;

/// <summary>
/// ADMIN'lere anlık (in-app) bildirim itmek için kullanılan SignalR hub'ı. Sunucudan istemciye tek
/// yönlü "ReceiveNotification" mesajı gönderilir (bkz. Services/SignalRRealtimeNotifier); istemcinin
/// çağırdığı bir hub metodu yoktur. Bağlantı, JWT'deki NameIdentifier claim'i üzerinden otomatik
/// olarak kullanıcıya eşlenir (ASP.NET Core SignalR varsayılan davranışı). bkz. Proje Raporu bölüm 3.13.
/// </summary>
[Authorize(Roles = "Admin")]
public class NotificationsHub : Hub
{
}
