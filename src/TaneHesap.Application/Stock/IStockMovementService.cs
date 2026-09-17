namespace TaneHesap.Application.Stock;

/// <summary>
/// Malzeme stok hareketleri — her hareket Ingredient.CurrentStockQuantity alanını günceller.
/// bkz. Proje Raporu bölüm 3.9.
/// </summary>
public interface IStockMovementService
{
    Task<List<StockMovementDto>> GetAllAsync(Guid businessId, Guid? ingredientId, CancellationToken ct = default);

    /// <summary>
    /// Yeni bir stok hareketi kaydeder ve ilgili Ingredient.CurrentStockQuantity alanını
    /// (QuantityChange kadar) günceller. Pozitif değer stok artışı, negatif değer stok azalışıdır.
    /// </summary>
    Task<StockMovementDto> CreateAsync(Guid businessId, CreateStockMovementRequest request, Guid createdByUserId, CancellationToken ct = default);
}
