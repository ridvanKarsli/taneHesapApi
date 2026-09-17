using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Notifications;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdentityService _identityService;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public NotificationService(IUnitOfWork unitOfWork, IIdentityService identityService, IRealtimeNotifier realtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _identityService = identityService;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<List<NotificationDto>> GetForUserAsync(Guid businessId, Guid userId, bool unreadOnly, CancellationToken ct = default)
    {
        var notifications = await _unitOfWork.Repository<Notification>()
            .ListAsync(n => n.BusinessId == businessId && n.RecipientUserId == userId && (!unreadOnly || !n.IsRead), ct);

        return notifications
            .OrderByDescending(n => n.CreatedAtUtc)
            .Select(ToDto)
            .ToList();
    }

    public async Task<NotificationDto> MarkAsReadAsync(Guid businessId, Guid userId, Guid notificationId, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<Notification>();
        var notification = await repo.GetByIdAsync(notificationId, ct);
        if (notification is null || notification.BusinessId != businessId || notification.RecipientUserId != userId)
        {
            throw new NotFoundException(nameof(Notification), notificationId);
        }

        notification.IsRead = true;
        notification.UpdatedByUserId = userId;
        notification.UpdatedAtUtc = DateTime.UtcNow;
        repo.Update(notification);
        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(notification);
    }

    public async Task NotifyAdminsAsync(Guid businessId, NotificationType type, string message, CancellationToken ct = default)
    {
        var admins = await _identityService.GetAdminsByBusinessAsync(businessId);
        if (admins.Count == 0)
        {
            return;
        }

        var repo = _unitOfWork.Repository<Notification>();
        var created = new List<Notification>();

        foreach (var admin in admins)
        {
            var notification = new Notification
            {
                BusinessId = businessId,
                RecipientUserId = admin.UserId,
                Type = type,
                Message = message,
                IsRead = false
            };

            await repo.AddAsync(notification, ct);
            created.Add(notification);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // Kalıcı kayıt tamamlandıktan sonra bağlı istemcilere anlık iletim denenir; bir alıcının
        // bağlı olmaması iş akışını (bildirim kaydını) etkilemez, sadece anlık teslimat atlanır.
        foreach (var notification in created)
        {
            await _realtimeNotifier.NotifyUserAsync(
                notification.RecipientUserId,
                new NotificationPushDto(notification.Id, notification.Type.ToString(), notification.Message, notification.CreatedAtUtc),
                ct);
        }
    }

    public Task NotifyLowStockAsync(Guid businessId, string ingredientName, decimal currentQuantity, decimal threshold, CancellationToken ct = default)
        => NotifyAdminsAsync(
            businessId,
            NotificationType.LowStock,
            $"'{ingredientName}' malzemesi düşük stokta: mevcut {currentQuantity}, eşik {threshold}.",
            ct);

    private static NotificationDto ToDto(Notification n) => new(n.Id, n.Type, n.Message, n.IsRead, n.CreatedAtUtc);
}
