using System.Globalization;

namespace TaneHesap.Application.Common;

/// <summary>Kullanıcıya dönen mesajlarda tutar biçimi: "₺1.250,50" (sunucunun kültüründen bağımsız).</summary>
public static class MoneyText
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static string Format(decimal amount) => "₺" + amount.ToString("N2", Tr);
}
