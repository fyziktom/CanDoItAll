using System.Collections.Immutable;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workspace.StorageCatalog.Contracts;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceStorageCatalogOwner : IStorageCatalogOwner {
    private readonly WorkspaceService workspace;
    private readonly IStorageCatalogService catalog;
    private readonly IDatabaseRuntimeState runtime;
    private readonly StorageCatalogCommands commands;
    private readonly DatabaseRuntimeSnapshot snapshot;

    public WorkspaceStorageCatalogOwner(WorkspaceService workspace, IStorageCatalogService catalog, ICanonicalRuntimeDatabase canonical,
        IDatabaseRuntimeState runtime, StorageCatalogCommands commands) {
        this.workspace = workspace;
        this.catalog = catalog;
        this.runtime = runtime;
        this.commands = commands;
        snapshot = runtime.GetSnapshot();
        Context = new(canonical.Profile.Profile.Id, snapshot.Generation);
    }

    public CatalogContext Context { get; }
    public bool IsCurrent => snapshot.ActiveProfileId == Context.ProfileId && runtime.GetSnapshot() == snapshot;
    public CatalogChoices Choices => StorageCatalogProjection.Choices;
    public async Task<ImmutableArray<CatalogRow>> ReadCatalogAsync(CancellationToken cancellationToken) =>
        [.. (await catalog.ListAsync(cancellationToken)).Select(StorageCatalogProjection.Row)];
    public async Task<ImmutableArray<CatalogSecret>> ReadSecretsAsync(CancellationToken cancellationToken) =>
        [.. (await workspace.ListSecretsAsync(cancellationToken)).Select(secret => new CatalogSecret(secret.Id, secret.Name, true))];
    public async Task<ImmutableArray<CatalogRoute>> ReadRoutesAsync(CancellationToken cancellationToken) =>
        [.. (await workspace.ListStorageRoutingDefaultsAsync(cancellationToken)).Select(route =>
            new CatalogRoute((CatalogPurpose)route.UsagePurpose, route.PreferredStorageId, route.PreferredStorageName, route.IsEnabled))];
    public async Task<CatalogEdit?> ReadEditorAsync(Guid id, CancellationToken cancellationToken) {
        var editor = await catalog.GetEditorAsync(id, cancellationToken);
        return editor is null ? null : StorageCatalogProjection.Editor(editor, await catalog.ListRulesAsync(cancellationToken));
    }
    public Task<CatalogOutcome> ExecuteAsync(CatalogCommand command, CancellationToken cancellationToken) =>
        !IsCurrent || command.Context != Context
            ? Task.FromResult(new CatalogOutcome { Diagnostic = CatalogDiagnostic.Retired })
            : commands.ExecuteAsync(command, snapshot, cancellationToken);
}
