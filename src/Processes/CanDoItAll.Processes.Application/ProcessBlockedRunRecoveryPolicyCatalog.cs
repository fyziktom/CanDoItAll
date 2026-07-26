using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessBlockedRunRecoveryPolicyCatalog : IProcessBlockedRunRecoveryPolicyCatalog
{
    private const string SimpleAppTemplateKey = "simple-app-delivery";

    public ProcessBlockedRunRecoveryPolicy Resolve(
        ProcessRuntimeStateSnapshot state,
        ProcessInstancePlan plan,
        ProcessRuntimeStepState blockedStep,
        StrategyResultReceipt receipt,
        ProcessRecoveryDecisionReceipt decision)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(blockedStep);
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(decision);

        if (receipt.Diagnostics.Count > 0 &&
            receipt.Diagnostics.All(diagnostic =>
                diagnostic.Sensitivity == StrategyDiagnosticSensitivity.Normal &&
                diagnostic.RestrictedEvidenceReference is null &&
                diagnostic.RetrySafety == ProcessDiagnosticRetrySafety.SafeToRetry &&
                diagnostic.Idempotency == ProcessDiagnosticIdempotencyClassification.Idempotent))
        {
            return ProcessBlockedRunRecoveryPolicy.SafeIdempotentRework;
        }

        if (!IsSimpleAppPlan(plan) ||
            decision.FailureCategory != ProcessFailureCategory.MissingArtifact)
        {
            return ProcessBlockedRunRecoveryPolicy.None;
        }

        if (decision.RouteKind == ProcessRecoveryRouteKind.ManagerAction &&
            (decision.ResponsibleStepInstanceId is null ||
             decision.ResponsibleStepInstanceId == blockedStep.StepInstanceId) &&
            HasOnlyNormalIdempotentDiagnostics(
                receipt,
                ProcessRuntimeDiagnosticCodes.MissingExpectedOutputArtifact))
        {
            return ProcessBlockedRunRecoveryPolicy.SimpleAppMissingOutputRework;
        }

        if (decision.RouteKind != ProcessRecoveryRouteKind.UpstreamStepRework ||
            decision.ResponsibleStepInstanceId is null ||
            !HasOnlyNormalIdempotentDiagnostics(
                receipt,
                ProcessRuntimeDiagnosticCodes.MissingRequiredInputArtifact))
        {
            return ProcessBlockedRunRecoveryPolicy.None;
        }

        return ProcessRuntimeArtifactContracts.DependenciesSatisfied(state, blockedStep) &&
               ProcessRuntimeArtifactContracts.RequiredArtifactsAvailable(state, blockedStep)
            ? ProcessBlockedRunRecoveryPolicy.SimpleAppRestoredInputConsumerRework
            : ProcessBlockedRunRecoveryPolicy.SimpleAppMissingInputProducerRework;
    }

    private static bool HasOnlyNormalIdempotentDiagnostics(
        StrategyResultReceipt receipt,
        string expectedCode)
    {
        return receipt.Diagnostics.Count > 0 &&
               receipt.Diagnostics.All(diagnostic =>
                   string.Equals(
                       diagnostic.Code,
                       expectedCode,
                       StringComparison.OrdinalIgnoreCase) &&
                   diagnostic.Sensitivity == StrategyDiagnosticSensitivity.Normal &&
                   diagnostic.RestrictedEvidenceReference is null &&
                   diagnostic.Idempotency == ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static bool IsSimpleAppPlan(ProcessInstancePlan plan)
    {
        return plan.Definition.TemplateComponents.Any(component =>
            string.Equals(component.Key, SimpleAppTemplateKey, StringComparison.OrdinalIgnoreCase));
    }
}
