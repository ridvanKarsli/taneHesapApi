namespace TaneHesap.Application.Ingredients;

public record IngredientDto(
    Guid Id,
    string Name,
    string Unit,
    decimal CurrentUnitPrice,
    decimal MinimumStockThreshold,
    decimal CurrentStockQuantity,
    bool IsActive);

public record CreateIngredientRequest(string Name, string Unit, decimal CurrentUnitPrice, decimal MinimumStockThreshold);

public record UpdateIngredientRequest(
    string Name, string Unit, decimal CurrentUnitPrice, decimal MinimumStockThreshold, bool IsActive);
