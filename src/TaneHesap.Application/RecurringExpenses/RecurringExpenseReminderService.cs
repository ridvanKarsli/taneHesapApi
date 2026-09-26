using System.Globalization;
using TaneHesap.Application.Common;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.Notifications;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.RecurringExpenses;

/// <summary>
/// Dönemi bitmiş ama "ödendi" işaretlenmemiş düzenli giderler için ADMIN'lere in-app hatırlatma
/// üretir (bkz. Proje Raporu bölüm 3.8, 3.13). Periyodik olarak bir arka plan görevinden çağrılır;
/// aynı dönem için aynı hatırlatmayı tekrar göndermez (idempotent).
/// </summary>
public interface IRecurringExpenseReminderService
{
    /// <returns>Gönderilen yeni hatırlatma sayısı.</returns>
    Task<int> SendDueRemindersAsync(CancellationToken ct = default);
}

public class RecurringExpenseReminderService : IRecurringExpenseReminderService
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private readonly IUnitOfWork _unitOfWork;
    private readonly IRecurringExpenseService _recurringExpenseService;
    private readonly INotificationService _notificationService;

    public RecurringExpenseReminderService(
        IUnitOfWork unitOfWork, IRecurringExpenseService recurringExpenseService, INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _recurringExpenseService = recurringExpenseService;
        _notificationService = notificationService;
    }

    public async Task<int> SendDueRemindersAsync(CancellationToken ct = default)
    {
        var businesses = await _unitOfWork.Repository<Business>().ListAsync(b => b.IsActive, ct);
        var sent = 0;

        foreach (var business in businesses)
        {
            var dueItems = await _recurringExpenseService.GetDueForReminderAsync(business.Id, ct);
            if (dueItems.Count == 0)
            {
                continue;
            }

            var alreadySent = (await _unitOfWork.Repository<Notification>()
                    .ListAsync(n => n.BusinessId == business.Id && n.Type == NotificationType.RecurringExpenseReminder, ct))
                .Select(n => n.Message)
                .ToHashSet();

            foreach (var item in dueItems)
            {
                var message = BuildMessage(item);
                if (alreadySent.Contains(message))
                {
                    continue;
                }

                await _notificationService.NotifyAdminsAsync(business.Id, NotificationType.RecurringExpenseReminder, message, ct);
                sent++;
            }
        }

        return sent;
    }

    /// <summary>Mesaj dönem tarihlerini içerdiği için aynı dönemin tekrar bildirilmesini önleyen anahtar olarak da kullanılır.</summary>
    private static string BuildMessage(RecurringExpenseDto item)
    {
        var overdue = item.CurrentPeriodEndDate < BusinessClock.Today;
        return $"'{item.Name}' düzenli gideri {(overdue ? "gecikti, hâlâ ödenmedi" : "ödenmedi")}: " +
               $"{item.CurrentPeriodStartDate.ToString("dd.MM.yyyy", Tr)} – {item.CurrentPeriodEndDate.ToString("dd.MM.yyyy", Tr)} dönemi, " +
               $"{item.Amount.ToString("N2", Tr)} ₺.";
    }
}
