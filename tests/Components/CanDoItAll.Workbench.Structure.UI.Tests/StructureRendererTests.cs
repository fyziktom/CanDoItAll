using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Workbench.Structure.UI;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.Workbench.Structure.UI.Tests;

public sealed class StructureRendererTests {
    [Fact]
    public async Task Canvas_and_toolbox_apply_the_current_snapshot_in_the_same_render() {
        await using var context = Context();
        var first = Presentation("Original") with { Toolbox = new() { State = new() { IsVisible = false } } };
        var rendered = context.Render<StructureWorkspace>(parameters => parameters.Add(component => component.Presentation, first));
        var originalCanvas = rendered.FindComponent<CanvasWorkbench>().Instance;
        var updated = first with {
            Canvas = new() { SurfaceId = first.Canvas!.SurfaceId, Mode = "dependency", Nodes = [new() { Id = "root", Title = "Updated" }] },
            Toolbox = new() { State = new() { IsVisible = true }, SourceLabel = "Updated" }
        };
        rendered.Render(parameters => parameters.Add(component => component.Presentation, updated));
        Assert.Same(originalCanvas, rendered.FindComponent<CanvasWorkbench>().Instance);
        Assert.Equal("dependency", originalCanvas.Surface.Mode);
        Assert.Single(rendered.FindComponents<ProjectStructureToolboxWindow>());
        Assert.Contains("Updated", rendered.Markup);
    }

    [Fact]
    public async Task Real_canvas_and_toolbox_render_without_native_services_and_keep_independent_receivers() {
        await using var context = Context();
        var first = Presentation("First");
        var second = Presentation("Second");
        var leftCommands = new List<StructureIntent>();
        var rightCommands = new List<StructureIntent>();
        var left = context.Render<StructureWorkspace>(parameters => parameters
            .Add(component => component.Presentation, first).Add(component => component.Dispatch, leftCommands.Add));
        var right = context.Render<StructureWorkspace>(parameters => parameters
            .Add(component => component.Presentation, second).Add(component => component.Dispatch, rightCommands.Add));
        Assert.Single(left.FindComponents<CanvasWorkbench>());
        Assert.Single(right.FindComponents<ProjectStructureToolboxWindow>());
        Assert.Contains("First", left.Markup);
        Assert.Contains("Second", right.Markup);
        await left.InvokeAsync(() => left.FindComponent<ProjectStructureToolbarActions>().Instance.ToggleSignalsWindow.InvokeAsync());
        Assert.Equal(new StructureToolbarIntent(first.ContextId, StructureToolbarCommand.Signals), Assert.Single(leftCommands));
        Assert.Empty(rightCommands);
        Assert.DoesNotContain(typeof(StructureWorkspace).Assembly.GetReferencedAssemblies(), reference =>
            reference.Name!.StartsWith("CanDoItAll.Modules.", StringComparison.Ordinal) ||
            reference.Name.StartsWith("CanDoItAll.AgentFramework.", StringComparison.Ordinal) ||
            reference.Name.StartsWith("CanDoItAll.Infrastructure", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Retired_toolbar_callback_retains_original_context_after_A_B_A() {
        await using var context = Context();
        var commands = new List<StructureIntent>();
        var first = Presentation("A");
        var rendered = context.Render<StructureWorkspace>(parameters => parameters
            .Add(component => component.Presentation, first).Add(component => component.Dispatch, commands.Add));
        var retired = rendered.FindComponent<ProjectStructureToolbarActions>().Instance.ToggleToolboxWindow;
        rendered.Render(parameters => parameters.Add(component => component.Presentation, Presentation("B")));
        var reopened = Presentation("A");
        rendered.Render(parameters => parameters.Add(component => component.Presentation, reopened));
        await rendered.InvokeAsync(() => retired.InvokeAsync());
        Assert.Equal(first.ContextId, Assert.Single(commands).ContextId);
        Assert.NotEqual(reopened.ContextId, commands[0].ContextId);
    }

    [Fact]
    public async Task Structural_callbacks_keep_the_original_opening_without_changing_a_successor() {
        await using var context = Context();
        var commands = new List<StructureDialogIntent>();
        var original = Hierarchy();
        var rendered = context.Render<StructureStructuralDialogs>(parameters => parameters
            .Add(component => component.Presentation, new(original)).Add(component => component.Dispatch, commands.Add));
        var close = rendered.Instance.CloseProjectHierarchy;
        var submit = rendered.Instance.ExecuteProjectHierarchyCommand;
        var successor = Hierarchy() with { Error = "Successor validation", SelectedProjectId = Guid.NewGuid() };
        rendered.Render(parameters => parameters.Add(component => component.Presentation, new(successor)));
        await rendered.InvokeAsync(() => close.InvokeAsync());
        await rendered.InvokeAsync(() => submit.InvokeAsync());
        Assert.Equal(2, commands.Count);
        Assert.All(commands, intent => Assert.Equal(original.OpeningId, intent.OpeningId));
        Assert.Same(successor, rendered.Instance.ProjectHierarchyDialog);
        Assert.Contains("Successor validation", rendered.Markup);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Pending_or_unknown_hierarchy_disables_mutation_but_preserves_close(bool busy, bool unknown) {
        await using var context = Context();
        var commands = new List<StructureDialogIntent>();
        var hierarchy = Hierarchy() with { IsBusy = busy, RequiresObservation = unknown };
        var rendered = context.Render<StructureStructuralDialogs>(parameters => parameters
            .Add(component => component.Presentation, new(hierarchy)).Add(component => component.Dispatch, commands.Add));
        Assert.True(rendered.Find("[data-testid='project-structure-hierarchy-project-select']").HasAttribute("disabled"));
        Assert.True(rendered.Find("[data-testid='project-structure-hierarchy-submit']").HasAttribute("disabled"));
        await rendered.InvokeAsync(() => rendered.Instance.CloseProjectHierarchy.InvokeAsync());
        Assert.Equal(new StructureDialogCommand(hierarchy.OpeningId, StructureDialogOperation.CloseHierarchy), Assert.Single(commands));
    }

    [Fact]
    public async Task Failed_load_has_explicit_retry_and_does_not_mount_an_editable_canvas() {
        await using var context = Context();
        var commands = new List<StructureIntent>();
        var presentation = Presentation("Unavailable read") with { Error = "Read failed" };
        var rendered = context.Render<StructureWorkspace>(parameters => parameters
            .Add(component => component.Presentation, presentation).Add(component => component.Dispatch, commands.Add));
        Assert.Empty(rendered.FindComponents<CanvasWorkbench>());
        await rendered.InvokeAsync(() => rendered.Find("[data-testid='structure-load-retry']").ClickAsync(new MouseEventArgs()));
        Assert.Equal(new StructureToolbarIntent(presentation.ContextId, StructureToolbarCommand.Retry), Assert.Single(commands));
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static StructurePresentation Presentation(string title) => new(Guid.NewGuid(),
        SkeletonStateOverlayFactory.CreateLoadingSnapshot("Loading", "Loading the canvas")) {
        Canvas = new() { SurfaceId = Guid.NewGuid().ToString("N"), Nodes = [new() { Id = "root", Title = title }] },
        Toolbox = new() { State = new() { IsVisible = true }, SourceLabel = title }
    };

    private static StructureHierarchyPresentation Hierarchy() => new(Guid.NewGuid(), "Add subproject", "Select the child project", "Add subproject", "",
        [new(Guid.NewGuid(), "Child")], null, "", false, false);
}
