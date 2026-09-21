using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.Notifications;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Stock;

public class StockMovementService : IStockMovementService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;

    public StockMovementService(IUnitOfWork unitOfWork, INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
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

        if (ingredient.IsActive && ingredient.CurrentStockQuantity <= ingredient.MinimumStockThreshold)
        {
            await _notificationService.NotifyLowStockAsync(
                businessId, ingredient.Name, ingredient.CurrentStockQuantity, ingredient.MinimumStockThreshold, ct);
        }

        return ToDto(movement, ingredient);
    }

    public async Task DeleteAsync(Guid businessId, Guid id, Guid deletedByUserId, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<StockMovement>();
        var movement = await repo.GetByIdAsync(id, ct);
        if (movement is null || movement.BusinessId != businessId)
        {
            throw new NotFoundException(nameof(StockMovement), id);
        }

        if (movement.MovementType is not (StockMovementType.ManualAdjustment or StockMovementType.Waste))
        {
            throw new ConflictAppException("Alış ve satış tüketimi hareketleri buradan silinemez; tedarikçi alışı veya gün sonu kapanışı üzerinden düzeltilir.");
        }

        var ingredientRepo = _unitOfWork.Repository<Ingredient>();
        var ingredient = await ingredientRepo.GetByIdAsync(movement.IngredientId, ct);
        if (ingredient is not null)
        {
            ingredient.CurrentStockQuantity -= movement.QuantityChange;
            ingredient.UpdatedByUserId = deletedByUserId;
            ingredient.UpdatedAtUtc = DateTime.UtcNow;
            ingredientRepo.Update(ingredient);
        }

        repo.Remove(movement);
        await _unitOfWork.SaveChangesAsync(ct);
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
