using TaneHesap.Application.Common;
using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Treasury;

public class TreasuryFundsGuard : ITreasuryFundsGuard
{
    private readonly IUnitOfWork _unitOfWork;

    public TreasuryFundsGuard(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task EnsureAvailableAsync(Guid businessId, IReadOnlyCollection<TreasuryTransaction> removed,
        IReadOnlyCollection<TreasuryTransaction> added, CancellationToken ct = default)
    {
        var outflows = removed.Select(t => (Row: t, Amount: -t.Amount))
            .Concat(added.Select(t => (Row: t, Amount: t.Amount)))
            .Where(x => x.Row.Account != TreasuryAccount.Cash)
            .GroupBy(x => (x.Row.Account, CardId: x.Row.Account == TreasuryAccount.CreditCard ? x.Row.PaymentCardId : null))
            .Select(g => (g.Key.Account, g.Key.CardId, Net: g.Sum(x => x.Amount)))
            .Where(b => b.Net < 0);

        foreach (var (account, cardId, net) in outflows)
        {
            if (account == TreasuryAccount.Bank)
            {
                await EnsureBankAsync(businessId, -net, ct);
            }
            else if (cardId is Guid id)
            {
                await EnsureCardAsync(id, -net, ct);
            }
        }
    }

    private async Task EnsureBankAsync(Guid businessId, decimal outflow, CancellationToken ct)
    {
        var balance = await _unitOfWork.Repository<TreasuryTransaction>()
            .SumAsync(t => t.BusinessId == businessId && t.Account == TreasuryAccount.Bank, t => t.Amount, ct);
        if (balance - outflow < 0)
        {
            throw new ValidationAppException(
                $"Banka hesabında yeterli para yok: bakiye {MoneyText.Format(balance)}, bu işlemle çıkacak {MoneyText.Format(outflow)}. "
                + "Nakitten ödeyin ya da önce Kasa → Transfer ile banka hesabına para aktarın.");
        }
    }

    private async Task EnsureCardAsync(Guid cardId, decimal outflow, CancellationToken ct)
    {
        var card = await _unitOfWork.Repository<PaymentCard>().GetByIdAsync(cardId, ct)
            ?? throw new NotFoundException(nameof(PaymentCard), cardId);
        var used = -await _unitOfWork.Repository<TreasuryTransaction>().SumAsync(t => t.PaymentCardId == cardId, t => t.Amount, ct);
        var available = card.Limit - used;
        if (outflow > available)
        {
            throw new ValidationAppException(
                $"“{card.Name}” kartının limiti yetmiyor: kalan limit {MoneyText.Format(Math.Max(available, 0))}, bu işlemle çıkacak {MoneyText.Format(outflow)}. "
                + "Başka bir kart ya da ödeme şekli seçin veya önce kart borcunu ödeyin.");
        }
    }
}
