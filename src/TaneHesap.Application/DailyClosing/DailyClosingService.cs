using System.Globalization;
using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.DailySales;
using TaneHesap.Application.Notifications;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.DailyClosing;

/// <summary>
/// Gün sonu kapanışı. Satılan ürünler reçeteye göre zaten otomatik düşülür (SalesStockConsumptionPoster);
/// burada ADMIN'in saydığı gerçek tüketim ile beklenen arasındaki FARK fire/düzeltme hareketi olarak işlenir,
/// gerçek gelir ile beklenen gelir farkı nakit kasasına sayım farkı olarak yazılır ve fire raporu üretilir.
/// Tüm türetilmiş kayıtlar gün bazında idempotenttir: <see cref="RecalculateAsync"/> aynı günün satışı
/// sonradan değişse bile (DailyClosingRecalculationSideEffect) tutarlılığı korur. bkz. Proje Raporu bölüm 3.10.
/// </summary>
public class DailyClosingService : IDailyClosingService
{
    public const string VarianceSourceType = "DailyClosingVariance";

    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private readonly IUnitOfWork _unitOfWork;
    private readonly IExpectedConsumptionCalculator _consumptionCalculator;
    private readonly INotificationService _notificationService;

    public DailyClosingService(IUnitOfWork unitOfWork, IExpectedConsumptionCalculator consumptionCalculator, INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _consumptionCalculator = consumptionCalculator;
        _notificationService = notificationService;
    }

    public async Task<DailyLossReportDto> SubmitActualEntryAsync(Guid businessId, SubmitDailyActualEntryRequest request, Guid enteredByUserId, CancellationToken ct = default)
    {
        if (request.ActualRevenue < 0 || request.ConsumptionItems.Any(i => i.ActualQuantityUsed < 0))
        {
            throw new ValidationAppException("Gerçek gelir ve tüketim miktarları negatif olamaz.");
        }

        var ingredientIds = (await _unitOfWork.Repository<Ingredient>().ListAsync(i => i.BusinessId == businessId, ct)).Select(i => i.Id).ToHashSet();
        var missing = request.ConsumptionItems.FirstOrDefault(i => !ingredientIds.Contains(i.IngredientId));
        if (missing is not null)
        {
            throw new NotFoundException(nameof(Ingredient), missing.IngredientId);
        }

        var entry = await UpsertEntryAsync(businessId, request, enteredByUserId, ct);
        var report = await RecalculateEntryAsync(entry, enteredByUserId, ct);
        await WarnIfLossAsync(businessId, report, ct);
        return report;
    }

    public async Task<bool> RecalculateAsync(Guid businessId, DateOnly date, Guid userId, CancellationToken ct = default)
    {
        var entry = (await _unitOfWork.Repository<DailyActualEntry>().ListAsync(e => e.BusinessId == businessId && e.EntryDate == date, ct)).FirstOrDefault();
        if (entry is null)
        {
            return false;
        }

        await RecalculateEntryAsync(entry, userId, ct);
        return true;
    }

    public async Task<DailyActualEntryDto?> GetActualEntryByDateAsync(Guid businessId, DateOnly date, CancellationToken ct = default)
    {
        var entry = (await _unitOfWork.Repository<DailyActualEntry>().ListAsync(e => e.BusinessId == businessId && e.EntryDate == date, ct)).FirstOrDefault();
        return entry is null ? null : await BuildActualEntryDtoAsync(businessId, entry, ct);
    }

