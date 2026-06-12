namespace CanDoItAll.Processes.Core.Artifacts;

public enum ProcessCoreArtifactProjectionSourceKind
{
    Unknown = 0,
    AgentExecutionArtifact = 1,
    FileWrite = 2,
    ExistingManagedFile = 3,
    AssistantResponse = 4,
    WorkflowRun = 5,
    WorkflowArtifact = 6,
    SubprocessArtifact = 7,
    CompletedDecision = 8,
    ProcessMock = 9,
    ProviderNativeBrowser = 10,
    Manual = 11
}

public enum ProcessCoreArtifactProducerKind
{
    Unknown = 0,
    AgentExecutionArtifact = 1,
    FileWrite = 2,
    ExistingManagedFile = 3,
    AssistantResponse = 4,
    CompletedDecision = 5,
    ProcessMock = 6,
    ProviderNativeBrowser = 7,
    WorkflowRun = 8,
    WorkflowArtifact = 9,
    SubprocessArtifact = 10,
    ManagerRecovery = 11,
    Manual = 12
}

public enum ProcessCoreArtifactExpectationMode
{
    Narrative = 0,
    Decision = 1,
    Evidence = 2,
    Deliverable = 3,
    RuntimeProof = 4,
    RecoveryDiagnostic = 5
}

public sealed record ProcessArtifactProjectionEligibilityDescriptor(
    ProcessCoreArtifactProjectionSourceKind SourceKind,
    ProcessCoreArtifactProducerKind ProducerKind,
    bool IsRuntimeEvidenceSource,
    bool IsRecordOnlySource);

public sealed record ProcessArtifactValidationRequirementDescriptor(
    Guid ExpectationId,
    ProcessCoreArtifactKind ArtifactKind,
    string Title,
    bool IsRequired,
    string ValidationRequirementSummary,
    string AllowedFutureUsageSummary,
    ProcessCoreArtifactExpectationMode Mode);

public static class ProcessArtifactProjectionEligibilityRules
{
    public static ProcessArtifactProjectionEligibilityDescriptor Describe(
        ProcessCoreArtifactProjectionSourceKind sourceKind)
    {
        return new ProcessArtifactProjectionEligibilityDescriptor(
            sourceKind,
            ResolveProducerKind(sourceKind),
            IsRuntimeEvidenceSource(sourceKind),
            IsRecordOnlySource(sourceKind));
    }

    public static ProcessCoreArtifactProducerKind ResolveProducerKind(
        ProcessCoreArtifactProjectionSourceKind sourceKind)
    {
        return sourceKind switch
        {
            ProcessCoreArtifactProjectionSourceKind.AgentExecutionArtifact => ProcessCoreArtifactProducerKind.AgentExecutionArtifact,
            ProcessCoreArtifactProjectionSourceKind.FileWrite => ProcessCoreArtifactProducerKind.FileWrite,
            ProcessCoreArtifactProjectionSourceKind.ExistingManagedFile => ProcessCoreArtifactProducerKind.ExistingManagedFile,
            ProcessCoreArtifactProjectionSourceKind.AssistantResponse => ProcessCoreArtifactProducerKind.AssistantResponse,
            ProcessCoreArtifactProjectionSourceKind.WorkflowRun => ProcessCoreArtifactProducerKind.WorkflowRun,
            ProcessCoreArtifactProjectionSourceKind.WorkflowArtifact => ProcessCoreArtifactProducerKind.WorkflowArtifact,
            ProcessCoreArtifactProjectionSourceKind.SubprocessArtifact => ProcessCoreArtifactProducerKind.SubprocessArtifact,
            ProcessCoreArtifactProjectionSourceKind.CompletedDecision => ProcessCoreArtifactProducerKind.CompletedDecision,
            ProcessCoreArtifactProjectionSourceKind.ProcessMock => ProcessCoreArtifactProducerKind.ProcessMock,
            ProcessCoreArtifactProjectionSourceKind.ProviderNativeBrowser => ProcessCoreArtifactProducerKind.ProviderNativeBrowser,
            ProcessCoreArtifactProjectionSourceKind.Manual => ProcessCoreArtifactProducerKind.Manual,
            _ => ProcessCoreArtifactProducerKind.Unknown
        };
    }

