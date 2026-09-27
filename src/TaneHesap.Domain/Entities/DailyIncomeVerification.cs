using TaneHesap.Domain.Common;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// Bir günün dükkân içi gelirinin doğrulaması: kasadan sayılan gerçek nakit ve POS'tan (banka) okunan gerçek
/// kart toplamı. Beklenen tutarlar kayıtlı değildir — o günün Kasa Excel satışlarından her okumada hesaplanır.
/// Doğrulanan günde nakit kasası ve banka hesabına beklenen yerine bu tutarlar yazılır, raporlarda gelir de
/// bu tutarlardır. Paket servis (platform) satışları kapsam dışıdır. bkz. Proje Raporu bölüm 3.10.
/// </summary>
public class DailyIncomeVerification : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public DateOnly VerificationDate { get; set; }

    /// <summary>Kasadan sayılan gerçek nakit gelir.</summary>
    public decimal ActualCash { get; set; }

    /// <summary>POS / banka dökümündeki gerçek kart geliri (brüt; POS komisyonu ayrıca gider olur).</summary>
    public decimal ActualCard { get; set; }

    public string? Note { get; set; }
}
