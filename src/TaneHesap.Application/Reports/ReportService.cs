using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Reports;

public class ReportService : IReportService
{
    private readonly IUnitOfWork _unitOfWork;

    public ReportService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PeriodReportDto> GetPeriodReportAsync(Guid businessId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var salesEntries = await _unitOfWork.Repository<DailySalesEntry>()
            .ListAsync(e => e.BusinessId == businessId && e.SaleDate >= fromDate && e.SaleDate <= toDate, ct);

        var expenses = await _unitOfWork.Repository<Expense>()
            .ListAsync(e => e.BusinessId == businessId && e.ExpenseDate >= fromDate && e.ExpenseDate <= toDate, ct);

        var expenseTypes = await _unitOfWork.Repository<ExpenseType>().ListAsync(t => t.BusinessId == businessId, ct);
        var expenseTypesById = expenseTypes.ToDictionary(t => t.Id);

        var platforms = await _unitOfWork.Repository<Platform>().ListAsync(p => p.BusinessId == businessId, ct);
        var platformsById = platforms.ToDictionary(p => p.Id);

        var totalRevenue = salesEntries.Sum(e => e.TotalAmount);
        var cashRevenue = salesEntries.Where(e => e.PaymentMethod == PaymentMethod.Cash).Sum(e => e.TotalAmount);
        var cardRevenue = salesEntries.Where(e => e.PaymentMethod == PaymentMethod.Card).Sum(e => e.TotalAmount);
        var inStoreRevenue = salesEntries.Where(e => e.Channel == SalesChannel.InStore).Sum(e => e.TotalAmount);
        var platformRevenue = salesEntries.Where(e => e.Channel == SalesChannel.Platform).Sum(e => e.TotalAmount);

        var totalExpense = expenses.Sum(e => e.Amount);

        var expenseByCategory = expenses
            .GroupBy(e => expenseTypesById.TryGetValue(e.ExpenseTypeId, out var t) ? t.Category : ExpenseCategory.Other)
            .Select(g => new ExpenseCategoryTotalDto(g.Key, g.Sum(e => e.Amount)))
            .OrderBy(d => d.Category)
            .ToList();

        var revenueByPlatform = salesEntries
            .Where(e => e.Channel == SalesChannel.Platform && e.PlatformId.HasValue)
            .GroupBy(e => e.PlatformId!.Value)
            .Select(g =>
            {
                var gross = g.Sum(e => e.TotalAmount);
                var platform = platformsById.GetValueOrDefault(g.Key);
                var commissionRate = platform?.CommissionPercentage ?? 0;
                var commission = gross * commissionRate / 100m;
                return new PlatformRevenueTotalDto(g.Key, platform?.Name ?? "-", gross, commission, gross - commission);
            })
            .OrderBy(d => d.PlatformName)
            .ToList();

        var netProfit = totalRevenue - totalExpense;

        return new PeriodReportDto(
            fromDate, toDate, totalRevenue, cashRevenue, cardRevenue, inStoreRevenue, platformRevenue,
            totalExpense, netProfit, expenseByCategory, revenueByPlatform);
    }
}
