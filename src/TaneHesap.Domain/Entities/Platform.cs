using TaneHesap.Domain.Common;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// Paket servis platformu (Yemeksepeti, Getir, Trendyol Yemek vb.) ve komisyon yüzdesi.
/// bkz. Proje Raporu bölüm 3.4.
/// </summary>
public class Platform : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>0-100 arası komisyon yüzdesi.</summary>
    public decimal CommissionPercentage { get; set; }

    public bool IsActive { get; set; } = true;
}
