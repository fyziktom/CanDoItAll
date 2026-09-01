using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentChatPanelResponsivenessTests
{
    [Fact]
    public async Task Failed_send_reloads_and_selects_the_exact_persisted_run()
    {
        var agent = CreateAgent();
        var session = CreateSession(agent.Id);
        var failedRun = CreateRunningRun(agent.Id, session.Id) with
        {
            State = ExecutionState.Failed,
            Outcome = RunOutcome.Failed,
            ResultSummary = "The configured provider rejected the request.",
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };
        var unrelatedRun = CreateRunningRun(agent.Id, session.Id);
        var workspaceService = DispatchProxy.Create<
            IAgentFrameworkWorkspaceService,
            DeferredWorkspaceProxy>();
        var workspace = (DeferredWorkspaceProxy)(object)workspaceService;
        workspace.Service = workspaceService;
        workspace.InitialWorkspace = CreateWorkspace(agent.Id, session);
        workspace.InitialRunDetail = new ExecutionRunDetail(failedRun, session, [], []);
        workspace.PostRunWorkspace.SetResult(CreateWorkspace(agent.Id, session, unrelatedRun));

        var orchestratorService = DispatchProxy.Create<
            IAgentChatExecutionOrchestrator,
            CompletedRunOrchestratorProxy>();
        var orchestrator = (CompletedRunOrchestratorProxy)(object)orchestratorService;
        orchestrator.Failure = new AgentChatRunFailedException(
            agent.Id,
            failedRun.Id,
            session.Id,
            "OpenAI default",
            "gpt-5.4-mini",
            new InvalidOperationException("api_key=provider-secret"),
            failedRun.ResultSummary,
            AgentProviderFailureCategory.RequestCompatibility);

        using var context = CreateContext(workspaceService, orchestratorService);
        var cut = RenderFocusedChat(context, agent, session);
        cut.WaitForElement("[data-testid='chat-prompt-input']")
            .Input("Use the selected project structure.");

        await cut.Find("[data-testid='chat-send-button']")
            .ClickAsync(new MouseEventArgs())
            .WaitAsync(TimeSpan.FromSeconds(2));

        await workspace.PostRunRefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(
                "Failed",
                cut.Find("[data-testid='agent-chat-run-state']").TextContent.Trim());
            Assert.Contains(failedRun.Id, workspace.ExecutionRunDetailRequests);
            Assert.DoesNotContain("provider-secret", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Failed_send_does_not_reload_a_run_from_another_thread()
    {
        var agent = CreateAgent();
        var executionSession = CreateSession(agent.Id);
        var unrelatedSession = CreateSession(agent.Id) with
        {
            Title = "Unrelated thread"
        };
        var unrelatedRun = CreateRunningRun(agent.Id, unrelatedSession.Id) with
        {
            State = ExecutionState.Failed,
            Outcome = RunOutcome.Failed,
            ResultSummary = "This failure belongs to another thread."
        };
        var workspaceService = DispatchProxy.Create<
            IAgentFrameworkWorkspaceService,
            DeferredWorkspaceProxy>();
        var workspace = (DeferredWorkspaceProxy)(object)workspaceService;
        workspace.Service = workspaceService;
        workspace.InitialWorkspace = CreateWorkspace(agent.Id, executionSession);
        workspace.InitialRunDetail = new ExecutionRunDetail(
            unrelatedRun,
            unrelatedSession,
            [],
            []);

        var orchestratorService = DispatchProxy.Create<
            IAgentChatExecutionOrchestrator,
            CompletedRunOrchestratorProxy>();
        var orchestrator = (CompletedRunOrchestratorProxy)(object)orchestratorService;
        orchestrator.Failure = new AgentChatRunFailedException(
            agent.Id,
            unrelatedRun.Id,
            unrelatedSession.Id,
            "OpenAI default",
            "gpt-5.4-mini",
            new InvalidOperationException("api_key=provider-secret"),
            unrelatedRun.ResultSummary,
            AgentProviderFailureCategory.ProviderError);

        using var context = CreateContext(workspaceService, orchestratorService);
        var cut = RenderFocusedChat(context, agent, executionSession);
        cut.WaitForElement("[data-testid='chat-prompt-input']")
            .Input("Keep this failure scoped to its actual thread.");

        await cut.Find("[data-testid='chat-send-button']")
            .ClickAsync(new MouseEventArgs())
            .WaitAsync(TimeSpan.FromSeconds(2));

        cut.WaitForAssertion(() =>
        {
            Assert.False(cut.Find("[data-testid='chat-send-button']").HasAttribute("disabled"));
            Assert.Contains(executionSession.Title, cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(unrelatedSession.Title, cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(unrelatedRun.ResultSummary, cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(unrelatedRun.Id, workspace.ExecutionRunDetailRequests);
            Assert.Equal(1, workspace.WorkspaceRequestCount);
        });
        Assert.DoesNotContain(
            context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Summary == "Attention");
    }

    [Fact]
    public async Task Failed_send_keeps_the_current_thread_when_reload_returns_another_thread()
    {
        var agent = CreateAgent();
        var executionSession = CreateSession(agent.Id);
        var unrelatedSession = CreateSession(agent.Id) with
        {
            Title = "Incorrect reload thread"
        };
        var failedRun = CreateRunningRun(agent.Id, executionSession.Id) with
        {
            State = ExecutionState.Failed,
            Outcome = RunOutcome.Failed,
            ResultSummary = "The provider rejected the scoped request."
        };
        var workspaceService = DispatchProxy.Create<
            IAgentFrameworkWorkspaceService,
            DeferredWorkspaceProxy>();
        var workspace = (DeferredWorkspaceProxy)(object)workspaceService;
        workspace.Service = workspaceService;
        workspace.InitialWorkspace = CreateWorkspace(agent.Id, executionSession);
        workspace.InitialRunDetail = new ExecutionRunDetail(
            failedRun,
            executionSession,
            [],
            []);
        workspace.PostRunWorkspace.SetResult(
            CreateWorkspace(agent.Id, unrelatedSession));

        var orchestratorService = DispatchProxy.Create<
            IAgentChatExecutionOrchestrator,
            CompletedRunOrchestratorProxy>();
        var orchestrator = (CompletedRunOrchestratorProxy)(object)orchestratorService;
        orchestrator.Failure = new AgentChatRunFailedException(
            agent.Id,
            failedRun.Id,
            executionSession.Id,
            "OpenAI default",
            "gpt-5.4-mini",
            new InvalidOperationException("api_key=provider-secret"),
            failedRun.ResultSummary,
            AgentProviderFailureCategory.ProviderError);

        using var context = CreateContext(workspaceService, orchestratorService);
        var cut = RenderFocusedChat(context, agent, executionSession);
        cut.WaitForElement("[data-testid='chat-prompt-input']")
            .Input("Reject a cross-thread reload response.");

        await cut.Find("[data-testid='chat-send-button']")
            .ClickAsync(new MouseEventArgs())
            .WaitAsync(TimeSpan.FromSeconds(2));

        cut.WaitForAssertion(() =>
        {
            var notification = context.Services
                .GetRequiredService<NotificationService>()
                .Messages
                .Last();
            Assert.Contains(executionSession.Title, cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(unrelatedSession.Title, cut.Markup, StringComparison.Ordinal);
            Assert.Equal("Attention", notification.Summary);
            Assert.Contains(
                failedRun.ResultSummary,
                notification.Detail,
                StringComparison.Ordinal);
            Assert.Contains(
                "failed run was persisted",
                notification.Detail,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(failedRun.Id, workspace.ExecutionRunDetailRequests);
        });
    }

    [Fact]
    public async Task Failed_approval_reloads_and_selects_the_exact_persisted_run()
    {
        var agent = CreateAgent();
        var session = CreateSession(agent.Id);
        var waitingRun = CreateWaitingApprovalRun(agent.Id, session.Id);
        var failedRun = waitingRun with
        {
            State = ExecutionState.Failed,
            Outcome = RunOutcome.Failed,
            ResultSummary = "The configured provider rejected the approval continuation.",
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };
        var workspaceService = DispatchProxy.Create<
            IAgentFrameworkWorkspaceService,
            DeferredWorkspaceProxy>();
        var workspace = (DeferredWorkspaceProxy)(object)workspaceService;
        workspace.Service = workspaceService;
        workspace.InitialWorkspace = CreateWorkspace(agent.Id, session, waitingRun);
        workspace.InitialRunDetail = new ExecutionRunDetail(failedRun, session, [], []);
        workspace.PostRunWorkspace.SetResult(
            CreateWorkspace(agent.Id, session, CreateRunningRun(agent.Id, session.Id)));

        var orchestratorService = DispatchProxy.Create<
            IAgentChatExecutionOrchestrator,
            CompletedRunOrchestratorProxy>();
        var orchestrator = (CompletedRunOrchestratorProxy)(object)orchestratorService;
        orchestrator.Failure = new AgentChatRunFailedException(
            agent.Id,
            failedRun.Id,
            session.Id,
            "OpenAI default",
            "gpt-5.4-mini",
            new InvalidOperationException("api_key=provider-secret"),
            failedRun.ResultSummary,
            AgentProviderFailureCategory.ProviderError);

        using var context = CreateContext(workspaceService, orchestratorService);
        var cut = RenderFocusedChat(context, agent, session);

        await cut.WaitForElement("[data-testid='chat-approve-once-button']")
            .ClickAsync(new MouseEventArgs())
            .WaitAsync(TimeSpan.FromSeconds(2));

        await orchestrator.ApprovalStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(
                "Failed",
                cut.Find("[data-testid='agent-chat-run-state']").TextContent.Trim());
            Assert.Contains(failedRun.Id, workspace.ExecutionRunDetailRequests);
            Assert.DoesNotContain("provider-secret", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Failed_send_does_not_publish_reload_message_after_session_selection_supersedes_reload()
    {
        var agent = CreateAgent();
        var executionSession = CreateSession(agent.Id);
        var selectedSession = CreateSession(agent.Id) with
        {
            Title = "Newly selected thread"
        };
        var failedRun = CreateRunningRun(agent.Id, executionSession.Id) with
        {
            State = ExecutionState.Failed,
            Outcome = RunOutcome.Failed,
            ResultSummary = "The configured provider rejected the request.",
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };
        var workspaceService = DispatchProxy.Create<
            IAgentFrameworkWorkspaceService,
            DeferredWorkspaceProxy>();
        var workspace = (DeferredWorkspaceProxy)(object)workspaceService;
        workspace.Service = workspaceService;
        workspace.InitialWorkspace = CreateWorkspace(agent.Id, executionSession);
        workspace.InitialRunDetail = new ExecutionRunDetail(failedRun, executionSession, [], []);

        var orchestratorService = DispatchProxy.Create<
            IAgentChatExecutionOrchestrator,
            CompletedRunOrchestratorProxy>();
        var orchestrator = (CompletedRunOrchestratorProxy)(object)orchestratorService;
        orchestrator.Failure = new AgentChatRunFailedException(
            agent.Id,
            failedRun.Id,
            executionSession.Id,
            "OpenAI default",
            "gpt-5.4-mini",
            new InvalidOperationException("api_key=provider-secret"),
            failedRun.ResultSummary,
            AgentProviderFailureCategory.ProviderError);

        using var context = CreateContext(workspaceService, orchestratorService);
        var cut = RenderFocusedChat(context, agent, executionSession);
        cut.WaitForElement("[data-testid='chat-prompt-input']")
            .Input("Use the selected project structure.");

        await cut.Find("[data-testid='chat-send-button']")
            .ClickAsync(new MouseEventArgs())
            .WaitAsync(TimeSpan.FromSeconds(2));
        await workspace.PostRunRefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var selectionTask = cut.InvokeAsync(() => cut.Render(parameters => parameters
            .Add(component => component.PreferredAgentId, agent.Id)
            .Add(component => component.PreferredAgent, agent)
            .Add(component => component.PreferredSessionId, selectedSession.Id)
            .Add(component => component.DisplayMode, AgentChatPanelDisplayMode.FocusedFloating)));
        await Task.Yield();
        workspace.PostRunWorkspace.SetResult(CreateWorkspace(agent.Id, selectedSession));
        await selectionTask.WaitAsync(TimeSpan.FromSeconds(2));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(selectedSession.Title, cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(failedRun.ResultSummary, cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("failed run was persisted", cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("provider-secret", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Successful_send_does_not_apply_after_same_agent_session_aba_selection()
    {
        var agent = CreateAgent();
        var executionSession = CreateSession(agent.Id);
        var intermediateSession = CreateSession(agent.Id) with
        {
            Title = "Intermediate thread"
        };
        var workspaceService = DispatchProxy.Create<
            IAgentFrameworkWorkspaceService,
            DeferredWorkspaceProxy>();
        var workspace = (DeferredWorkspaceProxy)(object)workspaceService;
        workspace.Service = workspaceService;
        workspace.InitialWorkspace = CreateWorkspace(agent.Id, executionSession);
        workspace.WorkspaceResponses.Enqueue(CreateWorkspace(agent.Id, intermediateSession));
        workspace.WorkspaceResponses.Enqueue(CreateWorkspace(agent.Id, executionSession));

        var orchestratorService = DispatchProxy.Create<
            IAgentChatExecutionOrchestrator,
            CompletedRunOrchestratorProxy>();
        var orchestrator = (CompletedRunOrchestratorProxy)(object)orchestratorService;
        orchestrator.Result = CreateRunResult(agent.Id, executionSession.Id);
        orchestrator.SendCompletion = new TaskCompletionSource<AgentChatRunResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var context = CreateContext(workspaceService, orchestratorService);
        var cut = RenderFocusedChat(context, agent, executionSession);
        cut.WaitForElement("[data-testid='chat-prompt-input']")
            .Input("Complete after an ABA selection.");
        await cut.Find("[data-testid='chat-send-button']")
            .ClickAsync(new MouseEventArgs());
        await orchestrator.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await RerenderFocusedChatAsync(cut, agent, intermediateSession);
        await RerenderFocusedChatAsync(cut, agent, executionSession);
        orchestrator.SendCompletion.SetResult(orchestrator.Result);

        cut.WaitForAssertion(() =>
        {
            Assert.False(cut.Find("[data-testid='chat-send-button']").HasAttribute("disabled"));
            Assert.Contains(executionSession.Title, cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(
                orchestrator.Result.AssistantMessage.Content,
                cut.Markup,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "Prompt sent through the integrated runtime",
                cut.Markup,
                StringComparison.Ordinal);
            Assert.Equal(3, workspace.WorkspaceRequestCount);
        });
    }

    [Fact]
    public async Task Failed_send_does_not_apply_after_same_agent_session_aba_selection()
    {
        var agent = CreateAgent();
        var executionSession = CreateSession(agent.Id);
        var intermediateSession = CreateSession(agent.Id);
        var failedRun = CreateRunningRun(agent.Id, executionSession.Id) with
        {
            State = ExecutionState.Failed,
            Outcome = RunOutcome.Failed,
            ResultSummary = "The configured provider rejected the ABA request."
        };
        var workspaceService = DispatchProxy.Create<
            IAgentFrameworkWorkspaceService,
            DeferredWorkspaceProxy>();
        var workspace = (DeferredWorkspaceProxy)(object)workspaceService;
        workspace.Service = workspaceService;
        workspace.InitialWorkspace = CreateWorkspace(agent.Id, executionSession);
        workspace.InitialRunDetail = new ExecutionRunDetail(failedRun, executionSession, [], []);
        workspace.WorkspaceResponses.Enqueue(CreateWorkspace(agent.Id, intermediateSession));
        workspace.WorkspaceResponses.Enqueue(CreateWorkspace(agent.Id, executionSession));

        var orchestratorService = DispatchProxy.Create<
            IAgentChatExecutionOrchestrator,
            CompletedRunOrchestratorProxy>();
        var orchestrator = (CompletedRunOrchestratorProxy)(object)orchestratorService;
        var failure = new AgentChatRunFailedException(
            agent.Id,
            failedRun.Id,
            executionSession.Id,
            "OpenAI default",
            "gpt-5.4-mini",
            new InvalidOperationException("api_key=provider-secret"),
            failedRun.ResultSummary,
            AgentProviderFailureCategory.ProviderError);
        orchestrator.SendCompletion = new TaskCompletionSource<AgentChatRunResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var context = CreateContext(workspaceService, orchestratorService);
        var cut = RenderFocusedChat(context, agent, executionSession);
        cut.WaitForElement("[data-testid='chat-prompt-input']")
            .Input("Fail after an ABA selection.");
        await cut.Find("[data-testid='chat-send-button']")
            .ClickAsync(new MouseEventArgs());
        await orchestrator.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await RerenderFocusedChatAsync(cut, agent, intermediateSession);
        await RerenderFocusedChatAsync(cut, agent, executionSession);
        orchestrator.SendCompletion.SetException(failure);

        cut.WaitForAssertion(() =>
        {
            Assert.False(cut.Find("[data-testid='chat-send-button']").HasAttribute("disabled"));
            Assert.Contains(executionSession.Title, cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(failure.SanitizedDisplayMessage, cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("provider-secret", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(failedRun.Id, workspace.ExecutionRunDetailRequests);
            Assert.Equal(3, workspace.WorkspaceRequestCount);
        });
    }

    [Fact]
    public async Task Failed_approval_does_not_apply_after_same_agent_session_aba_selection()
    {
        var agent = CreateAgent();
        var executionSession = CreateSession(agent.Id);
        var intermediateSession = CreateSession(agent.Id);
        var waitingRun = CreateWaitingApprovalRun(agent.Id, executionSession.Id);
        var failedRun = waitingRun with
        {
            State = ExecutionState.Failed,
            Outcome = RunOutcome.Failed,
            ResultSummary = "The configured provider rejected the ABA approval."
        };
        var workspaceService = DispatchProxy.Create<
            IAgentFrameworkWorkspaceService,
            DeferredWorkspaceProxy>();
        var workspace = (DeferredWorkspaceProxy)(object)workspaceService;
        workspace.Service = workspaceService;
        workspace.InitialWorkspace = CreateWorkspace(agent.Id, executionSession, waitingRun);
        workspace.InitialRunDetail = new ExecutionRunDetail(waitingRun, executionSession, [], []);
        workspace.WorkspaceResponses.Enqueue(CreateWorkspace(agent.Id, intermediateSession));
        workspace.WorkspaceResponses.Enqueue(CreateWorkspace(agent.Id, executionSession, waitingRun));

        var orchestratorService = DispatchProxy.Create<
            IAgentChatExecutionOrchestrator,
            CompletedRunOrchestratorProxy>();
        var orchestrator = (CompletedRunOrchestratorProxy)(object)orchestratorService;
        var failure = new AgentChatRunFailedException(
            agent.Id,
            failedRun.Id,
            executionSession.Id,
            "OpenAI default",
            "gpt-5.4-mini",
            new InvalidOperationException("api_key=provider-secret"),
            failedRun.ResultSummary,
            AgentProviderFailureCategory.ProviderError);
        orchestrator.ApprovalCompletion = new TaskCompletionSource<AgentChatRunResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var context = CreateContext(workspaceService, orchestratorService);
        var cut = RenderFocusedChat(context, agent, executionSession);
        await cut.WaitForElement("[data-testid='chat-approve-once-button']")
            .ClickAsync(new MouseEventArgs());
        await orchestrator.ApprovalStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await RerenderFocusedChatAsync(cut, agent, intermediateSession);
        await RerenderFocusedChatAsync(cut, agent, executionSession);
        var detailRequestCountBeforeCompletion = workspace.ExecutionRunDetailRequests.Count;
        orchestrator.ApprovalCompletion.SetException(failure);

        cut.WaitForAssertion(() =>
        {
            Assert.False(cut.Find("[data-testid='chat-approve-once-button']").HasAttribute("disabled"));
            Assert.Contains(executionSession.Title, cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(failure.SanitizedDisplayMessage, cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("provider-secret", cut.Markup, StringComparison.Ordinal);
            Assert.Equal(
                detailRequestCountBeforeCompletion,
                workspace.ExecutionRunDetailRequests.Count);
            Assert.Equal(3, workspace.WorkspaceRequestCount);
        });
    }

    [Fact]
    public async Task Send_releases_the_ui_event_while_the_post_run_workspace_refresh_is_pending()
    {
        var agent = CreateAgent();
        var session = CreateSession(agent.Id);
        var workspaceService = DispatchProxy.Create<
            IAgentFrameworkWorkspaceService,
            DeferredWorkspaceProxy>();
        var workspace = (DeferredWorkspaceProxy)(object)workspaceService;
        workspace.Service = workspaceService;
        workspace.InitialWorkspace = CreateWorkspace(agent.Id, session);

        var orchestratorService = DispatchProxy.Create<IAgentChatExecutionOrchestrator, CompletedRunOrchestratorProxy>();
        var orchestrator = (CompletedRunOrchestratorProxy)(object)orchestratorService;
        orchestrator.Result = CreateRunResult(agent.Id, session.Id);

        using var context = CreateContext(workspaceService, orchestratorService);
        var cut = RenderFocusedChat(context, agent, session);

        cut.WaitForElement("[data-testid='chat-prompt-input']")
            .Input("Report the current context.");

        var sendEvent = cut.Find("[data-testid='chat-send-button']")
            .ClickAsync(new MouseEventArgs());

        await sendEvent.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(orchestrator.Started.Task.IsCompleted);
        await orchestrator.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cut.WaitForAssertion(() =>
        {
            var status = cut.Find("[data-testid='agent-execution-activity-status']");
            Assert.Equal(
                orchestrator.LastStreamId?.OperationId.ToString(),
                status.GetAttribute("data-activity-operation-id"));
        });
        await workspace.PostRunRefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(workspace.PostRunWorkspace.Task.IsCompleted);

        var assistantMessage = orchestrator.Result.AssistantMessage;
        workspace.PostRunWorkspace.SetResult(CreateWorkspace(
            agent.Id,
            session with
            {
                Messages = [assistantMessage],
                UpdatedAtUtc = assistantMessage.CreatedAtUtc
            }));

        cut.WaitForAssertion(() => Assert.Contains(assistantMessage.Content, cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Successful_send_workspace_reload_failure_hides_secret_and_does_not_restore_draft()
    {
        const string prompt = "successful-floating-draft-must-not-return";
        const string secret = "floating-refresh-secret";
        var agent = CreateAgent();
        var session = CreateSession(agent.Id);
        var workspaceService = DispatchProxy.Create<
            IAgentFrameworkWorkspaceService,
            DeferredWorkspaceProxy>();
        var workspace = (DeferredWorkspaceProxy)(object)workspaceService;
        workspace.Service = workspaceService;
        workspace.InitialWorkspace = CreateWorkspace(agent.Id, session);

        var orchestratorService = DispatchProxy.Create<
            IAgentChatExecutionOrchestrator,
            CompletedRunOrchestratorProxy>();
        var orchestrator = (CompletedRunOrchestratorProxy)(object)orchestratorService;
        orchestrator.Result = CreateRunResult(agent.Id, session.Id);
        var logger = new RecordingLogger<AgentChatPanel>("Agent chat operation failed.");

        using var context = CreateContext(workspaceService, orchestratorService);
        context.Services.AddSingleton<ILogger<AgentChatPanel>>(logger);
        var cut = RenderFocusedChat(context, agent, session);
        cut.WaitForElement("[data-testid='chat-prompt-input']").Input(prompt);

        await cut.Find("[data-testid='chat-send-button']")
            .ClickAsync(new MouseEventArgs());
        await workspace.PostRunRefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        workspace.PostRunWorkspace.SetException(
            new InvalidOperationException($"api_key={secret}"));

        cut.WaitForAssertion(() =>
        {
            var notification = context.Services
                .GetRequiredService<NotificationService>()
                .Messages
                .Last();
            Assert.Equal("Refresh needed", notification.Summary);
            Assert.Contains(
                "latest thread state could not be loaded",
                notification.Detail,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(prompt, cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, notification.Detail, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, cut.Markup, StringComparison.Ordinal);
        });
        var log = await logger.Entry.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Null(log.Exception);
        Assert.Contains(nameof(InvalidOperationException), log.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("api_key", log.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secret, log.Message, StringComparison.Ordinal);
        Assert.Contains(agent.Id.ToString(), log.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Send_captures_registry_context_before_workspace_execution()
    {
        var agent = CreateAgent();
        var session = CreateSession(agent.Id);
        var workspaceService = DispatchProxy.Create<
            IResponsiveWorkspaceService,
            DeferredWorkspaceProxy>();
        var workspace = (DeferredWorkspaceProxy)(object)workspaceService;
        workspace.Service = workspaceService;
        workspace.InitialWorkspace = CreateWorkspace(agent.Id, session);
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var scopeId = AgentChatContextScopeId.Create();
        using var scope = registry.ActivateScope(CreateContextScope(
            scopeId,
            agent.Id,
            "original-project",
            "Original project"));
        using var fragment = registry.RegisterFragment(
            scopeId,
            CreateContextFragment("Original selected project"));
        var orchestrator = CreateExecutionOrchestrator(
            workspaceService,
            registry);

        using var context = CreateContext(workspaceService, orchestrator);
        var cut = RenderFocusedChat(context, agent, session);
        cut.WaitForElement("[data-testid='chat-prompt-input']")
            .Input("Use the selected project.");

        await cut.Find("[data-testid='chat-send-button']")
            .ClickAsync(new MouseEventArgs())
            .WaitAsync(TimeSpan.FromSeconds(2));

        await workspace.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        scope.Update(CreateContextScope(
            scopeId,
            agent.Id,
            "next-project",
            "Next project"));
        fragment.Update(CreateContextFragment("Next selected project"));

        var capturedOptions = Assert.IsType<AgentChatRunOptions>(workspace.CapturedSendOptions);
        var capturedContext = Assert.IsType<AgentRuntimeTransientContext>(capturedOptions.TransientContext);
        Assert.Equal("original-project", capturedOptions.Context?.SourceId);
        Assert.Contains(
            "Original selected project",
            capturedContext.Content,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Next selected project",
            capturedContext.Content,
            StringComparison.Ordinal);

        var result = CreateRunResult(agent.Id, session.Id);
        workspace.PostRunWorkspace.SetResult(CreateWorkspace(
            agent.Id,
            session with
            {
                Messages = [result.AssistantMessage],
                UpdatedAtUtc = result.AssistantMessage.CreatedAtUtc
            }));
        workspace.SendResult.SetResult(result);

        cut.WaitForAssertion(() => Assert.Contains(result.AssistantMessage.Content, cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Approval_releases_the_ui_event_while_the_post_run_workspace_refresh_is_pending()
    {
        var agent = CreateAgent();
        var session = CreateSession(agent.Id);
        var waitingRun = CreateWaitingApprovalRun(agent.Id, session.Id);
        var workspaceService = DispatchProxy.Create<IAgentFrameworkWorkspaceService, DeferredWorkspaceProxy>();
        var workspace = (DeferredWorkspaceProxy)(object)workspaceService;
        workspace.Service = workspaceService;
        workspace.InitialWorkspace = CreateWorkspace(agent.Id, session, waitingRun);
        workspace.InitialRunDetail = new ExecutionRunDetail(waitingRun, session, [], []);

        var orchestratorService = DispatchProxy.Create<IAgentChatExecutionOrchestrator, CompletedRunOrchestratorProxy>();
        var orchestrator = (CompletedRunOrchestratorProxy)(object)orchestratorService;
        orchestrator.Result = CreateRunResult(agent.Id, session.Id);

        using var context = CreateContext(workspaceService, orchestratorService);
        var cut = RenderFocusedChat(context, agent, session);

        var approvalEvent = cut.WaitForElement("[data-testid='chat-approve-once-button']")
            .ClickAsync(new MouseEventArgs());

        await approvalEvent.WaitAsync(TimeSpan.FromSeconds(2));
        await orchestrator.ApprovalStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await workspace.PostRunRefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(workspace.PostRunWorkspace.Task.IsCompleted);

        var assistantMessage = orchestrator.Result.AssistantMessage;
        workspace.PostRunWorkspace.SetResult(CreateWorkspace(
            agent.Id,
            session with
            {
                Messages = [assistantMessage],
                UpdatedAtUtc = assistantMessage.CreatedAtUtc
            }));

        cut.WaitForAssertion(() => Assert.Contains(assistantMessage.Content, cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reopened_panel_without_the_external_run_reloads_once_when_it_completes()
    {
        var agent = CreateAgent();
        var session = CreateSession(agent.Id);
        var externalRun = CreateRunningRun(agent.Id, session.Id);
        var workspaceService = DispatchProxy.Create<IAgentFrameworkWorkspaceService, DeferredWorkspaceProxy>();
        var workspace = (DeferredWorkspaceProxy)(object)workspaceService;
        workspace.Service = workspaceService;
        workspace.InitialWorkspace = CreateWorkspace(agent.Id, session);

        var orchestratorService = DispatchProxy.Create<IAgentChatExecutionOrchestrator, CompletedRunOrchestratorProxy>();
        var orchestrator = (CompletedRunOrchestratorProxy)(object)orchestratorService;
        orchestrator.Result = CreateRunResult(agent.Id, session.Id);

        using var context = CreateContext(workspaceService, orchestratorService);
        var cut = RenderFocusedChat(context, agent, session);
        var assistantMessage = orchestrator.Result.AssistantMessage;
        var completedRun = externalRun with
        {
            State = ExecutionState.Completed,
            Outcome = RunOutcome.Succeeded,
            ResultSummary = assistantMessage.Content,
            UpdatedAtUtc = assistantMessage.CreatedAtUtc,
            CompletedAtUtc = assistantMessage.CreatedAtUtc
        };
        var completedSession = session with
        {
            Messages = [assistantMessage],
            UpdatedAtUtc = assistantMessage.CreatedAtUtc,
            LatestExecutionRunId = completedRun.Id
        };
        workspace.InitialRunDetail = new ExecutionRunDetail(completedRun, completedSession, [], []);
        var terminalUpdate = new ExecutionLogEntry(
            Guid.NewGuid(),
            agent.Id,
            session.Id,
            assistantMessage.CreatedAtUtc,
            ExecutionState.Completed,
            "completed",
            "Execution completed.")
        {
            ExecutionRunId = externalRun.Id
        };

        workspace.RaiseExecutionUpdated(terminalUpdate);
        workspace.RaiseExecutionUpdated(terminalUpdate);

        await workspace.PostRunRefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2, workspace.WorkspaceRequestCount);
        workspace.PostRunWorkspace.SetResult(CreateWorkspace(
            agent.Id,
            completedSession,
            completedRun));

        cut.WaitForAssertion(() => Assert.Contains(assistantMessage.Content, cut.Markup, StringComparison.Ordinal));
        Assert.Equal(2, workspace.WorkspaceRequestCount);
    }

    private static BunitContext CreateContext(
        IAgentFrameworkWorkspaceService workspaceService,
        IAgentChatExecutionOrchestrator orchestratorService)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddStubProviderRuntimeAdministration();
        context.Services.AddSingleton(workspaceService);
        context.Services.AddSingleton(orchestratorService);
        context.Services.AddSingleton<IAgentExecutionActivityReader>(
            new UnknownActivityReader());
        context.Services.AddSingleton(DispatchProxy.Create<IAgentVoiceService, UnexpectedCallProxy>());
        context.Services.AddSingleton(DispatchProxy.Create<IAgentChatAttachmentStagingService, UnexpectedCallProxy>());
        context.Services.AddSingleton(DispatchProxy.Create<IFloatingAgentChatCoordinator, UnexpectedCallProxy>());
        context.Services.AddSingleton(DispatchProxy.Create<IPromptGalleryService, UnexpectedCallProxy>());
        return context;
    }

    private static AgentChatExecutionOrchestrator CreateExecutionOrchestrator(
        IResponsiveWorkspaceService workspace,
        IAgentChatContextRegistry registry)
    {
        var profileId = Guid.NewGuid();
        var scope = WorkspaceScopeDescriptor.Organization(profileId.ToString("N"));
        var coordinator = new AgentExecutionActivityCoordinator(
            new PartitionedSequencedStream<
                AgentExecutionActivityStreamId,
                AgentExecutionActivity>(
                PartitionedSequencedStreamPolicy.Default,
                TimeProvider.System),
            TimeProvider.System);
        var generationSource = new FixedAgentExecutionProfileGenerationSource(
            new DatabaseProfileGeneration(0));
        return new AgentChatExecutionOrchestrator(
            workspace,
            new AgentTurnContextCaptureService(
                registry,
                new ResponsiveSandboxAuthorityResolver(),
                generationSource,
                TimeProvider.System),
            new AgentChatExecutionNotificationHub(
                NullLogger<AgentChatExecutionNotificationHub>.Instance),
            coordinator,
            new ResponsiveWorkspaceFactory(workspace, scope),
            new ResponsiveDatabaseProfileRuntimeAccessor(profileId),
            generationSource);
    }

    private sealed class ResponsiveSandboxAuthorityResolver
        : IAgentExecutionAuthorityResolver
    {
        public ValueTask<AgentExecutionAuthorityRecord> ResolveAsync(
            AgentExecutionAuthorityResolutionRequest request,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new AgentExecutionAuthorityRecord(
                AgentExecutionAuthorityId.Create(),
                request.AgentId,
                Guid.NewGuid(),
                request.ExpectedDatabaseProfileGeneration,
                request.ObservedWorkspaceScope ?? WorkspaceScopeDescriptor.Sandbox,
                readAllowed: true,
                mutationAllowed: false,
                "test",
                "test-fingerprint",
                DateTimeOffset.UtcNow));
        }
    }

    private static IRenderedComponent<AgentChatPanel> RenderFocusedChat(
        BunitContext context,
        AgentDefinition agent,
        ChatSessionRecord session)
        => context.Render<AgentChatPanel>(parameters => parameters
            .Add(component => component.PreferredAgentId, agent.Id)
            .Add(component => component.PreferredAgent, agent)
            .Add(component => component.PreferredSessionId, session.Id)
            .Add(component => component.DisplayMode, AgentChatPanelDisplayMode.FocusedFloating));

    private static Task RerenderFocusedChatAsync(
        IRenderedComponent<AgentChatPanel> cut,
        AgentDefinition agent,
        ChatSessionRecord session)
    {
        return cut.InvokeAsync(() => cut.Render(parameters => parameters
            .Add(component => component.PreferredAgentId, agent.Id)
            .Add(component => component.PreferredAgent, agent)
            .Add(component => component.PreferredSessionId, session.Id)
            .Add(component => component.DisplayMode, AgentChatPanelDisplayMode.FocusedFloating)));
    }

    private static AgentDefinition CreateAgent()
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Id: Guid.NewGuid(),
            Name: "Responsive agent",
            RoleTitle: "Test agent",
            Summary: "Exercises the floating chat event boundary.",
            Instructions: "Stay scoped.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: "gpt-test",
            Workload: AgentWorkloadKind.General,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: true,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static AgentChatContextScope CreateContextScope(
        AgentChatContextScopeId scopeId,
        Guid agentId,
        string sourceId,
        string displayName)
    {
        return new AgentChatContextScope(
            scopeId,
            new AgentChatContextSource(
                new AgentChatContextSourceKind("projects"),
                new AgentChatContextSourceId(sourceId)),
            displayName,
            agentAccess:
            [
                new AgentChatContextAgentAccess(
                    agentId,
                    AgentChatContextPermission.Read,
                    displayName)
            ],
            accessMode: AgentChatContextScopeAccessMode.AllowListed);
    }

    private static AgentChatContextFragment CreateContextFragment(string content)
    {
        return new AgentChatContextFragment(
            new AgentChatContextContributorId("selection"),
            0,
            content);
    }

    private static ChatSessionRecord CreateSession(Guid agentId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ChatSessionRecord(
            Id: Guid.NewGuid(),
            AgentId: agentId,
            Title: "Responsive thread",
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            Messages: []);
    }

    private static ChatAgentWorkspaceSnapshot CreateWorkspace(
        Guid agentId,
        ChatSessionRecord session,
        ExecutionRunRecord? selectedRun = null)
        => new ChatAgentWorkspaceSnapshot(
            agentId,
            [new ChatSessionSummaryRecord(
                session.Id,
                session.AgentId,
                session.Title,
                session.CreatedAtUtc,
                session.UpdatedAtUtc,
                session.Messages.Count,
                session.Messages.LastOrDefault()?.Content ?? "No messages yet.",
                PendingApprovalCount: 0,
                AutoApprovePendingToolCalls: false)],
            session,
            session.Id,
            LatestRun: null)
        {
            SelectedRun = selectedRun
        };

    private static ExecutionRunRecord CreateWaitingApprovalRun(Guid agentId, Guid sessionId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: agentId,
            ChatSessionId: sessionId,
            Title: "Waiting for approval",
            SourceKind: "chat",
            SourceId: sessionId.ToString("D"),
            CorrelationId: Guid.NewGuid().ToString("N"),
            CausationId: string.Empty,
            RequestedBy: "test",
            RequestedByKind: "test",
            MetadataJson: "{}",
            InputSummary: "Approve the operation.",
            ResultSummary: string.Empty,
            ProviderName: "test-provider",
            Model: "test-model",
            State: ExecutionState.WaitingOnTool,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals:
            [
                new PendingToolApprovalRecord(
                    "approval-1",
                    "call-1",
                    "test_tool",
                    "function",
                    "Approve the test operation.",
                    "{}")
            ]);
    }

    private static ExecutionRunRecord CreateRunningRun(Guid agentId, Guid sessionId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: agentId,
            ChatSessionId: sessionId,
            Title: "Running execution",
            SourceKind: "chat",
            SourceId: sessionId.ToString("D"),
            CorrelationId: Guid.NewGuid().ToString("N"),
            CausationId: string.Empty,
            RequestedBy: "test",
            RequestedByKind: "test",
            MetadataJson: "{}",
            InputSummary: "Continue the operation.",
            ResultSummary: string.Empty,
            ProviderName: "test-provider",
            Model: "test-model",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }

    private static AgentChatRunResult CreateRunResult(Guid agentId, Guid sessionId)
    {
        var now = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid();
        return new AgentChatRunResult(
            sessionId,
            new ChatMessageRecord(
                Guid.NewGuid(),
                ChatMessageRole.Assistant,
                "Context captured.",
                now,
                TokenEstimate: 2),
            new AgentRunMetric(
                Guid.NewGuid(),
                agentId,
                sessionId,
                now,
                RunOutcome.Succeeded,
                "test-provider",
                "test-model",
                DurationMs: 1,
                InputTokens: 1,
                OutputTokens: 2,
                ToolCalls: 0)
            {
                ExecutionRunId = runId
            })
        {
            ExecutionRunId = runId,
            State = ExecutionState.Completed
        };
    }

    private static AgentExecutionActivityStreamId CreateActivityStreamId()
    {
        var profileId = Guid.NewGuid();
        return new AgentExecutionActivityStreamId(
            profileId,
            WorkspaceScopeDescriptor.Organization(profileId.ToString("N")),
            new DatabaseProfileGeneration(0),
            AgentExecutionOperationId.New());
    }

    private class DeferredWorkspaceProxy : DispatchProxy
    {
        private int workspaceRequestCount;
        private EventHandler<ExecutionLogEntry>? executionUpdated;

        public IAgentFrameworkWorkspaceService Service { get; set; } = default!;

        public ChatAgentWorkspaceSnapshot InitialWorkspace { get; set; } = default!;

        public ExecutionRunDetail? InitialRunDetail { get; set; }

        public List<Guid> ExecutionRunDetailRequests { get; } = [];

        public int WorkspaceRequestCount => Volatile.Read(ref workspaceRequestCount);

        public TaskCompletionSource PostRunRefreshStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<ChatAgentWorkspaceSnapshot> PostRunWorkspace { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Queue<ChatAgentWorkspaceSnapshot> WorkspaceResponses { get; } = [];

        public TaskCompletionSource SendStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<AgentChatRunResult> SendResult { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public AgentChatRunOptions? CapturedSendOptions { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "add_ExecutionUpdated" => AddExecutionUpdated((EventHandler<ExecutionLogEntry>)args![0]!),
                "remove_ExecutionUpdated" => RemoveExecutionUpdated((EventHandler<ExecutionLogEntry>)args![0]!),
                nameof(IAgentFrameworkWorkspaceService.GetChatAgentWorkspaceAsync) => GetWorkspace(),
                nameof(IAgentFrameworkWorkspaceService.SendMessageAsync) => SendMessage(args!),
                nameof(IAgentFrameworkWorkspaceActivityExecutionService.SendMessageWithinOperationAsync) =>
                    SendMessageWithinOperationAsync(args!),
                nameof(IAgentFrameworkWorkspaceService.GetExecutionRunDetailAsync) => GetExecutionRunDetail(args!),
                _ => throw new InvalidOperationException(
                    $"Workspace service member '{targetMethod?.Name}' was not expected in this component test.")
            };
        }

        public void RaiseExecutionUpdated(ExecutionLogEntry entry)
            => executionUpdated?.Invoke(Service, entry);

        private Task<ExecutionRunDetail> GetExecutionRunDetail(object?[] args)
        {
            ExecutionRunDetailRequests.Add(Assert.IsType<Guid>(args[0]));
            return Task.FromResult(
                InitialRunDetail ?? throw new InvalidOperationException("No initial execution detail was configured."));
        }

        private object? AddExecutionUpdated(EventHandler<ExecutionLogEntry> handler)
        {
            executionUpdated += handler;
            return null;
        }

        private object? RemoveExecutionUpdated(EventHandler<ExecutionLogEntry> handler)
        {
            executionUpdated -= handler;
            return null;
        }

        private Task<ChatAgentWorkspaceSnapshot> GetWorkspace()
        {
            if (Interlocked.Increment(ref workspaceRequestCount) == 1)
            {
                return Task.FromResult(InitialWorkspace);
            }

            PostRunRefreshStarted.TrySetResult();
            if (WorkspaceResponses.TryDequeue(out var response))
            {
                return Task.FromResult(response);
            }

            return PostRunWorkspace.Task;
        }

        private Task<AgentChatRunResult> SendMessage(object?[] args)
        {
            CapturedSendOptions = Assert.IsType<AgentChatRunOptions>(args[3]);
            SendStarted.TrySetResult();
            return SendResult.Task;
        }

        private async Task<AgentChatRunResult> SendMessageWithinOperationAsync(
            object?[] args)
        {
            var operation = Assert.IsAssignableFrom<
                IAgentExecutionActivityOperationLease>(args[0]);
            CapturedSendOptions = Assert.IsType<AgentChatRunOptions>(args[4]);
            SendStarted.TrySetResult();
            var result = await SendResult.Task;
            if (!operation.ChatSessionId.HasValue)
            {
                operation.BindChatSession(result.ChatSessionId);
            }

            operation.BindExecutionRun(
                result.ExecutionRunId,
                result.ChatSessionId);
            operation.Report(
                AgentExecutionActivityPhase.PersistingResult,
                "Persisting test result.");
            operation.Complete("Test operation completed.");
            return result;
        }
    }

    private interface IResponsiveWorkspaceService :
        IAgentFrameworkWorkspaceService,
        IAgentFrameworkWorkspaceActivityExecutionService
    {
    }

    private sealed class ResponsiveWorkspaceFactory(
        IAgentFrameworkWorkspaceService workspace,
        WorkspaceScopeDescriptor organizationScope)
        : ICanDoItAllAgentWorkspaceFactory
    {
        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService()
        {
            return workspace;
        }

        public IAgentFrameworkWorkspaceService GetWorkspaceService(
            WorkspaceScopeDescriptor scope)
        {
            return workspace;
        }

        public WorkspaceScopeDescriptor GetOrganizationScope()
        {
            return organizationScope;
        }

        public string GetWorkspaceRoot()
        {
            return "test-workspace";
        }
    }

    private sealed class ResponsiveDatabaseProfileRuntimeAccessor(
        Guid profileId) : IDatabaseProfileRuntimeAccessor
    {
        private readonly ResolvedDatabaseProfile profile = new(
            new DatabaseProfileRecord
            {
                Id = profileId,
                DisplayName = "Test profile",
                ProviderKind = DatabaseProviderKind.InMemory,
                SourceKind = DatabaseProfileSourceKind.InMemory
            },
            DatabaseProfileResolutionSource.ExplicitOverride,
            "test");

        public ResolvedDatabaseProfile ResolveCurrentProfile()
        {
            return profile;
        }

        public ResolvedDatabaseProfile ResolveProfile(Guid requestedProfileId)
        {
            if (requestedProfileId != profileId)
            {
                throw new KeyNotFoundException();
            }

            return profile;
        }
    }

    private class CompletedRunOrchestratorProxy : DispatchProxy
    {
        public AgentChatRunResult Result { get; set; } = default!;

        public Exception? Failure { get; set; }

        public TaskCompletionSource<AgentChatRunResult>? SendCompletion { get; set; }

        public TaskCompletionSource<AgentChatRunResult>? ApprovalCompletion { get; set; }

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ApprovalStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public AgentExecutionActivityStreamId? LastStreamId { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IAgentChatExecutionOrchestrator.StartSendMessage))
            {
                Started.TrySetResult();
                LastStreamId = CreateActivityStreamId();
                return new AgentChatOperationHandle(
                    LastStreamId,
                    SendCompletion?.Task ??
                    (Failure is null
                        ? Task.FromResult(Result)
                        : Task.FromException<AgentChatRunResult>(Failure)));
            }

            if (targetMethod?.Name == nameof(IAgentChatExecutionOrchestrator.StartApprovalContinuation))
            {
                ApprovalStarted.TrySetResult();
                LastStreamId = CreateActivityStreamId();
                return new AgentChatOperationHandle(
                    LastStreamId,
                    ApprovalCompletion?.Task ??
                    (Failure is null
                        ? Task.FromResult(Result)
                        : Task.FromException<AgentChatRunResult>(Failure)));
            }

            throw new InvalidOperationException(
                $"Agent chat orchestrator member '{targetMethod?.Name}' was not expected in this component test.");
        }
    }

    private sealed class UnknownActivityReader : IAgentExecutionActivityReader
    {
        public ISequencedStreamReader<AgentExecutionActivity> OpenReader(
            AgentExecutionActivityStreamId streamId,
            StreamSequence fromInclusive)
        {
            return new UnknownSequencedStreamReader();
        }
    }

    private sealed class UnknownSequencedStreamReader :
        ISequencedStreamReader<AgentExecutionActivity>
    {
        public StreamSequence NextSequence => StreamSequence.Beginning;

        public ValueTask<SequencedStreamReadResult<AgentExecutionActivity>> ReadAsync(
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<
                SequencedStreamReadResult<AgentExecutionActivity>>(
                new SequencedStreamUnknown<AgentExecutionActivity>());
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private class UnexpectedCallProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException(
                $"Service member '{targetMethod?.Name}' was not expected in this component test.");
    }

    private sealed class RecordingLogger<T>(string messagePrefix) : ILogger<T>
    {
        public TaskCompletionSource<CapturedLog> Entry { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (logLevel >= LogLevel.Warning &&
                message.StartsWith(messagePrefix, StringComparison.Ordinal))
            {
                Entry.TrySetResult(new CapturedLog(message, exception));
            }
        }
    }

    private sealed record CapturedLog(string Message, Exception? Exception);

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
