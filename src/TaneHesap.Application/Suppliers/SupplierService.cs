using TaneHesap.Application.Common;
using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Suppliers;

public class SupplierService : ISupplierService
{
    private readonly IUnitOfWork _unitOfWork;

    public SupplierService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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

        var totalAmount = request.Quantity * request.UnitPrice;

        var purchase = new SupplierPurchase
        {
            BusinessId = businessId,
            SupplierId = supplier.Id,
            IngredientId = ingredient.Id,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            TotalAmount = totalAmount,
            PurchaseDate = request.PurchaseDate,
            IsFullyPaid = false,
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

        return await BuildPurchaseDtoAsync(businessId, purchase, ct);
    }

    public async Task<SupplierPurchaseDto> AddPaymentAsync(Guid businessId, Guid purchaseId, CreateSupplierPaymentRequest request, Guid createdByUserId, CancellationToken ct = default)
    {
        var purchaseRepo = _unitOfWork.Repository<SupplierPurchase>();
        var purchase = await purchaseRepo.GetByIdAsync(purchaseId, ct);
        if (purchase is null || purchase.BusinessId != businessId)
        {
            throw new NotFoundException(nameof(SupplierPurchase), purchaseId);
        }

        await _unitOfWork.Repository<SupplierPayment>().AddAsync(new SupplierPayment
        {
            BusinessId = businessId,
            SupplierPurchaseId = purchase.Id,
            Amount = request.Amount,
            PaymentDate = request.PaymentDate,
            CreatedByUserId = createdByUserId
        }, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        var payments = await _unitOfWork.Repository<SupplierPayment>().ListAsync(p => p.SupplierPurchaseId == purchase.Id, ct);
        var totalPaid = payments.Sum(p => p.Amount);

        if (totalPaid >= purchase.TotalAmount && !purchase.IsFullyPaid)
        {
            purchase.IsFullyPaid = true;
            purchase.UpdatedByUserId = createdByUserId;
            purchase.UpdatedAtUtc = DateTime.UtcNow;
            purchaseRepo.Update(purchase);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return await BuildPurchaseDtoAsync(businessId, purchase, ct);
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
            .Select(p => new SupplierPaymentDto(p.Id, p.Amount, p.PaymentDate))
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
