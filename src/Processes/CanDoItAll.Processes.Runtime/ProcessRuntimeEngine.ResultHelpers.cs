using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public sealed partial class ProcessRuntimeEngine
{
    private const string MissingBlockedDiagnosticCode = "process.runtime.blocked_without_diagnostics";
    private const string MissingBlockedDiagnosticEvidenceHash = "sha256:missing-strategy-diagnostics";
    private const string MissingBlockedDiagnosticSummary =
        "Step blocked without strategy diagnostics. Inspect the result receipt, assignment, and execution observation for the missing cause.";

    private static ProcessRuntimeStepStatus ToStepStatus(StrategyResultEnvelope result)
    {
        if (IsAutomaticallyRetryableManagerResult(result))
        {
            return ProcessRuntimeStepStatus.Ready;
        }

        return result.Outcome switch
        {
            StrategyOutcome.Succeeded => ProcessRuntimeStepStatus.Completed,
            StrategyOutcome.Failed => ProcessRuntimeStepStatus.Failed,
            StrategyOutcome.Waiting or StrategyOutcome.NeedsManager => ProcessRuntimeStepStatus.Blocked,
            StrategyOutcome.Canceled => ProcessRuntimeStepStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unknown strategy outcome.")
        };
    }

    private static bool IsAutomaticallyRetryableManagerResult(StrategyResultEnvelope result)
    {
        return result.Outcome == StrategyOutcome.NeedsManager &&
               result.Diagnostics.Count > 0 &&
               result.Diagnostics.All(IsAutomaticallyRetryableDiagnostic) &&
               result.ManagerSignals.Any(signal => signal.Code.Value.StartsWith("process.adapter.", StringComparison.Ordinal));
    }

    private static bool IsAutomaticallyRetryableDiagnostic(StrategyDiagnosticRef diagnostic)
    {
        return diagnostic.RetrySafety == ProcessDiagnosticRetrySafety.SafeToRetry &&
               diagnostic.Idempotency == ProcessDiagnosticIdempotencyClassification.Idempotent &&
               diagnostic.Code.Value.StartsWith("process.adapter.", StringComparison.Ordinal);
    }

    private static IReadOnlyList<StrategyResultDiagnosticReceipt> BuildDiagnosticReceipts(
        StrategyResultEnvelope result,
        ProcessRuntimeStepStatus appliedStepStatus)
    {
        if (result.Diagnostics.Count > 0)
        {
            var receipts = new List<StrategyResultDiagnosticReceipt>(result.Diagnostics.Count);
            foreach (var diagnostic in result.Diagnostics)
            {
                receipts.Add(new StrategyResultDiagnosticReceipt(
                    diagnostic.Code.Value.Trim(),
                    diagnostic.Sensitivity,
                    diagnostic.EvidenceHash.Trim(),
                    diagnostic.SafeSummary.Trim(),
                    string.IsNullOrWhiteSpace(diagnostic.RestrictedEvidenceReference)
                        ? null
                        : diagnostic.RestrictedEvidenceReference.Trim(),
                    diagnostic.RetrySafety,
                    diagnostic.Idempotency));
            }

            return receipts;
        }

        return appliedStepStatus == ProcessRuntimeStepStatus.Blocked
            ?
            [
                new StrategyResultDiagnosticReceipt(
                    MissingBlockedDiagnosticCode,
                    StrategyDiagnosticSensitivity.Normal,
                    MissingBlockedDiagnosticEvidenceHash,
                    MissingBlockedDiagnosticSummary,
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.Unknown,
                    ProcessDiagnosticIdempotencyClassification.Unknown)
            ]
            : [];
    }

    private static IReadOnlyList<StrategyResultArtifactReceipt> BuildProducedArtifactReceipts(
        StrategyResultEnvelope result)
    {
        if (result.ProducedArtifacts.Count == 0)
        {
            return [];
        }

        var receipts = new List<StrategyResultArtifactReceipt>(result.ProducedArtifacts.Count);
        foreach (var artifact in result.ProducedArtifacts)
        {
            receipts.Add(new StrategyResultArtifactReceipt(
                artifact.SlotId,
                artifact.ArtifactId,
                artifact.ContentHash));
        }

        return receipts;
    }

    private static ProcessRecoveryDecisionReceipt? BuildRecoveryDecision(
        StrategyResultEnvelope result,
        ProcessRuntimeStepStatus appliedStepStatus)
    {
        var primaryDiagnosticCode = result.Diagnostics.FirstOrDefault()?.Code.Value ?? string.Empty;
        if (IsAutomaticallyRetryableManagerResult(result))
        {
            return new ProcessRecoveryDecisionReceipt(
                ClassifyFailureCategory(primaryDiagnosticCode),
                ProcessRecoveryDecisionKind.SafeRetry,
                primaryDiagnosticCode,
                "process.adapter.safe-idempotent-retry",
                "All strategy diagnostics were marked safe to retry, idempotent, and emitted by the process adapter.");
        }

        if (appliedStepStatus is ProcessRuntimeStepStatus.Blocked)
        {
            var sourceCode = string.IsNullOrWhiteSpace(primaryDiagnosticCode)
                ? MissingBlockedDiagnosticCode
                : primaryDiagnosticCode;
            return new ProcessRecoveryDecisionReceipt(
                ClassifyFailureCategory(sourceCode),
                ProcessRecoveryDecisionKind.ManagerRequired,
                sourceCode,
                "process.manager-review-required",
                "The strategy result blocked the step and requires manager or operator decision before rework.");
        }

        if (appliedStepStatus is ProcessRuntimeStepStatus.Failed)
        {
            var sourceCode = string.IsNullOrWhiteSpace(primaryDiagnosticCode)
                ? "process.runtime.failed_without_diagnostics"
                : primaryDiagnosticCode;
            return new ProcessRecoveryDecisionReceipt(
                ClassifyFailureCategory(sourceCode),
                ProcessRecoveryDecisionKind.TerminalBlocked,
                sourceCode,
                "process.terminal-failure",
                "The strategy result failed the step and no automatic recovery decision was applied.");
        }

        return null;
    }

    private static ProcessFailureCategory ClassifyFailureCategory(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return ProcessFailureCategory.Unknown;
        }

        if (string.Equals(code, MissingBlockedDiagnosticCode, StringComparison.OrdinalIgnoreCase) ||
            code.Contains("without_diagnostics", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessFailureCategory.MissingDiagnostics;
        }

        if (code.StartsWith("process.adapter.", StringComparison.OrdinalIgnoreCase) &&
            code.Contains("retry", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessFailureCategory.AdapterRetryable;
        }

        if (code.Contains("artifact", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("receipt", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessFailureCategory.MissingArtifact;
        }

        if (code.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("suppressed", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessFailureCategory.DeniedCapability;
        }

        if (code.Contains("capability", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("tool", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("mcp", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("skill", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessFailureCategory.MissingCapability;
        }

        if (code.Contains("policy", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessFailureCategory.PolicyViolation;
        }

        if (code.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessFailureCategory.Timeout;
        }

        if (code.Contains("provider", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessFailureCategory.ProviderFailure;
        }

        if (code.Contains("child", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessFailureCategory.ChildRunBlocked;
        }

        if (code.Contains("instruction", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("non_compliance", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessFailureCategory.InstructionNonCompliance;
        }

        return ProcessFailureCategory.Unknown;
    }

    private static IReadOnlyList<ProcessRuntimeEventEnvelope> BuildResultEvents(
        ProcessRuntimeStateSnapshot state,
        RuntimeCommandContext context,
        SubmitStrategyResultCommand command,
        ProcessRuntimeStepStatus stepStatus)
    {
        var events = new List<ProcessRuntimeEventEnvelope>
        {
            CreateEvent(state, context, ProcessRuntimeEventTypes.DispatchClaimCompleted, command.ClaimToken.ToString()),
            CreateEvent(state, context, ToStepEventType(stepStatus), command.Result.ResultHash)
        };

        if (state.Status == ProcessRuntimeStatus.Completed)
        {
            events.Add(CreateEvent(state, context, ProcessRuntimeEventTypes.ProcessRunCompleted, state.PlanHash));
        }
        else if (state.Status == ProcessRuntimeStatus.Failed)
        {
            events.Add(CreateEvent(state, context, ProcessRuntimeEventTypes.ProcessRunFailed, command.Result.ResultHash));
        }
        else if (state.Status == ProcessRuntimeStatus.Cancelled)
        {
            events.Add(CreateEvent(state, context, ProcessRuntimeEventTypes.ProcessRunCancelled, command.Result.ResultHash));
        }

        return events;
    }

    private static ProcessEventType ToStepEventType(ProcessRuntimeStepStatus status)
    {
        return status switch
        {
            ProcessRuntimeStepStatus.Completed => ProcessRuntimeEventTypes.StepCompleted,
            ProcessRuntimeStepStatus.Failed => ProcessRuntimeEventTypes.StepFailed,
            ProcessRuntimeStepStatus.Ready => ProcessRuntimeEventTypes.StepReady,
            ProcessRuntimeStepStatus.Blocked => ProcessRuntimeEventTypes.StepBlocked,
            ProcessRuntimeStepStatus.Cancelled => ProcessRuntimeEventTypes.StepCancelled,
            _ => ProcessRuntimeEventTypes.StepBlocked
        };
    }

    private static IReadOnlyList<ProcessArtifactLedgerEvent> BuildArtifactLedgerEvents(
        RuntimeEventId eventId,
        SubmitStrategyResultCommand command)
    {
        if (command.Result.ProducedArtifacts.Count == 0)
        {
            return [];
        }

        var ledgerEvents = new List<ProcessArtifactLedgerEvent>(command.Result.ProducedArtifacts.Count);
        foreach (var artifact in command.Result.ProducedArtifacts)
        {
            ledgerEvents.Add(new ProcessArtifactLedgerEvent(
                ArtifactLedgerEventId.New(),
                eventId,
                artifact.SlotId,
                artifact.ArtifactId,
                artifact.ContentHash));
        }

        return ledgerEvents;
    }

    private static ProcessRuntimeStateSnapshot CompleteRunIfTerminal(
        ProcessRuntimeStateSnapshot state,
        DateTimeOffset occurredAtUtc)
    {
        var hasOpenExecutableSteps = false;
        var hasFailedStep = false;
        var hasCancelledStep = false;
        foreach (var step in state.Steps)
        {
            if (!step.IsExecutable)
            {
                continue;
            }

            if (step.Status == ProcessRuntimeStepStatus.Failed)
            {
                hasFailedStep = true;
                break;
            }

            if (step.Status == ProcessRuntimeStepStatus.Cancelled)
            {
                hasCancelledStep = true;
            }

            if (!ProcessRuntimeTerminalStates.IsStepTerminal(step.Status))
            {
                hasOpenExecutableSteps = true;
            }
        }

        if (hasFailedStep)
        {
            return state with
            {
                Status = ProcessRuntimeStatus.Failed,
                UpdatedAtUtc = occurredAtUtc
            };
        }

        if (hasCancelledStep && !hasOpenExecutableSteps)
        {
            return state with
            {
                Status = ProcessRuntimeStatus.Cancelled,
                UpdatedAtUtc = occurredAtUtc
            };
        }

        if (!hasOpenExecutableSteps)
        {
            return state with
            {
                Status = ProcessRuntimeStatus.Completed,
                UpdatedAtUtc = occurredAtUtc
            };
        }

        return state;
    }

    private static IReadOnlySet<ArtifactSlotId> AddProducedSlots(
        IReadOnlySet<ArtifactSlotId> availableSlots,
        StrategyResultEnvelope result)
    {
        if (result.ProducedArtifacts.Count == 0)
        {
            return availableSlots;
        }

        var next = new HashSet<ArtifactSlotId>(availableSlots);
        foreach (var producedArtifact in result.ProducedArtifacts)
        {
            next.Add(producedArtifact.SlotId);
        }

        return next;
    }
}
