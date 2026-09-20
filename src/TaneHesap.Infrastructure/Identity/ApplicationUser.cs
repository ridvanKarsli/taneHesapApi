using Microsoft.AspNetCore.Identity;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity kullanıcı sınıfı. taneHesap'a özgü alanlar (rol, işletme) burada
/// tutulur. Domain katmanı bu sınıfa bağımlı değildir (bkz. Proje Raporu bölüm 2, 8).
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    /// <summary>SUPER_ADMIN için null.</summary>
    public Guid? BusinessId { get; set; }

    public bool IsActive { get; set; } = true;
}
