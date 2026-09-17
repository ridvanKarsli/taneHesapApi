using TaneHesap.Domain.Common;

namespace TaneHesap.Domain.Entities;

/// <summary>Tedarikçi kartı. bkz. Proje Raporu bölüm 3.12.</summary>
public class Supplier : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? ContactInfo { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<SupplierPurchase> Purchases { get; set; } = new List<SupplierPurchase>();
}

/// <summary>Bir tedarikçiden yapılan malzeme alışı ve ödeme durumu.</summary>
public class SupplierPurchase : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public Guid IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }

    public DateOnly PurchaseDate { get; set; }

    /// <summary>SupplierPayment kayıtları toplamı TotalAmount'a ulaşınca true olur (denormalize).</summary>
    public bool IsFullyPaid { get; set; }

    public ICollection<SupplierPayment> Payments { get; set; } = new List<SupplierPayment>();
}

/// <summary>Bir tedarikçi alışına karşılık yapılan (kısmi olabilen) ödeme.</summary>
public class SupplierPayment : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public Guid SupplierPurchaseId { get; set; }
    public SupplierPurchase? SupplierPurchase { get; set; }

    public decimal Amount { get; set; }
    public DateOnly PaymentDate { get; set; }
}
