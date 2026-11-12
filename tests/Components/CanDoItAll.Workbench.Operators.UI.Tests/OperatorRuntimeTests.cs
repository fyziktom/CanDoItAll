using Bunit;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Workbench.Operators.UI.Runtime;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Workbench.Operators.UI.Tests;

public sealed class OperatorRuntimeTests {
    [Fact]
    public async Task Quick_actions_gate_direct_duplicates_and_cannot_release_a_new_opening() {
        await using var context = Context();
        var first = Quick();
        var next = Quick();
        var firstGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var nextGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var cut = context.Render<OperatorQuickActions>(parameters => parameters.Add(component => component.View, first)
            .Add(component => component.Execute, (int _) => {
                calls++;
                return firstGate.Task;
            }));
        var old = cut.FindComponents<Button>().Single(button => button.Instance.ChildContent is not null).Instance.Click;
        var pending = cut.InvokeAsync(() => old.InvokeAsync());
        await cut.InvokeAsync(() => old.InvokeAsync());
        Assert.Equal(1, calls);
        cut.Render(parameters => parameters.Add(component => component.View, next)
            .Add(component => component.Execute, (int _) => {
                calls++;
                return nextGate.Task;
            }));
        var current = cut.FindComponents<Button>().Single(button => button.Instance.ChildContent is not null).Instance.Click;
        var successor = cut.InvokeAsync(() => current.InvokeAsync());
        firstGate.SetResult();
        await pending;
        await cut.InvokeAsync(() => old.InvokeAsync());
        await cut.InvokeAsync(() => current.InvokeAsync());
        Assert.Equal(2, calls);
        Assert.True(cut.Find("[data-testid='run']").HasAttribute("disabled"));
        nextGate.SetResult();
        await successor;
    }

    [Fact]
    public async Task Approval_can_answer_only_once_with_the_reviewed_display() {
        await using var context = Context();
        var results = new List<bool>();
        var cut = context.Render<OperatorRuntimeApproval>(parameters => parameters.Add(component => component.DisplayName, "Original operation")
            .Add(component => component.Review, "Exact plan under original directory")
            .Add(component => component.Decide, (bool approved) => results.Add(approved)));
        var approve = cut.FindComponents<Button>().Single(button => button.Instance.Text == "Approve this launch").Instance.Click;
        var cancel = cut.FindComponents<Button>().Single(button => button.Instance.Text == "Cancel").Instance.Click;
        await cut.InvokeAsync(() => approve.InvokeAsync());
        await cut.InvokeAsync(() => cancel.InvokeAsync());
        Assert.Equal([true], results);
        Assert.Contains("Exact plan under original directory", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preview_stop_is_gated_and_old_completion_does_not_release_successor() {
        await using var context = Context();
        var first = Preview();
        var next = Preview();
        var firstGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var nextGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var cut = context.Render<OperatorWebPreview>(parameters => parameters.Add(component => component.View, first)
            .Add(component => component.Stop, () => {
                calls++;
                return firstGate.Task;
            }));
        EventCallback Stop() => cut.FindComponents<Button>().Single(button => button.Instance.Text == "Stop").Instance.Click;
        var old = Stop();
        var pending = cut.InvokeAsync(() => old.InvokeAsync());
        await cut.InvokeAsync(() => old.InvokeAsync());
        cut.Render(parameters => parameters.Add(component => component.View, next)
            .Add(component => component.Stop, () => {
                calls++;
                return nextGate.Task;
            }));
        var current = Stop();
        var successor = cut.InvokeAsync(() => current.InvokeAsync());
        firstGate.SetResult();
        await pending;
        await cut.InvokeAsync(() => old.InvokeAsync());
        await cut.InvokeAsync(() => current.InvokeAsync());
        Assert.Equal(2, calls);
        Assert.True(cut.Find("[data-testid='project-structure-web-preview-stop']").HasAttribute("disabled"));
        nextGate.SetResult();
        await successor;
    }

    [Fact]
    public async Task Two_real_previews_keep_size_state_and_embedding_policy_independent() {
        await using var context = Context();
        var left = context.Render<OperatorWebPreview>(parameters => parameters.Add(component => component.View, Preview()));
        var right = context.Render<OperatorWebPreview>(parameters => parameters.Add(component => component.View,
            Preview() with { CanStop = false, CanEmbed = false, Url = new("https://example.invalid/"), EmbedUnavailableReason = "Remote embedding denied" }));
        Assert.Single(left.FindComponents<EmbeddedBrowser>());
        Assert.Equal(EmbeddedBrowser.LocalApplicationSandbox, left.Find("iframe").GetAttribute("sandbox"));
        Assert.Empty(right.FindAll("iframe"));
        Assert.Empty(right.FindAll("[data-testid='project-structure-web-preview-stop']"));
        await left.InvokeAsync(() => left.Find("[data-testid='project-structure-dialog-size-toggle']").ClickAsync(new()));
        Assert.Equal("true", left.Find("[role='dialog']").GetAttribute("data-maximized"));
        Assert.Equal("false", right.Find("[role='dialog']").GetAttribute("data-maximized"));
        Assert.Equal("noopener noreferrer", left.Find("[data-testid='project-structure-web-preview-open-browser']").GetAttribute("rel"));
    }

    private static OperatorQuickActionsView Quick() => new(Guid.NewGuid(), "Runtime", "Script", "Choose", "Notes",
        [new(0, "Run", "Typed native plan", "play_arrow", OperatorActionTone.Primary, "run", false)]);
    private static OperatorWebPreviewView Preview() => new(Guid.NewGuid(), "Owned preview", "Runtime", new("http://localhost:63451/"),
        "Original notes", true, string.Empty, true, string.Empty, ProcessLabel: "Fixture process identity");
    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
