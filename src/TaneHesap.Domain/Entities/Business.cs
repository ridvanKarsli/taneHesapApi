using TaneHesap.Domain.Common;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// Bir işletmeyi (örn. Meydan Pilavcısı) temsil eder. SUPER_ADMIN tarafından oluşturulur.
/// Sistemdeki tüm tenant-scoped veriler bir Business'a bağlıdır.
/// </summary>
public class Business : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Kart (POS) satışlarında bankanın kestiği komisyon yüzdesi — kart kasasına net tutar yazılır. Varsayılan %3.</summary>
    public decimal CardFeePercentage { get; set; } = 3m;
}
