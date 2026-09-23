using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Employees;

public record EmployeeWorkLogDto(Guid Id, DateOnly WorkDate, decimal Hours, decimal HourlyWage, decimal Amount, string? Note);

public record CreateWorkLogRequest(DateOnly WorkDate, decimal Hours, string? Note);

/// <summary>Çalışana yapılan ödeme — aslında Personnel kategorisinde bir Expense kaydıdır (Giderler'den de görünür/silinir).</summary>
public record EmployeePaymentDto(Guid ExpenseId, DateOnly Date, decimal Amount, PaymentMethod? PaymentMethod, string? PaymentCardName, string? Description);

public record CreateEmployeePaymentRequest(decimal Amount, DateOnly Date, PaymentMethod PaymentMethod, Guid? PaymentCardId, string? Note);

/// <summary>
/// Çalışanın cüzdanı: Σ hak ediş (çalışma saati × saatlik ücret) − Σ ödeme = bakiye (işletmenin çalışana borcu).
/// bkz. Proje Raporu bölüm 3.7.
/// </summary>
public record EmployeeWalletDto(
    Guid UserId,
    string FullName,
    decimal HourlyWage,
    decimal TotalHours,
    decimal TotalEarned,
    decimal TotalPaid,
    decimal Balance,
    List<EmployeeWorkLogDto> WorkLogs,
    List<EmployeePaymentDto> Payments);
