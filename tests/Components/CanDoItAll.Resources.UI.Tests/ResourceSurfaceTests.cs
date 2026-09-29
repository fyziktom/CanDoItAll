using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Configuration.UI;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Resources.UI;
using CanDoItAll.Resources.UiSandbox;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ResourcesUi;

public sealed class ResourceSurfaceTests {
    [Fact]
    public async Task Unblurred_fields_validation_and_edit_context_survive_tabs_and_reference_refresh() {
        var store = new ResourceScenarioStore(ResourceScenario.InvalidFields);
        using var registry = await ResourceRegistryTests.OpenAsync(store);
        using var context = Context();
        await using var browse = await ResourceBrowseTests.OpenAsync(store, context);
        var cut = Render(context, registry, browse);
        var draft = registry.Draft;
        cut.Find("[data-testid='resource-name-input']").Input(" raw unfinished name ");
        cut.Find("[data-testid='resource-primary-input']").Input("1e-");
        cut.Find($"[data-testid='resource-config-{ResourceScenarioStore.JsonField}']").Input("{ \"value\":");
        await cut.Find("form").SubmitAsync();
        var errors = draft.EditContext.GetValidationMessages().ToArray();
        Assert.NotEmpty(errors);
        cut.Find("[data-testid='resources-tab-browse']").Click();
        await cut.InvokeAsync(registry.RefreshAsync);
        cut.Find("[data-testid='resources-tab-registry']").Click();
        Assert.Same(draft.EditContext, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal(" raw unfinished name ", cut.Find("[data-testid='resource-name-input']").GetAttribute("value"));
        Assert.Equal("1e-", cut.Find("[data-testid='resource-primary-input']").GetAttribute("value"));
        Assert.Equal("{ \"value\":", draft.Configuration.Text(ResourceScenarioStore.JsonField));
        Assert.Equal(errors, draft.EditContext.GetValidationMessages());
        Assert.Empty(store.Writes);
    }

    [Fact]
    public async Task Save_dispatches_actual_preblur_input_and_keeps_same_form_after_owner_normalization() {
        var store = new ResourceScenarioStore();
        using var registry = await ResourceRegistryTests.OpenAsync(store);
        using var context = Context();
        await using var browse = ResourceBrowseTests.Create(store, context);
        var cut = Render(context, registry, browse);
        var form = cut.FindComponent<EditForm>().Instance.EditContext;
        cut.Find("[data-testid='resource-name-input']").Input(" current input ");
        cut.Find("[data-testid='resource-primary-input']").Input("https://unblurred.test/");
        await cut.Find("form").SubmitAsync();
        var command = Assert.Single(store.Writes).Command;
        Assert.Equal(" current input ", command.Name);
        Assert.Equal("https://unblurred.test/", command.Configuration.GetText(ResourceConnectorFieldKeys.WebUrl));
        Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("current input", registry.Draft.Editor.Name);
    }

    [Theory]
    [InlineData(ResourceScenario.UnavailableReferences, "Unavailable secret", "Unavailable party")]
    [InlineData(ResourceScenario.MissingConnector, "Unavailable connector", ResourceScenarioStore.MissingPlugin)]
    [InlineData(ResourceScenario.RetiredProject, "loaded project lifetime is unavailable", "admission is retained")]
    public async Task Missing_exact_references_are_visible_without_inventing_a_selection(ResourceScenario scenario, string first, string second) {
        var store = new ResourceScenarioStore(scenario);
        using var registry = await ResourceRegistryTests.OpenAsync(store);
        using var context = Context();
        await using var browse = ResourceBrowseTests.Create(store, context);
        var cut = Render(context, registry, browse);
        Assert.Contains(first, cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(second, cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(store.Records.Values.First().ExpectedProjectAdmission, registry.Draft.Editor.ExpectedProjectAdmission);
    }

    [Fact]
    public async Task Filtered_empty_is_distinct_from_empty_and_does_not_reset_editor() {
        var store = new ResourceScenarioStore();
        using var registry = await ResourceRegistryTests.OpenAsync(store);
        using var context = Context();
        await using var browse = ResourceBrowseTests.Create(store, context);
        var cut = Render(context, registry, browse);
        var draft = registry.Draft;
        cut.Find("input[placeholder='Search resources']").Input("no matching resource");
        Assert.Contains("No resources match the filters", cut.Markup, StringComparison.Ordinal);
        Assert.Same(draft, registry.Draft);
        Assert.Equal(3, registry.Resources.Count);
    }

    [Fact]
    public async Task Real_browser_promotion_and_readonly_interaction_share_the_same_production_renderer() {
        var store = new ResourceScenarioStore();
        using var registry = await ResourceRegistryTests.OpenAsync(store);
        using var context = Context();
        await using var browse = await ResourceBrowseTests.OpenAsync(store, context);
        browse.Promoted = _ => registry.RefreshAsync();
        var cut = Render(context, registry, browse);
        cut.Find("[data-testid='resources-tab-browse']").Click();
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".ft-file-browser__item-main").Count));
        foreach (var sourceClass in Enum.GetValues<ResourceFileSourceClass>()) {
            Assert.NotNull(cut.Find($"[data-testid='resources-source-group-{sourceClass.ToString().ToLowerInvariant()}']"));
        }
        await cut.Find(".ft-file-browser__item-main").DoubleClickAsync();
        cut.Find("[data-testid='resources-promotion-name']").Input("Unblurred promotion name");
        await cut.Find("[data-testid='resources-promotion-save']").ClickAsync();
        var receipt = Assert.Single(browse.Promotions);
        Assert.Equal("Unblurred promotion name", Assert.Single(store.PromotionCommands).Name);
        Assert.Equal(4, registry.Resources.Count);
        await cut.Find("[data-testid='resources-open-stored-object']").ClickAsync();
        var interaction = cut.FindComponent<FileInteraction>();
        Assert.False(interaction.Instance.AllowModeSwitch);
        Assert.Equal(16 * 1024 * 1024, interaction.Instance.MaximumContentBytes);
        Assert.Equal(receipt.Observation!.ResourceId, browse.Preview!.ResourceId);
        cut.WaitForAssertion(() => Assert.Contains("Owned synthetic Resources content", cut.Markup, StringComparison.Ordinal));
        await cut.InvokeAsync(() => registry.SelectAsync(receipt.Observation.ResourceId));
        cut.Find("[data-testid='resources-tab-registry']").Click();
        Assert.NotNull(cut.Find("[data-testid='resource-governed-storage-object']"));
        Assert.Empty(cut.FindComponents<EditForm>());
    }

