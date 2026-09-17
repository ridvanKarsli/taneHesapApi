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

    public async Task<ExpenseTypeDto> CreateAsync(Guid businessId, CreateExpenseTypeRequest request, Guid createdByUserId, CancellationToken ct = default)
    {
        var entity = new ExpenseType
        {
            BusinessId = businessId,
            Name = request.Name,
            Unit = request.Unit,
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
