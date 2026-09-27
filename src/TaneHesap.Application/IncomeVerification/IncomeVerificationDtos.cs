namespace TaneHesap.Application.IncomeVerification;

/// <summary>Bir günün doğrulama durumu: beklenen (Kasa Excel'i), girilen gerçek tutarlar ve fark (gerçek − beklenen).</summary>
public record IncomeVerificationDayDto(
    DateOnly Date,
    bool HasInStoreSales,
    decimal ExpectedCash,
    decimal ExpectedCard,
    bool IsVerified,
    decimal? ActualCash,
    decimal? ActualCard,
    decimal? CashDifference,
    decimal? CardDifference,
    string? Note,
    DateTime? VerifiedAtUtc);

public record SaveIncomeVerificationRequest(decimal ActualCash, decimal ActualCard, string? Note);
