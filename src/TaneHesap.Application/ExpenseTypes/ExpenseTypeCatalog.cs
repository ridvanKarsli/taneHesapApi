using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.ExpenseTypes;

public class ExpenseTypeCatalog : IExpenseTypeCatalog
{
    private const string DefaultUnit = "TL";

    private readonly IUnitOfWork _unitOfWork;

    public ExpenseTypeCatalog(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ExpenseType> GetOrCreateAsync(Guid businessId, string name, ExpenseCategory category, Guid userId, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<ExpenseType>();
        var existing = (await repo.ListAsync(t => t.BusinessId == businessId && t.Name == name, ct)).FirstOrDefault();
        if (existing is not null)
        {
            if (!existing.IsActive)
            {
                existing.IsActive = true; // Otomatik kullanılan tür pasif bırakılmış olsa da kayıt yazılabilmeli.
                repo.Update(existing);
            }

            return existing;
        }

        var created = new ExpenseType { BusinessId = businessId, Name = name, Unit = DefaultUnit, Category = category, CreatedByUserId = userId };
        await repo.AddAsync(created, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return created;
    }
}
