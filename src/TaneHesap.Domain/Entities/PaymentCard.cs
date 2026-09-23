using TaneHesap.Domain.Common;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// İşletmenin gider ödemelerinde kullandığı kredi kartı. Limit ADMIN tarafından elle belirlenir/güncellenir;
/// kullanılabilir limit = Limit + Σ(bu karta ait TreasuryTransaction tutarları: giderler negatif, kart
/// ödemeleri pozitif). bkz. Proje Raporu bölüm 3.15.
/// </summary>
public class PaymentCard : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Toplam kart limiti (elle değiştirilebilir).</summary>
    public decimal Limit { get; set; }

    public bool IsActive { get; set; } = true;
}
