using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Runtime;

public sealed record ProcessDecisionReviewResult(ProcessDecisionChildAddress Child, StrategyId StrategyId,
    StrategyResultIdempotencyKey WaitingResultKey, StrategyResultIdempotencyKey CompletionKey,
    StrategyOutcome Outcome, ProcessDecisionChildClosure Closure, DecisionBudgetSettlement Work);

public static class ProcessDecisionReviewCompletion {
    public static ProcessRuntimeMutation Prepare(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context,
        ProcessDecisionReviewResult result) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(result.Child);
        ArgumentNullException.ThrowIfNull(result.Closure);
        ArgumentNullException.ThrowIfNull(result.Work);
        context.Validate();
        result.Child.Validate();
        result.Closure.Validate();
        result.Work.Validate();
        if (result.Closure.OwnerRevision != result.Work.OwnerWorkRevision) {
            throw new InvalidDataException("The reviewed child closure and measured work must have the same original owner revision.");
        }
        if (state.RootRunId != result.Child.RootRunId || state.RunId != result.Child.RunId ||
                !ProcessStrategyReceiptValuePolicy.IsStableIdentifier(result.StrategyId.Value) ||
                result.WaitingResultKey.Value == Guid.Empty || result.CompletionKey.Value == Guid.Empty ||
                result.WaitingResultKey == result.CompletionKey ||
                result.Outcome is not (StrategyOutcome.Succeeded or StrategyOutcome.Failed or StrategyOutcome.Canceled)) {
            return Reject(state, "Runtime.ReviewIdentityInvalid", "Review completion requires its exact original child, strategy and distinct result identities.");
        }
        var waiting = state.AppliedResults.SingleOrDefault(item => item.StepInstanceId == result.Child.StepInstanceId &&
            item.StrategyId == result.StrategyId && item.IdempotencyKey == result.WaitingResultKey);
        var execution = new ProcessExecutionRunId(result.Child.ChildId.Value);
        if (waiting is not { Outcome: StrategyOutcome.NeedsManager, AppliedSequence: > 0 } ||
                !IsWaiting(waiting.AppliedStepStatus) || waiting.ExecutionRunId != execution ||
                !ProcessStrategyReceiptValuePolicy.IsSha256Digest(waiting.ResultHash)) {
            return Reject(state, "Runtime.ReviewOriginalMissing", "Review completion requires the retained NeedsManager receipt for its original child.");
        }
        var status = result.Outcome switch {
            StrategyOutcome.Succeeded => ProcessRuntimeStepStatus.Completed,
            StrategyOutcome.Failed => ProcessRuntimeStepStatus.Failed,
            StrategyOutcome.Canceled => ProcessRuntimeStepStatus.Cancelled,
            _ => throw new InvalidOperationException("The reviewed outcome was not validated.")
        };
        var digest = Digest(result, waiting.ResultHash);
        var existing = state.AppliedResults.SingleOrDefault(item => item.StepInstanceId == result.Child.StepInstanceId &&
            item.StrategyId == result.StrategyId && item.IdempotencyKey == result.CompletionKey);
        if (existing is not null) {
            return existing.ResultHash == digest && existing.Outcome == result.Outcome &&
                existing.AppliedStepStatus == status && existing.ExecutionRunId == execution
                    ? ProcessRuntimeMutation.Duplicate(state)
                    : Reject(state, "Runtime.ReviewResultChanged", "The completion key already belongs to different original review evidence.");
        }
        if (!Enum.IsDefined(state.Status) || state.Status == ProcessRuntimeStatus.CancelRequested ||
                ProcessRuntimeTerminalStates.IsRunTerminal(state.Status)) {
            return Reject(state, "Runtime.ReviewRunStopped", "A terminal or cancelling run cannot acquire a new review result.");
        }
        var step = state.Steps.SingleOrDefault(item => item.StepInstanceId == result.Child.StepInstanceId);
        if (step is null || !step.IsExecutable || !IsWaiting(step.Status) || step.ActiveClaimToken is not null ||
                step.CompletedResultKey is not null) {
            return Reject(state, "Runtime.ReviewStepChanged", "Review completion requires the original waiting executable step without an active claim.");
        }
        if (state.Claims.Any(item => item.StepInstanceId == step.StepInstanceId &&
                item.Status is DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed) ||
                state.Claims.Count(item => item.StepInstanceId == step.StepInstanceId &&
                    item.Status == DispatchClaimStatus.Completed && item.ResultIdempotencyKey == result.WaitingResultKey &&
                    item.AttemptNumber == step.AttemptNumber) != 1) {
            return Reject(state, "Runtime.ReviewClaimChanged", "The original waiting claim must remain completed, with no other open claim.");
        }
        if (state.AppliedResults.Any(item => item.StepInstanceId == step.StepInstanceId && item != waiting &&
                item.AppliedSequence >= waiting.AppliedSequence)) {
            return Reject(state, "Runtime.ReviewResultSuperseded", "A later result has superseded the displayed waiting receipt.");
        }
        if (!SupportsRuntimeContract(step)) {
            return Reject(state, "Runtime.ReviewContractUnsupported", "This review contract cannot discharge artifact, tool, host or subprocess obligations.");
        }
        var receipt = new StrategyResultReceipt(step.StepInstanceId, result.StrategyId, result.CompletionKey,
            result.Outcome, status, digest) {
            AppliedSequence = StrategyResultReceipt.NextSequence(state.AppliedResults),
            ExecutionRunId = execution,
            UserSafeSummary = result.Outcome switch {
                StrategyOutcome.Succeeded => "The original reviewed child completed successfully.",
                StrategyOutcome.Failed => "The original reviewed child failed; its recorded effects and work remain accounted for.",
                StrategyOutcome.Canceled => "The original reviewed child was cancelled; its recorded effects and work remain accounted for.",
                _ => throw new InvalidOperationException("The reviewed outcome was not validated.")
            }
        };
        var next = state with {
            Steps = state.Steps.Select(item => item.StepInstanceId == step.StepInstanceId ? item with {
                Status = status, CompletedResultKey = status == ProcessRuntimeStepStatus.Completed ? result.CompletionKey : null
            } : item).ToArray(),
            AppliedResults = [.. state.AppliedResults, receipt],
            UpdatedAtUtc = context.OccurredAtUtc
        };
        next = ProcessRuntimeTerminalStates.CompleteRunIfTerminal(next, context.OccurredAtUtc);
        var eventType = status switch {
            ProcessRuntimeStepStatus.Completed => ProcessRuntimeEventTypes.StepCompleted,
            ProcessRuntimeStepStatus.Failed => ProcessRuntimeEventTypes.StepFailed,
            ProcessRuntimeStepStatus.Cancelled => ProcessRuntimeEventTypes.StepCancelled,
            _ => throw new InvalidOperationException("The reviewed step status was not validated.")
        };
        List<ProcessRuntimeEventEnvelope> events = [ProcessRuntimeEventFactory.Create(next, context, eventType, digest)];
        if (next.Status == ProcessRuntimeStatus.Completed) {
            events.Add(ProcessRuntimeEventFactory.Create(next, context, ProcessRuntimeEventTypes.ProcessRunCompleted, next.PlanHash));
        } else if (next.Status == ProcessRuntimeStatus.Failed) {
            events.Add(ProcessRuntimeEventFactory.Create(next, context, ProcessRuntimeEventTypes.ProcessRunFailed, digest));
        } else if (next.Status == ProcessRuntimeStatus.Cancelled) {
            events.Add(ProcessRuntimeEventFactory.Create(next, context, ProcessRuntimeEventTypes.ProcessRunCancelled, digest));
        }
        return ProcessRuntimeMutation.Applied(next, events, []);
    }

    public static bool SupportsRuntimeContract(ProcessRuntimeStepState step) {
        ArgumentNullException.ThrowIfNull(step);
        return step.IsExecutable && step.RequiredArtifactSlots.Count == 0 && step.ProducedArtifactSlots.Count == 0 &&
            step.RequiredRuntimeToolNames.Count == 0 && step.RequiredHostCapabilities.Count == 0 &&
            step.ArtifactDescriptors.Count == 0 && step.SubprocessArtifactMappings.Count == 0;
    }

    private static bool IsWaiting(ProcessRuntimeStepStatus status)
        => status is ProcessRuntimeStepStatus.Waiting or ProcessRuntimeStepStatus.WaitingApproval or ProcessRuntimeStepStatus.Blocked;

    private static string Digest(ProcessDecisionReviewResult result, string waitingHash) {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new {
            Domain = "process-reviewed-child-result/v1", result.Child, Strategy = result.StrategyId.Value,
            WaitingKey = result.WaitingResultKey.Value, CompletionKey = result.CompletionKey.Value,
            WaitingHash = waitingHash, result.Outcome, result.Closure, result.Work
        });
        return "sha256:" + Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }

    private static ProcessRuntimeMutation Reject(ProcessRuntimeStateSnapshot state, string code, string message)
        => ProcessRuntimeMutation.Rejected(state, code, message);
}
