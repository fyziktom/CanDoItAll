namespace CanDoItAll.Manager;

internal sealed class ManagerProcessRecoveryService(
    IManagerProcessCoordinator processCoordinator,
    ILogger<ManagerProcessRecoveryService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var purpose in Enum.GetValues<ManagerProcessPurpose>())
        {
            var results = await processCoordinator.ReclaimRegisteredAsync(
                purpose,
                "manager-startup-recovery",
                cancellationToken).ConfigureAwait(false);
            if (results.Count == 0)
            {
                continue;
            }

            logger.LogInformation(
                "Reconciled {ProcessCount} registered Manager process record(s) for {Purpose}. ResidualCount={ResidualCount}.",
                results.Count,
                purpose,
                results.Count(result => result.ResidualProcessPossible));
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
