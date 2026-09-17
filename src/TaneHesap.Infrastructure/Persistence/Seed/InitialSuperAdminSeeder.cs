using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Infrastructure.Persistence.Seed;

/// <summary>
/// Uygulama açılışında, sistemde HİÇ SUPER_ADMIN yoksa ve konfigürasyonda ("InitialSuperAdmin"
/// bölümü — appsettings/user-secrets/ortam değişkeni) kullanıcı adı/şifre tanımlıysa ilk
/// SUPER_ADMIN kullanıcısını oluşturur. Zaten bir SUPER_ADMIN varsa ya da ayar boşsa hiçbir şey
/// yapmaz; bu yüzden her açılışta güvenle çağrılabilir (idempotent). Kimlik bilgileri koda gömülmez.
/// bkz. Proje Raporu bölüm 2, 3.7; README "İlk SUPER_ADMIN kullanıcısını oluşturma".
/// </summary>
public class InitialSuperAdminSeeder
{
    private readonly IIdentityService _identityService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InitialSuperAdminSeeder> _logger;

    public InitialSuperAdminSeeder(IIdentityService identityService, IConfiguration configuration, ILogger<InitialSuperAdminSeeder> logger)
    {
        _identityService = identityService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        if (await _identityService.AnySuperAdminExistsAsync())
        {
            return;
        }

        var section = _configuration.GetSection("InitialSuperAdmin");
        var username = section["Username"];
        var password = section["Password"];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning(
                "Sistemde SUPER_ADMIN yok ve InitialSuperAdmin:Username/Password tanımlı değil. " +
                "İlk SUPER_ADMIN'i oluşturmak için appsettings/user-secrets/ortam değişkenleriyle " +
                "InitialSuperAdmin:Username ve InitialSuperAdmin:Password değerlerini tanımlayıp uygulamayı yeniden başlatın.");
            return;
        }

        var fullName = section["FullName"];
        var result = await _identityService.CreateAdminOrSuperAdminAsync(
            username,
            password,
            string.IsNullOrWhiteSpace(fullName) ? "Sistem Yöneticisi" : fullName,
            UserRole.SuperAdmin,
            businessId: null);

        if (result.Succeeded)
        {
            _logger.LogInformation("İlk SUPER_ADMIN kullanıcısı '{Username}' oluşturuldu.", username);
        }
        else
        {
            _logger.LogError("İlk SUPER_ADMIN oluşturulamadı: {Errors}", string.Join("; ", result.Errors));
        }
    }
}
