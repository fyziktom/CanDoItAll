using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessBlockedRunRecoveryPolicyCatalog : IProcessBlockedRunRecoveryPolicyCatalog
{
    private static readonly HashSet<ProcessDefinitionStepOperationKind> ArtifactOnlyRecoveryOperations =
    [
        ProcessDefinitionStepOperationKind.ReadProcessContext,
        ProcessDefinitionStepOperationKind.ReadProjectStructure,
        ProcessDefinitionStepOperationKind.ReadUpstreamArtifacts,
        ProcessDefinitionStepOperationKind.WriteManagedProcessArtifacts,
        ProcessDefinitionStepOperationKind.RecoverArtifactsOnly
    ];

    public ProcessBlockedRunRecoveryPolicy Resolve(
        ProcessRuntimeStateSnapshot state,
        ProcessInstancePlan plan,
        ProcessRuntimeStepState blockedStep,
        ProcessRuntimeStepAssignment targetAssignment,
        StrategyResultReceipt receipt,
        ProcessRecoveryDecisionReceipt decision)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(blockedStep);
        ArgumentNullException.ThrowIfNull(targetAssignment);
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(decision);

        if (decision.RouteKind == ProcessRecoveryRouteKind.ChildRunPropagation)
        {
            return decision.DecisionKind == ProcessRecoveryDecisionKind.ManagerRequired &&
                   decision.FailureCategory == ProcessFailureCategory.ChildRunBlocked &&
                   decision.ResponsibleStepInstanceId == blockedStep.StepInstanceId &&
                   targetAssignment.StepInstanceId == blockedStep.StepInstanceId &&
                   decision.RelatedChildRunId is not null &&
                   string.Equals(
                       decision.SourceDiagnosticCode,
                       ProcessExecutionAdapterDiagnosticCodes.SubprocessChildBlocked,
                       StringComparison.Ordinal) &&
                   receipt.Diagnostics.Count > 0 &&
                   receipt.Diagnostics.All(diagnostic =>
                       string.Equals(
                           diagnostic.Code,
                           ProcessExecutionAdapterDiagnosticCodes.SubprocessChildBlocked,
                           StringComparison.Ordinal) &&
                       diagnostic.Sensitivity == StrategyDiagnosticSensitivity.Normal &&
                       diagnostic.RestrictedEvidenceReference is null &&
                       diagnostic.RetrySafety == ProcessDiagnosticRetrySafety.UnsafeToRetry &&
                       diagnostic.Idempotency == ProcessDiagnosticIdempotencyClassification.Idempotent &&
                       diagnostic.RelatedChildRunId == decision.RelatedChildRunId)
                ? ProcessBlockedRunRecoveryPolicy.CompletedChildConsumerRework
                : ProcessBlockedRunRecoveryPolicy.None;
        }

        if (string.Equals(
                decision.SourceDiagnosticCode,
                ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionBeforeSideEffects,
                StringComparison.Ordinal) ||
            receipt.Diagnostics.Any(diagnostic =>
                string.Equals(
                    diagnostic.Code,
                    ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionBeforeSideEffects,
                    StringComparison.Ordinal)))
        {
            return ProcessBlockedRunRecoveryPolicy.None;
        }

        if (decision.DecisionKind == ProcessRecoveryDecisionKind.ManagerRequired &&
            decision.RouteKind == ProcessRecoveryRouteKind.ManagerAction &&
            (decision.ResponsibleStepInstanceId is null ||
             decision.ResponsibleStepInstanceId == blockedStep.StepInstanceId) &&
            targetAssignment.StepInstanceId == blockedStep.StepInstanceId &&
            IsArtifactOnlyRecoveryTarget(targetAssignment) &&
            HasOnlyNormalSafeIdempotentDiagnostics(receipt))
        {
            return ProcessBlockedRunRecoveryPolicy.SafeIdempotentRework;
        }

        if (decision.FailureCategory == ProcessFailureCategory.MissingArtifact)
        {
            if (!IsArtifactOnlyRecoveryTarget(targetAssignment))
            {
                return ProcessBlockedRunRecoveryPolicy.None;
            }

            return ResolveMissingArtifactPolicy(state, blockedStep, receipt, decision);
        }

        return ProcessBlockedRunRecoveryPolicy.None;
    }

    private static ProcessBlockedRunRecoveryPolicy ResolveMissingArtifactPolicy(
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState blockedStep,
        StrategyResultReceipt receipt,
        ProcessRecoveryDecisionReceipt decision)
    {
        if (decision.RouteKind == ProcessRecoveryRouteKind.ManagerAction &&
            (decision.ResponsibleStepInstanceId is null ||
             decision.ResponsibleStepInstanceId == blockedStep.StepInstanceId) &&
            HasOnlyNormalIdempotentDiagnostics(
                receipt,
                decision,
                ProcessRuntimeDiagnosticCodes.MissingExpectedOutputArtifact) &&
            HasMissingExpectedOutput(blockedStep, receipt))
        {
            return ProcessBlockedRunRecoveryPolicy.MissingOutputRework;
        }

        if (decision.RouteKind != ProcessRecoveryRouteKind.UpstreamStepRework ||
            decision.ResponsibleStepInstanceId is null ||
            decision.ResponsibleStepInstanceId == blockedStep.StepInstanceId ||
            !HasOnlyNormalIdempotentDiagnostics(
                receipt,
                decision,
                ProcessRuntimeDiagnosticCodes.MissingRequiredInputArtifact) ||
            !HasRequiredArtifactConnectionFromResponsibleStep(
                state,
                blockedStep,
                decision.ResponsibleStepInstanceId.Value))
        {
            return ProcessBlockedRunRecoveryPolicy.None;
        }

        return ProcessRuntimeArtifactContracts.DependenciesSatisfied(state, blockedStep) &&
               ProcessRuntimeArtifactContracts.RequiredArtifactsAvailable(state, blockedStep)
            ? ProcessBlockedRunRecoveryPolicy.RestoredInputConsumerRework
            : ProcessBlockedRunRecoveryPolicy.MissingInputProducerRework;
    }

    private static bool IsArtifactOnlyRecoveryTarget(
        ProcessRuntimeStepAssignment targetAssignment)
    {
        return Enum.TryParse<ProcessDefinitionStepTargetScopeKind>(
                   targetAssignment.OperationTargetScope,
                   ignoreCase: false,
                   out var targetScope) &&
               targetScope == ProcessDefinitionStepTargetScopeKind.ManagedProcessArtifactsOnly &&
               targetAssignment.AllowedOperations.Count > 0 &&
               targetAssignment.AllowedOperations.All(operation =>
                   Enum.TryParse<ProcessDefinitionStepOperationKind>(
                       operation,
                       ignoreCase: false,
                       out var operationKind) &&
                   ArtifactOnlyRecoveryOperations.Contains(operationKind)) &&
               targetAssignment.AllowedOperations.Any(operation =>
                   Enum.TryParse<ProcessDefinitionStepOperationKind>(
                       operation,
                       ignoreCase: false,
                       out var operationKind) &&
                   operationKind is
                       ProcessDefinitionStepOperationKind.WriteManagedProcessArtifacts or
                       ProcessDefinitionStepOperationKind.RecoverArtifactsOnly);
    }

    private static bool HasOnlyNormalSafeIdempotentDiagnostics(StrategyResultReceipt receipt)
    {
        return receipt.Diagnostics.Count > 0 &&
               receipt.Diagnostics.All(diagnostic =>
                   diagnostic.Sensitivity == StrategyDiagnosticSensitivity.Normal &&
                   diagnostic.RestrictedEvidenceReference is null &&
                   diagnostic.RetrySafety == ProcessDiagnosticRetrySafety.SafeToRetry &&
                   diagnostic.Idempotency == ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static bool HasOnlyNormalIdempotentDiagnostics(
        StrategyResultReceipt receipt,
        ProcessRecoveryDecisionReceipt decision,
        string expectedCode)
    {
        return string.Equals(
                   decision.SourceDiagnosticCode,
                   expectedCode,
                   StringComparison.OrdinalIgnoreCase) &&
               receipt.Diagnostics.Count > 0 &&
               receipt.Diagnostics.All(diagnostic =>
                   string.Equals(
                       diagnostic.Code,
                       expectedCode,
                       StringComparison.OrdinalIgnoreCase) &&
                   diagnostic.Sensitivity == StrategyDiagnosticSensitivity.Normal &&
                   diagnostic.RestrictedEvidenceReference is null &&
                   diagnostic.Idempotency == ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static bool HasMissingExpectedOutput(
        ProcessRuntimeStepState blockedStep,
        StrategyResultReceipt receipt)
    {
        return blockedStep.ProducedArtifactSlots.Count > 0 &&
               blockedStep.ProducedArtifactSlots.Any(expectedSlotId =>
                   receipt.ProducedArtifacts.All(producedArtifact =>
                       producedArtifact.SlotId != expectedSlotId));
    }

    private static bool HasRequiredArtifactConnectionFromResponsibleStep(
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState blockedStep,
        ProcessStepInstanceId responsibleStepInstanceId)
    {
        return blockedStep.RequiredArtifactSlots.Count > 0 &&
               state.ConnectedInputArtifacts.Any(artifact =>
                   artifact.ConsumerStepInstanceId == blockedStep.StepInstanceId &&
                   blockedStep.RequiredArtifactSlots.Contains(artifact.RequiredSlotId) &&
                   artifact.ProducerStepInstanceId == responsibleStepInstanceId);
    }
}
