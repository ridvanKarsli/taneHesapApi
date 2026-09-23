using TaneHesap.Domain.Entities;

namespace TaneHesap.Application.Treasury;

/// <summary>
/// Bir gider kaydının kasa hareketini ödeme şekline göre yazar: Cash → nakit kasası, Bank → kart kasası,
/// Card → ilgili kredi kartının limiti; ödeme şekli boşsa kasaya dokunmaz. Gider oluşturma/güncelleme/silme
/// yapan her servis (ExpenseService, PlatformCommissionExpensePoster, çalışan ödemesi) bunu çağırır — kasa
/// kuralı tek yerde (Single Responsibility). SaveChanges çağırmaz; çağıran servis kaydeder.
/// </summary>
public interface IExpenseTreasuryPoster
{
    Task SyncAsync(Expense expense, CancellationToken ct = default);

    Task RemoveAsync(Guid expenseId, CancellationToken ct = default);
}
