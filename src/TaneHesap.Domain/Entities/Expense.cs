using TaneHesap.Domain.Common;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// Bir gider kaydı. ADMIN veya EMPLOYEE tarafından girilir; kim/ne zaman girdi BaseEntity üzerinden loglanır.
/// bkz. Proje Raporu bölüm 3.1.
/// </summary>
public class Expense : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public Guid ExpenseTypeId { get; set; }
    public ExpenseType? ExpenseType { get; set; }

    public decimal Amount { get; set; }

    public decimal? Quantity { get; set; }

    public DateOnly ExpenseDate { get; set; }

    public PaymentMethod? PaymentMethod { get; set; }

    /// <summary>PaymentMethod = Card ise ödemenin yapıldığı kredi kartı (limitten düşer).</summary>
    public Guid? PaymentCardId { get; set; }
    public PaymentCard? PaymentCard { get; set; }

    /// <summary>Gider türü Personnel kategorisindeyse ödemenin yapıldığı çalışan (cüzdanından düşer).</summary>
    public Guid? EmployeeUserId { get; set; }

    public string? Description { get; set; }
}
