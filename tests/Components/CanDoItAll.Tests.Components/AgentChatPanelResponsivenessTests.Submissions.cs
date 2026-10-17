using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed partial class AgentChatPanelResponsivenessTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Definite_admission_conflict_retains_original_attachments_without_overwriting_later_typing(bool laterTyping) {
        var agent = CreateAgent();
        var session = CreateSession(agent.Id);
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, DeferredWorkspaceProxy>();
        var reads = (DeferredWorkspaceProxy)(object)service;
        reads.Service = service;
        reads.Agents = [agent];
        reads.InitialWorkspace = CreateWorkspace(agent.Id, session);
        var orchestratorService = DispatchProxy.Create<IAgentChatExecutionOrchestrator, CompletedRunOrchestratorProxy>();
        var owner = (CompletedRunOrchestratorProxy)(object)orchestratorService;
        var completion = new TaskCompletionSource<AgentChatRunResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.SendCompletion = completion;
        using var context = CreateContext(service, orchestratorService);
        var staging = DispatchProxy.Create<IAgentChatAttachmentStagingService, AgentChatEffectOwnershipTests.EffectsProxy>();
        ((AgentChatEffectOwnershipTests.EffectsProxy)(object)staging).Upload.SetResult(new("uploads/original.png", "image/png", 3));
        context.Services.AddSingleton(staging);
        var cut = context.Render<AgentChatPanel>(parameters => parameters
            .Add(component => component.PreferredAgentId, agent.Id)
            .Add(component => component.PreferredSessionId, session.Id));
        var retention = context.Services.GetRequiredService<AgentChatSubmissionRetention>();
        Task? submittedOperation = null;
        Exception? primary = null;
        try {
            await cut.InvokeAsync(() => cut.FindComponent<ChatWorkspacePanel>().Instance.AttachmentFilesSelected
                .InvokeAsync(new InputFileChangeEventArgs([new SubmissionFile()])));
            cut.Find("[data-testid='chat-prompt-input']").Input("Original submitted text");
            await cut.Find("[data-testid='chat-send-button']").ClickAsync(new MouseEventArgs());
            submittedOperation = CaptureSubmittedOperation(cut.Instance);
            await owner.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Empty(cut.FindComponent<ChatWorkspacePanel>().Instance.DraftAttachmentPaths);
            if (laterTyping) {
                cut.Find("[data-testid='chat-prompt-input']").Input("Newer unsent text");
            }
            completion.SetException(new AgentChatAdmissionConflictException(agent.Id, session.Id, new SandboxWorkspaceConcurrencyException(2, 3)));
            cut.WaitForAssertion(() => {
                var panel = cut.FindComponent<ChatWorkspacePanel>().Instance;
                Assert.False(panel.IsBusy);
                Assert.Null(panel.ActiveRun);
                Assert.Empty(panel.Session!.Messages);
                Assert.Equal(laterTyping ? "Newer unsent text" : "Original submitted text", panel.DraftPrompt);
                Assert.Single(owner.SendRequests);
                if (laterTyping) {
                    var retained = Assert.Single(retention.Snapshot());
                    Assert.Equal(AgentChatSubmissionState.NotSent, retained.State);
                    Assert.Equal("Original submitted text", retained.Draft);
                    Assert.Equal(new[] { "uploads/original.png" }, retained.AttachmentPaths);
                    Assert.Equal(owner.LastStreamId, retained.Stream);
                    Assert.True(cut.Find("[data-testid='agents-chat-restore-submission']").HasAttribute("disabled"));
                } else {
                    Assert.Empty(retention.Snapshot());
                    Assert.Equal(new[] { "uploads/original.png" }, panel.DraftAttachmentPaths);
                }
            });
            if (laterTyping) {
                cut.Find("[data-testid='chat-prompt-input']").Input("");
                await cut.Find("[data-testid='agents-chat-restore-submission']").ClickAsync(new MouseEventArgs());
                Assert.Equal("Original submitted text", cut.FindComponent<ChatWorkspacePanel>().Instance.DraftPrompt);
                Assert.Equal(new[] { "uploads/original.png" }, cut.FindComponent<ChatWorkspacePanel>().Instance.DraftAttachmentPaths);
                Assert.Empty(retention.Snapshot());
                Assert.Single(owner.SendRequests);
            }
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            completion.TrySetCanceled();
            await DrainSubmittedOperationAsync(submittedOperation, primary);
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    public async Task Retired_or_disposed_panel_keeps_original_submission_knowledge_without_retargeting_it(bool dispose, bool changeProfile, bool savedThenFailed) {
        var agent = CreateAgent();
        var original = CreateSession(agent.Id);
        var successor = changeProfile ? original : CreateSession(agent.Id);
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, DeferredWorkspaceProxy>();
        var reads = (DeferredWorkspaceProxy)(object)service;
        reads.Service = service;
        reads.Agents = [agent];
        reads.InitialWorkspace = CreateWorkspace(agent.Id, original);
        var orchestratorService = DispatchProxy.Create<IAgentChatExecutionOrchestrator, CompletedRunOrchestratorProxy>();
        var owner = (CompletedRunOrchestratorProxy)(object)orchestratorService;
        var completion = new TaskCompletionSource<AgentChatRunResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.SendCompletion = completion;
        using var context = CreateContext(service, orchestratorService);
        var generation = new RecoveryPanelGeneration();
        context.Services.AddSingleton<IAgentExecutionProfileGenerationSource>(generation);
        var retention = context.Services.GetRequiredService<AgentChatSubmissionRetention>();
        var cut = context.Render<AgentChatPanel>(parameters => parameters
            .Add(component => component.PreferredAgentId, agent.Id)
            .Add(component => component.PreferredSessionId, original.Id));
        Task? submittedOperation = null;
        Exception? primary = null;
        try {
            cut.Find("[data-testid='chat-prompt-input']").Input("Original retained target");
            await cut.Find("[data-testid='chat-send-button']").ClickAsync(new MouseEventArgs());
            submittedOperation = CaptureSubmittedOperation(cut.Instance);
            await owner.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            reads.WorkspaceResponses.Enqueue(CreateWorkspace(agent.Id, successor));
            if (dispose) {
                await cut.Instance.DisposeAsync();
                cut.Dispose();
                cut = context.Render<AgentChatPanel>(parameters => parameters
                    .Add(component => component.PreferredAgentId, agent.Id)
                    .Add(component => component.PreferredSessionId, successor.Id));
            } else {
                await cut.InvokeAsync(() => cut.Render(parameters => parameters
                    .Add(component => component.PreferredAgentId, agent.Id)
                    .Add(component => component.PreferredSessionId, successor.Id)));
            }
            cut.Find("[data-testid='chat-prompt-input']").Input("Successor draft");
            if (changeProfile) {
                generation.Value = new(2);
            }
            completion.SetException(savedThenFailed
                ? new AgentChatRunFailedException(agent.Id, Guid.NewGuid(), original.Id, "provider", "model", new IOException("runtime"), "The run failed.")
                : new AgentChatAdmissionConflictException(agent.Id, original.Id, new SandboxWorkspaceConcurrencyException(2, 3)));
            cut.WaitForAssertion(() => {
                if (savedThenFailed) {
                    Assert.Empty(retention.Snapshot());
                } else {
                    var retained = Assert.Single(retention.Snapshot());
                    Assert.Equal(AgentChatSubmissionState.NotSent, retained.State);
                    Assert.Equal(original.Id, retained.SessionId);
                    Assert.Equal(new DatabaseProfileGeneration(0), retained.ProfileGeneration);
                    Assert.Equal(owner.LastStreamId, retained.Stream);
                    Assert.True(cut.Find("[data-testid='agents-chat-restore-submission']").HasAttribute("disabled"));
                }
                Assert.Equal("Successor draft", cut.FindComponent<ChatWorkspacePanel>().Instance.DraftPrompt);
                Assert.Equal(successor.Id, cut.FindComponent<ChatWorkspacePanel>().Instance.Session!.Id);
                Assert.Single(owner.SendRequests);
                Assert.Null(cut.FindComponent<ChatWorkspacePanel>().Instance.ActiveRun);
            });
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            completion.TrySetCanceled();
            await DrainSubmittedOperationAsync(submittedOperation, primary);
        }
    }

    [Fact]
    public async Task Unconfirmed_completion_is_retained_and_never_restored_or_automatically_dispatched() {
        var agent = CreateAgent();
        var session = CreateSession(agent.Id);
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, DeferredWorkspaceProxy>();
        var reads = (DeferredWorkspaceProxy)(object)service;
        reads.Service = service;
        reads.Agents = [agent];
        reads.InitialWorkspace = CreateWorkspace(agent.Id, session);
        var orchestratorService = DispatchProxy.Create<IAgentChatExecutionOrchestrator, CompletedRunOrchestratorProxy>();
        var owner = (CompletedRunOrchestratorProxy)(object)orchestratorService;
        var completion = new TaskCompletionSource<AgentChatRunResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.SendCompletion = completion;
        using var context = CreateContext(service, orchestratorService);
        var cut = context.Render<AgentChatPanel>(parameters => parameters
            .Add(component => component.PreferredAgentId, agent.Id)
            .Add(component => component.PreferredSessionId, session.Id));
        Task? submittedOperation = null;
        Exception? primary = null;
        try {
            cut.Find("[data-testid='chat-prompt-input']").Input("Original uncertain submission");
            await cut.Find("[data-testid='chat-send-button']").ClickAsync(new MouseEventArgs());
            submittedOperation = CaptureSubmittedOperation(cut.Instance);
            await owner.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cut.Find("[data-testid='chat-prompt-input']").Input("Later unsent draft");
            completion.SetException(new AggregateException(new SandboxWorkspaceConcurrencyException(2, 3), new IOException("release failed")));
            cut.WaitForAssertion(() => {
                var retained = Assert.Single(context.Services.GetRequiredService<AgentChatSubmissionRetention>().Snapshot());
                Assert.Equal(AgentChatSubmissionState.Unconfirmed, retained.State);
                Assert.Equal("Later unsent draft", cut.FindComponent<ChatWorkspacePanel>().Instance.DraftPrompt);
                Assert.True(cut.Find("[data-testid='agents-chat-restore-submission']").HasAttribute("disabled"));
                Assert.True(cut.Find("[data-testid='chat-send-button']").HasAttribute("disabled"));
                Assert.Single(owner.SendRequests);
                Assert.DoesNotContain("release failed", cut.Markup, StringComparison.Ordinal);
            });
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            completion.TrySetCanceled();
            await DrainSubmittedOperationAsync(submittedOperation, primary);
        }
    }

    [Fact]
    public async Task Full_retention_refuses_before_dispatch_and_keeps_the_current_composer() {
        var agent = CreateAgent();
        var session = CreateSession(agent.Id);
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, DeferredWorkspaceProxy>();
        var reads = (DeferredWorkspaceProxy)(object)service;
        reads.Service = service;
        reads.Agents = [agent];
        reads.InitialWorkspace = CreateWorkspace(agent.Id, session);
        var orchestratorService = DispatchProxy.Create<IAgentChatExecutionOrchestrator, CompletedRunOrchestratorProxy>();
        var owner = (CompletedRunOrchestratorProxy)(object)orchestratorService;
        using var context = CreateContext(service, orchestratorService);
        var retention = context.Services.GetRequiredService<AgentChatSubmissionRetention>();
        for (var index = 0; index < AgentChatSubmissionRetention.MaximumSubmissions; index++) {
            Assert.True(retention.TryReserve(Guid.NewGuid(), null, null, new(0), "held", "held", [], out _));
        }
        var cut = context.Render<AgentChatPanel>(parameters => parameters
            .Add(component => component.PreferredAgentId, agent.Id)
            .Add(component => component.PreferredSessionId, session.Id));
        cut.Find("[data-testid='chat-prompt-input']").Input("Do not clear this");
        await cut.Find("[data-testid='chat-send-button']").ClickAsync(new MouseEventArgs());
        Assert.Equal("Do not clear this", cut.FindComponent<ChatWorkspacePanel>().Instance.DraftPrompt);
        Assert.False(cut.FindComponent<ChatWorkspacePanel>().Instance.IsBusy);
        Assert.Empty(owner.SendRequests);
        Assert.Equal(AgentChatSubmissionRetention.MaximumSubmissions, retention.Snapshot().Length);
    }

    private static Task CaptureSubmittedOperation(AgentChatPanel panel) => Assert.IsAssignableFrom<Task>(
        typeof(AgentChatPanel).GetField("trackedChatOperation", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(panel));

    private static async Task DrainSubmittedOperationAsync(Task? submittedOperation, Exception? primary) {
        if (submittedOperation is null) {
            return;
        }
        try {
            await submittedOperation;
        } catch (Exception cleanup) {
            if (primary is not null) {
                throw new AggregateException(primary, cleanup);
            }
            throw;
        }
    }

    private sealed class SubmissionFile : IBrowserFile {
        public string Name => "original.png";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => 3;
        public string ContentType => "image/png";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => new MemoryStream([1, 2, 3]);
    }
}
