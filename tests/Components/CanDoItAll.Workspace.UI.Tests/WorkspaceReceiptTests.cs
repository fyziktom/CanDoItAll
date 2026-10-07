using CanDoItAll.Modules.Workspace;
using CanDoItAll.Workspace.UI;
using CanDoItAll.Workspace.UiSandbox;

namespace CanDoItAll.Tests.Components.WorkspaceUi;

public sealed class WorkspaceReceiptTests {
    [Fact]
    public async Task Defaults_warning_survives_reference_and_observation_failures() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceDefaultsController(store);
        await state.RefreshAsync();
        store.FaultNext(WorkspaceOperation.DefaultsSave, WorkspaceFault.CommittedWarning);
        store.FaultNext(WorkspaceOperation.ProvidersRead, WorkspaceFault.ReadFailure);
        await state.SaveAsync();
        var receipt = Assert.Single(state.Receipts);
        store.FaultNext(WorkspaceOperation.DefaultsRead, WorkspaceFault.ReadFailure);
        await state.ReviewAsync(receipt);
        AssertPreserved(receipt);
    }

    [Fact]
    public async Task Secret_cleanup_warning_survives_observation_failure() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceSecretsController(store);
        await state.SelectAsync(WorkspaceScenarioStore.SecretId);
        store.FaultNext(WorkspaceOperation.SecretSave, WorkspaceFault.CommittedWarning);
        await state.SaveAsync();
        var receipt = Assert.Single(state.Receipts);
        store.FaultNext(WorkspaceOperation.SecretList, WorkspaceFault.ReadFailure);
        await state.ReviewAsync(receipt);
        AssertPreserved(receipt);
    }

    [Fact]
    public async Task File_warning_survives_observation_failure() {
        var store = new WorkspaceScenarioStore();
        using var state = new WorkspaceFilesController(store);
        await state.RefreshAsync();
        state.Select(Assert.Single(state.Preferences));
        store.FaultNext(WorkspaceOperation.FileSave, WorkspaceFault.CommittedWarning);
        await state.SaveAsync();
        var receipt = Assert.Single(state.Receipts);
        store.FaultNext(WorkspaceOperation.FileList, WorkspaceFault.ReadFailure);
        await state.ReviewAsync(receipt);
        AssertPreserved(receipt);
    }

    private static void AssertPreserved(SettingsReceipt receipt) {
        Assert.Equal(SettingsEffect.CommittedWarning, receipt.Effect);
        Assert.Equal(SettingsDiagnostic.CleanupPending, receipt.Diagnostic);
        Assert.True(receipt.ObservationFailed);
    }
}
