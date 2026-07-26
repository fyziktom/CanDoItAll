using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public static class ProcessRuntimeBlockedRecoveryAuthorizationRules
{
    public const int MaximumActionsPerSourceBlockedStep = 2;

    public static StrategyResultReceipt? FindLatestReceipt(
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId stepInstanceId)
    {
        ArgumentNullException.ThrowIfNull(state);

        return SelectLatest(
            state.AppliedResults
            .Select((receipt, index) => new ReceiptCandidate(receipt, index))
            .Where(candidate => candidate.Receipt.StepInstanceId == stepInstanceId)
            .ToArray(),
            HasCanonicalSequence(state.AppliedResults));
    }

    public static StrategyResultReceipt? FindLatestBlockedManagerRequiredReceipt(
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId stepInstanceId)
    {
        return FindLatestBlockedManagerRequiredReceipt(
            state,
            new HashSet<ProcessStepInstanceId> { stepInstanceId });
    }

    public static StrategyResultReceipt? FindLatestBlockedManagerRequiredReceipt(
        ProcessRuntimeStateSnapshot state,
        IReadOnlySet<ProcessStepInstanceId> stepInstanceIds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(stepInstanceIds);

        var candidates = state.AppliedResults
            .Select((receipt, index) => new ReceiptCandidate(receipt, index))
            .Where(candidate =>
                stepInstanceIds.Contains(candidate.Receipt.StepInstanceId) &&
                candidate.Receipt.Outcome == StrategyOutcome.NeedsManager &&
                candidate.Receipt.AppliedStepStatus == ProcessRuntimeStepStatus.Blocked &&
                candidate.Receipt.RecoveryDecision?.DecisionKind ==
                ProcessRecoveryDecisionKind.ManagerRequired)
            .ToArray();
        return SelectLatest(candidates, HasCanonicalSequence(state.AppliedResults));
    }

    private static StrategyResultReceipt? SelectLatest(
        ReceiptCandidate[] candidates,
        bool hasCanonicalSequence)
    {
        if (candidates.Length == 0)
        {
            return null;
        }

        return candidates
            .OrderByDescending(candidate =>
                hasCanonicalSequence
                    ? candidate.Receipt.AppliedSequence
                    : candidate.Index + 1L)
            .ThenByDescending(candidate => candidate.Receipt.IdempotencyKey.Value)
            .First()
            .Receipt;
    }

    private static bool HasCanonicalSequence(
        IReadOnlyList<StrategyResultReceipt> receipts)
    {
        if (receipts.All(receipt => receipt.AppliedSequence == 0))
        {
            return false;
        }

        if (receipts.Any(receipt => receipt.AppliedSequence <= 0) ||
            receipts.Select(receipt => receipt.AppliedSequence).Distinct().Count() != receipts.Count)
        {
            throw new InvalidOperationException(
                "Process result receipt sequences are mixed, missing, or duplicated; automatic recovery is denied.");
        }

        return true;
    }

    public static string? FindIssue(
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId targetStepInstanceId,
        ProcessRuntimeBlockedRecoveryAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(authorization);

        if (authorization.ExpectedStateStatus != ProcessRuntimeStatus.Blocked)
        {
            return
                $"Expected status must be '{ProcessRuntimeStatus.Blocked}', but authorization specified '{authorization.ExpectedStateStatus}'.";
        }

        if (state.Status != authorization.ExpectedStateStatus ||
            state.UpdatedAtUtc != authorization.ExpectedStateUpdatedAtUtc)
        {
            return
                $"Expected blocked state version '{authorization.ExpectedStateUpdatedAtUtc:O}', but the loaded state is '{state.Status}' at '{state.UpdatedAtUtc:O}'.";
        }

        var sourceStep = state.Steps.FirstOrDefault(candidate =>
            candidate.StepInstanceId == authorization.SourceBlockedStepInstanceId);
        if (sourceStep is not
            {
                IsExecutable: true,
                Status: ProcessRuntimeStepStatus.Blocked
            })
        {
            return
                $"Source step '{authorization.SourceBlockedStepInstanceId}' is no longer an executable blocked step.";
        }

        var targetStep = state.Steps.FirstOrDefault(candidate =>
            candidate.StepInstanceId == targetStepInstanceId);
        if (targetStep is not { IsExecutable: true })
        {
            return $"Recovery target step '{targetStepInstanceId}' is not executable in the current run.";
        }

        if (string.IsNullOrWhiteSpace(authorization.DiagnosticFingerprint))
        {
            return "The blocked-recovery authorization has no diagnostic fingerprint.";
        }

        var receipt = FindLatestBlockedManagerRequiredReceipt(
            state,
            authorization.SourceBlockedStepInstanceId);
        if (receipt is null)
        {
            return
                $"Source step '{authorization.SourceBlockedStepInstanceId}' has no durable blocked manager-required receipt.";
        }

        var decision = receipt.RecoveryDecision!;
        if (receipt.IdempotencyKey != authorization.SourceResultIdempotencyKey ||
            !string.Equals(
                decision.DiagnosticFingerprint,
                authorization.DiagnosticFingerprint,
                StringComparison.Ordinal) ||
            decision.RouteKind != authorization.RecoveryRouteKind ||
            decision.ResponsibleStepInstanceId != authorization.ResponsibleTargetStepInstanceId)
        {
            return "The blocked-recovery authorization no longer matches the latest durable recovery receipt.";
        }

        if (!IsPhaseTargetAuthorized(state, sourceStep, targetStep, authorization))
        {
            return
                $"Step '{targetStepInstanceId}' is not the target authorized for recovery phase '{authorization.Phase}'.";
        }

        var sourceStepActions = state.BlockedRecoveryActions
            .Where(action =>
                action.SourceBlockedStepInstanceId ==
                authorization.SourceBlockedStepInstanceId)
            .ToArray();
        if (sourceStepActions.Length >= MaximumActionsPerSourceBlockedStep)
        {
            return
                $"Blocked source step '{authorization.SourceBlockedStepInstanceId}' exhausted its automatic action budget of {MaximumActionsPerSourceBlockedStep}.";
        }

        if (sourceStepActions.Any(action =>
                string.Equals(
                    action.DiagnosticFingerprint,
                    authorization.DiagnosticFingerprint,
                    StringComparison.Ordinal) &&
                action.Phase == authorization.Phase))
        {
            return
                $"Recovery phase '{authorization.Phase}' for blocked source step '{authorization.SourceBlockedStepInstanceId}' and diagnostic '{authorization.DiagnosticFingerprint}' was already applied.";
        }

        if (sourceStepActions.Any(action =>
                action.SourceResultIdempotencyKey ==
                authorization.SourceResultIdempotencyKey &&
                action.TargetStepInstanceId == targetStepInstanceId &&
                action.Phase == authorization.Phase))
        {
            return
                $"Recovery phase '{authorization.Phase}' for source receipt '{authorization.SourceResultIdempotencyKey}' and target '{targetStepInstanceId}' was already applied.";
        }

        return null;
    }

    private static bool IsPhaseTargetAuthorized(
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState sourceStep,
        ProcessRuntimeStepState targetStep,
        ProcessRuntimeBlockedRecoveryAuthorization authorization)
    {
        var inputsRestored =
            ProcessRuntimeArtifactContracts.DependenciesSatisfied(state, sourceStep) &&
            ProcessRuntimeArtifactContracts.RequiredArtifactsAvailable(state, sourceStep);
        return authorization.Phase switch
        {
            ProcessRuntimeBlockedRecoveryPhase.CurrentStep =>
                targetStep.StepInstanceId == sourceStep.StepInstanceId &&
                authorization.RecoveryRouteKind is
                    ProcessRecoveryRouteKind.ManagerAction or
                    ProcessRecoveryRouteKind.CurrentStepRetry,
            ProcessRuntimeBlockedRecoveryPhase.UpstreamProducer =>
                !inputsRestored &&
                authorization.RecoveryRouteKind == ProcessRecoveryRouteKind.UpstreamStepRework &&
                targetStep.StepInstanceId != sourceStep.StepInstanceId &&
                targetStep.StepInstanceId == authorization.ResponsibleTargetStepInstanceId,
            ProcessRuntimeBlockedRecoveryPhase.RestoredConsumer =>
                inputsRestored &&
                authorization.RecoveryRouteKind == ProcessRecoveryRouteKind.UpstreamStepRework &&
                targetStep.StepInstanceId == sourceStep.StepInstanceId,
            _ => false
        };
    }

    private readonly record struct ReceiptCandidate(
        StrategyResultReceipt Receipt,
        int Index);
}
