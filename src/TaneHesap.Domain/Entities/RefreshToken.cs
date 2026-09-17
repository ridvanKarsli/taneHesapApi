using TaneHesap.Domain.Common;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// JWT refresh token kaydı — rotation ve iptal edilebilirlik için hash'lenmiş olarak saklanır.
/// bkz. Proje Raporu bölüm 8 (Kimlik doğrulama ve token yönetimi).
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Token'ın kendisi değil, hash'i saklanır (örn. SHA-256).</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Rotation zincirinde bu token'ın yerine geçen yeni token'ın hash'i.</summary>
    public string? ReplacedByTokenHash { get; set; }

    public string? CreatedByIp { get; set; }

    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}
