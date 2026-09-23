using TaneHesap.Application.Common;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.DailySales;
using TaneHesap.Application.Expenses;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Platforms;

/// <summary>
/// Paket servis platformu (Yemeksepeti, Getir vb.) satışlarının komisyonunu otomatik bir gidere dönüştürür
/// (bkz. Proje Raporu bölüm 3.4). Gün sonu satışı değişince <see cref="IDailySalesSideEffect"/> olarak çağrılır;
/// platform+gün başına tek gider (kaynak: "PlatformCommission" + deterministik Id) — satış kalmadıysa silinir.
/// Komisyon platform hakedişinden kesildiği için ödeme şekli Bank'tır (kart kasasından düşer).
/// </summary>
public class PlatformCommissionExpensePoster : IDailySalesSideEffect
{
    public const string SourceType = "PlatformCommission";
    private const string ExpenseTypeName = "Platform Komisyonu";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAutoExpenseWriter _autoExpenses;

    public PlatformCommissionExpensePoster(IUnitOfWork unitOfWork, IAutoExpenseWriter autoExpenses)
    {
        _unitOfWork = unitOfWork;
        _autoExpenses = autoExpenses;
    }

    public async Task ApplyAsync(Guid businessId, IReadOnlyCollection<DateOnly> saleDates, Guid userId, CancellationToken ct = default)
    {
        var dates = saleDates.Distinct().ToList();
        var platforms = await _unitOfWork.Repository<Platform>().ListAsync(p => p.BusinessId == businessId, ct);
        if (dates.Count == 0 || platforms.Count == 0)
        {
            return;
        }

        var platformSales = await _unitOfWork.Repository<DailySalesEntry>()
            .ListAsync(e => e.BusinessId == businessId && e.Channel == SalesChannel.Platform && e.PlatformId != null && dates.Contains(e.SaleDate), ct);
        var grossByPlatformAndDate = platformSales
            .GroupBy(e => (PlatformId: e.PlatformId!.Value, e.SaleDate))
            .ToDictionary(g => g.Key, g => g.Sum(e => e.TotalAmount));

        foreach (var platform in platforms)
        {
            foreach (var date in dates)
            {
                var sourceId = DeterministicGuid.From(SourceType, platform.Id, date);
                var commission = grossByPlatformAndDate.TryGetValue((platform.Id, date), out var gross)
                    ? Math.Round(gross * platform.CommissionPercentage / 100m, 2)
                    : 0m;

                if (commission <= 0)
                {
                    await _autoExpenses.RemoveAsync(businessId, SourceType, sourceId, ct);
                    continue;
                }

                await _autoExpenses.UpsertAsync(new AutoExpenseSpec(
                    businessId, SourceType, sourceId, ExpenseTypeName, ExpenseCategory.Other, commission, date,
                    PaymentMethod.Bank, null,
                    $"{platform.Name} — %{platform.CommissionPercentage:0.##} komisyon (otomatik, {date:dd.MM.yyyy} satışlarından)",
                    userId), ct);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }
}
