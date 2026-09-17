namespace TaneHesap.Application.DailyClosing;

public record DailyActualConsumptionItemDto(Guid IngredientId, string IngredientName, string Unit, decimal ActualQuantityUsed);

public record DailyActualEntryDto(Guid Id, DateOnly EntryDate, decimal ActualRevenue, string? Note, List<DailyActualConsumptionItemDto> ConsumptionItems);

public record ConsumptionItemRequest(Guid IngredientId, decimal ActualQuantityUsed);

/// <summary>
/// ADMIN'in gün sonu Excel yüklemesinden SONRA girdiği gerçekleşen gelir ve malzeme tüketimi.
/// Gönderilince: (1) stok, girilen gerçek tüketim kadar düşülür (bu tarih için daha önce bir giriş
/// varsa önce geri alınır — yeniden gönderim desteklenir), (2) o gün için DailyLossReport
/// (beklenen vs gerçek fark raporu) otomatik (yeniden) üretilir. bkz. Proje Raporu bölüm 3.10.
/// </summary>
public record SubmitDailyActualEntryRequest(DateOnly EntryDate, decimal ActualRevenue, string? Note, List<ConsumptionItemRequest> ConsumptionItems);

public record DailyLossReportItemDto(
    Guid IngredientId,
    string IngredientName,
    string Unit,
    decimal ExpectedQuantity,
    decimal ActualQuantity,
    decimal VarianceQuantity,
    decimal VarianceCost);

public record DailyLossReportDto(
    Guid Id,
    DateOnly ReportDate,
    decimal ExpectedRevenue,
    decimal ActualRevenue,
    decimal RevenueVarianceAmount,
    DateTime GeneratedAtUtc,
    List<DailyLossReportItemDto> Items);
