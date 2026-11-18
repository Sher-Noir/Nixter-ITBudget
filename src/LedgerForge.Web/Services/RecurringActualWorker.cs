using LedgerForge.Infrastructure.Actuals;

namespace LedgerForge.Web.Services;

/// <summary>Runs on application startup and once each hour. IIS should be Always Running.</summary>
public sealed class RecurringActualWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<RecurringActualWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProcessAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ProcessAsync(stoppingToken);
    }

    private async Task ProcessAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<RecurringActualService>();
            var posted = await service.PostDueAsync(RecurringActualService.EasternToday(), stoppingToken);
            if (posted > 0)
                logger.LogInformation("Posted {Count} due recurring actual transactions.", posted);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            // Missing schema during initial migration or a transient outage: retry next tick.
            logger.LogError(exception, "Recurring actual posting failed. Will retry on the next cycle.");
        }
    }
}
