using TaneHesap.Domain.Common;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// ADMIN'in gün sonu Excel yüklemesinden SONRA manuel olarak girdiği gerçekleşen gelir ve gerçek malzeme
/// tüketim miktarları. Sistemin (Excel'den) hesapladığı beklenen değerlerle karşılaştırılarak
/// DailyLossReport üretilir. bkz. Proje Raporu bölüm 3.10.
/// </summary>
public class DailyActualEntry : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public DateOnly EntryDate { get; set; }

    /// <summary>ADMIN'in kasadan/sayımdan girdiği gerçek toplam gelir.</summary>
    public decimal ActualRevenue { get; set; }

    public Guid EnteredByUserId { get; set; }

    public string? Note { get; set; }

    public ICollection<DailyActualConsumptionItem> ConsumptionItems { get; set; } = new List<DailyActualConsumptionItem>();
}

/// <summary>Bir DailyActualEntry içindeki, malzeme bazında gerçek tüketim satırı.</summary>
public class DailyActualConsumptionItem : BaseEntity
{
    public Guid DailyActualEntryId { get; set; }
    public DailyActualEntry? DailyActualEntry { get; set; }

    public Guid IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    /// <summary>ADMIN'in girdiği gerçek kullanılan miktar (Ingredient.Unit biriminde).</summary>
    public decimal ActualQuantityUsed { get; set; }
}
