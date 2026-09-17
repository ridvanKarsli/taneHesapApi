using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Common.Interfaces;

public record AccessTokenResult(string Token, DateTime ExpiresAtUtc);

/// <summary>
/// JWT access token üretimi. Refresh token'ların kendisi Application/Auth katmanında
/// RefreshToken entity'si üzerinden, IUnitOfWork ile yönetilir (bkz. Proje Raporu bölüm 8).
/// </summary>
public interface IJwtTokenService
{
    AccessTokenResult GenerateAccessToken(Guid userId, string username, string fullName, UserRole role, Guid? businessId);

    /// <summary>Kriptografik olarak güvenli, rastgele bir refresh token değeri üretir (henüz hash'lenmemiş).</summary>
    string GenerateRefreshTokenValue();

    /// <summary>Bir refresh token değerinin veritabanında saklanacak hash'ini üretir.</summary>
    string HashRefreshToken(string refreshTokenValue);
}
