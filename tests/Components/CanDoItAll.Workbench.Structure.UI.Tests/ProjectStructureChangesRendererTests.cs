using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Workbench.Structure.UI;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.Workbench.Structure.UI.Tests;

public sealed class ProjectStructureChangesRendererTests {
    [Fact]
    public async Task Quiet_state_does_not_render_a_floating_window() {
        await using var context = Context();
        var view = Presentation() with { Window = new() { IsVisible = false } };
        var rendered = context.Render<StructureChangesWindow>(parameters => parameters.Add(component => component.Presentation, view));
        Assert.Empty(rendered.FindComponents<CanvasFloatingWindow>());
    }

    [Fact]
    public async Task Update_and_refresh_intents_retain_the_rendered_context_and_multi_node_item() {
        await using var context = Context();
        var view = Presentation();
        var intents = new List<StructureChangesIntent>();
        var rendered = context.Render<StructureChangesWindow>(parameters => parameters
            .Add(component => component.Presentation, view).Add(component => component.Dispatch, intents.Add));
        Assert.Contains("Two changed nodes", rendered.Markup);
        Assert.Contains("Editor B", rendered.Markup);
        await rendered.InvokeAsync(() => rendered.Find("[data-testid='project-structure-change-7']").ClickAsync(new MouseEventArgs()));
        Assert.Equal(new StructureChangesRefreshIntent(view.ContextId, 7), Assert.Single(intents));
        Assert.Equal(new[] { "first", "second" }, view.Items[0].NodeIds);
        intents.Clear();
        await rendered.InvokeAsync(() => rendered.Find("[data-testid='project-structure-changes-refresh']").ClickAsync(new MouseEventArgs()));
        Assert.Equal(new StructureChangesRefreshIntent(view.ContextId), Assert.Single(intents));
    }

    [Fact]
    public async Task Busy_refresh_disables_actions_and_failure_is_visible_without_removing_history() {
        await using var context = Context();
        var view = Presentation() with { IsBusy = true, Message = "Read failed; current view retained." };
        var rendered = context.Render<StructureChangesWindow>(parameters => parameters.Add(component => component.Presentation, view));
        Assert.True(rendered.Find("[data-testid='project-structure-changes-refresh']").HasAttribute("disabled"));
        Assert.True(rendered.Find("[data-testid='project-structure-change-7']").HasAttribute("disabled"));
        Assert.Contains("Read failed; current view retained.", rendered.Markup);
        Assert.Contains("Two changed nodes", rendered.Markup);
    }

    private static StructureChangesPresentation Presentation() => new(Guid.NewGuid(), new() { IsVisible = true },
        [new(7, "Editor B", DateTimeOffset.UtcNow, "Two changed nodes", ["first", "second"])], true, false, null);

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
