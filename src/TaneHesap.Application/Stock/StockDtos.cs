using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Stock;

public record StockMovementDto(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    decimal QuantityChange,
    StockMovementType MovementType,
    DateTime MovementDateUtc,
    string? SourceReferenceType,
    Guid? SourceReferenceId,
    string? Note,
    decimal ResultingStockQuantity);

/// <summary>
/// Manuel stok hareketi girişi (alış/tüketim dışı — örn. sayım düzeltmesi, fire).
/// Alış hareketleri Suppliers modülü üzerinden, satış tüketimi ise gün sonu Excel içe aktarımıyla
/// (reçeteye göre) otomatik oluşturulur.
/// </summary>
public record CreateStockMovementRequest(
    Guid IngredientId,
    decimal QuantityChange,
    StockMovementType MovementType,
    string? Note);
