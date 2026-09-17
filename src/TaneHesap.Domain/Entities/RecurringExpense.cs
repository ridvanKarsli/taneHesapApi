using TaneHesap.Domain.Common;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// Kira, elektrik gibi periyodik/düzenli giderler. Periyot sonunda ödendi işaretlenmezse
/// ADMIN'e in-app hatırlatma gönderilir. bkz. Proje Raporu bölüm 3.8.
/// </summary>
public class RecurringExpense : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public RecurringPeriod Period { get; set; }

    public DateOnly StartDate { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<RecurringExpensePayment> Payments { get; set; } = new List<RecurringExpensePayment>();
}

/// <summary>Bir düzenli giderin belirli bir dönem için ödeme durumu.</summary>
public class RecurringExpensePayment : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public Guid RecurringExpenseId { get; set; }
    public RecurringExpense? RecurringExpense { get; set; }

    public DateOnly PeriodStartDate { get; set; }
    public DateOnly PeriodEndDate { get; set; }

    public bool IsPaid { get; set; }
    public DateOnly? PaidDate { get; set; }
    public decimal? PaidAmount { get; set; }
}
