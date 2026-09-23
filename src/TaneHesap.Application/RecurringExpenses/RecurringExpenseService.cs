using TaneHesap.Application.Common;
using TaneHesap.Application.Common.Exceptions;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.Expenses;
using TaneHesap.Domain.Entities;
using TaneHesap.Domain.Enums;

namespace TaneHesap.Application.RecurringExpenses;

public class RecurringExpenseService : IRecurringExpenseService
{
    public const string PaymentSourceType = "RecurringExpensePayment";
    private const string PaymentExpenseTypeName = "Düzenli Gider";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAutoExpenseWriter _autoExpenses;

    public RecurringExpenseService(IUnitOfWork unitOfWork, IAutoExpenseWriter autoExpenses)
    {
        _unitOfWork = unitOfWork;
        _autoExpenses = autoExpenses;
    }

    public async Task<List<RecurringExpenseDto>> GetAllAsync(Guid businessId, CancellationToken ct = default)
    {
        var items = await _unitOfWork.Repository<RecurringExpense>().ListAsync(r => r.BusinessId == businessId, ct);
        var result = new List<RecurringExpenseDto>();
        foreach (var item in items.OrderBy(r => r.Name))
        {
            result.Add(await BuildDtoAsync(item, ct));
        }

        return result;
    }

    public async Task<RecurringExpenseDto> GetByIdAsync(Guid businessId, Guid id, CancellationToken ct = default)
        => await BuildDtoAsync(await GetTenantScopedAsync(businessId, id, ct), ct);

    public async Task<RecurringExpenseDto> CreateAsync(Guid businessId, CreateRecurringExpenseRequest request, Guid createdByUserId, CancellationToken ct = default)
    {
        var entity = new RecurringExpense
        {
            BusinessId = businessId,
            Name = request.Name,
            Amount = request.Amount,
            Period = request.Period,
            StartDate = request.StartDate,
            IsActive = true,
            CreatedByUserId = createdByUserId
        };

        await _unitOfWork.Repository<RecurringExpense>().AddAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await BuildDtoAsync(entity, ct);
    }

    public async Task<RecurringExpenseDto> UpdateAsync(Guid businessId, Guid id, UpdateRecurringExpenseRequest request, Guid updatedByUserId, CancellationToken ct = default)
    {
        var repo = _unitOfWork.Repository<RecurringExpense>();
        var entity = await GetTenantScopedAsync(businessId, id, ct);

        entity.Name = request.Name;
        entity.Amount = request.Amount;
        entity.Period = request.Period;
        entity.IsActive = request.IsActive;
        entity.UpdatedByUserId = updatedByUserId;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        repo.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return await BuildDtoAsync(entity, ct);
    }

    public async Task<RecurringExpenseDto> MarkPeriodPaidAsync(Guid businessId, Guid id, MarkPeriodPaidRequest request, Guid updatedByUserId, CancellationToken ct = default)
    {
        var entity = await GetTenantScopedAsync(businessId, id, ct);

        var paymentRepo = _unitOfWork.Repository<RecurringExpensePayment>();
        var payments = await paymentRepo.ListAsync(p => p.RecurringExpenseId == entity.Id && p.PeriodStartDate == request.PeriodStartDate, ct);
        var payment = payments.FirstOrDefault();

        if (payment is null)
        {
            payment = new RecurringExpensePayment
            {
                BusinessId = businessId,
                RecurringExpenseId = entity.Id,
                PeriodStartDate = request.PeriodStartDate,
                PeriodEndDate = request.PeriodEndDate,
                IsPaid = true,
                PaidDate = request.PaidDate,
                PaidAmount = request.PaidAmount,
                PaymentMethod = request.PaymentMethod,
                PaymentCardId = request.PaymentCardId,
                CreatedByUserId = updatedByUserId
            };
            await paymentRepo.AddAsync(payment, ct);
        }
        else
        {
            payment.IsPaid = true;
            payment.PaidDate = request.PaidDate;
            payment.PaidAmount = request.PaidAmount;
            payment.PaymentMethod = request.PaymentMethod;
            payment.PaymentCardId = request.PaymentCardId;
            payment.UpdatedByUserId = updatedByUserId;
            payment.UpdatedAtUtc = DateTime.UtcNow;
            paymentRepo.Update(payment);
        }

        // Ödeme, kasadan/karttan düşen ve raporlara giren otomatik bir gider olarak da kaydedilir (dönem başına tek kayıt).
        await _autoExpenses.UpsertAsync(new AutoExpenseSpec(
            businessId, PaymentSourceType, payment.Id, PaymentExpenseTypeName, ExpenseCategory.Other,
            request.PaidAmount, request.PaidDate, request.PaymentMethod, request.PaymentCardId,
            $"{entity.Name} — {request.PeriodStartDate:dd.MM.yyyy}–{request.PeriodEndDate:dd.MM.yyyy} dönemi (otomatik)",
            updatedByUserId), ct);

        await _unitOfWork.SaveChangesAsync(ct);

        return await BuildDtoAsync(entity, ct);
    }

