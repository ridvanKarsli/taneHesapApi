using TaneHesap.API.Services;
using TaneHesap.Application.Reports;

namespace TaneHesap.API.BackgroundJobs;

/// <summary>
/// Her ay sonunda aylık raporu üretip ADMIN'lere bildirir (bkz. Proje Raporu bölüm 3.16). Günde bir kez
/// çalışır; biten ve henüz kapatılmamış ay varsa <see cref="IMonthlyReportService.CloseFinishedMonthsAsync"/>
/// onu kapatır — idempotent olduğu için uygulamanın yeniden başlaması veya birkaç gün kapalı kalması sorun değildir.
/// </summary>
public class MonthlyReportJob : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MonthlyReportJob> _logger;

    public MonthlyReportJob(IServiceScopeFactory scopeFactory, ILogger<MonthlyReportJob> logger)
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

            var service = scope.ServiceProvider.GetRequiredService<IMonthlyReportService>();
            var closed = await service.CloseFinishedMonthsAsync(DateOnly.FromDateTime(DateTime.UtcNow), ct);
            if (closed > 0)
            {
                _logger.LogInformation("{Count} işletme için aylık rapor kapatıldı ve bildirildi.", closed);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Aylık rapor görevi başarısız oldu.");
        }
    }
}
