using TaneHesap.Domain.Common;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// Reçetelerde kullanılan malzeme (örn. pilav, et, yağ). Stok ve maliyet hesaplamalarının temelidir.
/// bkz. Proje Raporu bölüm 3.9, 3.3.
/// </summary>
public class Ingredient : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Ölçü birimi (gram, ml, adet vb.).</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>En güncel alış birim fiyatı — tabak maliyeti hesabında kullanılır.</summary>
    public decimal CurrentUnitPrice { get; set; }

    /// <summary>Altına düşülünce ADMIN'e in-app bildirim gönderilecek eşik.</summary>
    public decimal MinimumStockThreshold { get; set; }

    /// <summary>Güncel stok miktarı (StockMovement kayıtlarından türetilen denormalize alan).</summary>
    public decimal CurrentStockQuantity { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<DishRecipeItem> RecipeItems { get; set; } = new List<DishRecipeItem>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
