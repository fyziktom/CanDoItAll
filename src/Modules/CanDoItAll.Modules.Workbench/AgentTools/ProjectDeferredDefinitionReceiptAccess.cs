using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectDeferredDefinitionReceiptAccess(
    Func<CancellationToken, Task<ProjectGovernedSourceAdmissions>> resolve,
    Func<ProjectGovernedSourceAdmissions, IProjectDefinitionReceiptAccess> bind) : IProjectDefinitionReceiptAccess {
    public async Task<bool> CanReadAsync(ProjectWriteAdmission target, CancellationToken cancellationToken) {
        try {
            return await bind(await resolve(cancellationToken)).CanReadAsync(target, cancellationToken);
        } catch (Exception failure) when (failure is AgentToolReceiptAccessDeniedException or AgentToolAdmissionException) {
            return false;
        }
    }
}
