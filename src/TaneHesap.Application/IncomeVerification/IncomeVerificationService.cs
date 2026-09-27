using TaneHesap.Application.Common;
using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.DailySales;
using TaneHesap.Domain.Entities;

namespace TaneHesap.Application.IncomeVerification;

public class IncomeVerificationService : IIncomeVerificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReadOnlyList<IIncomeDependentSideEffect> _incomeSideEffects;

    public IncomeVerificationService(IUnitOfWork unitOfWork, IEnumerable<IDailySalesSideEffect> sideEffects)
    {
        _unitOfWork = unitOfWork;
        // Kayıt sırası korunur (önce kasaya gelir, sonra POS komisyonu).
        _incomeSideEffects = sideEffects.OfType<IIncomeDependentSideEffect>().ToList();
    }

    public async Task<IncomeVerificationDayDto> GetDayAsync(Guid businessId, DateOnly date, CancellationToken ct = default)
    {
        var sales = await SalesOfAsync(businessId, date, date, ct);
        var verification = await FindAsync(businessId, date, ct);
        return ToDto(date, sales, verification);
    }

    public async Task<List<IncomeVerificationDayDto>> GetVerifiedAsync(Guid businessId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var verifications = await InStoreIncome.LoadAsync(_unitOfWork, businessId, fromDate, toDate, ct);
        if (verifications.Count == 0)
        {
            return new List<IncomeVerificationDayDto>();
        }

        var salesByDate = (await SalesOfAsync(businessId, fromDate, toDate, ct)).ToLookup(s => s.SaleDate);
        return verifications.Values
            .OrderByDescending(v => v.VerificationDate)
            .Select(v => ToDto(v.VerificationDate, salesByDate[v.VerificationDate].ToList(), v))
            .ToList();
    }

    public async Task<IncomeVerificationDayDto> SaveAsync(Guid businessId, DateOnly date, SaveIncomeVerificationRequest request, Guid userId, CancellationToken ct = default)
    {
        if (request.ActualCash < 0 || request.ActualCard < 0)
        {
            throw new ValidationAppException("Gerçek nakit ve kart geliri negatif olamaz.");
        }

        if (date > BusinessClock.Today)
        {
            throw new ValidationAppException("İleri bir tarih doğrulanamaz.");
        }

        var sales = await SalesOfAsync(businessId, date, date, ct);
        if (!InStoreIncome.HasInStoreSales(sales))
        {
            throw new ValidationAppException($"{date:dd.MM.yyyy} için önce Kasa Excel'ini yükleyin; beklenen gelir oradan hesaplanır.");
        }

        var repo = _unitOfWork.Repository<DailyIncomeVerification>();
        var verification = await FindAsync(businessId, date, ct);
        if (verification is null)
        {
            verification = new DailyIncomeVerification { BusinessId = businessId, VerificationDate = date, CreatedByUserId = userId };
            await repo.AddAsync(verification, ct);
        }
        else
        {
            verification.UpdatedByUserId = userId;
            verification.UpdatedAtUtc = DateTime.UtcNow;
            repo.Update(verification);
        }

        verification.ActualCash = MoneyMath.Round(request.ActualCash);
        verification.ActualCard = MoneyMath.Round(request.ActualCard);
        verification.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        await _unitOfWork.SaveChangesAsync(ct);

        await RepostAsync(businessId, date, userId, ct);
        return ToDto(date, sales, verification);
    }

    public async Task DeleteAsync(Guid businessId, DateOnly date, Guid userId, CancellationToken ct = default)
    {
        var verification = await FindAsync(businessId, date, ct)
            ?? throw new NotFoundException("Gelir doğrulaması", date.ToString("dd.MM.yyyy"));

        _unitOfWork.Repository<DailyIncomeVerification>().Remove(verification);
        await _unitOfWork.SaveChangesAsync(ct);
        await RepostAsync(businessId, date, userId, ct);
    }

    /// <summary>Kasadaki gelir ve POS komisyonu, o günün yeni etkin gelirine göre yeniden yazılır.</summary>
    private async Task RepostAsync(Guid businessId, DateOnly date, Guid userId, CancellationToken ct)
    {
        foreach (var sideEffect in _incomeSideEffects)
        {
            await sideEffect.ApplyAsync(businessId, new[] { date }, userId, ct);
        }
    }

    private Task<List<DailySalesEntry>> SalesOfAsync(Guid businessId, DateOnly fromDate, DateOnly toDate, CancellationToken ct)
        => _unitOfWork.Repository<DailySalesEntry>()
            .ListAsync(e => e.BusinessId == businessId && e.SaleDate >= fromDate && e.SaleDate <= toDate, ct);

    private async Task<DailyIncomeVerification?> FindAsync(Guid businessId, DateOnly date, CancellationToken ct)
        => (await _unitOfWork.Repository<DailyIncomeVerification>()
            .ListAsync(v => v.BusinessId == businessId && v.VerificationDate == date, ct)).FirstOrDefault();

    private static IncomeVerificationDayDto ToDto(DateOnly date, IReadOnlyCollection<DailySalesEntry> daySales, DailyIncomeVerification? verification)
    {
        var expected = InStoreIncome.Expected(daySales);
        if (verification is null)
        {
            return new IncomeVerificationDayDto(date, InStoreIncome.HasInStoreSales(daySales), expected.Cash, expected.Card,
                false, null, null, null, null, null, null);
        }

        return new IncomeVerificationDayDto(date, InStoreIncome.HasInStoreSales(daySales), expected.Cash, expected.Card,
            true, verification.ActualCash, verification.ActualCard,
            verification.ActualCash - expected.Cash, verification.ActualCard - expected.Card,
            verification.Note, verification.UpdatedAtUtc ?? verification.CreatedAtUtc);
    }
}
