using CanDoItAll.Modules.Workspace.DataSources.Contracts;
using CanDoItAll.Workspace.DataSources.UI;
using CanDoItAll.Workspace.DataSources.UiSandbox;

namespace CanDoItAll.Tests.Components.WorkspaceDataSourcesUi;

public sealed class DataSourcesSessionTests {
    [Fact]
    public async Task Save_captures_once_adopts_confirmed_identity_and_preserves_later_fields_and_password_intent() {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = new DataSourcesScenarioOwner { BeforeWrite = () => gate.Task };
        var operations = new DataSourceOperationLedger();
        using var session = new DataSourcesSession(owner, operations);
        await session.InitializeAsync();
        session.NewProfile();
        var draft = session.Draft;
        var form = session.EditContext;
        draft.DisplayName = "Captured";
        session.Password = "synthetic-once";
        var saving = session.SaveAsync();
        Assert.True(operations.IsBusy);
        Assert.Empty(session.Password);
        draft.DisplayName = "Later změna";
        session.Password = "later-ephemeral";
        await session.SaveAsync();
        Assert.Equal("Captured", owner.LastSave!.DisplayName);
        Assert.Single(owner.Commands);
        gate.SetResult();
        await saving;
        Assert.Same(draft, session.Draft);
        Assert.Same(form, session.EditContext);
        Assert.NotNull(draft.Id);
        Assert.Equal("Later změna", draft.DisplayName);
        Assert.Equal("later-ephemeral", session.Password);
        Assert.True(owner.ReceivedPassword);
        Assert.Equal(draft.Id, Assert.Single(operations.Receipts).Result!.ProfileId);
    }

    [Fact]
    public async Task Acknowledged_save_survives_failed_independent_observations() {
        var owner = new DataSourcesScenarioOwner(DataSourcesScenario.RefreshFailure);
        var operations = new DataSourceOperationLedger();
        using var session = new DataSourcesSession(owner, operations);
        await session.InitializeAsync();
        session.NewProfile();
        await session.SaveAsync();
        Assert.NotNull(session.Draft.Id);
        Assert.NotNull(session.ListError);
        Assert.NotNull(session.RuntimeError);
        Assert.Equal(DataSourceOutcome.Confirmed, Assert.Single(operations.Receipts).Result!.Outcome);
        owner.FailObservation = false;
        await session.RefreshAsync();
        Assert.Single(owner.Commands);
    }

    [Fact]
    public async Task Closing_the_panel_does_not_release_a_pending_operation_or_publish_into_its_successor() {
        var owner = new DataSourcesScenarioOwner(DataSourcesScenario.DelayedWrite);
        var operations = new DataSourceOperationLedger();
        var original = new DataSourcesSession(owner, operations);
        await original.InitializeAsync();
        original.NewProfile();
        var saving = original.SaveAsync();
        original.Dispose();
        using var successor = new DataSourcesSession(owner, operations);
        await successor.InitializeAsync();
        var draft = successor.Draft;
        await successor.SaveAsync();
        Assert.Single(owner.Commands);
        Assert.True(operations.IsBusy);
        owner.Release();
        await saving;
        Assert.False(operations.IsBusy);
        Assert.Same(draft, successor.Draft);
        Assert.Equal(DataSourcesScenarioOwner.ProfileA, successor.Draft.Id);
        Assert.NotEqual(successor.Draft.Id, Assert.Single(operations.Receipts).Result!.ProfileId);
    }