    [Fact]
    public async Task Actual_host_download_action_streams_bounded_fixture_and_releases_authorization() {
        var store = new ResourceScenarioStore();
        using var registry = await ResourceRegistryTests.OpenAsync(store);
        using var context = Context();
        await using var browse = await ResourceBrowseTests.OpenAsync(store, context);
        var cut = Render(context, registry, browse);
        cut.Find("[data-testid='resources-tab-browse']").Click();
        await cut.WaitForElement("tr[data-item-key] .ft-file-browser__action-menu-button").ClickAsync();
        cut.WaitForAssertion(() => Assert.Contains("Download", string.Join("|", cut.FindAll(".ft-file-browser__action-item").Select(button => button.TextContent.Trim())), StringComparison.Ordinal));
        var download = cut.FindAll(".ft-file-browser__action-item").Single(button => button.TextContent.Trim() == "Download");
        await download.ClickAsync();
        var receipt = Assert.Single(browse.Actions);
        Assert.Equal(ResourceEffectState.Committed, receipt.State);
        Assert.Equal(store.Sources[0].Key, receipt.Selection.Source.Key);
        Assert.Equal(1, store.DownloadReleaseCount);
        Assert.Contains(context.JSInterop.Invocations, invocation => invocation.Identifier == "downloadFileFromStream");
    }

    [Fact]
    public async Task Large_browser_keeps_page_size_bounded_and_uses_real_paging() {
        var store = new ResourceScenarioStore(ResourceScenario.Large);
        using var registry = await ResourceRegistryTests.OpenAsync(store);
        using var context = Context();
        await using var browse = await ResourceBrowseTests.OpenAsync(store, context);
        var cut = Render(context, registry, browse);
        cut.Find("[data-testid='resources-tab-browse']").Click();
        cut.WaitForAssertion(() => Assert.Equal(50, cut.FindAll(".ft-file-browser__item-main").Count));
        Assert.Single(cut.FindComponents<FileBrowser>());
        Assert.Equal(200, registry.Resources.Count);
        cut.Find("input[placeholder='Search files and folders']").Input("report-005");
        cut.WaitForAssertion(() => Assert.Contains("report-005.txt", Assert.Single(cut.FindAll(".ft-file-browser__item-main")).TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void Shared_field_renderer_retains_invalid_raw_number_json_and_unavailable_secret_across_remount() {
        using var context = Context();
        var state = new CanDoItAll.SharedKernel.Configuration.ConfigurationState();
        var draft = new ConfigurationInputDraft(state);
        var field = new CanDoItAll.SharedKernel.Configuration.ConfigurationFieldDescriptor(ResourceScenarioStore.NumberField,
            "Number", CanDoItAll.SharedKernel.Configuration.ConfigurationFieldType.Number, false, "");
        var cut = context.Render<ConnectorConfigFieldEditor>(p => p.Add(c => c.Field, field).Add(c => c.State, state).Add(c => c.Draft, draft));
        cut.Find("input").Input(" 1e- ");
        cut.Dispose();
        var remounted = context.Render<ConnectorConfigFieldEditor>(p => p.Add(c => c.Field, field).Add(c => c.State, state).Add(c => c.Draft, draft));
        Assert.Equal(" 1e- ", remounted.Find("input").GetAttribute("value"));
        Assert.Equal("1e-", state.GetText(ResourceScenarioStore.NumberField));
        Assert.Single(new CanDoItAll.SharedKernel.Configuration.ConfigurationSchemaValidator().Validate(new("1", [field]), state).Issues);
    }

    internal static BunitContext Context() {
        var context = ResourceBrowseTests.Context();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton(new FileInteractionComponentBuilder().AddBuiltIns().Build());
        return context;
    }
    internal static IRenderedComponent<ResourcesWorkspaceSurface> Render(BunitContext context, ResourceRegistryController registry, ResourceBrowseController browse) =>
        context.Render<ResourcesWorkspaceSurface>(p => p.Add(c => c.Registry, registry).Add(c => c.Browse, browse));
}
