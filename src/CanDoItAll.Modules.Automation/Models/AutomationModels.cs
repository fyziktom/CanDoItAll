using CanDoItAll.Infrastructure.BackgroundJobs;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Automation;

public sealed record AutomationWorkspaceModel(
    IReadOnlyList<BackgroundJobSummary> Jobs,
    IReadOnlyList<AutomationSignalItem> Signals);

public sealed class AutomationWorkspaceService(
    IBackgroundJobTracker backgroundJobTracker,
    IAutomationSignalProvider automationSignalProvider)
{
    public Task<IReadOnlyList<BackgroundJobSummary>> ListJobsAsync(CancellationToken cancellationToken = default)
        => backgroundJobTracker.ListAsync(cancellationToken);

    public Task<IReadOnlyList<AutomationSignalItem>> ListSignalsAsync(CancellationToken cancellationToken = default)
        => automationSignalProvider.ListSignalsAsync(cancellationToken);

    public async Task<AutomationWorkspaceModel> GetWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await backgroundJobTracker.ListAsync(cancellationToken);
        var signals = await automationSignalProvider.ListSignalsAsync(cancellationToken);
        return new AutomationWorkspaceModel(jobs, signals);
    }
}


