using TaneHesap.Domain.Common;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// Gün sonu Excel yüklemesinden gelen tek bir sipariş satırı.
/// bkz. Proje Raporu bölüm 3.5.
/// </summary>
public class DailySalesEntry : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public DateOnly SaleDate { get; set; }
    public TimeOnly? SaleTime { get; set; }

    /// <summary>
    /// Sistemdeki ürün boyu. Platform dosyasındaki ürün sistemde eşleşmediyse null'dır: satır gelir olarak sayılır,
    /// ama reçete olmadığı için stoktan düşüm olmaz; adı <see cref="ProductName"/>'de tutulur.
    /// </summary>
    public Guid? DishSizeId { get; set; }
    public DishSize? DishSize { get; set; }

    /// <summary>Dosyadaki ürün adı (eşleşmeyen satırlar için; eşleşenlerde de bilgi amaçlı saklanır).</summary>
    public string? ProductName { get; set; }

    /// <summary>Platformun sipariş numarası (Trendyol Go / Yemeksepeti) — bir siparişin satırlarını bağlar.</summary>
    public string? ExternalOrderNumber { get; set; }

    public int Quantity { get; set; }

    public decimal TotalAmount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public SalesChannel Channel { get; set; }

    /// <summary>Channel = Platform ise ilgili paket servis platformu.</summary>
    public Guid? PlatformId { get; set; }
    public Platform? Platform { get; set; }

    public decimal? DiscountAmount { get; set; }

    /// <summary>Bu satırın geldiği Excel içe aktarım kaydı.</summary>
    public Guid? ExcelImportLogId { get; set; }
    public ExcelImportLog? ExcelImportLog { get; set; }
}
