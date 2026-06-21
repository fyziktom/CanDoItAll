using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessSubprocessCapabilityGapStep(
    string Title,
    ProcessStepRunStatus Status,
    ProcessCapabilityGapSeverity CapabilityGapSeverity,
    Guid? CurrentExecutorPartyId,
    string CurrentExecutorName);

internal sealed class ProcessSubprocessCapabilityGapInspector(IDbContextFactory<AppDbContext> dbContextFactory)
{
    public async Task<string?> TryBuildBlockReasonAsync(
        ProcessSubprocessRunStartResult subprocessRun,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subprocessRun);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var activeChildSteps = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item =>
                item.ProcessRunId == subprocessRun.RunId &&
                (item.Status == ProcessStepRunStatus.Ready ||
                 item.Status == ProcessStepRunStatus.WaitingApproval ||
                 item.Status == ProcessStepRunStatus.InProgress))
            .OrderBy(item => item.Sequence)
            .Select(item => new ProcessSubprocessCapabilityGapStep(
                item.Title,
                item.Status,
                item.CapabilityGapSeverity,
                item.CurrentExecutorPartyId,
                item.CurrentExecutorName))
            .ToListAsync(cancellationToken);
        if (activeChildSteps.Count == 0)
        {
            return null;
        }

        var executableChildStepExists = activeChildSteps.Any(item =>
            item.CapabilityGapSeverity == ProcessCapabilityGapSeverity.None &&
            item.CurrentExecutorPartyId.HasValue);
        if (executableChildStepExists)
        {
            return null;
        }

        var blockingSteps = activeChildSteps
            .Where(item =>
                item.CapabilityGapSeverity != ProcessCapabilityGapSeverity.None ||
                !item.CurrentExecutorPartyId.HasValue)
            .Take(3)
            .Select(BuildStepSummary)
            .ToList();
        if (blockingSteps.Count == 0)
        {
            return null;
        }

        var additionalCount = activeChildSteps.Count - blockingSteps.Count;
        var additionalSummary = additionalCount <= 0
            ? string.Empty
            : $" and {additionalCount} more active child step(s)";

        return $"Subprocess run '{subprocessRun.RunName}' cannot proceed because active child step(s) have unresolved required role assignments or capability gaps: {string.Join("; ", blockingSteps)}{additionalSummary}. Resolve the subprocess role assignments or rerun with a launch plan that binds the required roles.";
    }

    internal static string BuildStepSummary(ProcessSubprocessCapabilityGapStep step)
    {
        var executorName = string.IsNullOrWhiteSpace(step.CurrentExecutorName)
            ? "unassigned"
            : step.CurrentExecutorName.Trim();

        return $"'{step.Title}' is {step.Status} for executor '{executorName}' ({step.CapabilityGapSeverity})";
    }
}
