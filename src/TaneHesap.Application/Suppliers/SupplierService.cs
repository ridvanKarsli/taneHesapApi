using TaneHesap.Application.Common;
using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.Expenses;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Suppliers;

public class SupplierService : ISupplierService
{
    public const string PaymentSourceType = "SupplierPayment";
    private const string PaymentExpenseTypeName = "Tedarikçi Ödemesi";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAutoExpenseWriter _autoExpenses;

    public SupplierService(IUnitOfWork unitOfWork, IAutoExpenseWriter autoExpenses)
    {
        _unitOfWork = unitOfWork;
        _autoExpenses = autoExpenses;
    }

    public async Task<List<SupplierDto>> GetAllAsync(Guid businessId, CancellationToken ct = default)
    {
        var suppliers = await _unitOfWork.Repository<Supplier>().ListAsync(s => s.BusinessId == businessId, ct);
        var result = new List<SupplierDto>();
        foreach (var supplier in suppliers.OrderBy(s => s.Name))
        {
            result.Add(await BuildSupplierDtoAsync(businessId, supplier, ct));
        }

        return result;
    }

    public async Task<SupplierDto> GetByIdAsync(Guid businessId, Guid id, CancellationToken ct = default)
    {
        var supplier = await GetTenantScopedSupplierAsync(businessId, id, ct);
        return await BuildSupplierDtoAsync(businessId, supplier, ct);
    }

