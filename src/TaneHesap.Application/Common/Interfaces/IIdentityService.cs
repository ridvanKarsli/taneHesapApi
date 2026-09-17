using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Common.Interfaces;

public record IdentityOperationResult(bool Succeeded, Guid? UserId, IReadOnlyList<string> Errors)
{
    public static IdentityOperationResult Success(Guid userId) => new(true, userId, Array.Empty<string>());
    public static IdentityOperationResult Failure(params string[] errors) => new(false, null, errors);
}

public record ApplicationUserInfo(
    Guid UserId,
    string Username,
    string FullName,
    UserRole Role,
    Guid? BusinessId,
    bool IsActive,
    bool TotpEnabled);

/// <summary>
/// Application katmanı ile ASP.NET Core Identity (Infrastructure) arasındaki köprü.
/// Kullanıcı oluşturma/güncelleme, şifre doğrulama ve TOTP secret yönetimi buradan yapılır.
/// bkz. Proje Raporu bölüm 2, 3.7.
/// </summary>
public interface IIdentityService
{
    /// <summary>SUPER_ADMIN veya ADMIN oluşturur (authenticator/2FA zorunlu roller).</summary>
    Task<IdentityOperationResult> CreateAdminOrSuperAdminAsync(
        string username, string password, string fullName, UserRole role, Guid? businessId);

    /// <summary>ADMIN tarafından çağrılır; EMPLOYEE oluşturur (2FA yok). bkz. bölüm 3.7.</summary>
    Task<IdentityOperationResult> CreateEmployeeAsync(
        Guid businessId, string username, string password, string fullName);

    Task<bool> UpdateEmployeeAsync(Guid userId, Guid businessId, string? fullName, string? username, string? newPassword);

    Task<bool> SetActiveAsync(Guid userId, bool isActive);

    /// <summary>Kullanıcı adı/şifre doğrular; başarılıysa kullanıcı bilgisini döner.</summary>
    Task<ApplicationUserInfo?> ValidatePasswordAsync(string username, string password);

    Task<ApplicationUserInfo?> GetByIdAsync(Guid userId);

    Task<List<ApplicationUserInfo>> GetEmployeesByBusinessAsync(Guid businessId);

    /// <summary>Bir işletmenin ADMIN kullanıcılarını döner — in-app bildirimler bunlara gönderilir.</summary>
    Task<List<ApplicationUserInfo>> GetAdminsByBusinessAsync(Guid businessId);

    /// <summary>Sistemde en az bir SUPER_ADMIN var mı — uygulama açılışındaki tek seferlik ilk kurulum seed'i için kullanılır.</summary>
    Task<bool> AnySuperAdminExistsAsync();

    /// <summary>SUPER_ADMIN/ADMIN için TOTP secret'ı döner; yoksa yeni bir tane oluşturup kaydeder.</summary>
    Task<string> GetOrCreateTotpSecretAsync(Guid userId);

    Task<bool> ValidateTotpCodeAsync(Guid userId, string code);

    Task MarkTotpEnabledAsync(Guid userId);
}
