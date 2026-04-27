using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AiAgentsDatabaseTransferHandler(IWorkspacePathResolver workspacePathResolver) : IDatabaseTransferHandler
{
    public DatabaseTransferItemDescriptor Descriptor { get; } = new(
        "ai-agents",
        "AI agents",
        "Copies file-backed AI agent catalog entries and their capability catalog.",
        SortOrder: 30);

    public async Task<DatabaseTransferItemPreview> PreviewAsync(
        DatabaseTransferContext context,
        CancellationToken cancellationToken = default)
    {
        var sourceCatalog = await CreateStore(context.SourceProfile).LoadCatalogAsync(cancellationToken);
        var targetCatalog = await CreateStore(context.TargetProfile).LoadCatalogAsync(cancellationToken);

        return new DatabaseTransferItemPreview(
            Descriptor,
            sourceCatalog.Agents.Count > 0,
            $"{sourceCatalog.Agents.Count} agent catalog item(s) and {sourceCatalog.Capabilities.Count} capability item(s) are available.",
            sourceCatalog.Agents.Count == 0 ? "The source database profile has no agent catalog entries." : null,
            sourceCatalog.Agents.Count + sourceCatalog.Capabilities.Count,
            targetCatalog.Agents.Count + targetCatalog.Capabilities.Count);
    }

    public async Task<DatabaseTransferItemResult> TransferAsync(
        DatabaseTransferContext context,
        CancellationToken cancellationToken = default)
    {
        var sourceCatalog = await CreateStore(context.SourceProfile).LoadCatalogAsync(cancellationToken);
        if (sourceCatalog.Agents.Count == 0)
        {
            return new DatabaseTransferItemResult(Descriptor.Key, Descriptor.Label, false, "The source database profile has no AI agents to transfer.", 0);
        }

        var targetStore = CreateStore(context.TargetProfile);
        await targetStore.UpdateCatalogAsync(
            targetCatalog => new SandboxWorkspaceCatalog(
                string.IsNullOrWhiteSpace(sourceCatalog.Version) ? targetCatalog.Version : sourceCatalog.Version,
                sourceCatalog.Agents,
                targetCatalog.Providers,
                sourceCatalog.Capabilities,
                targetCatalog.Memory),
            cancellationToken);

        return new DatabaseTransferItemResult(
            Descriptor.Key,
            Descriptor.Label,
            true,
            $"Copied {sourceCatalog.Agents.Count} AI agent catalog item(s).",
            sourceCatalog.Agents.Count + sourceCatalog.Capabilities.Count);
    }

    private FileSandboxWorkspaceStore CreateStore(ResolvedDatabaseProfile databaseProfile)
    {
        var workspaceRoot = string.IsNullOrWhiteSpace(databaseProfile.Profile.Storage.WorkspaceRoot)
            ? workspacePathResolver.ResolveWorkspaceRoot()
            : databaseProfile.Profile.Storage.WorkspaceRoot;
        var scope = WorkspaceScopeDescriptor.Organization(databaseProfile.Profile.Id.ToString("N"));
        return new FileSandboxWorkspaceStore(workspaceRoot, scope);
    }
}
