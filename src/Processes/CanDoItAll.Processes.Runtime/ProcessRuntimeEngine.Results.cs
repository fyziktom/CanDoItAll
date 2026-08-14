using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public sealed partial class ProcessRuntimeEngine
{
    private static ProcessRuntimeMutation SubmitStrategyResult(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        SubmitStrategyResultCommand command)
    {
        ValidateArguments(state, context);

        var existing = FindReceipt(state, command.StepInstanceId, command.Result.StrategyId, command.IdempotencyKey);
        if (existing is not null)
        {
            return Duplicate(state);
        }

        var step = FindStep(state, command.StepInstanceId);
        var claim = FindClaim(state, command.ClaimToken);
        if (step is null || claim is null || step.ActiveClaimToken != command.ClaimToken)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.LostLease", "Strategy result was rejected because the active claim token no longer matches.");
        }

        if (claim.OwnerId != command.OwnerId)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.ClaimOwnerMismatch", "Strategy result owner does not match the active claim.");
        }

        if (claim.ExpiresAtUtc <= context.OccurredAtUtc)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.ClaimExpired", "Strategy result was rejected because the dispatch claim expired.");
        }

        if (step.Status != ProcessRuntimeStepStatus.Running)
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.StepNotRunning", "Strategy result requires a running step.");
        }

        if (!IsValidBoundedStrategyResult(command.Result))
        {
            return ProcessRuntimeMutation.Rejected(
                state,
                "Runtime.StrategyResultReceiptInvalid",
                "Strategy result receipt fields are invalid or exceed the bounded runtime contract.");
        }

        if (!TryNormalizeHostCapabilityEvidence(
                command.Result.HostCapabilityEvidence,
                out var normalizedHostCapabilityEvidence))
        {
            return ProcessRuntimeMutation.Rejected(
                state,
                "Runtime.HostCapabilityEvidenceInvalid",
                "Strategy result host capability evidence is invalid or exceeds the bounded runtime contract.");
        }

        var normalizedResult = command.Result with
        {
            HostCapabilityEvidence = normalizedHostCapabilityEvidence
        };
        var appliedResult = EnforceStepFinalizationContract(state, step, normalizedResult);
        if (!IsValidBoundedStrategyResult(appliedResult))
        {
            return ProcessRuntimeMutation.Rejected(
                state,
                "Runtime.StrategyResultReceiptInvalid",
                "Strategy result receipt fields are invalid or exceed the bounded runtime contract.");
        }

        var resultStepStatus = ToStepStatus(appliedResult);
        var diagnosticReceipts = BuildDiagnosticReceipts(appliedResult, resultStepStatus);
        var recoveryDecision = BuildRecoveryDecision(appliedResult, resultStepStatus, state, step, diagnosticReceipts);
        var nextStepStatus = ResolveStepStatusForRecoveryDecision(resultStepStatus, recoveryDecision);
        var receipt = new StrategyResultReceipt(
            step.StepInstanceId,
            appliedResult.StrategyId,
            command.IdempotencyKey,
            appliedResult.Outcome,
            nextStepStatus,
            appliedResult.ResultHash,
            diagnosticReceipts,
            BuildProducedArtifactReceipts(appliedResult),
            recoveryDecision)
        {
            UserSafeSummary = string.IsNullOrWhiteSpace(appliedResult.UserSafeSummary)
                ? string.Empty
                : appliedResult.UserSafeSummary.Trim(),
            AppliedSequence = NextAppliedResultSequence(state.AppliedResults),
            ExecutionRunId = ResolveExecutionRunId(appliedResult),
            HostCapabilityEvidence = appliedResult.HostCapabilityEvidence
        };
        var nextClaims = ReplaceClaim(
            state,
            claim with
            {
                Status = appliedResult.Outcome == StrategyOutcome.Canceled
                    ? DispatchClaimStatus.Cancelled
                    : DispatchClaimStatus.Completed,
                ResultIdempotencyKey = command.IdempotencyKey
            });
        var nextSteps = ReplaceStep(
            state,
            step with
            {
                Status = nextStepStatus,
                ActiveClaimToken = null,
                CompletedResultKey = nextStepStatus == ProcessRuntimeStepStatus.Completed
                    ? command.IdempotencyKey
                    : null
            });
        var next = state with
        {
            Steps = nextSteps,
            Claims = nextClaims,
            AppliedResults = Append(state.AppliedResults, receipt),
            AvailableArtifactSlots = AddProducedSlots(state.AvailableArtifactSlots, appliedResult),
            ConnectedInputArtifacts = ProcessRuntimeArtifactContracts.ApplyProducedArtifacts(state, step.StepInstanceId, appliedResult),
            UpdatedAtUtc = context.OccurredAtUtc
        };
        next = CompleteRunIfTerminal(next, context.OccurredAtUtc);

        var events = BuildResultEvents(next, context, command, appliedResult, nextStepStatus);
        var resultEventId = events[1].EventId;

        return Applied(
            next,
            events,
            BuildArtifactLedgerEvents(resultEventId, appliedResult));
    }

    private static long NextAppliedResultSequence(IReadOnlyList<StrategyResultReceipt> receipts)
    {
        return Math.Max(
            receipts.Count + 1L,
            receipts.Count == 0
                ? 1L
                : receipts.Max(receipt => receipt.AppliedSequence) + 1L);
    }

    private static bool TryNormalizeHostCapabilityEvidence(
        ProcessHostCapabilityEvaluationEvidence? evidence,
        out ProcessHostCapabilityEvaluationEvidence? normalized)
    {
        normalized = null;
        if (evidence is null)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(evidence.ProfileId.Value) ||
            evidence.Capabilities is null ||
            evidence.Capabilities.Count > 32 ||
            evidence.Capabilities.Any(capability =>
                capability is null || !capability.IsStructurallyValid()) ||
            evidence.Capabilities.Select(capability => capability.Id).Distinct().Count() !=
            evidence.Capabilities.Count)
        {
            return false;
        }

        normalized = new ProcessHostCapabilityEvaluationEvidence(
            evidence.ProfileId,
            evidence.Capabilities
                .OrderBy(capability => capability.Id.Value, StringComparer.Ordinal)
                .ToArray());
        return true;
    }

    private static bool IsValidBoundedStrategyResult(StrategyResultEnvelope result)
    {
        return result is not null &&
               ProcessStrategyReceiptValuePolicy.IsStableIdentifier(result.StrategyId.Value) &&
               ProcessStrategyReceiptValuePolicy.IsStableVersion(result.StrategyVersion) &&
               result.IdempotencyKey != Guid.Empty &&
               Enum.IsDefined(result.Outcome) &&
               ProcessStrategyReceiptValuePolicy.IsSha256Digest(result.ResultHash) &&
               ProcessPublicReceiptTextPolicy.IsSafe(
                   result.UserSafeSummary,
                   ProcessStrategyResultLimits.MaximumUserSafeSummaryLength) &&
               result.ProducedArtifacts is { Count: <= ProcessStrategyResultLimits.MaximumArtifacts } &&
               result.ProducedArtifacts.All(IsValidProducedArtifact) &&
               result.RequestedArtifacts is { Count: <= ProcessStrategyResultLimits.MaximumArtifacts } &&
               result.RequestedArtifacts.All(IsValidRequestedArtifact) &&
               result.Diagnostics is { Count: <= ProcessStrategyResultLimits.MaximumDiagnostics } &&
               result.Diagnostics.All(IsValidDiagnostic) &&
               result.ManagerSignals is { Count: <= ProcessStrategyResultLimits.MaximumManagerSignals } &&
               result.ManagerSignals.All(IsValidManagerSignal);
    }

    private static bool IsValidProducedArtifact(ProducedArtifactRef artifact)
        => artifact is not null &&
           artifact.ArtifactId.Value != Guid.Empty &&
           artifact.SlotId.Value != Guid.Empty &&
           ProcessStrategyReceiptValuePolicy.IsSha256Digest(artifact.ContentHash);

    private static bool IsValidRequestedArtifact(RequestedArtifactRef artifact)
        => artifact is not null &&
           artifact.SlotId.Value != Guid.Empty &&
           ProcessStrategyReceiptValuePolicy.IsSha256Digest(artifact.RequestHash);

    private static bool IsValidDiagnostic(StrategyDiagnosticRef diagnostic)
        => diagnostic is not null &&
           ProcessStrategyReceiptValuePolicy.IsStableIdentifier(diagnostic.Code.Value) &&
           Enum.IsDefined(diagnostic.Sensitivity) &&
           ProcessStrategyReceiptValuePolicy.IsSha256Digest(diagnostic.EvidenceHash) &&
            ProcessPublicReceiptTextPolicy.IsSafe(
                diagnostic.SafeSummary,
                ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength) &&
           ProcessStrategyReceiptValuePolicy.IsRestrictedEvidenceReference(
               diagnostic.RestrictedEvidenceReference) &&
           Enum.IsDefined(diagnostic.RetrySafety) &&
           Enum.IsDefined(diagnostic.Idempotency) &&
           (diagnostic.ExecutionSafetyAttestation is null ||
            diagnostic.ExecutionSafetyAttestation.IsStructurallyValid());

    private static bool IsValidManagerSignal(ManagerSignal signal)
        => signal is not null &&
           ProcessStrategyReceiptValuePolicy.IsStableIdentifier(signal.Code.Value) &&
           ProcessStrategyReceiptValuePolicy.IsSha256Digest(signal.SignalHash) &&
            ProcessPublicReceiptTextPolicy.IsSafe(
                signal.SafeSummary,
                ProcessStrategyResultLimits.MaximumManagerSignalSummaryLength);

    private static bool IsBoundedRequiredText(string? value, int maximumLength)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;

    private static ProcessRuntimeMutation RequestCancellation(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context)
    {
        ValidateArguments(state, context);

        if (ProcessRuntimeTerminalStates.IsRunTerminal(state.Status))
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.TerminalRunImmutable", "Terminal runs cannot be cancelled again.");
        }

        var next = CancelOpenWork(state, ProcessRuntimeStatus.Cancelled, context.OccurredAtUtc);
        return Applied(next, context, ProcessRuntimeEventTypes.ProcessRunCancelled, state.RunId.ToString());
    }

    private static ProcessRuntimeMutation BeginRootCancellation(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context)
    {
        ValidateArguments(state, context);

        if (state.RunId != state.RootRunId)
        {
            return ProcessRuntimeMutation.Rejected(
                state,
                "Runtime.RootCancellationRequiresRootRun",
                "The root cancellation barrier can only be started on the root process run.");
        }

        if (state.Status == ProcessRuntimeStatus.CancelRequested)
        {
            return Duplicate(state);
        }

        if (ProcessRuntimeTerminalStates.IsRunTerminal(state.Status))
        {
            return ProcessRuntimeMutation.Rejected(state, "Runtime.TerminalRunImmutable", "Terminal runs cannot be cancelled again.");
        }

        var next = CancelOpenWork(state, ProcessRuntimeStatus.CancelRequested, context.OccurredAtUtc);
        return Applied(next, context, ProcessRuntimeEventTypes.ProcessRunCancelRequested, state.RunId.ToString());
    }

    private static ProcessRuntimeMutation FinalizeRootCancellation(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context)
    {
        ValidateArguments(state, context);

        if (state.RunId != state.RootRunId)
        {
            return ProcessRuntimeMutation.Rejected(
                state,
                "Runtime.RootCancellationRequiresRootRun",
                "The root cancellation barrier can only be finalized on the root process run.");
        }

        if (state.Status != ProcessRuntimeStatus.CancelRequested)
        {
            return ProcessRuntimeMutation.Rejected(
                state,
                "Runtime.RootCancellationBarrierMissing",
                "Root cancellation can only be finalized after the cancellation barrier was committed.");
        }

        var next = state with
        {
            Status = ProcessRuntimeStatus.Cancelled,
            UpdatedAtUtc = context.OccurredAtUtc
        };
        return Applied(next, context, ProcessRuntimeEventTypes.ProcessRunCancelled, state.RunId.ToString());
    }

    private static ProcessRuntimeStateSnapshot CancelOpenWork(
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStatus status,
        DateTimeOffset occurredAtUtc)
    {
        var steps = new List<ProcessRuntimeStepState>(state.Steps.Count);
        foreach (var step in state.Steps)
        {
            steps.Add(ProcessRuntimeTerminalStates.IsStepTerminal(step.Status)
                ? step
                : step with
                {
                    Status = ProcessRuntimeStepStatus.Cancelled,
                    ActiveClaimToken = null
                });
        }

        var claims = new List<DispatchClaimState>(state.Claims.Count);
        foreach (var claim in state.Claims)
        {
            claims.Add(claim.Status is DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed
                ? claim with { Status = DispatchClaimStatus.Cancelled }
                : claim);
        }

        return state with
        {
            Status = status,
            Steps = steps,
            Claims = claims,
            UpdatedAtUtc = occurredAtUtc
        };
    }
}
