using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Treasury;

/// <summary>
/// Bir ödemenin kart bilgisini doğrular: kartla ödemede kart zorunlu, işletmeye ait ve aktif olmalı; diğer
/// ödeme şekillerinde kart yok sayılır. Elle girilen giderler, tedarikçi ödemeleri ve düzenli gider ödemeleri
/// aynı kuraldan geçer — kural tek yerde yaşar.
/// </summary>
public interface IPaymentCardResolver
{
    Task<Guid?> ResolveAsync(Guid businessId, PaymentMethod? paymentMethod, Guid? paymentCardId, CancellationToken ct = default);
}
