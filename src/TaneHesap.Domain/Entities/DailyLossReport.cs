using TaneHesap.Domain.Common;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// Bir gün için üretilen fire/kayıp raporu: sistemin (reçete + Excel siparişlerinden) hesapladığı beklenen
/// değerler ile ADMIN'in manuel girdiği gerçek değerler (DailyActualEntry) arasındaki farkı özetler.
/// bkz. Proje Raporu bölüm 3.10.
/// </summary>
public class DailyLossReport : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public DateOnly ReportDate { get; set; }

    public decimal ExpectedRevenue { get; set; }
    public decimal ActualRevenue { get; set; }

    /// <summary>ActualRevenue - ExpectedRevenue (negatifse gelir açığı var demektir).</summary>
    public decimal RevenueVarianceAmount { get; set; }

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<DailyLossReportItem> Items { get; set; } = new List<DailyLossReportItem>();
}

/// <summary>Bir DailyLossReport içindeki, malzeme bazında beklenen/gerçek fark satırı.</summary>
public class DailyLossReportItem : BaseEntity
{
    public Guid DailyLossReportId { get; set; }
    public DailyLossReport? DailyLossReport { get; set; }

    public Guid IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    /// <summary>Reçeteye göre satışlardan beklenen tüketim miktarı.</summary>
    public decimal ExpectedQuantity { get; set; }

    /// <summary>ADMIN'in manuel girdiği gerçek tüketim miktarı.</summary>
    public decimal ActualQuantity { get; set; }

    /// <summary>ActualQuantity - ExpectedQuantity (pozitifse fazla/fire var demektir).</summary>
    public decimal VarianceQuantity { get; set; }

    /// <summary>VarianceQuantity × Ingredient.CurrentUnitPrice (tutar bazında etki).</summary>
    public decimal VarianceCost { get; set; }
}
