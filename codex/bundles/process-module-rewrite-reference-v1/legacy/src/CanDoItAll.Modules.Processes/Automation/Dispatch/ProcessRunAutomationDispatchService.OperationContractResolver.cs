namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    internal static class ProcessStepOperationContractResolver
    {
        internal static ProcessStepOperationContract Resolve(DispatchCandidate candidate)
            => ResolveProcessStepOperationContract(candidate);

        internal static ProcessStepExecutionBoundaryDescriptor ResolveExecutionBoundary(DispatchCandidate candidate)
            => ResolveProcessStepExecutionBoundary(candidate);

        internal static ProcessStepExecutionBoundaryDescriptor ResolveExecutionBoundary(
            DispatchCandidate candidate,
            ProcessStepOperationContract operationContract)
            => ResolveProcessStepExecutionBoundary(candidate, operationContract);

        internal static bool TryResolvePersistedOperationContract(
            ProcessStepDefinition stepDefinition,
            out ProcessStepOperationContract contract)
            => ProcessRunAutomationDispatchService.TryResolvePersistedOperationContract(stepDefinition, out contract);

        internal static bool IsProductReadOnlyValidationStep(DispatchCandidate candidate)
            => ProcessRunAutomationDispatchService.IsProductReadOnlyValidationStep(candidate);

        internal static bool ContainsProductRepairIntent(DispatchCandidate candidate)
            => ProcessRunAutomationDispatchService.ContainsProductRepairIntent(candidate);
    }
}
