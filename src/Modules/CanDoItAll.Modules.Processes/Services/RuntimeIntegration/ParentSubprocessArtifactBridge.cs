using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal interface IParentSubprocessArtifactBridge
{
    ValueTask<ParentSubprocessArtifactBridgeResult> ResolveExistingAsync(
        ProcessRuntimeStepAssignment assignment,
        CancellationToken cancellationToken = default);

    ValueTask<ParentSubprocessArtifactBridgeResult> ResolveFromOutputAsync(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        CancellationToken cancellationToken = default);
}

internal enum ParentSubprocessArtifactBridgeResultKind
{
    NotSubprocess,
    NoMatchingChildRun,
    ChildActive,
    AcceptedChildOutputBridged,
    NoGoChildOutputBridged,
    NoGoChildOutputFound,
    ChildCompletedWithoutAcceptedOutput,
    ChildForwardedContextUnavailable,
    ChildStoppedBlocked,
    ChildStoppedFailed,
    ContractMissing
}

internal sealed record ParentSubprocessArtifactBridgeResult(
    ParentSubprocessArtifactBridgeResultKind Kind,
    ProcessRunId? ChildRunId = null,
    ProcessSubprocessContract? Contract = null,
    IReadOnlyList<string>? BridgeEvidenceRefs = null,
    ParentSubprocessBridgedOutcome? BridgedOutcome = null,
    ParentSubprocessStoppedChild? StoppedChild = null,
    ParentSubprocessForwardedContextIssue? ForwardedContextIssue = null)
{
    public IReadOnlyList<string> EvidenceRefs { get; init; } = BridgeEvidenceRefs ?? [];

    public static ParentSubprocessArtifactBridgeResult NotSubprocess { get; } = new(ParentSubprocessArtifactBridgeResultKind.NotSubprocess);

    public static ParentSubprocessArtifactBridgeResult NoMatchingChildRun { get; } = new(ParentSubprocessArtifactBridgeResultKind.NoMatchingChildRun);
}

internal sealed record ParentSubprocessBridgedOutcome(
    ProcessRunId ChildRunId,
    DateTimeOffset ChildCompletedAtUtc,
    ChildOutputDisposition Disposition,
    string ChildStepKey,
    string ChildArtifactExpectationKey,
    string ParentBranchOutcomeKey,
    ProcessSubprocessVerifiedChildArtifact VerifiedChildOutput,
    ProcessStepOutcomeResult Output,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<ParentSubprocessForwardedContextArtifact> ForwardedContextArtifacts,
    string RawOutputHash,
    Guid SyntheticExecutionRunId,
    IReadOnlyList<ToolExecutionReceiptRecord> ToolReceipts);

internal sealed record ParentSubprocessStoppedChild(
    ProcessRuntimeStatus ChildStatus,
    string ChildStepKey,
    ProcessStepInstanceId? ChildStepInstanceId,
    ProcessRuntimeStepStatus? ChildStepStatus,
    IReadOnlyList<ParentSubprocessChildDiagnostic> Diagnostics,
    ProcessRecoveryDecisionReceipt? RecoveryDecision);

internal sealed record ParentSubprocessChildDiagnostic(
    string Code,
    string SafeSummary,
    string EvidenceHash,
    ProcessDiagnosticRetrySafety RetrySafety,
    ProcessDiagnosticIdempotencyClassification Idempotency);

internal enum ChildOutputDisposition
{
    Accepted,
    NoGo
}

