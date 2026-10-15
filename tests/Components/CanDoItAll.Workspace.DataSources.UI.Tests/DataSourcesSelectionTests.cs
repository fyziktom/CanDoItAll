using CanDoItAll.Modules.Workspace.DataSources.Contracts;
using CanDoItAll.Workspace.DataSources.UI;
using CanDoItAll.Workspace.DataSources.UiSandbox;

namespace CanDoItAll.Tests.Components.WorkspaceDataSourcesUi;

public sealed class DataSourcesSelectionTests {
    private static readonly Guid ProfileA = DataSourcesScenarioOwner.ProfileA;
    private static readonly Guid ProfileB = DataSourcesScenarioOwner.ProfileB;

    [Fact]
    public async Task The_runtime_default_is_owner_resolved_and_operator_choices_are_requested_once() {
        using var session = new DataSourcesSession(new DataSourcesScenarioOwner(), new());
        var selections = Observe(session);

        await session.InitializeAsync();
        await session.SelectAsync(ProfileB);
        await session.SelectAsync(ProfileB);
        session.NewProfile();
        session.NewProfile();

        Assert.Equal([
            new DataSourceSelection(ProfileA, DataSourceSelectionCause.Owner),
            new DataSourceSelection(ProfileB, DataSourceSelectionCause.Operator),
            new DataSourceSelection(null, DataSourceSelectionCause.Operator)
        ], selections);
        Assert.Null(session.SelectedProfileId);
    }

    [Fact]
    public async Task An_explicit_profile_wins_over_the_runtime_default() {
        using var session = new DataSourcesSession(new DataSourcesScenarioOwner(), new());
        var selections = Observe(session);

        await session.InitializeAsync(ProfileB);

        Assert.Equal(ProfileB, session.Draft.Id);
        Assert.Equal(ProfileB, session.SelectedProfileId);
        Assert.Equal([new DataSourceSelection(ProfileB, DataSourceSelectionCause.Operator)], selections);
    }

    [Fact]
    public async Task A_missing_explicit_profile_stays_selected_and_is_never_replaced_by_the_runtime_profile() {
        var missing = Guid.Parse("20000000-0000-0000-0000-0000000000ff");
        using var session = new DataSourcesSession(new DataSourcesScenarioOwner(), new());

        await session.InitializeAsync(missing);

        Assert.Equal(missing, session.SelectedProfileId);
        Assert.NotNull(session.EditorError);
        Assert.Null(session.Draft.Id);
        Assert.False(session.CanMutate);
    }

    [Fact]
    public async Task Created_identity_and_deleted_selection_are_owner_resolved() {
        using var session = new DataSourcesSession(new DataSourcesScenarioOwner(), new());
        await session.InitializeAsync();
        session.NewProfile();
        var selections = Observe(session);

        await session.SaveAsync();
        var created = Assert.Single(selections);
        Assert.Equal(DataSourceSelectionCause.Owner, created.Cause);
        Assert.Equal(session.Draft.Id, created.ProfileId);

        await session.SelectAsync(ProfileB);
        await session.ExecuteAsync(DataSourceAction.Delete);

        Assert.Equal([
            created,
            new DataSourceSelection(ProfileB, DataSourceSelectionCause.Operator),
            new DataSourceSelection(null, DataSourceSelectionCause.Owner)
        ], selections);
    }

    [Fact]
    public async Task Selecting_the_default_again_restores_the_runtime_profile() {
        using var session = new DataSourcesSession(new DataSourcesScenarioOwner(), new());
        await session.InitializeAsync(ProfileB);
        var selections = Observe(session);

        await session.SelectDefaultAsync();

        Assert.Equal(ProfileA, session.Draft.Id);
        Assert.Equal([new DataSourceSelection(ProfileA, DataSourceSelectionCause.Owner)], selections);
    }

    private static List<DataSourceSelection> Observe(DataSourcesSession session) {
        var selections = new List<DataSourceSelection>();
        session.SelectionChanged += selections.Add;
        return selections;
    }
}
