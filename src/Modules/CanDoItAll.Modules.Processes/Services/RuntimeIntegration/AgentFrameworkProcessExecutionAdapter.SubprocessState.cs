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
using CanDoItAll.Processes.Contracts;
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

internal sealed partial class AgentFrameworkProcessExecutionAdapter
{
    internal static ValueTask<ProcessRunId?> TryResolvePendingChildRunAsync(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IProcessRuntimeStateStore stateStore,
        CancellationToken cancellationToken = default)
        => ParentSubprocessArtifactBridge.TryResolvePendingChildRunAsync(
            assignment,
            output,
            stateStore,
            cancellationToken);

    private static ProcessRuntimeDispatchDeferredException CreatePendingChildRunDeferredException(
        ProcessRuntimeStepAssignment assignment,
        ProcessRunId pendingChildRunId)
    {
        return new ProcessRuntimeDispatchDeferredException(
            $"Step '{assignment.StepKey}' is waiting for active child process run '{pendingChildRunId}'.",
            pendingChildRunId);
    }

    internal static ValueTask<ProcessRunId?> TryResolveExistingPendingChildRunAsync(
        ProcessRuntimeStepAssignment assignment,
        IProcessRuntimeStepAssignmentStore assignmentStore,
        IProcessRuntimeStateStore stateStore,
        CancellationToken cancellationToken = default)
        => ParentSubprocessArtifactBridge.TryResolveExistingPendingChildRunAsync(
            assignment,
            assignmentStore,
            stateStore,
            cancellationToken);

    private static ToolExecutionReceiptRecord CreateCoordinatedSubprocessLaunchReceipt(
        ProcessRuntimeStepAssignment assignment,
        ProcessSubprocessLaunchCoordinatorResult launch)
    {
        var childRunSummary = launch.ChildRunId is { } childRunId
            ? childRunId.Value.ToString("D")
            : "none";
        var evidenceSummary = launch.ExpectedChildEvidenceRefs.Count == 0
            ? "no expected child evidence refs"
            : string.Join("; ", launch.ExpectedChildEvidenceRefs);
        return new ToolExecutionReceiptRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "process-runtime",
            SubprocessLaunchToolName,
            "ProcessRuntime",
            "NotRequired",
            "Process runtime coordinated the mapped subprocess launch before parent agent execution.",
            $"definitionKey={launch.DefinitionKey}; parentRunId={assignment.RunId.Value:D}; parentStepId={assignment.StepInstanceId.Value:D}; childRunId={childRunSummary}; stage={launch.Stage}",
            ".",
            $"Succeeded: coordinated subprocess launch returned stage '{launch.Stage}' for child run '{childRunSummary}' with evidence refs: {evidenceSummary}",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static bool IsWaitingOnStoppedSubprocessOutcome(ProcessStepOutcomeResult output)
    {
        if (output.Status is not (ProcessStepOutcomeStatus.Blocked or ProcessStepOutcomeStatus.WaitingApproval))
        {
            return false;
        }

        var text = string.Join(
            " ",
            EnumerateOutcomeText(output).Where(value => !string.IsNullOrWhiteSpace(value)));
        return ContainsAny(
            text,
            "waiting for active child process run",
            "wait for active child process run",
            "parent step should be deferred",
            "child run is still active",
            "child run is still running",
            "child run id",
            "child subprocess",
            "child process run") &&
            ContainsAny(text, "active", "running", "finish", "stops", "stopped");
    }

    private static async ValueTask<ProcessRunId?> TryResolveNonTerminalProcessTreeRunAsync(
        ProcessRuntimeStepAssignment assignment,
        ProcessRuntimeStateSnapshot currentState,
        ProcessRunId candidateRunId,
        IProcessRuntimeStateStore stateStore,
        CancellationToken cancellationToken)
    {
        if (IsCurrentOrAncestorRunReference(assignment, currentState, candidateRunId))
        {
            return null;
        }

        var candidateState = await stateStore.LoadAsync(candidateRunId, cancellationToken).ConfigureAwait(false);
        if (candidateState is null ||
            ProcessRuntimeChildRunParentQuery.IsStoppedChildStatus(candidateState.Status) ||
            !IsSameProcessTree(currentState, candidateState))
        {
            return null;
        }

        return candidateRunId;
    }

    private static bool IsCurrentOrAncestorRunReference(
        ProcessRuntimeStepAssignment assignment,
        ProcessRuntimeStateSnapshot currentState,
        ProcessRunId candidateRunId)
    {
        if (candidateRunId == assignment.RunId ||
            candidateRunId == currentState.RunId ||
            candidateRunId == currentState.RootRunId)
        {
            return true;
        }

        return ProcessRuntimeLaunchVariables.TryReadParentRunId(assignment.LaunchVariables, out var parentRunId) &&
            candidateRunId == parentRunId;
    }

    private static bool CanWaitOnControlledChildRun(ProcessRuntimeStepAssignment assignment)
    {
        return assignment.AllowedOperations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase) ||
            string.Equals(assignment.OperationTargetScope, ProcessOperationContractNames.ExternalActionControlled, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameProcessTree(
        ProcessRuntimeStateSnapshot currentState,
        ProcessRuntimeStateSnapshot candidateState)
    {
        return candidateState.RootRunId == currentState.RootRunId ||
            candidateState.RootRunId == currentState.RunId ||
            candidateState.RunId == currentState.RootRunId;
    }

    private static IReadOnlyList<ProcessRunId> ExtractReferencedRunIds(ProcessStepOutcomeResult output)
    {
        var runIds = new List<ProcessRunId>();
        foreach (var text in EnumerateOutcomeText(output))
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (Match match in ProcessRunIdRegex().Matches(text))
            {
                if (Guid.TryParse(match.Value, out var runGuid))
                {
                    var runId = new ProcessRunId(runGuid);
                    if (!runIds.Contains(runId))
                    {
                        runIds.Add(runId);
                    }
                }
            }
        }

        return runIds;
    }

    private static IEnumerable<string?> EnumerateOutcomeText(ProcessStepOutcomeResult output)
    {
        yield return output.Reason;
        yield return output.BranchOutcomeKey;
        yield return output.BranchOutcomeTitle;
        yield return output.HumanReadableSummaryMarkdown;

        foreach (var evidenceRef in output.EvidenceRefs)
        {
            yield return evidenceRef;
        }

        foreach (var nextAction in output.NextActions)
        {
            yield return nextAction;
        }
    }

    private static class ProcessLaunchVariableNames
    {
        public const string AgentId = "AgentId";
        public const string AgentName = "AgentName";
        public const string BranchName = "BranchName";
        public const string CurrentProcessRunNodeId = "CurrentProcessRunNodeId";
        public const string MachineName = "MachineName";
        public const string ParentProcessRunNodeId = "ParentProcessRunNodeId";
        public const string ProjectId = "ProjectId";
        public const string ProcessRunNodeId = "ProcessRunNodeId";
        public const string RepositoryRoot = "RepositoryRoot";
        public const string SessionId = "SessionId";
        public const string TargetProcessRunNodeId = "TargetProcessRunNodeId";
    }

}
