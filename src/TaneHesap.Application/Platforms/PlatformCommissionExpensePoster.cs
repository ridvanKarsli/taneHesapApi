using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.DailySales;
using TaneHesap.Application.Treasury;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Platforms;

/// <summary>
/// Paket servis platformu (Yemeksepeti, Getir vb.) satışlarının komisyonunu otomatik bir Expense kaydına
/// dönüştürür (bkz. Proje Raporu bölüm 3.4). Gün sonu satışı değişince <see cref="IDailySalesSideEffect"/>
/// olarak çağrılır; platform başına otomatik bir ExpenseType (get-or-create) ve tarih+platform başına bir
/// Expense kaydı (idempotent: varsa tutarını günceller, satış kalmadıysa siler) oluşturur. Komisyon platform
/// hakedişinden kesildiği için ödeme şekli Bank'tır (kart kasasından düşer — bkz. IExpenseTreasuryPoster).
/// </summary>
public class PlatformCommissionExpensePoster : IDailySalesSideEffect
{
    private const string ExpenseTypeNamePrefix = "Platform Komisyonu";
    private const string ExpenseTypeUnit = "TL";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IExpenseTreasuryPoster _treasuryPoster;

    public PlatformCommissionExpensePoster(IUnitOfWork unitOfWork, IExpenseTreasuryPoster treasuryPoster)
    {
        _unitOfWork = unitOfWork;
        _treasuryPoster = treasuryPoster;
    }

    public async Task ApplyAsync(Guid businessId, IReadOnlyCollection<DateOnly> saleDates, Guid postedByUserId, CancellationToken ct = default)
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

        // Satışlar silindiğinde de çağrılır: o gün satışı kalmayan platformun eski komisyon gideri kaldırılır.
        var platformSales = await _unitOfWork.Repository<DailySalesEntry>()
            .ListAsync(e => e.BusinessId == businessId
                && e.Channel == SalesChannel.Platform
                && e.PlatformId != null
                && distinctDates.Contains(e.SaleDate), ct);

        var grossByPlatformAndDate = platformSales
            .GroupBy(e => (PlatformId: e.PlatformId!.Value, e.SaleDate))
            .ToDictionary(g => g.Key, g => g.Sum(e => e.TotalAmount));

        var expenseTypes = await _unitOfWork.Repository<ExpenseType>().ListAsync(t => t.BusinessId == businessId, ct);

        foreach (var platform in platforms)
        {
            foreach (var date in distinctDates)
            {
                if (grossByPlatformAndDate.TryGetValue((platform.Id, date), out var grossAmount))
                {
                    var expenseType = await GetOrCreateCommissionExpenseTypeAsync(businessId, platform, expenseTypes, postedByUserId, ct);
                    await UpsertCommissionExpenseAsync(businessId, expenseType, platform, date, grossAmount, postedByUserId, ct);
                }
                else
                {
                    await RemoveCommissionExpenseAsync(businessId, platform, date, expenseTypes, ct);
                }
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

    private async Task RemoveCommissionExpenseAsync(Guid businessId, Platform platform, DateOnly date, List<ExpenseType> expenseTypes, CancellationToken ct)
    {
        var expenseType = expenseTypes.FirstOrDefault(t => t.Name == $"{ExpenseTypeNamePrefix} - {platform.Name}");
        if (expenseType is null)
        {
            return;
        }

        var repo = _unitOfWork.Repository<Expense>();
        foreach (var stale in await repo.ListAsync(e => e.BusinessId == businessId && e.ExpenseTypeId == expenseType.Id && e.ExpenseDate == date, ct))
        {
            await _treasuryPoster.RemoveAsync(stale.Id, ct);
            repo.Remove(stale);
        }
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
            existingExpense.PaymentMethod = PaymentMethod.Bank;
            existingExpense.UpdatedByUserId = postedByUserId;
            existingExpense.UpdatedAtUtc = DateTime.UtcNow;
            _unitOfWork.Repository<Expense>().Update(existingExpense);
            await _treasuryPoster.SyncAsync(existingExpense, ct);
            return;
        }

        if (commissionAmount <= 0)
        {
            return;
        }

        var expense = new Expense
        {
            BusinessId = businessId,
            ExpenseTypeId = expenseType.Id,
            ExpenseType = expenseType,
            Amount = commissionAmount,
            ExpenseDate = date,
            PaymentMethod = PaymentMethod.Bank,
            Description = $"{platform.Name} platform komisyonu (otomatik, gün sonu içe aktarımından hesaplandı).",
            CreatedByUserId = postedByUserId
        };
        await _unitOfWork.Repository<Expense>().AddAsync(expense, ct);
        await _treasuryPoster.SyncAsync(expense, ct);
    }
}
