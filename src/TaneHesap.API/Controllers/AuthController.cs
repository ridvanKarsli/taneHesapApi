using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Auth;
using TaneHesap.Application.Common.Interfaces;

namespace TaneHesap.API.Controllers;

/// <summary>
/// Giriş, token yenileme ve iptal. Authenticator (2FA) zorunluluğu kaldırıldı — tüm roller
/// kullanıcı adı/şifre ile giriş yapar (bkz. Proje Raporu bölüm 2, 7).
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

    /// <summary>Tüm roller: kullanıcı adı + şifre (authenticator/2FA zorunluluğu kaldırıldı).</summary>
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
        var result = await _authService.RefreshTokenAsync(request.RefreshToken, request.ActingBusinessId, ip, ct);

        if (!result.Success || result.Data is null)
        {
            return Unauthorized(new { error = result.ErrorMessage });
        }

        return Ok(result.Data);
    }

    /// <summary>SUPER_ADMIN seçtiği işletmeye girer: dönen oturum o işletmenin sahibi (Admin) yetkisindedir. Çıkış: ActingBusinessId'siz refresh.</summary>
    [HttpPost("enter-business")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<LoginResponse>> EnterBusiness([FromBody] EnterBusinessRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.EnterBusinessAsync(_currentUserService.UserId!.Value, request.BusinessId, ip, ct);

        if (!result.Success || result.Data is null)
        {
            return BadRequest(new { error = result.ErrorMessage });
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
}
