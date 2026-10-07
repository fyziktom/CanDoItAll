using CanDoItAll.Modules.Resources;
using CanDoItAll.Resources.UI;
using CanDoItAll.Resources.UiSandbox;

namespace CanDoItAll.Tests.Components.ResourcesUi;

public sealed class ResourceRegistryTests {
    [Fact]
    public async Task Combined_route_mismatch_stays_failed_after_reference_refresh() {
        var store = new ResourceScenarioStore();
        using var controller = new ResourceRegistryController(store);
        await controller.LoadRouteAsync(store.Records.Keys.First(), store.Projects[1].Id);
        Assert.Equal(ResourceViewAccess.Failed, controller.Access);
        await controller.RefreshAsync();
        Assert.Equal(ResourceViewAccess.Failed, controller.Access);
        Assert.Null(controller.Draft.Editor.ExpectedProjectAdmission);
    }

    [Fact]
    public async Task Unknown_create_can_review_an_exact_candidate_without_replaying_or_inventing_commit_acknowledgement() {
        var store = new ResourceScenarioStore(ResourceScenario.UnknownWrite);
        using var controller = new ResourceRegistryController(store);
        await controller.LoadRouteAsync(null, store.PrimaryProject.Id);
        Fill(controller);
        await controller.SaveAsync();
        var receipt = Assert.Single(controller.Receipts);
        Assert.Null(receipt.ResourceId);
        var id = Assert.Single(store.Writes).ResourceId;
        await controller.ReviewIdentityAsync(receipt, id);
        Assert.Null(receipt.ResourceId);
        Assert.Null(controller.Draft.Editor.Id);
        Assert.Equal(ResourceEffectState.Unknown, receipt.State);
        Assert.Equal(id, receipt.ReviewedResource!.Id);
        await controller.SaveAsync();
        Assert.Single(store.Writes);
        await controller.SelectAsync(id);
        Assert.Equal(id, controller.Draft.Editor.Id);
    }
    [Fact]
    public async Task Save_captures_deep_command_and_preserves_later_raw_fields_and_edit_context() {
        var store = new ResourceScenarioStore();
        using var controller = await OpenAsync(store);
        var draft = controller.Draft;
        var context = draft.EditContext;
        draft.Editor.Name = " submitted ";
        draft.Configuration.SetText(ResourceConnectorFieldKeys.WebUrl, "https://captured.test/");
        var gate = store.HoldNext(ResourceScenarioLane.Save);
        var save = controller.SaveAsync();
        await gate.Entered.Task;
        draft.Editor.Name = "later";
        draft.Edited(ResourceEditorField.Name);
        draft.Configuration.SetText(ResourceConnectorFieldKeys.WebUrl, "https://later.test/");
        gate.Release();
        await save;
        Assert.Equal(" submitted ", Assert.Single(store.Writes).Command.Name);
        Assert.Equal("https://captured.test/", store.Writes[0].Command.Configuration.GetText(ResourceConnectorFieldKeys.WebUrl));
        Assert.Equal("later", draft.Editor.Name);
        Assert.Equal("https://later.test/", draft.Configuration.Text(ResourceConnectorFieldKeys.WebUrl));
        Assert.Same(context, controller.Draft.EditContext);
        Assert.Equal(ResourceEffectState.Committed, Assert.Single(controller.Receipts).State);
    }

    [Fact]
    public async Task Edit_away_and_back_preserves_raw_value_instead_of_owner_normalization() {
        var store = new ResourceScenarioStore();
        using var controller = await OpenAsync(store);
        var draft = controller.Draft;
        draft.Editor.Name = " raw ";
        var gate = store.HoldNext(ResourceScenarioLane.Save);
        var pending = controller.SaveAsync();
        draft.Editor.Name = "away";
        draft.Edited(ResourceEditorField.Name);
        draft.Editor.Name = " raw ";
        draft.Edited(ResourceEditorField.Name);
        gate.Release();
        await pending;
        Assert.Equal(" raw ", draft.Editor.Name);
        Assert.Equal("raw", store.Records[draft.Editor.Id!.Value].Name);
    }

