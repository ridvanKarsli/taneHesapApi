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

/// <summary>Gider türüne göre toplam ("Kira", "Pirinç", "POS Komisyonu"…), büyükten küçüğe.</summary>
public record ExpenseTypeTotalDto(Guid ExpenseTypeId, string ExpenseTypeName, ExpenseCategory Category, decimal Amount);

public record PlatformRevenueTotalDto(Guid PlatformId, string PlatformName, decimal GrossRevenue, decimal CommissionAmount, decimal NetRevenue);

/// <summary>
/// Günlük/haftalık/aylık gelir-gider raporu. Gelir, doğrulanan günlerde gerçek nakit/kart tutarlarıdır
/// (bkz. InStoreIncome) — kasaya yazılanla aynı.
/// bkz. Proje Raporu bölüm 3.14.
/// </summary>
public record PeriodReportDto(
    DateOnly FromDate,
    DateOnly ToDate,
    decimal TotalRevenue,
    decimal CashRevenue,
    decimal CardRevenue,
    decimal PlatformRevenue,
    decimal TotalExpense,
    /// <summary>Gelir − gider.</summary>
    decimal NetProfit,
    List<ExpenseCategoryTotalDto> ExpenseByCategory,
    List<ExpenseTypeTotalDto> ExpenseByType,
    List<PlatformRevenueTotalDto> RevenueByPlatform,
    List<DishSalesTotalDto> SalesByDish);
