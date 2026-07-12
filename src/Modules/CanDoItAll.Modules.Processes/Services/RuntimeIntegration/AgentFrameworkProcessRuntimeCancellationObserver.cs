using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;


internal sealed class AgentFrameworkProcessRuntimeCancellationObserver(
    IAgentExecutionCancellationRegistry cancellationRegistry,
    ISandboxWorkspaceExecutionRunStore executionRunStore,
    ILogger<AgentFrameworkProcessRuntimeCancellationObserver> logger) : IProcessRuntimeRunCancellationObserver
{
    private const string CancellationPhase = "process-cancellation";
    private const string CancellationSummary = "Execution run cancelled because the owning process run was cancelled.";

    public async ValueTask<ProcessRuntimeRunCancellationObservationResult> OnRunsCancelledAsync(
        ProcessRuntimeRunCancellationObservation observation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);

        var processRunIds = observation.CancelledRunIds
            .Select(runId => runId.Value.ToString("D"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (processRunIds.Length == 0)
        {
            return ProcessRuntimeRunCancellationObservationResult.Empty;
        }

        var signaledCount = cancellationRegistry.RequestCancellationByProcessRunIds(
            processRunIds,
            observation.RequestedBy,
            observation.Reason);
        var repairedCount = await MarkExecutionRunRecordsCancelledAsync(
            processRunIds,
            observation.CancelledAtUtc,
            cancellationToken).ConfigureAwait(false);
        var diagnostics = new List<string>();
        if (signaledCount > 0)
        {
            diagnostics.Add($"Signaled cancellation to {signaledCount} active AgentFramework execution run(s).");
        }

        if (repairedCount > 0)
        {
            diagnostics.Add($"Marked {repairedCount} AgentFramework execution run record(s) cancelled for the cancelled process run(s).");
        }

        if (signaledCount > 0 || repairedCount > 0)
        {
            logger.LogInformation(
                "Process cancellation signaled {SignaledCount} active AgentFramework execution run(s) and marked {RepairedCount} record(s) cancelled. ProcessRunIds={ProcessRunIds}",
                signaledCount,
                repairedCount,
                string.Join(", ", processRunIds));
        }

        return diagnostics.Count == 0
            ? ProcessRuntimeRunCancellationObservationResult.Empty
            : new ProcessRuntimeRunCancellationObservationResult(diagnostics);
    }

    private async Task<int> MarkExecutionRunRecordsCancelledAsync(
        IReadOnlyList<string> processRunIds,
        DateTimeOffset cancelledAtUtc,
        CancellationToken cancellationToken)
    {
        var processRunIdSet = processRunIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var executionRuns = await executionRunStore.ListExecutionRunsAsync(cancellationToken).ConfigureAwait(false);
        var activeRuns = executionRuns
            .Where(run =>
                processRunIdSet.Contains(run.ProcessRunId) &&
                IsActiveExecutionRun(run))
            .OrderBy(run => run.CreatedAtUtc)
            .ToArray();
        var repairedCount = 0;
        foreach (var executionRun in activeRuns)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var detail = await executionRunStore.GetExecutionRunDetailAsync(executionRun.Id, cancellationToken).ConfigureAwait(false);
            if (detail is null || !IsActiveExecutionRun(detail.Run))
            {
                continue;
            }

            var cancelledRun = detail.Run with
            {
                State = ExecutionState.Failed,
                Outcome = RunOutcome.Cancelled,
                ResultSummary = CancellationSummary,
                UpdatedAtUtc = cancelledAtUtc,
                CompletedAtUtc = cancelledAtUtc,
                RuntimeSessionKey = string.Empty,
                SerializedSessionStateJson = null,
                PendingApprovals = [],
                Revision = detail.Run.Revision + 1L
            };
            var cancelledSession = detail.ChatSession is null
                ? null
                : detail.ChatSession with
                {
                    UpdatedAtUtc = cancelledAtUtc,
                    Compatibility = null,
                    LatestExecutionRunId = cancelledRun.Id
                };
            var cancelledDetail = new ExecutionRunDetail(
                cancelledRun,
                cancelledSession,
                AppendCancellationLog(detail.ExecutionLog, cancelledRun, cancelledAtUtc),
                detail.Metrics)
            {
                UsageObservations = detail.UsageObservations,
                Approvals = detail.Approvals,
                Artifacts = detail.Artifacts,
                Checkpoints = detail.Checkpoints,
                ToolReceipts = detail.ToolReceipts
            };

            await executionRunStore.SaveExecutionRunDetailAsync(cancelledDetail, cancellationToken).ConfigureAwait(false);
            repairedCount++;
        }

        return repairedCount;
    }

    private static bool IsActiveExecutionRun(ExecutionRunRecord run)
    {
        return run.State is not ExecutionState.Completed and not ExecutionState.Failed;
    }

    private static IReadOnlyList<ExecutionLogEntry> AppendCancellationLog(
        IReadOnlyList<ExecutionLogEntry> executionLog,
        ExecutionRunRecord run,
        DateTimeOffset cancelledAtUtc)
    {
        var entry = new ExecutionLogEntry(
            Id: Guid.NewGuid(),
            AgentId: run.AgentId,
            ChatSessionId: run.ChatSessionId,
            CreatedAtUtc: cancelledAtUtc,
            State: ExecutionState.Failed,
            Phase: CancellationPhase,
            Message: CancellationSummary)
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

