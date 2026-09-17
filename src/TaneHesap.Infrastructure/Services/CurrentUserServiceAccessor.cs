using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Infrastructure.Services;

/// <summary>
/// ApplicationDbContext gibi Infrastructure bileşenlerinin DI'da her zaman bir ICurrentUserService
/// bulabilmesi için tasarım-zamanı / arka plan görevleri (migration, seed, scheduled job) senaryosunda
/// kullanılan varsayılan implementasyon. HTTP istekleri sırasında API katmanındaki gerçek
/// ICurrentUserService implementasyonu (HttpContext tabanlı) bunun yerine kaydedilir.
/// </summary>
public class NullCurrentUserService : ICurrentUserService
{
    public Guid? UserId => null;
    public Guid? BusinessId => null;
    public UserRole? Role => null;
    public bool IsAuthenticated => false;
}
