namespace TaneHesap.Application.RecurringExpenses;

/// <summary>
/// Kira, elektrik gibi düzenli/periyodik giderler ve dönemsel ödeme durumu takibi.
/// Bir periyot sonunda ödendi işaretlenmezse ADMIN'e in-app hatırlatma gönderilmesi gerekir
/// (bkz. GetDueForReminderAsync — Notifications modülü tarafından tüketilecek). bkz. Proje Raporu bölüm 3.8.
/// </summary>
public interface IRecurringExpenseService
{
    Task<List<RecurringExpenseDto>> GetAllAsync(Guid businessId, CancellationToken ct = default);

    Task<RecurringExpenseDto> GetByIdAsync(Guid businessId, Guid id, CancellationToken ct = default);

    Task<RecurringExpenseDto> CreateAsync(Guid businessId, CreateRecurringExpenseRequest request, Guid createdByUserId, CancellationToken ct = default);

    Task<RecurringExpenseDto> UpdateAsync(Guid businessId, Guid id, UpdateRecurringExpenseRequest request, Guid updatedByUserId, CancellationToken ct = default);

    /// <summary>Belirtilen dönemi ödendi olarak işaretler (yoksa oluşturur, varsa günceller).</summary>
    Task<RecurringExpenseDto> MarkPeriodPaidAsync(Guid businessId, Guid id, MarkPeriodPaidRequest request, Guid updatedByUserId, CancellationToken ct = default);

    /// <summary>Güncel dönemi (bugün itibariyle) ödenmemiş VE dönem sonuna gelinmiş aktif giderleri döner — hatırlatma üretimi için.</summary>
    Task<List<RecurringExpenseDto>> GetDueForReminderAsync(Guid businessId, CancellationToken ct = default);
}
