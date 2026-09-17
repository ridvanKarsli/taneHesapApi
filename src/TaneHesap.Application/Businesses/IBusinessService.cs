namespace TaneHesap.Application.Businesses;

/// <summary>
/// Sadece SUPER_ADMIN tarafından kullanılır — yeni işletme açma ve işletmeleri yönetme.
/// bkz. Proje Raporu bölüm 2.
/// </summary>
public interface IBusinessService
{
    Task<List<BusinessDto>> GetAllAsync(CancellationToken ct = default);

    Task<BusinessDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<BusinessDto> CreateAsync(CreateBusinessRequest request, Guid createdByUserId, CancellationToken ct = default);

    Task<BusinessDto> UpdateAsync(Guid id, UpdateBusinessRequest request, Guid updatedByUserId, CancellationToken ct = default);
}
