namespace TaneHesap.Application.Reports;

/// <summary>
/// Bir malzemenin aylık verimliliği: tüketilen miktar başına elde edilen gelir (örn. 600 kg pirinç → 600.000 ₺
/// ⇒ 1.000 ₺/kg). Önceki aya göre <see cref="MonthlyReportService.WarningDropPercent"/>'ten fazla düşerse uyarı.
/// </summary>
public record IngredientEfficiencyDto(
    Guid IngredientId,
    string IngredientName,
    string Unit,
    decimal QuantityUsed,
    decimal RevenuePerUnit,
    decimal PreviousQuantityUsed,
    decimal PreviousRevenuePerUnit,
    decimal? ChangePercent,
    bool IsWarning);

/// <summary>Aylık rapor (bkz. Proje Raporu bölüm 3.16): tabak başı genel maliyet + malzeme verimliliği uyarıları.</summary>
public record MonthlyReportDto(
    int Year,
    int Month,
    decimal TotalRevenue,
    decimal TotalExpense,
    /// <summary>Gün sonu kasa farkı toplamı (dönem raporuyla aynı kural: kâra dahil).</summary>
    decimal ClosingVariance,
    decimal NetProfit,
    int PlatesSold,
    decimal CostPerPlate,
    decimal RevenuePerPlate,
    decimal PreviousCostPerPlate,
    List<IngredientEfficiencyDto> Ingredients,
    List<string> Warnings,
    DateTime? ClosedAtUtc);