internal sealed class ParentSubprocessArtifactBridge(
    IProcessRuntimeStepAssignmentStore assignmentStore,
    IProcessRuntimeStateStore stateStore,
    IWorkspaceFileService workspaceFiles,
    ProcessSubprocessContractResolver subprocessContractResolver) : IParentSubprocessArtifactBridge
{
    private readonly ProcessSubprocessChildArtifactVerifier childArtifactVerifier = new(workspaceFiles);
    private readonly IParentSubprocessForwardedContextResolver forwardedContextResolver =
        new ParentSubprocessForwardedContextResolver(workspaceFiles);

    public async ValueTask<ParentSubprocessArtifactBridgeResult> ResolveExistingAsync(
        ProcessRuntimeStepAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        if (!RequiresRuntimeOwnedSubprocessLaunch(assignment))
        {
            return ParentSubprocessArtifactBridgeResult.NotSubprocess;
        }

        if (!subprocessContractResolver.TryResolve(assignment, out var contract))
        {
            return new ParentSubprocessArtifactBridgeResult(
                ParentSubprocessArtifactBridgeResultKind.ContractMissing);
        }

        var currentState = await stateStore.LoadAsync(assignment.RunId, cancellationToken).ConfigureAwait(false);
        if (currentState is null)
        {
            return ParentSubprocessArtifactBridgeResult.NoMatchingChildRun;
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
            .OrderByDescending(group => group.Max(childAssignment => childAssignment.CreatedAtUtc))
            .ThenByDescending(group => group.Key.Value))
        {
            var childRunId = childGroup.Key;
            var childState = await stateStore.LoadAsync(childRunId, cancellationToken).ConfigureAwait(false);
            if (childState is null ||
                !IsSameProcessTree(currentState, childState))
            {
                continue;
            }

            if (!ProcessRuntimeChildRunParentQuery.IsStoppedChildStatus(childState.Status))
            {
                return new ParentSubprocessArtifactBridgeResult(
                    ParentSubprocessArtifactBridgeResultKind.ChildActive,
                    ChildRunId: childRunId,
                    Contract: contract);
            }

            if (childState.Status != ProcessRuntimeStatus.Completed)
            {
                var stoppedChild = ResolveStoppedChild(childState, childGroup);
                return new ParentSubprocessArtifactBridgeResult(
                    ResolveStoppedChildResultKind(childState.Status),
                    ChildRunId: childRunId,
                    Contract: contract,
                    StoppedChild: stoppedChild);
            }

            if (TryResolveChildOutput(
                    childRunId,
                    childGroup,
                    childState,
                    contract.NoGoChildOutputs,
                    contract.ForwardedChildContextArtifacts,
                    out var noGoOutput,
                    out var noGoEvidenceRefs,
                    out var noGoVerifiedChildOutput,
                    out var noGoForwardedContextArtifacts,
                    out var noGoForwardedContextIssue))
            {
                if (noGoForwardedContextIssue is not null)
                {
                    return new ParentSubprocessArtifactBridgeResult(
                        ParentSubprocessArtifactBridgeResultKind.ChildForwardedContextUnavailable,
                        ChildRunId: childRunId,
                        Contract: contract,
                        BridgeEvidenceRefs: noGoEvidenceRefs,
                        ForwardedContextIssue: noGoForwardedContextIssue);
                }

                if (!string.IsNullOrWhiteSpace(noGoOutput.ParentBranchOutcomeKey))
                {
                    var bridgedNoGoOutcome = CreateBridgedOutcome(
                        assignment,
                        childRunId,
                        childState.UpdatedAtUtc,
                        noGoOutput,
                        noGoEvidenceRefs,
                        noGoVerifiedChildOutput,
                        noGoForwardedContextArtifacts,
                        ChildOutputDisposition.NoGo);
                    return new ParentSubprocessArtifactBridgeResult(
                        ParentSubprocessArtifactBridgeResultKind.NoGoChildOutputBridged,
                        ChildRunId: childRunId,
                        Contract: contract,
                        BridgeEvidenceRefs: noGoEvidenceRefs,
                        BridgedOutcome: bridgedNoGoOutcome);
                }

                return new ParentSubprocessArtifactBridgeResult(
                    ParentSubprocessArtifactBridgeResultKind.NoGoChildOutputFound,
                    ChildRunId: childRunId,
                    Contract: contract,
                    BridgeEvidenceRefs: noGoEvidenceRefs);
            }

            if (!TryResolveChildOutput(
                    childRunId,
                    childGroup,
                    childState,
                    contract.AcceptedChildOutputs,
                    contract.ForwardedChildContextArtifacts,
                    out var acceptedOutput,
                    out var acceptedEvidenceRefs,
                    out var acceptedVerifiedChildOutput,
                    out var acceptedForwardedContextArtifacts,
                    out var acceptedForwardedContextIssue))
            {
                return new ParentSubprocessArtifactBridgeResult(
                    ParentSubprocessArtifactBridgeResultKind.ChildCompletedWithoutAcceptedOutput,
                    ChildRunId: childRunId,
                    Contract: contract);
            }

            if (acceptedForwardedContextIssue is not null)
            {
                return new ParentSubprocessArtifactBridgeResult(
                    ParentSubprocessArtifactBridgeResultKind.ChildForwardedContextUnavailable,
                    ChildRunId: childRunId,
                    Contract: contract,
                    BridgeEvidenceRefs: acceptedEvidenceRefs,
                    ForwardedContextIssue: acceptedForwardedContextIssue);
            }

            var acceptedOutcome = CreateBridgedOutcome(
                assignment,
                childRunId,
                childState.UpdatedAtUtc,
                acceptedOutput,
                acceptedEvidenceRefs,
                acceptedVerifiedChildOutput,
                acceptedForwardedContextArtifacts,
                ChildOutputDisposition.Accepted);
            return new ParentSubprocessArtifactBridgeResult(
                ParentSubprocessArtifactBridgeResultKind.AcceptedChildOutputBridged,
                ChildRunId: childRunId,
                Contract: contract,
                BridgeEvidenceRefs: acceptedEvidenceRefs,
                BridgedOutcome: acceptedOutcome);
        }

        return ParentSubprocessArtifactBridgeResult.NoMatchingChildRun;
    }

    public async ValueTask<ParentSubprocessArtifactBridgeResult> ResolveFromOutputAsync(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(output);

        if (output.Status is not (ProcessStepOutcomeStatus.Blocked or ProcessStepOutcomeStatus.WaitingApproval) ||
            !CanWaitOnControlledChildRun(assignment))
        {
            return ParentSubprocessArtifactBridgeResult.NotSubprocess;
        }

        if (await TryResolvePendingChildRunAsync(
                assignment,
                output,
                stateStore,
                cancellationToken).ConfigureAwait(false) is { } pendingChildRunId)
        {
            return new ParentSubprocessArtifactBridgeResult(
                ParentSubprocessArtifactBridgeResultKind.ChildActive,
                ChildRunId: pendingChildRunId);
        }

        var existing = await ResolveExistingAsync(assignment, cancellationToken).ConfigureAwait(false);
        if (existing.Kind != ParentSubprocessArtifactBridgeResultKind.NoMatchingChildRun ||
            IsWaitingOnStoppedSubprocessOutcome(output))
        {
            return existing;
        }

        if (await TryResolveExistingPendingChildRunAsync(
                assignment,
                assignmentStore,
                stateStore,
                cancellationToken).ConfigureAwait(false) is { } existingPendingChildRunId)
        {
            return new ParentSubprocessArtifactBridgeResult(
                ParentSubprocessArtifactBridgeResultKind.ChildActive,
                ChildRunId: existingPendingChildRunId);
        }

        return ParentSubprocessArtifactBridgeResult.NoMatchingChildRun;
    }

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

    private ParentSubprocessBridgedOutcome CreateBridgedOutcome(
        ProcessRuntimeStepAssignment assignment,
        ProcessRunId childRunId,
        DateTimeOffset childCompletedAtUtc,
        ProcessSubprocessChildOutputContract childOutput,
        IReadOnlyList<string> evidenceRefs,
        ProcessSubprocessVerifiedChildArtifact verifiedChildOutput,
        IReadOnlyList<ParentSubprocessForwardedContextArtifact> forwardedContextArtifacts,
        ChildOutputDisposition disposition)
    {
        var syntheticExecutionRunId = CreateSyntheticSubprocessExecutionRunId(
            assignment,
            childRunId,
            childCompletedAtUtc,
            childOutput.ParentBranchOutcomeKey);
        var rawOutputHash = ComputeHash(
            $"{assignment.RunId}:{assignment.StepInstanceId}:completed-child:{childRunId}:{childCompletedAtUtc:O}:{disposition}:{childOutput.StepKey}:{childOutput.BranchOutcomeKey}:{childOutput.ParentBranchOutcomeKey}:{verifiedChildOutput.ArtifactRef}:{verifiedChildOutput.ContentHash}:{string.Join("|", evidenceRefs)}:{string.Join("|", forwardedContextArtifacts.Select(artifact => $"{artifact.BindingKey}:{artifact.ChildArtifactRef}"))}");
        return new ParentSubprocessBridgedOutcome(
            childRunId,
            childCompletedAtUtc,
            disposition,
            childOutput.StepKey,
            childOutput.ArtifactExpectationKey,
            childOutput.ParentBranchOutcomeKey,
            verifiedChildOutput,
            BuildCompletedSubprocessProcessStepOutcome(
                assignment,
                childRunId,
                childCompletedAtUtc,
                evidenceRefs,
                verifiedChildOutput,
                forwardedContextArtifacts,
                childOutput,
                disposition),
            evidenceRefs,
            forwardedContextArtifacts,
            rawOutputHash,
            syntheticExecutionRunId,
            [CreateSubprocessOutcomeReceipt(
                syntheticExecutionRunId,
                assignment,
                childRunId,
                evidenceRefs,
                childOutput.ParentBranchOutcomeKey,
                disposition)]);
    }

    private static ParentSubprocessArtifactBridgeResultKind ResolveStoppedChildResultKind(ProcessRuntimeStatus childStatus)
        => childStatus is ProcessRuntimeStatus.Blocked or ProcessRuntimeStatus.Escalated or ProcessRuntimeStatus.WaitingForUser
            ? ParentSubprocessArtifactBridgeResultKind.ChildStoppedBlocked
            : ParentSubprocessArtifactBridgeResultKind.ChildStoppedFailed;

    private static ParentSubprocessStoppedChild ResolveStoppedChild(
        ProcessRuntimeStateSnapshot childState,
        IEnumerable<ProcessRuntimeStepAssignment> childAssignments)
    {
        var childAssignmentList = childAssignments.ToArray();
        var receipt = childState.AppliedResults.LastOrDefault(result => result.Diagnostics.Count > 0) ??
            childState.AppliedResults.LastOrDefault();
        var stepState = receipt is null
            ? childState.Steps.LastOrDefault(step =>
                step.Status is ProcessRuntimeStepStatus.Blocked or
                    ProcessRuntimeStepStatus.Failed or
                    ProcessRuntimeStepStatus.Cancelled or
                    ProcessRuntimeStepStatus.WaitingApproval)
            : childState.Steps.FirstOrDefault(step => step.StepInstanceId == receipt.StepInstanceId);
        var stepInstanceId = receipt?.StepInstanceId ?? stepState?.StepInstanceId;
        var childAssignment = stepInstanceId is null
            ? null
            : childAssignmentList.FirstOrDefault(assignment => assignment.StepInstanceId == stepInstanceId);
        var diagnostics = receipt?.Diagnostics
            .Select(diagnostic => new ParentSubprocessChildDiagnostic(
                diagnostic.Code,
                diagnostic.SafeSummary,
                diagnostic.EvidenceHash,
                diagnostic.RetrySafety,
                diagnostic.Idempotency))
            .ToArray() ?? [];
        var stepKey = childAssignment?.StepKey ??
            childAssignmentList.FirstOrDefault()?.StepKey ??
            "unknown";
        return new ParentSubprocessStoppedChild(
            childState.Status,
            stepKey,
            stepInstanceId,
            stepState?.Status,
            diagnostics,
            receipt?.RecoveryDecision);
    }

    private bool TryResolveChildOutput(
        ProcessRunId childRunId,
        IEnumerable<ProcessRuntimeStepAssignment> childAssignments,
        ProcessRuntimeStateSnapshot childState,
        IReadOnlyList<ProcessSubprocessChildOutputContract> childOutputs,
        IReadOnlyList<ProcessSubprocessForwardedChildContextArtifactContract> forwardedContextArtifactContracts,
        out ProcessSubprocessChildOutputContract matchedOutput,
        out IReadOnlyList<string> evidenceRefs,
        out ProcessSubprocessVerifiedChildArtifact verifiedChildOutput,
        out IReadOnlyList<ParentSubprocessForwardedContextArtifact> forwardedContextArtifacts,
        out ParentSubprocessForwardedContextIssue? forwardedContextIssue)
    {
        matchedOutput = new ProcessSubprocessChildOutputContract();
        evidenceRefs = [];
        verifiedChildOutput = null!;
        forwardedContextArtifacts = [];
        forwardedContextIssue = null;
        var childManagedArtifactRoot = $"artifacts/process-runs/{childRunId.Value:D}";
        var assignmentsByStepKey = childAssignments
            .Where(childAssignment => !string.IsNullOrWhiteSpace(childAssignment.StepKey))
            .GroupBy(childAssignment => childAssignment.StepKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.CreatedAtUtc).First(), StringComparer.OrdinalIgnoreCase);
        foreach (var childOutput in childOutputs)
        {
            if (string.IsNullOrWhiteSpace(childOutput.StepKey))
            {
                continue;
            }

            if (!assignmentsByStepKey.TryGetValue(childOutput.StepKey, out var childAssignment) ||
                !TryResolveAcceptedChildOutputReceipt(
                    childAssignment,
                    childState,
                    childOutput,
                    out var childOutputReceipt))
            {
                continue;
            }

            var candidateRef = $"{childManagedArtifactRoot}/steps/{SanitizeManagedArtifactPathSegment(childOutput.StepKey)}.md";
            if (childArtifactVerifier.CanBridge(
                    candidateRef,
                    childOutput.StepKey,
                    childOutput.ArtifactExpectationKey,
                    childOutput.BranchOutcomeKey,
                    childOutputReceipt.ContentHash,
                    out verifiedChildOutput))
            {
                matchedOutput = childOutput;
                evidenceRefs = [candidateRef];
                if (!forwardedContextResolver.TryResolve(
                        childRunId,
                        childAssignment,
                        childState,
                        forwardedContextArtifactContracts,
                        out forwardedContextArtifacts,
                        out forwardedContextIssue))
                {
                    return true;
                }

                if (!ParentSubprocessRuntimeEnvelopeBudget.IsWithinLimit(
                        verifiedChildOutput,
                        forwardedContextArtifacts,
                        out var combinedEnvelopeCharacters))
                {
                    forwardedContextIssue = new ParentSubprocessForwardedContextIssue(
                        "process.adapter.subprocess_handoff_size_limit_exceeded",
                        $"The verified child output and forwarded context require {combinedEnvelopeCharacters} characters, which exceeds the runtime subprocess handoff limit {ParentSubprocessRuntimeEnvelopeBudget.MaxCombinedEnvelopeCharacters}.",
                        $"{childRunId.Value:D}:{childAssignment.StepInstanceId.Value:D}:{combinedEnvelopeCharacters}");
                }

                return true;
            }
        }

        return false;
    }

    private static bool TryResolveAcceptedChildOutputReceipt(
        ProcessRuntimeStepAssignment childAssignment,
        ProcessRuntimeStateSnapshot childState,
        ProcessSubprocessChildOutputContract childOutput,
        out StrategyResultArtifactReceipt childOutputReceipt)
    {
        childOutputReceipt = null!;
        var childOutputSteps = childState.Steps
            .Where(step => step.StepInstanceId == childAssignment.StepInstanceId)
            .ToArray();
        if (childOutputSteps.Length != 1)
        {
            return false;
        }

        var childOutputStep = childOutputSteps[0];
        if (childOutputStep.Status != ProcessRuntimeStepStatus.Completed ||
            childOutputStep.CompletedResultKey is null)
        {
            return false;
        }

        var declaredProducedSlotIds = childAssignment.ProducedArtifactSlotIds
            .Concat(childOutputStep.ProducedArtifactSlots)
            .Distinct()
            .ToHashSet();
        var expectedOutputSlotIds = childOutputStep.ArtifactDescriptors
            .Where(descriptor =>
                declaredProducedSlotIds.Contains(descriptor.SlotId) &&
                string.Equals(descriptor.StepKey, childOutput.StepKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(descriptor.ArtifactExpectationKey, childOutput.ArtifactExpectationKey, StringComparison.OrdinalIgnoreCase))
            .Select(descriptor => descriptor.SlotId)
            .Distinct()
            .ToArray();
        if (expectedOutputSlotIds.Length != 1)
        {
            return false;
        }

        var matchingReceipts = childState.AppliedResults
            .Where(receipt =>
                receipt.StepInstanceId == childAssignment.StepInstanceId &&
                receipt.IdempotencyKey == childOutputStep.CompletedResultKey &&
                receipt.AppliedStepStatus == ProcessRuntimeStepStatus.Completed)
            .ToArray();
        if (matchingReceipts.Length != 1)
        {
            return false;
        }

        var matchingArtifacts = matchingReceipts[0].ProducedArtifacts
            .Where(artifact =>
                expectedOutputSlotIds.Contains(artifact.SlotId) &&
                !string.IsNullOrWhiteSpace(artifact.ContentHash))
            .ToArray();
        if (matchingArtifacts.Length != 1)
        {
            return false;
        }

        childOutputReceipt = matchingArtifacts[0];
        return true;
    }

    private static ProcessStepOutcomeResult BuildCompletedSubprocessProcessStepOutcome(
        ProcessRuntimeStepAssignment assignment,
        ProcessRunId childRunId,
        DateTimeOffset childCompletedAtUtc,
        IReadOnlyList<string> evidenceRefs,
        ProcessSubprocessVerifiedChildArtifact verifiedChildOutput,
        IReadOnlyList<ParentSubprocessForwardedContextArtifact> forwardedContextArtifacts,
        ProcessSubprocessChildOutputContract childOutput,
        ChildOutputDisposition disposition)
    {
        var childRunValue = childRunId.Value.ToString("D");
        var childCompletedAt = childCompletedAtUtc.UtcDateTime.ToString("u", CultureInfo.InvariantCulture);
        var isNoGo = disposition == ChildOutputDisposition.NoGo;
        var parentBranchOutcomeKey = childOutput.ParentBranchOutcomeKey;
        var branchRouteSummary = string.IsNullOrWhiteSpace(parentBranchOutcomeKey)
            ? "without a parent branch route"
            : $"on configured parent branch '{parentBranchOutcomeKey}'";
        return new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = $"Matching child process run {childRunValue} completed at {childCompletedAt}; its {(isNoGo ? "no-go" : "accepted")} output is bridged {branchRouteSummary} from typed managed child evidence.",
            BranchOutcomeKey = parentBranchOutcomeKey,
            BranchOutcomeTitle = string.IsNullOrWhiteSpace(parentBranchOutcomeKey)
                ? string.Empty
                : isNoGo
                    ? "Child no-go routed to configured parent branch"
                    : "Child output routed to configured parent branch",
            EvidenceRefs = evidenceRefs,
            NextActions = [],
            HumanReadableSummaryMarkdown = $"""
            ## Subprocess handoff completed

            The process runtime completed parent step `{assignment.StepKey}` from matching completed child process run `{childRunValue}`.

            Child output disposition: `{(isNoGo ? "no-go" : "accepted")}`.
            Child output step: `{childOutput.StepKey}`.
            Child artifact expectation: `{childOutput.ArtifactExpectationKey}`.
            Parent branch route: `{(string.IsNullOrWhiteSpace(parentBranchOutcomeKey) ? "none" : parentBranchOutcomeKey)}`.

            ## Child evidence

            {FormatMarkdownEvidenceList(evidenceRefs)}

            {ParentSubprocessVerifiedChildOutputEnvelope.Format(verifiedChildOutput)}

            {ParentSubprocessForwardedContextEnvelope.Format(forwardedContextArtifacts)}
            """
        };
    }

    private static ToolExecutionReceiptRecord CreateSubprocessOutcomeReceipt(
        Guid syntheticExecutionRunId,
        ProcessRuntimeStepAssignment assignment,
        ProcessRunId childRunId,
        IReadOnlyList<string> evidenceRefs,
        string parentBranchOutcomeKey,
        ChildOutputDisposition disposition)
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
            ProcessSubprocessState.SubprocessLaunchToolName,
            "ProcessRuntime",
            "NotRequired",
            "Process runtime resolved a previously completed matching child subprocess.",
            $"definitionKey={childDefinitionKey}; parentRunId={assignment.RunId.Value:D}; parentStepId={assignment.StepInstanceId.Value:D}; childRunId={childRunId.Value:D}; childOutputDisposition={disposition}; parentBranchOutcomeKey={parentBranchOutcomeKey}",
            ".",
            $"Succeeded: matching child run {childRunId.Value:D} completed with {disposition} typed evidence refs: {evidenceSummary}",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
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

    private static bool RequiresRuntimeOwnedSubprocessLaunch(ProcessRuntimeStepAssignment assignment)
        => ProcessRuntimeLaunchVariables.TryReadProcessStepSubprocessDefinitionKey(
            assignment.LaunchVariables,
            out _);

    private static bool CanWaitOnControlledChildRun(ProcessRuntimeStepAssignment assignment)
        => assignment.AllowedOperations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase) ||
           string.Equals(assignment.OperationTargetScope, ProcessOperationContractNames.ExternalActionControlled, StringComparison.OrdinalIgnoreCase);

    private static bool IsSameProcessTree(
        ProcessRuntimeStateSnapshot currentState,
        ProcessRuntimeStateSnapshot candidateState)
        => candidateState.RootRunId == currentState.RootRunId ||
           candidateState.RootRunId == currentState.RunId ||
           candidateState.RunId == currentState.RootRunId;

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

    private static IReadOnlyList<ProcessRunId> ExtractReferencedRunIds(ProcessStepOutcomeResult output)
    {
        var runIds = new List<ProcessRunId>();
        foreach (var text in EnumerateOutcomeText(output))
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (Match match in ProcessRunIdRegex.Matches(text))
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

    private static string SanitizeManagedArtifactPathSegment(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? "step"
            : value.Trim();
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-');
        }

        return builder.Length == 0 ? "step" : builder.ToString();
    }

    private static Guid CreateSyntheticSubprocessExecutionRunId(
        ProcessRuntimeStepAssignment assignment,
        ProcessRunId childRunId,
        DateTimeOffset childUpdatedAtUtc,
        string parentBranchOutcomeKey)
    {
        var input = Encoding.UTF8.GetBytes(
            $"completed-child:{assignment.RunId.Value:D}:{assignment.StepInstanceId.Value:D}:{childRunId.Value:D}:{childUpdatedAtUtc.UtcDateTime:O}:{parentBranchOutcomeKey}");
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        return new Guid(hash[..16]);
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return $"sha256:{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }

    private static bool ContainsAny(string text, params string[] needles)
        => needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));

    private static readonly Regex ProcessRunIdRegex = new(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
}
