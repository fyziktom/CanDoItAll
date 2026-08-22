namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowBackendCheckpointSessionEntity
{
    public string Id { get; set; } = string.Empty;

    public Guid RunId { get; set; }

    public Guid WorkflowId { get; set; }

    public Guid WorkflowVersionId { get; set; }

    public int Backend { get; set; }

    public string Format { get; set; } = string.Empty;

    public int FormatVersion { get; set; }

    public int CompilerContractVersion { get; set; }

    public string TopologyFingerprint { get; set; } = string.Empty;

    public long NextCommitOrdinal { get; set; }
}
