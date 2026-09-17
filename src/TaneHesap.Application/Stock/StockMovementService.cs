using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;

namespace TaneHesap.Application.Stock;

public class StockMovementService : IStockMovementService
{
    private readonly IUnitOfWork _unitOfWork;

    public StockMovementService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<StockMovementDto>> GetAllAsync(Guid businessId, Guid? ingredientId, CancellationToken ct = default)
    {
        var movements = await _unitOfWork.Repository<StockMovement>()
            .ListAsync(m => m.BusinessId == businessId && (ingredientId == null || m.IngredientId == ingredientId), ct);

        var ingredients = await _unitOfWork.Repository<Ingredient>().ListAsync(i => i.BusinessId == businessId, ct);
        var ingredientsById = ingredients.ToDictionary(i => i.Id);

        return movements
            .OrderByDescending(m => m.MovementDateUtc)
            .Select(m => ToDto(m, ingredientsById.TryGetValue(m.IngredientId, out var ing) ? ing : null))
            .ToList();
    }

    public async Task<StockMovementDto> CreateAsync(Guid businessId, CreateStockMovementRequest request, Guid createdByUserId, CancellationToken ct = default)
    {
        var ingredientRepo = _unitOfWork.Repository<Ingredient>();
        var ingredient = await ingredientRepo.GetByIdAsync(request.IngredientId, ct);
        if (ingredient is null || ingredient.BusinessId != businessId)
        {
            throw new NotFoundException(nameof(Ingredient), request.IngredientId);
        }

        var movement = new StockMovement
        {
            BusinessId = businessId,
            IngredientId = ingredient.Id,
            QuantityChange = request.QuantityChange,
            MovementType = request.MovementType,
            MovementDateUtc = DateTime.UtcNow,
            Note = request.Note,
            CreatedByUserId = createdByUserId
        };

        await _unitOfWork.Repository<StockMovement>().AddAsync(movement, ct);

        ingredient.CurrentStockQuantity += request.QuantityChange;
        ingredient.UpdatedByUserId = createdByUserId;
        ingredient.UpdatedAtUtc = DateTime.UtcNow;
        ingredientRepo.Update(ingredient);

        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(movement, ingredient);
    }

    private static StockMovementDto ToDto(StockMovement m, Ingredient? ingredient) => new(
        m.Id,
        m.IngredientId,
        ingredient?.Name ?? string.Empty,
        m.QuantityChange,
        m.MovementType,
        m.MovementDateUtc,
        m.SourceReferenceType,
        m.SourceReferenceId,
        m.Note,
        ingredient?.CurrentStockQuantity ?? 0);
}
