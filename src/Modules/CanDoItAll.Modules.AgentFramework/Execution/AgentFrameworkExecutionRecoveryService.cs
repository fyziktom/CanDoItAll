using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class AgentFrameworkExecutionRecoveryService(
    ISandboxWorkspaceExecutionRunStore executionRunStore,
    IWorkspaceExecutionRunProcessLeaseCleaner workspaceProcessLeaseCleaner,
    IEnumerable<IAgentExecutionRecoveryObserver> recoveryObservers,
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
        var executionRuns = await executionRunStore.ListExecutionRunsAsync(cancellationToken);
        foreach (var terminalRun in executionRuns.Where(IsTerminalRun))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await CleanupRetainedProcessLeasesAsync(terminalRun);
        }

        var strandedRunIds = executionRuns
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
                UsageObservations = detail.UsageObservations,
                Approvals = detail.Approvals,
                Artifacts = detail.Artifacts,
                Checkpoints = detail.Checkpoints,
                ToolReceipts = detail.ToolReceipts
            };

            await executionRunStore.SaveExecutionRunDetailAsync(repairedDetail, cancellationToken);
            await CleanupRetainedProcessLeasesAsync(repairedRun);
            await NotifyRecoveryObserversAsync(repairedRun, repairedAtUtc, cancellationToken);
            repairedCount++;

            logger.LogInformation(
                "Recovered interrupted AgentFramework execution run {ExecutionRunId} for agent {AgentId}.",
                repairedRun.Id,
                repairedRun.AgentId);
        }

        return repairedCount;
    }

    private async Task CleanupRetainedProcessLeasesAsync(ExecutionRunRecord run)
    {
        try
        {
            using var auditScope = WorkspaceExecutionAuditContext.BeginScope(run);
            var result = await workspaceProcessLeaseCleaner
                .CleanupAsync(run.Id)
                .ConfigureAwait(false);

            if (result.CleanedStartupReceiptPaths.Count > 0)
            {
                logger.LogInformation(
                    "Startup recovery cleaned {WorkspaceProcessLeaseCount} retained ExecutionRun workspace process lease(s) for execution run {ExecutionRunId}. Startup receipts: {StartupReceiptPaths}.",
                    result.CleanedStartupReceiptPaths.Count,
                    run.Id,
                    result.CleanedStartupReceiptPaths);
            }

            foreach (var failure in result.Failures)
            {
                logger.LogError(
                    "Startup recovery could not clean the retained ExecutionRun workspace process lease for execution run {ExecutionRunId} and startup receipt {StartupReceiptPath}. The durable lease remains available for a later retry. Failure: {CleanupFailure}.",
                    run.Id,
                    failure.StartupReceiptPath,
                    failure.Message);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Startup recovery failed unexpectedly while reconciling retained ExecutionRun workspace process leases for execution run {ExecutionRunId}.",
                run.Id);
        }
    }

    private async Task NotifyRecoveryObserversAsync(
        ExecutionRunRecord run,
        DateTimeOffset repairedAtUtc,
        CancellationToken cancellationToken)
    {
        var observation = new AgentExecutionRecoveryObservation(
            run.Id,
            run.SourceKind,
            run.ProcessRunId,
            run.ProcessStepId,
            run.State,
            run.Outcome,
            run.ResultSummary,
            repairedAtUtc);

        foreach (var observer in recoveryObservers)
        {
            try
            {
                await observer.OnExecutionRecoveredAsync(observation, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "AgentFramework execution recovery observer {ObserverType} failed for execution run {ExecutionRunId}.",
                    observer.GetType().FullName,
                    run.Id);
            }
        }
    }

    private static bool IsInterruptedRun(ExecutionRunRecord run, DateTimeOffset startupCutoffUtc)
    {
        return run.State is not ExecutionState.Completed
               and not ExecutionState.Failed
               && run.CreatedAtUtc <= startupCutoffUtc;
    }

    private static bool IsTerminalRun(ExecutionRunRecord run)
        => run.State is ExecutionState.Completed or ExecutionState.Failed;

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
