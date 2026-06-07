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
            .Describe(ProcessCoreArtifactModelAdapters.ToCoreProjectionSourceKind(sourceKind));
        return ProcessCoreArtifactModelAdapters.FromCoreProducerKind(descriptor.ProducerKind);
    }

    public static bool IsProducerAllowedForMode(
        ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode mode,
        ProcessRunAutomationDispatchService.ProcessArtifactProducerKind producerKind)
    {
        return global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationPolicyRules
            .IsProducerAllowedForMode(
                ToCoreExpectationMode(mode),
                ProcessCoreArtifactModelAdapters.ToCoreProducerKind(producerKind));
    }

    public static bool RequiresManagedEvidencePath(
        ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode mode,
        ProcessRunAutomationDispatchService.ProcessArtifactProducerKind producerKind)
    {
        return global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationPolicyRules
            .RequiresManagedEvidencePath(
                ToCoreExpectationMode(mode),
                ProcessCoreArtifactModelAdapters.ToCoreProducerKind(producerKind));
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
                ProcessCoreArtifactModelAdapters.ToCoreProducerKind(producerKind),
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
