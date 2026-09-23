using TaneHesap.Domain.Common;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// Çalışanın saatlik ücreti — kimlik (Identity) kullanıcısından ayrı, işletmeye ait bir profil kaydı.
/// Çalışan silinse bile geçmiş çalışma kayıtları anlamını korur. bkz. Proje Raporu bölüm 3.7.
/// </summary>
public class EmployeeProfile : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    /// <summary>Identity kullanıcı Id'si (Role = Employee).</summary>
    public Guid UserId { get; set; }

    public decimal HourlyWage { get; set; }
}

/// <summary>
/// Çalışanın bir günde çalıştığı saat ve o günkü hak edişi. Cüzdan bakiyesi = Σ Amount − Σ personel ödemesi
/// (Expense.EmployeeUserId). Ücret anlık olarak kopyalanır ki sonradan ücret değişse geçmiş bozulmasın.
/// </summary>
public class EmployeeWorkLog : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public Guid UserId { get; set; }

    public DateOnly WorkDate { get; set; }

    public decimal Hours { get; set; }

    /// <summary>Kaydın girildiği andaki saatlik ücret.</summary>
    public decimal HourlyWage { get; set; }

    /// <summary>Hours × HourlyWage.</summary>
    public decimal Amount { get; set; }

    public string? Note { get; set; }
}
