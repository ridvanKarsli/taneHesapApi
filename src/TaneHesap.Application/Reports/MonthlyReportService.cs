using System.Globalization;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.Notifications;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Reports;

public class MonthlyReportService : IMonthlyReportService
{
    /// <summary>Malzeme başına gelir önceki aya göre bu oranın üzerinde düşerse uyarı verilir.</summary>
    public const decimal WarningDropPercent = 10m;

    private static readonly string[] ConsumptionSourceTypes = { "DailySalesConsumption", nameof(DailyActualEntry) };
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;

    public MonthlyReportService(IUnitOfWork unitOfWork, INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
    }

    public async Task<MonthlyReportDto> GetAsync(Guid businessId, int year, int month, CancellationToken ct = default)
    {
        var current = await LoadMonthAsync(businessId, year, month, ct);
        var previousStart = new DateOnly(year, month, 1).AddMonths(-1);
        var previous = await LoadMonthAsync(businessId, previousStart.Year, previousStart.Month, ct);

        var ingredients = (await _unitOfWork.Repository<Ingredient>().ListAsync(i => i.BusinessId == businessId, ct)).ToDictionary(i => i.Id);
        var efficiency = current.QuantityUsedByIngredient.Keys.Union(previous.QuantityUsedByIngredient.Keys)
            .Select(id => BuildEfficiency(id, ingredients.GetValueOrDefault(id), current, previous))
            .OrderByDescending(e => e.IsWarning).ThenBy(e => e.IngredientName)
            .ToList();

        var warnings = efficiency.Where(e => e.IsWarning).Select(e =>
            $"{e.IngredientName}: geçen ay {e.PreviousQuantityUsed.ToString("N2", Tr)} {e.Unit} ile {e.PreviousRevenuePerUnit.ToString("N2", Tr)} ₺/{e.Unit}, "
            + $"bu ay {e.QuantityUsed.ToString("N2", Tr)} {e.Unit} ile {e.RevenuePerUnit.ToString("N2", Tr)} ₺/{e.Unit} (%{(-e.ChangePercent!.Value).ToString("N1", Tr)} düşüş).")
            .ToList();

        var closed = (await _unitOfWork.Repository<MonthlyReport>().ListAsync(r => r.BusinessId == businessId && r.Year == year && r.Month == month, ct)).FirstOrDefault();

        return new MonthlyReportDto(
            year, month, current.Revenue, current.Expense, current.Revenue - current.Expense, current.PlatesSold,
            current.CostPerPlate, current.PlatesSold == 0 ? 0 : Math.Round(current.Revenue / current.PlatesSold, 2),
            previous.CostPerPlate, efficiency, warnings, closed?.GeneratedAtUtc);
    }

