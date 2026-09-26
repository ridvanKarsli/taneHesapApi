using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

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

        if (!await IsBusinessUsableAsync(user, ct))
        {
            return ServiceResult<LoginResponse>.Fail("İşletmeniz pasif durumda; giriş yapılamaz.");
        }

        // Not: authenticator (2FA) zorunluluğu kaldırıldı — tüm roller sadece kullanıcı adı/şifre
        // ile giriş yapar (bkz. Proje Raporu bölüm 2, 7).
        var response = await IssueTokensAsync(user, ipAddress, ct);
        return ServiceResult<LoginResponse>.Ok(response);
    }

    public Task<ServiceResult<LoginResponse>> RefreshTokenAsync(string refreshToken, string? ipAddress, CancellationToken ct = default)
        => RefreshTokenAsync(refreshToken, actingBusinessId: null, ipAddress, ct);

    public async Task<ServiceResult<LoginResponse>> RefreshTokenAsync(string refreshToken, Guid? actingBusinessId, string? ipAddress, CancellationToken ct = default)
    {
        var tokenHash = _jwtTokenService.HashRefreshToken(refreshToken);
        var repo = _unitOfWork.Repository<RefreshToken>();

        var existing = (await repo.ListAsync(t => t.TokenHash == tokenHash, ct)).FirstOrDefault();
        if (existing is null || !existing.IsActive)
        {
            return ServiceResult<LoginResponse>.Fail("Refresh token geçersiz veya süresi dolmuş.");
        }

        var user = await _identityService.GetByIdAsync(existing.UserId);
        if (user is null || !user.IsActive || !await IsBusinessUsableAsync(user, ct))
        {
            return ServiceResult<LoginResponse>.Fail("Kullanıcı bulunamadı veya pasif.");
        }

        // Rotation: eski refresh token iptal edilir, yenisi verilir.
        existing.RevokedAtUtc = DateTime.UtcNow;

        var acting = actingBusinessId is not null && user.Role == UserRole.SuperAdmin
            ? await FindActiveBusinessAsync(actingBusinessId.Value, ct)
            : null;

        var response = await IssueTokensAsync(user, ipAddress, ct, existingTokenToReplace: existing, actingBusiness: acting);
        return ServiceResult<LoginResponse>.Ok(response);
    }

    public async Task<ServiceResult<LoginResponse>> EnterBusinessAsync(Guid superAdminUserId, Guid businessId, string? ipAddress, CancellationToken ct = default)
    {
        var user = await _identityService.GetByIdAsync(superAdminUserId);
        if (user is null || !user.IsActive || user.Role != UserRole.SuperAdmin)
        {
            return ServiceResult<LoginResponse>.Fail("Yalnızca süper yönetici bir işletmeye girebilir.");
        }

        var business = await FindActiveBusinessAsync(businessId, ct);
        if (business is null)
        {
            return ServiceResult<LoginResponse>.Fail("İşletme bulunamadı veya pasif.");
        }

        return ServiceResult<LoginResponse>.Ok(await IssueTokensAsync(user, ipAddress, ct, actingBusiness: business));
    }

    private async Task<Business?> FindActiveBusinessAsync(Guid businessId, CancellationToken ct)
    {
        var business = await _unitOfWork.Repository<Business>().GetByIdAsync(businessId, ct);
        return business is { IsActive: true } ? business : null;
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

    /// <summary>SUPER_ADMIN işletmeye bağlı değildir; ADMIN/EMPLOYEE'nin işletmesi pasifse oturum açılamaz ve yenilenemez.</summary>
    private async Task<bool> IsBusinessUsableAsync(ApplicationUserInfo user, CancellationToken ct)
    {
        if (user.BusinessId is null)
        {
            return user.Role == UserRole.SuperAdmin;
        }

        var business = await _unitOfWork.Repository<Business>().GetByIdAsync(user.BusinessId.Value, ct);
        return business is { IsActive: true };
    }

    private async Task<LoginResponse> IssueTokensAsync(
        ApplicationUserInfo user, string? ipAddress, CancellationToken ct, RefreshToken? existingTokenToReplace = null, Business? actingBusiness = null)
    {
        // İşletme içindeki SUPER_ADMIN: rol Admin, business_id o işletme; kullanıcı kimliği (sub) kendisi kalır.
        var role = actingBusiness is null ? user.Role : UserRole.Admin;
        var businessId = actingBusiness?.Id ?? user.BusinessId;
        var businessName = actingBusiness?.Name
            ?? (user.BusinessId is null ? null : (await _unitOfWork.Repository<Business>().GetByIdAsync(user.BusinessId.Value, ct))?.Name);

        var accessToken = _jwtTokenService.GenerateAccessToken(user.UserId, user.Username, user.FullName, role, businessId);

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
            role,
            businessId,
            businessName,
            IsActingAsBusiness: actingBusiness is not null);
    }
}
