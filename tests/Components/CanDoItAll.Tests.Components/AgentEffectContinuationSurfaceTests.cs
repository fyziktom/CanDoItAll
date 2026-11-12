using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Chat;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed partial class AgentChatPanelResponsivenessTests {
    private const string ContinuationSelector = "[data-testid='agents-chat-effect-continuation']";

    [Fact]
    public async Task Native_recovery_distinguishes_known_commitment_from_uncertainty_and_access_denial() {
        await using var fixture = await CreateRecoveryPanelAsync();
        var notifications = fixture.Context.Services.GetRequiredService<NotificationService>();
        var stagedAttachment = Assert.Single(notifications.Messages, message => message.Severity == NotificationSeverity.Success);
        Assert.Equal("Staged 1 attachment path(s) for the next prompt.", stagedAttachment.Detail);
        var continuation = EffectContinuation(fixture.Run, AgentEffectCheckpointPersistence.Unconfirmed);
        await ClickRecoveryAsync(fixture);
        fixture.Orchestrator.RecoveryCompletion.SetException(new AgentEffectReconciliationIncompleteException(continuation));
        fixture.Cut.WaitForAssertion(() => {
            Assert.Contains("At least 1 effects are known committed", fixture.Cut.Find(ContinuationSelector).TextContent, StringComparison.Ordinal);
            Assert.Contains("checkpoint is not confirmed", fixture.Cut.Find(ContinuationSelector).TextContent, StringComparison.Ordinal);
            Assert.Equal(ExecutionState.Failed, RecoveryWorkspace(fixture.Cut).ActiveRun!.State);
            Assert.False(RecoveryWorkspace(fixture.Cut).IsBusy);
        });
        foreach (var denied in new[] { false, true }) {
            fixture.Orchestrator.RecoveryCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await ClickRecoveryAsync(fixture);
            fixture.Orchestrator.RecoveryCompletion.SetException(denied
                ? new AgentToolReceiptAccessDeniedException()
                : new AgentToolReceiptReadUnavailableException(new IOException(PrivateRecoveryFailure)));
            fixture.Cut.WaitForAssertion(() => {
                var text = fixture.Cut.Find(ContinuationSelector).TextContent;
                Assert.Contains("At least 1 effects are known committed", text, StringComparison.Ordinal);
                Assert.Contains(denied ? "was denied" : "could not be verified", text, StringComparison.Ordinal);
                Assert.False(RecoveryWorkspace(fixture.Cut).IsBusy);
            });
        }
        Assert.Equal(3, fixture.Orchestrator.RecoveryRequests.Count);
        Assert.Equal(1, fixture.Workspace.WorkspaceRequestCount);
        fixture.Orchestrator.RecoveryCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await ClickRecoveryAsync(fixture);
        var saved = EffectContinuation(fixture.Run, AgentEffectCheckpointPersistence.Confirmed);
        fixture.Orchestrator.RecoveryCompletion.SetException(new AgentEffectReconciliationCleanupException(saved,
            new IOException(PrivateRecoveryFailure)));
        fixture.Cut.WaitForAssertion(() => {
            Assert.NotEmpty(fixture.Cut.FindAll("[data-testid='agents-chat-lease-release-unconfirmed']"));
            Assert.Contains("At least 1 effects are known committed", fixture.Cut.Find(ContinuationSelector).TextContent, StringComparison.Ordinal);
            Assert.True(fixture.Cut.Find(RecoveryButtonSelector).HasAttribute("disabled"));
            Assert.False(RecoveryWorkspace(fixture.Cut).IsBusy);
        });
        var waiting = fixture.Run with { State = ExecutionState.WaitingOnTool };
        fixture.Workspace.InitialRunDetail = new(waiting, fixture.Session, [], []) {
            EffectContinuation = new(waiting.Id, waiting.ChatSessionId, 1, 1, saved.Requirement, saved.Counts, 0,
                saved.Counts.EligibleOwnerEffects, AgentEffectReconciliationStop.SnapshotRead, AgentEffectCheckpointPersistence.Confirmed, [])
        };
        fixture.Workspace.WorkspaceResponses.Enqueue(CreateWorkspace(fixture.Agent.Id, fixture.Session, waiting));
        await fixture.Cut.InvokeAsync(() => RecoveryWorkspace(fixture.Cut).NavigationIntent.InvokeAsync(
            new(RecoveryWorkspace(fixture.Cut).SelectionGeneration, AgentChatNavigationAction.Refresh)));
        Assert.NotEmpty(fixture.Cut.FindAll("[data-testid='agents-chat-lease-release-unconfirmed']"));
        Assert.True(fixture.Cut.Find(RecoveryButtonSelector).HasAttribute("disabled"));
        await fixture.Cut.Find(RecoveryButtonSelector).ClickAsync(new MouseEventArgs());
        Assert.Equal(4, fixture.Orchestrator.RecoveryRequests.Count);
        Assert.DoesNotContain(PrivateRecoveryFailure, fixture.Cut.Markup, StringComparison.Ordinal);
        Assert.Equal(RecoveryDraft, RecoveryWorkspace(fixture.Cut).DraftPrompt);
        Assert.Equal([RecoveryAttachment], RecoveryWorkspace(fixture.Cut).DraftAttachmentPaths);
        Assert.DoesNotContain(notifications.Messages,
            message => message.Severity == NotificationSeverity.Success && !ReferenceEquals(message, stagedAttachment));
        await using var combined = await CreateRecoveryPanelAsync();
        await ClickRecoveryAsync(combined);
        combined.Orchestrator.RecoveryCompletion.SetException(new AgentEffectReconciliationIncompleteException(
            EffectContinuation(combined.Run, AgentEffectCheckpointPersistence.Unconfirmed))
            .WithCleanupFailure(new IOException(PrivateRecoveryFailure)));
        combined.Cut.WaitForAssertion(() => {
            var text = combined.Cut.Find(ContinuationSelector).TextContent;
            Assert.Contains("At least 1 effects are known committed", text, StringComparison.Ordinal);
            Assert.Contains("checkpoint is not confirmed", text, StringComparison.Ordinal);
            Assert.NotEmpty(combined.Cut.FindAll("[data-testid='agents-chat-lease-release-unconfirmed']"));
            Assert.True(combined.Cut.Find(RecoveryButtonSelector).HasAttribute("disabled"));
            Assert.Equal(ExecutionState.Failed, RecoveryWorkspace(combined.Cut).ActiveRun!.State);
            Assert.False(RecoveryWorkspace(combined.Cut).IsBusy);
        });
        await combined.Cut.Find(RecoveryButtonSelector).ClickAsync(new MouseEventArgs());
        Assert.Single(combined.Orchestrator.RecoveryRequests);
        Assert.Equal(RecoveryDraft, RecoveryWorkspace(combined.Cut).DraftPrompt);
        Assert.DoesNotContain(PrivateRecoveryFailure, combined.Cut.Markup, StringComparison.Ordinal);
        foreach (var alreadyKnown in new[] { false, true }) {
            await using var releaseFailure = await CreateRecoveryPanelAsync();
            if (alreadyKnown) {
                await ClickRecoveryAsync(releaseFailure);
                releaseFailure.Orchestrator.RecoveryCompletion.SetException(new AgentEffectReconciliationIncompleteException(
                    EffectContinuation(releaseFailure.Run, AgentEffectCheckpointPersistence.Unconfirmed)));
                releaseFailure.Cut.WaitForAssertion(() => Assert.False(RecoveryWorkspace(releaseFailure.Cut).IsBusy));
                releaseFailure.Orchestrator.RecoveryCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            await ClickRecoveryAsync(releaseFailure);
            releaseFailure.Orchestrator.RecoveryCompletion.SetException(new AgentEffectReconciliationLeaseException(
                new AgentToolReceiptAccessDeniedException(), new TimeoutException(PrivateRecoveryFailure)));
            releaseFailure.Cut.WaitForAssertion(() => {
                Assert.NotEmpty(releaseFailure.Cut.FindAll("[data-testid='agents-chat-lease-release-unconfirmed']"));
                Assert.Equal(alreadyKnown, releaseFailure.Cut.FindAll(ContinuationSelector).Count == 1);
                if (alreadyKnown) {
                    Assert.Contains("At least 1 effects are known committed", releaseFailure.Cut.Find(ContinuationSelector).TextContent, StringComparison.Ordinal);
                    Assert.Contains("was denied", releaseFailure.Cut.Find(ContinuationSelector).TextContent, StringComparison.Ordinal);
                }
                Assert.True(releaseFailure.Cut.Find(RecoveryButtonSelector).HasAttribute("disabled"));
                Assert.False(RecoveryWorkspace(releaseFailure.Cut).IsBusy);
            });
            await releaseFailure.Cut.Find(RecoveryButtonSelector).ClickAsync(new MouseEventArgs());
            Assert.Equal(alreadyKnown ? 2 : 1, releaseFailure.Orchestrator.RecoveryRequests.Count);
            Assert.Equal(RecoveryDraft, RecoveryWorkspace(releaseFailure.Cut).DraftPrompt);
            Assert.DoesNotContain(PrivateRecoveryFailure, releaseFailure.Cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("checkpoint is confirmed", releaseFailure.Cut.Markup, StringComparison.Ordinal);
        }
        foreach (var fault in Enum.GetValues<EffectActivityFault>()) {
            await using var activityFailure = await CreateRecoveryPanelAsync();
            var activityNotifications = activityFailure.Context.Services.GetRequiredService<NotificationService>();
            var activityStagedAttachment = Assert.Single(activityNotifications.Messages,
                message => message.Severity == NotificationSeverity.Success);
            Assert.Equal("Staged 1 attachment path(s) for the next prompt.", activityStagedAttachment.Detail);
            if (fault == EffectActivityFault.Denied) {
                await ClickRecoveryAsync(activityFailure);
                activityFailure.Orchestrator.RecoveryCompletion.SetException(new AgentEffectReconciliationIncompleteException(
                    EffectContinuation(activityFailure.Run, AgentEffectCheckpointPersistence.Unconfirmed)));
                activityFailure.Cut.WaitForAssertion(() => Assert.False(RecoveryWorkspace(activityFailure.Cut).IsBusy));
                activityFailure.Orchestrator.RecoveryCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            await ClickRecoveryAsync(activityFailure);
            Assert.NotEqual(activityFailure.Orchestrator.RecoveryRequests[^1].StreamId,
                activityFailure.Orchestrator.LastStreamId);
            var accepted = fault == EffectActivityFault.Confirmed
                ? EffectContinuation(activityFailure.Run, AgentEffectCheckpointPersistence.Confirmed) : null;
            Exception? primary = fault switch {
                EffectActivityFault.IncompleteAndLeaseFailure => new AgentEffectReconciliationIncompleteException(
                    EffectContinuation(activityFailure.Run, AgentEffectCheckpointPersistence.Unconfirmed))
                    .WithCleanupFailure(new IOException(PrivateRecoveryFailure)),
                EffectActivityFault.Denied => new AgentToolReceiptAccessDeniedException(),
                EffectActivityFault.Unavailable => new AgentToolReceiptReadUnavailableException(new IOException(PrivateRecoveryFailure)),
                _ => null
            };
            activityFailure.Orchestrator.RecoveryCompletion.SetException(ActivityPublicationFailure(activityFailure, primary, accepted,
                disposalFailed: fault != EffectActivityFault.Confirmed));
            activityFailure.Cut.WaitForAssertion(() => {
                Assert.NotEmpty(activityFailure.Cut.FindAll("[data-testid='agents-chat-activity-finalization-failed']"));
                Assert.Equal(fault == EffectActivityFault.IncompleteAndLeaseFailure,
                    activityFailure.Cut.FindAll("[data-testid='agents-chat-lease-release-unconfirmed']").Count == 1);
                Assert.Equal(fault != EffectActivityFault.Unavailable, activityFailure.Cut.FindAll(ContinuationSelector).Count == 1);
                if (fault != EffectActivityFault.Unavailable) {
                    var text = activityFailure.Cut.Find(ContinuationSelector).TextContent;
                    Assert.Contains("At least 1 effects are known committed", text, StringComparison.Ordinal);
                    if (fault == EffectActivityFault.Denied) {
                        Assert.Contains("was denied", text, StringComparison.Ordinal);
                    } else if (fault == EffectActivityFault.IncompleteAndLeaseFailure) {
                        Assert.Contains("checkpoint is not confirmed", text, StringComparison.Ordinal);
                    }
                }
                Assert.True(activityFailure.Cut.Find(RecoveryButtonSelector).HasAttribute("disabled"));
                Assert.True(activityFailure.Cut.Find("[data-testid='chat-send-button']").HasAttribute("disabled"));
                Assert.Equal(ExecutionState.Failed, RecoveryWorkspace(activityFailure.Cut).ActiveRun!.State);
                if (accepted is not null) {
                    Assert.Same(accepted, activityFailure.Cut.FindComponent<AgentChatRecoverySurface>().Instance.State.EffectRecovery!.Latest);
                }
                Assert.False(RecoveryWorkspace(activityFailure.Cut).IsBusy);
            });
            Assert.Equal(1, activityFailure.Workspace.WorkspaceRequestCount);
            var requests = activityFailure.Orchestrator.RecoveryRequests.Count;
            var current = activityFailure.Run with { State = ExecutionState.WaitingOnTool };
            var snapshot = EffectContinuation(current, AgentEffectCheckpointPersistence.Confirmed);
            activityFailure.Workspace.InitialRunDetail = new(current, activityFailure.Session, [], []) {
                EffectContinuation = new(current.Id, current.ChatSessionId, 1, 1, snapshot.Requirement, snapshot.Counts, 0,
                    snapshot.Counts.EligibleOwnerEffects, AgentEffectReconciliationStop.SnapshotRead, AgentEffectCheckpointPersistence.Confirmed, [])
            };
            activityFailure.Workspace.WorkspaceResponses.Enqueue(CreateWorkspace(activityFailure.Agent.Id, activityFailure.Session, current));
            await activityFailure.Cut.InvokeAsync(() => RecoveryWorkspace(activityFailure.Cut).NavigationIntent.InvokeAsync(
                new(RecoveryWorkspace(activityFailure.Cut).SelectionGeneration, AgentChatNavigationAction.Refresh)));
            Assert.NotEmpty(activityFailure.Cut.FindAll("[data-testid='agents-chat-activity-finalization-failed']"));
            Assert.Equal(fault == EffectActivityFault.IncompleteAndLeaseFailure,
                activityFailure.Cut.FindAll("[data-testid='agents-chat-lease-release-unconfirmed']").Count == 1);
            await activityFailure.Cut.Find(RecoveryButtonSelector).ClickAsync(new MouseEventArgs());
            Assert.Equal(requests, activityFailure.Orchestrator.RecoveryRequests.Count);
            Assert.Equal(RecoveryDraft, RecoveryWorkspace(activityFailure.Cut).DraftPrompt);
            Assert.Equal([RecoveryAttachment], RecoveryWorkspace(activityFailure.Cut).DraftAttachmentPaths);
            Assert.DoesNotContain(PrivateRecoveryFailure, activityFailure.Cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(activityNotifications.Messages,
                message => message.Severity == NotificationSeverity.Success && !ReferenceEquals(message, activityStagedAttachment));
        }
        await using var ordinary = await CreateRecoveryPanelAsync();
        await ClickRecoveryAsync(ordinary);
        ordinary.Orchestrator.RecoveryCompletion.SetException(new AgentExecutionActivityPublicationException(
            ordinary.Orchestrator.LastStreamId!, AgentExecutionActivityPhase.Failed, new IOException(PrivateRecoveryFailure)));
        ordinary.Cut.WaitForAssertion(() => {
            Assert.False(RecoveryWorkspace(ordinary.Cut).IsBusy);
            Assert.Empty(ordinary.Cut.FindAll("[data-testid='agents-chat-activity-finalization-failed']"));
            Assert.Empty(ordinary.Cut.FindAll("[data-testid='agents-chat-lease-release-unconfirmed']"));
            Assert.Empty(ordinary.Cut.FindAll(ContinuationSelector));
        });
    }

    [Fact]
    public async Task Native_recovery_keeps_original_draft_and_profile_generation_fence() {
        await using var fixture = await CreateRecoveryPanelAsync(floating: true);
        var composer = RecoveryWorkspace(fixture.Cut).ComposerKey;
        var originalStream = fixture.Orchestrator.LastStreamId!;
        var continuation = EffectContinuation(fixture.Run, AgentEffectCheckpointPersistence.Confirmed);
        var waiting = fixture.Run with { State = ExecutionState.WaitingOnTool, CompletedAtUtc = null };
        fixture.Workspace.InitialRunDetail = new(waiting, fixture.Session, [], []) { EffectContinuation = continuation };
        fixture.Workspace.WorkspaceResponses.Enqueue(CreateWorkspace(fixture.Agent.Id, fixture.Session, waiting));
        await ClickRecoveryAsync(fixture);
        fixture.Orchestrator.RecoveryCompletion.SetResult(ExecutionRunResult.ForEffectContinuation(continuation));
        fixture.Cut.WaitForAssertion(() => {
            Assert.False(RecoveryWorkspace(fixture.Cut).IsBusy);
            Assert.Equal("Awaiting continuation", RecoveryWorkspace(fixture.Cut).RunStateText);
            Assert.False(fixture.Cut.Find(RecoveryButtonSelector).HasAttribute("disabled"));
            Assert.Equal(RecoveryDraft, RecoveryWorkspace(fixture.Cut).DraftPrompt);
            Assert.Equal([RecoveryAttachment], RecoveryWorkspace(fixture.Cut).DraftAttachmentPaths);
            Assert.Equal(composer, RecoveryWorkspace(fixture.Cut).ComposerKey);
            Assert.Equal(ActiveAgentChatRunState.AwaitingContinuation, Assert.Single(fixture.Coordinator!.Snapshot().ActiveChats).RunState);
        });
        Assert.Single(fixture.Orchestrator.RecoveryRequests);
        await fixture.Cut.InvokeAsync(() => fixture.Cut.Render(parameters =>
            parameters.Add(component => component.PersistedActiveChatRunState, ActiveAgentChatRunState.AwaitingContinuation)));
        const string newerDraft = "Keep the new draft while checking receipts.";
        await fixture.Cut.InvokeAsync(() => RecoveryWorkspace(fixture.Cut).DraftPromptChanged.InvokeAsync(newerDraft));
        fixture.Orchestrator.RecoveryCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await ClickRecoveryAsync(fixture);
        Assert.Equal(2, fixture.Orchestrator.RecoveryRequests.Count);
        Assert.All(fixture.Orchestrator.RecoveryRequests, item => Assert.Equal(originalStream, item.StreamId));
        var reads = fixture.Workspace.WorkspaceRequestCount;
        fixture.Generation.Value = new(1);
        fixture.Orchestrator.RecoveryCompletion.SetResult(ExecutionRunResult.ForEffectContinuation(continuation));
        await fixture.Logger.Detached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await fixture.Cut.InvokeAsync(() => fixture.Cut.Render());
        Assert.Equal(reads, fixture.Workspace.WorkspaceRequestCount);
        Assert.Equal(newerDraft, RecoveryWorkspace(fixture.Cut).DraftPrompt);
        Assert.Equal([RecoveryAttachment], RecoveryWorkspace(fixture.Cut).DraftAttachmentPaths);
        Assert.True(fixture.Cut.Find(RecoveryButtonSelector).HasAttribute("disabled"));
        Assert.Single(fixture.Orchestrator.SendRequests);
    }

    [Fact]
    public async Task Voice_and_scenario_surfaces_require_actual_assistant_output() {
        var agent = CreateAgent() with { ConfigurationJson = AgentVoiceAccessMetadata.Write("{}", new() { CanUseVoiceMode = true }) };
        var session = CreateSession(agent.Id);
        var waiting = CreateRunningRun(agent.Id, session.Id) with { State = ExecutionState.WaitingOnTool };
        var continuation = EffectContinuation(waiting, AgentEffectCheckpointPersistence.Confirmed);
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, DeferredWorkspaceProxy>();
        var reads = (DeferredWorkspaceProxy)(object)service;
        reads.Service = service;
        reads.Agents = [agent];
        reads.InitialWorkspace = CreateWorkspace(agent.Id, session);
        reads.InitialRunDetail = new(waiting, session, [], []) { EffectContinuation = continuation };
        reads.WorkspaceResponses.Enqueue(CreateWorkspace(agent.Id, session, waiting));
        var orchestratorService = DispatchProxy.Create<IAgentChatExecutionOrchestrator, CompletedRunOrchestratorProxy>();
        var orchestrator = (CompletedRunOrchestratorProxy)(object)orchestratorService;
        orchestrator.SendCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var context = CreateContext(service, orchestratorService);
        var voiceService = DispatchProxy.Create<IAgentVoiceService, NoContinuationSpeechProxy>();
        var voice = (NoContinuationSpeechProxy)(object)voiceService;
        context.Services.AddSingleton(voiceService);
        var cut = context.Render<AgentChatPanel>(parameters => parameters
            .Add(component => component.PreferredAgentId, agent.Id)
            .Add(component => component.PreferredSessionId, session.Id));
        cut.WaitForElement("[data-testid='chat-send-button']");
        await cut.InvokeAsync(() => RecoveryWorkspace(cut).VoiceModeChanged.InvokeAsync(true));
        Assert.True(RecoveryWorkspace(cut).IsVoiceModeEnabled);
        await cut.InvokeAsync(() => RecoveryWorkspace(cut).DraftPromptChanged.InvokeAsync("Use the original effects."));
        await cut.Find("[data-testid='chat-send-button']").ClickAsync(new MouseEventArgs());
        var result = AgentChatRunResult.ForEffectContinuation(continuation);
        Assert.Null(result.AssistantMessage);
        Assert.Null(result.Metric);
        orchestrator.SendCompletion.SetResult(result);
        cut.WaitForAssertion(() => {
            Assert.False(RecoveryWorkspace(cut).IsBusy);
            Assert.NotEmpty(cut.FindAll(ContinuationSelector));
        });
        Assert.Equal(0, voice.Calls);
        Assert.DoesNotContain(context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Severity is NotificationSeverity.Success or NotificationSeverity.Error);
        Assert.Equal("warning", AgentScenarioCompletionPresentation.From(waiting, continuation, false, "older response").Tone);
        Assert.Equal("info", AgentScenarioCompletionPresentation.From(waiting with { State = ExecutionState.Completed }, null, false, null).Tone);
        Assert.Equal("success", AgentScenarioCompletionPresentation.From(waiting with { State = ExecutionState.Completed }, null, false, "Actual response").Tone);
    }

    [Fact]
    public async Task Awaiting_continuation_never_renders_as_approval_or_success() {
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var run = CreateRunningRun(Guid.NewGuid(), Guid.NewGuid()) with { State = ExecutionState.WaitingOnTool };
        var continuation = EffectContinuation(run, AgentEffectCheckpointPersistence.Confirmed);
        var origin = new AgentChatActionOrigin(1, run.AgentId, run.ChatSessionId, run.Id);
        var state = new AgentChatActionState(origin, null, false, false, false, false, false, true, true) {
            EffectRecovery = AgentChatEffectRecovery.Observe(null, continuation)
        };
        var intents = new List<AgentChatActionIntent>();
        var cut = context.Render<AgentChatRecoverySurface>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.Intent, intent => intents.Add(intent)));
        Assert.Contains("awaiting explicit continuation", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("original execution context is not retained", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Reviewing saved receipts does not authorize a new recovery attempt", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(RecoveryButtonSelector));
        Assert.Empty(intents);
        cut.Render(parameters => parameters.Add(component => component.State, state with { CanRecover = true }));
        await cut.Find(RecoveryButtonSelector).ClickAsync(new MouseEventArgs());
        Assert.Equal(new AgentChatActionIntent(origin, AgentChatAction.RecoverOriginalRun), Assert.Single(intents));
        Assert.DoesNotContain("Waiting on approval", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("rz-alert-success", cut.Markup, StringComparison.Ordinal);
        var currentRead = new AgentEffectContinuation(run.Id, run.ChatSessionId, 1, 1, continuation.Requirement,
            continuation.Counts, 0, continuation.Counts.EligibleOwnerEffects, AgentEffectReconciliationStop.SnapshotRead,
            AgentEffectCheckpointPersistence.Confirmed, []);
        cut.Render(parameters => parameters.Add(component => component.State, state with {
            EffectRecovery = AgentChatEffectRecovery.Observe(state.EffectRecovery, currentRead)
        }));
        Assert.Contains("No receipt lookup or continuation was performed", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("awaiting explicit continuation", cut.Markup, StringComparison.Ordinal);
        Assert.Single(intents);
    }

    private static AgentEffectContinuation EffectContinuation(ExecutionRunRecord run, AgentEffectCheckpointPersistence persistence) {
        var digest = AgentToolProtocolEnvelope.Create("test-recovery", 1, "{}").Digest;
        var confirmed = persistence == AgentEffectCheckpointPersistence.Confirmed;
        return new(run.Id, run.ChatSessionId, 1, 1, AgentToolContinuationRequirement.EffectReconciliationRequired,
            new(3, confirmed ? 1 : 0, confirmed ? 1 : 2, 1, 0, 0, 1), 1, 1,
            confirmed ? AgentEffectReconciliationStop.LookupLimit : AgentEffectReconciliationStop.CheckpointFailure,
            persistence, [new(new(new(Guid.NewGuid()), new(Guid.NewGuid()), 1, digest),
                AgentEffectReceiptKnowledge.ReceiptConfirmed, confirmed)]);
    }

    private static AgentExecutionActivityPublicationException ActivityPublicationFailure(RecoveryPanelFixture fixture,
        Exception? primary, AgentEffectContinuation? continuation, bool disposalFailed) {
        var coordinator = new AgentExecutionActivityCoordinator(
            new PartitionedSequencedStream<AgentExecutionActivityStreamId, AgentExecutionActivity>(
                PartitionedSequencedStreamPolicy.Default, TimeProvider.System), TimeProvider.System);
        using var operation = Assert.IsType<AgentExecutionActivityAdmitted>(coordinator.AdmitOperation(
            fixture.Orchestrator.LastStreamId!, fixture.Agent.Id, fixture.Session.Id, "Testing activity finalization.")).Operation;
        return AgentExecutionActivityPublicationException.PreserveEffectOutcome(operation, primary,
            new IOException(PrivateRecoveryFailure), continuation, disposalFailed);
    }

    private enum EffectActivityFault { Confirmed, IncompleteAndLeaseFailure, Denied, Unavailable }

    public class NoContinuationSpeechProxy : DispatchProxy {
        public int Calls { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            Calls++;
            throw new InvalidOperationException("Receipt-only continuation must not invoke voice output.");
        }
    }
}
