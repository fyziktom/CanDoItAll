using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentCatalogCommitObserver {
    void Committed(SandboxWorkspaceCatalog before, SandboxWorkspaceCatalog after);
}
