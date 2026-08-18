using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ContextualAgentWorkspaceWindowsFailureTests
{
    [Fact]
    public async Task Project_structure_failure_selects_the_exact_persisted_run()
    {
        var harness = CreateHarness();
        using var context = harness.Context;
        var refresh = new List<ContextualAgentWorkspaceRefreshRequest>();
        var cut = Render(context, harness, refresh);

        await OpenChatAndSendAsync(cut, harness.Agent);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(
                "Failed",
                cut.Find("[data-testid='agent-chat-run-state']").TextContent.Trim());
            Assert.Contains(harness.FailedRun.Id, harness.Workspace.ExecutionRunDetailRequests);
            Assert.Equal(harness.FailedRun.Id, Assert.Single(refresh).ExecutionRunId);
        });
        var notification = context.Services
            .GetRequiredService<NotificationService>()
            .Messages
            .Last();
        Assert.Equal("Prompt failed", notification.Summary);
        Assert.Equal(harness.Failure.SanitizedDisplayMessage, notification.Detail);
        Assert.DoesNotContain("provider-secret", notification.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("provider-secret", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Project_structure_failure_does_not_reload_a_run_from_another_thread()
    {
        var harness = CreateHarness();
        var unrelatedSession = CreateSession(harness.Agent.Id) with
        {
            Title = "Unrelated contextual thread"
        };
        var unrelatedRun = CreateFailedRun(
            harness.Agent.Id,
            unrelatedSession.Id);
        harness.Workspace.Failure = new AgentChatRunFailedException(
            harness.Agent.Id,
            unrelatedRun.Id,
            unrelatedSession.Id,
            "OpenAI default",
            "gpt-5.4-mini",
            new InvalidOperationException("api_key=provider-secret"),
            unrelatedRun.ResultSummary,
            AgentProviderFailureCategory.ProviderError);
        harness.Workspace.ReloadedWorkspace = CreateWorkspace(
            harness.Agent.Id,
            unrelatedSession,
            unrelatedRun);
        harness.Workspace.FailedRunDetail = new ExecutionRunDetail(
            unrelatedRun,
            unrelatedSession,
            [],
            []);
        using var context = harness.Context;
        var cut = Render(context, harness, []);

        await OpenChatAndSendAsync(cut, harness.Agent);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(harness.Workspace.Session.Title, cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(unrelatedSession.Title, cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(unrelatedRun.ResultSummary, cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(unrelatedRun.Id, harness.Workspace.ExecutionRunDetailRequests);
        });
        Assert.DoesNotContain(
            context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Summary == "Prompt failed");
    }

    [Fact]
    public async Task Project_structure_failure_keeps_provider_error_and_warns_when_reload_fails()
    {
        var harness = CreateHarness();
        harness.Workspace.DetailFailure = new InvalidOperationException("Simulated projection read failure.");
        using var context = harness.Context;
        var cut = Render(context, harness, []);

        await OpenChatAndSendAsync(cut, harness.Agent);

        cut.WaitForAssertion(() =>
        {
            var notification = context.Services
                .GetRequiredService<NotificationService>()
                .Messages
                .Last();
            Assert.Equal("Prompt failed", notification.Summary);
            Assert.StartsWith(
                harness.Failure.SanitizedDisplayMessage,
                notification.Detail,
                StringComparison.Ordinal);
            Assert.Contains(
                "failed run was persisted",
                notification.Detail,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "Reload this workspace",
                notification.Detail,
                StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Project_structure_approval_failure_selects_the_exact_persisted_run()
    {
        var harness = CreateHarness();
        harness.Workspace.InitialWorkspace = CreateWorkspace(
            harness.Agent.Id,
            harness.Workspace.Session,
            CreateWaitingApprovalRun(
                harness.Agent.Id,
                harness.Workspace.Session.Id));
        using var context = harness.Context;
        var refresh = new List<ContextualAgentWorkspaceRefreshRequest>();
        var cut = Render(context, harness, refresh);

        await OpenChatAsync(cut, harness.Agent);
        await cut.WaitForElement("[data-testid='chat-approve-once-button']")
            .ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(
                "Failed",
                cut.Find("[data-testid='agent-chat-run-state']").TextContent.Trim());
            Assert.Contains(harness.FailedRun.Id, harness.Workspace.ExecutionRunDetailRequests);
            Assert.Equal(harness.FailedRun.Id, Assert.Single(refresh).ExecutionRunId);
        });
        var notification = context.Services
            .GetRequiredService<NotificationService>()
            .Messages
            .Last();
        Assert.Equal("Approval failed", notification.Summary);
        Assert.Equal(harness.Failure.SanitizedDisplayMessage, notification.Detail);
        Assert.DoesNotContain("provider-secret", notification.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Project_structure_failure_keeps_reload_success_when_refresh_callback_fails()
    {
        var harness = CreateHarness();
        using var context = harness.Context;
        var refresh = new List<ContextualAgentWorkspaceRefreshRequest>();
        var cut = Render(
            context,
            harness,
            refresh,
            _ => throw new InvalidOperationException("Simulated host refresh failure."));

        await OpenChatAndSendAsync(cut, harness.Agent);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(
                "Failed",
                cut.Find("[data-testid='agent-chat-run-state']").TextContent.Trim());
            var notification = context.Services
                .GetRequiredService<NotificationService>()
                .Messages
                .Last();
            Assert.Equal(harness.Failure.SanitizedDisplayMessage, notification.Detail);
            Assert.DoesNotContain(
                "failed run was persisted",
                notification.Detail,
                StringComparison.OrdinalIgnoreCase);
        });
        Assert.Equal(harness.FailedRun.Id, Assert.Single(refresh).ExecutionRunId);
    }

    [Fact]
    public async Task Project_structure_successful_send_is_not_reclassified_when_refresh_callback_fails()
    {
        const string prompt = "successful-send-draft-must-not-return";
        var harness = CreateHarness();
        var result = CreateCompletedExecutionResult(
            harness.Agent.Id,
            harness.Workspace.Session.Id);
        harness.Workspace.ReloadedWorkspace = CreateWorkspace(
            harness.Agent.Id,
            harness.Workspace.Session,
            CreateCompletedRun(
                harness.Agent.Id,
                harness.Workspace.Session.Id,
                result.ExecutionRunId));
        harness.Workspace.DeferredExecution = new TaskCompletionSource<ExecutionRunResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Workspace.DeferredExecution.SetResult(result);
        using var context = harness.Context;
        var refresh = new List<ContextualAgentWorkspaceRefreshRequest>();
        var cut = Render(
            context,
            harness,
            refresh,
            _ => throw new InvalidOperationException("Simulated host refresh failure."));

        await OpenChatAsync(cut, harness.Agent);
        cut.WaitForElement("[data-testid='chat-prompt-input']").Input(prompt);
        await cut.Find("[data-testid='chat-send-button']")
            .ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            var notification = context.Services
                .GetRequiredService<NotificationService>()
                .Messages
                .Last();
            Assert.Equal("Refresh needed", notification.Summary);
            Assert.Contains("prompt completed", notification.Detail, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(prompt, cut.Markup, StringComparison.Ordinal);
            Assert.Equal(
                "Completed",
                cut.Find("[data-testid='agent-chat-run-state']").TextContent.Trim());
        });
        Assert.Equal(result.ExecutionRunId, Assert.Single(refresh).ExecutionRunId);
        Assert.DoesNotContain(
            context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Summary == "Prompt failed");
    }

    [Fact]
    public async Task Project_structure_successful_approval_is_not_reclassified_when_refresh_callback_fails()
    {
        var harness = CreateHarness();
        var waitingRun = CreateWaitingApprovalRun(
            harness.Agent.Id,
            harness.Workspace.Session.Id);
        var result = CreateCompletedChatRunResult(
            harness.Agent.Id,
            harness.Workspace.Session.Id);
        harness.Workspace.InitialWorkspace = CreateWorkspace(
            harness.Agent.Id,
            harness.Workspace.Session,
            waitingRun);
        harness.Workspace.ReloadedWorkspace = CreateWorkspace(
            harness.Agent.Id,
            harness.Workspace.Session,
            CreateCompletedRun(
                harness.Agent.Id,
                harness.Workspace.Session.Id,
                result.ExecutionRunId));
        harness.Workspace.DeferredApproval = new TaskCompletionSource<AgentChatRunResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Workspace.DeferredApproval.SetResult(result);
        using var context = harness.Context;
        var refresh = new List<ContextualAgentWorkspaceRefreshRequest>();
        var cut = Render(
            context,
            harness,
            refresh,
            _ => throw new InvalidOperationException("Simulated host refresh failure."));

        await OpenChatAsync(cut, harness.Agent);
        await cut.WaitForElement("[data-testid='chat-approve-once-button']")
            .ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            var notification = context.Services
                .GetRequiredService<NotificationService>()
                .Messages
                .Last();
            Assert.Equal("Refresh needed", notification.Summary);
            Assert.Contains("approval completed", notification.Detail, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(
                "Completed",
                cut.Find("[data-testid='agent-chat-run-state']").TextContent.Trim());
        });
        Assert.Equal(result.ExecutionRunId, Assert.Single(refresh).ExecutionRunId);
        Assert.DoesNotContain(
            context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Summary == "Approval failed");
    }

    [Fact]
    public async Task Project_structure_successful_send_reload_failure_hides_secret_and_does_not_restore_draft()
    {
        const string prompt = "successful-contextual-draft-must-not-return";
        const string secret = "contextual-refresh-secret";
        var harness = CreateHarness();
        var result = CreateCompletedExecutionResult(
            harness.Agent.Id,
            harness.Workspace.Session.Id);
        harness.Workspace.DeferredExecution = new TaskCompletionSource<ExecutionRunResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Workspace.DeferredExecution.SetResult(result);
        harness.Workspace.ReloadWorkspaceFailure = new InvalidOperationException(
            $"api_key={secret}");
        var logger = new RecordingLogger<ContextualAgentWorkspaceWindows>(
            "Contextual-agent operation failed.");
        using var context = harness.Context;
        context.Services.AddSingleton<ILogger<ContextualAgentWorkspaceWindows>>(logger);
        var cut = Render(context, harness, []);

        await OpenChatAsync(cut, harness.Agent);
        cut.WaitForElement("[data-testid='chat-prompt-input']").Input(prompt);
        await cut.Find("[data-testid='chat-send-button']")
            .ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            var notification = context.Services
                .GetRequiredService<NotificationService>()
                .Messages
                .Last();
            Assert.Equal("Refresh needed", notification.Summary);
            Assert.Contains(
                "latest contextual thread state could not be loaded",
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
        Assert.Contains(harness.Agent.Id.ToString(), log.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Project_structure_terminal_completion_does_not_replace_a_newer_agent_selection(
        bool providerFailed)
    {
        var harness = CreateHarness();
        var nextAgent = CreateAgent(harness.ProjectId, "Newer project structure agent");
        var nextSession = CreateSession(nextAgent.Id) with
        {
            Title = "Newer contextual thread"
        };
        harness.ReferenceData.Agents.Add(nextAgent);
        harness.Workspace.SessionsByAgent[nextAgent.Id] = nextSession;
        harness.Workspace.AgentWorkspaces[nextAgent.Id] = CreateWorkspace(
            nextAgent.Id,
            nextSession);
        harness.Workspace.DeferredExecution = new TaskCompletionSource<ExecutionRunResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var context = harness.Context;
        var refresh = new List<ContextualAgentWorkspaceRefreshRequest>();
        var cut = Render(context, harness, refresh);

        await OpenChatAsync(cut, harness.Agent);
        cut.WaitForElement("[data-testid='chat-prompt-input']")
            .Input("Inspect the selected project structure.");
        var sendTask = cut.Find("[data-testid='chat-send-button']")
            .ClickAsync(new MouseEventArgs());
        await harness.Workspace.ExecutionStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await OpenChatAsync(cut, nextAgent);
        if (providerFailed)
        {
            harness.Workspace.DeferredExecution.SetException(harness.Failure);
        }
        else
        {
            harness.Workspace.DeferredExecution.SetResult(
                CreateCompletedExecutionResult(
                    harness.Agent.Id,
                    harness.Workspace.Session.Id));
        }

        await sendTask.WaitAsync(TimeSpan.FromSeconds(2));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(nextAgent.Name, cut.Markup, StringComparison.Ordinal);
            Assert.Contains(nextSession.Title, cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain(
                harness.Failure.SanitizedDisplayMessage,
                cut.Markup,
                StringComparison.Ordinal);
            Assert.DoesNotContain("provider-secret", cut.Markup, StringComparison.Ordinal);
        });
        Assert.DoesNotContain(
            context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Summary is "Prompt failed" or "Prompt sent");
        Assert.DoesNotContain(harness.FailedRun.Id, harness.Workspace.ExecutionRunDetailRequests);
        Assert.Empty(refresh);
    }

    private static async Task OpenChatAndSendAsync(
        IRenderedComponent<ContextualAgentWorkspaceWindows> cut,
        AgentDefinition agent)
    {
        await OpenChatAsync(cut, agent);
        cut.WaitForElement("[data-testid='chat-prompt-input']")
            .Input("Inspect the selected project structure.");
        await cut.Find("[data-testid='chat-send-button']")
            .ClickAsync(new MouseEventArgs());
    }

    private static async Task OpenChatAsync(
        IRenderedComponent<ContextualAgentWorkspaceWindows> cut,
        AgentDefinition agent)
    {
        await cut.WaitForElement($"[data-testid='context-agents-agent-{agent.Id:N}-open']")
            .DoubleClickAsync(new MouseEventArgs());
    }

    private static IRenderedComponent<ContextualAgentWorkspaceWindows> Render(
        BunitContext context,
        Harness harness,
        List<ContextualAgentWorkspaceRefreshRequest> refresh,
        Action<ContextualAgentWorkspaceRefreshRequest>? onRefresh = null)
    {
        return context.Render<ContextualAgentWorkspaceWindows>(parameters => parameters
            .Add(component => component.WindowId, "context-agents")
            .Add(component => component.ChatWindowId, "context-chat")
            .Add(component => component.TestId, "context-agents")
            .Add(component => component.WindowState, new CanvasWorkbenchWindowState
            {
                IsVisible = true
            })
            .Add(component => component.WorkspaceKind, ContextualAgentWorkspaceKind.ProjectStructure)
            .Add(component => component.ProjectId, harness.ProjectId)
            .Add(
                component => component.WorkspaceRefreshRequested,
                request =>
                {
                    refresh.Add(request);
                    onRefresh?.Invoke(request);
                }));
    }

    private static Harness CreateHarness()
    {
        var projectId = Guid.NewGuid();
        var agent = CreateAgent(projectId);
        var session = CreateSession(agent.Id);
        var failedRun = CreateFailedRun(agent.Id, session.Id);
        var workspaceService = DispatchProxy.Create<
            IAgentFrameworkWorkspaceService,
            ContextualWorkspaceProxy>();
        var workspace = (ContextualWorkspaceProxy)(object)workspaceService;
        workspace.Service = workspaceService;
        workspace.Session = session;
        workspace.InitialWorkspace = CreateWorkspace(agent.Id, session);
        workspace.ReloadedWorkspace = CreateWorkspace(
            agent.Id,
            session,
            CreateFailedRun(agent.Id, session.Id));
        workspace.FailedRunDetail = new ExecutionRunDetail(failedRun, session, [], []);
        var failure = new AgentChatRunFailedException(
            agent.Id,
            failedRun.Id,
            session.Id,
            "OpenAI default",
            "gpt-5.4-mini",
            new InvalidOperationException("api_key=provider-secret"),
            "The configured provider rejected the request.",
            AgentProviderFailureCategory.RequestCompatibility);
        workspace.Failure = failure;

        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton(workspaceService);
        var referenceData = new StubReferenceDataProvider(agent);
        context.Services.AddSingleton<IAgentReferenceDataProvider>(referenceData);
        context.Services.AddSingleton(DispatchProxy.Create<
            IAgentChatAttachmentStagingService,
            UnexpectedCallProxy>());
        context.Services.AddSingleton(DispatchProxy.Create<
            CanDoItAll.AgentFramework.Voice.IAgentVoiceService,
            UnexpectedCallProxy>());
        return new Harness(
            context,
            projectId,
            agent,
            failedRun,
            failure,
            workspace,
            referenceData);
    }

    private static AgentDefinition CreateAgent(
        Guid projectId,
        string name = "Project structure verifier")
    {
        var now = DateTimeOffset.UtcNow;
        var configurationJson = AgentProjectStructureAccessMetadata.Write(
            null,
            new AgentProjectStructureAccessSettings
            {
                CanRead = true,
                CanWrite = true,
                AllowedProjectIds = [projectId]
            });
        return new AgentDefinition(
            Id: Guid.NewGuid(),
            Name: name,
            RoleTitle: "Technical agent",
            Summary: "Exercises contextual failure correlation.",
            Instructions: "Stay scoped.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: "gpt-test",
            Workload: AgentWorkloadKind.General,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: true,
            EnableBackgroundResponses: false,
            ConfigurationJson: configurationJson,
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: ["project-structure"],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static ChatSessionRecord CreateSession(Guid agentId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ChatSessionRecord(
            Guid.NewGuid(),
            agentId,
            "Project structure thread",
            now,
            now,
            []);
    }

    private static ChatAgentWorkspaceSnapshot CreateWorkspace(
        Guid agentId,
        ChatSessionRecord session,
        ExecutionRunRecord? selectedRun = null)
    {
        return new ChatAgentWorkspaceSnapshot(
            agentId,
            [new ChatSessionSummaryRecord(
                session.Id,
                session.AgentId,
                session.Title,
                session.CreatedAtUtc,
                session.UpdatedAtUtc,
                session.Messages.Count,
                "No messages yet.",
                0,
                false)],
            session,
            session.Id,
            LatestRun: null)
        {
            SelectedRun = selectedRun
        };
    }

    private static ExecutionRunRecord CreateFailedRun(Guid agentId, Guid sessionId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Guid.NewGuid(),
            agentId,
            sessionId,
            "Failed project structure run",
            "chat",
            sessionId.ToString("D"),
            Guid.NewGuid().ToString("N"),
            string.Empty,
            "test",
            "test",
            "{}",
            "Inspect the project structure.",
            "The configured provider rejected the request.",
            "OpenAI default",
            "gpt-5.4-mini",
            ExecutionState.Failed,
            RunOutcome.Failed,
            now,
            now,
            now,
            now,
            string.Empty,
            null,
            []);
    }

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

    private static ExecutionRunResult CreateCompletedExecutionResult(
        Guid agentId,
        Guid chatSessionId)
    {
        var now = DateTimeOffset.UtcNow;
        var executionRunId = Guid.NewGuid();
        return new ExecutionRunResult(
            executionRunId,
            chatSessionId,
            "Completed contextual response.",
            AssistantMessage: null,
            new AgentRunMetric(
                Guid.NewGuid(),
                agentId,
                chatSessionId,
                now,
                RunOutcome.Succeeded,
                "test-provider",
                "test-model",
                DurationMs: 1,
                InputTokens: 1,
                OutputTokens: 1,
                ToolCalls: 0)
            {
                ExecutionRunId = executionRunId
            })
        {
            State = ExecutionState.Completed
        };
    }

    private static AgentChatRunResult CreateCompletedChatRunResult(
        Guid agentId,
        Guid chatSessionId)
    {
        var now = DateTimeOffset.UtcNow;
        var executionRunId = Guid.NewGuid();
        return new AgentChatRunResult(
            chatSessionId,
            new ChatMessageRecord(
                Guid.NewGuid(),
                ChatMessageRole.Assistant,
                "Completed contextual approval response.",
                now,
                TokenEstimate: 1),
            new AgentRunMetric(
                Guid.NewGuid(),
                agentId,
                chatSessionId,
                now,
                RunOutcome.Succeeded,
                "test-provider",
                "test-model",
                DurationMs: 1,
                InputTokens: 1,
                OutputTokens: 1,
                ToolCalls: 0)
            {
                ExecutionRunId = executionRunId
            })
        {
            ExecutionRunId = executionRunId,
            State = ExecutionState.Completed
        };
    }

    private static ExecutionRunRecord CreateCompletedRun(
        Guid agentId,
        Guid chatSessionId,
        Guid executionRunId)
    {
        var now = DateTimeOffset.UtcNow;
        return CreateFailedRun(agentId, chatSessionId) with
        {
            Id = executionRunId,
            ResultSummary = "Completed contextual response.",
            State = ExecutionState.Completed,
            Outcome = RunOutcome.Succeeded,
            UpdatedAtUtc = now,
            CompletedAtUtc = now,
            PendingApprovals = []
        };
    }

    private sealed class StubReferenceDataProvider : IAgentReferenceDataProvider
    {
        public StubReferenceDataProvider(params AgentDefinition[] agents)
        {
            Agents.AddRange(agents);
        }

        public List<AgentDefinition> Agents { get; } = [];

        public Task<AgentReferenceDataSnapshot> GetAsync(
            AgentReferenceDataRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AgentReferenceDataSnapshot(
                AgentReferenceDataSections.Agents,
                Agents,
                [],
                new Dictionary<Guid, ProviderProfile>(),
                DateTimeOffset.UtcNow,
                TimeSpan.Zero));
        }
    }

    private class ContextualWorkspaceProxy : DispatchProxy
    {
        private int workspaceRequestCount;
        private EventHandler<ExecutionLogEntry>? executionUpdated;

        public IAgentFrameworkWorkspaceService Service { get; set; } = default!;

        public ChatSessionRecord Session { get; set; } = default!;

        public ChatAgentWorkspaceSnapshot InitialWorkspace { get; set; } = default!;

        public ChatAgentWorkspaceSnapshot ReloadedWorkspace { get; set; } = default!;

        public ExecutionRunDetail FailedRunDetail { get; set; } = default!;

        public AgentChatRunFailedException Failure { get; set; } = default!;

        public Exception? DetailFailure { get; set; }

        public Exception? ReloadWorkspaceFailure { get; set; }

        public TaskCompletionSource ExecutionStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<ExecutionRunResult>? DeferredExecution { get; set; }

        public TaskCompletionSource<AgentChatRunResult>? DeferredApproval { get; set; }

        public Dictionary<Guid, ChatSessionRecord> SessionsByAgent { get; } = [];

        public Dictionary<Guid, ChatAgentWorkspaceSnapshot> AgentWorkspaces { get; } = [];

        public List<Guid> ExecutionRunDetailRequests { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "add_ExecutionUpdated" => AddExecutionUpdated((EventHandler<ExecutionLogEntry>)args![0]!),
                "remove_ExecutionUpdated" => RemoveExecutionUpdated((EventHandler<ExecutionLogEntry>)args![0]!),
                nameof(IAgentFrameworkWorkspaceService.GetOrCreateChatSessionAsync) => GetSession(args!),
                nameof(IAgentFrameworkWorkspaceService.GetChatAgentWorkspaceAsync) => GetWorkspace(args!),
                nameof(IAgentFrameworkWorkspaceService.GetChatRuntimeSnapshotAsync) => Task.FromResult(
                    new ChatRuntimeSnapshot([], [])),
                nameof(IAgentFrameworkWorkspaceService.ExecuteRunAsync) => ExecuteRun(),
                nameof(IAgentFrameworkWorkspaceService.RespondToPendingApprovalsAsync) =>
                    RespondToPendingApproval(),
                nameof(IAgentFrameworkWorkspaceService.GetExecutionRunDetailAsync) => GetRunDetail(args!),
                _ => throw new InvalidOperationException(
                    $"Workspace service member '{targetMethod?.Name}' was not expected in this component test.")
            };
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

        private Task<ChatSessionRecord> GetSession(object?[] args)
        {
            var agentId = Assert.IsType<Guid>(args[0]);
            return Task.FromResult(
                SessionsByAgent.TryGetValue(agentId, out var session)
                    ? session
                    : Session);
        }

        private Task<ChatAgentWorkspaceSnapshot> GetWorkspace(object?[] args)
        {
            var agentId = Assert.IsType<Guid>(args[0]);
            if (AgentWorkspaces.TryGetValue(agentId, out var agentWorkspace))
            {
                return Task.FromResult(agentWorkspace);
            }

            if (Interlocked.Increment(ref workspaceRequestCount) == 1)
            {
                return Task.FromResult(InitialWorkspace);
            }

            return ReloadWorkspaceFailure is null
                ? Task.FromResult(ReloadedWorkspace)
                : Task.FromException<ChatAgentWorkspaceSnapshot>(ReloadWorkspaceFailure);
        }

        private Task<ExecutionRunResult> ExecuteRun()
        {
            ExecutionStarted.TrySetResult();
            return DeferredExecution?.Task ?? Task.FromException<ExecutionRunResult>(Failure);
        }

        private Task<AgentChatRunResult> RespondToPendingApproval()
        {
            return DeferredApproval?.Task ?? Task.FromException<AgentChatRunResult>(Failure);
        }

        private Task<ExecutionRunDetail> GetRunDetail(object?[] args)
        {
            ExecutionRunDetailRequests.Add(Assert.IsType<Guid>(args[0]));
            return DetailFailure is null
                ? Task.FromResult(FailedRunDetail)
                : Task.FromException<ExecutionRunDetail>(DetailFailure);
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

    private sealed record Harness(
        BunitContext Context,
        Guid ProjectId,
        AgentDefinition Agent,
        ExecutionRunRecord FailedRun,
        AgentChatRunFailedException Failure,
        ContextualWorkspaceProxy Workspace,
        StubReferenceDataProvider ReferenceData);
}
