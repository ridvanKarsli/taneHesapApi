using TaneHesap.Application.DailySales;

namespace TaneHesap.Application.DailyClosing;

/// <summary>
/// Kapanışı yapılmış bir günün satışı sonradan değişirse (yeniden içe aktarma, satır silme) kapanışın türetilmiş
/// kayıtları — stok farkı, kasa sayım farkı, fire raporu — eskimesin diye otomatik yeniden hesaplanır.
/// </summary>
public class DailyClosingRecalculationSideEffect : IDailySalesSideEffect
{
    private readonly IDailyClosingService _closingService;

    public DailyClosingRecalculationSideEffect(IDailyClosingService closingService)
    {
        _closingService = closingService;
    }

    public async Task ApplyAsync(Guid businessId, IReadOnlyCollection<DateOnly> saleDates, Guid userId, CancellationToken ct = default)
    {
        foreach (var date in saleDates.Distinct())
        {
            await _closingService.RecalculateAsync(businessId, date, userId, ct);
        }
    }
}
