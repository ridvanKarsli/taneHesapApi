using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TaneHesap.Application.Common;

/// <summary>
/// Doğal bir Guid'i olmayan kaynaklar (örn. "şu platformun şu günkü komisyonu") için aynı girdiden her
/// zaman aynı Guid'i üretir — otomatik giderlerin kaynak başına tek kayıt (idempotent) kuralı için.
/// </summary>
public static class DeterministicGuid
{
    public static Guid From(params object[] parts)
    {
        // Kültürden bağımsız metin: DateOnly/decimal gibi değerler sunucu diline göre farklı yazılırsa
        // (tr-TR "26.09.2026" / invariant "09/26/2026") aynı kaynak farklı anahtar alır ve gider iki kez yazılırdı.
        var key = string.Join("|", parts.Select(p => p switch
        {
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => p?.ToString() ?? string.Empty
        }));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
