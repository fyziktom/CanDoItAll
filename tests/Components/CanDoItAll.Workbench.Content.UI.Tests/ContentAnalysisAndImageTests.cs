using AngleSharp.Html.Dom;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Mermaid;
using CanDoItAll.Workbench.Content.UI.Analysis;
using CanDoItAll.Workbench.Content.UI.Generation;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkbenchContent;

public sealed class ContentAnalysisAndImageTests {
    private static readonly Guid ProviderId = Guid.Parse("f5b50eea-cc37-4e31-82a1-59a6e5280064");
    private static readonly ContentImageProvider Provider = new(ProviderId, "Source image profile", "Friendly default",
        [new("opaque-default", "Friendly default"), new("opaque-alternate", "Friendly alternate")], false);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Image_submission_is_frozen_and_retired_completion_cannot_change_successor(bool fail) {
        using var context = Context();
        var entered = new TaskCompletionSource<ContentImageDraft>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var cut = Image(context, async draft => {
            calls++;
            entered.TrySetResult(draft);
            await release.Task;
        });
        cut.Find("[data-testid='content-image-title']").Input("  Frozen title  ");
        cut.Find("[data-testid='content-image-prompt']").Input("  Exact raw prompt\nnext line  ");
        var pending = Submit(cut);
        var accepted = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cut.Find("[data-testid='content-image-title']").Input("Later typing");
        await Submit(cut);
        Assert.Equal(1, calls);
        Assert.Equal("  Frozen title  ", accepted.Title);
        Assert.Equal("  Exact raw prompt\nnext line  ", accepted.Prompt);
        Assert.Equal("opaque-alternate", accepted.Model);
        cut.Render(parameters => parameters.Add(component => component.State, Setup()).Add(component => component.InitialDraft,
            new("Successor title", "", "Successor prompt", ProviderId, "opaque-default")));
        try {
            Assert.False(cut.Find("[data-testid='content-image-submit']").HasAttribute("disabled"));
        } catch {
            release.TrySetResult();
            await pending;
            throw;
        }
        if (fail) {
            release.SetException(new IOException("Retired call outcome"));
        } else {
            release.SetResult();
        }
        await pending;
        Assert.Equal("Successor title", ((IHtmlInputElement)cut.Find("[data-testid='content-image-title']")).Value);
        Assert.False(cut.Find("[data-testid='content-image-submit']").HasAttribute("disabled"));
        Assert.DoesNotContain("request stopped", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_image_outcome_blocks_repeat_while_an_independent_instance_remains_editable() {
        using var context = Context();
        var calls = 0;
        var cut = Image(context, _ => {
            calls++;
            throw new IOException("Synthetic unknown outcome");
        });
        var neighbor = Image(context, _ => Task.CompletedTask);
        await Submit(cut);
        await Submit(cut);
        Assert.Equal(1, calls);
        Assert.True(cut.Find("[data-testid='content-image-submit']").HasAttribute("disabled"));
        Assert.False(neighbor.Find("[data-testid='content-image-submit']").HasAttribute("disabled"));
        Assert.Contains("Observe it before generating again", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Source_managed_models_show_labels_and_keep_opaque_values_without_custom_override() {
        using var context = Context();
        var requests = new List<ContentImageDraft>();
        var cut = Image(context, draft => {
            requests.Add(draft);
            return Task.CompletedTask;
        });
        var options = cut.FindAll("[data-testid='content-image-model'] option");
        Assert.Contains(options, option => option.TextContent == "Friendly alternate");
        Assert.Contains(options, option => option.TextContent == "Friendly default");
        Assert.DoesNotContain("Optional model override", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(options, option => option.TextContent.Contains("opaque-", StringComparison.Ordinal));
        foreach (var label in new[] { "Friendly default", "Friendly alternate" }) {
            cut.Find("[data-testid='content-image-model']").Change(options.Single(option => option.TextContent == label).GetAttribute("value"));
            await Submit(cut);
        }
        Assert.Equal(["opaque-default", "opaque-alternate"], requests.Select(request => request.Model));
    }

    [Theory]
    [InlineData(ContentConfirmationPhase.Loading)]
    [InlineData(ContentConfirmationPhase.Ready)]
    [InlineData(ContentConfirmationPhase.Submitting)]
    [InlineData(ContentConfirmationPhase.ObservationRequired)]
    [InlineData(ContentConfirmationPhase.Completed)]
    public void Transcript_confirmation_exposes_explicit_dispatch_state(ContentConfirmationPhase phase) {
        using var context = Context();
        var cut = context.Render<ContentTranscriptDialog>(parameters => parameters
            .Add(component => component.State, new(Guid.NewGuid(), "Synthetic transcript", ContentTranscriptAction.FindMyTasks,
                [new(ProviderId, "Safe provider")], ProviderId, "Prior provider", phase, "Native outcome"))
            .Add(component => component.Actions, new(_ => Task.CompletedTask, () => Task.CompletedTask, () => Task.CompletedTask)));
        Assert.Equal(phase != ContentConfirmationPhase.Ready, Button(cut, "Send request").HasAttribute("disabled"));
        Assert.Contains("sends transcript content", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("does not create tasks", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Native outcome", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ContentTranscriptAction.Summarize, "Summarize transcript")]
    [InlineData(ContentTranscriptAction.FindMyTasks, "Find my tasks")]
    [InlineData(ContentTranscriptAction.FindOthersDeliveries, "Find others' deliveries")]
    public async Task Transcript_cancel_never_dispatches(ContentTranscriptAction action, string label) {
        using var context = Context();
        var sends = 0;
        var closes = 0;
        var cut = context.Render<ContentTranscriptDialog>(parameters => parameters
            .Add(component => component.State, new(Guid.NewGuid(), "Transcript", action, [new(ProviderId, "Provider")], ProviderId, "", ContentConfirmationPhase.Ready))
            .Add(component => component.Actions, new(_ => Task.CompletedTask,
                () => {
                    sends++;
                    return Task.CompletedTask;
                }, () => {
                    closes++;
                    return Task.CompletedTask;
                })));
        Assert.Contains(label, cut.Find("[role='dialog']").GetAttribute("aria-label"), StringComparison.Ordinal);
        await cut.InvokeAsync(() => Button(cut, "Cancel").ClickAsync(new MouseEventArgs()));
        Assert.Equal(0, sends);
        Assert.Equal(1, closes);
    }

    [Fact]
    public async Task Summary_renders_every_row_status_and_separate_export_command() {
        using var context = Context();
        string[] statuses = ["Open", "Active", "Blocked", "Review", "Done", "Cancelled"];
        var rows = Enumerable.Range(0, 60).Select(index => new ContentProgressRow($"row:{index}", $"Exact row {index}", "Note", statuses[index % statuses.Length],
            "33%", index % 3, "7 Oct 2026 09:00", "7 Oct 2026 11:00")).ToArray();
        var exports = new List<string>();
        var cut = context.Render<ContentProgressSummaryDialog>(parameters => parameters
            .Add(component => component.State, new(Guid.NewGuid(), "Accepted hierarchy", rows, statuses, 10, 10, 10, 10, 0))
            .Add(component => component.Actions, new((_, _) => Task.CompletedTask,
                () => {
                    exports.Add("xlsx");
                    return Task.CompletedTask;
                }, () => {
                    exports.Add("mermaid");
                    return Task.CompletedTask;
                }, () => Task.CompletedTask)));
        Assert.Equal(60, cut.FindAll("[data-node-id]").Count);
        Assert.All(cut.FindAll("select"), select => Assert.Equal(statuses, select.QuerySelectorAll("option").Where(option => !string.IsNullOrEmpty(option.GetAttribute("value"))).Select(option => option.TextContent)));
        await cut.InvokeAsync(() => Button(cut, "Export XLSX").ClickAsync(new MouseEventArgs()));
        await cut.InvokeAsync(() => Button(cut, "Export Gantt").ClickAsync(new MouseEventArgs()));
        Assert.Equal(["xlsx", "mermaid"], exports);
    }

    [Fact]
    public void Legacy_mermaid_uses_the_real_strict_renderer_and_encodes_source() {
        using var context = Context();
        const string source = "flowchart LR\n A[\"<script>unsafe()</script>\"] --> B[Safe]";
        var cut = context.Render<ContentLegacyMermaidDialog>(parameters => parameters.Add(component => component.State, new("Legacy", source, "Flowchart", true)));
        var diagram = cut.FindComponent<MermaidDiagram>().Instance;
        Assert.Equal(source, diagram.Source);
        Assert.Equal("strict", diagram.Options.SecurityLevel);
        Assert.False(diagram.Options.HtmlLabels);
        Assert.Empty(cut.FindAll("script"));
        Assert.Equal(source, cut.Find("pre").TextContent);
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
    private static ContentImageSetup Setup() => new(Guid.NewGuid(), [Provider], false, false, true);
    private static IRenderedComponent<ContentImageDialog> Image(BunitContext context, Func<ContentImageDraft, Task> submit)
        => context.Render<ContentImageDialog>(parameters => parameters.Add(component => component.State, Setup())
            .Add(component => component.InitialDraft, new("Initial image", "Context", "Initial prompt", ProviderId, "opaque-alternate"))
            .Add(component => component.Submit, submit));
    private static Task Submit(IRenderedComponent<ContentImageDialog> cut)
        => cut.InvokeAsync(() => cut.Find("[data-testid='content-image-submit']").ClickAsync(new MouseEventArgs()));
    private static AngleSharp.Dom.IElement Button<T>(IRenderedComponent<T> cut, string text) where T : Microsoft.AspNetCore.Components.IComponent
        => cut.FindAll("button").Single(button => button.TextContent.Trim() == text);
}
