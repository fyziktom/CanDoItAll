using CanDoItAll.Modules.Workspace.StorageCatalog.Contracts;
using CanDoItAll.Workspace.StorageCatalog.UI;
using CanDoItAll.Workspace.StorageCatalog.UiSandbox;

namespace CanDoItAll.Tests.Components.WorkspaceStorageCatalogUi;

public sealed class CatalogSelectionTests {
    [Fact]
    public async Task Operator_choices_are_requested_once_and_a_created_identity_is_owner_resolved() {
        using var session = new CatalogSession(new CatalogScenarioStore(), new());
        var selections = Observe(session);
        await session.LoadAsync();

        await session.SelectAsync(CatalogScenarioData.FileSystemId);
        await session.SelectAsync(CatalogScenarioData.FileSystemId);
        session.New(CatalogProvider.FileSystem);
        session.New(CatalogProvider.Ipfs);
        session.New(CatalogProvider.FileSystem);
        var draft = session.Draft!;
        draft.Name = "Created target";
        draft.EndpointOrRoot = "/scenario/created";
        await session.MutateAsync(CatalogEffect.Save);

        Assert.NotNull(draft.Id);
        Assert.Equal([
            new CatalogSelection(CatalogScenarioData.FileSystemId, CatalogSelectionCause.Operator),
            new CatalogSelection(null, CatalogSelectionCause.Operator),
            new CatalogSelection(draft.Id, CatalogSelectionCause.Owner)
        ], selections);
        Assert.Equal(draft.Id, session.SelectedId);
    }

    [Fact]
    public async Task A_missing_exact_target_stays_selected_and_a_deleted_target_keeps_its_identity() {
        var missing = Guid.Parse("f5d1f0a1-4c07-4b41-9c55-3d1f8a5a8e01");
        using var session = new CatalogSession(new CatalogScenarioStore(), new());
        var selections = Observe(session);

        await session.SelectAsync(missing);
        Assert.Equal(missing, session.SelectedId);
        Assert.Null(session.Draft);

        await session.SelectAsync(CatalogScenarioData.FileSystemId);
        await session.MutateAsync(CatalogEffect.Delete);

        Assert.True(session.Draft!.Deleted);
        Assert.Equal(CatalogScenarioData.FileSystemId, session.SelectedId);
        Assert.Equal([
            new CatalogSelection(missing, CatalogSelectionCause.Operator),
            new CatalogSelection(CatalogScenarioData.FileSystemId, CatalogSelectionCause.Operator)
        ], selections);
    }

    [Fact]
    public async Task A_retired_session_publishes_no_selection() {
        var session = new CatalogSession(new CatalogScenarioStore(), new());
        var selections = Observe(session);
        session.Dispose();

        await session.SelectAsync(CatalogScenarioData.FileSystemId);
        session.New(CatalogProvider.Ipfs);

        Assert.Empty(selections);
    }

    private static List<CatalogSelection> Observe(CatalogSession session) {
        var selections = new List<CatalogSelection>();
        session.SelectionChanged += selections.Add;
        return selections;
    }
}
