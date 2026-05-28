namespace CanDoItAll.Modules.Processes;

public enum ProcessArtifactProjectionSourceKind
{
    Unknown = 0,
    AgentExecutionArtifact = 1,
    WorkspaceWrite = 2,
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

public sealed class ProcessArtifactProjectionLineage
{
    public ProcessArtifactProjectionSourceKind SourceKind { get; set; }

    public Guid? SourceExecutionRunId { get; set; }

    public Guid? RecoveryExecutionRunId { get; set; }

    public Guid? RecoveredForExecutionRunId { get; set; }

    public Guid? ProjectedExecutionRunId { get; set; }

    public Guid? WorkflowRunId { get; set; }

    public Guid? WorkflowArtifactId { get; set; }

    public Guid? SubprocessRunId { get; set; }

    public Guid? SourceArtifactId { get; set; }

    public Guid? ReworkPacketId { get; set; }

    public string SourceExternalReferenceKey { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;

    public string ProjectionIdentityHash { get; set; } = string.Empty;
}

internal static class ProcessArtifactProjectionLineageJson
{
    public static string Serialize(ProcessArtifactProjectionLineage? lineage)
        => ProcessArtifactIdentityService.SerializeProjectionLineage(lineage);

    public static string SerializeNormalized(ProcessArtifactProjectionLineage? lineage)
        => ProcessArtifactIdentityService.SerializeNormalizedProjectionLineage(lineage);

    public static ProcessArtifactProjectionLineage? Normalize(ProcessArtifactProjectionLineage? lineage)
        => ProcessArtifactIdentityService.NormalizeProjectionLineage(lineage);

    public static string ComputeIdentityHash(ProcessArtifactProjectionLineage? lineage)
        => ProcessArtifactIdentityService.ComputeProjectionIdentityHash(lineage);

    public static ProcessArtifactProjectionLineage? Deserialize(string? value)
        => ProcessArtifactIdentityService.DeserializeProjectionLineage(value);
}
