namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowBackendCheckpointPayloadEntity
{
    public string Id { get; set; } = string.Empty;

    public string SessionId { get; set; } = string.Empty;

    public string? ParentCheckpointId { get; set; }

    public long CommitOrdinal { get; set; }

    public string ProtectedPayload { get; set; } = string.Empty;

    public string PayloadHash { get; set; } = string.Empty;

    public Guid? ExternalRequestId { get; set; }

    public string? BackendRequestId { get; set; }

    public string? BackendRequestPortId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
