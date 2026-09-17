namespace TaneHesap.Application.Dishes;

/// <summary>
/// Ürün (Dish), tabak boyu (DishSize) ve reçete (DishRecipeItem) yönetimi — ADMIN yönetir.
/// Tabak maliyeti, reçetedeki her malzemenin miktarı × Ingredient.CurrentUnitPrice toplamı olarak
/// otomatik hesaplanır. bkz. Proje Raporu bölüm 3.3, 3.11.
/// </summary>
public interface IDishService
{
    Task<List<DishDto>> GetAllAsync(Guid businessId, CancellationToken ct = default);

    Task<DishDto> GetByIdAsync(Guid businessId, Guid dishId, CancellationToken ct = default);

    Task<DishDto> CreateDishAsync(Guid businessId, CreateDishRequest request, Guid createdByUserId, CancellationToken ct = default);

    Task<DishSizeDto> AddSizeAsync(Guid businessId, Guid dishId, CreateDishSizeRequest request, Guid createdByUserId, CancellationToken ct = default);

    Task<DishSizeDto> UpdateSizeAsync(Guid businessId, Guid dishId, Guid sizeId, UpdateDishSizeRequest request, Guid updatedByUserId, CancellationToken ct = default);
}
