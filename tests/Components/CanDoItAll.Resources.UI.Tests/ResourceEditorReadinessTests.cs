using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Resources.UI;
using CanDoItAll.Resources.UiSandbox;

namespace CanDoItAll.Tests.Components.ResourcesUi;

public sealed class ResourceEditorReadinessTests {
    [Fact]
    public async Task Catalog_refresh_cannot_admit_a_filled_placeholder_while_exact_read_is_pending() {
        var store = new ResourceScenarioStore();
        using var controller = await ResourceRegistryTests.OpenAsync(store);
        var id = store.Records.Keys.Last();
        var gate = store.HoldNext(ResourceScenarioLane.EditorRead);
        var selection = controller.SelectAsync(id);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var placeholder = controller.Draft;
        await controller.RefreshAsync();
        placeholder.Editor.Name = "Plausible placeholder";
        placeholder.Editor.ProjectId = store.PrimaryProject.Id;
        placeholder.Editor.ExpectedProjectAdmission = store.PrimaryProject.Admission;
        placeholder.Editor.ConnectorPluginKey = ResourceConnectorPluginKeys.WebLink;
        placeholder.Editor.ConfigSchemaVersion = ResourceScenarioStore.SchemaVersion;
        placeholder.Configuration.SetText(ResourceConnectorFieldKeys.WebUrl, "https://fixture.test/");
        await controller.SaveAsync();
        await controller.DeleteAsync();
        Assert.Empty(store.Writes);
        Assert.Equal(ResourceViewAccess.Loading, controller.Access);
        Assert.Same(placeholder, controller.Draft);
        gate.Release();
        await selection;
        Assert.Equal(ResourceViewAccess.Ready, controller.Access);
        Assert.Equal(store.Records[id].Name, controller.Draft.Editor.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_exact_read_survives_catalog_refresh_and_same_identity_retries(bool initialRoute) {
        var store = new ResourceScenarioStore();
        using var controller = new ResourceRegistryController(store);
        if (!initialRoute) {
            await controller.LoadRouteAsync(store.Records.Keys.First(), null);
        }
        var id = store.Records.Keys.Last();
        var calls = 0;
        store.BeforeGet = _ => ++calls == 1 ? Task.FromException(new InvalidOperationException("Fixture read failure")) : Task.CompletedTask;
        if (initialRoute) {
            await controller.LoadRouteAsync(id, null);
        } else {
            await controller.SelectAsync(id);
        }
        Assert.Equal(ResourceViewAccess.Failed, controller.Access);
        var error = controller.Error;
        await controller.RefreshAsync();
        Assert.Equal(ResourceViewAccess.Failed, controller.Access);
        Assert.Equal(error, controller.Error);
        await controller.SelectAsync(id);
        Assert.Equal(2, calls);
        Assert.Equal(ResourceViewAccess.Ready, controller.Access);
        var acquired = controller.Draft;
        acquired.Editor.Name = "dirty";
        acquired.Configuration.SetText(ResourceScenarioStore.JsonField, "{ unfinished");
        await controller.SelectAsync(id);
        await controller.RefreshAsync();
        Assert.Equal(2, calls);
        Assert.Same(acquired, controller.Draft);
        Assert.Equal("dirty", acquired.Editor.Name);
        Assert.Equal("{ unfinished", acquired.Configuration.Text(ResourceScenarioStore.JsonField));
    }

    [Fact]
    public async Task Initial_catalog_failure_requires_exact_acquisition_after_references_recover() {
        var store = new ResourceScenarioStore { FailNextRead = true };
        using var controller = new ResourceRegistryController(store);
        var id = store.Records.Keys.First();
        await controller.LoadRouteAsync(id, null);
        await controller.RefreshAsync();
        Assert.Equal(ResourceViewAccess.Failed, controller.Access);
        await controller.DeleteAsync();
        Assert.Empty(store.Writes);
        await controller.SelectAsync(id);
        Assert.Equal(ResourceViewAccess.Ready, controller.Access);
        Assert.Equal(store.Records[id].Name, controller.Draft.Editor.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Superseded_exact_failure_and_disposal_cannot_change_successor_readiness(bool dispose) {
        var store = new ResourceScenarioStore();
        using var controller = await ResourceRegistryTests.OpenAsync(store);
        var gate = store.HoldNext(ResourceScenarioLane.EditorRead);
        var pending = controller.SelectAsync(store.Records.Keys.Last());
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await controller.SelectAsync(store.Records.Keys.First());
        var successor = controller.Draft;
        if (dispose) {
            controller.Dispose();
        }
        store.BeforeGet = _ => Task.FromException(new InvalidOperationException("Retired read failure"));
        gate.Release();
        await pending;
        Assert.Same(successor, controller.Draft);
        Assert.Equal(ResourceViewAccess.Ready, controller.Access);
    }

    [Fact]
    public async Task Missing_exact_identity_cannot_become_a_new_draft_after_catalog_recovery() {
        var store = new ResourceScenarioStore();
        using var controller = await ResourceRegistryTests.OpenAsync(store);
        var id = store.Records.Keys.Last();
        store.BeforeGet = value => {
            store.Records.Remove(value);
            return Task.CompletedTask;
        };
        await controller.SelectAsync(id);
        await controller.RefreshAsync();
        await controller.SaveAsync();
        await controller.DeleteAsync();
        Assert.Equal(ResourceViewAccess.Failed, controller.Access);
        Assert.Empty(store.Writes);
        Assert.Equal(id, controller.Draft.Editor.Id);
    }

    [Fact]
    public async Task Acquired_historical_editor_can_be_deleted_with_failed_optional_references() {
        var store = new ResourceScenarioStore(ResourceScenario.RetiredProject);
        using var controller = await ResourceRegistryTests.OpenAsync(store);
        var draft = controller.Draft;
        var gate = store.HoldNext(ResourceScenarioLane.References);
        var refresh = controller.RefreshAsync();
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        store.FailNextRead = true;
        gate.Release();
        await refresh;
        Assert.Same(draft, controller.Draft);
        Assert.NotEmpty(controller.ReferenceErrors);
        await controller.DeleteAsync();
        Assert.Equal(ResourceMutationKind.Delete, Assert.Single(store.Writes).Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_header_refresh_and_list_retry_respect_pending_or_failed_exact_editor(bool fail) {
        var store = new ResourceScenarioStore();
        using var registry = await ResourceRegistryTests.OpenAsync(store);
        using var context = ResourceSurfaceTests.Context();
        await using var browse = ResourceBrowseTests.Create(store, context);
        var cut = ResourceSurfaceTests.Render(context, registry, browse);
        var id = store.Records.Keys.Last();
        var gate = store.HoldNext(ResourceScenarioLane.EditorRead);
        var selection = cut.InvokeAsync(() => cut.FindAll(".cda-selection-list-item__button").Last().ClickAsync());
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (fail) {
            store.BeforeGet = _ => Task.FromException(new InvalidOperationException("Exact fixture failure"));
            gate.Release();
            await selection;
        }
        await cut.InvokeAsync(() => cut.FindComponents<PageHeaderActionButton>().Single(c => c.Instance.Label == "Refresh").Find("button").ClickAsync());
        Assert.Equal(fail ? ResourceViewAccess.Failed : ResourceViewAccess.Loading, registry.Access);
        Assert.True(cut.Find("[data-testid='resource-save-button']").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        Assert.Empty(store.Writes);
        store.BeforeGet = null;
        if (fail) {
            await cut.InvokeAsync(() => cut.FindAll(".cda-selection-list-item__button").Last().ClickAsync());
        } else {
            gate.Release();
            await selection;
        }
        Assert.Equal(store.Records[id].Name, registry.Draft.Editor.Name);
        Assert.Equal(ResourceViewAccess.Ready, registry.Access);
    }
}
