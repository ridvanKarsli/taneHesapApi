using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;

namespace TaneHesap.Application.Ingredients;

public class IngredientService : IIngredientService
{
    private readonly IUnitOfWork _unitOfWork;

    public IngredientService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<IngredientDto>> GetAllAsync(Guid businessId, CancellationToken ct = default)
    {
        var items = await _unitOfWork.Repository<Ingredient>().ListAsync(i => i.BusinessId == businessId, ct);
        return items.OrderBy(i => i.Name).Select(ToDto).ToList();
    }

    public async Task<IngredientDto> GetByIdAsync(Guid businessId, Guid id, CancellationToken ct = default)
        => ToDto(await GetTenantScopedAsync(businessId, id, ct));

    public async Task<IngredientDto> CreateAsync(Guid businessId, CreateIngredientRequest request, Guid createdByUserId, CancellationToken ct = default)
    {
        var entity = new Ingredient
        {
            BusinessId = businessId,
            Name = request.Name,
            Unit = request.Unit,
            CurrentUnitPrice = request.CurrentUnitPrice,
            MinimumStockThreshold = request.MinimumStockThreshold,
            CurrentStockQuantity = 0,
            IsActive = true,
            CreatedByUserId = createdByUserId
        };

        await _unitOfWork.Repository<Ingredient>().AddAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    public async Task<IngredientDto> UpdateAsync(Guid businessId, Guid id, UpdateIngredientRequest request, Guid updatedByUserId, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<Ingredient>();
        var entity = await GetTenantScopedAsync(businessId, id, ct);

        entity.Name = request.Name;
        entity.Unit = request.Unit;
        entity.CurrentUnitPrice = request.CurrentUnitPrice;
        entity.MinimumStockThreshold = request.MinimumStockThreshold;
        entity.IsActive = request.IsActive;
        entity.UpdatedByUserId = updatedByUserId;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        repo.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    public async Task<List<IngredientDto>> GetBelowThresholdAsync(Guid businessId, CancellationToken ct = default)
    {
        var items = await _unitOfWork.Repository<Ingredient>()
            .ListAsync(i => i.BusinessId == businessId && i.IsActive && i.CurrentStockQuantity <= i.MinimumStockThreshold, ct);
        return items.OrderBy(i => i.Name).Select(ToDto).ToList();
    }

    private async Task<Ingredient> GetTenantScopedAsync(Guid businessId, Guid id, CancellationToken ct)
    {
        var entity = await _unitOfWork.Repository<Ingredient>().GetByIdAsync(id, ct);
        if (entity is null || entity.BusinessId != businessId)
        {
            throw new NotFoundException(nameof(Ingredient), id);
        }

        return entity;
    }

    private static IngredientDto ToDto(Ingredient i) => new(
        i.Id, i.Name, i.Unit, i.CurrentUnitPrice, i.MinimumStockThreshold, i.CurrentStockQuantity, i.IsActive);
}
