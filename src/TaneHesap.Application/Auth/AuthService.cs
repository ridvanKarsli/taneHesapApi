using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;

namespace TaneHesap.Application.Auth;

public class AuthService : IAuthService
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUnitOfWork _unitOfWork;

    // Access token ~30 dakika, refresh token ~14 gün (bkz. Proje Raporu bölüm 8).
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(14);

    public AuthService(
        IIdentityService identityService,
        IJwtTokenService jwtTokenService,
        IUnitOfWork unitOfWork)
    {
        _identityService = identityService;
        _jwtTokenService = jwtTokenService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var user = await _identityService.ValidatePasswordAsync(request.Username, request.Password);
        if (user is null || !user.IsActive)
        {
            return ServiceResult<LoginResponse>.Fail("Kullanıcı adı veya şifre hatalı.");
        }

        // Not: authenticator (2FA) zorunluluğu kaldırıldı — tüm roller sadece kullanıcı adı/şifre
        // ile giriş yapar (bkz. Proje Raporu bölüm 2, 7).
        var response = await IssueTokensAsync(user, ipAddress, ct);
        return ServiceResult<LoginResponse>.Ok(response);
    }

    public async Task<ServiceResult<LoginResponse>> RefreshTokenAsync(string refreshToken, string? ipAddress, CancellationToken ct = default)
    {
        var tokenHash = _jwtTokenService.HashRefreshToken(refreshToken);
        var repo = _unitOfWork.Repository<RefreshToken>();

        var existing = (await repo.ListAsync(t => t.TokenHash == tokenHash, ct)).FirstOrDefault();
        if (existing is null || !existing.IsActive)
        {
            return ServiceResult<LoginResponse>.Fail("Refresh token geçersiz veya süresi dolmuş.");
        }

        var user = await _identityService.GetByIdAsync(existing.UserId);
        if (user is null || !user.IsActive)
        {
            return ServiceResult<LoginResponse>.Fail("Kullanıcı bulunamadı veya pasif.");
        }

        // Rotation: eski refresh token iptal edilir, yenisi verilir.
        existing.RevokedAtUtc = DateTime.UtcNow;

        var response = await IssueTokensAsync(user, ipAddress, ct, existingTokenToReplace: existing);
        return ServiceResult<LoginResponse>.Ok(response);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenHash = _jwtTokenService.HashRefreshToken(refreshToken);
        var repo = _unitOfWork.Repository<RefreshToken>();

        var existing = (await repo.ListAsync(t => t.TokenHash == tokenHash, ct)).FirstOrDefault();
        if (existing is not null && existing.IsActive)
        {
            existing.RevokedAtUtc = DateTime.UtcNow;
            repo.Update(existing);
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    private async Task<LoginResponse> IssueTokensAsync(
        ApplicationUserInfo user, string? ipAddress, CancellationToken ct, RefreshToken? existingTokenToReplace = null)
    {
        var accessToken = _jwtTokenService.GenerateAccessToken(user.UserId, user.Username, user.FullName, user.Role, user.BusinessId);

        var refreshTokenValue = _jwtTokenService.GenerateRefreshTokenValue();
        var refreshTokenHash = _jwtTokenService.HashRefreshToken(refreshTokenValue);
        var refreshTokenExpiresAtUtc = DateTime.UtcNow.Add(RefreshTokenLifetime);

        var newRefreshToken = new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = refreshTokenHash,
            ExpiresAtUtc = refreshTokenExpiresAtUtc,
            CreatedByIp = ipAddress
        };

        if (existingTokenToReplace is not null)
        {
            existingTokenToReplace.ReplacedByTokenHash = refreshTokenHash;
            _unitOfWork.Repository<RefreshToken>().Update(existingTokenToReplace);
        }

        await _unitOfWork.Repository<RefreshToken>().AddAsync(newRefreshToken, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new LoginResponse(
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            refreshTokenValue,
            refreshTokenExpiresAtUtc,
            user.UserId,
            user.FullName,
            user.Role,
            user.BusinessId);
    }
}
