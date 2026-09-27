using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.Treasury;

public record PaymentCardDto(Guid Id, string Name, decimal Limit, decimal UsedAmount, decimal AvailableLimit, bool IsActive);

public record CreatePaymentCardRequest(string Name, decimal Limit);

public record UpdatePaymentCardRequest(string Name, decimal Limit, bool IsActive);

/// <summary>Panelde gösterilen kasa özeti: nakit kasası, banka hesabı ve kartların limit durumu.</summary>
public record TreasurySummaryDto(decimal CashBalance, decimal BankBalance, decimal CardFeePercentage, List<PaymentCardDto> Cards);

public record TreasuryTransactionDto(
    Guid Id,
    TreasuryAccount Account,
    Guid? PaymentCardId,
    string? PaymentCardName,
    decimal Amount,
    TreasuryTransactionKind Kind,
    DateOnly TransactionDate,
    string? Description,
    string? SourceReferenceType,
    Guid? SourceReferenceId,
    DateTime CreatedAtUtc);

public record TreasuryTransactionFilter(DateOnly? FromDate, DateOnly? ToDate, TreasuryAccount? Account, Guid? PaymentCardId);

/// <summary>Nakit kasası ↔ banka hesabı transferi (From ve To yalnızca Cash/Bank olabilir ve farklı olmalıdır).</summary>
public record TransferRequest(TreasuryAccount From, TreasuryAccount To, decimal Amount, DateOnly Date, string? Note);

/// <summary>Kredi kartı borcu ödemesi: banka hesabından (varsayılan) veya nakitten çıkar, kartın limiti geri açılır.</summary>
public record CardPaymentRequest(Guid PaymentCardId, decimal Amount, DateOnly Date, TreasuryAccount Source, string? Note);

/// <summary>Bakiyeyi ayarla: Cash/Bank için hesabın olması gereken bakiyesi (≥ 0), CreditCard için kartın güncel borcu (0..limit).</summary>
public record SetBalanceRequest(TreasuryAccount Account, Guid? PaymentCardId, decimal Balance, DateOnly Date, string? Note);

public record UpdateTreasurySettingsRequest(decimal CardFeePercentage);
