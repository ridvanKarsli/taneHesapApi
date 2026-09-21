using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.Platforms;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.DailySales;

public class DailySalesService : IDailySalesService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPlatformCommissionExpensePoster _commissionExpensePoster;

    public DailySalesService(IUnitOfWork unitOfWork, IPlatformCommissionExpensePoster commissionExpensePoster)
    {
        _unitOfWork = unitOfWork;
        _commissionExpensePoster = commissionExpensePoster;
    }

    public async Task<ImportDailySalesResult> ImportAsync(Guid businessId, ImportDailySalesRequest request, Guid importedByUserId, CancellationToken ct = default)
    {
        var dishSizes = await _unitOfWork.Repository<DishSize>().ListAsync(s => s.BusinessId == businessId, ct);
        var dishSizesById = dishSizes.ToDictionary(s => s.Id);

        var platforms = await _unitOfWork.Repository<Platform>().ListAsync(p => p.BusinessId == businessId, ct);
        var platformsById = platforms.ToDictionary(p => p.Id);

        var errors = new List<string>();
        var validEntries = new List<DailySalesEntry>();

        for (var i = 0; i < request.Rows.Count; i++)
        {
            var row = request.Rows[i];
            var rowNo = i + 1;

            if (!dishSizesById.ContainsKey(row.DishSizeId))
            {
                errors.Add($"Satır {rowNo}: tabak boyu bulunamadı ({row.DishSizeId}).");
                continue;
            }

            if (row.Channel == SalesChannel.Platform)
            {
                if (row.PlatformId is null || !platformsById.ContainsKey(row.PlatformId.Value))
                {
                    errors.Add($"Satır {rowNo}: kanal 'Platform' ama geçerli bir platform belirtilmemiş.");
                    continue;
                }
            }

            if (row.Quantity <= 0)
            {
                errors.Add($"Satır {rowNo}: adet 0'dan büyük olmalı.");
                continue;
            }

            validEntries.Add(new DailySalesEntry
            {
                BusinessId = businessId,
                SaleDate = row.SaleDate,
                SaleTime = row.SaleTime,
                DishSizeId = row.DishSizeId,
                Quantity = row.Quantity,
                TotalAmount = row.TotalAmount,
                PaymentMethod = row.PaymentMethod,
                Channel = row.Channel,
                PlatformId = row.Channel == SalesChannel.Platform ? row.PlatformId : null,
                DiscountAmount = row.DiscountAmount,
                CreatedByUserId = importedByUserId
            });
        }

        var importLog = new ExcelImportLog
        {
            BusinessId = businessId,
            FileName = request.FileName,
            ImportedByUserId = importedByUserId,
            ImportedAtUtc = DateTime.UtcNow,
            RowCount = request.Rows.Count,
            ErrorCount = errors.Count,
            ErrorsJson = errors.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(errors) : null,
            CreatedByUserId = importedByUserId
        };

        await _unitOfWork.Repository<ExcelImportLog>().AddAsync(importLog, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        foreach (var entry in validEntries)
        {
            entry.ExcelImportLogId = importLog.Id;
            await _unitOfWork.Repository<DailySalesEntry>().AddAsync(entry, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // Paket servis (Platform) kanalından gelen satırların komisyonunu otomatik olarak Expense
        // kaydına dönüştür (bkz. Proje Raporu bölüm 3.4). Ayrı bir servise devredilir; bu metot
        // satış içe aktarımından, IPlatformCommissionExpensePoster komisyon/gider dönüşümünden
        // sorumludur (Single Responsibility).
        var affectedDates = validEntries
            .Where(e => e.Channel == SalesChannel.Platform)
            .Select(e => e.SaleDate)
            .Distinct();

        await _commissionExpensePoster.PostCommissionExpensesAsync(businessId, affectedDates, importedByUserId, ct);

        return new ImportDailySalesResult(importLog.Id, request.Rows.Count, validEntries.Count, errors.Count, errors);
    }

    public async Task DeleteEntryAsync(Guid businessId, Guid entryId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<DailySalesEntry>();
        var entry = await repo.GetByIdAsync(entryId, ct);
        if (entry is null || entry.BusinessId != businessId)
        {
            throw new NotFoundException(nameof(DailySalesEntry), entryId);
        }

        repo.Remove(entry);
        await _unitOfWork.SaveChangesAsync(ct);
        await _commissionExpensePoster.PostCommissionExpensesAsync(businessId, new[] { entry.SaleDate }, deletedByUserId, ct);
    }

    public async Task<int> DeleteByDateAsync(Guid businessId, DateOnly date, Guid deletedByUserId, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<DailySalesEntry>();
        var entries = await repo.ListAsync(e => e.BusinessId == businessId && e.SaleDate == date, ct);
        foreach (var entry in entries)
        {
            repo.Remove(entry);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        await _commissionExpensePoster.PostCommissionExpensesAsync(businessId, new[] { date }, deletedByUserId, ct);
        return entries.Count;
    }

    public async Task<List<DailySalesEntryDto>> GetByDateAsync(Guid businessId, DateOnly date, CancellationToken ct = default)
    {
        var entries = await _unitOfWork.Repository<DailySalesEntry>()
            .ListAsync(e => e.BusinessId == businessId && e.SaleDate == date, ct);

        var dishSizes = await _unitOfWork.Repository<DishSize>().ListAsync(s => s.BusinessId == businessId, ct);
        var dishSizesById = dishSizes.ToDictionary(s => s.Id);

        var dishes = await _unitOfWork.Repository<Dish>().ListAsync(d => d.BusinessId == businessId, ct);
        var dishesById = dishes.ToDictionary(d => d.Id);

        var platforms = await _unitOfWork.Repository<Platform>().ListAsync(p => p.BusinessId == businessId, ct);
        var platformsById = platforms.ToDictionary(p => p.Id);

        return entries
            .OrderBy(e => e.SaleTime)
            .Select(e =>
            {
                dishSizesById.TryGetValue(e.DishSizeId, out var size);
                var dishName = size is not null && dishesById.TryGetValue(size.DishId, out var dish) ? dish.Name : "-";
                string? platformName = e.PlatformId.HasValue && platformsById.TryGetValue(e.PlatformId.Value, out var platform) ? platform.Name : null;

                return new DailySalesEntryDto(
                    e.Id, e.SaleDate, e.SaleTime, e.DishSizeId, dishName, size?.Name ?? "-",
                    e.Quantity, e.TotalAmount, e.PaymentMethod, e.Channel, e.PlatformId, platformName, e.DiscountAmount);
            })
            .ToList();
    }

    public async Task<ExpectedDaySummaryDto> GetExpectedSummaryAsync(Guid businessId, DateOnly date, CancellationToken ct = default)
    {
        var entries = await _unitOfWork.Repository<DailySalesEntry>()
            .ListAsync(e => e.BusinessId == businessId && e.SaleDate == date, ct);

        var expectedRevenue = entries.Sum(e => e.TotalAmount);

        var recipeItems = await _unitOfWork.Repository<DishRecipeItem>().ListAsync(r => r.BusinessId == businessId, ct);
        var recipeItemsByDishSize = recipeItems.GroupBy(r => r.DishSizeId).ToDictionary(g => g.Key, g => g.ToList());

        var ingredients = await _unitOfWork.Repository<Ingredient>().ListAsync(i => i.BusinessId == businessId, ct);
        var ingredientsById = ingredients.ToDictionary(i => i.Id);

        var consumptionByIngredient = new Dictionary<Guid, decimal>();

        foreach (var entry in entries)
        {
            if (!recipeItemsByDishSize.TryGetValue(entry.DishSizeId, out var items))
            {
                continue;
            }

            foreach (var item in items)
            {
                var qty = item.Quantity * entry.Quantity;
                consumptionByIngredient[item.IngredientId] = consumptionByIngredient.GetValueOrDefault(item.IngredientId) + qty;
            }
        }

        var consumptionDtos = consumptionByIngredient
            .Select(kv => ingredientsById.TryGetValue(kv.Key, out var ing)
                ? new ExpectedIngredientConsumptionDto(kv.Key, ing.Name, ing.Unit, kv.Value)
                : new ExpectedIngredientConsumptionDto(kv.Key, "-", "-", kv.Value))
            .OrderBy(d => d.IngredientName)
            .ToList();

        return new ExpectedDaySummaryDto(date, expectedRevenue, consumptionDtos);
    }
}
