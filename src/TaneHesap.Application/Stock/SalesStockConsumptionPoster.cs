using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.DailySales;
using TaneHesap.Application.Notifications;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Stock;

/// <summary>
/// Satılan her ürünü reçetesine göre stoktan otomatik düşer (bkz. Proje Raporu bölüm 3.9, 3.10 — güncellenen
/// kural). Gün bazında idempotenttir: o günün önceki otomatik düşümleri geri alınır, güncel satışlara göre
/// yeniden yazılır. Gün sonu kapanışında ADMIN'in girdiği gerçek tüketim ile bu beklenen tüketim arasındaki
/// FARK ayrıca fire/düzeltme hareketi olarak işlenir (DailyClosingService) — toplam düşüm gerçek tüketime eşitlenir.
/// </summary>
public class SalesStockConsumptionPoster : IDailySalesSideEffect
{
    public const string SourceReferenceType = "DailySalesConsumption";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IExpectedConsumptionCalculator _consumptionCalculator;
    private readonly INotificationService _notificationService;

    public SalesStockConsumptionPoster(IUnitOfWork unitOfWork, IExpectedConsumptionCalculator consumptionCalculator, INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _consumptionCalculator = consumptionCalculator;
        _notificationService = notificationService;
    }

    public async Task ApplyAsync(Guid businessId, IReadOnlyCollection<DateOnly> saleDates, Guid userId, CancellationToken ct = default)
    {
        if (saleDates.Count == 0)
        {
            return;
        }

        var ingredientRepo = _unitOfWork.Repository<Ingredient>();
        var movementRepo = _unitOfWork.Repository<StockMovement>();
        var ingredientsById = (await ingredientRepo.ListAsync(i => i.BusinessId == businessId, ct)).ToDictionary(i => i.Id);
        var touched = new HashSet<Guid>();

        foreach (var date in saleDates.Distinct())
        {
            foreach (var stale in await movementRepo.ListAsync(m => m.BusinessId == businessId && m.SourceReferenceType == SourceReferenceType && m.SourceDate == date, ct))
            {
                if (ingredientsById.TryGetValue(stale.IngredientId, out var ing))
                {
                    ing.CurrentStockQuantity -= stale.QuantityChange; // düşüm negatifti; geri eklenir
                    touched.Add(ing.Id);
                }

                movementRepo.Remove(stale);
            }

            foreach (var (ingredientId, quantity) in await _consumptionCalculator.CalculateAsync(businessId, date, ct))
            {
                if (quantity <= 0 || !ingredientsById.TryGetValue(ingredientId, out var ingredient))
                {
                    continue;
                }

                await movementRepo.AddAsync(new StockMovement
                {
                    BusinessId = businessId,
                    IngredientId = ingredientId,
                    QuantityChange = -quantity,
                    MovementType = StockMovementType.SaleConsumption,
                    MovementDateUtc = DateTime.UtcNow,
                    SourceReferenceType = SourceReferenceType,
                    SourceDate = date,
                    Note = $"{date:dd.MM.yyyy} satışlarından reçeteye göre otomatik düşüm",
                    CreatedByUserId = userId
                }, ct);

                ingredient.CurrentStockQuantity -= quantity;
                touched.Add(ingredient.Id);
            }
        }

        foreach (var id in touched)
        {
            var ingredient = ingredientsById[id];
            ingredient.UpdatedByUserId = userId;
            ingredient.UpdatedAtUtc = DateTime.UtcNow;
            ingredientRepo.Update(ingredient);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        foreach (var id in touched)
        {
            var ingredient = ingredientsById[id];
            if (ingredient.IsActive && ingredient.CurrentStockQuantity <= ingredient.MinimumStockThreshold)
            {
                await _notificationService.NotifyLowStockAsync(businessId, ingredient.Name, ingredient.CurrentStockQuantity, ingredient.MinimumStockThreshold, ct);
            }
        }
    }
}
