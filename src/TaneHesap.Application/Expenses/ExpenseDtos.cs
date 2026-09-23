using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Expenses;

public record ExpenseDto(
    Guid Id,
    Guid ExpenseTypeId,
    string ExpenseTypeName,
    ExpenseCategory ExpenseCategory,
    decimal Amount,
    decimal? Quantity,
    DateOnly ExpenseDate,
    PaymentMethod? PaymentMethod,
    Guid? PaymentCardId,
    string? PaymentCardName,
    Guid? EmployeeUserId,
    string? EmployeeName,
    string? Description,
    /// <summary>Doluysa gider sistem tarafından üretildi (platform/kart komisyonu, düzenli gider, tedarikçi ödemesi) ve kaynağından yönetilir.</summary>
    string? SourceReferenceType,
    Guid? CreatedByUserId,
    DateTime CreatedAtUtc);

/// <summary>
/// Ödeme şekli Card ise <paramref name="PaymentCardId"/> zorunludur (limitten düşer); gider türü Personnel
/// kategorisindeyse <paramref name="EmployeeUserId"/> verilir (çalışanın cüzdanından düşer). bkz. bölüm 3.15, 3.7.
/// </summary>
public record CreateExpenseRequest(
    Guid ExpenseTypeId,
    decimal Amount,
    decimal? Quantity,
    DateOnly ExpenseDate,
    PaymentMethod? PaymentMethod,
    Guid? PaymentCardId,
    Guid? EmployeeUserId,
    string? Description);

public record UpdateExpenseRequest(
    Guid ExpenseTypeId,
    decimal Amount,
    decimal? Quantity,
    DateOnly ExpenseDate,
    PaymentMethod? PaymentMethod,
    Guid? PaymentCardId,
    Guid? EmployeeUserId,
    string? Description);

/// <summary>
/// Gider listesi için opsiyonel filtreler. <paramref name="CreatedByUserId"/> doluysa sadece o
/// kullanıcının girdiği giderler döner — EMPLOYEE yalnızca kendi kayıtlarını görür (bkz. Proje Raporu bölüm 2).
/// </summary>
public record ExpenseListFilter(DateOnly? FromDate, DateOnly? ToDate, Guid? ExpenseTypeId, Guid? CreatedByUserId = null, Guid? EmployeeUserId = null);
