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
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components;

public sealed class AgentChatPanelResponsivenessTests
{
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
        return new AgentChatExecutionOrchestrator(
            workspace,
            registry,
            new AgentChatExecutionNotificationHub(
                NullLogger<AgentChatExecutionNotificationHub>.Instance),
            coordinator,
            new ResponsiveWorkspaceFactory(workspace, scope),
            new ResponsiveDatabaseProfileRuntimeAccessor(profileId),
            new FixedAgentExecutionProfileGenerationSource(
                new DatabaseProfileGeneration(0)),
            TimeProvider.System);
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

        public int WorkspaceRequestCount => Volatile.Read(ref workspaceRequestCount);

        public TaskCompletionSource PostRunRefreshStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<ChatAgentWorkspaceSnapshot> PostRunWorkspace { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
                nameof(IAgentFrameworkWorkspaceService.GetExecutionRunDetailAsync) => Task.FromResult(
                    InitialRunDetail ?? throw new InvalidOperationException("No initial execution detail was configured.")),
                _ => throw new InvalidOperationException(
                    $"Workspace service member '{targetMethod?.Name}' was not expected in this component test.")
            };
        }

        public void RaiseExecutionUpdated(ExecutionLogEntry entry)
            => executionUpdated?.Invoke(Service, entry);

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
                    Task.FromResult(Result));
            }

            if (targetMethod?.Name == nameof(IAgentChatExecutionOrchestrator.StartApprovalContinuation))
            {
                ApprovalStarted.TrySetResult();
                LastStreamId = CreateActivityStreamId();
                return new AgentChatOperationHandle(
                    LastStreamId,
                    Task.FromResult(Result));
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
}
