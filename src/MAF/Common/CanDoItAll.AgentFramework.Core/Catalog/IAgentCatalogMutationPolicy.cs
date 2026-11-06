using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum AgentCatalogMutationReason { UserCommand, ReadNormalization }

public interface IAgentCatalogMutationPolicy {
    Task<SandboxWorkspaceCatalog> ApplyAsync(SandboxWorkspaceCatalog before, SandboxWorkspaceCatalog after,
        CancellationToken cancellationToken = default);
    Task<SandboxWorkspaceCatalog> ApplyAsync(SandboxWorkspaceCatalog before, SandboxWorkspaceCatalog after,
        AgentCatalogMutationReason reason, CancellationToken cancellationToken = default)
        => ApplyAsync(before, after, cancellationToken);
}
