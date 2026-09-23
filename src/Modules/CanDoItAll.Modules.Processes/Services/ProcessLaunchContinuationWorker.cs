using CanDoItAll.Processes.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessLaunchContinuationWorker(IServiceScopeFactory scopes, ILogger<ProcessLaunchContinuationWorker> logger) : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do {
            try {
                await ReconcileAsync(stoppingToken);
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                return;
            } catch (Exception exception) {
                logger.LogWarning("Process launch continuation discovery failed ({ErrorType}).", exception.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ReconcileAsync(CancellationToken cancellationToken) {
        await using var discovery = scopes.CreateAsyncScope();
        var admissions = await discovery.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>()
            .ListPendingContinuationsAsync(32, cancellationToken);
        foreach (var admission in admissions) {
            await using var execution = scopes.CreateAsyncScope();
            try {
                var result = await execution.ServiceProvider.GetRequiredService<ProcessLaunchApplicationService>()
                    .ResumeAcceptedLaunchAsync(admission, cancellationToken);
                if (result.Observation?.ObservationException is { } failure) {
                    logger.LogWarning("Process launch admission {AdmissionId} remains in {State} ({ErrorType}).",
                        admission.Value, result.Observation.ContinuationState, failure.GetType().Name);
                }
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception exception) {
                logger.LogWarning("Process launch admission {AdmissionId} could not be reconciled ({ErrorType}).",
                    admission.Value, exception.GetType().Name);
            }
        }
    }
}
