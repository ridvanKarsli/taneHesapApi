using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Notifications;

public record NotificationDto(Guid Id, NotificationType Type, string Message, bool IsRead, DateTime CreatedAtUtc);
