using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessBlockedRunRecoveryCoordinator(
    IProcessRuntimeStateStore stateStore,
    IProcessInstancePlanStore planStore,
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
        if (!TryResolveCommand(
                state,
                plan,
                candidate.Value.Step,
                receipt,
                decision,
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
        ProcessRuntimeStepState blockedStep,
        StrategyResultReceipt receipt,
        ProcessRecoveryDecisionReceipt decision,
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

        var policy = policyCatalog.Resolve(state, plan, blockedStep, receipt, decision);
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
            phase);
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
            state.UpdatedAtUtc);
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
            return ProcessRuntimeBlockedRecoveryPhase.CurrentStep;
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
}
