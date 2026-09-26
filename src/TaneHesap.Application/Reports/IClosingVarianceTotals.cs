namespace TaneHesap.Application.Reports;

/// <summary>
/// Gün sonu kapanışındaki kasa farkı (sayılan − satışlardan beklenen gelir) toplamı. Kasa bu farkla artıp azaldığı
/// için kâr/zarar raporları da aynı tutarı içermelidir; aksi halde kasa ile kâr birbirinden kopar.
/// Tek kaynak: kapanışın kasaya yazdığı hareketler.
/// </summary>
public interface IClosingVarianceTotals
{
    Task<decimal> SumAsync(Guid businessId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}
