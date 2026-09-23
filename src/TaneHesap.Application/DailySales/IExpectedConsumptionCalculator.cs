namespace TaneHesap.Application.DailySales;

/// <summary>
/// Bir günün satış satırlarından reçeteye göre beklenen malzeme tüketimini hesaplar (malzeme Id → miktar).
/// Hem gün sonu özetinde (DailySalesService) hem otomatik stok düşümünde (SalesStockConsumptionPoster)
/// kullanılır — aynı hesap iki yerde yazılmaz (DRY) ve iki servis birbirine bağımlı olmaz.
/// </summary>
public interface IExpectedConsumptionCalculator
{
    Task<Dictionary<Guid, decimal>> CalculateAsync(Guid businessId, DateOnly date, CancellationToken ct = default);
}
