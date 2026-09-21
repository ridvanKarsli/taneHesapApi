namespace TaneHesap.Application.Expenses;

/// <summary>
/// Gider girişi — hem ADMIN hem EMPLOYEE kullanabilir (bkz. Proje Raporu bölüm 3.1).
/// Kim girdiği BaseEntity.CreatedByUserId üzerinden loglanır.
/// </summary>
public interface IExpenseService
{
    Task<List<ExpenseDto>> GetListAsync(Guid businessId, ExpenseListFilter filter, CancellationToken ct = default);

    Task<ExpenseDto> CreateAsync(Guid businessId, CreateExpenseRequest request, Guid createdByUserId, CancellationToken ct = default);

    /// <summary>
    /// Gideri günceller. <paramref name="onlyCreatedBy"/> doluysa (EMPLOYEE) sadece o kullanıcının
    /// girdiği gider güncellenebilir; başkasınınki 404 döner.
    /// </summary>
    Task<ExpenseDto> UpdateAsync(Guid businessId, Guid id, UpdateExpenseRequest request, Guid updatedByUserId, Guid? onlyCreatedBy, CancellationToken ct = default);

    /// <summary>Gideri siler; <paramref name="onlyCreatedBy"/> kuralı güncellemedeki ile aynıdır.</summary>
    Task DeleteAsync(Guid businessId, Guid id, Guid? onlyCreatedBy, CancellationToken ct = default);
}
