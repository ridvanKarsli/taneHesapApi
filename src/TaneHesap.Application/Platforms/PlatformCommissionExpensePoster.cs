using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Platforms;

/// <summary>
/// bkz. IPlatformCommissionExpensePoster. DailySalesService, gün sonu içe aktarımı tamamlandıktan
/// sonra bu servisi çağırır; platform başına otomatik bir ExpenseType (get-or-create) ve
/// tarih+platform başına bir Expense kaydı (idempotent: varsa tutarını günceller) oluşturur.
/// </summary>
public class PlatformCommissionExpensePoster : IPlatformCommissionExpensePoster
{
    private const string ExpenseTypeNamePrefix = "Platform Komisyonu";
    private const string ExpenseTypeUnit = "TL";

    private readonly IUnitOfWork _unitOfWork;

    public PlatformCommissionExpensePoster(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task PostCommissionExpensesAsync(Guid businessId, IEnumerable<DateOnly> saleDates, Guid postedByUserId, CancellationToken ct = default)
    {
        var distinctDates = saleDates.Distinct().ToList();
        if (distinctDates.Count == 0)
        {
            return;
        }

        var platforms = await _unitOfWork.Repository<Platform>().ListAsync(p => p.BusinessId == businessId, ct);
        if (platforms.Count == 0)
        {
            return;
        }

        var platformSales = await _unitOfWork.Repository<DailySalesEntry>()
            .ListAsync(e => e.BusinessId == businessId
                && e.Channel == SalesChannel.Platform
                && e.PlatformId != null
                && distinctDates.Contains(e.SaleDate), ct);

        if (platformSales.Count == 0)
        {
            return;
        }

        var grossByPlatformAndDate = platformSales
            .GroupBy(e => (PlatformId: e.PlatformId!.Value, e.SaleDate))
            .ToDictionary(g => g.Key, g => g.Sum(e => e.TotalAmount));

        var expenseTypes = await _unitOfWork.Repository<ExpenseType>().ListAsync(t => t.BusinessId == businessId, ct);

        foreach (var platform in platforms)
        {
            var expenseType = await GetOrCreateCommissionExpenseTypeAsync(businessId, platform, expenseTypes, postedByUserId, ct);

            foreach (var date in distinctDates)
            {
                if (!grossByPlatformAndDate.TryGetValue((platform.Id, date), out var grossAmount))
                {
                    continue;
                }

                await UpsertCommissionExpenseAsync(businessId, expenseType, platform, date, grossAmount, postedByUserId, ct);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<ExpenseType> GetOrCreateCommissionExpenseTypeAsync(Guid businessId, Platform platform, List<ExpenseType> expenseTypes, Guid postedByUserId, CancellationToken ct)
    {
        var expenseTypeName = $"{ExpenseTypeNamePrefix} - {platform.Name}";
        var expenseType = expenseTypes.FirstOrDefault(t => t.Name == expenseTypeName);

        if (expenseType is not null)
        {
            return expenseType;
        }

        expenseType = new ExpenseType
        {
            BusinessId = businessId,
            Name = expenseTypeName,
            Unit = ExpenseTypeUnit,
            Category = ExpenseCategory.Other,
            CreatedByUserId = postedByUserId
        };

        await _unitOfWork.Repository<ExpenseType>().AddAsync(expenseType, ct);
        expenseTypes.Add(expenseType);

        return expenseType;
    }

    private async Task UpsertCommissionExpenseAsync(Guid businessId, ExpenseType expenseType, Platform platform, DateOnly date, decimal grossAmount, Guid postedByUserId, CancellationToken ct)
    {
        var commissionAmount = Math.Round(grossAmount * platform.CommissionPercentage / 100m, 2);

        var existingExpense = (await _unitOfWork.Repository<Expense>()
                .ListAsync(e => e.BusinessId == businessId && e.ExpenseTypeId == expenseType.Id && e.ExpenseDate == date, ct))
            .FirstOrDefault();

        if (existingExpense is not null)
        {
            existingExpense.Amount = commissionAmount;
            existingExpense.UpdatedByUserId = postedByUserId;
            existingExpense.UpdatedAtUtc = DateTime.UtcNow;
            _unitOfWork.Repository<Expense>().Update(existingExpense);
            return;
        }

        if (commissionAmount <= 0)
        {
            return;
        }

        await _unitOfWork.Repository<Expense>().AddAsync(new Expense
        {
            BusinessId = businessId,
            ExpenseTypeId = expenseType.Id,
            ExpenseType = expenseType,
            Amount = commissionAmount,
            ExpenseDate = date,
            Description = $"{platform.Name} platform komisyonu (otomatik, gün sonu içe aktarımından hesaplandı).",
            CreatedByUserId = postedByUserId
        }, ct);
    }
}
