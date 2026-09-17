using TaneHesap.Domain.Common;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// Gün sonu Excel içe aktarımlarının denetim kaydı (dosya adı, satır sayısı, hata/uyarılar).
/// bkz. Proje Raporu bölüm 3.5, 3.6.
/// </summary>
public class ExcelImportLog : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public string FileName { get; set; } = string.Empty;

    public Guid ImportedByUserId { get; set; }

    public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;

    public int RowCount { get; set; }

    public int ErrorCount { get; set; }

    /// <summary>Satır bazlı hata/uyarı listesi (JSON olarak saklanır).</summary>
    public string? ErrorsJson { get; set; }

    public ICollection<DailySalesEntry> SalesEntries { get; set; } = new List<DailySalesEntry>();
}
