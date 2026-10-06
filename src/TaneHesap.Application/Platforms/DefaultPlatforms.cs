using TaneHesap.Domain.Entities;

namespace TaneHesap.Application.Platforms;

/// <summary>
/// Her işletmede hazır gelen paket servis platformları. Gün Sonu'ndaki Excel kartları (Yemeksepeti, Trendyol Go)
/// platformu adındaki anahtar kelimeyle bulur; bu yüzden platformlar işletme açılırken otomatik oluşturulur
/// (komisyon %0 — süper admin Paket Servis'ten gerçek oranı girer). Var olan işletmeler için açılışta tamamlanır
/// (bkz. LegacyDataFixups). Tek kaynak: ad ve anahtar kelimeler burada, frontend'deki <c>salesSources</c> ile aynı.
/// </summary>
public static class DefaultPlatforms
{
    public sealed record Definition(string Name, string[] Keywords);

    public static readonly IReadOnlyList<Definition> All =
    [
        new("Yemeksepeti", ["yemeksepeti", "yemek sepeti"]),
        new("Trendyol Go", ["trendyol", "uber"])
    ];

    /// <summary>Yeni işletme için varsayılan platform kayıtları.</summary>
    public static IEnumerable<Platform> CreateFor(Guid businessId, Guid createdByUserId)
        => All.Select(d => new Platform
        {
            BusinessId = businessId,
            Name = d.Name,
            CommissionPercentage = 0,
            IsActive = true,
            CreatedByUserId = createdByUserId
        });
}
