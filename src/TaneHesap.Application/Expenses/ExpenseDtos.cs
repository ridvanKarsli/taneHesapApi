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

/// <summary>Gider listesi için opsiyonel filtreler.</summary>
public record ExpenseListFilter(DateOnly? FromDate, DateOnly? ToDate, Guid? ExpenseTypeId);
