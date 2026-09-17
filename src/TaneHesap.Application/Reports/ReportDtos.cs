using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Reports;

public record ExpenseCategoryTotalDto(ExpenseCategory Category, decimal Amount);

public record PlatformRevenueTotalDto(Guid PlatformId, string PlatformName, decimal GrossRevenue, decimal CommissionAmount, decimal NetRevenue);

/// <summary>
/// Günlük/haftalık/aylık gelir-gider raporu — nakit/kart ve kanal bazlı kırılım ile.
/// bkz. Proje Raporu bölüm 3.14.
/// </summary>
public record PeriodReportDto(
    DateOnly FromDate,
    DateOnly ToDate,
    decimal TotalRevenue,
    decimal CashRevenue,
    decimal CardRevenue,
    decimal InStoreRevenue,
    decimal PlatformRevenue,
    decimal TotalExpense,
    decimal NetProfit,
    List<ExpenseCategoryTotalDto> ExpenseByCategory,
    List<PlatformRevenueTotalDto> RevenueByPlatform);
