using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.ExpenseTypes;

/// <summary>
/// Sistemin otomatik kullandığı gider türlerini (platform komisyonu, kart komisyonu, düzenli gider,
/// tedarikçi ödemesi, personel ödemesi) ada göre get-or-create eder — tek yerde, her modül kendi
/// türünü tekrar üretmez (DRY). Kalıcı hale getirir (SaveChanges) ki aynı istekte ikinci çağrı aynı türü bulsun.
/// </summary>
public interface IExpenseTypeCatalog
{
    Task<ExpenseType> GetOrCreateAsync(Guid businessId, string name, ExpenseCategory category, Guid userId, CancellationToken ct = default);
}
