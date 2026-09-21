using TaneHesap.Application.Common.Exceptions;

namespace TaneHesap.Application.Common;

/// <summary>
/// Silme işlemlerinde "bu kayıt geçmiş verilerde kullanılıyor mu" kontrolünün tek noktası. Geçmişi
/// olan kayıtlar silinmez (raporlar ve denetim bozulmasın); kullanıcıya pasif yapması önerilir.
/// </summary>
public static class DeletionGuard
{
    public static void EnsureNotUsed(bool isUsed, string subject, string usedIn)
    {
        if (isUsed)
        {
            throw new ConflictAppException($"{subject} {usedIn} kullanıldığı için silinemez. Silmek yerine pasif yapabilirsiniz.");
        }
    }
}
