using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessBlockedRunRecoveryCoordinator(
    IProcessRuntimeStateStore stateStore,
    IProcessInstancePlanStore planStore,
    IProcessRuntimeStepAssignmentStore assignmentStore,
    IProcessBlockedRunRecoveryCommandExecutor commandExecutor,
    IProcessBlockedRunRecoveryPolicyCatalog policyCatalog) : IProcessBlockedRunRecoveryCoordinator
{
    public async Task<ProcessBlockedRunRecoveryResult> TryRecoverAsync(
        ProcessRunId runId,
        string requestedBy,
        CancellationToken cancellationToken = default)
    {
        var state = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Process run '{runId}' was not found.");
        if (state.Status != ProcessRuntimeStatus.Blocked)
        {
            return Result(
                runId,
                ProcessBlockedRunRecoveryOutcome.NotBlocked,
                state.Status,
                diagnostics: ["The run is no longer blocked."]);
        }

        var plan = await planStore.LoadAsync(state.PlanId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Process run '{runId}' references missing plan '{state.PlanId}'.");
        var assignments = await assignmentStore
            .LoadByRunAsync(runId, cancellationToken)
            .ConfigureAwait(false);
        var candidate = FindLatestRecoveryCandidate(state);
        if (candidate is null)
        {
            return Result(
                runId,
                ProcessBlockedRunRecoveryOutcome.RequiresAttention,
                state.Status,
                diagnostics: ["No blocked step has a durable manager-required recovery receipt."]);
        }

        var receipt = candidate.Value.Receipt;
        var decision = receipt.RecoveryDecision!;
        var childRecovery = await ResolveChildRecoveryEvidenceAsync(
                state,
                candidate.Value.Step,
                receipt,
                decision,
                cancellationToken)
            .ConfigureAwait(false);
        if (!childRecovery.IsValid)
        {
            return Result(
                runId,
                ProcessBlockedRunRecoveryOutcome.RequiresAttention,
                state.Status,
                diagnostics: [childRecovery.Issue]);
        }

        if (!TryResolveCommand(
                state,
                plan,
                assignments,
                candidate.Value.Step,
                receipt,
                decision,
                childRecovery.Evidence,
                out var command,
                out var issue))
        {
            return Result(
                runId,
                ProcessBlockedRunRecoveryOutcome.RequiresAttention,
                state.Status,
                diagnostics: [issue]);
        }

        var commandResult = await commandExecutor
            .ExecuteAsync(command, NormalizeRequestedBy(requestedBy), cancellationToken)
            .ConfigureAwait(false);
        return commandResult.Succeeded
            ? new ProcessBlockedRunRecoveryResult(
                runId,
                ProcessBlockedRunRecoveryOutcome.Recovered,
                command.ActionKind,
                command.TargetStepInstanceId,
                command.Policy,
                commandResult.Status,
                commandResult.Diagnostics)
            : new ProcessBlockedRunRecoveryResult(
                runId,
                ProcessBlockedRunRecoveryOutcome.RequiresAttention,
                command.ActionKind,
                command.TargetStepInstanceId,
                command.Policy,
                commandResult.Status,
                commandResult.Diagnostics.Count == 0
                    ? ["The typed rework command was rejected without a diagnostic."]
                    : commandResult.Diagnostics);
    }

    private static RecoveryCandidate? FindLatestRecoveryCandidate(ProcessRuntimeStateSnapshot state)
    {
        var blockedStepIds = state.Steps
            .Where(step =>
                step.IsExecutable &&
                step.Status == ProcessRuntimeStepStatus.Blocked)
            .Select(step => step.StepInstanceId)
            .ToHashSet();
        if (blockedStepIds.Count == 0)
        {
            return null;
        }

        var receipt = ProcessRuntimeBlockedRecoveryAuthorizationRules
            .FindLatestBlockedManagerRequiredReceipt(state, blockedStepIds);
        if (receipt is null)
        {
            return null;
        }

        var step = state.Steps.First(item => item.StepInstanceId == receipt.StepInstanceId);
        return new RecoveryCandidate(step, receipt);
    }

    private bool TryResolveCommand(
        ProcessRuntimeStateSnapshot state,
        ProcessInstancePlan plan,
        IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
        ProcessRuntimeStepState blockedStep,
        StrategyResultReceipt receipt,
        ProcessRecoveryDecisionReceipt decision,
        RelatedChildRecoveryEvidence? childRecoveryEvidence,
        out ProcessBlockedRunRecoveryCommand command,
        out string issue)
    {
        command = null!;
        if (string.IsNullOrWhiteSpace(decision.DiagnosticFingerprint))
        {
            issue = "The recovery receipt has no stable diagnostic fingerprint.";
            return false;
        }

        if (!receipt.Diagnostics.Any(diagnostic =>
                string.Equals(
                    diagnostic.Code,
                    decision.SourceDiagnosticCode,
                    StringComparison.OrdinalIgnoreCase)))
        {
            issue = "The recovery decision source code is not grounded in the durable diagnostic receipts.";
            return false;
        }

        if (IsPolicyOrCapabilityBoundary(decision))
        {
            issue = "The blocker crosses a policy, approval, or capability boundary and cannot be recovered automatically.";
            return false;
        }

        if (HasExhaustedClassifierBudget(decision))
        {
            issue = "The runtime classifier already exhausted the typed automatic retry budget.";
            return false;
        }

        if (plan.Security.RequiredApprovalKeys.Count > 0)
        {
            issue = "The process plan requires explicit approvals and cannot use automatic manager rework.";
            return false;
        }

        if (!TryResolveTarget(
                state,
                blockedStep,
                decision,
                out var targetStep,
                out var actionKind,
                out issue))
        {
            return false;
        }

        var targetAssignment = assignments.FirstOrDefault(assignment =>
            assignment.StepInstanceId == targetStep.StepInstanceId);
        if (targetAssignment is null)
        {
            issue =
                $"Recovery target step '{targetStep.StepInstanceId}' has no durable runtime assignment.";
            return false;
        }

        var policy = policyCatalog.Resolve(
            state,
            plan,
            blockedStep,
            targetAssignment,
            receipt,
            decision);
        if (policy == ProcessBlockedRunRecoveryPolicy.None)
        {
            issue = "The typed diagnostics do not satisfy an automatic blocked-run recovery policy.";
            return false;
        }

        var phase = ResolvePhase(blockedStep, targetStep, decision);
        var authorization = new ProcessRuntimeBlockedRecoveryAuthorization(
            state.UpdatedAtUtc,
            ProcessRuntimeStatus.Blocked,
            blockedStep.StepInstanceId,
            receipt.IdempotencyKey,
            decision.DiagnosticFingerprint,
            decision.RouteKind,
            decision.ResponsibleStepInstanceId,
            phase)
        {
            RelatedChildRunId = childRecoveryEvidence?.RunId,
            ExpectedRelatedChildUpdatedAtUtc = childRecoveryEvidence?.UpdatedAtUtc,
            ExpectedChildLineageEvidence = childRecoveryEvidence?.LineageEvidence
        };
        var authorizationIssue = ProcessRuntimeBlockedRecoveryAuthorizationRules.FindIssue(
            state,
            targetStep.StepInstanceId,
            authorization);
        if (authorizationIssue is not null)
        {
            issue = authorizationIssue;
            return false;
        }

        command = new ProcessBlockedRunRecoveryCommand(
            state.RunId,
            blockedStep.StepInstanceId,
            targetStep.StepInstanceId,
            actionKind,
            policy,
            receipt.IdempotencyKey,
            decision.DiagnosticFingerprint,
            decision.RouteKind,
            decision.ResponsibleStepInstanceId,
            phase,
            state.UpdatedAtUtc)
        {
            RelatedChildRunId = childRecoveryEvidence?.RunId,
            ExpectedRelatedChildUpdatedAtUtc = childRecoveryEvidence?.UpdatedAtUtc,
            ExpectedChildLineageEvidence = childRecoveryEvidence?.LineageEvidence
        };
        issue = string.Empty;
        return true;
    }

    private static ProcessRuntimeBlockedRecoveryPhase ResolvePhase(
        ProcessRuntimeStepState blockedStep,
        ProcessRuntimeStepState targetStep,
        ProcessRecoveryDecisionReceipt decision)
    {
        if (decision.RouteKind != ProcessRecoveryRouteKind.UpstreamStepRework)
        {
            return decision.RouteKind == ProcessRecoveryRouteKind.ChildRunPropagation
                ? ProcessRuntimeBlockedRecoveryPhase.CompletedChildConsumer
                : ProcessRuntimeBlockedRecoveryPhase.CurrentStep;
        }

        return targetStep.StepInstanceId == blockedStep.StepInstanceId
            ? ProcessRuntimeBlockedRecoveryPhase.RestoredConsumer
            : ProcessRuntimeBlockedRecoveryPhase.UpstreamProducer;
    }

    private static bool TryResolveTarget(
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState blockedStep,
        ProcessRecoveryDecisionReceipt decision,
        out ProcessRuntimeStepState targetStep,
        out ProcessBlockedRunRecoveryActionKind actionKind,
        out string issue)
    {
        var restoredUpstreamInput =
            decision.RouteKind == ProcessRecoveryRouteKind.UpstreamStepRework &&
            ProcessRuntimeArtifactContracts.DependenciesSatisfied(state, blockedStep) &&
            ProcessRuntimeArtifactContracts.RequiredArtifactsAvailable(state, blockedStep);
        var targetStepId = restoredUpstreamInput
            ? blockedStep.StepInstanceId
            : decision.RouteKind switch
            {
                ProcessRecoveryRouteKind.ManagerAction or ProcessRecoveryRouteKind.CurrentStepRetry =>
                    decision.ResponsibleStepInstanceId ?? blockedStep.StepInstanceId,
                ProcessRecoveryRouteKind.UpstreamStepRework =>
                    decision.ResponsibleStepInstanceId,
                ProcessRecoveryRouteKind.ChildRunPropagation =>
                    decision.ResponsibleStepInstanceId ?? blockedStep.StepInstanceId,
                _ => null
            };
        if (targetStepId is null)
        {
            targetStep = null!;
            actionKind = ProcessBlockedRunRecoveryActionKind.None;
            issue = $"Recovery route '{decision.RouteKind}' has no automatic typed rework target.";
            return false;
        }

        var resolvedTarget = state.Steps.FirstOrDefault(step => step.StepInstanceId == targetStepId);
        if (resolvedTarget is null)
        {
            targetStep = null!;
            actionKind = ProcessBlockedRunRecoveryActionKind.None;
            issue = $"Recovery target step '{targetStepId}' is not present in the current run.";
            return false;
        }

        var authorizedCompletedUpstreamTarget =
            decision.RouteKind == ProcessRecoveryRouteKind.UpstreamStepRework &&
            !restoredUpstreamInput &&
            resolvedTarget.StepInstanceId == decision.ResponsibleStepInstanceId &&
            resolvedTarget.Status == ProcessRuntimeStepStatus.Completed;
        if (!resolvedTarget.IsExecutable ||
            !authorizedCompletedUpstreamTarget &&
            resolvedTarget.Status is not (
                ProcessRuntimeStepStatus.Waiting or
                ProcessRuntimeStepStatus.Blocked or
                ProcessRuntimeStepStatus.Failed))
        {
            targetStep = null!;
            actionKind = ProcessBlockedRunRecoveryActionKind.None;
            issue =
                $"Recovery target step '{targetStepId}' has non-reworkable status '{resolvedTarget.Status}'.";
            return false;
        }

        targetStep = resolvedTarget;
        actionKind = targetStep.StepInstanceId == blockedStep.StepInstanceId
            ? ProcessBlockedRunRecoveryActionKind.CurrentStepRework
            : ProcessBlockedRunRecoveryActionKind.UpstreamStepRework;
        issue = string.Empty;
        return true;
    }

    private async Task<ChildRecoveryEvidenceResolution> ResolveChildRecoveryEvidenceAsync(
        ProcessRuntimeStateSnapshot parentState,
        ProcessRuntimeStepState blockedStep,
        StrategyResultReceipt receipt,
        ProcessRecoveryDecisionReceipt decision,
        CancellationToken cancellationToken)
    {
        if (decision.RouteKind != ProcessRecoveryRouteKind.ChildRunPropagation)
        {
            return ChildRecoveryEvidenceResolution.NotRequired;
        }

        if (decision.FailureCategory != ProcessFailureCategory.ChildRunBlocked ||
            decision.ResponsibleStepInstanceId != blockedStep.StepInstanceId ||
            decision.RelatedChildRunId is not { } childRunId)
        {
            return ChildRecoveryEvidenceResolution.Invalid(
                "Child-run recovery requires a typed child id and the blocked parent step as the responsible target.");
        }

        var matchingDiagnosticChildIds = receipt.Diagnostics
            .Where(diagnostic =>
                string.Equals(
                    diagnostic.Code,
                    decision.SourceDiagnosticCode,
                    StringComparison.OrdinalIgnoreCase) &&
                diagnostic.RelatedChildRunId is not null)
            .Select(diagnostic => diagnostic.RelatedChildRunId!.Value)
            .Distinct()
            .ToArray();
        if (matchingDiagnosticChildIds.Length != 1 ||
            matchingDiagnosticChildIds[0] != childRunId)
        {
            return ChildRecoveryEvidenceResolution.Invalid(
                "The child-run recovery decision is not grounded in one exact typed child diagnostic.");
        }

        var childState = await stateStore
            .LoadAsync(childRunId, cancellationToken)
            .ConfigureAwait(false);
        if (childState is null)
        {
            return ChildRecoveryEvidenceResolution.Invalid(
                $"Related child run '{childRunId}' was not found.");
        }

        if (childState.Status != ProcessRuntimeStatus.Completed)
        {
            return ChildRecoveryEvidenceResolution.Invalid(
                $"Related child run '{childRunId}' has status '{childState.Status}', not '{ProcessRuntimeStatus.Completed}'.");
        }

        if (childState.RootRunId != parentState.RootRunId)
        {
            return ChildRecoveryEvidenceResolution.Invalid(
                $"Related child run '{childRunId}' is outside parent run '{parentState.RunId}' process tree.");
        }

        var linkedAssignmentSearch = await assignmentStore
            .FindByLaunchVariablesBoundedAsync(
                ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                    parentState.RunId,
                    blockedStep.StepInstanceId),
                ProcessRuntimeChildLineageEvidenceRules.MaximumLinkedChildRunCount,
                cancellationToken)
            .ConfigureAwait(false);
        if (linkedAssignmentSearch.LimitExceeded)
        {
            return ChildRecoveryEvidenceResolution.Invalid(
                "The parent step has too many linked child runs for bounded automatic recovery.");
        }

        var linkedAssignments = linkedAssignmentSearch.Assignments;
        if (!linkedAssignments.Any(assignment => assignment.RunId == childRunId))
        {
            return ChildRecoveryEvidenceResolution.Invalid(
                $"Related child run '{childRunId}' has no durable assignment link to parent step '{blockedStep.StepInstanceId}'.");
        }

        var linkedChildRuns = linkedAssignments
            .GroupBy(assignment => assignment.RunId)
            .Select(group => new
            {
                RunId = group.Key,
                CreatedAtUtc = group.Max(assignment => assignment.CreatedAtUtc)
            })
            .OrderByDescending(child => child.CreatedAtUtc)
            .ThenByDescending(child => child.RunId.Value)
            .ToArray();
        if (linkedChildRuns[0].RunId != childRunId)
        {
            return ChildRecoveryEvidenceResolution.Invalid(
                $"Related child run '{childRunId}' is not the newest durable child linked to parent step '{blockedStep.StepInstanceId}'.");
        }

        var siblingRunIds = linkedChildRuns
            .Select(child => child.RunId)
            .Where(runId => runId != childRunId)
            .ToArray();
        if (siblingRunIds.Length > IProcessRuntimeStateStore.MaximumBatchRunCount)
        {
            return ChildRecoveryEvidenceResolution.Invalid(
                "The parent step has too many linked child runs for bounded automatic recovery.");
        }

        var siblingStates = await stateStore
            .LoadManyAsync(siblingRunIds, cancellationToken)
            .ConfigureAwait(false);
        var loadedSiblingRunIds = siblingStates
            .Select(sibling => sibling.RunId)
            .ToHashSet();
        if (siblingStates.Count != siblingRunIds.Length ||
            !loadedSiblingRunIds.SetEquals(siblingRunIds))
        {
            return ChildRecoveryEvidenceResolution.Invalid(
                "The parent step has linked child runs without exact durable runtime state evidence.");
        }

        if (siblingStates.Any(sibling =>
                !ProcessRuntimeTerminalStates.IsChildRunStopped(sibling.Status)))
        {
            return ChildRecoveryEvidenceResolution.Invalid(
                "The parent step still has a linked child run that is not stopped.");
        }

        var linkedChildStates = siblingStates.ToDictionary(sibling => sibling.RunId);
        linkedChildStates.Add(childState.RunId, childState);
        var lineageEvidence = ProcessRuntimeChildLineageEvidence.Create(
            parentState.RunId,
            blockedStep.StepInstanceId,
            linkedChildRuns.Select(link =>
            {
                var linkedState = linkedChildStates[link.RunId];
                return new ProcessRuntimeLinkedChildEvidence(
                    linkedState.RunId,
                    linkedState.RootRunId,
                    linkedState.Status,
                    linkedState.UpdatedAtUtc,
                    link.CreatedAtUtc);
            }));
        var lineageIssue = ProcessRuntimeChildLineageEvidenceRules.FindIssue(
            lineageEvidence,
            parentState.RunId,
            blockedStep.StepInstanceId,
            parentState.RootRunId,
            childRunId,
            childState.UpdatedAtUtc);
        if (lineageIssue is not null)
        {
            return ChildRecoveryEvidenceResolution.Invalid(lineageIssue);
        }

        return ChildRecoveryEvidenceResolution.Valid(
            new RelatedChildRecoveryEvidence(
                childRunId,
                childState.UpdatedAtUtc,
                lineageEvidence));
    }

    private static bool IsPolicyOrCapabilityBoundary(
        ProcessRecoveryDecisionReceipt decision)
    {
        return decision.FailureCategory is
            ProcessFailureCategory.MissingCapability or
            ProcessFailureCategory.DeniedCapability or
            ProcessFailureCategory.PolicyViolation;
    }

    private static bool HasExhaustedClassifierBudget(ProcessRecoveryDecisionReceipt decision)
    {
        return (decision.MaximumAutomaticRetryAttempts > 0 &&
                decision.AutomaticRetryAttempt > decision.MaximumAutomaticRetryAttempts) ||
               (decision.MaximumSameDiagnosticFingerprintAttempts > 0 &&
                decision.SameDiagnosticFingerprintAttempt >
                decision.MaximumSameDiagnosticFingerprintAttempts);
    }

    private static ProcessBlockedRunRecoveryResult Result(
        ProcessRunId runId,
        ProcessBlockedRunRecoveryOutcome outcome,
        ProcessRuntimeStatus status,
        IReadOnlyList<string> diagnostics)
    {
        return new ProcessBlockedRunRecoveryResult(
            runId,
            outcome,
            ProcessBlockedRunRecoveryActionKind.None,
            TargetStepInstanceId: null,
            ProcessBlockedRunRecoveryPolicy.None,
            status,
            diagnostics);
    }

    private static string NormalizeRequestedBy(string requestedBy)
    {
        return string.IsNullOrWhiteSpace(requestedBy)
            ? "process-blocked-run-recovery"
            : requestedBy.Trim();
    }

    private readonly record struct RecoveryCandidate(
        ProcessRuntimeStepState Step,
        StrategyResultReceipt Receipt);

    private sealed record RelatedChildRecoveryEvidence(
        ProcessRunId RunId,
        DateTimeOffset UpdatedAtUtc,
        ProcessRuntimeChildLineageEvidence LineageEvidence);

    private readonly record struct ChildRecoveryEvidenceResolution(
        RelatedChildRecoveryEvidence? Evidence,
        string Issue)
    {
        public bool IsValid => string.IsNullOrWhiteSpace(Issue);

        public static ChildRecoveryEvidenceResolution NotRequired { get; } = new(null, string.Empty);

        public static ChildRecoveryEvidenceResolution Valid(RelatedChildRecoveryEvidence evidence)
            => new(evidence, string.Empty);

        public static ChildRecoveryEvidenceResolution Invalid(string issue)
            => new(null, issue);
    }
}
