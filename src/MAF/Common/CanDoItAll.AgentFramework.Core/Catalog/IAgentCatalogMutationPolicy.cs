using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentCatalogMutationPolicy {
    Task<SandboxWorkspaceCatalog> ApplyAsync(SandboxWorkspaceCatalog before, SandboxWorkspaceCatalog after,
        CancellationToken cancellationToken = default);
}
