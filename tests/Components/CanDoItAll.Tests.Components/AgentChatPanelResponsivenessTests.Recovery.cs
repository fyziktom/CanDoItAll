using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Chat;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed partial class AgentChatPanelResponsivenessTests {
    private const string RecoveryButtonSelector = "[data-testid='agents-chat-recover-original-run']";
    private const string RejectionSelector = "[data-testid='agents-chat-start-rejected']";
    private const string RecoveryDraft = "Read the existing result after recovery.";
    private const string RecoveryAttachment = "uploads/recovery.png";
    private const string PrivateRecoveryFailure = "PRIVATE_RECOVERY_DIAGNOSTIC";

    [Theory]
    [InlineData(ExecutionState.Completed, false)]
    [InlineData(ExecutionState.Completed, true)]
    [InlineData(ExecutionState.WaitingOnTool, false)]
    [InlineData(ExecutionState.Failed, false)]
    public async Task Explicit_recovery_preserves_the_composer_and_reads_the_original_run_before_accepting_its_state(ExecutionState state, bool unresolved) {
        await using var fixture = await CreateRecoveryPanelAsync();
        var cut = fixture.Cut;
        var composerKey = RecoveryWorkspace(cut).ComposerKey;
        fixture.Workspace.WorkspaceResponses.Enqueue(CreateWorkspace(fixture.Agent.Id, fixture.Session, fixture.Run));
        await cut.InvokeAsync(() => RecoveryWorkspace(cut).NavigationIntent.InvokeAsync(
            new(RecoveryWorkspace(cut).SelectionGeneration, AgentChatNavigationAction.Refresh)));
        Assert.Empty(fixture.Orchestrator.RecoveryRequests);
        Assert.NotEmpty(cut.FindAll(RejectionSelector));
        var source = fixture.Orchestrator.LastStreamId!;
        await ClickRecoveryAsync(fixture);
        Assert.True(cut.Find(RecoveryButtonSelector).HasAttribute("disabled"));
        await cut.Find(RecoveryButtonSelector).ClickAsync(new MouseEventArgs());
        var request = Assert.Single(fixture.Orchestrator.RecoveryRequests);
        Assert.Equal((fixture.Agent.Id, fixture.Session.Id, fixture.Run.Id, source), request);
        Assert.NotEqual(source.OperationId, fixture.Orchestrator.LastStreamId!.OperationId);
        var saved = fixture.Run with {
            State = state,
            ToolAdmission = unresolved ? CreateUnresolvedJournal(fixture) : null,
            PendingApprovals = state == ExecutionState.WaitingOnTool ? CreateWaitingApprovalRun(fixture.Agent.Id, fixture.Session.Id).PendingApprovals : []
        };
        fixture.Workspace.InitialRunDetail = new(saved, fixture.Session, [], []);
        fixture.Workspace.WorkspaceResponses.Enqueue(CreateWorkspace(fixture.Agent.Id, fixture.Session, saved));
        fixture.Orchestrator.RecoveryCompletion.SetResult(RecoveryResult(fixture, state));

        cut.WaitForAssertion(() => {
            var panel = RecoveryWorkspace(cut);
            Assert.Equal(saved.Id, panel.ActiveRun!.Id);
            Assert.Equal(saved.State, panel.ActiveRun.State);
            Assert.Equal(saved.PendingApprovals, panel.ActiveRun.PendingApprovals);
            Assert.False(panel.IsBusy);
            Assert.Equal(RecoveryDraft, panel.DraftPrompt);
            Assert.Equal([RecoveryAttachment], panel.DraftAttachmentPaths);
            Assert.Equal(composerKey, panel.ComposerKey);
            Assert.Empty(panel.PendingUserPrompt);
            Assert.Equal(state == ExecutionState.Failed || unresolved ? 1 : 0, cut.FindAll(RejectionSelector).Count);
        });
        Assert.Single(fixture.Orchestrator.SendRequests);
        Assert.False(fixture.Orchestrator.ApprovalStarted.Task.IsCompleted);
        Assert.All(fixture.Workspace.ExecutionRunDetailRequests, id => Assert.Equal(fixture.Run.Id, id));
        if (state == ExecutionState.WaitingOnTool) {
            Assert.Single(RecoveryWorkspace(cut).ActiveRun!.PendingApprovals);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recovery_or_readback_failure_retains_the_warning_and_draft_without_exposing_diagnostics(bool readback) {
        await using var fixture = await CreateRecoveryPanelAsync();
        var composerKey = RecoveryWorkspace(fixture.Cut).ComposerKey;
        await ClickRecoveryAsync(fixture);
        if (readback) {
            fixture.Orchestrator.RecoveryCompletion.SetResult(RecoveryResult(fixture, ExecutionState.Completed));
            await fixture.Workspace.PostRunRefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            fixture.Workspace.PostRunWorkspace.SetException(new IOException(PrivateRecoveryFailure));
        } else {
            fixture.Orchestrator.RecoveryCompletion.SetException(new IOException(PrivateRecoveryFailure));
        }
        fixture.Cut.WaitForAssertion(() => {
            Assert.False(RecoveryWorkspace(fixture.Cut).IsBusy);
            Assert.NotEmpty(fixture.Cut.FindAll(RejectionSelector));
            Assert.Equal(RecoveryDraft, RecoveryWorkspace(fixture.Cut).DraftPrompt);
            Assert.Equal([RecoveryAttachment], RecoveryWorkspace(fixture.Cut).DraftAttachmentPaths);
            Assert.Equal(composerKey, RecoveryWorkspace(fixture.Cut).ComposerKey);
        });
        Assert.DoesNotContain(PrivateRecoveryFailure, fixture.Cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Context.Services.GetRequiredService<NotificationService>().Messages,
            item => item.Detail?.Contains(PrivateRecoveryFailure, StringComparison.Ordinal) == true);
        Assert.Single(fixture.Orchestrator.RecoveryRequests);
        Assert.Single(fixture.Orchestrator.SendRequests);
        Assert.False(fixture.Orchestrator.ApprovalStarted.Task.IsCompleted);
    }

    [Theory]
    [InlineData(RecoveryDetach.SwitchThread, false)]
    [InlineData(RecoveryDetach.SwitchAwayAndBack, false)]
    [InlineData(RecoveryDetach.Dispose, false)]
    [InlineData(RecoveryDetach.ProfileBeforeCompletion, false)]
    [InlineData(RecoveryDetach.ProfileDuringReadback, false)]
    [InlineData(RecoveryDetach.SwitchThread, true)]
    [InlineData(RecoveryDetach.ProfileBeforeCompletion, true)]
    public async Task Late_recovery_cannot_replace_a_new_thread_or_profile_view(RecoveryDetach detach, bool floating) {
        await using var fixture = await CreateRecoveryPanelAsync(floating);
        var cut = fixture.Cut;
        await ClickRecoveryAsync(fixture);
        if (fixture.Coordinator is { } coordinator) {
            Assert.False(coordinator.TryBeginOperation(fixture.HandleId!.Value));
        }
        var expectedDraft = RecoveryDraft;
        var expectedSession = fixture.Session.Id;
        if (detach is RecoveryDetach.SwitchThread or RecoveryDetach.SwitchAwayAndBack) {
            var other = CreateSession(fixture.Agent.Id);
            fixture.Workspace.WorkspaceResponses.Enqueue(CreateWorkspace(fixture.Agent.Id, other));
            await cut.InvokeAsync(() => cut.Render(parameters => parameters
                .Add(component => component.PreferredAgentId, fixture.Agent.Id)
                .Add(component => component.PreferredSessionId, other.Id)));
            expectedSession = other.Id;
            if (detach == RecoveryDetach.SwitchAwayAndBack) {
                fixture.Workspace.WorkspaceResponses.Enqueue(CreateWorkspace(fixture.Agent.Id, fixture.Session, fixture.Run));
                await cut.InvokeAsync(() => cut.Render(parameters => parameters
                    .Add(component => component.PreferredAgentId, fixture.Agent.Id)
                    .Add(component => component.PreferredSessionId, fixture.Session.Id)));
                expectedSession = fixture.Session.Id;
            }
            expectedDraft = "A newer draft in the current selection.";
            await cut.InvokeAsync(() => RecoveryWorkspace(cut).DraftPromptChanged.InvokeAsync(expectedDraft));
        } else if (detach == RecoveryDetach.Dispose) {
            await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        } else if (detach == RecoveryDetach.ProfileDuringReadback) {
            fixture.Orchestrator.RecoveryCompletion.SetResult(RecoveryResult(fixture, ExecutionState.Completed));
            await fixture.Workspace.PostRunRefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            fixture.Generation.Value = new(2);
            fixture.Workspace.PostRunWorkspace.SetResult(CreateWorkspace(fixture.Agent.Id, fixture.Session,
                fixture.Run with { State = ExecutionState.Completed }));
        } else {
            fixture.Generation.Value = new(2);
        }
        var readsBeforeCompletion = fixture.Workspace.WorkspaceRequestCount;
        var notifications = fixture.Context.Services.GetRequiredService<NotificationService>().Messages.Count;
        if (detach != RecoveryDetach.ProfileDuringReadback) {
            fixture.Orchestrator.RecoveryCompletion.SetResult(RecoveryResult(fixture, ExecutionState.Completed));
        }
        await fixture.Logger.Detached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (fixture.Coordinator is { } releasedCoordinator) {
            await cut.InvokeAsync(() => {
                Assert.True(releasedCoordinator.TryBeginOperation(fixture.HandleId!.Value));
                releasedCoordinator.ReconcileRunStateAfterOperation(fixture.HandleId.Value);
            });
        }
        Assert.Equal(readsBeforeCompletion, fixture.Workspace.WorkspaceRequestCount);
        Assert.Equal(notifications, fixture.Context.Services.GetRequiredService<NotificationService>().Messages.Count);
        Assert.Single(fixture.Orchestrator.RecoveryRequests);
        Assert.Single(fixture.Orchestrator.SendRequests);
        if (detach != RecoveryDetach.Dispose) {
            await cut.InvokeAsync(() => cut.Render());
            Assert.Equal(expectedSession, RecoveryWorkspace(cut).Session!.Id);
            Assert.Equal(expectedDraft, RecoveryWorkspace(cut).DraftPrompt);
            if (detach is RecoveryDetach.ProfileBeforeCompletion or RecoveryDetach.ProfileDuringReadback) {
                Assert.Equal(ExecutionState.Failed, RecoveryWorkspace(cut).ActiveRun!.State);
                Assert.NotEmpty(cut.FindAll(RejectionSelector));
            }
        }
    }

    [Theory]
    [InlineData(ExecutionState.Running)]
    [InlineData(ExecutionState.WaitingOnTool)]
    [InlineData(ExecutionState.Failed)]
    public async Task Active_work_pending_approval_or_a_new_profile_blocks_recovery_before_dispatch(ExecutionState state) {
        await using var fixture = await CreateRecoveryPanelAsync();
        if (state == ExecutionState.Failed) {
            fixture.Generation.Value = new(2);
            await fixture.Cut.InvokeAsync(() => fixture.Cut.Render());
        } else {
            var run = fixture.Run with { State = state,
                PendingApprovals = state == ExecutionState.WaitingOnTool ? CreateWaitingApprovalRun(fixture.Agent.Id, fixture.Session.Id).PendingApprovals : [] };
            fixture.Workspace.InitialRunDetail = new(run, fixture.Session, [], []);
            fixture.Workspace.WorkspaceResponses.Enqueue(CreateWorkspace(fixture.Agent.Id, fixture.Session, run));
            await fixture.Cut.InvokeAsync(() => RecoveryWorkspace(fixture.Cut).NavigationIntent.InvokeAsync(
                new(RecoveryWorkspace(fixture.Cut).SelectionGeneration, AgentChatNavigationAction.Refresh)));
        }
        Assert.True(fixture.Cut.Find(RecoveryButtonSelector).HasAttribute("disabled"));
        await fixture.Cut.Find(RecoveryButtonSelector).ClickAsync(new MouseEventArgs());
        Assert.Empty(fixture.Orchestrator.RecoveryRequests);
        Assert.Equal(RecoveryDraft, RecoveryWorkspace(fixture.Cut).DraftPrompt);
        Assert.Equal([RecoveryAttachment], RecoveryWorkspace(fixture.Cut).DraftAttachmentPaths);
    }

    private static async Task<RecoveryPanelFixture> CreateRecoveryPanelAsync(bool floating = false) {
        var agent = CreateAgent();
        var session = CreateSession(agent.Id);
        var run = CreateRunningRun(agent.Id, session.Id) with { State = ExecutionState.Failed, PendingApprovals = [] };
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, DeferredWorkspaceProxy>();
        var workspace = (DeferredWorkspaceProxy)(object)service;
        workspace.Service = service;
        workspace.Agents = [agent];
        workspace.InitialWorkspace = CreateWorkspace(agent.Id, session, run);
        workspace.InitialRunDetail = new(run, session, [], []);
        var orchestratorService = DispatchProxy.Create<IAgentChatExecutionOrchestrator, CompletedRunOrchestratorProxy>();
        var orchestrator = (CompletedRunOrchestratorProxy)(object)orchestratorService;
        orchestrator.SendCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = CreateContext(service, orchestratorService);
        FloatingAgentChatCoordinator? coordinator = null;
        AgentChatHandleId? handleId = null;
        if (floating) {
            var registry = new ActiveAgentChatRegistry(TimeProvider.System);
            handleId = registry.Open(new(agent.Id, agent.Name, agent.RoleTitle, agent.AvatarImageUrl),
                session.Id, FloatingAgentChatSettings.Default).HandleId;
            coordinator = new FloatingAgentChatCoordinator(service, registry, new EmptyPreparationPool(),
                DispatchProxy.Create<IFloatingAgentChatSettingsService, UnexpectedCallProxy>(),
                TimeProvider.System, NullLogger<FloatingAgentChatCoordinator>.Instance);
            context.Services.AddSingleton<IFloatingAgentChatCoordinator>(coordinator);
        }
        var generation = new RecoveryPanelGeneration();
        var logger = new RecoveryPanelLogger();
        context.Services.AddSingleton<IAgentExecutionProfileGenerationSource>(generation);
        context.Services.AddSingleton<ILogger<AgentChatPanel>>(logger);
        var staging = DispatchProxy.Create<IAgentChatAttachmentStagingService, AgentChatEffectOwnershipTests.EffectsProxy>();
        ((AgentChatEffectOwnershipTests.EffectsProxy)(object)staging).Upload.SetResult(new(RecoveryAttachment, "image/png", 3));
        context.Services.AddSingleton(staging);
        var cut = context.Render<AgentChatPanel>(parameters => parameters
            .Add(component => component.PreferredAgentId, agent.Id)
            .Add(component => component.PreferredSessionId, session.Id)
            .Add(component => component.ActiveChatHandleId, handleId)
            .Add(component => component.DisplayMode, floating ? AgentChatPanelDisplayMode.FocusedFloating : AgentChatPanelDisplayMode.FullPage));
        cut.WaitForElement("[data-testid='chat-send-button']");
        Assert.Empty(cut.FindAll(RecoveryButtonSelector));
        await cut.InvokeAsync(() => RecoveryWorkspace(cut).AttachmentFilesSelected.InvokeAsync(new InputFileChangeEventArgs([new RecoveryImageFile()])));
        await cut.InvokeAsync(() => RecoveryWorkspace(cut).DraftPromptChanged.InvokeAsync(RecoveryDraft));
        await cut.Find("[data-testid='chat-send-button']").ClickAsync(new MouseEventArgs());
        await orchestrator.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        orchestrator.SendCompletion.SetException(new AgentChatSessionBlockedException(agent.Id, session.Id, run.Id,
            AgentChatSessionBlockReason.UnresolvedEffects));
        cut.WaitForAssertion(() => {
            Assert.False(RecoveryWorkspace(cut).IsBusy);
            Assert.False(cut.Find(RecoveryButtonSelector).HasAttribute("disabled"));
        });
        return new(context, cut, agent, session, run, workspace, orchestrator, generation, logger, coordinator, handleId);
    }

    private static AgentToolJournalRecord CreateUnresolvedJournal(RecoveryPanelFixture fixture) {
        var source = fixture.Orchestrator.RecoveryRequests[0].StreamId;
        var envelope = AgentToolProtocolEnvelope.Create("test-recovery", 1, "{}");
        var payload = new AgentToolPreparedPayload("test.read", 1, envelope.Digest, "{}",
            AgentToolProposalEffect.Read, AgentToolProposalRecovery.RevalidateAndRead);
        var proposal = new AgentToolProposalRecord(new(Guid.NewGuid()), 1, "test-call", payload, false,
            AgentToolProposalState.ReconciliationRequired, ExecutionApprovalStatus.Approved, null);
        return new(1, 1, new(new(fixture.Run.Id, fixture.Session.Id, AgentExecutionAuthorityId.Create()),
            fixture.Agent.Id, AgentRuntimeContextPurpose.InteractiveChat,
            new(source.DatabaseProfileId, "test-profile", source.DatabaseProfileGeneration)), [],
            [new(new(Guid.NewGuid()), 1, envelope.Digest, envelope, [proposal])]);
    }

    private static async Task ClickRecoveryAsync(RecoveryPanelFixture fixture) {
        await fixture.Cut.Find(RecoveryButtonSelector).ClickAsync(new MouseEventArgs()).WaitAsync(TimeSpan.FromSeconds(2));
        await fixture.Orchestrator.RecoveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static ExecutionRunResult RecoveryResult(RecoveryPanelFixture fixture, ExecutionState state)
        => new(fixture.Run.Id, fixture.Session.Id, "Recovered", null, CreateRunResult(fixture.Agent.Id, fixture.Session.Id).Metric) { State = state };

    private static ChatWorkspacePanel RecoveryWorkspace(IRenderedComponent<AgentChatPanel> cut)
        => cut.FindComponent<ChatWorkspacePanel>().Instance;

    public enum RecoveryDetach { SwitchThread, SwitchAwayAndBack, Dispose, ProfileBeforeCompletion, ProfileDuringReadback }

    private sealed class RecoveryPanelGeneration : IAgentExecutionProfileGenerationSource {
        public DatabaseProfileGeneration Value { get; set; } = new(0);
        public DatabaseProfileGeneration GetGeneration() => Value;
    }

    private sealed class RecoveryPanelLogger : ILogger<AgentChatPanel> {
        public TaskCompletionSource Detached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (formatter(state, exception).Contains("outside its original target", StringComparison.Ordinal)) {
                Detached.TrySetResult();
            }
        }
    }

    private sealed record RecoveryPanelFixture(BunitContext Context, IRenderedComponent<AgentChatPanel> Cut, AgentDefinition Agent,
        ChatSessionRecord Session, ExecutionRunRecord Run, DeferredWorkspaceProxy Workspace, CompletedRunOrchestratorProxy Orchestrator,
        RecoveryPanelGeneration Generation, RecoveryPanelLogger Logger, FloatingAgentChatCoordinator? Coordinator,
        AgentChatHandleId? HandleId) : IAsyncDisposable {
        public async ValueTask DisposeAsync() {
            Orchestrator.RecoveryCompletion.TrySetCanceled();
            Workspace.PostRunWorkspace.TrySetCanceled();
            Context.Dispose();
            if (Coordinator is not null) {
                await Coordinator.DisposeAsync();
            }
        }
    }

    private sealed class RecoveryImageFile : IBrowserFile {
        public string Name => "recovery.png";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => 3;
        public string ContentType => "image/png";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => new MemoryStream([1, 2, 3]);
    }
}
