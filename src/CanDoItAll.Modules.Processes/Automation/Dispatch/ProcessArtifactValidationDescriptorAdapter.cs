namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactValidationDescriptorAdapter
{
    public static ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode ResolveArtifactExpectationMode(
        ProcessRunAutomationDispatchService.DispatchArtifactExpectation expectation)
    {
        ArgumentNullException.ThrowIfNull(expectation);

        var descriptor = global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationRequirementDescriptorRules
            .Describe(ToCoreExpectationSnapshot(expectation));
        return FromCoreExpectationMode(descriptor.Mode);
    }

    public static ProcessRunAutomationDispatchService.ProcessArtifactProducerKind ResolveArtifactProducerKind(
        ProcessArtifactProjectionSourceKind sourceKind)
    {
        var descriptor = global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEligibilityRules
            .Describe(ToCoreProjectionSourceKind(sourceKind));
        return FromCoreProducerKind(descriptor.ProducerKind);
    }

    public static bool IsProducerAllowedForMode(
        ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode mode,
        ProcessRunAutomationDispatchService.ProcessArtifactProducerKind producerKind)
    {
        return global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationPolicyRules
            .IsProducerAllowedForMode(
                ToCoreExpectationMode(mode),
                ToCoreProducerKind(producerKind));
    }

    public static bool RequiresManagedEvidencePath(
        ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode mode,
        ProcessRunAutomationDispatchService.ProcessArtifactProducerKind producerKind)
    {
        return global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationPolicyRules
            .RequiresManagedEvidencePath(
                ToCoreExpectationMode(mode),
                ToCoreProducerKind(producerKind));
    }

    public static bool RequiresStoredArtifactContent(
        bool expectationIsRequired,
        ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode mode,
        ProcessRunAutomationDispatchService.ProcessArtifactProducerKind producerKind,
        string managedStoragePath)
    {
        return global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationPolicyRules
            .RequiresStoredArtifactContent(
                expectationIsRequired,
                ToCoreExpectationMode(mode),
                ToCoreProducerKind(producerKind),
                managedStoragePath);
    }

    private static global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot ToCoreExpectationSnapshot(
        ProcessRunAutomationDispatchService.DispatchArtifactExpectation expectation)
    {
        return new global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot(
            expectation.Id,
            ProcessCoreArtifactModelAdapters.ToCoreArtifactKind(expectation.ArtifactKind),
            expectation.Title,
            expectation.IsRequired,
            ToCoreTrustRequirement(expectation.TrustRequirement),
            ToCoreSensitivityLevel(expectation.SensitivityLevel),
            expectation.ValidationRequirementSummary,
            expectation.AllowedFutureUsageSummary);
    }

    private static global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind ToCoreProjectionSourceKind(
        ProcessArtifactProjectionSourceKind sourceKind)
    {
        return sourceKind switch
        {
            ProcessArtifactProjectionSourceKind.AgentExecutionArtifact => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.AgentExecutionArtifact,
            ProcessArtifactProjectionSourceKind.WorkspaceWrite => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.FileWrite,
            ProcessArtifactProjectionSourceKind.ExistingManagedFile => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.ExistingManagedFile,
            ProcessArtifactProjectionSourceKind.AssistantResponse => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.AssistantResponse,
            ProcessArtifactProjectionSourceKind.WorkflowRun => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.WorkflowRun,
            ProcessArtifactProjectionSourceKind.WorkflowArtifact => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.WorkflowArtifact,
            ProcessArtifactProjectionSourceKind.SubprocessArtifact => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.SubprocessArtifact,
            ProcessArtifactProjectionSourceKind.CompletedDecision => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.CompletedDecision,
            ProcessArtifactProjectionSourceKind.ProcessMock => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.ProcessMock,
            ProcessArtifactProjectionSourceKind.ProviderNativeBrowser => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.ProviderNativeBrowser,
            ProcessArtifactProjectionSourceKind.Manual => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.Manual,
            _ => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind.Unknown
        };
    }

    private static global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode ToCoreExpectationMode(
        ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode mode)
    {
        return mode switch
        {
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Narrative => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode.Narrative,
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Decision => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode.Decision,
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Evidence => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode.Evidence,
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Deliverable => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode.Deliverable,
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.RuntimeProof => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode.RuntimeProof,
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.RecoveryDiagnostic => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode.RecoveryDiagnostic,
            _ => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode.Narrative
        };
    }

    private static ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode FromCoreExpectationMode(
        global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode mode)
    {
        return mode switch
        {
            global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode.Narrative => ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Narrative,
            global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode.Decision => ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Decision,
            global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode.Evidence => ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Evidence,
            global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode.Deliverable => ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Deliverable,
            global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode.RuntimeProof => ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.RuntimeProof,
            global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode.RecoveryDiagnostic => ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.RecoveryDiagnostic,
            _ => ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Narrative
        };
    }

    private static global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind ToCoreProducerKind(
        ProcessRunAutomationDispatchService.ProcessArtifactProducerKind producerKind)
    {
        return producerKind switch
        {
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.AgentExecutionArtifact => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.AgentExecutionArtifact,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkspaceWrite => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.FileWrite,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ExistingManagedFile => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.ExistingManagedFile,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.AssistantResponse => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.AssistantResponse,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.CompletedDecision => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.CompletedDecision,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ProcessMock => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.ProcessMock,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ProviderNativeBrowser => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.ProviderNativeBrowser,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkflowRun => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.WorkflowRun,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkflowArtifact => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.WorkflowArtifact,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.SubprocessArtifact => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.SubprocessArtifact,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ManagerRecovery => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.ManagerRecovery,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.Manual => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.Manual,
            _ => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.Unknown
        };
    }

    private static ProcessRunAutomationDispatchService.ProcessArtifactProducerKind FromCoreProducerKind(
        global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind producerKind)
    {
        return producerKind switch
        {
            global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.AgentExecutionArtifact => ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.AgentExecutionArtifact,
            global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.FileWrite => ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkspaceWrite,
            global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.ExistingManagedFile => ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ExistingManagedFile,
            global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.AssistantResponse => ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.AssistantResponse,
            global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.CompletedDecision => ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.CompletedDecision,
            global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.ProcessMock => ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ProcessMock,
            global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.ProviderNativeBrowser => ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ProviderNativeBrowser,
            global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.WorkflowRun => ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkflowRun,
            global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.WorkflowArtifact => ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkflowArtifact,
            global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.SubprocessArtifact => ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.SubprocessArtifact,
            global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.ManagerRecovery => ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ManagerRecovery,
            global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind.Manual => ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.Manual,
            _ => ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.Unknown
        };
    }

    private static global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustRequirement ToCoreTrustRequirement(
        ProcessArtifactTrustRequirement trustRequirement)
    {
        return trustRequirement switch
        {
            ProcessArtifactTrustRequirement.None => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustRequirement.None,
            ProcessArtifactTrustRequirement.ReviewRequired => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustRequirement.ReviewRequired,
            ProcessArtifactTrustRequirement.HumanApproved => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustRequirement.HumanApproved,
            ProcessArtifactTrustRequirement.TrustedSource => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustRequirement.TrustedSource,
            ProcessArtifactTrustRequirement.ApprovalRequired => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustRequirement.ApprovalRequired,
            _ => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustRequirement.ReviewRequired
        };
    }

    private static global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreSensitivityLevel ToCoreSensitivityLevel(
        ProcessSensitivityLevel sensitivityLevel)
    {
        return sensitivityLevel switch
        {
            ProcessSensitivityLevel.Public => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreSensitivityLevel.Public,
            ProcessSensitivityLevel.Internal => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreSensitivityLevel.Internal,
            ProcessSensitivityLevel.Confidential => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreSensitivityLevel.Confidential,
            ProcessSensitivityLevel.Restricted => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreSensitivityLevel.Restricted,
            _ => global::CanDoItAll.Processes.Core.Artifacts.ProcessCoreSensitivityLevel.Internal
        };
    }
}