    public async Task<int> CloseFinishedMonthsAsync(DateOnly today, CancellationToken ct = default)
    {
        // Ayın son gününün satışları/kapanışı ertesi gün girilebilir; ay, en erken ayın 2'sinde kapatılır.
        if (today.Day < 2)
        {
            return 0;
        }

        var lastFinished = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        var businesses = await _unitOfWork.Repository<Business>().ListAsync(b => b.IsActive, ct);
        var closedCount = 0;

        foreach (var business in businesses)
        {
            var alreadyClosed = await _unitOfWork.Repository<MonthlyReport>()
                .AnyAsync(r => r.BusinessId == business.Id && r.Year == lastFinished.Year && r.Month == lastFinished.Month, ct);
            if (alreadyClosed)
            {
                continue;
            }

            var hasActivity = await _unitOfWork.Repository<DailySalesEntry>().AnyAsync(e => e.BusinessId == business.Id, ct)
                || await _unitOfWork.Repository<Expense>().AnyAsync(e => e.BusinessId == business.Id, ct);
            if (!hasActivity)
            {
                continue; // Hiç veri girilmemiş işletme için boş rapor/bildirim üretme.
            }

            var report = await GetAsync(business.Id, lastFinished.Year, lastFinished.Month, ct);
            await _unitOfWork.Repository<MonthlyReport>().AddAsync(new MonthlyReport
            {
                BusinessId = business.Id,
                Year = report.Year,
                Month = report.Month,
                TotalRevenue = report.TotalRevenue,
                TotalExpense = report.TotalExpense,
                PlatesSold = report.PlatesSold,
                CostPerPlate = report.CostPerPlate,
                WarningCount = report.Warnings.Count,
                GeneratedAtUtc = DateTime.UtcNow
            }, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await _notificationService.NotifyAdminsAsync(business.Id, NotificationType.MonthlyReport, BuildMessage(report), ct);
            closedCount++;
        }

        return closedCount;
    }

    private static string BuildMessage(MonthlyReportDto report)
    {
        var monthName = new DateOnly(report.Year, report.Month, 1).ToString("MMMM yyyy", Tr);
        var summary = $"{monthName} raporu hazır: {report.PlatesSold} tabak satıldı, tabak başı genel maliyet {report.CostPerPlate.ToString("N2", Tr)} ₺, "
            + $"net kâr {report.NetProfit.ToString("N2", Tr)} ₺.";
        return report.Warnings.Count == 0
            ? summary
            : $"{summary} UYARI — {report.Warnings.Count} malzemede verimlilik düştü: {string.Join(" ", report.Warnings)}";
    }

    private static IngredientEfficiencyDto BuildEfficiency(Guid id, Ingredient? ingredient, MonthFigures current, MonthFigures previous)
    {
        var qty = current.QuantityUsedByIngredient.GetValueOrDefault(id);
        var prevQty = previous.QuantityUsedByIngredient.GetValueOrDefault(id);
        var perUnit = qty > 0 ? Math.Round(current.Revenue / qty, 2) : 0;
        var prevPerUnit = prevQty > 0 ? Math.Round(previous.Revenue / prevQty, 2) : 0;
        decimal? change = prevPerUnit > 0 && qty > 0 ? Math.Round((perUnit - prevPerUnit) / prevPerUnit * 100m, 1) : null;
        var isWarning = change.HasValue && change.Value <= -WarningDropPercent;

        return new IngredientEfficiencyDto(id, ingredient?.Name ?? "-", ingredient?.Unit ?? "-", qty, perUnit, prevQty, prevPerUnit, change, isWarning);
    }

    private async Task<MonthFigures> LoadMonthAsync(Guid businessId, int year, int month, CancellationToken ct)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var sales = await _unitOfWork.Repository<DailySalesEntry>()
            .ListAsync(e => e.BusinessId == businessId && e.SaleDate >= from && e.SaleDate <= to, ct);
        var expenses = await _unitOfWork.Repository<Expense>()
            .ListAsync(e => e.BusinessId == businessId && e.ExpenseDate >= from && e.ExpenseDate <= to, ct);
        var movements = await _unitOfWork.Repository<StockMovement>()
            .ListAsync(m => m.BusinessId == businessId && m.SourceDate != null && m.SourceDate >= from && m.SourceDate <= to
                && m.SourceReferenceType != null && ConsumptionSourceTypes.Contains(m.SourceReferenceType), ct);

        var revenue = sales.Sum(s => s.TotalAmount);
        var expense = expenses.Sum(e => e.Amount);
        var plates = sales.Sum(s => s.Quantity);
        var used = movements.GroupBy(m => m.IngredientId).ToDictionary(g => g.Key, g => -g.Sum(m => m.QuantityChange));

        return new MonthFigures(revenue, expense, plates, plates == 0 ? 0 : Math.Round(expense / plates, 2), used);
    }

    private sealed record MonthFigures(decimal Revenue, decimal Expense, int PlatesSold, decimal CostPerPlate, Dictionary<Guid, decimal> QuantityUsedByIngredient);
}
