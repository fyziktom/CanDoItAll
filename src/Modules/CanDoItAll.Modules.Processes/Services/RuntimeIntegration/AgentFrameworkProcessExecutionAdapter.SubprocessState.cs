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

internal sealed partial class AgentFrameworkProcessExecutionAdapter
{
    internal static async ValueTask<ProcessRunId?> TryResolvePendingChildRunAsync(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IProcessRuntimeStateStore stateStore,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(stateStore);

        if (output.Status is not (ProcessStepOutcomeStatus.Blocked or ProcessStepOutcomeStatus.WaitingApproval) ||
            !CanWaitOnControlledChildRun(assignment))
        {
            return null;
        }

        var currentState = await stateStore.LoadAsync(assignment.RunId, cancellationToken).ConfigureAwait(false);
        if (currentState is null)
        {
            return null;
        }

        foreach (var candidateRunId in ExtractReferencedRunIds(output))
        {
            var pendingRunId = await TryResolveNonTerminalProcessTreeRunAsync(
                assignment,
                currentState,
                candidateRunId,
                stateStore,
                cancellationToken).ConfigureAwait(false);
            if (pendingRunId is not null)
            {
                return pendingRunId;
            }
        }

        return null;
    }

    private static ProcessRuntimeDispatchDeferredException CreatePendingChildRunDeferredException(
        ProcessRuntimeStepAssignment assignment,
        ProcessRunId pendingChildRunId)
    {
        return new ProcessRuntimeDispatchDeferredException(
            $"Step '{assignment.StepKey}' is waiting for active child process run '{pendingChildRunId}'.",
            pendingChildRunId);
    }

    private async ValueTask<CompletedSubprocessOutcome?> TryResolveExistingCompletedChildOutcomeAsync(
        ProcessRuntimeStepAssignment assignment,
        IProcessRuntimeStepAssignmentStore assignmentStore,
        IProcessRuntimeStateStore stateStore,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(assignmentStore);
        ArgumentNullException.ThrowIfNull(stateStore);

        if (!RequiresSubprocessLaunch(assignment))
        {
            return null;
        }

        var currentState = await stateStore.LoadAsync(assignment.RunId, cancellationToken).ConfigureAwait(false);
        if (currentState is null)
        {
            return null;
        }

        var childAssignments = await assignmentStore
            .FindByLaunchVariablesAsync(
                ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                    assignment.RunId,
                    assignment.StepInstanceId),
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var childGroup in childAssignments
            .GroupBy(childAssignment => childAssignment.RunId)
            .OrderByDescending(group => group.Max(childAssignment => childAssignment.CreatedAtUtc)))
        {
            var childRunId = childGroup.Key;
            var childState = await stateStore.LoadAsync(childRunId, cancellationToken).ConfigureAwait(false);
            if (childState is null ||
                !IsSameProcessTree(currentState, childState))
            {
                continue;
            }

            if (childState.Status != ProcessRuntimeStatus.Completed)
            {
                return null;
            }

            var evidenceRefs = ResolveCompletedChildEvidenceRefs(childRunId, childGroup);
            var syntheticExecutionRunId = CreateSyntheticSubprocessExecutionRunId(
                assignment,
                childRunId,
                childState.UpdatedAtUtc);
            var rawOutputHash = ComputeHash(
                $"{assignment.RunId}:{assignment.StepInstanceId}:completed-child:{childRunId}:{childState.UpdatedAtUtc:O}:{string.Join("|", evidenceRefs)}");
            return new CompletedSubprocessOutcome(
                childRunId,
                childState.UpdatedAtUtc,
                evidenceRefs,
                rawOutputHash,
                syntheticExecutionRunId,
                [CreateSubprocessOutcomeReceipt(syntheticExecutionRunId, assignment, childRunId, evidenceRefs)]);
        }

