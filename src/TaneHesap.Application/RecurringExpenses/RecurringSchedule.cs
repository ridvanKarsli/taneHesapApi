using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.RecurringExpenses;

/// <summary>
/// Düzenli giderin dönem takvimi (saf hesap, veritabanı yok). k'ıncı dönem: başlangıç = Shift(k·N),
/// bitiş = Shift((k+1)·N) − 1 gün; N = "kaç periyotta bir". Kaydırma hep başlangıç tarihinden yapıldığı için
/// ay sonu başlangıçlarında (31 Ocak → 30 Nisan → 31 Temmuz) dönemler arasında boşluk kalmaz.
/// </summary>
public readonly record struct RecurringSchedule(DateOnly StartDate, RecurringPeriod Period, int IntervalCount)
{
    public const int MaxIntervalCount = 24;

    /// <summary><paramref name="date"/> gününü içeren dönemin sırası (başlangıçtan önceki tarihler için 0).</summary>
    public int IndexAt(DateOnly date)
    {
        if (date <= StartDate)
        {
            return 0;
        }

        var step = Math.Max(1, IntervalCount);
        var units = Period switch
        {
            RecurringPeriod.Weekly => (date.DayNumber - StartDate.DayNumber) / 7,
            RecurringPeriod.Monthly => ((date.Year - StartDate.Year) * 12) + (date.Month - StartDate.Month),
            RecurringPeriod.Yearly => date.Year - StartDate.Year,
            _ => 0
        };

        // Birim farkı üst sınırdır; kaydırılmış başlangıç henüz gelmediyse bir önceki dönemdeyiz.
        var index = units / step;
        while (index > 0 && Shift(index) > date)
        {
            index--;
        }

        return index;
    }

    public (DateOnly Start, DateOnly End) PeriodAt(int index) => (Shift(index), Shift(index + 1).AddDays(-1));

    public (DateOnly Start, DateOnly End) PeriodContaining(DateOnly date) => PeriodAt(IndexAt(date));

    private DateOnly Shift(int index)
    {
        var units = index * Math.Max(1, IntervalCount);
        return Period switch
        {
            RecurringPeriod.Weekly => StartDate.AddDays(7 * units),
            RecurringPeriod.Monthly => StartDate.AddMonths(units),
            RecurringPeriod.Yearly => StartDate.AddYears(units),
            _ => StartDate.AddDays(7 * units)
        };
    }
}
