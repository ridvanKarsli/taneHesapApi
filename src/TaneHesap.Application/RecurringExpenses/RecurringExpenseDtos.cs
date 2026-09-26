using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.RecurringExpenses;

public record RecurringExpenseDto(
    Guid Id,
    string Name,
    decimal Amount,
    RecurringPeriod Period,
    int IntervalCount,
    DateOnly StartDate,
    bool IsActive,
    DateOnly CurrentPeriodStartDate,
    DateOnly CurrentPeriodEndDate,
    bool IsCurrentPeriodPaid);

/// <param name="IntervalCount">Kaç periyotta bir (örn. Monthly + 3 = 3 ayda bir). Verilmezse 1.</param>
public record CreateRecurringExpenseRequest(string Name, decimal Amount, RecurringPeriod Period, DateOnly StartDate, int IntervalCount = 1);

public record UpdateRecurringExpenseRequest(string Name, decimal Amount, RecurringPeriod Period, bool IsActive, int IntervalCount = 1);

/// <summary>
/// "Ödenecekler" listesinin bir satırı: ödenmemiş bir dönem (içinde bulunulan dönem ya da gecikmiş önceki dönem).
/// Ödenince listeden çıkar.
/// </summary>
public record RecurringPayableDto(
    Guid RecurringExpenseId,
    string Name,
    decimal Amount,
    RecurringPeriod Period,
    int IntervalCount,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    bool IsOverdue);

/// <summary>
/// Bir dönemi (ör. bu ayı) ödendi olarak işaretler. Ödeme, ödeme şekline göre kasadan/karttan düşen otomatik
/// bir gider olarak da kaydedilir (raporlar ve tabak başı maliyet bunu görür) — bkz. bölüm 3.8, 3.15.
/// </summary>
public record MarkPeriodPaidRequest(DateOnly PeriodStartDate, DateOnly PeriodEndDate, decimal PaidAmount, DateOnly PaidDate, PaymentMethod PaymentMethod, Guid? PaymentCardId);
