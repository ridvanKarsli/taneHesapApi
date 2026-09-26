using TaneHesap.Application.Common;
using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;

namespace TaneHesap.Application.Dishes;

public class DishService : IDishService
{
    private readonly IUnitOfWork _unitOfWork;

    public DishService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<DishDto>> GetAllAsync(Guid businessId, CancellationToken ct = default)
    {
        var dishes = await _unitOfWork.Repository<Dish>().ListAsync(d => d.BusinessId == businessId, ct);
        var catalog = await LoadCatalogAsync(businessId, dishes.Select(d => d.Id).ToList(), ct);
        return dishes.OrderBy(d => d.Name).Select(d => ToDto(d, catalog)).ToList();
    }

    public async Task<DishDto> GetByIdAsync(Guid businessId, Guid dishId, CancellationToken ct = default)
    {
        var dish = await GetTenantScopedDishAsync(businessId, dishId, ct);
        return await BuildDishDtoAsync(businessId, dish, ct);
    }

    public async Task<DishDto> CreateDishAsync(Guid businessId, CreateDishRequest request, Guid createdByUserId, CancellationToken ct = default)
    {
        var dish = new Dish
        {
            BusinessId = businessId,
            Name = request.Name,
            Description = request.Description,
            IsActive = true,
            CreatedByUserId = createdByUserId
        };

        await _unitOfWork.Repository<Dish>().AddAsync(dish, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await BuildDishDtoAsync(businessId, dish, ct);
    }

    public async Task<DishDto> UpdateDishAsync(Guid businessId, Guid dishId, UpdateDishRequest request, Guid updatedByUserId, CancellationToken ct = default)
    {
        var dish = await GetTenantScopedDishAsync(businessId, dishId, ct);

        dish.Name = request.Name;
        dish.Description = request.Description;
        dish.IsActive = request.IsActive;
        dish.UpdatedByUserId = updatedByUserId;
        dish.UpdatedAtUtc = DateTime.UtcNow;

        _unitOfWork.Repository<Dish>().Update(dish);
        await _unitOfWork.SaveChangesAsync(ct);

        return await BuildDishDtoAsync(businessId, dish, ct);
    }

    public async Task<DishSizeDto> AddSizeAsync(Guid businessId, Guid dishId, CreateDishSizeRequest request, Guid createdByUserId, CancellationToken ct = default)
    {
        var dish = await GetTenantScopedDishAsync(businessId, dishId, ct);
        await ValidateIngredientsAsync(businessId, request.RecipeItems, ct);

        var size = new DishSize
        {
            BusinessId = businessId,
            DishId = dish.Id,
            Name = request.Name,
            SalePrice = request.SalePrice,
            IsActive = true,
            CreatedByUserId = createdByUserId
        };

        await _unitOfWork.Repository<DishSize>().AddAsync(size, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        foreach (var item in request.RecipeItems)
        {
            await _unitOfWork.Repository<DishRecipeItem>().AddAsync(new DishRecipeItem
            {
                BusinessId = businessId,
                DishSizeId = size.Id,
                IngredientId = item.IngredientId,
                Quantity = item.Quantity,
                CreatedByUserId = createdByUserId
            }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return await BuildDishSizeDtoAsync(businessId, size, ct);
    }

    public async Task<DishSizeDto> UpdateSizeAsync(Guid businessId, Guid dishId, Guid sizeId, UpdateDishSizeRequest request, Guid updatedByUserId, CancellationToken ct = default)
    {
        await GetTenantScopedDishAsync(businessId, dishId, ct);
        await ValidateIngredientsAsync(businessId, request.RecipeItems, ct);

        var sizeRepo = _unitOfWork.Repository<DishSize>();
        var size = await sizeRepo.GetByIdAsync(sizeId, ct);
        if (size is null || size.BusinessId != businessId || size.DishId != dishId)
        {
            throw new NotFoundException(nameof(DishSize), sizeId);
        }

        size.Name = request.Name;
        size.SalePrice = request.SalePrice;
        size.IsActive = request.IsActive;
        size.UpdatedByUserId = updatedByUserId;
        size.UpdatedAtUtc = DateTime.UtcNow;
        sizeRepo.Update(size);

        // Mevcut reçeteyi tamamen değiştir (basit/anlaşılır güncelleme stratejisi).
        var recipeRepo = _unitOfWork.Repository<DishRecipeItem>();
        var existingItems = await recipeRepo.ListAsync(r => r.DishSizeId == sizeId, ct);
        foreach (var item in existingItems)
        {
            recipeRepo.Remove(item);
        }

        foreach (var item in request.RecipeItems)
        {
            await recipeRepo.AddAsync(new DishRecipeItem
            {
                BusinessId = businessId,
                DishSizeId = sizeId,
                IngredientId = item.IngredientId,
                Quantity = item.Quantity,
                CreatedByUserId = updatedByUserId
            }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return await BuildDishSizeDtoAsync(businessId, size, ct);
    }

    private async Task ValidateIngredientsAsync(Guid businessId, List<RecipeItemRequest> items, CancellationToken ct)
    {
        foreach (var item in items)
        {
            var ingredient = await _unitOfWork.Repository<Ingredient>().GetByIdAsync(item.IngredientId, ct);
            if (ingredient is null || ingredient.BusinessId != businessId)
            {
                throw new NotFoundException(nameof(Ingredient), item.IngredientId);
            }
        }
    }

    public async Task DeleteDishAsync(Guid businessId, Guid dishId, CancellationToken ct = default)
    {
        var dish = await GetTenantScopedDishAsync(businessId, dishId, ct);
        var sizes = await _unitOfWork.Repository<DishSize>().ListAsync(s => s.DishId == dishId, ct);
        var sizeIds = sizes.Select(s => s.Id).ToList();

        DeletionGuard.EnsureNotUsed(
            await _unitOfWork.Repository<DailySalesEntry>().AnyAsync(e => sizeIds.Contains(e.DishSizeId), ct),
            "Bu ürün", "gün sonu satışlarında");

        await RemoveSizesAsync(sizes, ct);
        _unitOfWork.Repository<Dish>().Remove(dish);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DeleteSizeAsync(Guid businessId, Guid dishId, Guid sizeId, CancellationToken ct = default)
    {
        await GetTenantScopedDishAsync(businessId, dishId, ct);
        var size = (await _unitOfWork.Repository<DishSize>().ListAsync(s => s.Id == sizeId && s.DishId == dishId, ct)).FirstOrDefault()
            ?? throw new NotFoundException(nameof(DishSize), sizeId);

        DeletionGuard.EnsureNotUsed(
            await _unitOfWork.Repository<DailySalesEntry>().AnyAsync(e => e.DishSizeId == sizeId, ct),
            "Bu tabak boyu", "gün sonu satışlarında");

        await RemoveSizesAsync(new List<DishSize> { size }, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Boyları reçete kalemleriyle birlikte siler (SaveChanges çağırmaz).</summary>
    private async Task RemoveSizesAsync(List<DishSize> sizes, CancellationToken ct)
    {
        var sizeIds = sizes.Select(s => s.Id).ToList();
        var recipeRepo = _unitOfWork.Repository<DishRecipeItem>();
        foreach (var item in await recipeRepo.ListAsync(r => sizeIds.Contains(r.DishSizeId), ct))
        {
            recipeRepo.Remove(item);
        }

        foreach (var size in sizes)
        {
            _unitOfWork.Repository<DishSize>().Remove(size);
        }
    }

    private async Task<Dish> GetTenantScopedDishAsync(Guid businessId, Guid dishId, CancellationToken ct)
    {
        var dish = await _unitOfWork.Repository<Dish>().GetByIdAsync(dishId, ct);
        if (dish is null || dish.BusinessId != businessId)
        {
            throw new NotFoundException(nameof(Dish), dishId);
        }

        return dish;
    }

    private async Task<DishDto> BuildDishDtoAsync(Guid businessId, Dish dish, CancellationToken ct)
        => ToDto(dish, await LoadCatalogAsync(businessId, new List<Guid> { dish.Id }, ct));

    private async Task<DishSizeDto> BuildDishSizeDtoAsync(Guid businessId, DishSize size, CancellationToken ct)
        => ToDto(size, await LoadCatalogAsync(businessId, new List<Guid> { size.DishId }, ct));

    /// <summary>
    /// Ürün listesi için gereken her şey sabit sayıda sorguyla (boylar, reçete kalemleri, malzemeler) — ürün/boy başına
    /// ayrı sorgu atılmaz (N+1). Veritabanı gidiş-dönüşü pahalı olduğu için liste ne kadar büyürse büyüsün 3 sorgu.
    /// </summary>
    private async Task<DishCatalog> LoadCatalogAsync(Guid businessId, List<Guid> dishIds, CancellationToken ct)
    {
        var sizes = await _unitOfWork.Repository<DishSize>().ListAsync(s => dishIds.Contains(s.DishId), ct);
        var sizeIds = sizes.Select(s => s.Id).ToList();
        var recipeItems = sizeIds.Count == 0
            ? new List<DishRecipeItem>()
            : await _unitOfWork.Repository<DishRecipeItem>().ListAsync(r => sizeIds.Contains(r.DishSizeId), ct);
        var ingredients = await _unitOfWork.Repository<Ingredient>().ListAsync(i => i.BusinessId == businessId, ct);

        return new DishCatalog(
            sizes.ToLookup(s => s.DishId),
            recipeItems.ToLookup(r => r.DishSizeId),
            ingredients.ToDictionary(i => i.Id));
    }

    private sealed record DishCatalog(
        ILookup<Guid, DishSize> SizesByDish,
        ILookup<Guid, DishRecipeItem> RecipeBySize,
        IReadOnlyDictionary<Guid, Ingredient> Ingredients);

    private static DishDto ToDto(Dish dish, DishCatalog catalog) => new(
        dish.Id, dish.Name, dish.Description, dish.IsActive,
        catalog.SizesByDish[dish.Id].OrderBy(s => s.SalePrice).Select(s => ToDto(s, catalog)).ToList());

    private static DishSizeDto ToDto(DishSize size, DishCatalog catalog)
    {
        var recipeDtos = new List<RecipeItemDto>();
        decimal totalCost = 0;

        foreach (var item in catalog.RecipeBySize[size.Id])
        {
            if (!catalog.Ingredients.TryGetValue(item.IngredientId, out var ingredient))
            {
                continue;
            }

            var lineCost = item.Quantity * ingredient.CurrentUnitPrice;
            totalCost += lineCost;
            recipeDtos.Add(new RecipeItemDto(ingredient.Id, ingredient.Name, item.Quantity, ingredient.Unit, lineCost));
        }

        return new DishSizeDto(size.Id, size.Name, size.SalePrice, size.IsActive, totalCost, size.SalePrice - totalCost, recipeDtos);
    }
}
