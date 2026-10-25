using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Chat;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed partial class AgentChatPanelResponsivenessTests {
    private const string HistoricalReviewSelector = "[data-testid='agents-chat-review-saved-receipts']";
    private const string HistoricalNextSelector = "[data-testid='agents-chat-next-saved-receipts']";
    private const string HistoricalObservationSelector = "[data-testid='agents-chat-historical-observation']";

    [Fact]
    public async Task Saved_receipt_review_retains_original_selection_after_detail_failure_and_pages_only_on_explicit_intent() {
        await using var fixture = CreateHistoricalPanel(detailUnavailable: true);
        await OpenSavedReceiptDetailsAsync(fixture);
        var reader = Assert.IsAssignableFrom<HistoricalReviewOrchestratorProxy>(fixture.Orchestrator);
        fixture.Cut.WaitForAssertion(() => Assert.False(fixture.Cut.Find(HistoricalReviewSelector).HasAttribute("disabled")));
        Assert.Single(fixture.Workspace.ExecutionRunDetailRequests);
        Assert.Empty(reader.Requests);
        Assert.Null(RecoveryWorkspace(fixture.Cut).ActiveRun);
        await fixture.Cut.InvokeAsync(() => RecoveryWorkspace(fixture.Cut).DraftPromptChanged.InvokeAsync(RecoveryDraft));
        var composer = RecoveryWorkspace(fixture.Cut).ComposerKey;
        AgentHistoricalReceiptCursor? cursor = null;
        reader.Read = (request, _) => {
            Assert.Equal(fixture.Run.Id, request.ExecutionRunId);
            Assert.Equal(fixture.Run.CreatedAtUtc, request.ExecutionCreatedAtUtc);
            Assert.Equal(fixture.Agent.Id, request.AgentId);
            Assert.Equal(fixture.Session.Id, request.ChatSessionId);
            Assert.Null(request.Cursor);
            var context = new AgentHistoricalReceiptReadContext(Guid.NewGuid(), fixture.Generation.Value, WorkspaceScopeDescriptor.Sandbox);
            cursor = new(request.ExecutionRunId, 8, 5, 4, context);
            return Task.FromResult(new AgentHistoricalReceiptReview(request, context, 8, 5,
                AgentHistoricalReceiptReviewStop.LookupLimit,
                Enumerable.Range(0, 4).Select(_ => HistoricalObservation(AgentHistoricalReceiptKnowledge.Accepted)).ToArray(), cursor));
        };
        await fixture.Cut.Find(HistoricalReviewSelector).ClickAsync(new MouseEventArgs());
        Assert.Equal(4, fixture.Cut.FindAll(HistoricalObservationSelector).Count);
        Assert.Single(reader.Requests);
        var firstPageSurface = fixture.Cut.FindComponent<AgentChatRecoverySurface>().Instance;
        var staleNext = new AgentChatActionIntent(firstPageSurface.State.Origin, AgentChatAction.ReadNextSavedReceipts) {
            HistoricalRequest = new(fixture.Run.Id, fixture.Session.Id, fixture.Agent.Id, fixture.Run.CreatedAtUtc, cursor)
        };
        foreach (var foreignContext in new[] { false, true }) {
            reader.Read = (request, _) => Task.FromResult(new AgentHistoricalReceiptReview(request,
                foreignContext ? new(Guid.NewGuid(), fixture.Generation.Value, WorkspaceScopeDescriptor.Sandbox) : cursor!.CurrentContext,
                foreignContext ? 8 : 9, 5, AgentHistoricalReceiptReviewStop.ExhaustedSnapshot,
                [HistoricalObservation(AgentHistoricalReceiptKnowledge.Accepted)]));
            await fixture.Cut.Find(HistoricalNextSelector).ClickAsync(new MouseEventArgs());
            Assert.NotEmpty(fixture.Cut.FindAll("[data-testid='agents-chat-historical-read-error']"));
            Assert.Equal(4, fixture.Cut.FindAll(HistoricalObservationSelector).Count);
            Assert.Same(cursor, fixture.Cut.FindComponent<AgentChatRecoverySurface>().Instance.State.HistoricalReceipts!.Page!.NextCursor);
        }
        reader.Read = (request, _) => {
            Assert.Same(cursor, request.Cursor);
            return Task.FromResult(new AgentHistoricalReceiptReview(request, cursor!.CurrentContext, 8, 5,
                AgentHistoricalReceiptReviewStop.ExhaustedSnapshot, [HistoricalObservation(AgentHistoricalReceiptKnowledge.NotObserved)]));
        };
        await fixture.Cut.Find(HistoricalNextSelector).ClickAsync(new MouseEventArgs());
        Assert.Equal(4, reader.Requests.Count);
        Assert.Contains("outcome remains unresolved", Assert.Single(fixture.Cut.FindAll(HistoricalObservationSelector)).TextContent, StringComparison.Ordinal);
        Assert.Empty(fixture.Cut.FindAll(HistoricalNextSelector));
        await fixture.Cut.InvokeAsync(() => firstPageSurface.Intent.InvokeAsync(staleNext));
        Assert.Equal(4, reader.Requests.Count);
        reader.Read = (request, _) => {
            Assert.Null(request.Cursor);
            return Task.FromResult(new AgentHistoricalReceiptReview(request, cursor!.CurrentContext, null, null,
                AgentHistoricalReceiptReviewStop.SnapshotChanged, []));
        };
        await fixture.Cut.Find(HistoricalReviewSelector).ClickAsync(new MouseEventArgs());
        Assert.Equal(5, reader.Requests.Count);
        Assert.Empty(fixture.Cut.FindAll(HistoricalObservationSelector));
        Assert.Contains("pages were not combined", fixture.Cut.Markup, StringComparison.Ordinal);
        reader.Read = (request, _) => Task.FromResult(new AgentHistoricalReceiptReview(request, cursor!.CurrentContext, null, null,
            AgentHistoricalReceiptReviewStop.ContextChanged, []));
        await fixture.Cut.Find(HistoricalReviewSelector).ClickAsync(new MouseEventArgs());
        Assert.Equal(6, reader.Requests.Count);
        Assert.True(fixture.Cut.Find(HistoricalReviewSelector).HasAttribute("disabled"));
        Assert.Equal(RecoveryDraft, RecoveryWorkspace(fixture.Cut).DraftPrompt);
        Assert.Equal(composer, RecoveryWorkspace(fixture.Cut).ComposerKey);
        Assert.Empty(reader.SendRequests);
        Assert.Empty(reader.RecoveryRequests);
        Assert.False(reader.ApprovalStarted.Task.IsCompleted);
        Assert.Empty(fixture.Cut.FindAll(ContinuationSelector));
        Assert.DoesNotContain(fixture.Context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Severity == NotificationSeverity.Success);
    }

    [Fact]
    public async Task Saved_receipt_review_preserves_uncertainty_and_discards_late_results_outside_its_current_view() {
        await using (var fixture = CreateHistoricalPanel(detailUnavailable: false)) {
            var reader = Assert.IsAssignableFrom<HistoricalReviewOrchestratorProxy>(fixture.Orchestrator);
            fixture.Cut.WaitForElement("[data-testid='chat-send-button']");
            await fixture.Cut.InvokeAsync(() => RecoveryWorkspace(fixture.Cut).AttachmentFilesSelected.InvokeAsync(
                new InputFileChangeEventArgs([new RecoveryImageFile()])));
            await fixture.Cut.InvokeAsync(() => RecoveryWorkspace(fixture.Cut).DraftPromptChanged.InvokeAsync(RecoveryDraft));
            await fixture.Cut.Find("[data-testid='chat-send-button']").ClickAsync(new MouseEventArgs());
            await reader.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            reader.SendCompletion!.SetException(new AgentChatSessionBlockedException(fixture.Agent.Id, fixture.Session.Id,
                fixture.Run.Id, AgentChatSessionBlockReason.UnresolvedEffects));
            fixture.Cut.WaitForAssertion(() => Assert.False(fixture.Cut.Find(RecoveryButtonSelector).HasAttribute("disabled")));
            await ClickRecoveryAsync(fixture);
            var continuation = EffectContinuation(fixture.Run, AgentEffectCheckpointPersistence.Unconfirmed);
            var incomplete = new AgentEffectReconciliationIncompleteException(continuation)
                .WithCleanupFailure(new IOException(PrivateRecoveryFailure));
            reader.RecoveryCompletion.SetException(ActivityPublicationFailure(fixture, incomplete, null, disposalFailed: true));
            fixture.Cut.WaitForAssertion(() => {
                Assert.False(RecoveryWorkspace(fixture.Cut).IsBusy);
                Assert.True(fixture.Cut.Find(RecoveryButtonSelector).HasAttribute("disabled"));
            });
            var known = fixture.Cut.FindComponent<AgentChatRecoverySurface>().Instance.State.EffectRecovery;
            reader.Read = (request, _) => Task.FromResult(HistoricalAccepted(request, fixture.Generation.Value));
            await OpenSavedReceiptDetailsAsync(fixture);
            await fixture.Cut.Find(HistoricalReviewSelector).ClickAsync(new MouseEventArgs());
            Assert.Single(reader.Requests);
            Assert.Single(fixture.Cut.FindAll(HistoricalObservationSelector));
            Assert.Same(known, fixture.Cut.FindComponent<AgentChatRecoverySurface>().Instance.State.EffectRecovery);
            Assert.NotEmpty(fixture.Cut.FindAll("[data-testid='agents-chat-lease-release-unconfirmed']"));
            Assert.NotEmpty(fixture.Cut.FindAll("[data-testid='agents-chat-activity-finalization-failed']"));
            Assert.True(fixture.Cut.Find(RecoveryButtonSelector).HasAttribute("disabled"));
            Assert.True(fixture.Cut.Find("[data-testid='chat-send-button']").HasAttribute("disabled"));
            Assert.Equal(ExecutionState.Failed, RecoveryWorkspace(fixture.Cut).ActiveRun!.State);
            Assert.Equal(RecoveryDraft, RecoveryWorkspace(fixture.Cut).DraftPrompt);
            Assert.Equal([RecoveryAttachment], RecoveryWorkspace(fixture.Cut).DraftAttachmentPaths);
            Assert.Single(reader.RecoveryRequests);
            Assert.Single(reader.SendRequests);
            Assert.False(reader.ApprovalStarted.Task.IsCompleted);
        }
        foreach (var detach in new[] { RecoveryDetach.SwitchThread, RecoveryDetach.ProfileBeforeCompletion, RecoveryDetach.Dispose }) {
            await using var fixture = CreateHistoricalPanel(detailUnavailable: false);
            var reader = Assert.IsAssignableFrom<HistoricalReviewOrchestratorProxy>(fixture.Orchestrator);
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var completion = new TaskCompletionSource<AgentHistoricalReceiptReview>(TaskCreationOptions.RunContinuationsAsynchronously);
            reader.Read = (_, _) => {
                started.TrySetResult();
                return completion.Task;
            };
            await OpenSavedReceiptDetailsAsync(fixture);
            fixture.Cut.WaitForAssertion(() => Assert.False(fixture.Cut.Find(HistoricalReviewSelector).HasAttribute("disabled")));
            var pending = fixture.Cut.Find(HistoricalReviewSelector).ClickAsync(new MouseEventArgs());
            try {
                await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var original = Assert.Single(reader.Requests);
                Assert.True(fixture.Cut.Find(HistoricalReviewSelector).HasAttribute("disabled"));
                if (detach == RecoveryDetach.SwitchThread) {
                    var other = CreateSession(fixture.Agent.Id);
                    fixture.Workspace.WorkspaceResponses.Enqueue(CreateWorkspace(fixture.Agent.Id, other));
                    await RerenderFocusedChatAsync(fixture.Cut, fixture.Agent, other);
                    await fixture.Cut.InvokeAsync(() => RecoveryWorkspace(fixture.Cut).DraftPromptChanged.InvokeAsync("New thread draft"));
                    Assert.True(original.Cancellation.IsCancellationRequested);
                } else if (detach == RecoveryDetach.Dispose) {
                    await fixture.Cut.InvokeAsync(() => fixture.Cut.Instance.DisposeAsync().AsTask());
                    Assert.True(original.Cancellation.IsCancellationRequested);
                } else {
                    fixture.Generation.Value = new(2);
                }
                completion.SetResult(HistoricalAccepted(original.Request, new(0)));
                await pending.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Single(reader.Requests);
                Assert.Empty(reader.SendRequests);
                Assert.Empty(reader.RecoveryRequests);
                if (detach != RecoveryDetach.Dispose) {
                    await fixture.Cut.InvokeAsync(() => fixture.Cut.Render());
                    Assert.Empty(fixture.Cut.FindAll(HistoricalObservationSelector));
                    if (detach == RecoveryDetach.SwitchThread) {
                        Assert.Equal("New thread draft", RecoveryWorkspace(fixture.Cut).DraftPrompt);
                        Assert.Empty(fixture.Cut.FindAll(HistoricalReviewSelector));
                    } else {
                        Assert.True(fixture.Cut.Find(HistoricalReviewSelector).HasAttribute("disabled"));
                    }
                }
            } finally {
                completion.TrySetCanceled();
                await pending;
            }
        }
    }

    private static async Task OpenSavedReceiptDetailsAsync(RecoveryPanelFixture fixture) {
        fixture.Cut.WaitForAssertion(() => Assert.NotNull(
            fixture.Cut.FindComponent<AgentChatRecoverySurface>().Instance.State.HistoricalReceipts));
        if (fixture.Cut.FindAll(HistoricalReviewSelector).Count > 0) {
            return;
        }
        fixture.Cut.WaitForElement("[data-testid='chat-conversation-details']");
        await fixture.Cut.InvokeAsync(() => fixture.Cut.Find("[data-testid='chat-conversation-details']").ClickAsync());
    }

    private static RecoveryPanelFixture CreateHistoricalPanel(bool detailUnavailable) {
        var agent = CreateAgent();
        var session = CreateSession(agent.Id);
        var run = CreateRunningRun(agent.Id, session.Id) with { State = ExecutionState.Failed, PendingApprovals = [] };
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, HistoricalWorkspaceProxy>();
        var workspace = (HistoricalWorkspaceProxy)(object)service;
        workspace.Service = service;
        workspace.Agents = [agent];
        workspace.InitialWorkspace = CreateWorkspace(agent.Id, session, run);
        workspace.InitialRunDetail = new(run, session, [], []);
        workspace.DetailUnavailable = detailUnavailable;
        var orchestratorService = DispatchProxy.Create<IAgentChatExecutionOrchestrator, HistoricalReviewOrchestratorProxy>();
        var orchestrator = (HistoricalReviewOrchestratorProxy)(object)orchestratorService;
        orchestrator.SendCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = CreateContext(service, orchestratorService);
        try {
            var generation = new RecoveryPanelGeneration();
            var logger = new RecoveryPanelLogger();
            context.Services.AddSingleton<IAgentExecutionProfileGenerationSource>(generation);
            context.Services.AddSingleton<ILogger<AgentChatPanel>>(logger);
            var staging = DispatchProxy.Create<IAgentChatAttachmentStagingService, AgentChatEffectOwnershipTests.EffectsProxy>();
            ((AgentChatEffectOwnershipTests.EffectsProxy)(object)staging).Upload.SetResult(new(RecoveryAttachment, "image/png", 3));
            context.Services.AddSingleton(staging);
            return new(context, RenderFocusedChat(context, agent, session), agent, session, run, workspace, orchestrator,
                generation, logger, null, null);
        } catch {
            context.Dispose();
            throw;
        }
    }

    private static AgentHistoricalReceiptReview HistoricalAccepted(AgentHistoricalReceiptReviewRequest request, DatabaseProfileGeneration generation)
        => new(request, new(Guid.NewGuid(), generation, WorkspaceScopeDescriptor.Sandbox), 8, 5,
            AgentHistoricalReceiptReviewStop.ExhaustedSnapshot, [HistoricalObservation(AgentHistoricalReceiptKnowledge.Accepted)]);

    private static AgentHistoricalReceiptObservation HistoricalObservation(AgentHistoricalReceiptKnowledge knowledge)
        => new(new(new(Guid.NewGuid()), new(Guid.NewGuid()), 1, AgentToolProtocolEnvelope.Create("test-historical-read", 1, "{}").Digest), knowledge);

    private class HistoricalWorkspaceProxy : DeferredWorkspaceProxy {
        public bool DetailUnavailable { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (DetailUnavailable && targetMethod?.Name == nameof(IAgentFrameworkWorkspaceService.GetExecutionRunDetailAsync)) {
                ExecutionRunDetailRequests.Add(Assert.IsType<Guid>(args![0]));
                return Task.FromException<ExecutionRunDetail>(new InvalidOperationException("Original generation is unavailable in ordinary detail."));
            }
            return base.Invoke(targetMethod, args);
        }
    }

    private class HistoricalReviewOrchestratorProxy : CompletedRunOrchestratorProxy {
        public List<(AgentHistoricalReceiptReviewRequest Request, CancellationToken Cancellation)> Requests { get; } = [];
        public Func<AgentHistoricalReceiptReviewRequest, CancellationToken, Task<AgentHistoricalReceiptReview>> Read { get; set; }
            = (_, _) => throw new InvalidOperationException("Historical review must be explicitly arranged.");
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod?.Name != nameof(IAgentChatExecutionOrchestrator.ReadSavedEffectReceiptsAsync)) {
                return base.Invoke(targetMethod, args);
            }
            var request = Assert.IsType<AgentHistoricalReceiptReviewRequest>(args![0]);
            var cancellation = Assert.IsType<CancellationToken>(args[1]);
            Requests.Add((request, cancellation));
            return Read(request, cancellation);
        }
    }
}
