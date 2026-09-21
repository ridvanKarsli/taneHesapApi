namespace TaneHesap.API.Services;

/// <summary>
/// HTTP isteği dışındaki (arka plan görevi) işlerin "sistem" kimliğiyle çalıştığını işaretler.
/// Scoped'dur: bir arka plan görevi kendi DI scope'unu açar, <see cref="EnterSystemMode"/> çağırır ve
/// o scope'taki <see cref="CurrentUserService"/> tüm işletmelere erişen sistem kullanıcısı gibi davranır
/// (multi-tenant sorgu filtresi devre dışı). HTTP isteklerinde hiçbir zaman açılmaz.
/// </summary>
public class SystemExecutionScope
{
    public bool IsSystem { get; private set; }

    public void EnterSystemMode() => IsSystem = true;
}
