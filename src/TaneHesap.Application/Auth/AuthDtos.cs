using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Auth;

/// <summary>
/// Giriş isteği. Tüm roller (SUPER_ADMIN/ADMIN/EMPLOYEE) sadece kullanıcı adı/şifre ile giriş
/// yapar — authenticator (2FA) zorunluluğu kaldırıldı (bkz. Proje Raporu bölüm 2, 7).
/// </summary>
public record LoginRequest(string Username, string Password);

public record LoginResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    Guid UserId,
    string FullName,
    UserRole Role,
    Guid? BusinessId);

public record RefreshTokenRequest(string RefreshToken);
