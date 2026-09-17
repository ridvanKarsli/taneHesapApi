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

/// <summary>Bir dönemi (ör. bu ayı) ödendi olarak işaretlemek için kullanılır.</summary>
public record MarkPeriodPaidRequest(DateOnly PeriodStartDate, DateOnly PeriodEndDate, decimal PaidAmount, DateOnly PaidDate);