        return null;
    }

    internal static async ValueTask<ProcessRunId?> TryResolveExistingPendingChildRunAsync(
        ProcessRuntimeStepAssignment assignment,
        IProcessRuntimeStepAssignmentStore assignmentStore,
        IProcessRuntimeStateStore stateStore,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(assignmentStore);
        ArgumentNullException.ThrowIfNull(stateStore);

        if (!CanWaitOnControlledChildRun(assignment))
        {
            return null;
        }

        var currentState = await stateStore.LoadAsync(assignment.RunId, cancellationToken).ConfigureAwait(false);
        if (currentState is null)
        {
            return null;
        }

        var childAssignments = await assignmentStore
            .FindByLaunchVariablesAsync(
                ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                    assignment.RunId,
                    assignment.StepInstanceId),
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var candidateRunId in childAssignments
            .OrderByDescending(childAssignment => childAssignment.CreatedAtUtc)
            .Select(childAssignment => childAssignment.RunId)
            .Distinct())
        {
            var pendingRunId = await TryResolveNonTerminalProcessTreeRunAsync(
                assignment,
                currentState,
                candidateRunId,
                stateStore,
                cancellationToken).ConfigureAwait(false);
            if (pendingRunId is not null)
            {
                return pendingRunId;
            }
        }

        return null;
    }

    private IReadOnlyList<string> ResolveCompletedChildEvidenceRefs(
        ProcessRunId childRunId,
        IEnumerable<ProcessRuntimeStepAssignment> childAssignments)
    {
        var childManagedArtifactRoot = $"artifacts/process-runs/{childRunId.Value:D}";
        var refs = new List<string>();
        foreach (var childAssignment in childAssignments.OrderBy(childAssignment => childAssignment.CreatedAtUtc))
        {
            if (string.IsNullOrWhiteSpace(childAssignment.StepKey))
            {
                continue;
            }

            var candidateRef = $"{childManagedArtifactRoot}/steps/{SanitizeManagedArtifactPathSegment(childAssignment.StepKey)}.md";
            var stat = workspaceFiles.StatPath(candidateRef);
            if (stat.Exists)
            {
                refs.Add(candidateRef);
            }
        }

        if (refs.Count == 0)
        {
            refs.Add($"{childManagedArtifactRoot}/steps");
        }

        return refs
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ProcessStepOutcomeResult BuildCompletedSubprocessProcessStepOutcome(
        ProcessRuntimeStepAssignment assignment,
        CompletedSubprocessOutcome completedChildOutcome)
    {
        var childRunId = completedChildOutcome.ChildRunId.Value.ToString("D");
        var childCompletedAt = completedChildOutcome.ChildCompletedAtUtc.UtcDateTime.ToString("u", CultureInfo.InvariantCulture);
        return new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = $"Matching child process run {childRunId} completed at {childCompletedAt}; the parent subprocess step is completed from managed child evidence.",
            BranchOutcomeKey = string.Empty,
            BranchOutcomeTitle = string.Empty,
            EvidenceRefs = completedChildOutcome.EvidenceRefs,
            NextActions = [],
            HumanReadableSummaryMarkdown = $"""
            ## Subprocess handoff completed

            The process runtime completed parent step `{assignment.StepKey}` from matching completed child process run `{childRunId}`.

            ## Child evidence

            {FormatMarkdownEvidenceList(completedChildOutcome.EvidenceRefs)}
            """
        };
    }

    private static string FormatMarkdownEvidenceList(IReadOnlyList<string> evidenceRefs)
    {
        if (evidenceRefs.Count == 0)
        {
            return "- No child managed evidence refs were available.";
        }

        return string.Join(
            Environment.NewLine,
            evidenceRefs.Select(evidenceRef => $"- `{evidenceRef}`"));
    }

    private static ToolExecutionReceiptRecord CreateSubprocessOutcomeReceipt(
        Guid syntheticExecutionRunId,
        ProcessRuntimeStepAssignment assignment,
        ProcessRunId childRunId,
        IReadOnlyList<string> evidenceRefs)
    {
        var childDefinitionKey = ProcessRuntimeLaunchVariables.TryReadProcessStepSubprocessDefinitionKey(
            assignment.LaunchVariables,
            out var definitionKey)
            ? definitionKey
            : "unknown";
        var evidenceSummary = evidenceRefs.Count == 0
            ? "no child evidence refs"
            : string.Join("; ", evidenceRefs);
        return new ToolExecutionReceiptRecord(
            Guid.NewGuid(),
            syntheticExecutionRunId,
            "process-runtime",
            SubprocessLaunchToolName,
            "ProcessRuntime",
            "NotRequired",
            "Process runtime resolved a previously completed matching child subprocess.",
            $"definitionKey={childDefinitionKey}; parentRunId={assignment.RunId.Value:D}; parentStepId={assignment.StepInstanceId.Value:D}; childRunId={childRunId.Value:D}",
            ".",
            $"Succeeded: matching child run {childRunId.Value:D} completed with evidence refs: {evidenceSummary}",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

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

    private static Guid CreateSyntheticSubprocessExecutionRunId(
        ProcessRuntimeStepAssignment assignment,
        ProcessRunId childRunId,
        DateTimeOffset childUpdatedAtUtc)
    {
        var input = Encoding.UTF8.GetBytes(
            $"completed-child:{assignment.RunId.Value:D}:{assignment.StepInstanceId.Value:D}:{childRunId.Value:D}:{childUpdatedAtUtc.UtcDateTime:O}");
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        return new Guid(hash[..16]);
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
