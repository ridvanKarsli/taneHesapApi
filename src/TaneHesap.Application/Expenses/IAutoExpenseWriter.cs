using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Expenses;

/// <summary>
/// Sistemin kendi ürettiği bir giderin tanımı. <paramref name="SourceType"/> + <paramref name="SourceId"/>
/// kaynağı tekil tanımlar; aynı kaynak için tekrar yazıldığında yeni satır değil güncelleme olur.
/// </summary>
public record AutoExpenseSpec(
    Guid BusinessId,
    string SourceType,
    Guid SourceId,
    string ExpenseTypeName,
    ExpenseCategory Category,
    decimal Amount,
    DateOnly Date,
    PaymentMethod? PaymentMethod,
    Guid? PaymentCardId,
    string Description,
    Guid UserId);

/// <summary>
/// Paranın çıktığı her olay (platform komisyonu, kart komisyonu, düzenli gider ödemesi, tedarikçi ödemesi,
/// personel ödemesi) tek bir yerde <see cref="Expense"/>'e dönüşür: gider türü get-or-create, kaynak başına
/// idempotent upsert, kasa hareketi (<c>IExpenseTreasuryPoster</c>). Böylece raporlar, tabak başı maliyet
/// ve kasa her para çıkışını aynı kanaldan görür. SaveChanges çağırmaz; çağıran kaydeder.
/// </summary>
public interface IAutoExpenseWriter
{
    Task<Expense> UpsertAsync(AutoExpenseSpec spec, CancellationToken ct = default);

    /// <summary>Kaynağa bağlı gideri (ve kasa hareketini) kaldırır; yoksa hiçbir şey yapmaz.</summary>
    Task RemoveAsync(Guid businessId, string sourceType, Guid sourceId, CancellationToken ct = default);
}
