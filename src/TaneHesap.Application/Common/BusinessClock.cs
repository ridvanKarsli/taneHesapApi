namespace TaneHesap.Application.Common;

/// <summary>
/// İşletmenin "bugün"ü Türkiye saatine göredir (sunucu Railway'de UTC çalışır). 00:00–03:00 arasında UTC tarihi
/// bir gün geride kalır; düzenli gider dönemi, hatırlatma ve ay kapanışı bu yüzden yerel tarihi kullanır.
/// </summary>
public static class BusinessClock
{
    private static readonly TimeZoneInfo Turkey = ResolveTurkey();

    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Turkey));

    private static TimeZoneInfo ResolveTurkey()
    {
        foreach (var id in new[] { "Europe/Istanbul", "Turkey Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // sıradaki kimliği dene
            }
            catch (InvalidTimeZoneException)
            {
                // sıradaki kimliği dene
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("Turkey", TimeSpan.FromHours(3), "Türkiye Saati", "Türkiye Saati");
    }
}
