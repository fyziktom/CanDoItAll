using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessDispatchRecoveryQueryHelper
{
    public static Guid? ResolveRecoverableExecutionRunId(
        ProcessStepRun stepRun,
        IReadOnlyList<ProcessAutomationExecutionRunRecord> executionRuns)
    {
        return ProcessRunAutomationDispatchService.ResolveRecoverableAutomationExecutionRunId(stepRun, executionRuns);
    }

    public static async Task<string> LoadLatestManualRecoveryDirectiveAsync(
        AppDbContext dbContext,
        Guid runId,
        Guid stepRunId,
        DateTimeOffset? stepStartedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var query = dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .Where(item =>
                item.ProcessRunId == runId &&
                item.StepRunId == stepRunId &&
                item.EventType == ProcessRuntimeEventTypes.ManualAgentStepRerun);
        var journalEntries = await query.ToListAsync(cancellationToken);
        var candidateEntries = stepStartedAtUtc.HasValue
            ? journalEntries.Where(item => item.OccurredAtUtc >= stepStartedAtUtc.Value)
            : journalEntries;

        return candidateEntries
            .OrderByDescending(item => item.OccurredAtUtc)
            .Select(item => item.Description)
            .FirstOrDefault() ?? string.Empty;
    }
}
