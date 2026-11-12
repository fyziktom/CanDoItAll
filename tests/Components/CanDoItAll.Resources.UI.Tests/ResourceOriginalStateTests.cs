using Bunit;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Resources.UI;
using CanDoItAll.Resources.UiSandbox;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components.ResourcesUi;

public sealed class ResourceOriginalStateTests {
    [Fact]
    public async Task Accepted_base_advances_while_later_project_connector_and_raw_configuration_remain_in_the_same_draft() {
        var store = new ResourceScenarioStore();
        using var controller = await ResourceRegistryTests.OpenAsync(store);
        var draft = controller.Draft;
        var original = draft.Expected;
        var context = draft.EditContext;
        var gate = store.HoldNext(ResourceScenarioLane.Save);
        var save = controller.SaveAsync();
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await controller.ChangeProjectAsync(store.Projects[1].Id);
            await controller.ChangeConnectorAsync(ResourceScenarioStore.FixturePlugin);
            draft.Configuration.SetText(ResourceScenarioStore.JsonField, "{ unfinished");
            draft.Editor.ConfigJson = "{ later raw";
            draft.Editor.LocationOrIdentifier = "later location";
            Assert.Same(draft, controller.Draft);
            Assert.Equal(original, draft.Expected);
            Assert.True(controller.IsBusy);
        } finally {
            gate.Release();
        }
        await save;
        var receipt = Assert.Single(controller.Receipts);
        Assert.Equal(original, Assert.Single(store.SaveCommands).Expected);
        Assert.Equal(receipt.AcceptedSnapshot!.Expected, draft.Expected);
        Assert.NotEqual(original, draft.Expected);
        Assert.Same(context, draft.EditContext);
        Assert.Equal(store.Projects[1].Admission, draft.Editor.ExpectedProjectAdmission);
        Assert.Equal(ResourceScenarioStore.FixturePlugin, draft.Editor.ConnectorPluginKey);
        Assert.Equal("{ unfinished", draft.Configuration.Text(ResourceScenarioStore.JsonField));
        Assert.Equal("{ later raw", draft.Editor.ConfigJson);
        Assert.Equal("later location", draft.Editor.LocationOrIdentifier);
        draft.Configuration.SetText(ResourceScenarioStore.JsonField, "{}");
        draft.Editor.ConfigJson = "{}";
        await controller.SaveAsync();
        Assert.Equal(2, store.SaveCommands.Count);
        Assert.Equal(receipt.AcceptedSnapshot.Expected, store.SaveCommands[1].Expected);
        Assert.Equal(store.Projects[1].Admission, store.SaveCommands[1].TargetProject);
    }

    [Fact]
    public async Task Accepted_metadata_survives_a_refusal_shaped_local_projection_failure_and_explicit_reload_restores_submission() {
        var store = new ResourceScenarioStore();
        using var controller = await ResourceRegistryTests.OpenAsync(store);
        using var context = ResourceSurfaceTests.Context();
        await using var browse = ResourceBrowseTests.Create(store, context);
        var cut = ResourceSurfaceTests.Render(context, controller, browse);
        var draft = controller.Draft;
        var reads = store.ExactReadCount;
        store.FailNextEditorProjection = true;
        await cut.InvokeAsync(controller.SaveAsync);
        var receipt = Assert.Single(controller.Receipts);
        Assert.Equal(ResourceEffectState.CommittedWarning, receipt.State);
        Assert.Equal(receipt.AcceptedSnapshot!.Expected, draft.Expected);
        Assert.Same(draft, controller.Draft);
        Assert.Equal(reads, store.ExactReadCount);
        Assert.True(cut.Find("[data-testid='resource-save-button']").HasAttribute("disabled"));
        cut.Find("[data-testid='resource-name-input']").Input("still editable");
        Assert.Equal("still editable", draft.Editor.Name);
        await cut.InvokeAsync(controller.SaveAsync);
        Assert.Single(store.SaveCommands);
        await cut.InvokeAsync(controller.ReloadAsync);
        Assert.True(controller.CanSave);
        Assert.Equal(receipt.AcceptedSnapshot.Expected, controller.Draft.Expected);
        Assert.Equal(reads + 1, store.ExactReadCount);
        Assert.Single(store.SaveCommands);
    }

    [Fact]
    public async Task A_later_stored_edit_is_not_adopted_as_the_accepted_snapshot_or_as_the_next_expected_revision() {
        var store = new ResourceScenarioStore();
        using var controller = await ResourceRegistryTests.OpenAsync(store);
        var reads = store.ExactReadCount;
        controller.Draft.Editor.Name = "our submission";
        store.AfterSave = id => {
            store.Records[id].Name = "another writer";
            store.AdvanceRevision(id);
            return Task.CompletedTask;
        };
        await controller.SaveAsync();
        var receipt = Assert.Single(controller.Receipts);
        Assert.Equal("our submission", receipt.AcceptedSnapshot!.Values.Name);
        Assert.Equal("our submission", controller.Draft.Editor.Name);
        Assert.Equal(reads, store.ExactReadCount);
        store.AfterSave = null;
        controller.Draft.Editor.Name = "next edit";
        await controller.SaveAsync();
        Assert.Equal(ResourceEffectState.Refused, controller.Receipts.Last().State);
        Assert.Equal(receipt.AcceptedSnapshot.Expected, store.SaveCommands.Last().Expected);
        Assert.Equal("next edit", controller.Draft.Editor.Name);
        Assert.Single(store.Writes);
        await controller.ReloadAsync();
        Assert.Equal("another writer", controller.Draft.Editor.Name);
        Assert.NotEqual(receipt.AcceptedSnapshot.Expected, controller.Draft.Expected);
    }

    [Fact]
    public async Task New_draft_and_promotion_keep_their_opening_context_when_the_dataset_changes() {
        var store = new ResourceScenarioStore();
        using var registry = new ResourceRegistryController(store);
        await registry.LoadRouteAsync(null, store.PrimaryProject.Id);
        ResourceRegistryTests.Fill(registry);
        using var context = ResourceSurfaceTests.Context();
        await using var browse = await ResourceBrowseTests.OpenAsync(store, context);
        await ResourceBrowseTests.InvokeAsync(browse, store, 0);
        browse.Promotion!.ProjectId = store.PrimaryProject.Id;
        var expected = registry.Draft.Expected;
        var promotion = Assert.IsType<ResourcePromotionDraft>(browse.Promotion);
        store.Context = new(store.Context.DatabaseProfileId, new DatasetNamespaceId(Guid.NewGuid()));
        await registry.SaveAsync();
        await browse.SavePromotionAsync();
        Assert.Equal(expected, registry.Draft.Expected);
        Assert.Equal(expected!.Context, Assert.Single(store.SaveCommands).Expected.Context);
        Assert.Equal(promotion.Context, Assert.Single(browse.Promotions).Command.Context);
        Assert.Equal(ResourceEffectState.Refused, Assert.Single(registry.Receipts).State);
        Assert.Equal(ResourceEffectState.Refused, browse.Promotions[0].State);
        Assert.Empty(store.Writes);
        Assert.Empty(store.PromotionCommands);
        await registry.NewAsync();
        Assert.Equal(store.Context, registry.Draft.Expected!.Context);
        Assert.NotEqual(expected.ResourceId, registry.Draft.Expected.ResourceId);
    }

    [Fact]
    public async Task Oversized_unbound_metadata_can_be_deleted_by_its_exact_header_without_editor_projection_or_a_current_project() {
        var store = new ResourceScenarioStore(ResourceScenario.MetadataOnlyUnbound);
        using var registry = await ResourceRegistryTests.OpenAsync(store);
        using var context = ResourceSurfaceTests.Context();
        await using var browse = ResourceBrowseTests.Create(store, context);
        var cut = ResourceSurfaceTests.Render(context, registry, browse);
        var expected = Assert.IsType<ResourceMetadataExpectation.Existing>(registry.Draft.Expected);
        Assert.IsType<ResourceProjectBinding.RetainedUnbound>(expected.Project);
        Assert.Equal(ResourceMetadataReadOnlyReason.TooLarge, registry.Draft.ReadOnlyReason);
        Assert.Equal(0, store.ProjectionCount);
        Assert.False(registry.CanSave);
        Assert.True(registry.CanDelete);
        Assert.Single(cut.FindAll("[data-testid='resource-metadata-readonly']"));
        Assert.Empty(cut.FindAll("[data-testid='resource-name-input']"));
        await cut.InvokeAsync(registry.DeleteAsync);
        Assert.Equal(expected, Assert.Single(store.DeleteCommands).Expected);
        Assert.Equal(expected, Assert.Single(registry.Receipts).Deleted);
        Assert.False(store.Records.ContainsKey(expected.ResourceId));
        Assert.Equal(0, store.ProjectionCount);
        Assert.Null(registry.Draft.Editor.ExpectedProjectAdmission);
    }

    [Fact]
    public async Task Unknown_update_and_delete_remain_blocked_after_current_found_or_missing_observations() {
        foreach (var delete in new[] { false, true }) {
            var store = new ResourceScenarioStore(ResourceScenario.UnknownWrite);
            using var registry = await ResourceRegistryTests.OpenAsync(store);
            var draft = registry.Draft;
            var expected = draft.Expected;
            if (delete) {
                await registry.DeleteAsync();
            } else {
                await registry.SaveAsync();
            }
            var receipt = Assert.Single(registry.Receipts);
            await registry.ReviewAsync(receipt);
            Assert.Equal(ResourceEffectState.Unknown, receipt.State);
            Assert.Equal(expected, draft.Expected);
            Assert.True(registry.IsBusy);
            Assert.False(registry.CanSave);
            Assert.False(registry.CanDelete);
            Assert.True(registry.CanMutate);
            if (delete) {
                Assert.IsType<ResourceMetadataReadResult.Missing>(receipt.Observation);
            } else {
                Assert.IsType<ResourceMetadataReadResult.Found>(receipt.Observation);
            }
            await registry.ReloadAsync();
            await registry.SaveAsync();
            await registry.DeleteAsync();
            Assert.Same(draft, registry.Draft);
            Assert.Single(store.Writes);
        }
    }

    [Fact]
    public async Task Reused_promotion_reports_exact_existing_metadata_even_when_a_later_notification_fails() {
        var store = new ResourceScenarioStore();
        using var context = ResourceSurfaceTests.Context();
        await using var browse = await ResourceBrowseTests.OpenAsync(store, context);
        await ResourceBrowseTests.InvokeAsync(browse, store, 0);
        browse.Promotion!.ProjectId = store.PrimaryProject.Id;
        browse.Promotion.Name = "stored name";
        await browse.SavePromotionAsync();
        var original = Assert.Single(browse.Promotions).Snapshot!;
        await ResourceBrowseTests.InvokeAsync(browse, store, 0);
        browse.Promotion!.ProjectId = store.PrimaryProject.Id;
        browse.Promotion.Name = "requested replacement";
        browse.Promotion.Sensitivity = ResourceSensitivity.Sensitive;
        browse.Promoted = _ => throw new ResourceActionRefusedException("Synthetic notification refusal after accepted reuse.");
        await browse.SavePromotionAsync();
        var receipt = browse.Promotions.Last();
        Assert.IsType<ResourcePromotionObservation.Reused>(receipt.Observation);
        Assert.Equal(original, receipt.Snapshot);
        Assert.Equal("stored name", receipt.Snapshot!.Values.Name);
        Assert.NotEqual(receipt.Command.Sensitivity, receipt.Snapshot.Values.Sensitivity);
        Assert.Equal(ResourceEffectState.CommittedWarning, receipt.State);
        Assert.Equal(2, store.PromotionCommands.Count);
    }

    [Fact]
    public async Task Raw_metadata_bounds_refuse_before_owner_dispatch_without_replacing_the_draft() {
        var store = new ResourceScenarioStore();
        using var registry = await ResourceRegistryTests.OpenAsync(store);
        var draft = registry.Draft;
        var raw = new string('x', ResourceMetadataLimits.ConfigurationValueBytes + 1);
        draft.Configuration.SetText(ResourceConnectorFieldKeys.WebUrl, raw);
        await registry.SaveAsync();
        Assert.Same(draft, registry.Draft);
        Assert.Equal(raw, draft.Configuration.Text(ResourceConnectorFieldKeys.WebUrl));
        Assert.Empty(store.SaveCommands);
        Assert.Empty(registry.Receipts);
        Assert.Contains("bounds", Assert.IsType<string>(registry.Error), StringComparison.Ordinal);
    }
}
