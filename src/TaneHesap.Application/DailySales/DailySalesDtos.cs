using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.DailySales;

public record DailySalesEntryDto(
    Guid Id,
    DateOnly SaleDate,
    TimeOnly? SaleTime,
    Guid DishSizeId,
    string DishName,
    string SizeName,
    int Quantity,
    decimal TotalAmount,
    PaymentMethod PaymentMethod,
    SalesChannel Channel,
    Guid? PlatformId,
    string? PlatformName,
    decimal? DiscountAmount);

/// <summary>
/// Excel'den (veya ilerideki bir dosya yükleme uç noktasından) ayrıştırılmış tek bir satır.
/// Not: Excel şablonunun kesin kolon yapısı henüz netleşmedi (bkz. Proje Raporu bölüm 7/9);
/// bu yüzden import bu ortamda önce ayrıştırılmış satırları (JSON) kabul eder — asıl .xlsx
/// ayrıştırması (ör. ClosedXML ile) API katmanında bu şablon netleşince eklenecek ve aynı
/// ImportDailySalesRequest'i üretecek şekilde bağlanacaktır.
/// </summary>
/// <remarks>
/// <c>TotalAmount</c> müşteriden tahsil edilen NET tutardır (indirim düşülmüş); <c>DiscountAmount</c> yalnızca
/// bilgi amaçlıdır — gelir, kasa ve komisyon hesapları TotalAmount üzerinden yapılır. Arayüz, fiyat × adet − indirim'i
/// otomatik önerir. bkz. Proje Raporu bölüm 3.5.
/// </remarks>
public record ImportRowRequest(
    DateOnly SaleDate,
    TimeOnly? SaleTime,
    Guid DishSizeId,
    int Quantity,
    decimal TotalAmount,
    PaymentMethod PaymentMethod,
    SalesChannel Channel,
    Guid? PlatformId,
    decimal? DiscountAmount);

/// <summary>Aynı güne ikinci kez satış gelirse ne yapılacağı.</summary>
public enum DailySalesImportMode
{
    /// <summary>Var olan kayıtların üzerine eklenir (elle tek tek giriş).</summary>
    Append = 0,
    /// <summary>O günlerde kayıt varsa 409 ile reddedilir — aynı dosyanın iki kez yüklenip günün iki kez sayılması engellenir.</summary>
    RejectIfExists = 1,
    /// <summary>O günlerin mevcut kayıtları silinip dosyadakiler yazılır (düzeltilmiş dosya yeniden yüklendi).</summary>
    Replace = 2
}

public record ImportDailySalesRequest(string FileName, List<ImportRowRequest> Rows, DailySalesImportMode Mode = DailySalesImportMode.RejectIfExists);

public record ImportDailySalesResult(Guid ImportLogId, int RowCount, int SuccessCount, int ErrorCount, List<string> Errors);

public record ExpectedIngredientConsumptionDto(Guid IngredientId, string IngredientName, string Unit, decimal ExpectedQuantity);

/// <summary>Bir gün için, Excel'den yüklenen siparişlere göre sistemin hesapladığı beklenen değerler.</summary>
public record ExpectedDaySummaryDto(
    DateOnly Date,
    decimal ExpectedRevenue,
    List<ExpectedIngredientConsumptionDto> ExpectedConsumption);
