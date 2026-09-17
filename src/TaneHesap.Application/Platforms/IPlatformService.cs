namespace TaneHesap.Application.Platforms;

/// <summary>
/// Paket servis platformları (Yemeksepeti, Getir vb.) ve komisyon yüzdeleri — ADMIN yönetir.
/// Gün sonu Excel içe aktarımında Channel=Platform olan satırların komisyonu buradan hesaplanır.
/// bkz. Proje Raporu bölüm 3.4.
/// </summary>
public interface IPlatformService
{
    Task<List<PlatformDto>> GetAllAsync(Guid businessId, CancellationToken ct = default);

    Task<PlatformDto> GetByIdAsync(Guid businessId, Guid id, CancellationToken ct = default);

    Task<PlatformDto> CreateAsync(Guid businessId, CreatePlatformRequest request, Guid createdByUserId, CancellationToken ct = default);

    Task<PlatformDto> UpdateAsync(Guid businessId, Guid id, UpdatePlatformRequest request, Guid updatedByUserId, CancellationToken ct = default);
}
