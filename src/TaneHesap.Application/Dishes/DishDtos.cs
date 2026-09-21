namespace TaneHesap.Application.Dishes;

public record RecipeItemDto(Guid IngredientId, string IngredientName, decimal Quantity, string Unit, decimal LineCost);

public record DishSizeDto(
    Guid Id,
    string Name,
    decimal SalePrice,
    bool IsActive,
    decimal Cost,
    decimal ProfitMargin,
    List<RecipeItemDto> RecipeItems);

public record DishDto(Guid Id, string Name, string? Description, bool IsActive, List<DishSizeDto> Sizes);

public record CreateDishRequest(string Name, string? Description);

public record UpdateDishRequest(string Name, string? Description, bool IsActive);

public record RecipeItemRequest(Guid IngredientId, decimal Quantity);

public record CreateDishSizeRequest(string Name, decimal SalePrice, List<RecipeItemRequest> RecipeItems);

public record UpdateDishSizeRequest(string Name, decimal SalePrice, bool IsActive, List<RecipeItemRequest> RecipeItems);
