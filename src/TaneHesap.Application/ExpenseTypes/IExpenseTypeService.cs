namespace TaneHesap.Application.ExpenseTypes;

/// <summary>
/// Gider türü kataloğu — sadece ADMIN oluşturur/günceller; EMPLOYEE sadece listeler.
/// bkz. Proje Raporu bölüm 3.2.
/// </summary>
public interface IExpenseTypeService
{
    Task<List<ExpenseTypeDto>> GetAllAsync(Guid businessId, CancellationToken ct = default);

    Task<ExpenseTypeDto> GetByIdAsync(Guid businessId, Guid id, CancellationToken ct = default);

    Task<ExpenseTypeDto> CreateAsync(Guid businessId, CreateExpenseTypeRequest request, Guid createdByUserId, CancellationToken ct = default);

    Task<ExpenseTypeDto> UpdateAsync(Guid businessId, Guid id, UpdateExpenseTypeRequest request, Guid updatedByUserId, CancellationToken ct = default);
}
