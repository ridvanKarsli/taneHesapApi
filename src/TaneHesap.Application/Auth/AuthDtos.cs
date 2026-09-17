using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Auth;

/// <summary>
/// Giriş isteği. TotpCode, SUPER_ADMIN ve ADMIN için zorunludur; EMPLOYEE için gönderilmez/yoksayılır.
/// bkz. Proje Raporu bölüm 2, 5 (iş akışları 7-8).
/// </summary>
public record LoginRequest(string Username, string Password, string? TotpCode);

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

/// <summary>SUPER_ADMIN/ADMIN'in ilk kez authenticator kurulumu yaparken kullandığı yanıt.</summary>
public record TotpSetupResponse(string Secret, string QrCodeUri);
