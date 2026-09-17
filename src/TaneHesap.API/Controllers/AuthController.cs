using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Auth;
using TaneHesap.Application.Common.Interfaces;

namespace TaneHesap.API.Controllers;

/// <summary>
/// Giriş, token yenileme ve authenticator (TOTP) kurulumu.
/// bkz. Proje Raporu bölüm 2, 5 (iş akışı 7-9), 8.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUserService;

    public AuthController(IAuthService authService, ICurrentUserService currentUserService)
    {
        _authService = authService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// SUPER_ADMIN/ADMIN: kullanıcı adı + şifre + authenticator kodu.
    /// EMPLOYEE: sadece kullanıcı adı + şifre (TotpCode gönderilmez).
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.LoginAsync(request, ip, ct);

        if (!result.Success || result.Data is null)
        {
            return Unauthorized(new { error = result.ErrorMessage });
        }

        return Ok(result.Data);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.RefreshTokenAsync(request.RefreshToken, ip, ct);

        if (!result.Success || result.Data is null)
        {
            return Unauthorized(new { error = result.ErrorMessage });
        }

        return Ok(result.Data);
    }

    [HttpPost("revoke")]
    [AllowAnonymous]
    public async Task<IActionResult> Revoke([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        await _authService.RevokeRefreshTokenAsync(request.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>SUPER_ADMIN/ADMIN ilk girişte authenticator (TOTP) kurulum bilgisini (secret + QR URI) alır.</summary>
    [HttpPost("totp/setup")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<ActionResult<TotpSetupResponse>> SetupTotp(CancellationToken ct)
    {
        var userId = _currentUserService.UserId!.Value;
        var response = await _authService.SetupTotpAsync(userId, ct);
        return Ok(response);
    }
}
