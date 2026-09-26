using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.DailySales;

public class DailySalesService : IDailySalesService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExpectedConsumptionCalculator _consumptionCalculator;
    private readonly IReadOnlyList<IDailySalesSideEffect> _sideEffects;

    public DailySalesService(IUnitOfWork unitOfWork, IExpectedConsumptionCalculator consumptionCalculator, IEnumerable<IDailySalesSideEffect> sideEffects)
    {
        _unitOfWork = unitOfWork;
        _consumptionCalculator = consumptionCalculator;
        _sideEffects = sideEffects.ToList();
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

            if (row.TotalAmount < 0 || row.DiscountAmount is < 0)
            {
                errors.Add($"Satır {rowNo}: tutar ve indirim negatif olamaz.");
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

        await GuardAgainstDoubleImportAsync(businessId, validEntries, request.Mode, ct);

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

        await ApplySideEffectsAsync(businessId, validEntries.Select(e => e.SaleDate), importedByUserId, ct);

        return new ImportDailySalesResult(importLog.Id, request.Rows.Count, validEntries.Count, errors.Count, errors);
    }

    /// <summary>
    /// Aynı dosyanın (ya da aynı günün) ikinci kez yüklenmesi geliri, kasayı ve stok düşümünü iki katına çıkarırdı.
    /// Dosyadaki tarihlerde kayıt varsa ya reddedilir ya da (ReplaceExisting) o günlerin kayıtları önce silinir.
    /// </summary>
    private async Task GuardAgainstDoubleImportAsync(Guid businessId, List<DailySalesEntry> incoming, DailySalesImportMode mode, CancellationToken ct)
    {
        var dates = incoming.Select(e => e.SaleDate).Distinct().ToList();
        if (dates.Count == 0 || mode == DailySalesImportMode.Append)
        {
            return;
        }

        var repo = _unitOfWork.Repository<DailySalesEntry>();
        var existing = await repo.ListAsync(e => e.BusinessId == businessId && dates.Contains(e.SaleDate), ct);
        if (existing.Count == 0)
        {
            return;
        }

        if (mode == DailySalesImportMode.RejectIfExists)
        {
            var listed = string.Join(", ", existing.Select(e => e.SaleDate).Distinct().OrderBy(d => d).Select(d => d.ToString("dd.MM.yyyy")));
            throw new ConflictAppException(
                $"{listed} tarih(ler)i için satış kaydı zaten var. Dosyayı yeniden yüklemek istiyorsanız \"o günlerin mevcut kayıtlarını değiştir\" seçeneğini işaretleyin.");
        }

        foreach (var entry in existing)
        {
            repo.Remove(entry);
        }
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
        await ApplySideEffectsAsync(businessId, new[] { entry.SaleDate }, deletedByUserId, ct);
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
        await ApplySideEffectsAsync(businessId, new[] { date }, deletedByUserId, ct);
        return entries.Count;
    }

    /// <summary>
    /// Satış verisi değişen günler için türetilmiş kayıtları (platform komisyonu gideri, stok düşümü, kasa
    /// geliri) yeniden hesaplatır — bu metot satıştan, her IDailySalesSideEffect kendi kuralından sorumludur.
    /// </summary>
    private async Task ApplySideEffectsAsync(Guid businessId, IEnumerable<DateOnly> dates, Guid userId, CancellationToken ct)
    {
        var distinctDates = dates.Distinct().ToList();
        if (distinctDates.Count == 0)
        {
            return;
        }

        foreach (var sideEffect in _sideEffects)
        {
            await sideEffect.ApplyAsync(businessId, distinctDates, userId, ct);
        }
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

        var ingredients = await _unitOfWork.Repository<Ingredient>().ListAsync(i => i.BusinessId == businessId, ct);
        var ingredientsById = ingredients.ToDictionary(i => i.Id);

        var consumptionByIngredient = await _consumptionCalculator.CalculateAsync(businessId, date, ct);

        var consumptionDtos = consumptionByIngredient
            .Select(kv => ingredientsById.TryGetValue(kv.Key, out var ing)
                ? new ExpectedIngredientConsumptionDto(kv.Key, ing.Name, ing.Unit, kv.Value)
                : new ExpectedIngredientConsumptionDto(kv.Key, "-", "-", kv.Value))
            .OrderBy(d => d.IngredientName)
            .ToList();

        return new ExpectedDaySummaryDto(date, expectedRevenue, consumptionDtos);
    }
}
