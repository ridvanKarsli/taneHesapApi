namespace TaneHesap.Application.Treasury;

/// <summary>
/// İşletme kasası: nakit kasası, kart kasası (banka/POS hesabı) ve kredi kartları. Bakiyeler
/// <see cref="Domain.Entities.TreasuryTransaction"/> defterinin toplamından türetilir. Satış geliri ve
/// gider ödemeleri buraya ilgili poster'lar üzerinden otomatik yazılır; bu servis ADMIN'in elle yaptığı
/// işlemleri (kart tanımı, transfer, kart ödemesi, düzeltme) ve raporlamayı üstlenir. bkz. Proje Raporu bölüm 3.15.
/// </summary>
public interface ITreasuryService
{
    Task<TreasurySummaryDto> GetSummaryAsync(Guid businessId, CancellationToken ct = default);

    Task<List<TreasuryTransactionDto>> GetTransactionsAsync(Guid businessId, TreasuryTransactionFilter filter, CancellationToken ct = default);

    Task<List<PaymentCardDto>> GetCardsAsync(Guid businessId, CancellationToken ct = default);

    Task<PaymentCardDto> CreateCardAsync(Guid businessId, CreatePaymentCardRequest request, Guid userId, CancellationToken ct = default);

    Task<PaymentCardDto> UpdateCardAsync(Guid businessId, Guid cardId, UpdatePaymentCardRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>Hiç hareketi olmayan kart silinebilir; aksi halde pasif yapılması önerilir.</summary>
    Task DeleteCardAsync(Guid businessId, Guid cardId, CancellationToken ct = default);

    Task<List<TreasuryTransactionDto>> TransferAsync(Guid businessId, TransferRequest request, Guid userId, CancellationToken ct = default);

    Task<List<TreasuryTransactionDto>> PayCardAsync(Guid businessId, CardPaymentRequest request, Guid userId, CancellationToken ct = default);

    Task<TreasuryTransactionDto> AdjustAsync(Guid businessId, ManualAdjustmentRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>Yalnızca elle girilen hareketler (transfer, kart ödemesi, düzeltme) silinebilir; satış/gider kaynaklılar kendi modülünden yönetilir.</summary>
    Task DeleteTransactionAsync(Guid businessId, Guid transactionId, CancellationToken ct = default);

    Task<TreasurySummaryDto> UpdateSettingsAsync(Guid businessId, UpdateTreasurySettingsRequest request, Guid userId, CancellationToken ct = default);
}
