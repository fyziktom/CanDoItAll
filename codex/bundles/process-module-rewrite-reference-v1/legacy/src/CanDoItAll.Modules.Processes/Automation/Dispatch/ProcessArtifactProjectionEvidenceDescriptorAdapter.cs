namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactProjectionEvidenceDescriptorAdapter
{
    public static global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor DescribeLineage(
        ProcessArtifactProjectionLineage? lineage)
    {
        return global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEvidenceDescriptorRules
            .DescribeLineage(
                ProcessCoreArtifactModelAdapters.ToCoreProjectionSourceKind(lineage?.SourceKind ?? ProcessArtifactProjectionSourceKind.Unknown),
                lineage?.SourceExecutionRunId,
                lineage?.RecoveryExecutionRunId,
                lineage?.RecoveredForExecutionRunId,
                lineage?.ProjectedExecutionRunId,
                lineage?.WorkflowRunId,
                lineage?.WorkflowArtifactId,
                lineage?.SubprocessRunId,
                lineage?.SourceArtifactId,
                lineage?.ReworkPacketId,
                lineage?.SourceExternalReferenceKey,
                lineage?.ContentHash,
                lineage?.ProjectionIdentityHash);
    }

    public static IReadOnlyList<global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionSourceOrderDescriptor> DescribeProjectionSourceOrder(
        IReadOnlyList<ProcessArtifactProjectionSourceKind> sourceKinds)
    {
        ArgumentNullException.ThrowIfNull(sourceKinds);

        return sourceKinds
            .Select(sourceKind => global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEvidenceDescriptorRules
                .DescribeSourceOrder(ProcessCoreArtifactModelAdapters.ToCoreProjectionSourceKind(sourceKind)))
            .ToList();
    }

    public static void VerifyProjectionSourceOrder(
        IReadOnlyList<ProcessArtifactProjectionSourceKind> sourceKinds)
    {
        ArgumentNullException.ThrowIfNull(sourceKinds);

        var coreSourceKinds = sourceKinds
            .Select(ProcessCoreArtifactModelAdapters.ToCoreProjectionSourceKind)
            .ToList();
        if (!global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEvidenceDescriptorRules
                .IsDefaultProjectionOrder(coreSourceKinds))
        {
            throw new InvalidOperationException(
                $"Process artifact projection source order is not compatible with Core projection evidence descriptors. Sources: {string.Join(", ", sourceKinds)}.");
        }
    }

    public static global::CanDoItAll.Processes.Core.Artifacts.ProcessProviderNativeBrowserEvidenceDescriptor DescribeProviderNativeBrowserEvidence(
        string? toolName,
        bool hasDeclaredPath,
        bool hasMatchedOutput)
    {
        return global::CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEvidenceDescriptorRules
            .DescribeProviderNativeBrowserEvidence(
                toolName,
                hasDeclaredPath,
                hasMatchedOutput);
    }
}
