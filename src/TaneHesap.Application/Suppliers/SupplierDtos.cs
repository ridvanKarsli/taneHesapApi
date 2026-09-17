namespace TaneHesap.Application.Suppliers;

public record SupplierDto(Guid Id, string Name, string? ContactInfo, bool IsActive, decimal TotalOutstandingDebt);

public record CreateSupplierRequest(string Name, string? ContactInfo);

public record UpdateSupplierRequest(string Name, string? ContactInfo, bool IsActive);

public record SupplierPaymentDto(Guid Id, decimal Amount, DateOnly PaymentDate);

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
public record CreateSupplierPurchaseRequest(Guid IngredientId, decimal Quantity, decimal UnitPrice, DateOnly PurchaseDate);

/// <summary>Bir alışa karşılık (kısmi olabilen) ödeme kaydı. Toplam ödeme TotalAmount'a ulaşınca IsFullyPaid true olur.</summary>
public record CreateSupplierPaymentRequest(decimal Amount, DateOnly PaymentDate);
