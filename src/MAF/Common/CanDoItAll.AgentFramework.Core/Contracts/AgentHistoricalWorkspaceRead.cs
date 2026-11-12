using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum AgentHistoricalStorageReadStatus {
    Ready,
    Missing,
    RecoveryRequired,
    UnsupportedLayout,
    SizeLimitExceeded,
    SnapshotChanged,
    ReadBudgetExceeded
}

public sealed record AgentHistoricalChatIdentity(Guid Id, Guid AgentId);

public sealed record AgentHistoricalRunSnapshot(ExecutionRunRecord Run, AgentHistoricalChatIdentity? Chat,
    AgentToolSemanticDigest FileDigest);

public sealed record AgentHistoricalRunRead(AgentHistoricalStorageReadStatus Status, AgentHistoricalRunSnapshot? Snapshot = null);

public sealed record AgentHistoricalAgentRead(AgentHistoricalStorageReadStatus Status, WorkspaceScopeDescriptor Scope,
    CatalogDataRevision Revision = default, AgentDefinition? Agent = null, AgentToolSemanticDigest FileDigest = default);

public interface ISandboxWorkspaceHistoricalReadStore {
    Task<AgentHistoricalRunRead> ReadHistoricalRunAsync(Guid executionRunId, CancellationToken cancellationToken = default);
}

public interface IAgentHistoricalCatalogReadStore {
    Task<AgentHistoricalAgentRead> ReadHistoricalAgentAsync(Guid agentId, CancellationToken cancellationToken = default);
}
