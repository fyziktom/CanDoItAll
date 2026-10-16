using CanDoItAll.Modules.Workspace;
using CanDoItAll.Workspace.UiSandbox;

namespace CanDoItAll.Tests.Components.WorkspaceUi;

public sealed class WorkspaceSecretSelectionTests {
    [Fact]
    public async Task Operator_choices_are_requested_once_and_save_and_delete_resolutions_are_owner_resolved() {
        var store = new WorkspaceScenarioStore();
        using var secrets = new WorkspaceSecretsController(store);
        await secrets.RefreshAsync();
        var selections = Observe(secrets);

        await secrets.SelectAsync(WorkspaceScenarioStore.SecretId);
        await secrets.SelectAsync(WorkspaceScenarioStore.SecretId);
        secrets.New();
        secrets.New();
        secrets.Draft.Model.Name = "Created by the selection proof";
        secrets.Draft.Model.SecretValue = "synthetic-value";
        await secrets.SaveAsync();
        Assert.Null(secrets.SelectedSecretId);

        secrets.Draft.Model.Name = "Retained after save";
        secrets.Draft.Model.SecretValue = "synthetic-value";
        var gate = store.HoldNext(WorkspaceOperation.SecretSave);
        var saving = secrets.SaveAsync();
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        secrets.Draft.Edited();
        gate.Release();
        await saving;
        var created = secrets.SelectedSecretId;
        Assert.NotNull(created);

        await secrets.DeleteAsync();

        Assert.Equal([
            new WorkspaceSecretSelection(WorkspaceScenarioStore.SecretId, WorkspaceSelectionCause.Operator),
            new WorkspaceSecretSelection(null, WorkspaceSelectionCause.Operator),
            new WorkspaceSecretSelection(created, WorkspaceSelectionCause.Owner),
            new WorkspaceSecretSelection(null, WorkspaceSelectionCause.Owner)
        ], selections);
    }

    [Fact]
    public async Task A_missing_exact_secret_stays_selected_with_an_explicit_error() {
        using var secrets = new WorkspaceSecretsController(new WorkspaceScenarioStore(WorkspaceScenario.MissingSecret));
        await secrets.RefreshAsync();

        await secrets.SelectAsync(WorkspaceScenarioStore.SecretId);

        Assert.Equal(WorkspaceScenarioStore.SecretId, secrets.SelectedSecretId);
        Assert.NotNull(secrets.Error);
        Assert.False(secrets.CanEdit);
    }

    [Fact]
    public async Task A_retired_controller_publishes_no_selection() {
        var secrets = new WorkspaceSecretsController(new WorkspaceScenarioStore());
        var selections = Observe(secrets);
        secrets.Dispose();

        await secrets.SelectAsync(WorkspaceScenarioStore.SecretId);
        secrets.New();

        Assert.Empty(selections);
    }

    private static List<WorkspaceSecretSelection> Observe(WorkspaceSecretsController secrets) {
        var selections = new List<WorkspaceSecretSelection>();
        secrets.SelectionChanged += selections.Add;
        return selections;
    }
}
