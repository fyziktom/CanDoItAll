using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class AgentFrameworkExecutionRecoveryService(
    ISandboxWorkspaceStore workspaceStore,
    ISandboxWorkspaceExecutionRunStore executionRunStore,
    ILogger<AgentFrameworkExecutionRecoveryService> logger)
{
    private const string RestartRecoveryPhase = "startup-recovery";
    private const string RestartRecoveryMessage = "Execution interrupted because the CanDoItAll host restarted before the run completed.";

    public async Task<int> RecoverInterruptedRunsAsync(CancellationToken cancellationToken = default)
        => await RecoverInterruptedRunsAsync(DateTimeOffset.UtcNow, cancellationToken);

    internal async Task<int> RecoverInterruptedRunsAsync(
        DateTimeOffset startupCutoffUtc,
        CancellationToken cancellationToken = default)
    {
        var executionState = await workspaceStore.LoadExecutionAsync(cancellationToken);
        var strandedRunIds = executionState.ExecutionRuns
            .Where(run => IsInterruptedRun(run, startupCutoffUtc))
            .Select(item => item.Id)
            .ToList();
        if (strandedRunIds.Count == 0)
        {
            return 0;
        }

        var repairedCount = 0;
        foreach (var executionRunId in strandedRunIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var detail = await executionRunStore.GetExecutionRunDetailAsync(executionRunId, cancellationToken);
            if (detail is null || !IsInterruptedRun(detail.Run, startupCutoffUtc) || HasResumableApprovals(detail))
            {
                continue;
            }

            var repairedAtUtc = DateTimeOffset.UtcNow;
            var repairedRun = detail.Run with
            {
                State = ExecutionState.Failed,
                Outcome = RunOutcome.Cancelled,
                ResultSummary = RestartRecoveryMessage,
                UpdatedAtUtc = repairedAtUtc,
                CompletedAtUtc = repairedAtUtc,
                RuntimeSessionKey = string.Empty,
                SerializedSessionStateJson = null,
                PendingApprovals = [],
                Revision = detail.Run.Revision + 1L
            };
            var repairedSession = detail.ChatSession is null
                ? null
                : detail.ChatSession with
                {
                    UpdatedAtUtc = repairedAtUtc,
                    Compatibility = null,
                    LatestExecutionRunId = repairedRun.Id
                };
            var repairedDetail = new ExecutionRunDetail(
                repairedRun,
                repairedSession,
                AppendRestartRecoveryLog(detail.ExecutionLog, detail.Run, repairedAtUtc),
                detail.Metrics)
            {
                Approvals = detail.Approvals,
                Artifacts = detail.Artifacts,
                Checkpoints = detail.Checkpoints,
                ToolReceipts = detail.ToolReceipts
            };

            await executionRunStore.SaveExecutionRunDetailAsync(repairedDetail, cancellationToken);
            repairedCount++;

            logger.LogInformation(
                "Recovered interrupted AgentFramework execution run {ExecutionRunId} for agent {AgentId}.",
                repairedRun.Id,
                repairedRun.AgentId);
        }

        return repairedCount;
    }

    private static bool IsInterruptedRun(ExecutionRunRecord run, DateTimeOffset startupCutoffUtc)
    {
        return run.State is not ExecutionState.Completed
               and not ExecutionState.Failed
               && run.CreatedAtUtc <= startupCutoffUtc;
    }

    private static bool HasResumableApprovals(ExecutionRunDetail detail)
    {
        return detail.Run.PendingApprovals.Count > 0 ||
               (detail.ChatSession?.Compatibility?.PendingApprovals.Count ?? 0) > 0;
    }

    private static IReadOnlyList<ExecutionLogEntry> AppendRestartRecoveryLog(
        IReadOnlyList<ExecutionLogEntry> executionLog,
        ExecutionRunRecord run,
        DateTimeOffset repairedAtUtc)
    {
        var entry = new ExecutionLogEntry(
            Id: Guid.NewGuid(),
            AgentId: run.AgentId,
            ChatSessionId: run.ChatSessionId,
            CreatedAtUtc: repairedAtUtc,
            State: ExecutionState.Failed,
            Phase: RestartRecoveryPhase,
            Message: RestartRecoveryMessage)
        {
            ExecutionRunId = run.Id
        };

        var entries = new List<ExecutionLogEntry>(executionLog.Count + 1)
        {
            entry
        };
        entries.AddRange(executionLog.Where(item => item.Id != entry.Id));
        return entries;
    }
}

internal sealed class AgentFrameworkExecutionRecoveryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AgentFrameworkExecutionRecoveryWorker> logger) : BackgroundService
{
    private readonly DateTimeOffset startupCutoffUtc = DateTimeOffset.UtcNow;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var recoveryService = scope.ServiceProvider.GetRequiredService<AgentFrameworkExecutionRecoveryService>();
            var repairedCount = await recoveryService.RecoverInterruptedRunsAsync(startupCutoffUtc, stoppingToken);
            if (repairedCount > 0)
            {
                logger.LogInformation(
                    "Recovered {RecoveredCount} interrupted AgentFramework execution run(s) during startup.",
                    repairedCount);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "AgentFramework execution recovery failed during startup.");
        }
    }
}
