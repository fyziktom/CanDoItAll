namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactEvidenceValidationRules
{
    public static bool IsProducerAllowedForMode(
        ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode mode,
        ProcessRunAutomationDispatchService.ProcessArtifactProducerKind producerKind)
    {
        return ProcessArtifactValidationDescriptorAdapter.IsProducerAllowedForMode(mode, producerKind);
    }

    public static bool RequiresManagedEvidencePath(
        ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode mode,
        ProcessRunAutomationDispatchService.ProcessArtifactProducerKind producerKind)
    {
        return ProcessArtifactValidationDescriptorAdapter.RequiresManagedEvidencePath(mode, producerKind);
    }

    public static bool RequiresStoredArtifactContent(
        bool expectationIsRequired,
        ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode mode,
        ProcessRunAutomationDispatchService.ProcessArtifactProducerKind producerKind,
        string managedStoragePath)
    {
        return ProcessArtifactValidationDescriptorAdapter.RequiresStoredArtifactContent(
            expectationIsRequired,
            mode,
            producerKind,
            managedStoragePath);
    }
}
