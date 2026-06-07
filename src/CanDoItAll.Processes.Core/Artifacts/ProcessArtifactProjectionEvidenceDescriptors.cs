namespace CanDoItAll.Processes.Core.Artifacts;

public enum ProcessCoreProviderNativeBrowserEvidenceKind
{
    Unknown = 0,
    Screenshot = 1,
    Snapshot = 2,
    ConsoleLog = 3,
    DomOrState = 4
}

public sealed record ProcessArtifactProjectionLineageDescriptor(
    ProcessCoreArtifactProjectionSourceKind SourceKind,
    Guid? SourceExecutionRunId,
    Guid? RecoveryExecutionRunId,
    Guid? RecoveredForExecutionRunId,
    Guid? ProjectedExecutionRunId,
    Guid? WorkflowRunId,
    Guid? WorkflowArtifactId,
    Guid? SubprocessRunId,
    Guid? SourceArtifactId,
    Guid? ReworkPacketId,
    string SourceExternalReferenceKey,
    string ContentHash,
    string ProjectionIdentityHash,
    bool HasRuntimeSource,
    bool HasRecordOnlySource,
    bool HasRecoveryLineage,
    bool HasSourceArtifact,
    bool IsProviderNativeBrowserEvidence);

public sealed record ProcessArtifactProjectionSourceOrderDescriptor(
    ProcessCoreArtifactProjectionSourceKind SourceKind,
    ProcessCoreArtifactProducerKind ProducerKind,
    int ProjectionOrder,
    bool IsRuntimeEvidenceSource,
    bool IsRecordOnlySource,
    bool RunsBeforeRecordOnlySources,
    bool IsProviderNativeBrowserEvidence);

public sealed record ProcessProviderNativeBrowserEvidenceDescriptor(
    ProcessCoreProviderNativeBrowserEvidenceKind EvidenceKind,
    string ToolName,
    bool HasDeclaredPath,
    bool HasMatchedOutput,
    bool CanSatisfyRequiredArtifact);

public static class ProcessArtifactProjectionEvidenceDescriptorRules
{
    public static ProcessArtifactProjectionLineageDescriptor DescribeLineage(
        ProcessCoreArtifactProjectionSourceKind sourceKind,
        Guid? sourceExecutionRunId,
        Guid? recoveryExecutionRunId,
        Guid? recoveredForExecutionRunId,
        Guid? projectedExecutionRunId,
        Guid? workflowRunId,
        Guid? workflowArtifactId,
        Guid? subprocessRunId,
        Guid? sourceArtifactId,
        Guid? reworkPacketId,
        string? sourceExternalReferenceKey,
        string? contentHash,
        string? projectionIdentityHash)
    {
        var eligibility = ProcessArtifactProjectionEligibilityRules.Describe(sourceKind);
        return new ProcessArtifactProjectionLineageDescriptor(
            sourceKind,
            sourceExecutionRunId,
            recoveryExecutionRunId,
            recoveredForExecutionRunId,
            projectedExecutionRunId,
            workflowRunId,
            workflowArtifactId,
            subprocessRunId,
            sourceArtifactId,
            reworkPacketId,
            NormalizeDescriptorText(sourceExternalReferenceKey),
            NormalizeDescriptorText(contentHash),
            NormalizeDescriptorText(projectionIdentityHash),
            eligibility.IsRuntimeEvidenceSource,
            eligibility.IsRecordOnlySource,
            recoveryExecutionRunId.HasValue && recoveredForExecutionRunId.HasValue,
            sourceArtifactId.HasValue,
            sourceKind == ProcessCoreArtifactProjectionSourceKind.ProviderNativeBrowser);
    }

