using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Auth;

public class AuthService : IAuthService
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ITotpService _totpService;
    private readonly IUnitOfWork _unitOfWork;

    // Access token ~30 dakika, refresh token ~14 gün (bkz. Proje Raporu bölüm 8).
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(14);

    public AuthService(
        IIdentityService identityService,
        IJwtTokenService jwtTokenService,
        ITotpService totpService,
        IUnitOfWork unitOfWork)
    {
        _identityService = identityService;
        _jwtTokenService = jwtTokenService;
        _totpService = totpService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var user = await _identityService.ValidatePasswordAsync(request.Username, request.Password);
        if (user is null || !user.IsActive)
        {
            return ServiceResult<LoginResponse>.Fail("Kullanıcı adı veya şifre hatalı.");
        }

        // SUPER_ADMIN ve ADMIN için authenticator (TOTP) zorunlu; EMPLOYEE için yok (bkz. bölüm 2).
        // TotpEnabled henüz false ise (ilk kurulum tamamlanmamış — bkz. SetupTotpAsync/ConfirmTotpAsync),
        // kod istenmez: kullanıcı önce kodsuz giriş yapıp /api/auth/totp/setup ile QR/secret alabilir,
        // authenticator uygulamasına ekleyip /api/auth/totp/confirm ile kurulumu tamamlayabilir. TotpEnabled
        // true olduktan sonra her girişte kod zorunludur.
        if (user.Role is UserRole.SuperAdmin or UserRole.Admin && user.TotpEnabled)
        {
            if (string.IsNullOrWhiteSpace(request.TotpCode))
            {
                return ServiceResult<LoginResponse>.Fail("Authenticator kodu gereklidir.");
            }

            var totpValid = await _identityService.ValidateTotpCodeAsync(user.UserId, request.TotpCode);
            if (!totpValid)
            {
                return ServiceResult<LoginResponse>.Fail("Authenticator kodu hatalı.");
            }
        }

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

    public async Task<TotpSetupResponse> SetupTotpAsync(Guid userId, CancellationToken ct = default)
    {
        var secret = await _identityService.GetOrCreateTotpSecretAsync(userId);
        var user = await _identityService.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        var qrUri = _totpService.GenerateQrCodeUri(secret, user.Username);
        return new TotpSetupResponse(secret, qrUri);
    }

    public async Task<ServiceResult<bool>> ConfirmTotpAsync(Guid userId, string code, CancellationToken ct = default)
    {
        var totpValid = await _identityService.ValidateTotpCodeAsync(userId, code);
        if (!totpValid)
        {
            return ServiceResult<bool>.Fail("Authenticator kodu hatalı.");
        }

        await _identityService.MarkTotpEnabledAsync(userId);
        return ServiceResult<bool>.Ok(true);
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
