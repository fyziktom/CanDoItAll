using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Workbench.Structure.UI;

namespace CanDoItAll.Workbench.Structure.UI.Tests;

public sealed class StructureComposerLeaveGuardTests {
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task Only_an_explicit_discard_for_the_same_input_can_leave(bool discard, bool changed, bool expected) {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var request = new CanvasWorkbenchComposerCloseRequest(Guid.NewGuid(), 2, false, false);
        var current = request;
        var rendered = context.Render<StructureComposerLeaveGuard>(parameters => parameters
            .Add(component => component.IsOpen, true).Add(component => component.OpeningId, request.OpeningId)
            .Add(component => component.ReadCurrent, () => Task.FromResult<CanvasWorkbenchComposerCloseRequest?>(current))
            .Add(component => component.IsSameRoute, _ => false));
        Task<bool>? leaving = null;
        await rendered.InvokeAsync(() => { leaving = rendered.Instance.RequestLeaveAsync(request); });
        rendered.WaitForElement("[data-testid='structure-composer-dirty-close']");
        if (changed) {
            current = current with { Revision = 3 };
        }

        await rendered.Find(discard ? "[data-testid='structure-composer-dirty-discard']" : "[data-testid='structure-composer-dirty-continue']").ClickAsync(new());

        Assert.Equal(expected, await leaving!);
        Assert.Empty(rendered.FindAll("[data-testid='structure-composer-dirty-close']"));
    }

    [Fact]
    public async Task A_pristine_editor_can_close_but_a_comparison_or_pending_operation_requires_a_decision() {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var request = new CanvasWorkbenchComposerCloseRequest(Guid.NewGuid(), 0, false, false);
        var rendered = context.Render<StructureComposerLeaveGuard>(parameters => parameters
            .Add(component => component.IsOpen, true).Add(component => component.OpeningId, request.OpeningId)
            .Add(component => component.ReadCurrent, () => Task.FromResult<CanvasWorkbenchComposerCloseRequest?>(request))
            .Add(component => component.IsSameRoute, _ => false));

        Assert.True(await rendered.InvokeAsync(() => rendered.Instance.RequestLeaveAsync(request)));
        Task<bool>? leaving = null;
        await rendered.InvokeAsync(() => { leaving = rendered.Instance.RequestLeaveAsync(request with { HasPendingSubmission = true }); });
        rendered.WaitForElement("[data-testid='structure-composer-dirty-close']");
        await rendered.Find("[data-testid='structure-composer-dirty-continue']").ClickAsync(new());
        Assert.False(await leaving!);
    }
}
