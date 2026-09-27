using TaneHesap.Application.Common;
using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;

namespace TaneHesap.Application.ExpenseTypes;

public class ExpenseTypeService : IExpenseTypeService
{
    private readonly IUnitOfWork _unitOfWork;

    public ExpenseTypeService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<ExpenseTypeDto>> GetAllAsync(Guid businessId, CancellationToken ct = default)
    {
        var items = await _unitOfWork.Repository<ExpenseType>().ListAsync(e => e.BusinessId == businessId, ct);
        return items.OrderBy(e => e.Name).Select(ToDto).ToList();
    }

    public async Task<ExpenseTypeDto> GetByIdAsync(Guid businessId, Guid id, CancellationToken ct = default)
    {
        var entity = await GetTenantScopedAsync(businessId, id, ct);
        return ToDto(entity);
    }

    /// <summary>
    /// Gider girerken "+ Yeni tür" ile de çağrılır: ad boş olamaz; aynı adda (büyük/küçük harf farkıyla) tür varsa
    /// yenisi açılmaz, mevcut tür (pasifse yeniden aktif edilerek) döner. Birim boşsa "adet".
    /// </summary>
    public async Task<ExpenseTypeDto> CreateAsync(Guid businessId, CreateExpenseTypeRequest request, Guid createdByUserId, CancellationToken ct = default)
    {
        var name = request.Name?.Trim() ?? "";
        if (name.Length == 0)
        {
            throw new ValidationAppException("Gider türü adı boş olamaz.");
        }

        var repo = _unitOfWork.Repository<ExpenseType>();
        var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        var existing = (await repo.ListAsync(e => e.BusinessId == businessId, ct))
            .FirstOrDefault(e => StringComparer.Create(tr, ignoreCase: true).Equals(e.Name.Trim(), name));
        if (existing is not null)
        {
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                existing.UpdatedByUserId = createdByUserId;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                repo.Update(existing);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            return ToDto(existing);
        }

        var entity = new ExpenseType
        {
            BusinessId = businessId,
            Name = name,
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? "adet" : request.Unit.Trim(),
            Category = request.Category,
            IsActive = true,
            CreatedByUserId = createdByUserId
        };

        await _unitOfWork.Repository<ExpenseType>().AddAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    public async Task<ExpenseTypeDto> UpdateAsync(Guid businessId, Guid id, UpdateExpenseTypeRequest request, Guid updatedByUserId, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<ExpenseType>();
        var entity = await GetTenantScopedAsync(businessId, id, ct);

        entity.Name = request.Name;
        entity.Unit = request.Unit;
        entity.Category = request.Category;
        entity.IsActive = request.IsActive;
        entity.UpdatedByUserId = updatedByUserId;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        repo.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid businessId, Guid id, CancellationToken ct = default)
    {
        var entity = await GetTenantScopedAsync(businessId, id, ct);
        DeletionGuard.EnsureNotUsed(
            await _unitOfWork.Repository<Expense>().AnyAsync(e => e.ExpenseTypeId == id, ct), "Bu gider türü", "girilmiş giderlerde");

        _unitOfWork.Repository<ExpenseType>().Remove(entity);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<ExpenseType> GetTenantScopedAsync(Guid businessId, Guid id, CancellationToken ct)
    {
        var entity = await _unitOfWork.Repository<ExpenseType>().GetByIdAsync(id, ct);
        if (entity is null || entity.BusinessId != businessId)
        {
            throw new NotFoundException(nameof(ExpenseType), id);
        }

        return entity;
    }

    private static ExpenseTypeDto ToDto(ExpenseType e) => new(e.Id, e.Name, e.Unit, e.Category, e.IsActive);
}
