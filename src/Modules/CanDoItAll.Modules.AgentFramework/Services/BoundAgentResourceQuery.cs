using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Modules.AgentFramework;

public interface IBoundAgentResourceQuery {
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}

public sealed class BoundAgentResourceQuery(IAiTechnicalAgentProjectionStore projections) : IBoundAgentResourceQuery {
    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => projections.CountBoundAsync(cancellationToken);
}
