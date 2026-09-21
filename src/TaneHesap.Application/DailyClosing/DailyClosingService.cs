using System.Globalization;
using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.DailySales;
using TaneHesap.Application.Notifications;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.DailyClosing;

public class DailyClosingService : IDailyClosingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDailySalesService _dailySalesService;
    private readonly INotificationService _notificationService;

    public DailyClosingService(IUnitOfWork unitOfWork, IDailySalesService dailySalesService, INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _dailySalesService = dailySalesService;
        _notificationService = notificationService;
    }

    public async Task<DailyLossReportDto> SubmitActualEntryAsync(Guid businessId, SubmitDailyActualEntryRequest request, Guid enteredByUserId, CancellationToken ct = default)
    {
        var ingredientRepo = _unitOfWork.Repository<Ingredient>();
        var ingredients = await ingredientRepo.ListAsync(i => i.BusinessId == businessId, ct);
        var ingredientsById = ingredients.ToDictionary(i => i.Id);

        foreach (var item in request.ConsumptionItems)
        {
            if (!ingredientsById.ContainsKey(item.IngredientId))
            {
                throw new NotFoundException(nameof(Ingredient), item.IngredientId);
            }
        }

        var entryRepo = _unitOfWork.Repository<DailyActualEntry>();
        var existingEntries = await entryRepo.ListAsync(e => e.BusinessId == businessId && e.EntryDate == request.EntryDate, ct);
        var entry = existingEntries.FirstOrDefault();

        var stockMovementRepo = _unitOfWork.Repository<StockMovement>();

        if (entry is not null)
        {
            // Yeniden gönderim: önce bu girişe bağlı önceki stok düşümlerini geri al.
            var previousMovements = await stockMovementRepo
                .ListAsync(m => m.SourceReferenceType == nameof(DailyActualEntry) && m.SourceReferenceId == entry.Id, ct);

            foreach (var movement in previousMovements)
            {
                if (ingredientsById.TryGetValue(movement.IngredientId, out var ing))
                {
                    ing.CurrentStockQuantity -= movement.QuantityChange; // QuantityChange negatifti, çıkarınca geri eklenir.
                    ingredientRepo.Update(ing);
                }

                stockMovementRepo.Remove(movement);
            }

            var itemRepo = _unitOfWork.Repository<DailyActualConsumptionItem>();
            var previousItems = await itemRepo.ListAsync(i => i.DailyActualEntryId == entry.Id, ct);
            foreach (var item in previousItems)
            {
                itemRepo.Remove(item);
            }

            entry.ActualRevenue = request.ActualRevenue;
            entry.Note = request.Note;
            entry.UpdatedByUserId = enteredByUserId;
            entry.UpdatedAtUtc = DateTime.UtcNow;
            entryRepo.Update(entry);
        }
        else
        {
            entry = new DailyActualEntry
            {
                BusinessId = businessId,
                EntryDate = request.EntryDate,
                ActualRevenue = request.ActualRevenue,
                EnteredByUserId = enteredByUserId,
                Note = request.Note,
                CreatedByUserId = enteredByUserId
            };
            await entryRepo.AddAsync(entry, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        var consumptionItemRepo = _unitOfWork.Repository<DailyActualConsumptionItem>();
        foreach (var item in request.ConsumptionItems)
        {
            await consumptionItemRepo.AddAsync(new DailyActualConsumptionItem
            {
                DailyActualEntryId = entry.Id,
                IngredientId = item.IngredientId,
                ActualQuantityUsed = item.ActualQuantityUsed,
                CreatedByUserId = enteredByUserId
            }, ct);

            var ingredient = ingredientsById[item.IngredientId];
            await stockMovementRepo.AddAsync(new StockMovement
            {
                BusinessId = businessId,
                IngredientId = item.IngredientId,
                QuantityChange = -item.ActualQuantityUsed,
                MovementType = StockMovementType.SaleConsumption,
                MovementDateUtc = DateTime.UtcNow,
                SourceReferenceType = nameof(DailyActualEntry),
                SourceReferenceId = entry.Id,
                CreatedByUserId = enteredByUserId
            }, ct);

            ingredient.CurrentStockQuantity -= item.ActualQuantityUsed;
            ingredient.UpdatedByUserId = enteredByUserId;
            ingredient.UpdatedAtUtc = DateTime.UtcNow;
            ingredientRepo.Update(ingredient);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        foreach (var item in request.ConsumptionItems)
        {
            var ingredient = ingredientsById[item.IngredientId];
            if (ingredient.IsActive && ingredient.CurrentStockQuantity <= ingredient.MinimumStockThreshold)
            {
                await _notificationService.NotifyLowStockAsync(
                    businessId, ingredient.Name, ingredient.CurrentStockQuantity, ingredient.MinimumStockThreshold, ct);
            }
        }

        var report = await GenerateLossReportAsync(businessId, request.EntryDate, entry, ct);
        await WarnIfLossAsync(businessId, report, ct);
        return report;
    }

    /// <summary>
    /// Gün sonu raporunda gelir açığı veya fazladan malzeme tüketimi (fire/kayıp) varsa ADMIN'lere
    /// in-app uyarı gönderir (bkz. Proje Raporu bölüm 3.10, 3.13 — "gün sonu fire/açık uyarısı").
    /// </summary>
    private async Task WarnIfLossAsync(Guid businessId, DailyLossReportDto report, CancellationToken ct)
    {
        var revenueShortfall = report.RevenueVarianceAmount < 0 ? -report.RevenueVarianceAmount : 0;
        var excessMaterialCost = report.Items.Where(i => i.VarianceCost > 0).Sum(i => i.VarianceCost);
        if (revenueShortfall == 0 && excessMaterialCost == 0)
        {
            return;
        }

        var tr = CultureInfo.GetCultureInfo("tr-TR");
        var findings = new List<string>();
        if (revenueShortfall > 0)
        {
            findings.Add($"gelir beklenenden {revenueShortfall.ToString("N2", tr)} ₺ eksik");
        }
        if (excessMaterialCost > 0)
        {
            findings.Add($"malzemede {excessMaterialCost.ToString("N2", tr)} ₺ tutarında fazla tüketim (fire/kayıp)");
        }

        await _notificationService.NotifyAdminsAsync(
            businessId,
            NotificationType.DailyLossWarning,
            $"{report.ReportDate.ToString("dd.MM.yyyy", tr)} gün sonu: {string.Join("; ", findings)}.",
            ct);
    }

    public async Task<DailyActualEntryDto?> GetActualEntryByDateAsync(Guid businessId, DateOnly date, CancellationToken ct = default)
    {
        var entries = await _unitOfWork.Repository<DailyActualEntry>()
            .ListAsync(e => e.BusinessId == businessId && e.EntryDate == date, ct);
        var entry = entries.FirstOrDefault();
        if (entry is null)
        {
            return null;
        }

        return await BuildActualEntryDtoAsync(businessId, entry, ct);
    }

    public async Task<DailyLossReportDto?> GetLossReportByDateAsync(Guid businessId, DateOnly date, CancellationToken ct = default)
    {
        var reports = await _unitOfWork.Repository<DailyLossReport>()
            .ListAsync(r => r.BusinessId == businessId && r.ReportDate == date, ct);
        var report = reports.FirstOrDefault();
        return report is null ? null : await BuildLossReportDtoAsync(report, ct);
    }

    public async Task<List<DailyLossReportDto>> GetLossReportsAsync(Guid businessId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var reports = await _unitOfWork.Repository<DailyLossReport>()
            .ListAsync(r => r.BusinessId == businessId && r.ReportDate >= fromDate && r.ReportDate <= toDate, ct);

        var result = new List<DailyLossReportDto>();
        foreach (var report in reports.OrderBy(r => r.ReportDate))
        {
            result.Add(await BuildLossReportDtoAsync(report, ct));
        }

        return result;
    }

    private async Task<DailyLossReportDto> GenerateLossReportAsync(Guid businessId, DateOnly date, DailyActualEntry entry, CancellationToken ct)
    {
        var expected = await _dailySalesService.GetExpectedSummaryAsync(businessId, date, ct);

        var actualItemRepo = _unitOfWork.Repository<DailyActualConsumptionItem>();
        var actualItems = await actualItemRepo.ListAsync(i => i.DailyActualEntryId == entry.Id, ct);
        var actualByIngredient = actualItems.ToDictionary(i => i.IngredientId, i => i.ActualQuantityUsed);

        var ingredients = await _unitOfWork.Repository<Ingredient>().ListAsync(i => i.BusinessId == businessId, ct);
        var ingredientsById = ingredients.ToDictionary(i => i.Id);

        var allIngredientIds = expected.ExpectedConsumption.Select(e => e.IngredientId)
            .Union(actualByIngredient.Keys)
            .Distinct();

        var reportRepo = _unitOfWork.Repository<DailyLossReport>();
        var existingReports = await reportRepo.ListAsync(r => r.BusinessId == businessId && r.ReportDate == date, ct);
        var report = existingReports.FirstOrDefault();

        var itemRepo = _unitOfWork.Repository<DailyLossReportItem>();

        if (report is not null)
        {
            var oldItems = await itemRepo.ListAsync(i => i.DailyLossReportId == report.Id, ct);
            foreach (var oldItem in oldItems)
            {
                itemRepo.Remove(oldItem);
            }

            report.ExpectedRevenue = expected.ExpectedRevenue;
            report.ActualRevenue = entry.ActualRevenue;
            report.RevenueVarianceAmount = entry.ActualRevenue - expected.ExpectedRevenue;
            report.GeneratedAtUtc = DateTime.UtcNow;
            report.UpdatedByUserId = entry.EnteredByUserId;
            report.UpdatedAtUtc = DateTime.UtcNow;
            reportRepo.Update(report);
        }
        else
        {
            report = new DailyLossReport
            {
                BusinessId = businessId,
                ReportDate = date,
                ExpectedRevenue = expected.ExpectedRevenue,
                ActualRevenue = entry.ActualRevenue,
                RevenueVarianceAmount = entry.ActualRevenue - expected.ExpectedRevenue,
                GeneratedAtUtc = DateTime.UtcNow,
                CreatedByUserId = entry.EnteredByUserId
            };
            await reportRepo.AddAsync(report, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        var expectedByIngredient = expected.ExpectedConsumption.ToDictionary(e => e.IngredientId, e => e.ExpectedQuantity);

        foreach (var ingredientId in allIngredientIds)
        {
            var expectedQty = expectedByIngredient.GetValueOrDefault(ingredientId);
            var actualQty = actualByIngredient.GetValueOrDefault(ingredientId);
            var varianceQty = actualQty - expectedQty;
            var unitPrice = ingredientsById.TryGetValue(ingredientId, out var ing) ? ing.CurrentUnitPrice : 0;

            await itemRepo.AddAsync(new DailyLossReportItem
            {
                DailyLossReportId = report.Id,
                IngredientId = ingredientId,
                ExpectedQuantity = expectedQty,
                ActualQuantity = actualQty,
                VarianceQuantity = varianceQty,
                VarianceCost = varianceQty * unitPrice,
                CreatedByUserId = entry.EnteredByUserId
            }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return await BuildLossReportDtoAsync(report, ct);
    }

    private async Task<DailyActualEntryDto> BuildActualEntryDtoAsync(Guid businessId, DailyActualEntry entry, CancellationToken ct)
    {
        var items = await _unitOfWork.Repository<DailyActualConsumptionItem>().ListAsync(i => i.DailyActualEntryId == entry.Id, ct);
        var ingredients = await _unitOfWork.Repository<Ingredient>().ListAsync(i => i.BusinessId == businessId, ct);
        var ingredientsById = ingredients.ToDictionary(i => i.Id);

        var itemDtos = items.Select(i => ingredientsById.TryGetValue(i.IngredientId, out var ing)
                ? new DailyActualConsumptionItemDto(i.IngredientId, ing.Name, ing.Unit, i.ActualQuantityUsed)
                : new DailyActualConsumptionItemDto(i.IngredientId, "-", "-", i.ActualQuantityUsed))
            .ToList();

        return new DailyActualEntryDto(entry.Id, entry.EntryDate, entry.ActualRevenue, entry.Note, itemDtos);
    }

    private async Task<DailyLossReportDto> BuildLossReportDtoAsync(DailyLossReport report, CancellationToken ct)
    {
        var items = await _unitOfWork.Repository<DailyLossReportItem>().ListAsync(i => i.DailyLossReportId == report.Id, ct);
        var ingredients = await _unitOfWork.Repository<Ingredient>().ListAsync(i => i.BusinessId == report.BusinessId, ct);
        var ingredientsById = ingredients.ToDictionary(i => i.Id);

        var itemDtos = items
            .Select(i => ingredientsById.TryGetValue(i.IngredientId, out var ing)
                ? new DailyLossReportItemDto(i.IngredientId, ing.Name, ing.Unit, i.ExpectedQuantity, i.ActualQuantity, i.VarianceQuantity, i.VarianceCost)
                : new DailyLossReportItemDto(i.IngredientId, "-", "-", i.ExpectedQuantity, i.ActualQuantity, i.VarianceQuantity, i.VarianceCost))
            .OrderBy(i => i.IngredientName)
            .ToList();

        return new DailyLossReportDto(
            report.Id, report.ReportDate, report.ExpectedRevenue, report.ActualRevenue,
            report.RevenueVarianceAmount, report.GeneratedAtUtc, itemDtos);
    }
}