    public async Task<List<RecurringExpenseDto>> GetDueForReminderAsync(Guid businessId, CancellationToken ct = default)
    {
        var items = await _unitOfWork.Repository<RecurringExpense>().ListAsync(r => r.BusinessId == businessId && r.IsActive, ct);
        var result = new List<RecurringExpenseDto>();

        foreach (var item in items)
        {
            var dto = await BuildDtoAsync(item, ct);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (!dto.IsCurrentPeriodPaid && today >= dto.CurrentPeriodEndDate)
            {
                result.Add(dto);
            }
        }

        return result;
    }

    public async Task DeleteAsync(Guid businessId, Guid id, CancellationToken ct = default)
    {
        var entity = await GetTenantScopedAsync(businessId, id, ct);
        DeletionGuard.EnsureNotUsed(
            await _unitOfWork.Repository<RecurringExpensePayment>().AnyAsync(p => p.RecurringExpenseId == id, ct),
            "Bu düzenli gider", "ödeme geçmişinde");

        _unitOfWork.Repository<RecurringExpense>().Remove(entity);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<RecurringExpense> GetTenantScopedAsync(Guid businessId, Guid id, CancellationToken ct)
    {
        var entity = await _unitOfWork.Repository<RecurringExpense>().GetByIdAsync(id, ct);
        if (entity is null || entity.BusinessId != businessId)
        {
            throw new NotFoundException(nameof(RecurringExpense), id);
        }

        return entity;
    }

    private async Task<RecurringExpenseDto> BuildDtoAsync(RecurringExpense entity, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (periodStart, periodEnd) = ComputeCurrentPeriod(entity.StartDate, entity.Period, today);

        var payments = await _unitOfWork.Repository<RecurringExpensePayment>()
            .ListAsync(p => p.RecurringExpenseId == entity.Id && p.PeriodStartDate == periodStart, ct);
        var isPaid = payments.Any(p => p.IsPaid);

        return new RecurringExpenseDto(
            entity.Id, entity.Name, entity.Amount, entity.Period, entity.StartDate, entity.IsActive,
            periodStart, periodEnd, isPaid);
    }

    private static (DateOnly Start, DateOnly End) ComputeCurrentPeriod(DateOnly startDate, RecurringPeriod period, DateOnly asOf)
    {
        if (asOf < startDate)
        {
            return (startDate, GetPeriodEnd(startDate, period));
        }

        var periodStart = period switch
        {
            RecurringPeriod.Weekly => startDate.AddDays(((asOf.DayNumber - startDate.DayNumber) / 7) * 7),
            RecurringPeriod.Monthly => ComputeMonthlyPeriodStart(startDate, asOf),
            RecurringPeriod.Yearly => ComputeYearlyPeriodStart(startDate, asOf),
            _ => startDate
        };

        return (periodStart, GetPeriodEnd(periodStart, period));
    }

    private static DateOnly GetPeriodEnd(DateOnly periodStart, RecurringPeriod period) => period switch
    {
        RecurringPeriod.Weekly => periodStart.AddDays(6),
        RecurringPeriod.Monthly => periodStart.AddMonths(1).AddDays(-1),
        RecurringPeriod.Yearly => periodStart.AddYears(1).AddDays(-1),
        _ => periodStart.AddDays(6)
    };

    private static DateOnly ComputeMonthlyPeriodStart(DateOnly startDate, DateOnly asOf)
    {
        var months = ((asOf.Year - startDate.Year) * 12) + (asOf.Month - startDate.Month);
        var candidate = startDate.AddMonths(months);
        if (candidate > asOf)
        {
            candidate = startDate.AddMonths(months - 1);
        }

        return candidate;
    }

    private static DateOnly ComputeYearlyPeriodStart(DateOnly startDate, DateOnly asOf)
    {
        var years = asOf.Year - startDate.Year;
        var candidate = startDate.AddYears(years);
        if (candidate > asOf)
        {
            candidate = startDate.AddYears(years - 1);
        }

        return candidate;
    }
}