    [Fact]
    public async Task Conflicting_direct_dispatch_is_blocked_but_an_independent_target_is_admitted() {
        var store = new ResourceScenarioStore();
        using var controller = await OpenAsync(store);
        var firstId = controller.Draft.Editor.Id;
        var gate = store.HoldNext(ResourceScenarioLane.Save);
        var save = controller.SaveAsync();
        await controller.SaveAsync();
        await controller.DeleteAsync();
        Assert.Single(controller.Receipts);
        await controller.SelectAsync(store.Records.Keys.Last());
        await controller.SaveAsync();
        gate.Release();
        await save;
        Assert.Equal(2, store.Writes.Count);
        Assert.NotEqual(firstId, controller.Draft.Editor.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Project_or_new_draft_lifetime_cannot_adopt_an_old_create(bool newDraft) {
        var store = new ResourceScenarioStore();
        using var controller = new ResourceRegistryController(store);
        await controller.LoadRouteAsync(null, store.PrimaryProject.Id);
        Fill(controller);
        var gate = store.HoldNext(ResourceScenarioLane.Save);
        var pending = controller.SaveAsync();
        if (newDraft) {
            await controller.NewAsync();
        }
        await controller.ChangeProjectAsync(store.Projects[1].Id);
        controller.Draft.Editor.Name = "successor";
        gate.Release();
        await pending;
        Assert.Null(controller.Draft.Editor.Id);
        Assert.Equal(store.Projects[1].Admission, controller.Draft.Editor.ExpectedProjectAdmission);
        Assert.Equal("successor", controller.Draft.Editor.Name);
        Assert.Equal(store.PrimaryProject.Admission, Assert.Single(store.Writes).Command.ExpectedProjectAdmission);
        Assert.NotNull(Assert.Single(controller.Receipts).ResourceId);
    }

    [Fact]
    public async Task No_op_project_and_party_read_do_not_release_write_gate_or_replace_draft() {
        var store = new ResourceScenarioStore();
        using var controller = await OpenAsync(store);
        var draft = controller.Draft;
        var gate = store.HoldNext(ResourceScenarioLane.Save);
        var pending = controller.SaveAsync();
        await controller.ChangeProjectAsync(store.PrimaryProject.Id);
        await controller.RefreshAsync();
        await controller.SaveAsync();
        Assert.Same(draft, controller.Draft);
        Assert.Single(controller.Receipts);
        gate.Release();
        await pending;
        Assert.Single(store.Writes);
    }

    [Theory]
    [InlineData(ResourceScenario.RefusedWrite, ResourceEffectState.Refused, 0)]
    [InlineData(ResourceScenario.CommittedWarning, ResourceEffectState.CommittedWarning, 1)]
    [InlineData(ResourceScenario.UnknownWrite, ResourceEffectState.Unknown, 1)]
    public async Task Owner_outcomes_are_distinct_and_refresh_never_replays(ResourceScenario scenario, ResourceEffectState state, int writes) {
        var store = new ResourceScenarioStore(scenario);
        using var controller = await OpenAsync(store);
        await controller.SaveAsync();
        var receipt = Assert.Single(controller.Receipts);
        Assert.Equal(state, receipt.State);
        await controller.RefreshAsync();
        Assert.Equal(writes, store.Writes.Count);
        Assert.Equal(state == ResourceEffectState.Unknown, controller.IsBusy);
    }

    [Fact]
    public async Task Confirmed_new_id_survives_a_missing_postcommit_read_and_next_save_uses_same_id() {
        var store = new ResourceScenarioStore();
        using var controller = new ResourceRegistryController(store);
        await controller.LoadRouteAsync(null, store.PrimaryProject.Id);
        Fill(controller);
        store.AfterSave = id => {
            store.Records.Remove(id);
            return Task.CompletedTask;
        };
        await controller.SaveAsync();
        var receipt = Assert.Single(controller.Receipts);
        Assert.Equal(ResourceEffectState.CommittedWarning, receipt.State);
        Assert.Equal(receipt.ResourceId, controller.Draft.Editor.Id);
        store.AfterSave = null;
        await controller.SaveAsync();
        Assert.Equal(ResourceEffectState.Refused, controller.Receipts.Last().State);
        Assert.Single(store.Writes);
    }

    [Fact]
    public async Task Newer_save_readback_cannot_be_overwritten_by_earlier_normalization() {
        var store = new ResourceScenarioStore();
        using var controller = await OpenAsync(store);
        var oldSnapshot = controller.Draft.Editor.Capture();
        var gate = store.HoldNext(ResourceScenarioLane.EditorRead);
        controller.Draft.Editor.Name = "old";
        var first = controller.SaveAsync();
        await gate.Entered.Task;
        controller.Draft.Editor.Name = "new";
        await controller.SaveAsync();
        store.BeforeGet = id => {
            store.Records[id] = oldSnapshot;
            return Task.CompletedTask;
        };
        gate.Release();
        await first;
        Assert.Equal("new", controller.Draft.Editor.Name);
        Assert.Equal(2, store.Writes.Count);
    }

    [Fact]
    public async Task Old_editor_read_and_aba_cannot_replace_successor_draft() {
        var store = new ResourceScenarioStore();
        using var controller = await OpenAsync(store);
        var firstId = store.Records.Keys.First();
        var secondId = store.Records.Keys.Last();
        var gate = store.HoldNext(ResourceScenarioLane.EditorRead);
        var oldRead = controller.SelectAsync(secondId);
        await gate.Entered.Task;
        await controller.SelectAsync(firstId);
        await controller.SelectAsync(secondId);
        var successor = controller.Draft;
        successor.Editor.Name = "do not replace";
        gate.Release();
        await oldRead;
        Assert.Same(successor, controller.Draft);
        Assert.Equal("do not replace", successor.Editor.Name);
    }

    [Fact]
    public async Task Failed_reference_read_keeps_ids_and_raw_configuration_without_fallback() {
        var store = new ResourceScenarioStore(ResourceScenario.UnavailableReferences);
        using var controller = await OpenAsync(store);
        var draft = controller.Draft;
        draft.Configuration.SetText(ResourceScenarioStore.JsonField, "{ unfinished");
        store.FailNextRead = true;
        await controller.RefreshAsync();
        Assert.Same(draft, controller.Draft);
        Assert.Equal(ResourceViewAccess.Failed, controller.Access);
        Assert.Equal(ResourceScenarioStore.FixtureId(300), draft.Editor.LinkedSecretId);
        Assert.Equal("{ unfinished", draft.Configuration.Text(ResourceScenarioStore.JsonField));
    }

    [Theory]
    [InlineData(ResourceScenario.MissingConnector)]
    [InlineData(ResourceScenario.InvalidFields)]
    public async Task Invalid_or_unavailable_schema_does_not_dispatch_or_discard_inputs(ResourceScenario scenario) {
        var store = new ResourceScenarioStore(scenario);
        using var controller = await OpenAsync(store);
        var original = controller.Draft.Editor.Capture();
        await controller.SaveAsync();
        Assert.Empty(store.Writes);
        Assert.Equal(original.ConnectorPluginKey, controller.Draft.Editor.ConnectorPluginKey);
        Assert.Equal(original.Configuration.ToJson(), controller.Draft.Editor.Configuration.ToJson());
        Assert.NotEmpty(controller.Draft.EditContext.GetValidationMessages());
    }

    [Fact]
    public async Task Current_project_filter_excludes_retired_lifetime_and_refuses_implicit_rebinding() {
        var store = new ResourceScenarioStore(ResourceScenario.RetiredProject);
        using var controller = await OpenAsync(store);
        Assert.Null(controller.SelectedProjectName);
        controller.ProjectFilter = store.PrimaryProject.Id;
        Assert.DoesNotContain(controller.FilteredResources, r => r.Id == controller.Draft.Editor.Id);
        await controller.SaveAsync();
        Assert.Equal(ResourceEffectState.Refused, Assert.Single(controller.Receipts).State);
        await controller.ChangeProjectAsync(store.PrimaryProject.Id);
        Assert.Equal(store.PrimaryProject.Admission, controller.Draft.Editor.ExpectedProjectAdmission);
    }

    [Fact]
    public async Task Unknown_review_is_single_flight_and_observation_cannot_finish_another_request() {
        var store = new ResourceScenarioStore(ResourceScenario.UnknownWrite);
        using var controller = await OpenAsync(store);
        await controller.SaveAsync();
        var receipt = Assert.Single(controller.Receipts);
        var gate = store.HoldNext(ResourceScenarioLane.EditorRead);
        var review = controller.ReviewAsync(receipt);
        await gate.Entered.Task;
        var reads = store.ReadCount;
        await controller.ReviewAsync(receipt);
        await controller.SaveAsync();
        Assert.Equal(reads, store.ReadCount);
        Assert.Single(store.Writes);
        await controller.NewAsync();
        var draft = controller.Draft;
        gate.Release();
        await review;
        Assert.Same(draft, controller.Draft);
        Assert.Equal(ResourceEffectState.Reviewed, receipt.State);
        Assert.Null(draft.Editor.Id);
    }

    [Fact]
    public async Task Dispose_does_not_claim_accepted_write_rolled_back_or_publish_it_into_a_new_controller() {
        var store = new ResourceScenarioStore();
        var controller = await OpenAsync(store);
        var gate = store.HoldNext(ResourceScenarioLane.Save);
        var pending = controller.SaveAsync();
        controller.Dispose();
        using var next = await OpenAsync(new ResourceScenarioStore());
        next.Draft.Editor.Name = "successor";
        gate.Release();
        await pending;
        Assert.Single(store.Writes);
        Assert.Equal(ResourceEffectState.Committed, Assert.Single(controller.Receipts).State);
        Assert.Equal("successor", next.Draft.Editor.Name);
    }

    internal static async Task<ResourceRegistryController> OpenAsync(ResourceScenarioStore store) {
        var controller = new ResourceRegistryController(store);
        await controller.LoadRouteAsync(store.Records.Keys.First(), null);
        return controller;
    }
    internal static void Fill(ResourceRegistryController controller) {
        controller.Draft.Editor.Name = "New resource";
        controller.Draft.Configuration.SetText(ResourceConnectorFieldKeys.WebUrl, "https://new.test/");
    }
}
