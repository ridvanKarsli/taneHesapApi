using TaneHesap.Domain.Common;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// ADMIN tarafından tanımlanan gider türü kataloğu (örn. ürün adı, birim, kategori).
/// Hem ADMIN hem EMPLOYEE gider girişinde bu türleri kullanır. bkz. Proje Raporu bölüm 3.2.
/// </summary>
public class ExpenseType : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Ölçü birimi (kg, lt, adet vb.).</summary>
    public string Unit { get; set; } = string.Empty;

    public ExpenseCategory Category { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
