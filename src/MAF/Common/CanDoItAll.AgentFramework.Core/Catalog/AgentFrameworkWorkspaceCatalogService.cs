using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceCatalogService(
    ISandboxWorkspaceStore store,
    IAgentPackageService packageService,
    ICapabilityProofService capabilityProofService,
    IProviderProfileService providerProfileService,
    IProviderDiagnosticsService providerDiagnosticsService,
    IProviderProfileRegistry providerRegistry,
    IProviderRuntimeProfileSource providerSource)
{
    private readonly LegacyAgentMemoryCatalog legacyMemoryCatalog = new(store, TimeProvider.System);

    private Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
        Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
        CancellationToken cancellationToken = default)
        => store.UpdateCatalogAsync(update, cancellationToken);

    public Task<IReadOnlyList<AgentMemoryRecord>> ListMemoryAsync(
        Guid agentId,
        CancellationToken cancellationToken = default) =>
        legacyMemoryCatalog.ListAsync(agentId, cancellationToken);

    public Task<Guid> SaveMemoryAsync(
        MemoryEditorModel model,
        CancellationToken cancellationToken = default) =>
        legacyMemoryCatalog.SaveAsync(model, cancellationToken);

    public Task DeleteMemoryAsync(
        Guid memoryId,
        CancellationToken cancellationToken = default) =>
        legacyMemoryCatalog.DeleteAsync(memoryId, cancellationToken);
}
