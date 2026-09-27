namespace TaneHesap.Application.IncomeVerification;

/// <summary>Günlük dükkân içi gelir doğrulaması (bkz. <see cref="InStoreIncome"/>).</summary>
public interface IIncomeVerificationService
{
    Task<IncomeVerificationDayDto> GetDayAsync(Guid businessId, DateOnly date, CancellationToken ct = default);

    /// <summary>Aralıktaki doğrulanmış günler, en yeni önce.</summary>
    Task<List<IncomeVerificationDayDto>> GetVerifiedAsync(Guid businessId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);

    Task<IncomeVerificationDayDto> SaveAsync(Guid businessId, DateOnly date, SaveIncomeVerificationRequest request, Guid userId, CancellationToken ct = default);

    Task DeleteAsync(Guid businessId, DateOnly date, Guid userId, CancellationToken ct = default);
}
