using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.RecurringExpenses;

public record RecurringExpenseDto(
    Guid Id,
    string Name,
    decimal Amount,
    RecurringPeriod Period,
    DateOnly StartDate,
    bool IsActive,
    DateOnly CurrentPeriodStartDate,
    DateOnly CurrentPeriodEndDate,
    bool IsCurrentPeriodPaid);

public record CreateRecurringExpenseRequest(string Name, decimal Amount, RecurringPeriod Period, DateOnly StartDate);

public record UpdateRecurringExpenseRequest(string Name, decimal Amount, RecurringPeriod Period, bool IsActive);

/// <summary>
/// Bir dönemi (ör. bu ayı) ödendi olarak işaretler. Ödeme, ödeme şekline göre kasadan/karttan düşen otomatik
/// bir gider olarak da kaydedilir (raporlar ve tabak başı maliyet bunu görür) — bkz. bölüm 3.8, 3.15.
/// </summary>
public record MarkPeriodPaidRequest(DateOnly PeriodStartDate, DateOnly PeriodEndDate, decimal PaidAmount, DateOnly PaidDate, PaymentMethod PaymentMethod, Guid? PaymentCardId);
