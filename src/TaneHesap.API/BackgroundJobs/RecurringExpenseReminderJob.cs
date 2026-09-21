using TaneHesap.API.Services;
using TaneHesap.Application.RecurringExpenses;

namespace TaneHesap.API.BackgroundJobs;

/// <summary>
/// Düzenli gider hatırlatmalarını periyodik olarak üretir (bkz. Proje Raporu bölüm 3.8). Uygulama
/// açılışından kısa süre sonra bir kez, ardından her <see cref="Interval"/> aralığında çalışır. Asıl iş
/// kuralı Application katmanındaki <see cref="IRecurringExpenseReminderService"/>'tedir; bu sınıf yalnızca
/// zamanlama ve sistem kapsamını (tenant filtresi olmadan) sağlar.
/// </summary>
public class RecurringExpenseReminderJob : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecurringExpenseReminderJob> _logger;

    public RecurringExpenseReminderJob(IServiceScopeFactory scopeFactory, ILogger<RecurringExpenseReminderJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Uygulama kapanıyor.
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<SystemExecutionScope>().EnterSystemMode();

            var reminderService = scope.ServiceProvider.GetRequiredService<IRecurringExpenseReminderService>();
            var sent = await reminderService.SendDueRemindersAsync(ct);
            if (sent > 0)
            {
                _logger.LogInformation("{Count} düzenli gider hatırlatması gönderildi.", sent);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Tek bir çalıştırmanın hatası görevi durdurmamalı; bir sonraki periyotta tekrar denenir.
            _logger.LogError(ex, "Düzenli gider hatırlatma görevi başarısız oldu.");
        }
    }
}
