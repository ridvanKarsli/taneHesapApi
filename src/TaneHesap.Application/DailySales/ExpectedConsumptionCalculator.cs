using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;

namespace TaneHesap.Application.DailySales;

public class ExpectedConsumptionCalculator : IExpectedConsumptionCalculator
{
    private readonly IUnitOfWork _unitOfWork;

    public ExpectedConsumptionCalculator(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Dictionary<Guid, decimal>> CalculateAsync(Guid businessId, DateOnly date, CancellationToken ct = default)
    {
        var entries = await _unitOfWork.Repository<DailySalesEntry>()
            .ListAsync(e => e.BusinessId == businessId && e.SaleDate == date, ct);
        var result = new Dictionary<Guid, decimal>();
        if (entries.Count == 0)
        {
            return result;
        }

        var recipeItemsByDishSize = (await _unitOfWork.Repository<DishRecipeItem>().ListAsync(r => r.BusinessId == businessId, ct))
            .GroupBy(r => r.DishSizeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var entry in entries)
        {
            if (!recipeItemsByDishSize.TryGetValue(entry.DishSizeId, out var items))
            {
                continue;
            }

            foreach (var item in items)
            {
                result[item.IngredientId] = result.GetValueOrDefault(item.IngredientId) + item.Quantity * entry.Quantity;
            }
        }

        return result;
    }
}