    public async Task<SupplierDto> CreateAsync(Guid businessId, CreateSupplierRequest request, Guid createdByUserId, CancellationToken ct = default)
    {
        var supplier = new Supplier
        {
            BusinessId = businessId,
            Name = request.Name,
            ContactInfo = request.ContactInfo,
            IsActive = true,
            CreatedByUserId = createdByUserId
        };

        await _unitOfWork.Repository<Supplier>().AddAsync(supplier, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await BuildSupplierDtoAsync(businessId, supplier, ct);
    }

    public async Task<SupplierDto> UpdateAsync(Guid businessId, Guid id, UpdateSupplierRequest request, Guid updatedByUserId, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<Supplier>();
        var supplier = await GetTenantScopedSupplierAsync(businessId, id, ct);

        supplier.Name = request.Name;
        supplier.ContactInfo = request.ContactInfo;
        supplier.IsActive = request.IsActive;
        supplier.UpdatedByUserId = updatedByUserId;
        supplier.UpdatedAtUtc = DateTime.UtcNow;

        repo.Update(supplier);
        await _unitOfWork.SaveChangesAsync(ct);

        return await BuildSupplierDtoAsync(businessId, supplier, ct);
    }

    public async Task<List<SupplierPurchaseDto>> GetPurchasesAsync(Guid businessId, Guid supplierId, CancellationToken ct = default)
    {
        await GetTenantScopedSupplierAsync(businessId, supplierId, ct);

        var purchases = await _unitOfWork.Repository<SupplierPurchase>()
            .ListAsync(p => p.BusinessId == businessId && p.SupplierId == supplierId, ct);

        var result = new List<SupplierPurchaseDto>();
        foreach (var purchase in purchases.OrderByDescending(p => p.PurchaseDate))
        {
            result.Add(await BuildPurchaseDtoAsync(businessId, purchase, ct));
        }

        return result;
    }

    public async Task<SupplierPurchaseDto> AddPurchaseAsync(Guid businessId, Guid supplierId, CreateSupplierPurchaseRequest request, Guid createdByUserId, CancellationToken ct = default)
    {
        var supplier = await GetTenantScopedSupplierAsync(businessId, supplierId, ct);

        var ingredientRepo = _unitOfWork.Repository<Ingredient>();
        var ingredient = await ingredientRepo.GetByIdAsync(request.IngredientId, ct);
        if (ingredient is null || ingredient.BusinessId != businessId)
        {
            throw new NotFoundException(nameof(Ingredient), request.IngredientId);
        }

        // Tüm doğrulamalar ilk yazımdan önce: alış kaydedilip ödeme adımında hata alınırsa yeniden deneme stoğu iki kez artırırdı.
        if (request.Quantity <= 0)
        {
            throw new ValidationAppException("Alış miktarı 0'dan büyük olmalı.");
        }

        if (request.UnitPrice < 0)
        {
            throw new ValidationAppException("Birim fiyat negatif olamaz.");
        }

        var totalAmount = MoneyMath.Round(request.Quantity * request.UnitPrice);
        if (request.PaidAmount is > 0)
        {
            if (request.PaymentMethod is null)
            {
                throw new ValidationAppException("Alış anında ödeme için ödeme şekli seçilmeli.");
            }

            EnsurePaymentWithinDebt(request.PaidAmount.Value, totalAmount);
        }

        var purchase = new SupplierPurchase
        {
            BusinessId = businessId,
            SupplierId = supplier.Id,
            IngredientId = ingredient.Id,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            TotalAmount = totalAmount,
            PurchaseDate = request.PurchaseDate,
            IsFullyPaid = totalAmount == 0,
            CreatedByUserId = createdByUserId
        };

        await _unitOfWork.Repository<SupplierPurchase>().AddAsync(purchase, ct);

        // Stoğa alış miktarı kadar ekle ve malzemenin güncel birim fiyatını bu alışla güncelle.
        await _unitOfWork.Repository<StockMovement>().AddAsync(new StockMovement
        {
            BusinessId = businessId,
            IngredientId = ingredient.Id,
            QuantityChange = request.Quantity,
            MovementType = StockMovementType.Purchase,
            MovementDateUtc = DateTime.UtcNow,
            SourceReferenceType = nameof(SupplierPurchase),
            SourceReferenceId = purchase.Id,
            CreatedByUserId = createdByUserId
        }, ct);

        ingredient.CurrentStockQuantity += request.Quantity;
        ingredient.CurrentUnitPrice = request.UnitPrice;
        ingredient.UpdatedByUserId = createdByUserId;
        ingredient.UpdatedAtUtc = DateTime.UtcNow;
        ingredientRepo.Update(ingredient);

        await _unitOfWork.SaveChangesAsync(ct);

        // Alış anında ödeme yapıldıysa aynı akıştan (kasa + otomatik gider) kaydedilir.
        if (request.PaidAmount is > 0)
        {
            return await AddPaymentAsync(businessId, purchase.Id,
                new CreateSupplierPaymentRequest(request.PaidAmount.Value, request.PurchaseDate, request.PaymentMethod!.Value, request.PaymentCardId), createdByUserId, ct);
        }

        return await BuildPurchaseDtoAsync(businessId, purchase, ct);
    }

    public async Task<SupplierPurchaseDto> AddPaymentAsync(Guid businessId, Guid purchaseId, CreateSupplierPaymentRequest request, Guid createdByUserId, CancellationToken ct = default)
    {
        var purchase = await GetTenantScopedPurchaseAsync(businessId, purchaseId, ct);

        if (request.Amount <= 0)
        {
            throw new ValidationAppException("Ödeme tutarı 0'dan büyük olmalı.");
        }

        var paymentRepo = _unitOfWork.Repository<SupplierPayment>();
        var alreadyPaid = (await paymentRepo.ListAsync(p => p.SupplierPurchaseId == purchase.Id, ct)).Sum(p => p.Amount);
        EnsurePaymentWithinDebt(request.Amount, purchase.TotalAmount - alreadyPaid);

        var payment = new SupplierPayment
        {
            BusinessId = businessId,
            SupplierPurchaseId = purchase.Id,
            Amount = MoneyMath.Round(request.Amount),
            PaymentDate = request.PaymentDate,
            PaymentMethod = request.PaymentMethod,
            PaymentCardId = request.PaymentCardId,
            CreatedByUserId = createdByUserId
        };
        await paymentRepo.AddAsync(payment, ct);

        // Tedarikçiye ödenen para: kasadan/karttan düşen, raporlarda "Malzeme" görünen otomatik gider (bkz. 3.12, 3.15).
        var supplier = await _unitOfWork.Repository<Supplier>().GetByIdAsync(purchase.SupplierId, ct);
        var ingredient = await _unitOfWork.Repository<Ingredient>().GetByIdAsync(purchase.IngredientId, ct);
        var expense = await _autoExpenses.UpsertAsync(new AutoExpenseSpec(
            businessId, PaymentSourceType, payment.Id, PaymentExpenseTypeName, ExpenseCategory.Material,
            payment.Amount, request.PaymentDate, request.PaymentMethod, request.PaymentCardId,
            $"{supplier?.Name ?? "Tedarikçi"} — {ingredient?.Name ?? "alış"} ({purchase.PurchaseDate:dd.MM.yyyy} alışı) ödemesi (otomatik)",
            createdByUserId), ct);
        payment.PaymentCardId = expense.PaymentCardId; // Kart doğrulaması (işletmeye ait, aktif) gider yazıcısında tek yerde yapılır.

        purchase.IsFullyPaid = MoneyMath.Round(alreadyPaid + payment.Amount) >= purchase.TotalAmount;
        purchase.UpdatedByUserId = createdByUserId;
        purchase.UpdatedAtUtc = DateTime.UtcNow;
        _unitOfWork.Repository<SupplierPurchase>().Update(purchase);

        await _unitOfWork.SaveChangesAsync(ct);
        return await BuildPurchaseDtoAsync(businessId, purchase, ct);
    }

    /// <summary>Yanlış girilen ödeme geri alınır: otomatik gider ve kasa hareketi de silinir, alış yeniden "borçlu" olur.</summary>
    public async Task<SupplierPurchaseDto> DeletePaymentAsync(Guid businessId, Guid purchaseId, Guid paymentId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var purchase = await GetTenantScopedPurchaseAsync(businessId, purchaseId, ct);
        var paymentRepo = _unitOfWork.Repository<SupplierPayment>();
        var payment = await paymentRepo.GetByIdAsync(paymentId, ct);
        if (payment is null || payment.SupplierPurchaseId != purchase.Id)
        {
            throw new NotFoundException(nameof(SupplierPayment), paymentId);
        }

        await _autoExpenses.RemoveAsync(businessId, PaymentSourceType, payment.Id, ct);
        paymentRepo.Remove(payment);

        purchase.IsFullyPaid = false;
        purchase.UpdatedByUserId = deletedByUserId;
        purchase.UpdatedAtUtc = DateTime.UtcNow;
        _unitOfWork.Repository<SupplierPurchase>().Update(purchase);

        await _unitOfWork.SaveChangesAsync(ct);
        return await BuildPurchaseDtoAsync(businessId, purchase, ct);
    }

    /// <summary>
    /// Yanlış girilen alış geri alınır: stok girişi ters çevrilir. Ödemesi olan alış silinmez (önce ödemeler silinir);
    /// böylece kasa/gider tarafında sahipsiz kayıt kalmaz. Malzemenin güncel birim fiyatı geri alınmaz (son bilinen fiyat kalır).
    /// </summary>
    public async Task DeletePurchaseAsync(Guid businessId, Guid purchaseId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var purchase = await GetTenantScopedPurchaseAsync(businessId, purchaseId, ct);
        DeletionGuard.EnsureNotUsed(
            await _unitOfWork.Repository<SupplierPayment>().AnyAsync(p => p.SupplierPurchaseId == purchase.Id, ct),
            "Bu alış", "ödeme kayıtlarında (önce ödemeleri silin)");

        var movementRepo = _unitOfWork.Repository<StockMovement>();
        foreach (var movement in await movementRepo.ListAsync(m => m.SourceReferenceType == nameof(SupplierPurchase) && m.SourceReferenceId == purchase.Id, ct))
        {
            movementRepo.Remove(movement);
        }

        var ingredientRepo = _unitOfWork.Repository<Ingredient>();
        var ingredient = await ingredientRepo.GetByIdAsync(purchase.IngredientId, ct);
        if (ingredient is not null)
        {
            ingredient.CurrentStockQuantity -= purchase.Quantity;
            ingredient.UpdatedByUserId = deletedByUserId;
            ingredient.UpdatedAtUtc = DateTime.UtcNow;
            ingredientRepo.Update(ingredient);
        }

        _unitOfWork.Repository<SupplierPurchase>().Remove(purchase);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static void EnsurePaymentWithinDebt(decimal amount, decimal remaining)
    {
        if (MoneyMath.Round(amount) > MoneyMath.Round(remaining))
        {
            throw new ValidationAppException($"Ödeme kalan borçtan ({MoneyMath.Round(remaining):N2} ₺) fazla olamaz.");
        }
    }

    private async Task<SupplierPurchase> GetTenantScopedPurchaseAsync(Guid businessId, Guid purchaseId, CancellationToken ct)
    {
        var purchase = await _unitOfWork.Repository<SupplierPurchase>().GetByIdAsync(purchaseId, ct);
        if (purchase is null || purchase.BusinessId != businessId)
        {
            throw new NotFoundException(nameof(SupplierPurchase), purchaseId);
        }

        return purchase;
    }

    public async Task<decimal> GetTotalOutstandingDebtAsync(Guid businessId, CancellationToken ct = default)
    {
        var purchases = await _unitOfWork.Repository<SupplierPurchase>()
            .ListAsync(p => p.BusinessId == businessId && !p.IsFullyPaid, ct);

        decimal totalDebt = 0;
        foreach (var purchase in purchases)
        {
            var payments = await _unitOfWork.Repository<SupplierPayment>().ListAsync(p => p.SupplierPurchaseId == purchase.Id, ct);
            totalDebt += purchase.TotalAmount - payments.Sum(p => p.Amount);
        }

        return totalDebt;
    }

    public async Task DeleteAsync(Guid businessId, Guid id, CancellationToken ct = default)
    {
        var supplier = await GetTenantScopedSupplierAsync(businessId, id, ct);
        DeletionGuard.EnsureNotUsed(
            await _unitOfWork.Repository<SupplierPurchase>().AnyAsync(p => p.SupplierId == id, ct), "Bu tedarikçi", "alış kayıtlarında");

        _unitOfWork.Repository<Supplier>().Remove(supplier);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<Supplier> GetTenantScopedSupplierAsync(Guid businessId, Guid id, CancellationToken ct)
    {
        var supplier = await _unitOfWork.Repository<Supplier>().GetByIdAsync(id, ct);
        if (supplier is null || supplier.BusinessId != businessId)
        {
            throw new NotFoundException(nameof(Supplier), id);
        }

        return supplier;
    }

    private async Task<SupplierDto> BuildSupplierDtoAsync(Guid businessId, Supplier supplier, CancellationToken ct)
    {
        var purchases = await _unitOfWork.Repository<SupplierPurchase>()
            .ListAsync(p => p.BusinessId == businessId && p.SupplierId == supplier.Id && !p.IsFullyPaid, ct);

        decimal outstanding = 0;
        foreach (var purchase in purchases)
        {
            var payments = await _unitOfWork.Repository<SupplierPayment>().ListAsync(p => p.SupplierPurchaseId == purchase.Id, ct);
            outstanding += purchase.TotalAmount - payments.Sum(p => p.Amount);
        }

        return new SupplierDto(supplier.Id, supplier.Name, supplier.ContactInfo, supplier.IsActive, outstanding);
    }

    private async Task<SupplierPurchaseDto> BuildPurchaseDtoAsync(Guid businessId, SupplierPurchase purchase, CancellationToken ct)
    {
        var ingredient = await _unitOfWork.Repository<Ingredient>().GetByIdAsync(purchase.IngredientId, ct);
        var payments = await _unitOfWork.Repository<SupplierPayment>().ListAsync(p => p.SupplierPurchaseId == purchase.Id, ct);
        var paidAmount = payments.Sum(p => p.Amount);

        var paymentDtos = payments
            .OrderBy(p => p.PaymentDate)
            .Select(p => new SupplierPaymentDto(p.Id, p.Amount, p.PaymentDate, p.PaymentMethod))
            .ToList();

        return new SupplierPurchaseDto(
            purchase.Id,
            purchase.SupplierId,
            purchase.IngredientId,
            ingredient?.Name ?? string.Empty,
            purchase.Quantity,
            purchase.UnitPrice,
            purchase.TotalAmount,
            purchase.PurchaseDate,
            purchase.IsFullyPaid,
            paidAmount,
            purchase.TotalAmount - paidAmount,
            paymentDtos);
    }
}
