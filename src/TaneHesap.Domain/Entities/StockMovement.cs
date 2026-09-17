using TaneHesap.Domain.Common;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// Bir malzemenin stok hareketi (alış, satış tüketimi, manuel düzeltme, fire).
/// bkz. Proje Raporu bölüm 3.9.
/// </summary>
public class StockMovement : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public Guid IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    /// <summary>Pozitif = stok artışı (alış), negatif = stok azalışı (tüketim/fire).</summary>
    public decimal QuantityChange { get; set; }

    public StockMovementType MovementType { get; set; }

    public DateTime MovementDateUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Hareketin kaynağı (örn. "SupplierPurchase", "DailyActualEntry") — izlenebilirlik için.</summary>
    public string? SourceReferenceType { get; set; }
    public Guid? SourceReferenceId { get; set; }

    public string? Note { get; set; }
}
