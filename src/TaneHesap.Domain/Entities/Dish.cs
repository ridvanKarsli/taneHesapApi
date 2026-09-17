using TaneHesap.Domain.Common;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// Ürün (örn. kuru pilav, kavurmalı). Bir ürünün birden fazla tabak boyu (DishSize) olabilir.
/// bkz. Proje Raporu bölüm 3.11.
/// </summary>
public class Dish : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<DishSize> Sizes { get; set; } = new List<DishSize>();
}

/// <summary>
/// Bir ürünün tabak boyu/türü (küçük/orta/büyük). Her boyun kendi satış fiyatı ve reçetesi olur.
/// bkz. Proje Raporu bölüm 3.3.
/// </summary>
public class DishSize : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public Guid DishId { get; set; }
    public Dish? Dish { get; set; }

    /// <summary>Boy adı (Küçük / Orta / Büyük vb.).</summary>
    public string Name { get; set; } = string.Empty;

    public decimal SalePrice { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<DishRecipeItem> RecipeItems { get; set; } = new List<DishRecipeItem>();
}

/// <summary>
/// Bir tabak boyunun reçetesindeki tek bir malzeme satırı (örn. pilav 150 g).
/// Tabak maliyeti = Σ (Quantity × Ingredient.CurrentUnitPrice).
/// </summary>
public class DishRecipeItem : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public Guid DishSizeId { get; set; }
    public DishSize? DishSize { get; set; }

    public Guid IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    /// <summary>Bu tabak boyunda kullanılan malzeme miktarı (gram/ml, Ingredient.Unit ile aynı birimde).</summary>
    public decimal Quantity { get; set; }
}