    public static ProcessArtifactProjectionSourceOrderDescriptor DescribeSourceOrder(
        ProcessCoreArtifactProjectionSourceKind sourceKind)
    {
        var eligibility = ProcessArtifactProjectionEligibilityRules.Describe(sourceKind);
        return new ProcessArtifactProjectionSourceOrderDescriptor(
            sourceKind,
            eligibility.ProducerKind,
            ResolveProjectionOrder(sourceKind),
            eligibility.IsRuntimeEvidenceSource,
            eligibility.IsRecordOnlySource,
            ResolveProjectionOrder(sourceKind) < ResolveProjectionOrder(ProcessCoreArtifactProjectionSourceKind.CompletedDecision),
            sourceKind == ProcessCoreArtifactProjectionSourceKind.ProviderNativeBrowser);
    }

    public static bool IsDefaultProjectionOrder(
        IReadOnlyList<ProcessCoreArtifactProjectionSourceKind> sourceKinds)
    {
        ArgumentNullException.ThrowIfNull(sourceKinds);

        var previousOrder = int.MinValue;
        foreach (var sourceKind in sourceKinds)
        {
            var currentOrder = ResolveProjectionOrder(sourceKind);
            if (currentOrder < previousOrder)
            {
                return false;
            }

            previousOrder = currentOrder;
        }

        return true;
    }

    public static ProcessProviderNativeBrowserEvidenceDescriptor DescribeProviderNativeBrowserEvidence(
        string? toolName,
        bool hasDeclaredPath,
        bool hasMatchedOutput)
    {
        var evidenceKind = ResolveProviderNativeBrowserEvidenceKind(toolName);
        return new ProcessProviderNativeBrowserEvidenceDescriptor(
            evidenceKind,
            NormalizeToolName(toolName),
            hasDeclaredPath,
            hasMatchedOutput,
            hasDeclaredPath &&
            hasMatchedOutput &&
            evidenceKind != ProcessCoreProviderNativeBrowserEvidenceKind.Unknown);
    }

    public static ProcessCoreProviderNativeBrowserEvidenceKind ResolveProviderNativeBrowserEvidenceKind(
        string? toolName)
    {
        return NormalizeToolName(toolName) switch
        {
            "browser_take_screenshot" => ProcessCoreProviderNativeBrowserEvidenceKind.Screenshot,
            "browser_snapshot" => ProcessCoreProviderNativeBrowserEvidenceKind.Snapshot,
            "browser_console_messages" => ProcessCoreProviderNativeBrowserEvidenceKind.ConsoleLog,
            "browser_evaluate" => ProcessCoreProviderNativeBrowserEvidenceKind.DomOrState,
            _ => ProcessCoreProviderNativeBrowserEvidenceKind.Unknown
        };
    }

    private static int ResolveProjectionOrder(ProcessCoreArtifactProjectionSourceKind sourceKind)
    {
        return sourceKind switch
        {
            ProcessCoreArtifactProjectionSourceKind.AgentExecutionArtifact => 10,
            ProcessCoreArtifactProjectionSourceKind.ProcessMock => 20,
            ProcessCoreArtifactProjectionSourceKind.FileWrite => 30,
            ProcessCoreArtifactProjectionSourceKind.ExistingManagedFile => 40,
            ProcessCoreArtifactProjectionSourceKind.AssistantResponse => 50,
            ProcessCoreArtifactProjectionSourceKind.ProviderNativeBrowser => 60,
            ProcessCoreArtifactProjectionSourceKind.CompletedDecision => 70,
            ProcessCoreArtifactProjectionSourceKind.WorkflowRun => 80,
            ProcessCoreArtifactProjectionSourceKind.WorkflowArtifact => 90,
            ProcessCoreArtifactProjectionSourceKind.SubprocessArtifact => 100,
            ProcessCoreArtifactProjectionSourceKind.Manual => 110,
            _ => int.MaxValue
        };
    }

    private static string NormalizeToolName(string? toolName)
    {
        return string.IsNullOrWhiteSpace(toolName)
            ? string.Empty
            : toolName.Replace('-', '_').Trim().ToLowerInvariant();
    }

    private static string NormalizeDescriptorText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}