    public async Task<DailyLossReportDto?> GetLossReportByDateAsync(Guid businessId, DateOnly date, CancellationToken ct = default)
    {
        var report = (await _unitOfWork.Repository<DailyLossReport>().ListAsync(r => r.BusinessId == businessId && r.ReportDate == date, ct)).FirstOrDefault();
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

    // ---- Giriş kaydı ----

    private async Task<DailyActualEntry> UpsertEntryAsync(Guid businessId, SubmitDailyActualEntryRequest request, Guid userId, CancellationToken ct)
    {
        var entryRepo = _unitOfWork.Repository<DailyActualEntry>();
        var itemRepo = _unitOfWork.Repository<DailyActualConsumptionItem>();
        var entry = (await entryRepo.ListAsync(e => e.BusinessId == businessId && e.EntryDate == request.EntryDate, ct)).FirstOrDefault();

        if (entry is null)
        {
            entry = new DailyActualEntry { BusinessId = businessId, EntryDate = request.EntryDate, EnteredByUserId = userId, CreatedByUserId = userId };
            await entryRepo.AddAsync(entry, ct);
        }
        else
        {
            foreach (var old in await itemRepo.ListAsync(i => i.DailyActualEntryId == entry.Id, ct))
            {
                itemRepo.Remove(old);
            }

            entry.UpdatedByUserId = userId;
            entry.UpdatedAtUtc = DateTime.UtcNow;
            entryRepo.Update(entry);
        }

        entry.ActualRevenue = request.ActualRevenue;
        entry.Note = request.Note;
        await _unitOfWork.SaveChangesAsync(ct);

        foreach (var item in request.ConsumptionItems.GroupBy(i => i.IngredientId))
        {
            await itemRepo.AddAsync(new DailyActualConsumptionItem
            {
                DailyActualEntryId = entry.Id,
                IngredientId = item.Key,
                ActualQuantityUsed = item.Sum(i => i.ActualQuantityUsed),
                CreatedByUserId = userId
            }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return entry;
    }

    // ---- Türetilmiş kayıtlar (idempotent) ----

    private async Task<DailyLossReportDto> RecalculateEntryAsync(DailyActualEntry entry, Guid userId, CancellationToken ct)
    {
        var expectedConsumption = await _consumptionCalculator.CalculateAsync(entry.BusinessId, entry.EntryDate, ct);
        var expectedRevenue = (await _unitOfWork.Repository<DailySalesEntry>()
            .ListAsync(s => s.BusinessId == entry.BusinessId && s.SaleDate == entry.EntryDate, ct)).Sum(s => s.TotalAmount);
        var actualItems = await _unitOfWork.Repository<DailyActualConsumptionItem>().ListAsync(i => i.DailyActualEntryId == entry.Id, ct);
        var ingredientsById = (await _unitOfWork.Repository<Ingredient>().ListAsync(i => i.BusinessId == entry.BusinessId, ct)).ToDictionary(i => i.Id);

        await ApplyStockVarianceAsync(entry, actualItems, expectedConsumption, ingredientsById, userId, ct);
        await ApplyRevenueVarianceAsync(entry, expectedRevenue, userId, ct);
        var report = await UpsertLossReportAsync(entry, expectedRevenue, actualItems, expectedConsumption, ingredientsById, userId, ct);

        await NotifyLowStockAsync(entry.BusinessId, actualItems.Select(i => i.IngredientId), ingredientsById, ct);
        return await BuildLossReportDtoAsync(report, ct);
    }

    /// <summary>Gerçek − beklenen tüketim farkını stoktan düşer/geri ekler; önceki fark hareketleri geri alınır.</summary>
    private async Task ApplyStockVarianceAsync(DailyActualEntry entry, List<DailyActualConsumptionItem> actualItems,
        Dictionary<Guid, decimal> expected, Dictionary<Guid, Ingredient> ingredientsById, Guid userId, CancellationToken ct)
    {
        var movementRepo = _unitOfWork.Repository<StockMovement>();
        var ingredientRepo = _unitOfWork.Repository<Ingredient>();

        foreach (var stale in await movementRepo.ListAsync(m => m.SourceReferenceType == nameof(DailyActualEntry) && m.SourceReferenceId == entry.Id, ct))
        {
            if (ingredientsById.TryGetValue(stale.IngredientId, out var ing))
            {
                ing.CurrentStockQuantity -= stale.QuantityChange;
                ingredientRepo.Update(ing);
            }

            movementRepo.Remove(stale);
        }

        foreach (var item in actualItems)
        {
            var variance = item.ActualQuantityUsed - expected.GetValueOrDefault(item.IngredientId);
            if (variance == 0 || !ingredientsById.TryGetValue(item.IngredientId, out var ingredient))
            {
                continue;
            }

            await movementRepo.AddAsync(new StockMovement
            {
                BusinessId = entry.BusinessId,
                IngredientId = item.IngredientId,
                QuantityChange = -variance,
                MovementType = variance > 0 ? StockMovementType.Waste : StockMovementType.ManualAdjustment,
                MovementDateUtc = DateTime.UtcNow,
                SourceReferenceType = nameof(DailyActualEntry),
                SourceReferenceId = entry.Id,
                SourceDate = entry.EntryDate,
                Note = variance > 0
                    ? $"Gün sonu sayımı: beklenenden {variance} {ingredient.Unit} fazla tüketim (fire/kayıp)"
                    : $"Gün sonu sayımı: beklenenden {-variance} {ingredient.Unit} az tüketim (düzeltme)",
                CreatedByUserId = userId
            }, ct);

            ingredient.CurrentStockQuantity -= variance;
            ingredient.UpdatedByUserId = userId;
            ingredient.UpdatedAtUtc = DateTime.UtcNow;
            ingredientRepo.Update(ingredient);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Satış geliri kasaya satış satırlarından yazılır; sayımda gerçek gelir farklıysa fark nakit kasasına
    /// "sayım farkı" olarak işlenir — kasa gerçeği göstersin, açık raporda görünsün (gün başına tek kayıt).
    /// </summary>
    private async Task ApplyRevenueVarianceAsync(DailyActualEntry entry, decimal expectedRevenue, Guid userId, CancellationToken ct)
    {
        var repo = _unitOfWork.Repository<TreasuryTransaction>();
        foreach (var stale in await repo.ListAsync(t => t.SourceReferenceType == VarianceSourceType && t.SourceReferenceId == entry.Id, ct))
        {
            repo.Remove(stale);
        }

        var variance = entry.ActualRevenue - expectedRevenue;
        if (variance != 0)
        {
            await repo.AddAsync(new TreasuryTransaction
            {
                BusinessId = entry.BusinessId,
                Account = TreasuryAccount.Cash,
                Amount = variance,
                Kind = TreasuryTransactionKind.ManualAdjustment,
                TransactionDate = entry.EntryDate,
                Description = variance < 0 ? "Gün sonu sayımı: kasa açığı" : "Gün sonu sayımı: kasa fazlası",
                SourceReferenceType = VarianceSourceType,
                SourceReferenceId = entry.Id,
                CreatedByUserId = userId
            }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<DailyLossReport> UpsertLossReportAsync(DailyActualEntry entry, decimal expectedRevenue, List<DailyActualConsumptionItem> actualItems,
        Dictionary<Guid, decimal> expected, Dictionary<Guid, Ingredient> ingredientsById, Guid userId, CancellationToken ct)
    {
        var reportRepo = _unitOfWork.Repository<DailyLossReport>();
        var itemRepo = _unitOfWork.Repository<DailyLossReportItem>();
        var report = (await reportRepo.ListAsync(r => r.BusinessId == entry.BusinessId && r.ReportDate == entry.EntryDate, ct)).FirstOrDefault();

        if (report is null)
        {
            report = new DailyLossReport { BusinessId = entry.BusinessId, ReportDate = entry.EntryDate, CreatedByUserId = userId };
            await reportRepo.AddAsync(report, ct);
        }
        else
        {
            foreach (var old in await itemRepo.ListAsync(i => i.DailyLossReportId == report.Id, ct))
            {
                itemRepo.Remove(old);
            }

            report.UpdatedByUserId = userId;
            report.UpdatedAtUtc = DateTime.UtcNow;
            reportRepo.Update(report);
        }

        report.ExpectedRevenue = expectedRevenue;
        report.ActualRevenue = entry.ActualRevenue;
        report.RevenueVarianceAmount = entry.ActualRevenue - expectedRevenue;
        report.GeneratedAtUtc = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);

        // Sayımda girilmeyen malzeme "sayılmadı" demektir, "hiç tüketilmedi" değil: rapor onu beklenen = gerçek sayar
        // (stokta da yalnızca beklenen düşüm kalır). Aksi halde girilmeyen her malzeme sahte bir "tasarruf" gösterirdi.
        var actualByIngredient = actualItems.ToDictionary(i => i.IngredientId, i => i.ActualQuantityUsed);
        foreach (var ingredientId in expected.Keys.Union(actualByIngredient.Keys))
        {
            var expectedQty = expected.GetValueOrDefault(ingredientId);
            var actualQty = actualByIngredient.TryGetValue(ingredientId, out var counted) ? counted : expectedQty;
            var unitPrice = ingredientsById.TryGetValue(ingredientId, out var ing) ? ing.CurrentUnitPrice : 0;
            await itemRepo.AddAsync(new DailyLossReportItem
            {
                DailyLossReportId = report.Id,
                IngredientId = ingredientId,
                ExpectedQuantity = expectedQty,
                ActualQuantity = actualQty,
                VarianceQuantity = actualQty - expectedQty,
                VarianceCost = (actualQty - expectedQty) * unitPrice,
                CreatedByUserId = userId
            }, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return report;
    }

    private async Task NotifyLowStockAsync(Guid businessId, IEnumerable<Guid> ingredientIds, Dictionary<Guid, Ingredient> ingredientsById, CancellationToken ct)
    {
        foreach (var id in ingredientIds.Distinct())
        {
            if (ingredientsById.TryGetValue(id, out var ingredient) && ingredient.IsActive && ingredient.CurrentStockQuantity <= ingredient.MinimumStockThreshold)
            {
                await _notificationService.NotifyLowStockAsync(businessId, ingredient.Name, ingredient.CurrentStockQuantity, ingredient.MinimumStockThreshold, ct);
            }
        }
    }

    /// <summary>Gelir açığı veya fazla malzeme tüketimi varsa ADMIN'lere in-app uyarı (bkz. bölüm 3.10, 3.13).</summary>
    private async Task WarnIfLossAsync(Guid businessId, DailyLossReportDto report, CancellationToken ct)
    {
        var revenueShortfall = report.RevenueVarianceAmount < 0 ? -report.RevenueVarianceAmount : 0;
        var excessMaterialCost = report.Items.Where(i => i.VarianceCost > 0).Sum(i => i.VarianceCost);
        if (revenueShortfall == 0 && excessMaterialCost == 0)
        {
            return;
        }

        var findings = new List<string>();
        if (revenueShortfall > 0) findings.Add($"gelir beklenenden {revenueShortfall.ToString("N2", Tr)} ₺ eksik");
        if (excessMaterialCost > 0) findings.Add($"malzemede {excessMaterialCost.ToString("N2", Tr)} ₺ tutarında fazla tüketim (fire/kayıp)");

        await _notificationService.NotifyAdminsAsync(businessId, NotificationType.DailyLossWarning,
            $"{report.ReportDate.ToString("dd.MM.yyyy", Tr)} gün sonu: {string.Join("; ", findings)}.", ct);
    }

    // ---- DTO ----

    private async Task<DailyActualEntryDto> BuildActualEntryDtoAsync(Guid businessId, DailyActualEntry entry, CancellationToken ct)
    {
        var items = await _unitOfWork.Repository<DailyActualConsumptionItem>().ListAsync(i => i.DailyActualEntryId == entry.Id, ct);
        var ingredientsById = (await _unitOfWork.Repository<Ingredient>().ListAsync(i => i.BusinessId == businessId, ct)).ToDictionary(i => i.Id);

        var itemDtos = items.Select(i => ingredientsById.TryGetValue(i.IngredientId, out var ing)
                ? new DailyActualConsumptionItemDto(i.IngredientId, ing.Name, ing.Unit, i.ActualQuantityUsed)
                : new DailyActualConsumptionItemDto(i.IngredientId, "-", "-", i.ActualQuantityUsed))
            .ToList();

        return new DailyActualEntryDto(entry.Id, entry.EntryDate, entry.ActualRevenue, entry.Note, itemDtos);
    }

    private async Task<DailyLossReportDto> BuildLossReportDtoAsync(DailyLossReport report, CancellationToken ct)
    {
        var items = await _unitOfWork.Repository<DailyLossReportItem>().ListAsync(i => i.DailyLossReportId == report.Id, ct);
        var ingredientsById = (await _unitOfWork.Repository<Ingredient>().ListAsync(i => i.BusinessId == report.BusinessId, ct)).ToDictionary(i => i.Id);

        var itemDtos = items
            .Select(i => ingredientsById.TryGetValue(i.IngredientId, out var ing)
                ? new DailyLossReportItemDto(i.IngredientId, ing.Name, ing.Unit, i.ExpectedQuantity, i.ActualQuantity, i.VarianceQuantity, i.VarianceCost)
                : new DailyLossReportItemDto(i.IngredientId, "-", "-", i.ExpectedQuantity, i.ActualQuantity, i.VarianceQuantity, i.VarianceCost))
            .OrderBy(i => i.IngredientName)
            .ToList();

        return new DailyLossReportDto(report.Id, report.ReportDate, report.ExpectedRevenue, report.ActualRevenue, report.RevenueVarianceAmount, report.GeneratedAtUtc, itemDtos);
    }
}
