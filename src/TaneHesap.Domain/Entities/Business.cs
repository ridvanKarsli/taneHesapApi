using TaneHesap.Domain.Common;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// Bir işletmeyi (örn. Meydan Pilavcısı) temsil eder. SUPER_ADMIN tarafından oluşturulur.
/// Sistemdeki tüm tenant-scoped veriler bir Business'a bağlıdır.
/// </summary>
public class Business : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;
}
