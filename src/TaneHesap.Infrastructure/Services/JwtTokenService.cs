using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Infrastructure.Services;

/// <summary>
/// JWT access token üretimi ve refresh token değeri/hash üretimi.
/// Access token ömrü ~30 dakika (appsettings üzerinden yapılandırılabilir). bkz. Proje Raporu bölüm 8.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public AccessTokenResult GenerateAccessToken(Guid userId, string username, string fullName, UserRole role, Guid? businessId)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var secret = jwtSection["Secret"] ?? throw new InvalidOperationException("Jwt:Secret appsettings içinde tanımlı değil.");
        var issuer = jwtSection["Issuer"] ?? "taneHesap";
        var audience = jwtSection["Audience"] ?? "taneHesap";
        var minutes = int.TryParse(jwtSection["AccessTokenMinutes"], out var m) ? m : 30;

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(minutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new("full_name", fullName),
            new(ClaimTypes.Role, role.ToString())
        };

        if (businessId.HasValue)
        {
            claims.Add(new Claim("business_id", businessId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessTokenResult(tokenValue, expiresAtUtc);
    }

    public string GenerateRefreshTokenValue()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    public string HashRefreshToken(string refreshTokenValue)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshTokenValue));
        return Convert.ToHexString(bytes);
    }
}
