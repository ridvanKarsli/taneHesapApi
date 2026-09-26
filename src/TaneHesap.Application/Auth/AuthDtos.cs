using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Auth;

/// <summary>
/// Giriş isteği. Tüm roller (SUPER_ADMIN/ADMIN/EMPLOYEE) sadece kullanıcı adı/şifre ile giriş
/// yapar — authenticator (2FA) zorunluluğu kaldırıldı (bkz. Proje Raporu bölüm 2, 7).
/// </summary>
public record LoginRequest(string Username, string Password);

/// <param name="Role">Oturumun etkin rolü. SUPER_ADMIN bir işletmeye "girdiğinde" Admin'dir (bkz. <paramref name="IsActingAsBusiness"/>).</param>
/// <param name="BusinessName">Oturumun bağlı olduğu işletmenin adı (SUPER_ADMIN kendi kimliğindeyken null).</param>
/// <param name="IsActingAsBusiness">true: SUPER_ADMIN, seçtiği işletmenin sahibi gibi çalışıyor; "işletmeden çık" ile kendi kimliğine döner.</param>
public record LoginResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    Guid UserId,
    string FullName,
    UserRole Role,
    Guid? BusinessId,
    string? BusinessName = null,
    bool IsActingAsBusiness = false);

/// <param name="ActingBusinessId">SUPER_ADMIN bir işletmenin içindeyken yenilemede gönderilir; oturum o işletmede kalır. Diğer roller için yok sayılır.</param>
public record RefreshTokenRequest(string RefreshToken, Guid? ActingBusinessId = null);

/// <summary>SUPER_ADMIN'in bir işletmeye "girmesi": o işletmenin sahibi (Admin) yetkisiyle çalışan bir oturum alır.</summary>
public record EnterBusinessRequest(Guid BusinessId);
