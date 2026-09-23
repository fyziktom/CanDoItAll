using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Workspace;

namespace CanDoItAll.Composition;

internal sealed class ProviderManagementWorkspaceProviderCatalog(
    IProviderAdministrationService providerAdministration) :
    IWorkspaceProviderCatalog
{
    public async Task<IReadOnlyList<WorkspaceProviderOption>> ListAsync(
        CancellationToken cancellationToken = default)
        => (await providerAdministration.ListProviderProfilesAsync(cancellationToken))
            .OrderBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
            .Select(provider => new WorkspaceProviderOption(
                provider.Id,
                provider.Name,
                provider.IsEnabled))
            .ToList();
}