    [Fact]
    public async Task Unknown_save_is_not_unlocked_by_reopening_or_observation() {
        var owner = new DataSourcesScenarioOwner(DataSourcesScenario.UnknownWrite);
        var operations = new DataSourceOperationLedger();
        using (var first = new DataSourcesSession(owner, operations)) {
            await first.InitializeAsync();
            await first.SaveAsync();
        }
        using var second = new DataSourcesSession(owner, operations);
        await second.InitializeAsync();
        await second.SaveAsync();
        await second.ExecuteAsync(DataSourceAction.TestConnection);
        Assert.Single(owner.Commands);
        Assert.True(operations.HasUnknown);
        Assert.False(operations.IsBusy);
        Assert.Equal(DataSourceOutcome.Unknown, Assert.Single(operations.Receipts).Result!.Outcome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Superseded_editor_success_and_failure_cannot_replace_a_newer_same_target_lifetime(bool fail) {
        var owner = new DataSourcesScenarioOwner();
        using var session = new DataSourcesSession(owner, new());
        await session.InitializeAsync();
        var held = new TaskCompletionSource<ProfileEditor>(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        owner.EditorRead = id => ++count == 1 ? held.Task : Task.FromResult(new ProfileEditor(DataSourcesScenarioOwner.Values(id, "Current"), DataSourceProvider.PostgreSql, false, true));
        var old = session.SelectAsync(DataSourcesScenarioOwner.ProfileB);
        await session.SelectAsync(DataSourcesScenarioOwner.ProfileA);
        await session.SelectAsync(DataSourcesScenarioOwner.ProfileB);
        var draft = session.Draft;
        draft.DisplayName = "New B draft";
        if (fail) {
            held.SetException(new DataSourcesException(DataSourceFailure.Missing));
        } else {
            held.SetResult(new(DataSourcesScenarioOwner.Values(DataSourcesScenarioOwner.ProfileB, "Old B"), DataSourceProvider.PostgreSql, false, true));
        }
        await old;
        Assert.Same(draft, session.Draft);
        Assert.Equal("New B draft", draft.DisplayName);
        Assert.Null(session.EditorError);
        Assert.False(session.EditorLoading);
    }

    [Fact]
    public async Task Missing_exact_editor_stays_unacquired_and_can_retry_the_same_target() {
        var owner = new DataSourcesScenarioOwner();
        using var session = new DataSourcesSession(owner, new());
        await session.InitializeAsync();
        owner.EditorRead = id => Task.FromResult(new ProfileEditor(DataSourcesScenarioOwner.Values(null, "Silent new draft"), DataSourceProvider.PostgreSql, false, false));
        await session.SelectAsync(DataSourcesScenarioOwner.ProfileB);
        Assert.NotNull(session.EditorError);
        Assert.False(session.CanMutate);
        owner.EditorRead = null;
        await session.SelectAsync(DataSourcesScenarioOwner.ProfileB);
        Assert.Null(session.EditorError);
        Assert.Equal(DataSourcesScenarioOwner.ProfileB, session.Draft.Id);
        Assert.True(session.CanMutate);
    }

    [Fact]
    public async Task Late_save_after_A_B_A_keeps_original_receipt_without_replacing_the_new_A_draft() {
        var owner = new DataSourcesScenarioOwner(DataSourcesScenario.DelayedWrite);
        var operations = new DataSourceOperationLedger();
        using var session = new DataSourcesSession(owner, operations);
        await session.InitializeAsync();
        var save = session.SaveAsync();
        await session.SelectAsync(DataSourcesScenarioOwner.ProfileB);
        await session.SelectAsync(DataSourcesScenarioOwner.ProfileA);
        session.Draft.DisplayName = "Successor A";
        owner.Release();
        await save;
        Assert.Equal("Successor A", session.Draft.DisplayName);
        Assert.Equal(DataSourcesScenarioOwner.ProfileA, Assert.Single(operations.Receipts).TargetProfileId);
    }

    [Fact]
    public async Task Transfer_close_and_reopen_retains_admission_and_original_source_target_and_keys() {
        var owner = new DataSourcesScenarioOwner(DataSourcesScenario.DelayedWrite);
        var operations = new DataSourceOperationLedger();
        using var session = new DataSourcesSession(owner, operations);
        await session.InitializeAsync();
        await session.SelectAsync(DataSourcesScenarioOwner.ProfileB);
        await session.OpenTransferAsync(DataSourcesScenarioOwner.ProfileB);
        var original = session.Transfer!;
        original.Select(DataSourcesScenarioOwner.PreferenceGroup, true);
        original.ReplaceExisting = false;
        var transfer = original.TransferAsync();
        session.CloseTransfer();
        await session.OpenTransferAsync(DataSourcesScenarioOwner.ProfileB);
        var successor = session.Transfer!;
        successor.Select(DataSourcesScenarioOwner.AgentsGroup, true);
        await successor.TransferAsync();
        Assert.Single(owner.Commands);
        owner.Release();
        await transfer;
        Assert.Same(successor, session.Transfer);
        Assert.Null(successor.Result);
        Assert.Equal(DataSourcesScenarioOwner.ProfileA, owner.LastTransfer!.SourceProfileId);
        Assert.Equal(DataSourcesScenarioOwner.ProfileB, owner.LastTransfer.TargetProfileId);
        Assert.Equal(DataSourcesScenarioOwner.PreferenceGroup, Assert.Single(owner.LastTransfer.Groups));
        Assert.False(owner.LastTransfer.ReplaceExisting);
        Assert.Equal(DataSourceOutcome.Confirmed, Assert.Single(operations.Receipts).Result!.Outcome);
    }

    [Fact]
    public async Task Transfer_partial_group_progress_stays_visible_and_is_not_a_global_rollback() {
        var owner = new DataSourcesScenarioOwner(DataSourcesScenario.PartialTransfer);
        var operations = new DataSourceOperationLedger();
        using var session = new DataSourcesSession(owner, operations);
        await session.InitializeAsync();
        await session.SelectAsync(DataSourcesScenarioOwner.ProfileB);
        await session.OpenTransferAsync(DataSourcesScenarioOwner.ProfileB);
        var transfer = session.Transfer!;
        transfer.Select(DataSourcesScenarioOwner.PreferenceGroup, true);
        transfer.Select(DataSourcesScenarioOwner.AgentsGroup, true);
        await transfer.TransferAsync();
        var result = Assert.Single(operations.Receipts).Result!;
        Assert.Equal(DataSourceOutcome.Partial, result.Outcome);
        Assert.Equal(1, result.Groups!.Sum(item => item.RecordsCopied));
        Assert.True(result.Groups[0].Success);
        Assert.False(result.Groups[1].Success);
        Assert.Same(result, transfer.Result);
        session.CloseTransfer();
        await session.OpenTransferAsync(DataSourcesScenarioOwner.ProfileB);
        Assert.True(operations.HasUnknown);
        Assert.False(session.Transfer!.CanTransfer);
    }

    [Fact]
    public async Task Transfer_retains_acknowledged_groups_when_follow_up_reads_fail() {
        var owner = new DataSourcesScenarioOwner(DataSourcesScenario.RefreshFailure);
        var operations = new DataSourceOperationLedger();
        using var session = new DataSourcesSession(owner, operations);
        await session.InitializeAsync();
        await session.OpenTransferAsync(DataSourcesScenarioOwner.ProfileA);
        var transfer = session.Transfer!;
        transfer.Select(DataSourcesScenarioOwner.PreferenceGroup, true);
        await transfer.TransferAsync();
        Assert.NotNull(transfer.Message);
        Assert.NotNull(transfer.Result);
        Assert.True(Assert.Single(transfer.Result.Groups!).Success);
        Assert.False(transfer.CanTransfer);
        Assert.Single(owner.Commands);
    }

    [Fact]
    public async Task Current_or_pending_runtime_cannot_be_deleted_and_absent_schema_never_allows_activation() {
        var owner = new DataSourcesScenarioOwner(DataSourcesScenario.PendingRestart);
        using var session = new DataSourcesSession(owner, new());
        await session.InitializeAsync();
        await session.ExecuteAsync(DataSourceAction.Delete);
        await session.SelectAsync(DataSourcesScenarioOwner.ProfileB);
        await session.ExecuteAsync(DataSourceAction.Delete);
        owner.SchemaRead = id => throw new IOException("Controlled schema failure.");
        await session.RefreshSelectedSchemaAsync();
        await session.ExecuteAsync(DataSourceAction.ActivateForRestart);
        Assert.Empty(owner.Commands);
        Assert.Equal(SchemaStatus.Unavailable, session.SelectedSchemaHealth!.Status);
    }

    [Theory]
    [InlineData(DataSourcesScenario.Locked)]
    [InlineData(DataSourcesScenario.Unavailable)]
    public async Task Locked_or_unobserved_runtime_refuses_every_handler_without_dispatch(DataSourcesScenario scenario) {
        var owner = new DataSourcesScenarioOwner(scenario);
        using var session = new DataSourcesSession(owner, new());
        await session.InitializeAsync();
        session.NewProfile();
        await session.SaveAsync();
        foreach (var action in Enum.GetValues<DataSourceAction>()) {
            await session.ExecuteAsync(action);
        }
        await session.OpenTransferAsync(DataSourcesScenarioOwner.ProfileA);
        Assert.Empty(owner.Commands);
        Assert.Null(session.Transfer);
    }

    [Fact]
    public async Task Source_health_failure_revokes_previously_available_preview_and_stale_keys() {
        var owner = new DataSourcesScenarioOwner();
        using var session = new DataSourcesSession(owner, new());
        await session.InitializeAsync();
        await session.OpenTransferAsync(DataSourcesScenarioOwner.ProfileA);
        var transfer = session.Transfer!;
        transfer.Select(DataSourcesScenarioOwner.PreferenceGroup, true);
        Assert.True(transfer.CanTransfer);
        owner.SchemaRead = id => throw new IOException("Unavailable source.");
        await transfer.RefreshAsync();
        await transfer.TransferAsync();
        Assert.Empty(transfer.Items);
        Assert.False(transfer.CanTransfer);
        Assert.Empty(owner.Commands);
    }
}
