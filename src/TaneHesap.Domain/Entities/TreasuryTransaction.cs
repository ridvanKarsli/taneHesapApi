using TaneHesap.Domain.Common;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Domain.Entities;

/// <summary>
/// İşletme kasasının (nakit kasası, kart kasası/banka, kredi kartları) tek doğruluk kaynağı olan
/// hareket defteri. Bakiyeler bu tablonun toplamından türetilir; hiçbir yerde ayrıca saklanmaz.
/// Satış geliri, gider ödemesi, transfer, kart ödemesi ve manuel düzeltme hepsi buraya yazılır.
/// bkz. Proje Raporu bölüm 3.15.
/// </summary>
public class TreasuryTransaction : BaseEntity, ITenantEntity
{
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public TreasuryAccount Account { get; set; }

    /// <summary>Account = CreditCard ise ilgili kart.</summary>
    public Guid? PaymentCardId { get; set; }
    public PaymentCard? PaymentCard { get; set; }

    /// <summary>Pozitif = hesaba giriş, negatif = hesaptan çıkış (kredi kartında negatif = limit kullanımı).</summary>
    public decimal Amount { get; set; }

    public TreasuryTransactionKind Kind { get; set; }

    public DateOnly TransactionDate { get; set; }

    public string? Description { get; set; }

    /// <summary>Hareketin kaynağı (örn. "Expense", "DailySales", "Transfer") — idempotent yeniden hesaplama ve izlenebilirlik için.</summary>
    public string? SourceReferenceType { get; set; }
    public Guid? SourceReferenceId { get; set; }
}
