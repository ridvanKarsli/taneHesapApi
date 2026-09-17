using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;

namespace TaneHesap.Application.Platforms;

public class PlatformService : IPlatformService
{
    private readonly IUnitOfWork _unitOfWork;

    public PlatformService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<PlatformDto>> GetAllAsync(Guid businessId, CancellationToken ct = default)
    {
        var items = await _unitOfWork.Repository<Platform>().ListAsync(p => p.BusinessId == businessId, ct);
        return items.OrderBy(p => p.Name).Select(ToDto).ToList();
    }

    public async Task<PlatformDto> GetByIdAsync(Guid businessId, Guid id, CancellationToken ct = default)
        => ToDto(await GetTenantScopedAsync(businessId, id, ct));

    public async Task<PlatformDto> CreateAsync(Guid businessId, CreatePlatformRequest request, Guid createdByUserId, CancellationToken ct = default)
    {
        var entity = new Platform
        {
            BusinessId = businessId,
            Name = request.Name,
            CommissionPercentage = request.CommissionPercentage,
            IsActive = true,
            CreatedByUserId = createdByUserId
        };

        await _unitOfWork.Repository<Platform>().AddAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    public async Task<PlatformDto> UpdateAsync(Guid businessId, Guid id, UpdatePlatformRequest request, Guid updatedByUserId, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<Platform>();
        var entity = await GetTenantScopedAsync(businessId, id, ct);

        entity.Name = request.Name;
        entity.CommissionPercentage = request.CommissionPercentage;
        entity.IsActive = request.IsActive;
        entity.UpdatedByUserId = updatedByUserId;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        repo.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    private async Task<Platform> GetTenantScopedAsync(Guid businessId, Guid id, CancellationToken ct)
    {
        var entity = await _unitOfWork.Repository<Platform>().GetByIdAsync(id, ct);
        if (entity is null || entity.BusinessId != businessId)
        {
            throw new NotFoundException(nameof(Platform), id);
        }

        return entity;
    }

    private static PlatformDto ToDto(Platform p) => new(p.Id, p.Name, p.CommissionPercentage, p.IsActive);
}
