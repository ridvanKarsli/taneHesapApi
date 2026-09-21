using System.Security.Claims;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Enums;

namespace TaneHesap.API.Services;

/// <summary>
/// ICurrentUserService'in HTTP isteği bağlamındaki (JWT claim'leri üzerinden) implementasyonu.
/// Bilinçli olarak API katmanında tutulur; Infrastructure'ın HttpContext'e bağımlı olmaması için.
/// bkz. Proje Raporu bölüm 8 (multi-tenant izolasyon).
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SystemExecutionScope _systemScope;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, SystemExecutionScope systemScope)
    {
        _httpContextAccessor = httpContextAccessor;
        _systemScope = systemScope;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var value = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public Guid? BusinessId
    {
        get
        {
            var value = User?.FindFirstValue("business_id");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    /// <summary>Arka plan görevlerinde (bkz. <see cref="SystemExecutionScope"/>) sistem, SUPER_ADMIN gibi tüm işletmeleri görür.</summary>
    public UserRole? Role
    {
        get
        {
            if (_systemScope.IsSystem)
            {
                return UserRole.SuperAdmin;
            }

            var value = User?.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<UserRole>(value, out var role) ? role : null;
        }
    }
}
