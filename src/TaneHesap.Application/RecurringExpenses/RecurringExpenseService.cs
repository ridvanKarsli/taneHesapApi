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
        Validate(request.Name, request.Amount, request.IntervalCount);
        var entity = new RecurringExpense
        {
            BusinessId = businessId,
            Name = request.Name.Trim(),
            Amount = request.Amount,
            Period = request.Period,
            IntervalCount = request.IntervalCount,
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

        Validate(request.Name, request.Amount, request.IntervalCount);
        entity.Name = request.Name.Trim();
        entity.Amount = request.Amount;
        entity.Period = request.Period;
        entity.IntervalCount = request.IntervalCount;
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
        if (request.PaidAmount <= 0)
        {
            throw new ValidationAppException("Ödenen tutar 0'dan büyük olmalı.");
        }

        if (request.PeriodEndDate < request.PeriodStartDate)
        {
            throw new ValidationAppException("Dönem bitişi başlangıçtan önce olamaz.");
        }

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
        var expense = await _autoExpenses.UpsertAsync(new AutoExpenseSpec(
            businessId, PaymentSourceType, payment.Id, PaymentExpenseTypeName, ExpenseCategory.Other,
            request.PaidAmount, request.PaidDate, request.PaymentMethod, request.PaymentCardId,
            $"{entity.Name} — {request.PeriodStartDate:dd.MM.yyyy}–{request.PeriodEndDate:dd.MM.yyyy} dönemi (otomatik)",
            updatedByUserId), ct);
        payment.PaymentCardId = expense.PaymentCardId; // Kart doğrulaması gider yazıcısında tek yerde yapılır.

        await _unitOfWork.SaveChangesAsync(ct);

        return await BuildDtoAsync(entity, ct);
    }

    /// <summary>Dönem bitimine bu kadar gün kala ödenmemiş düzenli gider hatırlatılır.</summary>
    private const int ReminderLeadDays = 3;

    /// <summary>
    /// Hatırlatılacak kayıtlar: (1) içinde bulunulan dönemin sonuna <see cref="ReminderLeadDays"/> gün veya daha az
    /// kaldı ve ödenmedi; (2) bir önceki dönem hiç ödenmedi (gecikmiş). Görev günlük çalıştığı için "yalnızca
    /// dönemin son günü" gibi tek güne bağlı bir kural kaçırılırdı; gecikmiş dönem de aksi halde hiç bildirilmezdi.
    /// Aynı dönem için tekrar bildirim, mesajdaki dönem tarihleri üzerinden hatırlatma servisinde engellenir.
    /// </summary>
    public async Task<List<RecurringExpenseDto>> GetDueForReminderAsync(Guid businessId, CancellationToken ct = default)
    {
        var items = await _unitOfWork.Repository<RecurringExpense>().ListAsync(r => r.BusinessId == businessId && r.IsActive, ct);
        var today = BusinessClock.Today;
        var result = new List<RecurringExpenseDto>();

        foreach (var item in items)
        {
            foreach (var (start, end, isOverdue) in await UnpaidPeriodsAsync(item, today, ct))
            {
                if (isOverdue || end.DayNumber - today.DayNumber <= ReminderLeadDays)
                {
                    result.Add(ToDto(item, start, end, isPaid: false));
                }
            }
        }

        return result;
    }

    public async Task<List<RecurringPayableDto>> GetPayablesAsync(Guid businessId, CancellationToken ct = default)
    {
        var items = await _unitOfWork.Repository<RecurringExpense>().ListAsync(r => r.BusinessId == businessId && r.IsActive, ct);
        var today = BusinessClock.Today;
        var result = new List<RecurringPayableDto>();

        foreach (var item in items)
        {
            foreach (var (start, end, isOverdue) in await UnpaidPeriodsAsync(item, today, ct))
            {
                result.Add(new RecurringPayableDto(item.Id, item.Name, item.Amount, item.Period, item.IntervalCount, start, end, isOverdue));
            }
        }

        return result.OrderByDescending(p => p.IsOverdue).ThenBy(p => p.PeriodEndDate).ThenBy(p => p.Name).ToList();
    }

    /// <summary>Ödenmemiş dönemler: bir önceki dönem (gecikmiş) ve içinde bulunulan dönem. Başlangıçtan önce dönem yoktur.</summary>
    private async Task<List<(DateOnly Start, DateOnly End, bool IsOverdue)>> UnpaidPeriodsAsync(RecurringExpense item, DateOnly today, CancellationToken ct)
    {
        var schedule = ScheduleOf(item);
        var index = schedule.IndexAt(today);
        var unpaid = new List<(DateOnly, DateOnly, bool)>();

        if (index > 0)
        {
            var (previousStart, previousEnd) = schedule.PeriodAt(index - 1);
            if (!await IsPeriodPaidAsync(item.Id, previousStart, ct))
            {
                unpaid.Add((previousStart, previousEnd, true));
            }
        }

        var (start, end) = schedule.PeriodAt(index);
        if (start <= today && !await IsPeriodPaidAsync(item.Id, start, ct))
        {
            unpaid.Add((start, end, false));
        }

        return unpaid;
    }

    private static RecurringSchedule ScheduleOf(RecurringExpense item) => new(item.StartDate, item.Period, item.IntervalCount);

    private static void Validate(string name, decimal amount, int intervalCount)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationAppException("Düzenli giderin adı boş olamaz.");
        }

        if (amount <= 0)
        {
            throw new ValidationAppException("Tutar 0'dan büyük olmalı.");
        }

        if (intervalCount is < 1 or > RecurringSchedule.MaxIntervalCount)
        {
            throw new ValidationAppException($"Periyot 1 ile {RecurringSchedule.MaxIntervalCount} arasında olmalı (örn. 3 ayda bir).");
        }
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
        var (periodStart, periodEnd) = ScheduleOf(entity).PeriodContaining(BusinessClock.Today);
        return ToDto(entity, periodStart, periodEnd, await IsPeriodPaidAsync(entity.Id, periodStart, ct));
    }

    private Task<bool> IsPeriodPaidAsync(Guid recurringExpenseId, DateOnly periodStart, CancellationToken ct) =>
        _unitOfWork.Repository<RecurringExpensePayment>()
            .AnyAsync(p => p.RecurringExpenseId == recurringExpenseId && p.PeriodStartDate == periodStart && p.IsPaid, ct);

    private static RecurringExpenseDto ToDto(RecurringExpense entity, DateOnly periodStart, DateOnly periodEnd, bool isPaid) =>
        new(entity.Id, entity.Name, entity.Amount, entity.Period, entity.IntervalCount, entity.StartDate, entity.IsActive, periodStart, periodEnd, isPaid);
}
