namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactEvidenceValidationRules
{
    public static bool IsProducerAllowedForMode(
        ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode mode,
        ProcessRunAutomationDispatchService.ProcessArtifactProducerKind producerKind)
    {
        return mode switch
        {
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Narrative =>
                producerKind != ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkflowRun,
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Decision =>
                producerKind is not ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkflowRun and
                    not ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ProviderNativeBrowser,
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Evidence =>
                producerKind is not ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.AssistantResponse and
                    not ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.CompletedDecision,
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Deliverable =>
                producerKind is
                    ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.AgentExecutionArtifact or
                    ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkspaceWrite or
                    ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ExistingManagedFile or
                    ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkflowArtifact or
                    ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.SubprocessArtifact or
                    ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ProcessMock or
                    ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ManagerRecovery or
                    ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.Manual,
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.RuntimeProof =>
                producerKind is
                    ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.AgentExecutionArtifact or
                    ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkspaceWrite or
                    ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ProviderNativeBrowser or
                    ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkflowArtifact or
                    ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.SubprocessArtifact or
                    ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ProcessMock or
                    ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ManagerRecovery or
                    ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.Manual,
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.RecoveryDiagnostic => false,
            _ => false
        };
    }

    public static bool RequiresManagedEvidencePath(
        ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode mode,
        ProcessRunAutomationDispatchService.ProcessArtifactProducerKind producerKind)
    {
        if (producerKind == ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkflowArtifact)
        {
            return false;
        }

        return mode is
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Evidence or
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Deliverable or
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.RuntimeProof;
    }

    public static bool RequiresStoredArtifactContent(
        bool expectationIsRequired,
        ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode mode,
        ProcessRunAutomationDispatchService.ProcessArtifactProducerKind producerKind,
        string managedStoragePath)
    {
        if (RequiresManagedEvidencePath(mode, producerKind))
        {
            return true;
        }

        if (!expectationIsRequired ||
            mode is not (
                ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Narrative or
                ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Decision))
        {
            return false;
        }

        return producerKind is not ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkflowArtifact &&
               !string.IsNullOrWhiteSpace(managedStoragePath);
    }
}
