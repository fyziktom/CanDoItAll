using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AiAgentsDatabaseTransferHandler(IWorkspacePathResolver workspacePathResolver) : IDatabaseTransferHandler {
    public DatabaseTransferItemDescriptor Descriptor { get; } = new(
        "ai-agents",
        "AI agents",
        "Copies file-backed AI agent catalog entries and their capability catalog.",
        SortOrder: 30);

    public async Task<DatabaseTransferItemPreview> PreviewAsync(
        DatabaseTransferOperation context,
        CancellationToken cancellationToken = default) {
        var source = await CreateStore(context.SourceProfile).LoadCatalogSnapshotAsync(cancellationToken);
        var target = await CreateStore(context.TargetProfile).LoadCatalogSnapshotAsync(cancellationToken);
        var sourceCatalog = source.Catalog;
        var targetCount = CountEntries(target.Catalog);

        return new DatabaseTransferItemPreview(
            Descriptor,
            sourceCatalog.Agents.Count > 0,
            $"{sourceCatalog.Agents.Count} agent catalog item(s), {sourceCatalog.AgentTeams.Count} team(s), and {sourceCatalog.Capabilities.Count} capability item(s) are available.",
            sourceCatalog.Agents.Count == 0 ? "The source database profile has no agent catalog entries." :
                targetCount > 0 ? "Replacing the reviewed Agent, team, and capability catalog requires explicit approval." : null,
            CountEntries(sourceCatalog), targetCount) { Observation = new(source.Revision.Value, target.Revision.Value) };
    }

    public async Task<DatabaseTransferItemResult> TransferAsync(
        DatabaseTransferOperation context,
        CancellationToken cancellationToken = default) {
        var source = await CreateStore(context.SourceProfile).LoadCatalogSnapshotAsync(cancellationToken);
        var sourceCatalog = source.Catalog;
        if (sourceCatalog.Agents.Count == 0) {
            return new DatabaseTransferItemResult(Descriptor.Key, Descriptor.Label, false, "The source database profile has no AI agents to transfer.", 0);
        }

        var hasObservation = context.Observations.TryGetValue(Descriptor.Key, out var observed);
        if (hasObservation && observed.SourceRevision != source.Revision.Value) {
            return Changed();
        }

        var count = CountEntries(sourceCatalog);
        try {
            await CreateStore(context.TargetProfile).UpdateCatalogAsync(target => {
                if (hasObservation && observed.TargetRevision != target.CatalogDataRevision.Value ||
                    CountEntries(target) > 0 && (!context.ReplaceExisting || !hasObservation)) {
                    throw new TransferCatalogChangedException();
                }
                return target with {
                    Version = string.IsNullOrWhiteSpace(sourceCatalog.Version) ? target.Version : sourceCatalog.Version,
                    Agents = sourceCatalog.Agents,
                    Capabilities = sourceCatalog.Capabilities,
                    AgentTeams = sourceCatalog.AgentTeams
                };
            }, cancellationToken);
            return new(Descriptor.Key, Descriptor.Label, true, $"Copied {count} Agent catalog records.", count);
        } catch (TransferCatalogChangedException) {
            return Changed();
        } catch (SandboxWorkspaceCatalogCommittedException) {
            return new(Descriptor.Key, Descriptor.Label, true, "The catalog was copied. Review follow-up cleanup before another transfer.", count) {
                Warning = DatabaseTransferWarning.PostCommitCleanupIncomplete
            };
        }
    }

    private DatabaseTransferItemResult Changed()
        => new(Descriptor.Key, Descriptor.Label, false, "The catalog requires a current transfer preview and explicit approval for replacement.", 0) { ReviewOutdated = true };

    private static int CountEntries(SandboxWorkspaceCatalog catalog) => catalog.Agents.Count + catalog.AgentTeams.Count + catalog.Capabilities.Count;

    private sealed class TransferCatalogChangedException : InvalidOperationException { }

    private FileSandboxWorkspaceStore CreateStore(ResolvedDatabaseProfile databaseProfile) {
        var workspaceRoot = string.IsNullOrWhiteSpace(databaseProfile.Profile.Storage.WorkspaceRoot)
            ? workspacePathResolver.ResolveWorkspaceRoot()
            : databaseProfile.Profile.Storage.WorkspaceRoot;
        var scope = WorkspaceScopeDescriptor.Organization(databaseProfile.Profile.Id.ToString("N"));
        return new FileSandboxWorkspaceStore(workspaceRoot, scope);
    }
}
