using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public sealed partial class ProcessRuntimeEngine
{
    private const string MissingBlockedDiagnosticCode = "process.runtime.blocked_without_diagnostics";
    private static readonly string MissingBlockedDiagnosticEvidenceHash =
        ComputePayloadHash("missing-strategy-diagnostics");
    private const string MissingBlockedDiagnosticSummary =
        "Step blocked without strategy diagnostics. Inspect the result receipt, assignment, and execution observation for the missing cause.";

    private static ProcessRuntimeStepStatus ToStepStatus(StrategyResultEnvelope result)
    {
        return result.Outcome switch
        {
            StrategyOutcome.Succeeded => ProcessRuntimeStepStatus.Completed,
            StrategyOutcome.Failed => ProcessRuntimeStepStatus.Failed,
            StrategyOutcome.Waiting or StrategyOutcome.NeedsManager => ProcessRuntimeStepStatus.Blocked,
            StrategyOutcome.Canceled => ProcessRuntimeStepStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unknown strategy outcome.")
        };
    }

    private static StrategyResultEnvelope EnforceStepFinalizationContract(
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState step,
        StrategyResultEnvelope result)
    {
        if (result.Outcome != StrategyOutcome.Succeeded)
        {
            return result;
        }

        var diagnostics = new List<StrategyDiagnosticRef>();
        var requestedArtifacts = result.RequestedArtifacts.ToList();
        var missingInputSlots = ResolveMissingInputSlots(state, step);
        foreach (var slotId in missingInputSlots)
        {
            var evidenceHash = ComputePayloadHash($"missing-input:{state.RunId}:{step.StepInstanceId}:{slotId}:{result.ResultHash}");
            AddRequestedArtifact(requestedArtifacts, slotId, evidenceHash);
            diagnostics.Add(new StrategyDiagnosticRef(
                new StrategyDiagnosticCode(ProcessRuntimeDiagnosticCodes.MissingRequiredInputArtifact),
                StrategyDiagnosticSensitivity.Normal,
                evidenceHash,
                $"Step '{step.StepInstanceId}' cannot complete because required input artifact slot '{slotId}' is not available as a connected input receipt.",
                RestrictedEvidenceReference: null,
                ProcessDiagnosticRetrySafety.UnsafeToRetry,
                ProcessDiagnosticIdempotencyClassification.Idempotent));
        }

        var missingOutputSlots = ResolveMissingOutputSlots(step, result);
        foreach (var slotId in missingOutputSlots)
        {
            var evidenceHash = ComputePayloadHash($"missing-output:{state.RunId}:{step.StepInstanceId}:{slotId}:{result.ResultHash}");
            AddRequestedArtifact(requestedArtifacts, slotId, evidenceHash);
            diagnostics.Add(new StrategyDiagnosticRef(
                new StrategyDiagnosticCode(ProcessRuntimeDiagnosticCodes.MissingExpectedOutputArtifact),
                StrategyDiagnosticSensitivity.Normal,
                evidenceHash,
                $"Step '{step.StepInstanceId}' reported success without producing required artifact slot '{slotId}'.",
                RestrictedEvidenceReference: null,
                ProcessDiagnosticRetrySafety.UnsafeToRetry,
                ProcessDiagnosticIdempotencyClassification.Idempotent));
        }

        if (diagnostics.Count == 0)
        {
            return result;
        }

        var resultHash = ComputePayloadHash(
            $"finalization:{state.RunId}:{step.StepInstanceId}:{result.ResultHash}:{string.Join(';', diagnostics.Select(item => item.EvidenceHash))}");
        return result with
        {
            Outcome = StrategyOutcome.NeedsManager,
            RequestedArtifacts = requestedArtifacts,
            Diagnostics = [.. result.Diagnostics, .. diagnostics],
            ManagerSignals =
            [
                .. result.ManagerSignals,
                new ManagerSignal(
                    new ManagerSignalCode("process.runtime.step_finalization_incomplete"),
                    resultHash,
                    "The step result did not satisfy its required input/output artifact contract and requires manager review.")
            ],
            ResultHash = resultHash
        };
    }

    private static IReadOnlyList<ArtifactSlotId> ResolveMissingInputSlots(
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState step)
    {
        if (step.RequiredArtifactSlots.Count == 0)
        {
            return [];
        }

        var contract = ProcessRuntimeArtifactContracts.BuildStepContract(state, step);
        return step.RequiredArtifactSlots
            .Where(slotId => !contract.RequiredArtifacts.Any(artifact =>
                artifact.SlotId == slotId &&
                artifact.Availability == ProcessArtifactInputAvailability.Available &&
                artifact.ArtifactId is not null &&
                !string.IsNullOrWhiteSpace(artifact.ContentHash)))
            .OrderBy(slotId => slotId.Value)
            .ToArray();
    }

    private static IReadOnlyList<ArtifactSlotId> ResolveMissingOutputSlots(
        ProcessRuntimeStepState step,
        StrategyResultEnvelope result)
    {
        if (step.ProducedArtifactSlots.Count == 0)
        {
            return [];
        }

        var producedSlots = result.ProducedArtifacts
            .Select(artifact => artifact.SlotId)
            .ToHashSet();
        return step.ProducedArtifactSlots
            .Where(slotId => !producedSlots.Contains(slotId))
            .OrderBy(slotId => slotId.Value)
            .ToArray();
    }

    private static void AddRequestedArtifact(
        IList<RequestedArtifactRef> requestedArtifacts,
        ArtifactSlotId slotId,
        string requestHash)
    {
        if (requestedArtifacts.Any(artifact => artifact.SlotId == slotId))
        {
            return;
        }

        requestedArtifacts.Add(new RequestedArtifactRef(slotId, requestHash));
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
                    diagnostic.Idempotency)
                {
                    RelatedChildRunId = diagnostic.RelatedChildRunId,
                    ExecutionSafetyAttestation = diagnostic.ExecutionSafetyAttestation
                });
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

    private static ProcessExecutionRunId? ResolveExecutionRunId(StrategyResultEnvelope result)
    {
        if (result.ExecutionRunId is not { } executionRunId)
        {
            return null;
        }

        var attestedExecutionRunIds = result.Diagnostics
            .Where(diagnostic => diagnostic.ExecutionSafetyAttestation is not null)
            .Select(diagnostic => diagnostic.ExecutionSafetyAttestation!.ExecutionRunId)
            .Distinct()
            .ToArray();
        return attestedExecutionRunIds.Length == 0 ||
               attestedExecutionRunIds is [var attestedExecutionRunId] &&
               attestedExecutionRunId == executionRunId
            ? executionRunId
            : null;
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
        ProcessRuntimeStepStatus appliedStepStatus,
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState step,
        IReadOnlyList<StrategyResultDiagnosticReceipt> diagnostics)
    {
        var primaryDiagnostic = diagnostics.FirstOrDefault();
        var primaryDiagnosticCode = primaryDiagnostic?.Code ?? string.Empty;

        if (appliedStepStatus is ProcessRuntimeStepStatus.Blocked)
        {
            var sourceCode = string.IsNullOrWhiteSpace(primaryDiagnosticCode)
                ? MissingBlockedDiagnosticCode
                : primaryDiagnosticCode;
            var failureCategory = primaryDiagnostic?.RelatedChildRunId is not null
                ? ProcessFailureCategory.ChildRunBlocked
                : ClassifyFailureCategory(sourceCode);
            var routeKind = ResolveRecoveryRouteKind(state, step, result, failureCategory, out var responsibleStepId);
            return ProcessRecoveryClassifier.Default.ClassifyBlocked(new ProcessRecoveryClassificationInput(
                step.StepInstanceId,
                failureCategory,
                sourceCode,
                routeKind,
                responsibleStepId,
                diagnostics,
                state.AppliedResults.Where(receipt => receipt.StepInstanceId == step.StepInstanceId).ToArray()));
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
                "The strategy result failed the step and no automatic recovery decision was applied.")
            {
                RouteKind = ProcessRecoveryRouteKind.TerminalBlock,
                ResponsibleStepInstanceId = step.StepInstanceId
            };
        }

        return null;
    }

    private static ProcessRuntimeStepStatus ResolveStepStatusForRecoveryDecision(
        ProcessRuntimeStepStatus resultStepStatus,
        ProcessRecoveryDecisionReceipt? recoveryDecision)
    {
        if (resultStepStatus == ProcessRuntimeStepStatus.Blocked &&
            recoveryDecision is
            {
                DecisionKind: ProcessRecoveryDecisionKind.SafeRetry,
                RouteKind: ProcessRecoveryRouteKind.CurrentStepRetry
            })
        {
            return ProcessRuntimeStepStatus.Ready;
        }

        return resultStepStatus;
    }

    private static ProcessRecoveryRouteKind ResolveRecoveryRouteKind(
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState step,
        StrategyResultEnvelope result,
        ProcessFailureCategory failureCategory,
        out ProcessStepInstanceId? responsibleStepId)
    {
        responsibleStepId = null;
        if (failureCategory == ProcessFailureCategory.MissingArtifact)
        {
            responsibleStepId = ProcessRuntimeArtifactContracts.FindResponsibleStepForMissingArtifact(
                state,
                step.StepInstanceId,
                result);
            if (responsibleStepId is not null && responsibleStepId != step.StepInstanceId)
            {
                return ProcessRecoveryRouteKind.UpstreamStepRework;
            }

            responsibleStepId = step.StepInstanceId;
            return ProcessRecoveryRouteKind.ManagerAction;
        }

        responsibleStepId = step.StepInstanceId;
        return failureCategory switch
        {
            ProcessFailureCategory.ChildRunBlocked => ProcessRecoveryRouteKind.ChildRunPropagation,
            ProcessFailureCategory.PolicyViolation => ProcessRecoveryRouteKind.TemplateRepair,
            _ => ProcessRecoveryRouteKind.ManagerAction
        };
    }

    private static string ResolveRecoveryPolicy(ProcessRecoveryRouteKind routeKind)
    {
        return routeKind switch
        {
            ProcessRecoveryRouteKind.UpstreamStepRework => "process.upstream-artifact-rework-required",
            ProcessRecoveryRouteKind.ChildRunPropagation => "process.child-run-manager-review-required",
            ProcessRecoveryRouteKind.TemplateRepair => "process.template-or-policy-repair-required",
            ProcessRecoveryRouteKind.TerminalBlock => "process.terminal-failure",
            _ => "process.manager-review-required"
        };
    }

    private static string ResolveRecoveryReason(ProcessRecoveryRouteKind routeKind)
    {
        return routeKind switch
        {
            ProcessRecoveryRouteKind.UpstreamStepRework => "A required input artifact is still missing; the manager must rework the responsible upstream producer before this step is retried.",
            ProcessRecoveryRouteKind.ChildRunPropagation => "The step is blocked by child-run state and requires manager review of the child process boundary.",
            ProcessRecoveryRouteKind.TemplateRepair => "The blocker points to a process policy or template contract and requires manager repair before another execution.",
            ProcessRecoveryRouteKind.TerminalBlock => "The strategy result failed the step and no automatic recovery decision was applied.",
            _ => "The strategy result blocked the step and requires manager or operator decision before rework."
        };
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

        if (string.Equals(
                code,
                ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionBeforeSideEffects,
                StringComparison.Ordinal))
        {
            return ProcessFailureCategory.AdapterRetryable;
        }

        if (code.StartsWith("process.adapter.", StringComparison.OrdinalIgnoreCase) &&
            code.Contains("retry", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessFailureCategory.AdapterRetryable;
        }

        if (ProcessCompletionGateDiagnosticCatalog.IsCompletionGateDiagnosticCode(code))
        {
            return ProcessFailureCategory.ProductCompletionGate;
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
        StrategyResultEnvelope result,
        ProcessRuntimeStepStatus stepStatus)
    {
        var events = new List<ProcessRuntimeEventEnvelope>
        {
            CreateEvent(state, context, ProcessRuntimeEventTypes.DispatchClaimCompleted, command.ClaimToken.ToString()),
            CreateEvent(state, context, ToStepEventType(stepStatus), result.ResultHash)
        };

        if (state.Status == ProcessRuntimeStatus.Completed)
        {
            events.Add(CreateEvent(state, context, ProcessRuntimeEventTypes.ProcessRunCompleted, state.PlanHash));
        }
        else if (state.Status == ProcessRuntimeStatus.Failed)
        {
            events.Add(CreateEvent(state, context, ProcessRuntimeEventTypes.ProcessRunFailed, result.ResultHash));
        }
        else if (state.Status == ProcessRuntimeStatus.Cancelled)
        {
            events.Add(CreateEvent(state, context, ProcessRuntimeEventTypes.ProcessRunCancelled, result.ResultHash));
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
        StrategyResultEnvelope result)
    {
        if (result.ProducedArtifacts.Count == 0)
        {
            return [];
        }

        var ledgerEvents = new List<ProcessArtifactLedgerEvent>(result.ProducedArtifacts.Count);
        foreach (var artifact in result.ProducedArtifacts)
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
