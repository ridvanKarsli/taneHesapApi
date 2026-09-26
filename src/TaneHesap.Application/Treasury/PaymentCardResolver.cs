using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Treasury;

public class PaymentCardResolver : IPaymentCardResolver
{
    private readonly IUnitOfWork _unitOfWork;

    public PaymentCardResolver(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid?> ResolveAsync(Guid businessId, PaymentMethod? paymentMethod, Guid? paymentCardId, CancellationToken ct = default)
    {
        if (paymentMethod != PaymentMethod.Card)
        {
            return null;
        }

        if (paymentCardId is null)
        {
            throw new ValidationAppException("Kartla ödemede hangi karttan ödendiği seçilmeli.");
        }

        var card = await _unitOfWork.Repository<PaymentCard>().GetByIdAsync(paymentCardId.Value, ct);
        if (card is null || card.BusinessId != businessId)
        {
            throw new NotFoundException(nameof(PaymentCard), paymentCardId.Value);
        }

        if (!card.IsActive)
        {
            throw new ValidationAppException($"'{card.Name}' kartı pasif; bu karta ödeme yazılamaz.");
        }

        return card.Id;
    }
}
