using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

public sealed partial class ProjectProcessLaunchAuthorityService {
    internal void RequireHeldWorkflowSource(ProcessLaunchAuthority authority, IAgentCatalogReadLease? held, bool mutation) {
        authority.Validate();
        RequireReadSource(authority, held);
        if (mutation) {
            RequireSource(authority, authority, held);
        }
    }
}
