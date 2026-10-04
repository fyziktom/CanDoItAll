using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Avatars;
using CanDoItAll.AgentFramework.UI.Chat;
using CanDoItAll.AgentFramework.UI.Runtime;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentsAdjunctSurfaceTests {
    [Theory]
    [InlineData(AgentChatAction.CancelPendingRun)]
    [InlineData(AgentChatAction.NewThread)]
    [InlineData(AgentChatAction.OpenRuntime)]
    public async Task Queued_chat_actions_keep_their_original_run_session_and_generation(AgentChatAction action) {
        using var context = Context();
        var original = new AgentChatActionOrigin(1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var state = new AgentChatActionState(original, null, false, false, true, false, true, false, true);
        var intents = new List<AgentChatActionIntent>();
        var cut = context.Render<AgentChatActionsSurface>(p => p.Add(c => c.State, state).Add(c => c.Intent, intents.Add));
        var index = action switch {
            AgentChatAction.CancelPendingRun => 0,
            AgentChatAction.NewThread => 1,
            _ => 2
        };
        var queued = cut.FindComponents<Button>()[index].Instance.Click;
        cut.Render(p => p.Add(c => c.State, state with { Origin = original with { Generation = 3, RunId = Guid.NewGuid() } }));
        await cut.InvokeAsync(() => queued.InvokeAsync());
        Assert.Equal(new(original, action), Assert.Single(intents));
    }

    [Fact]
    public async Task Context_and_close_callbacks_keep_the_opening_handle_and_revision() {
        using var context = Context();
        var origin = new AgentConversationContextOrigin(Guid.NewGuid(), new(Guid.NewGuid()), Guid.NewGuid(), null, null);
        var intents = new List<AgentConversationContextIntent>();
        var cut = context.Render<AgentConversationContextSurface>(p => p
            .Add(c => c.State, new(origin, "First source", true, false, "Following first source", "Next turn: second source"))
            .Add(c => c.Intent, intents.Add).AddChildContent("Owned conversation"));
        var queued = cut.FindComponent<Button>().Instance.Click;
        cut.Render(p => p.Add(c => c.State, new(origin with { Activation = Guid.NewGuid() }, "Second source", false, true, "Detached", null)));
        await cut.InvokeAsync(() => queued.InvokeAsync());
        Assert.Equal(new(origin, AgentConversationContextMode.Detached), Assert.Single(intents));
        Assert.Contains("Owned conversation", cut.Markup, StringComparison.Ordinal);
        var decisions = new List<AgentFloatingCloseIntent>();
        var close = context.Render<AgentFloatingCloseSurface>(p => p.Add(c => c.State, new(origin.Activation, origin.HandleId, "Same name", true, true))
            .Add(c => c.Intent, decisions.Add));
        Assert.Contains("does not reject the durable pending approval", close.Markup, StringComparison.Ordinal);
        var stop = close.FindComponents<Button>().Single(button => button.Instance.Text == "Stop chat").Instance.Click;
        close.Render(p => p.Add(c => c.State, new(Guid.NewGuid(), new(Guid.NewGuid()), "Same name", false, false)));
        await close.InvokeAsync(() => stop.InvokeAsync());
        Assert.Equal(new(origin.Activation, origin.HandleId, AgentFloatingCloseChoice.Stop), Assert.Single(decisions));
        Assert.True(close.Find("[data-testid='floating-agent-chat-stop']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Input_file_retains_the_selection_generation_and_does_not_reconstruct_bytes() {
        using var context = Context();
        var selections = new List<AgentImageSelection>();
        var cut = context.Render<AgentImageAttachmentPicker>(p => p.Add(c => c.Generation, 17).Add(c => c.Selected, selections.Add));
        var original = cut.FindComponent<InputFile>().Instance.OnChange;
        var file = new TestImage();
        cut.Render(p => p.Add(c => c.Generation, 19));
        var args = new InputFileChangeEventArgs([file]);
        await cut.InvokeAsync(() => original.InvokeAsync(args));
        var selection = Assert.Single(selections);
        Assert.Equal(17, selection.Generation);
        Assert.Same(file, selection.Files.File);
        Assert.Equal(0, file.OpenCount);
        Assert.Null(cut.Find("input").GetAttribute("tabindex"));
        Assert.Equal("file", cut.Find("input").GetAttribute("type"));
    }

    [Fact]
    public void Log_highlight_and_copy_use_the_exact_visible_safe_entry() {
        using var context = Context();
        var entry = new AgentExecutionStepPresentation(Guid.NewGuid(), Guid.NewGuid(), "Completed", "success", "Write",
            "2026-10-04 12:00:00 UTC", "Safe <encoded> path; secret=[REDACTED]");
        var cut = context.Render<AgentExecutionLogSurface>(p => p.Add(c => c.State,
            new("Same label", "Saved thread", "Same provider", "Completed", "success", "2s", entry.Id, [entry])));
        Assert.Equal(entry.Message, cut.FindComponent<CopyButton>().Instance.Value);
        Assert.Equal(entry.Message, cut.Find("pre").TextContent);
        Assert.Equal(entry.RunId.ToString(), cut.Find("article").GetAttribute("data-run-id"));
        Assert.Contains("agent-execution-log-dialog-entry--highlight", cut.Find("article").ClassList);
        Assert.Empty(cut.FindAll("encoded"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Closing_or_retiring_an_avatar_attempt_cancels_only_its_request_and_suppresses_late_success(bool retire) {
        using var context = Context();
        using var owner = new CancellationTokenSource();
        var pending = new TaskCompletionSource<AvatarGenerationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken captured = default;
        AvatarGenerationRequest? submitted = null;
        var source = new AvatarGenerationSource(Guid.NewGuid(), "Same label", "opaque/image:1");
        var operations = new AvatarPickerOperations(_ => Task.FromResult<AvatarGenerationSource?>(source), (request, token) => {
            submitted = request;
            captured = token;
            return pending.Task;
        }, (_, _) => throw new InvalidOperationException());
        var values = new List<string>();
        var notices = new List<AvatarPickerNotice>();
        var cut = context.Render<AvatarPickerSurface>(p => p.Add(c => c.Operations, operations).Add(c => c.OwnerLifetime, owner.Token)
            .Add(c => c.EntityName, "Original owner").Add(c => c.ValueChanged, values.Add).Add(c => c.Notice, notices.Add));
        await cut.Find("[data-testid='avatar-picker-open']").ClickAsync();
        var generate = cut.Find("[data-testid='avatar-picker-ai-generate']").ClickAsync();
        cut.WaitForAssertion(() => Assert.NotNull(submitted));
        Assert.Same(source, submitted!.Source);
        Assert.Contains("Original owner", submitted.VisualBrief, StringComparison.Ordinal);
        Assert.True(cut.Find("input[type='file']").HasAttribute("disabled"));
        if (retire) {
            owner.Cancel();
        } else {
            await cut.Find("[data-testid='avatar-picker-close']").ClickAsync();
            await cut.Find("[data-testid='avatar-picker-open']").ClickAsync();
        }
        Assert.True(captured.IsCancellationRequested);
        Assert.True(captured.WaitHandle.WaitOne(0));
        pending.SetResult(new("data:image/png;base64,c2FtcGxl"));
        await generate;
        Assert.Empty(values);
        Assert.Empty(notices);
        Assert.Throws<ObjectDisposedException>(() => captured.WaitHandle);
        Assert.False(owner.IsCancellationRequested && !retire);
    }

    [Fact]
    public async Task Avatar_submissions_retain_the_original_callback_and_source_despite_rerender() {
        using var context = Context();
        var pending = new TaskCompletionSource<AvatarGenerationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new AvatarGenerationSource(Guid.NewGuid(), "Fixture", "opaque/original");
        var operations = new AvatarPickerOperations(_ => Task.FromResult<AvatarGenerationSource?>(source), (_, _) => pending.Task,
            (_, _) => throw new InvalidOperationException());
        var original = new List<string>();
        var successor = new List<string>();
        var cut = context.Render<AvatarPickerSurface>(p => p.Add(c => c.Operations, operations).Add(c => c.ValueChanged, original.Add));
        await cut.Find("[data-testid='avatar-picker-open']").ClickAsync();
        var generate = cut.Find("[data-testid='avatar-picker-ai-generate']").ClickAsync();
        cut.Render(p => p.Add(c => c.ValueChanged, successor.Add).Add(c => c.GenerationSource, source with { ProviderProfileId = Guid.NewGuid() }));
        pending.SetResult(new("captured result"));
        await generate;
        Assert.Equal("captured result", Assert.Single(original));
        Assert.Empty(successor);
    }

    [Fact]
    public async Task Simultaneous_avatar_forms_have_distinct_label_targets() {
        using var context = Context();
        var operations = new AvatarPickerOperations(_ => Task.FromResult<AvatarGenerationSource?>(null),
            (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
        var first = context.Render<AvatarPickerSurface>(p => p.Add(c => c.Operations, operations));
        var second = context.Render<AvatarPickerSurface>(p => p.Add(c => c.Operations, operations));
        await first.Find("[data-testid='avatar-picker-open']").ClickAsync();
        await second.Find("[data-testid='avatar-picker-open']").ClickAsync();
        Assert.NotEqual(first.Find("input[type='file']").Id, second.Find("input[type='file']").Id);
        Assert.Equal(first.Find("input[type='file']").Id, first.Find("label").GetAttribute("for"));
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private sealed class TestImage : IBrowserFile {
        public string Name => "owned.png";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => 4;
        public string ContentType => "image/png";
        public int OpenCount { get; private set; }
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) {
            OpenCount++;
            return new MemoryStream([1, 2, 3, 4]);
        }
    }
}
