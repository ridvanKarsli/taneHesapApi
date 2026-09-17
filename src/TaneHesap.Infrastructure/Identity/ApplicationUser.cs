using Microsoft.AspNetCore.Identity;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity kullanıcı sınıfı. taneHesap'a özgü alanlar (rol, işletme, TOTP secret)
/// burada tutulur. Domain katmanı bu sınıfa bağımlı değildir (bkz. Proje Raporu bölüm 2, 8).
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    /// <summary>SUPER_ADMIN için null.</summary>
    public Guid? BusinessId { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Yalnızca SUPER_ADMIN/ADMIN için dolu olur — authenticator (TOTP) secret'ı.</summary>
    public string? TotpSecret { get; set; }

    public bool TotpEnabled { get; set; }
}
