namespace TaneHesap.Application.Common;

/// <summary>
/// Para yuvarlama tek yerde: 2 basamak, yarımlar yukarı (AwayFromZero). <c>Math.Round</c>'un varsayılanı
/// "bankacı yuvarlaması"dır (0,125 → 0,12) ve kasa/fiş tutarlarıyla çelişir.
/// </summary>
public static class MoneyMath
{
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
