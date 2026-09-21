using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Reports;

public record ExpenseCategoryTotalDto(ExpenseCategory Category, decimal Amount);

/// <summary>
/// Tabak (boy) bazlı dönemsel satış ve kârlılık (bkz. Proje Raporu bölüm 3.11). Maliyet, reçete ×
/// malzemelerin GÜNCEL birim fiyatıyla hesaplanır — geçmiş fiyat geçmişi tutulmadığı için tahminidir.
/// </summary>
public record DishSalesTotalDto(
    Guid DishSizeId,
    string DishName,
    string SizeName,
    int Quantity,
    decimal Revenue,
    decimal EstimatedCost,
    decimal EstimatedProfit);

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
    List<PlatformRevenueTotalDto> RevenueByPlatform,
    List<DishSalesTotalDto> SalesByDish);
