using CanDoItAll.Infrastructure.BackgroundJobs;

namespace CanDoItAll.Modules.Automation;

public sealed class AutomationWorkspaceService(IBackgroundJobTracker backgroundJobTracker)
{
    public Task<IReadOnlyList<BackgroundJobSummary>> ListJobsAsync(CancellationToken cancellationToken = default)
        => backgroundJobTracker.ListAsync(cancellationToken);
}
