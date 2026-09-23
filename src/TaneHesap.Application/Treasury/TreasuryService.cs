using TaneHesap.Application.Common;
using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Treasury;

public class TreasuryService : ITreasuryService
{
    private const string ManualSourceType = "Manual";

    /// <summary>ADMIN'in elle girdiği ve bu servisten silinebilen hareket türleri.</summary>
    private static readonly TreasuryTransactionKind[] ManualKinds =
    {
        TreasuryTransactionKind.Transfer, TreasuryTransactionKind.CardPayment, TreasuryTransactionKind.ManualAdjustment
    };

    private readonly IUnitOfWork _unitOfWork;

    public TreasuryService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<TreasurySummaryDto> GetSummaryAsync(Guid businessId, CancellationToken ct = default)
    {
        var business = await _unitOfWork.Repository<Business>().GetByIdAsync(businessId, ct)
            ?? throw new NotFoundException(nameof(Business), businessId);
        var transactions = await _unitOfWork.Repository<TreasuryTransaction>().ListAsync(t => t.BusinessId == businessId, ct);
        var cards = await _unitOfWork.Repository<PaymentCard>().ListAsync(c => c.BusinessId == businessId, ct);

        return new TreasurySummaryDto(
            transactions.Where(t => t.Account == TreasuryAccount.Cash).Sum(t => t.Amount),
            transactions.Where(t => t.Account == TreasuryAccount.Bank).Sum(t => t.Amount),
            business.CardFeePercentage,
            cards.OrderBy(c => c.Name).Select(c => ToDto(c, transactions)).ToList());
    }

