using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.SchedulerPlanner;

public sealed class SchedulerFireRecoveryWorker(IServiceScopeFactory scopeFactory, ILogger<SchedulerFireRecoveryWorker> logger) : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try {
            while (await timer.WaitForNextTickAsync(stoppingToken)) {
                await RecoverAsync(stoppingToken);
            }
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
        }
    }

    private async Task RecoverAsync(CancellationToken cancellationToken) {
        await using var scope = scopeFactory.CreateAsyncScope();
        var admissions = scope.ServiceProvider.GetRequiredService<SchedulerFireAdmissionStore>();
        IReadOnlyList<SchedulerPlanFireRequest> requests;
        try {
            requests = await admissions.ListRecoveryAsync(50, cancellationToken);
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            logger.LogError(exception, "Scheduler fire recovery could not read its pending admissions.");
            return;
        }
        foreach (var request in requests) {
            try {
                await scope.ServiceProvider.GetRequiredService<ISchedulerPlannerRunDispatcher>().DispatchAsync(request, cancellationToken);
            } catch (Exception exception) when (exception is not OperationCanceledException) {
                logger.LogWarning(exception, "Scheduler fire observation remains incomplete for plan {PlanId} fired at {FiredAtUtc}.",
                    request.PlanId, request.FiredAtUtc);
            }
        }
    }
}
