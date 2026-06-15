using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessNoProgressRetryJournalQueryCoordinator(
    IDbContextFactory<AppDbContext> dbContextFactory)
{
    public async Task<bool> HasPriorSignalAsync(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessRunAutomationDispatchService.NoProgressRetrySignal signal,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entries = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .Where(entry =>
                entry.ProcessRunId == candidate.Run.Id &&
                entry.StepRunId == candidate.StepRun.Id &&
                entry.CorrelationId == signal.Fingerprint &&
                (entry.EventType == ProcessRuntimeEventTypes.NoProgressRetryObserved ||
                 entry.EventType == ProcessRuntimeEventTypes.NoProgressRetryCompressed))
            .ToListAsync(cancellationToken);

        return ProcessRunAutomationDispatchService.HasPriorNoProgressRetrySignal(entries, signal);
    }
}