    public async Task<List<TreasuryTransactionDto>> GetTransactionsAsync(Guid businessId, TreasuryTransactionFilter filter, CancellationToken ct = default)
    {
        var transactions = await _unitOfWork.Repository<TreasuryTransaction>().ListAsync(t => t.BusinessId == businessId, ct);
        var cardNames = (await _unitOfWork.Repository<PaymentCard>().ListAsync(c => c.BusinessId == businessId, ct))
            .ToDictionary(c => c.Id, c => c.Name);

        var query = transactions.AsEnumerable();
        if (filter.FromDate.HasValue) query = query.Where(t => t.TransactionDate >= filter.FromDate.Value);
        if (filter.ToDate.HasValue) query = query.Where(t => t.TransactionDate <= filter.ToDate.Value);
        if (filter.Account.HasValue) query = query.Where(t => t.Account == filter.Account.Value);
        if (filter.PaymentCardId.HasValue) query = query.Where(t => t.PaymentCardId == filter.PaymentCardId.Value);

        return query
            .OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.CreatedAtUtc)
            .Select(t => ToDto(t, cardNames))
            .ToList();
    }

    public async Task<List<PaymentCardDto>> GetCardsAsync(Guid businessId, CancellationToken ct = default)
    {
        var cards = await _unitOfWork.Repository<PaymentCard>().ListAsync(c => c.BusinessId == businessId, ct);
        var transactions = await _unitOfWork.Repository<TreasuryTransaction>()
            .ListAsync(t => t.BusinessId == businessId && t.Account == TreasuryAccount.CreditCard, ct);
        return cards.OrderBy(c => c.Name).Select(c => ToDto(c, transactions)).ToList();
    }

    public async Task<PaymentCardDto> CreateCardAsync(Guid businessId, CreatePaymentCardRequest request, Guid userId, CancellationToken ct = default)
    {
        EnsureNonNegative(request.Limit, "Kart limiti");
        var card = new PaymentCard { BusinessId = businessId, Name = request.Name.Trim(), Limit = request.Limit, CreatedByUserId = userId };
        await _unitOfWork.Repository<PaymentCard>().AddAsync(card, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return ToDto(card, Array.Empty<TreasuryTransaction>());
    }

    public async Task<PaymentCardDto> UpdateCardAsync(Guid businessId, Guid cardId, UpdatePaymentCardRequest request, Guid userId, CancellationToken ct = default)
    {
        EnsureNonNegative(request.Limit, "Kart limiti");
        var card = await GetCardAsync(businessId, cardId, ct);
        card.Name = request.Name.Trim();
        card.Limit = request.Limit;
        card.IsActive = request.IsActive;
        card.UpdatedByUserId = userId;
        card.UpdatedAtUtc = DateTime.UtcNow;
        _unitOfWork.Repository<PaymentCard>().Update(card);
        await _unitOfWork.SaveChangesAsync(ct);

        var transactions = await _unitOfWork.Repository<TreasuryTransaction>().ListAsync(t => t.PaymentCardId == cardId, ct);
        return ToDto(card, transactions);
    }

    public async Task DeleteCardAsync(Guid businessId, Guid cardId, CancellationToken ct = default)
    {
        var card = await GetCardAsync(businessId, cardId, ct);
        DeletionGuard.EnsureNotUsed(
            await _unitOfWork.Repository<TreasuryTransaction>().AnyAsync(t => t.PaymentCardId == cardId, ct)
            || await _unitOfWork.Repository<Expense>().AnyAsync(e => e.PaymentCardId == cardId, ct),
            "Kart", "giderlerde/kasa hareketlerinde");

        _unitOfWork.Repository<PaymentCard>().Remove(card);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<List<TreasuryTransactionDto>> TransferAsync(Guid businessId, TransferRequest request, Guid userId, CancellationToken ct = default)
    {
        EnsurePositive(request.Amount, "Transfer tutarı");
        if (request.From == request.To || !IsRegister(request.From) || !IsRegister(request.To))
        {
            throw new ValidationAppException("Transfer yalnızca nakit kasası ile kart kasası arasında yapılabilir.");
        }

        var note = request.Note ?? $"{Label(request.From)} → {Label(request.To)} transferi";
        var groupId = Guid.NewGuid();
        var rows = new[]
        {
            NewManual(businessId, request.From, null, -request.Amount, TreasuryTransactionKind.Transfer, request.Date, note, groupId, userId),
            NewManual(businessId, request.To, null, request.Amount, TreasuryTransactionKind.Transfer, request.Date, note, groupId, userId)
        };

        return await PersistAsync(businessId, rows, ct);
    }

    public async Task<List<TreasuryTransactionDto>> PayCardAsync(Guid businessId, CardPaymentRequest request, Guid userId, CancellationToken ct = default)
    {
        EnsurePositive(request.Amount, "Kart ödemesi tutarı");
        if (!IsRegister(request.Source))
        {
            throw new ValidationAppException("Kart ödemesi nakit kasasından veya kart kasasından yapılabilir.");
        }

        var card = await GetCardAsync(businessId, request.PaymentCardId, ct);
        var note = request.Note ?? $"{card.Name} kart borcu ödemesi ({Label(request.Source)})";
        var groupId = Guid.NewGuid();
        var rows = new[]
        {
            NewManual(businessId, request.Source, null, -request.Amount, TreasuryTransactionKind.CardPayment, request.Date, note, groupId, userId),
            NewManual(businessId, TreasuryAccount.CreditCard, card.Id, request.Amount, TreasuryTransactionKind.CardPayment, request.Date, note, groupId, userId)
        };

        return await PersistAsync(businessId, rows, ct);
    }

    public async Task<TreasuryTransactionDto> AdjustAsync(Guid businessId, ManualAdjustmentRequest request, Guid userId, CancellationToken ct = default)
    {
        if (request.Amount == 0)
        {
            throw new ValidationAppException("Düzeltme tutarı 0 olamaz.");
        }

        Guid? cardId = null;
        if (request.Account == TreasuryAccount.CreditCard)
        {
            cardId = (await GetCardAsync(businessId, request.PaymentCardId ?? Guid.Empty, ct)).Id;
        }

        var row = NewManual(businessId, request.Account, cardId, request.Amount, TreasuryTransactionKind.ManualAdjustment,
            request.Date, request.Note ?? "Manuel düzeltme / açılış bakiyesi", Guid.NewGuid(), userId);
        return (await PersistAsync(businessId, new[] { row }, ct))[0];
    }

    public async Task DeleteTransactionAsync(Guid businessId, Guid transactionId, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<TreasuryTransaction>();
        var transaction = await repo.GetByIdAsync(transactionId, ct);
        if (transaction is null || transaction.BusinessId != businessId)
        {
            throw new NotFoundException(nameof(TreasuryTransaction), transactionId);
        }

        if (!ManualKinds.Contains(transaction.Kind))
        {
            throw new ConflictAppException("Satış veya gider kaynaklı kasa hareketleri buradan silinemez; ilgili satışı/gideri düzenleyin.");
        }

        // Transfer ve kart ödemesi çift kayıttır (çıkış + giriş); ikisi aynı SourceReferenceId ile bağlıdır ve birlikte silinir.
        foreach (var row in await repo.ListAsync(t => t.SourceReferenceType == ManualSourceType && t.SourceReferenceId == transaction.SourceReferenceId, ct))
        {
            repo.Remove(row);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<TreasurySummaryDto> UpdateSettingsAsync(Guid businessId, UpdateTreasurySettingsRequest request, Guid userId, CancellationToken ct = default)
    {
        if (request.CardFeePercentage is < 0 or > 100)
        {
            throw new ValidationAppException("Kart komisyon yüzdesi 0-100 arasında olmalı.");
        }

        var business = await _unitOfWork.Repository<Business>().GetByIdAsync(businessId, ct)
            ?? throw new NotFoundException(nameof(Business), businessId);
        business.CardFeePercentage = request.CardFeePercentage;
        business.UpdatedByUserId = userId;
        business.UpdatedAtUtc = DateTime.UtcNow;
        _unitOfWork.Repository<Business>().Update(business);
        await _unitOfWork.SaveChangesAsync(ct);

        return await GetSummaryAsync(businessId, ct);
    }

    private async Task<List<TreasuryTransactionDto>> PersistAsync(Guid businessId, IEnumerable<TreasuryTransaction> rows, CancellationToken ct)
    {
        var list = rows.ToList();
        foreach (var row in list)
        {
            await _unitOfWork.Repository<TreasuryTransaction>().AddAsync(row, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        var cardNames = (await _unitOfWork.Repository<PaymentCard>().ListAsync(c => c.BusinessId == businessId, ct))
            .ToDictionary(c => c.Id, c => c.Name);
        return list.Select(t => ToDto(t, cardNames)).ToList();
    }

    private async Task<PaymentCard> GetCardAsync(Guid businessId, Guid cardId, CancellationToken ct)
    {
        var card = await _unitOfWork.Repository<PaymentCard>().GetByIdAsync(cardId, ct);
        if (card is null || card.BusinessId != businessId)
        {
            throw new NotFoundException(nameof(PaymentCard), cardId);
        }

        return card;
    }

    private static TreasuryTransaction NewManual(Guid businessId, TreasuryAccount account, Guid? cardId, decimal amount,
        TreasuryTransactionKind kind, DateOnly date, string note, Guid groupId, Guid userId) => new()
    {
        BusinessId = businessId,
        Account = account,
        PaymentCardId = cardId,
        Amount = amount,
        Kind = kind,
        TransactionDate = date,
        Description = note,
        SourceReferenceType = ManualSourceType,
        SourceReferenceId = groupId,
        CreatedByUserId = userId
    };

    private static bool IsRegister(TreasuryAccount account) => account is TreasuryAccount.Cash or TreasuryAccount.Bank;

    private static string Label(TreasuryAccount account) => account switch
    {
        TreasuryAccount.Cash => "Nakit kasası",
        TreasuryAccount.Bank => "Kart kasası",
        _ => "Kredi kartı"
    };

    private static void EnsurePositive(decimal amount, string subject)
    {
        if (amount <= 0) throw new ValidationAppException($"{subject} 0'dan büyük olmalı.");
    }

    private static void EnsureNonNegative(decimal amount, string subject)
    {
        if (amount < 0) throw new ValidationAppException($"{subject} negatif olamaz.");
    }

    private static PaymentCardDto ToDto(PaymentCard card, IEnumerable<TreasuryTransaction> transactions)
    {
        // Kart hareketleri: gider negatif, ödeme pozitif → toplam, kullanılan limitin negatifi.
        var used = -transactions.Where(t => t.PaymentCardId == card.Id).Sum(t => t.Amount);
        return new PaymentCardDto(card.Id, card.Name, card.Limit, used, card.Limit - used, card.IsActive);
    }

    private static TreasuryTransactionDto ToDto(TreasuryTransaction t, IReadOnlyDictionary<Guid, string> cardNames) => new(
        t.Id, t.Account, t.PaymentCardId,
        t.PaymentCardId.HasValue ? cardNames.GetValueOrDefault(t.PaymentCardId.Value) : null,
        t.Amount, t.Kind, t.TransactionDate, t.Description, t.SourceReferenceType, t.SourceReferenceId, t.CreatedAtUtc);
}
