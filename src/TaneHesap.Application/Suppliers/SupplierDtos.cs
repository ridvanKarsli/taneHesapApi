using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Suppliers;

public record SupplierDto(Guid Id, string Name, string? ContactInfo, bool IsActive, decimal TotalOutstandingDebt);

public record CreateSupplierRequest(string Name, string? ContactInfo);

public record UpdateSupplierRequest(string Name, string? ContactInfo, bool IsActive);

public record SupplierPaymentDto(Guid Id, decimal Amount, DateOnly PaymentDate, PaymentMethod? PaymentMethod);

public record SupplierPurchaseDto(
    Guid Id,
    Guid SupplierId,
    Guid IngredientId,
    string IngredientName,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalAmount,
    DateOnly PurchaseDate,
    bool IsFullyPaid,
    decimal PaidAmount,
    decimal RemainingAmount,
    List<SupplierPaymentDto> Payments);

/// <summary>
/// Yeni tedarikçi alışı. Kaydedilince: (1) ilgili malzemenin stoğuna Quantity kadar Purchase
/// hareketi eklenir, (2) malzemenin CurrentUnitPrice değeri bu alışın UnitPrice'ı ile güncellenir.
/// </summary>
public record CreateSupplierPurchaseRequest(
    Guid IngredientId,
    decimal Quantity,
    decimal UnitPrice,
    DateOnly PurchaseDate,
    /// <summary>Alış anında ödenen tutar (0 veya null → borç olarak kalır, sonra "Ödeme yap" ile kapatılır).</summary>
    decimal? PaidAmount = null,
    PaymentMethod? PaymentMethod = null,
    Guid? PaymentCardId = null);

/// <summary>
/// Bir alışa karşılık (kısmi olabilen) ödeme kaydı. Toplam ödeme TotalAmount'a ulaşınca IsFullyPaid true olur.
/// Ödeme, ödeme şekline göre kasadan/karttan düşen otomatik bir Malzeme gideri olarak da kaydedilir (bkz. 3.12, 3.15).
/// </summary>
public record CreateSupplierPaymentRequest(decimal Amount, DateOnly PaymentDate, PaymentMethod PaymentMethod, Guid? PaymentCardId);
