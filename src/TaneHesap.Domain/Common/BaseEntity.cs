namespace TaneHesap.Domain.Common;

/// <summary>
/// Tüm domain entity'leri için ortak alanları (Id, oluşturma/güncelleme bilgisi) içeren taban sınıf.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Kaydı oluşturan kullanıcının Id'si (SUPER_ADMIN/ADMIN/EMPLOYEE).</summary>
    public Guid? CreatedByUserId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Kaydı son güncelleyen kullanıcının Id'si.</summary>
    public Guid? UpdatedByUserId { get; set; }
}

/// <summary>
/// İşletmeye (Business) bağlı olan, multi-tenant izolasyonuna tabi entity'lerin uyguladığı arayüz.
/// EF Core global query filter bu arayüzü uygulayan tüm entity'lere otomatik BusinessId filtresi uygular.
/// </summary>
public interface ITenantEntity
{
    Guid BusinessId { get; set; }
}
