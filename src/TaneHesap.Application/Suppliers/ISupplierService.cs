namespace TaneHesap.Application.Suppliers;

/// <summary>
/// Tedarikçi kartları, alışları ve (kısmi olabilen) ödemeleri — borç takibi burada tutulur.
/// bkz. Proje Raporu bölüm 3.12.
/// </summary>
public interface ISupplierService
{
    Task<List<SupplierDto>> GetAllAsync(Guid businessId, CancellationToken ct = default);

    Task<SupplierDto> GetByIdAsync(Guid businessId, Guid id, CancellationToken ct = default);

    Task<SupplierDto> CreateAsync(Guid businessId, CreateSupplierRequest request, Guid createdByUserId, CancellationToken ct = default);

    Task<SupplierDto> UpdateAsync(Guid businessId, Guid id, UpdateSupplierRequest request, Guid updatedByUserId, CancellationToken ct = default);

    Task<List<SupplierPurchaseDto>> GetPurchasesAsync(Guid businessId, Guid supplierId, CancellationToken ct = default);

    /// <summary>Yeni alış kaydeder; ilgili malzemenin stoğunu ve güncel birim fiyatını otomatik günceller.</summary>
    Task<SupplierPurchaseDto> AddPurchaseAsync(Guid businessId, Guid supplierId, CreateSupplierPurchaseRequest request, Guid createdByUserId, CancellationToken ct = default);

    /// <summary>Bir alışa ödeme ekler; toplam ödeme tutar toplamına ulaşınca alışı IsFullyPaid=true yapar.</summary>
    Task<SupplierPurchaseDto> AddPaymentAsync(Guid businessId, Guid purchaseId, CreateSupplierPaymentRequest request, Guid createdByUserId, CancellationToken ct = default);

    /// <summary>Tüm tedarikçiler genelinde ödenmemiş toplam borç (raporlama için).</summary>
    Task<decimal> GetTotalOutstandingDebtAsync(Guid businessId, CancellationToken ct = default);
}
