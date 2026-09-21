using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Expenses;

public record ExpenseDto(
    Guid Id,
    Guid ExpenseTypeId,
    string ExpenseTypeName,
    decimal Amount,
    decimal? Quantity,
    DateOnly ExpenseDate,
    PaymentMethod? PaymentMethod,
    string? Description,
    Guid? CreatedByUserId,
    DateTime CreatedAtUtc);

public record CreateExpenseRequest(
    Guid ExpenseTypeId,
    decimal Amount,
    decimal? Quantity,
    DateOnly ExpenseDate,
    PaymentMethod? PaymentMethod,
    string? Description);

public record UpdateExpenseRequest(
    Guid ExpenseTypeId,
    decimal Amount,
    decimal? Quantity,
    DateOnly ExpenseDate,
    PaymentMethod? PaymentMethod,
    string? Description);

/// <summary>
/// Gider listesi için opsiyonel filtreler. <paramref name="CreatedByUserId"/> doluysa sadece o
/// kullanıcının girdiği giderler döner — EMPLOYEE yalnızca kendi kayıtlarını görür (bkz. Proje Raporu bölüm 2).
/// </summary>
public record ExpenseListFilter(DateOnly? FromDate, DateOnly? ToDate, Guid? ExpenseTypeId, Guid? CreatedByUserId = null);
