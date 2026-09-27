using TaneHesap.Domain.Entities;

namespace TaneHesap.Application.Treasury;

/// <summary>
/// Para çıkışı kuralı: banka hesabı eksiye düşemez, kredi kartı limitini aşamaz. Nakit kasası kontrol edilmez
/// (fiziksel kasa sayımı "Bakiyeyi ayarla" ile düzeltilir). Kayıt edilmeden ÖNCE çağrılır: <paramref name="removed"/>
/// bu işlemle silinecek (henüz veritabanında duran) hareketler, <paramref name="added"/> eklenecekler.
/// Yalnızca bir hesaptan net çıkış varsa kontrol edilir — hesabı artıran işlemler (satış, iade, gider silme) hiç engellenmez.
/// </summary>
public interface ITreasuryFundsGuard
{
    Task EnsureAvailableAsync(Guid businessId, IReadOnlyCollection<TreasuryTransaction> removed,
        IReadOnlyCollection<TreasuryTransaction> added, CancellationToken ct = default);
}
