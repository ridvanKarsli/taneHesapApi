using TaneHesap.Domain.Common;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// Sistemdeki her kritik işlemin değiştirilemez (append-only) denetim kaydı.
/// bkz. Proje Raporu bölüm 3.6.
/// </summary>
public class AuditLog : BaseEntity
{
    /// <summary>SUPER_ADMIN işlemlerinde null olabilir (business-scoped değildir).</summary>
    public Guid? BusinessId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Create / Update / Delete.</summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>Etkilenen entity adı (örn. "Expense", "Ingredient").</summary>
    public string EntityName { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
