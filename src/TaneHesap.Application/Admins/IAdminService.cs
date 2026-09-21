namespace TaneHesap.Application.Admins;

/// <summary>
/// SUPER_ADMIN'in bir işletmeye ADMIN (işletme sahibi) kullanıcı ekleyip yönetmesi.
/// bkz. Proje Raporu bölüm 2 — "SUPER_ADMIN ... işletmeleri ve ADMIN kullanıcılarını yönetir."
/// Bu, bir işletmenin gerçekte kullanılabilir olması için gereken tek eksik parçaydı: işletme
/// oluşturulduktan sonra oraya giriş yapabilecek bir ADMIN atanmadan işletme "boş" kalıyordu.
/// </summary>
public interface IAdminService
{
    Task<List<AdminDto>> GetAllAsync(Guid businessId, CancellationToken ct = default);

    Task<AdminDto> CreateAsync(Guid businessId, CreateAdminRequest request, CancellationToken ct = default);

    /// <summary>Yönetici hesabını siler (girdiği kayıtlar geçmiş olarak kalır).</summary>
    Task DeleteAsync(Guid businessId, Guid adminId, CancellationToken ct = default);

    Task<AdminDto> UpdateAsync(Guid businessId, Guid adminId, UpdateAdminRequest request, CancellationToken ct = default);
}
