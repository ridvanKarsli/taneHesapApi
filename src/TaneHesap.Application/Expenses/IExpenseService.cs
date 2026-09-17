namespace TaneHesap.Application.Expenses;

/// <summary>
/// Gider girişi — hem ADMIN hem EMPLOYEE kullanabilir (bkz. Proje Raporu bölüm 3.1).
/// Kim girdiği BaseEntity.CreatedByUserId üzerinden loglanır.
/// </summary>
public interface IExpenseService
{
    Task<List<ExpenseDto>> GetListAsync(Guid businessId, ExpenseListFilter filter, CancellationToken ct = default);

    Task<ExpenseDto> CreateAsync(Guid businessId, CreateExpenseRequest request, Guid createdByUserId, CancellationToken ct = default);
}
