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
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", parts)));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
