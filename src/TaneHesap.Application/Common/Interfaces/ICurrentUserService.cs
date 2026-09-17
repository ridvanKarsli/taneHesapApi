using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Common.Interfaces;

/// <summary>
/// İstek bağlamındaki (HTTP request) kimliği doğrulanmış kullanıcı bilgisine erişim sağlar.
/// Infrastructure katmanında HttpContext üzerinden implemente edilir.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }

    /// <summary>SUPER_ADMIN için null olur (tüm işletmelere erişimi vardır).</summary>
    Guid? BusinessId { get; }

    UserRole? Role { get; }

    bool IsAuthenticated { get; }
}