    public static bool IsRuntimeEvidenceSource(ProcessCoreArtifactProjectionSourceKind sourceKind)
    {
        return sourceKind is
            ProcessCoreArtifactProjectionSourceKind.AgentExecutionArtifact or
            ProcessCoreArtifactProjectionSourceKind.FileWrite or
            ProcessCoreArtifactProjectionSourceKind.ProviderNativeBrowser or
            ProcessCoreArtifactProjectionSourceKind.ProcessMock;
    }

    public static bool IsRecordOnlySource(ProcessCoreArtifactProjectionSourceKind sourceKind)
    {
        return sourceKind is
            ProcessCoreArtifactProjectionSourceKind.CompletedDecision or
            ProcessCoreArtifactProjectionSourceKind.WorkflowRun or
            ProcessCoreArtifactProjectionSourceKind.WorkflowArtifact or
            ProcessCoreArtifactProjectionSourceKind.SubprocessArtifact;
    }
}

public static class ProcessArtifactValidationRequirementDescriptorRules
{
    public static ProcessArtifactValidationRequirementDescriptor Describe(
        ProcessArtifactExpectationSnapshot expectation)
    {
        ArgumentNullException.ThrowIfNull(expectation);

        return new ProcessArtifactValidationRequirementDescriptor(
            expectation.Id,
            expectation.ArtifactKind,
            expectation.Title,
            expectation.IsRequired,
            expectation.ValidationRequirementSummary,
            expectation.AllowedFutureUsageSummary,
            ResolveExpectationMode(expectation));
    }

    public static ProcessCoreArtifactExpectationMode ResolveExpectationMode(
        ProcessArtifactExpectationSnapshot expectation)
    {
        ArgumentNullException.ThrowIfNull(expectation);

        var contractText = CollapsePromptWhitespace(string.Join(
            ' ',
            expectation.Title,
            expectation.ValidationRequirementSummary)).ToLowerInvariant();
        if (TryResolveExplicitArtifactExpectationMode(contractText, out var explicitMode))
        {
            return explicitMode;
        }

        if (contractText.Contains("runtime proof", StringComparison.Ordinal) ||
            contractText.Contains("browser proof", StringComparison.Ordinal) ||
            contractText.Contains("test output", StringComparison.Ordinal) ||
            contractText.Contains("build output", StringComparison.Ordinal) ||
            contractText.Contains("command output", StringComparison.Ordinal) ||
            contractText.Contains("screenshot", StringComparison.Ordinal) ||
            ContainsRuntimeLogSignal(contractText))
        {
            return ProcessCoreArtifactExpectationMode.RuntimeProof;
        }

        return expectation.ArtifactKind switch
        {
            ProcessCoreArtifactKind.Decision or ProcessCoreArtifactKind.DecisionRecord => ProcessCoreArtifactExpectationMode.Decision,
            ProcessCoreArtifactKind.Deliverable => ProcessCoreArtifactExpectationMode.Deliverable,
            ProcessCoreArtifactKind.Evidence or ProcessCoreArtifactKind.Transcript or ProcessCoreArtifactKind.Dataset => ProcessCoreArtifactExpectationMode.Evidence,
            _ => ProcessCoreArtifactExpectationMode.Narrative
        };
    }

