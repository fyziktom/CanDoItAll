namespace CanDoItAll.AgentFramework.Core;

public interface IIndexedWorkspaceReadGuard {
    Task EnsureReadyAsync(bool requireSummaryIndex = false, CancellationToken cancellationToken = default);
}
