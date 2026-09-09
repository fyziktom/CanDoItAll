using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static CanDoItAll.Tests.Components.AgentFramework.AgentChatPanelResponsivenessTests;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentChatEffectOwnershipTests {
    [Fact]
    public async Task Created_thread_advances_target_and_clears_old_composer_and_voice() {
        var (service, reads, original, first, second) = Reads();
        var agent = original with { ConfigurationJson = AgentVoiceAccessMetadata.Write("{}", new() { CanUseVoiceMode = true }) };
        reads.Agents = [agent];
        var creates = 0;
        reads.CreateThread = id => {
            Assert.Equal(agent.Id, id);
            creates++;
            return Task.FromResult(second);
        };
        using var context = Context(service, out _);
        var cut = Render(context, agent, first);
        await cut.InvokeAsync(() => Workspace(cut).DraftPromptChanged.InvokeAsync("Old composer"));
        await cut.InvokeAsync(() => Workspace(cut).VoiceRecordingToggled.InvokeAsync());
        var generation = Workspace(cut).SelectionGeneration;
        await cut.InvokeAsync(() => Workspace(cut).NavigationIntent.InvokeAsync(new(generation,
            CanDoItAll.AgentFramework.UI.Chat.AgentChatNavigationAction.NewThread)));
        Assert.Equal(second.Id, Workspace(cut).Session!.Id);
        Assert.True(Workspace(cut).SelectionGeneration > generation);
        Assert.Empty(Workspace(cut).DraftPrompt);
        Assert.False(Workspace(cut).IsVoiceRecording);
        Assert.False(Workspace(cut).IsBusy);
        Assert.Equal(1, creates);
    }

    [Fact]
    public async Task Full_mode_accepts_session_changes_and_parent_echo_without_duplicate_agent_observation() {
        var (service, reads) = AgentChatSessionTests.CreateReads();
        var agent = CreateAgent();
        var first = CreateSession(agent.Id);
        var second = CreateSession(agent.Id) with { Title = "Second thread" };
        reads.Agents = [agent];
        reads.Workspace = (_, id, _) => Task.FromResult(CreateWorkspace(agent.Id, id == first.Id ? first : second));
        using var context = Context(service, out _);
        var accepted = new List<AgentDefinition?>();
        var cut = context.Render<AgentChatPanel>(p => p.Add(x => x.PreferredAgentId, agent.Id)
            .Add(x => x.PreferredSessionId, first.Id).Add(x => x.SelectedAgentChanged, value => accepted.Add(value)));
        await Select(cut, agent, second);
        Assert.Equal(second.Id, Workspace(cut).Session!.Id);
        await Select(cut, agent, second);
        Assert.Equal(2, reads.WorkspaceCalls);
        Assert.Single(accepted);
        reads.Agents = [agent with { Name = "Updated metadata" }];
        await cut.InvokeAsync(() => Workspace(cut).NavigationIntent.InvokeAsync(new(
            Workspace(cut).Navigation!.Generation, CanDoItAll.AgentFramework.UI.Chat.AgentChatNavigationAction.Refresh)));
        Assert.Equal("Updated metadata", accepted[^1]!.Name);
        Assert.Equal(2, accepted.Count);
    }

    [Fact]
    public async Task Late_rename_keeps_the_captured_target_and_cannot_refresh_the_new_thread() {
        var (service, reads, agent, first, second) = Reads();
        var pending = new TaskCompletionSource<ChatSessionRecord>();
        var mutations = new List<(Guid Agent, Guid Session, string Title)>();
        reads.Rename = (id, session, title) => {
            mutations.Add((id, session, title));
            return pending.Task;
        };
        using var context = Context(service, out _);
        var cut = Render(context, agent, first);
        var rename = cut.InvokeAsync(() => Workspace(cut).SessionTitleChanged.InvokeAsync("  Captured title  "));
        await Select(cut, agent, second);
        pending.SetResult(first with { Title = "Captured title" });
        await rename;
        Assert.Equal((agent.Id, first.Id, "Captured title"), Assert.Single(mutations));
        Assert.Equal(2, reads.WorkspaceCalls);
        Assert.Equal(second.Id, Workspace(cut).Session!.Id);
        Assert.False(Workspace(cut).IsBusy);
    }

    [Fact]
    public async Task Old_execution_completion_cannot_clear_the_new_operation() {
        var (service, reads, agent, first, second) = Reads();
        using var context = Context(service, out var effects);
        var old = new TaskCompletionSource<AgentChatRunResult>();
        var current = new TaskCompletionSource<AgentChatRunResult>();
        effects.Sends.Enqueue(old);
        effects.Sends.Enqueue(current);
        var detached = new DetachedLogger();
        context.Services.AddSingleton<ILogger<AgentChatPanel>>(detached);
        var cut = Render(context, agent, first);
        await Send(cut, "Old prompt");
        await Select(cut, agent, second);
        await Send(cut, "Current prompt");
        old.SetResult(CreateRunResult(agent.Id, first.Id));
        await detached.Observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => { });
        Assert.True(Workspace(cut).IsBusy);
        Assert.Equal("Current prompt", Workspace(cut).PendingUserPrompt);
        Assert.Equal(second.Id, Workspace(cut).Session!.Id);
        Assert.Equal(2, effects.SendCalls);
        Assert.Equal(2, reads.WorkspaceCalls);
        current.SetResult(CreateRunResult(agent.Id, second.Id));
        cut.WaitForAssertion(() => Assert.False(Workspace(cut).IsBusy));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Replacing_or_disposing_the_panel_closes_only_its_runtime_dialog(bool dispose) {
        var (service, reads, agent, first, second) = Reads();
        var run = CreateRunningRun(agent.Id, first.Id);
        reads.Workspace = (_, id, _) => Task.FromResult(CreateWorkspace(agent.Id, id == first.Id ? first : second, id == first.Id ? run : null));
        reads.Detail = (_, _) => Task.FromResult(new ExecutionRunDetail(run, first, [], []));
        using var context = Context(service, out _);
        var cut = Render(context, agent, first);
        var dialogs = context.Services.GetRequiredService<DialogService>();
        _ = dialogs.OpenAsync("Unrelated", _ => builder => builder.AddContent(0, "Keep this overlay"));
        cut.Find("[data-testid='agents-chat-open-runtime-details']").Click();
        Assert.Equal(2, dialogs.Dialogs.Count);
        if (dispose) {
            await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        } else {
            await Select(cut, agent, second);
        }
        Assert.Equal("Unrelated", Assert.Single(dialogs.Dialogs).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Obsolete_transcription_does_not_send_or_clear_new_recording_and_cleans_the_browser_owner(bool changeAgent) {
        var (service, reads, original, first, second) = Reads();
        var agent = original with { ConfigurationJson = AgentVoiceAccessMetadata.Write("{}", new() { CanUseVoiceMode = true }) };
        var targetAgent = changeAgent ? agent with { Id = Guid.NewGuid() } : agent;
        var targetThread = second with { AgentId = targetAgent.Id };
        reads.Agents = changeAgent ? [agent, targetAgent] : [agent];
        reads.Workspace = (id, session, _) => Task.FromResult(CreateWorkspace(id, session == first.Id ? first : targetThread));
        using var context = Context(service, out var effects);
        context.Services.AddSingleton(DispatchProxy.Create<IAgentVoiceService, EffectsProxy>());
        var voice = (EffectsProxy)context.Services.GetRequiredService<IAgentVoiceService>();
        context.JSInterop.Setup<BrowserVoiceRecording>("CanDoItAll.agentFramework.voice.stopRecordingForOwner", _ => true)
            .SetResult(new() { Base64 = Convert.ToBase64String([1, 2, 3]) });
        var cut = Render(context, agent, first);
        await cut.InvokeAsync(() => Workspace(cut).VoiceRecordingToggled.InvokeAsync());
        var transcription = cut.InvokeAsync(() => Workspace(cut).VoiceRecordingToggled.InvokeAsync());
        Assert.True(Workspace(cut).IsVoiceTranscribing);
        await Select(cut, targetAgent, targetThread);
        await cut.InvokeAsync(() => Workspace(cut).VoiceRecordingToggled.InvokeAsync());
        voice.Transcription.SetResult(new("Obsolete spoken prompt", "fixture"));
        await transcription;
        Assert.True(Workspace(cut).IsVoiceRecording);
        Assert.False(Workspace(cut).IsVoiceTranscribing);
        Assert.Equal("Recording", Workspace(cut).VoiceStatusText);
        Assert.Equal(0, effects.SendCalls);
        Assert.Contains(context.JSInterop.Invocations, call => call.Identifier == "CanDoItAll.agentFramework.voice.disposeOwner");
    }

    [Fact]
    public async Task Old_artifact_read_cannot_stage_paths_after_thread_replacement() {
        var (service, reads, agent, first, second) = Reads();
        var run = CreateRunningRun(agent.Id, first.Id) with { State = ExecutionState.Completed };
        var detail = new ExecutionRunDetail(run, first, [], []) {
            Artifacts = [new(Guid.NewGuid(), run.Id, "text", "Old", "artifacts/old.txt", "text/plain", "fixture", "old", DateTimeOffset.UtcNow)]
        };
        var pending = new TaskCompletionSource<ExecutionRunDetail>();
        var calls = 0;
        reads.Workspace = (_, id, _) => Task.FromResult(CreateWorkspace(agent.Id, id == first.Id ? first : second, id == first.Id ? run : null));
        reads.Detail = (_, _) => ++calls == 1 ? Task.FromResult(detail) : pending.Task;
        using var context = Context(service, out _);
        var cut = Render(context, agent, first);
        var staging = cut.InvokeAsync(() => Workspace(cut).AttachmentRequested.InvokeAsync());
        await Select(cut, agent, second);
        pending.SetResult(detail);
        await staging;
        Assert.Equal(second.Id, Workspace(cut).Session!.Id);
        Assert.Empty(Workspace(cut).DraftAttachmentPaths);
    }

    [Fact]
    public async Task Favorite_and_switch_results_from_an_owned_closed_dialog_cannot_replace_the_new_target() {
        var (service, reads, agent, first, second) = Reads();
        reads.Editor = id => Task.FromResult(new AgentEditorModel { Id = id, Tags = [] });
        var save = new TaskCompletionSource<Guid>();
        var mutations = 0;
        reads.SaveAgent = editor => {
            Assert.Equal(agent.Id, editor.Id);
            Assert.Contains(editor.Tags, AgentSpecialTags.IsFavorite);
            mutations++;
            return save.Task;
        };
        using var context = Context(service, out _);
        var cut = Render(context, agent, first);
        await cut.InvokeAsync(() => Workspace(cut).NavigationIntent.InvokeAsync(new(Workspace(cut).SelectionGeneration,
            CanDoItAll.AgentFramework.UI.Chat.AgentChatNavigationAction.SwitchAgent)));
        var dialogs = context.Services.GetRequiredService<DialogService>();
        var dialog = Assert.Single(dialogs.Dialogs);
        var toggle = Assert.IsType<Func<AgentDefinition, Task<AgentDefinition>>>(dialog.Parameters[nameof(AgentSwitchDialog.FavoriteToggled)]);
        var favorite = cut.InvokeAsync(() => toggle(agent));
        await Select(cut, agent, second);
        Assert.Empty(dialogs.Dialogs);
        var catalogReads = reads.CatalogCalls;
        save.SetResult(agent.Id);
        await favorite;
        await cut.InvokeAsync(() => dialogs.CloseAsync(dialog, Guid.NewGuid()));
        Assert.Equal(catalogReads, reads.CatalogCalls);
        Assert.Equal(second.Id, Workspace(cut).Session!.Id);
        Assert.Equal(1, mutations);
        Assert.False(Workspace(cut).IsBusy);
    }

    [Fact]
    public async Task Late_uploaded_image_cannot_stage_into_a_new_target_or_clear_its_busy_state() {
        var (service, _, agent, first, second) = Reads();
        using var context = Context(service, out var effects);
        var staging = DispatchProxy.Create<IAgentChatAttachmentStagingService, EffectsProxy>();
        var files = (EffectsProxy)staging;
        context.Services.AddSingleton(staging);
        var cut = Render(context, agent, first);
        var upload = cut.InvokeAsync(() => Workspace(cut).AttachmentFilesSelected.InvokeAsync(new InputFileChangeEventArgs([new FixtureFile()])));
        await Select(cut, agent, second);
        var current = new TaskCompletionSource<AgentChatRunResult>();
        effects.Sends.Enqueue(current);
        await Send(cut, "New target prompt");
        files.Upload.SetResult(new("uploads/old.png", "image/png", 3));
        await upload;
        Assert.Empty(Workspace(cut).DraftAttachmentPaths);
        Assert.True(Workspace(cut).IsBusy);
        Assert.Equal("New target prompt", Workspace(cut).PendingUserPrompt);
        current.SetResult(CreateRunResult(agent.Id, second.Id));
        cut.WaitForAssertion(() => Assert.False(Workspace(cut).IsBusy));
    }

    private static (IAgentFrameworkWorkspaceService, AgentChatSessionTests.ReadProxy, AgentDefinition, ChatSessionRecord, ChatSessionRecord) Reads() {
        var (service, reads) = AgentChatSessionTests.CreateReads();
        var agent = CreateAgent();
        var first = CreateSession(agent.Id);
        var second = CreateSession(agent.Id) with { Title = "New target" };
        reads.Agents = [agent];
        reads.Workspace = (_, id, _) => Task.FromResult(CreateWorkspace(agent.Id, id == first.Id ? first : second));
        return (service, reads, agent, first, second);
    }

    private static BunitContext Context(IAgentFrameworkWorkspaceService service, out EffectsProxy effects) {
        var orchestrator = DispatchProxy.Create<IAgentChatExecutionOrchestrator, EffectsProxy>();
        effects = (EffectsProxy)orchestrator;
        return CreateContext(service, orchestrator);
    }

    private static IRenderedComponent<AgentChatPanel> Render(BunitContext context, AgentDefinition agent, ChatSessionRecord session)
        => context.Render<AgentChatPanel>(p => p.Add(x => x.PreferredAgentId, agent.Id).Add(x => x.PreferredSessionId, session.Id));

    private static Task Select(IRenderedComponent<AgentChatPanel> cut, AgentDefinition agent, ChatSessionRecord session)
        => cut.InvokeAsync(() => cut.Render(p => p.Add(x => x.PreferredAgentId, agent.Id).Add(x => x.PreferredSessionId, session.Id)));

    private static ChatWorkspacePanel Workspace(IRenderedComponent<AgentChatPanel> cut) => cut.FindComponent<ChatWorkspacePanel>().Instance;

    private static async Task Send(IRenderedComponent<AgentChatPanel> cut, string prompt) {
        await cut.InvokeAsync(() => Workspace(cut).DraftPromptChanged.InvokeAsync(prompt));
        await cut.InvokeAsync(() => Workspace(cut).SendRequested.InvokeAsync());
    }

    public class EffectsProxy : DispatchProxy {
        public Queue<TaskCompletionSource<AgentChatRunResult>> Sends { get; } = [];
        public TaskCompletionSource<AgentVoiceTranscriptionResult> Transcription { get; } = new();
        public TaskCompletionSource<AgentChatAttachmentStagingResult> Upload { get; } = new();
        public int SendCalls { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            switch (method?.Name) {
                case nameof(IAgentChatExecutionOrchestrator.StartSendMessage):
                    SendCalls++;
                    return new AgentChatOperationHandle(CreateActivityStreamId(), Sends.Dequeue().Task);
                case nameof(IAgentVoiceService.TranscribeAsync):
                    return Transcription.Task;
                case nameof(IAgentChatAttachmentStagingService.StageImageAsync):
                    return Upload.Task;
                default:
                    throw new NotSupportedException(method?.Name);
            }
        }
    }

    private sealed class FixtureFile : IBrowserFile {
        public string Name => "fixture.png";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => 3;
        public string ContentType => "image/png";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => new MemoryStream([1, 2, 3]);
    }

    private sealed class DetachedLogger : ILogger<AgentChatPanel> {
        public TaskCompletionSource Observed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (formatter(state, exception).Contains("outside its original target", StringComparison.Ordinal)) {
                Observed.TrySetResult();
            }
        }
    }
}