    private static bool TryResolveExplicitArtifactExpectationMode(
        string contractText,
        out ProcessCoreArtifactExpectationMode mode)
    {
        mode = ProcessCoreArtifactExpectationMode.Narrative;
        if (!contractText.Contains("artifact mode", StringComparison.OrdinalIgnoreCase) &&
            !contractText.Contains("expectation mode", StringComparison.OrdinalIgnoreCase) &&
            !contractText.Contains("mode:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var candidateMode in Enum.GetValues<ProcessCoreArtifactExpectationMode>())
        {
            if (contractText.Contains(candidateMode.ToString().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
            {
                mode = candidateMode;
                return true;
            }
        }

        return false;
    }

    private static bool ContainsRuntimeLogSignal(string contractText)
    {
        return contractText.Contains("test log", StringComparison.Ordinal) ||
               contractText.Contains("build log", StringComparison.Ordinal) ||
               contractText.Contains("command log", StringComparison.Ordinal) ||
               contractText.Contains("runtime log", StringComparison.Ordinal) ||
               contractText.Contains("execution log", StringComparison.Ordinal) ||
               contractText.Contains("browser console log", StringComparison.Ordinal) ||
               contractText.Contains("console log", StringComparison.Ordinal);
    }

    private static string CollapsePromptWhitespace(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

public static class ProcessArtifactValidationPolicyRules
{
    public static bool IsProducerAllowedForMode(
        ProcessCoreArtifactExpectationMode mode,
        ProcessCoreArtifactProducerKind producerKind)
    {
        return mode switch
        {
            ProcessCoreArtifactExpectationMode.Narrative =>
                producerKind != ProcessCoreArtifactProducerKind.WorkflowRun,
            ProcessCoreArtifactExpectationMode.Decision =>
                producerKind is not ProcessCoreArtifactProducerKind.WorkflowRun and
                    not ProcessCoreArtifactProducerKind.ProviderNativeBrowser,
            ProcessCoreArtifactExpectationMode.Evidence =>
                producerKind is not ProcessCoreArtifactProducerKind.AssistantResponse and
                    not ProcessCoreArtifactProducerKind.CompletedDecision,
            ProcessCoreArtifactExpectationMode.Deliverable =>
                producerKind is
                    ProcessCoreArtifactProducerKind.AgentExecutionArtifact or
                    ProcessCoreArtifactProducerKind.FileWrite or
                    ProcessCoreArtifactProducerKind.ExistingManagedFile or
                    ProcessCoreArtifactProducerKind.WorkflowArtifact or
                    ProcessCoreArtifactProducerKind.SubprocessArtifact or
                    ProcessCoreArtifactProducerKind.ProcessMock or
                    ProcessCoreArtifactProducerKind.ManagerRecovery or
                    ProcessCoreArtifactProducerKind.Manual,
            ProcessCoreArtifactExpectationMode.RuntimeProof =>
                producerKind is
                    ProcessCoreArtifactProducerKind.AgentExecutionArtifact or
                    ProcessCoreArtifactProducerKind.FileWrite or
                    ProcessCoreArtifactProducerKind.ProviderNativeBrowser or
                    ProcessCoreArtifactProducerKind.WorkflowArtifact or
                    ProcessCoreArtifactProducerKind.SubprocessArtifact or
                    ProcessCoreArtifactProducerKind.ProcessMock or
                    ProcessCoreArtifactProducerKind.ManagerRecovery or
                    ProcessCoreArtifactProducerKind.Manual,
            ProcessCoreArtifactExpectationMode.RecoveryDiagnostic => false,
            _ => false
        };
    }

    public static bool RequiresManagedEvidencePath(
        ProcessCoreArtifactExpectationMode mode,
        ProcessCoreArtifactProducerKind producerKind)
    {
        if (producerKind == ProcessCoreArtifactProducerKind.WorkflowArtifact)
        {
            return false;
        }

        return mode is
            ProcessCoreArtifactExpectationMode.Evidence or
            ProcessCoreArtifactExpectationMode.Deliverable or
            ProcessCoreArtifactExpectationMode.RuntimeProof;
    }

    public static bool RequiresStoredArtifactContent(
        bool expectationIsRequired,
        ProcessCoreArtifactExpectationMode mode,
        ProcessCoreArtifactProducerKind producerKind,
        string artifactReference)
    {
        if (RequiresManagedEvidencePath(mode, producerKind))
        {
            return true;
        }

        if (!expectationIsRequired ||
            mode is not (
                ProcessCoreArtifactExpectationMode.Narrative or
                ProcessCoreArtifactExpectationMode.Decision))
        {
            return false;
        }

        return producerKind is not ProcessCoreArtifactProducerKind.WorkflowArtifact &&
               !string.IsNullOrWhiteSpace(artifactReference);
    }
}
