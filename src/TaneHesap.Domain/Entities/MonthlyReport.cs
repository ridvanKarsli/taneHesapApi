using TaneHesap.Domain.Common;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// Ay sonunda otomatik üretilen aylık özet (bkz. Proje Raporu bölüm 3.16). Detay (malzeme verimliliği)
/// her istekte güncel veriden hesaplanır; burada yalnızca ay kapanış anındaki özet ve "bu ay için rapor
/// üretildi/bildirildi" bilgisi (idempotency) tutulur.
/// </summary>
public class MonthlyReport : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }

    public decimal TotalRevenue { get; set; }
    public decimal TotalExpense { get; set; }
    public int PlatesSold { get; set; }

    /// <summary>Toplam gider ÷ satılan tabak sayısı.</summary>
    public decimal CostPerPlate { get; set; }

    /// <summary>Önceki aya göre verimliliği düşen malzeme sayısı.</summary>
    public int WarningCount { get; set; }

    public DateTime GeneratedAtUtc { get; set; }
}
