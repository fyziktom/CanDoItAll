using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceCatalogService(
    ISandboxWorkspaceStore store,
    IAgentPackageService packageService,
    ICapabilityProofService capabilityProofService,
    IProviderProfileService providerProfileService,
    IProviderDiagnosticsService providerDiagnosticsService,
    IProviderProfileRegistry providerRegistry)
{
    private Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
        Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
        CancellationToken cancellationToken = default)
        => store.UpdateCatalogAsync(update, cancellationToken);
}
