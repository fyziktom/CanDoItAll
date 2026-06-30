namespace CanDoItAll.AgentFramework.Persistence;

internal sealed record WorkspaceStorageIndex(
    long Revision,
    DateTimeOffset UpdatedAtUtc);
