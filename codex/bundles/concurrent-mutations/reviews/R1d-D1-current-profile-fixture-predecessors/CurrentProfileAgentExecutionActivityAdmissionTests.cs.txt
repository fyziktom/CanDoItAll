using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class CurrentProfileAgentExecutionActivityAdmissionTests
{
    [Fact]
    public async Task Direct_workspace_send_admits_caller_operation_and_publishes_one_terminal_event()
    {
        var profileId = Guid.NewGuid();
        var accessor = new SwitchingDatabaseProfileRuntimeAccessor(profileId);
        var workspace = CreateWorkspace();
        var factory = new SwitchingWorkspaceFactory(accessor);
        factory.Register(profileId, workspace.Service);
        var coordinator = CreateCoordinator();
        using var currentProfile = CreateCurrentProfileService(
            factory,
            accessor,
            coordinator);
        var service = Assert.IsAssignableFrom<IAgentFrameworkWorkspaceService>(
            currentProfile);
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var operationId = AgentExecutionOperationId.New();
        var streamId = CreateStreamId(profileId, operationId);

        var result = await service.SendMessageAsync(
            agentId,
            sessionId,
            "Explain the current state.",
            new AgentChatRunOptions(operationId));

        Assert.Equal(workspace.Proxy.ExecutionRunId, result.ExecutionRunId);
        Assert.Equal(operationId, workspace.Proxy.OperationId);
        var events = await ReadEventsAsync(coordinator, streamId);
        Assert.Equal(AgentExecutionActivityPhase.Accepted, events[0].Event.Phase);
        Assert.Equal(agentId, events[0].Event.AgentId);
        Assert.Equal(sessionId, events[0].Event.ChatSessionId);
        var terminal = Assert.Single(events, item => item.Event.IsTerminal);
        Assert.Equal(AgentExecutionActivityPhase.Completed, terminal.Event.Phase);
        Assert.Equal(
            AgentExecutionActivityTerminalOutcome.Succeeded,
            terminal.Event.TerminalOutcome);
    }

    [Fact]
    public async Task Direct_workspace_pre_io_failure_remains_observable_as_failed_terminal_activity()
    {
        var profileId = Guid.NewGuid();
        var accessor = new SwitchingDatabaseProfileRuntimeAccessor(profileId);
        var workspace = CreateWorkspace(
            new InvalidOperationException("Catalog unavailable."));
        var factory = new SwitchingWorkspaceFactory(accessor);
        factory.Register(profileId, workspace.Service);
        var coordinator = CreateCoordinator();
        using var currentProfile = CreateCurrentProfileService(
            factory,
            accessor,
            coordinator);
        var service = Assert.IsAssignableFrom<IAgentFrameworkWorkspaceService>(
            currentProfile);
        var agentId = Guid.NewGuid();
        var operationId = AgentExecutionOperationId.New();
        var streamId = CreateStreamId(profileId, operationId);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SendMessageAsync(
                agentId,
                chatSessionId: null,
                "Explain the current state.",
                new AgentChatRunOptions(operationId)));

        Assert.Equal("Catalog unavailable.", exception.Message);
        var events = await ReadEventsAsync(coordinator, streamId);
        Assert.Equal(AgentExecutionActivityPhase.Accepted, events[0].Event.Phase);
        var terminal = Assert.Single(events, item => item.Event.IsTerminal);
        Assert.Equal(AgentExecutionActivityPhase.Failed, terminal.Event.Phase);
        Assert.Equal(
            AgentExecutionActivityTerminalOutcome.Failed,
            terminal.Event.TerminalOutcome);
        Assert.Equal(
            AgentExecutionActivityFailureCodes.UnhandledExecutionFailure,
            terminal.Event.ErrorCode);
    }

    [Theory]
    [InlineData(DirectExecutionEntry.ExecuteRun)]
    [InlineData(DirectExecutionEntry.ExecuteSameSourceRun)]
    [InlineData(DirectExecutionEntry.ContinueExecutionRun)]
    [InlineData(DirectExecutionEntry.SendMessage)]
    [InlineData(DirectExecutionEntry.RespondToPendingApprovals)]
    public async Task Direct_execution_entry_admits_before_cold_workspace_resolution_and_disposes_failed_operation(
        DirectExecutionEntry entry)
    {
        var profileId = Guid.NewGuid();
        var accessor = new SwitchingDatabaseProfileRuntimeAccessor(profileId);
        var factory = new SwitchingWorkspaceFactory(accessor);
        var resolutionFailure = new InvalidOperationException(
            "Cold workspace resolution failed.");
        var resolutionBlock = factory.BlockNextResolution(resolutionFailure);
        var coordinator = new TrackingAgentExecutionActivityCoordinator(
            CreateCoordinator());
        using var currentProfile = CreateCurrentProfileService(
            factory,
            accessor,
            coordinator);
        var service = Assert.IsAssignableFrom<IAgentFrameworkWorkspaceService>(
            currentProfile);
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var operationId = AgentExecutionOperationId.New();
        var streamId = CreateStreamId(profileId, operationId);
        var executionTask = Task.Run(
            () => InvokeDirectExecutionEntryAsync(
                service,
                entry,
                agentId,
                sessionId,
                operationId));

        IReadOnlyList<SequencedStreamEnvelope<AgentExecutionActivity>>
            acceptedEvents;
        try
        {
            await resolutionBlock.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            acceptedEvents = await ReadEventsAsync(
                coordinator.Inner,
                streamId);
        }
        finally
        {
            resolutionBlock.Release.TrySetResult(true);
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executionTask);
        Assert.Equal(resolutionFailure.Message, exception.Message);
        var accepted = Assert.Single(acceptedEvents);
        Assert.Equal(AgentExecutionActivityPhase.Accepted, accepted.Event.Phase);
        Assert.False(accepted.Event.IsTerminal);
        var completedEvents = await ReadEventsAsync(coordinator.Inner, streamId);
        var terminal = Assert.Single(
            completedEvents,
            item => item.Event.IsTerminal);
        Assert.Equal(AgentExecutionActivityPhase.Failed, terminal.Event.Phase);
        Assert.Equal(1, coordinator.OperationDisposeCount);
    }

    [Fact]
    public async Task Operation_bound_dispatch_uses_service_pinned_before_profile_switch()
    {
        var firstProfileId = Guid.NewGuid();
        var secondProfileId = Guid.NewGuid();
        var accessor = new SwitchingDatabaseProfileRuntimeAccessor(
            firstProfileId);
        var firstWorkspace = CreateWorkspace();
        var secondWorkspace = CreateWorkspace();
        var factory = new SwitchingWorkspaceFactory(accessor);
        factory.Register(firstProfileId, firstWorkspace.Service);
        factory.Register(secondProfileId, secondWorkspace.Service);
        var coordinator = CreateCoordinator();
        using var currentProfile = CreateCurrentProfileService(
            factory,
            accessor,
            coordinator);
        var service = Assert.IsAssignableFrom<
            IAgentFrameworkWorkspaceActivityExecutionService>(currentProfile);
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var operationId = AgentExecutionOperationId.New();
        var streamId = CreateStreamId(firstProfileId, operationId);
        using var admittedOperation = Assert.IsType<AgentExecutionActivityAdmitted>(
            coordinator.AdmitOperation(
                streamId,
                agentId,
                sessionId,
                "Accepted."))
            .Operation;
        using var switchingOperation = new SwitchingAgentOperationLease(
            admittedOperation,
            () => accessor.SwitchTo(secondProfileId));

        await service.SendMessageWithinOperationAsync(
            switchingOperation,
            agentId,
            sessionId,
            "Explain the current state.",
            new AgentChatRunOptions(operationId));

        Assert.Equal(1, firstWorkspace.Proxy.SendCallCount);
        Assert.Equal(0, secondWorkspace.Proxy.SendCallCount);
        var events = await ReadEventsAsync(coordinator, streamId);
        Assert.Single(events, item => item.Event.IsTerminal);
        Assert.Equal(
            AgentExecutionActivityPhase.Completed,
            events[^1].Event.Phase);
    }

    [Fact]
    public async Task Blocked_compatibility_subscriber_does_not_delay_send_or_activity_terminal()
    {
        var profileId = Guid.NewGuid();
        var accessor = new SwitchingDatabaseProfileRuntimeAccessor(profileId);
        var workspace = CreateWorkspace();
        workspace.Proxy.RaiseExecutionUpdatedDuringSend = true;
        var factory = new SwitchingWorkspaceFactory(accessor);
        factory.Register(profileId, workspace.Service);
        var coordinator = CreateCoordinator();
        using var currentProfile = CreateCurrentProfileService(
            factory,
            accessor,
            coordinator);
        var service = Assert.IsAssignableFrom<IAgentFrameworkWorkspaceService>(
            currentProfile);
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var operationId = AgentExecutionOperationId.New();
        var streamId = CreateStreamId(profileId, operationId);
        var subscriberEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriberRelease = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriberFinished = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<ExecutionLogEntry> subscriber = (_, _) =>
        {
            subscriberEntered.TrySetResult(true);
            subscriberRelease.Task.GetAwaiter().GetResult();
            subscriberFinished.TrySetResult(true);
        };
        service.ExecutionUpdated += subscriber;

        try
        {
            var sendTask = service.SendMessageAsync(
                agentId,
                sessionId,
                "Explain the current state.",
                new AgentChatRunOptions(operationId));

            await subscriberEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var result = await sendTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(workspace.Proxy.ExecutionRunId, result.ExecutionRunId);
            var events = await ReadEventsAsync(coordinator, streamId);
            Assert.Single(events, item => item.Event.IsTerminal);
            Assert.Equal(
                AgentExecutionActivityPhase.Completed,
                events[^1].Event.Phase);
            Assert.False(subscriberFinished.Task.IsCompleted);
        }
        finally
        {
            subscriberRelease.TrySetResult(true);
            await subscriberFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));
            service.ExecutionUpdated -= subscriber;
        }
    }

    private static AgentExecutionActivityCoordinator CreateCoordinator()
    {
        var timeProvider = TimeProvider.System;
        return new AgentExecutionActivityCoordinator(
            new PartitionedSequencedStream<
                AgentExecutionActivityStreamId,
                AgentExecutionActivity>(
                PartitionedSequencedStreamPolicy.Default,
                timeProvider),
            timeProvider);
    }

    private static IDisposable CreateCurrentProfileService(
        ICanDoItAllAgentWorkspaceFactory workspaceFactory,
        IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
        IAgentExecutionActivityCoordinator coordinator)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        using var serviceProvider = services.BuildServiceProvider();
        var implementationType = typeof(AgentFrameworkModuleServiceCollectionExtensions)
            .Assembly
            .GetType(
                "CanDoItAll.Modules.AgentFramework.CurrentProfileAgentFrameworkWorkspaceService",
                throwOnError: true)!;
        var loggerType = typeof(ILogger<>).MakeGenericType(implementationType);
        var logger = serviceProvider.GetRequiredService(loggerType);
        var technicalAgentBridge =
            DispatchProxy.Create<IAiTechnicalAgentBridge, UnusedTechnicalAgentBridgeProxy>();
        var constructor = Assert.Single(implementationType.GetConstructors());
        var instance = constructor.Invoke(
            [
                workspaceFactory,
                technicalAgentBridge,
                new NoOpReferenceDataCacheInvalidator(),
                databaseProfileRuntimeAccessor,
                new DatabaseSwitchNotificationService(),
                coordinator,
                new FixedAgentExecutionProfileGenerationSource(
                    new DatabaseProfileGeneration(0)),
                logger
            ]);
        return Assert.IsAssignableFrom<IDisposable>(instance);
    }

    private static async Task InvokeDirectExecutionEntryAsync(
        IAgentFrameworkWorkspaceService service,
        DirectExecutionEntry entry,
        Guid agentId,
        Guid sessionId,
        AgentExecutionOperationId operationId)
    {
        switch (entry)
        {
            case DirectExecutionEntry.ExecuteRun:
                await service.ExecuteRunAsync(
                    new ExecutionRunRequest(
                        agentId,
                        "Explain the current state.",
                        operationId,
                        sessionId));
                return;
            case DirectExecutionEntry.ExecuteSameSourceRun:
                await service.ExecuteSameSourceRunAsync(
                    new ExecutionRunSourceKey("test", "cold-resolution"),
                    new ExecutionRunRequest(
                        agentId,
                        "Explain the current state.",
                        operationId,
                        sessionId));
                return;
            case DirectExecutionEntry.ContinueExecutionRun:
                await service.ContinueExecutionRunAsync(
                    Guid.NewGuid(),
                    operationId,
                    decisions: [new PendingToolApprovalDecision("approval-1", Approved: true)]);
                return;
            case DirectExecutionEntry.SendMessage:
                await service.SendMessageAsync(
                    agentId,
                    sessionId,
                    "Explain the current state.",
                    new AgentChatRunOptions(operationId));
                return;
            case DirectExecutionEntry.RespondToPendingApprovals:
                await service.RespondToPendingApprovalsAsync(
                    agentId,
                    sessionId,
                    operationId,
                    decisions: [new PendingToolApprovalDecision("approval-1", Approved: true)]);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(entry), entry, null);
        }
    }

    private static (
        ICompositeWorkspaceService Service,
        ActivityWorkspaceServiceProxy Proxy) CreateWorkspace(
        Exception? failure = null)
    {
        var service =
            DispatchProxy.Create<ICompositeWorkspaceService, ActivityWorkspaceServiceProxy>();
        var proxy = (ActivityWorkspaceServiceProxy)(object)service;
        proxy.Failure = failure;
        return (service, proxy);
    }

    private static AgentExecutionActivityStreamId CreateStreamId(
        Guid profileId,
        AgentExecutionOperationId operationId)
    {
        return new AgentExecutionActivityStreamId(
            profileId,
            WorkspaceScopeDescriptor.Organization(profileId.ToString("N")),
            new DatabaseProfileGeneration(0),
            operationId);
    }

    private static async Task<
        IReadOnlyList<SequencedStreamEnvelope<AgentExecutionActivity>>>
        ReadEventsAsync(
            AgentExecutionActivityCoordinator coordinator,
            AgentExecutionActivityStreamId streamId)
    {
        await using var reader = coordinator.OpenReader(
            streamId,
            StreamSequence.Beginning);
        var result = Assert.IsType<
            SequencedStreamEvents<AgentExecutionActivity>>(
            await reader.ReadAsync());
        return result.Items;
    }

    private static AgentChatRunResult CreateRunResult(
        Guid agentId,
        Guid sessionId,
        Guid executionRunId)
    {
        return new AgentChatRunResult(
            sessionId,
            new ChatMessageRecord(
                Guid.NewGuid(),
                ChatMessageRole.Assistant,
                "Done",
                DateTimeOffset.UtcNow,
                TokenEstimate: 1),
            new AgentRunMetric(
                Guid.NewGuid(),
                agentId,
                sessionId,
                DateTimeOffset.UtcNow,
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

    private interface ICompositeWorkspaceService :
        IAgentFrameworkWorkspaceService,
        IAgentFrameworkWorkspaceActivityExecutionService
    {
    }

    private class ActivityWorkspaceServiceProxy : DispatchProxy
    {
        public Exception? Failure { get; set; }

        public Guid ExecutionRunId { get; } = Guid.NewGuid();

        public AgentExecutionOperationId? OperationId { get; private set; }

        public int SendCallCount { get; private set; }

        public bool RaiseExecutionUpdatedDuringSend { get; set; }

        private readonly Lock eventGate = new();
        private EventHandler<ExecutionLogEntry>? executionUpdated;

        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == "add_ExecutionUpdated")
            {
                lock (eventGate)
                {
                    executionUpdated += Assert.IsType<
                        EventHandler<ExecutionLogEntry>>(args![0]);
                }

                return null;
            }

            if (targetMethod.Name == "remove_ExecutionUpdated")
            {
                lock (eventGate)
                {
                    executionUpdated -= Assert.IsType<
                        EventHandler<ExecutionLogEntry>>(args![0]);
                }

                return null;
            }

            if (targetMethod.Name !=
                nameof(IAgentFrameworkWorkspaceActivityExecutionService.SendMessageWithinOperationAsync))
            {
                throw new NotSupportedException(
                    $"Unexpected workspace call '{targetMethod.Name}'.");
            }

            SendCallCount++;
            var operation = Assert.IsAssignableFrom<
                IAgentExecutionActivityOperationLease>(args![0]);
            OperationId = operation.StreamId.OperationId;
            if (Failure is not null)
            {
                return Task.FromException<AgentChatRunResult>(Failure);
            }

            var agentId = Assert.IsType<Guid>(args[1]);
            var sessionId = Assert.IsType<Guid>(args[2]);
            if (RaiseExecutionUpdatedDuringSend)
            {
                RaiseExecutionUpdated(agentId, sessionId);
            }

            operation.BindExecutionRun(ExecutionRunId, sessionId);
            operation.Report(
                AgentExecutionActivityPhase.PersistingResult,
                "Persisting the test result.");
            operation.Complete("The test operation completed.");
            return Task.FromResult(
                CreateRunResult(
                    agentId,
                    sessionId,
                    ExecutionRunId));
        }

        private void RaiseExecutionUpdated(
            Guid agentId,
            Guid chatSessionId)
        {
            EventHandler<ExecutionLogEntry>? subscribers;
            lock (eventGate)
            {
                subscribers = executionUpdated;
            }

            subscribers?.Invoke(
                this,
                new ExecutionLogEntry(
                    Guid.NewGuid(),
                    agentId,
                    chatSessionId,
                    DateTimeOffset.UtcNow,
                    ExecutionState.Running,
                    "Test",
                    "Compatibility update")
                {
                    ExecutionRunId = ExecutionRunId
                });
        }
    }

    private sealed class SwitchingWorkspaceFactory(
        SwitchingDatabaseProfileRuntimeAccessor runtimeAccessor)
        : ICanDoItAllAgentWorkspaceFactory
    {
        private readonly Dictionary<Guid, IAgentFrameworkWorkspaceService> services = [];
        private ResolutionBlock? nextResolutionBlock;

        public void Register(
            Guid profileId,
            IAgentFrameworkWorkspaceService service)
        {
            services.Add(profileId, service);
        }

        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService()
        {
            return GetWorkspaceService(GetOrganizationScope());
        }

        public IAgentFrameworkWorkspaceService GetWorkspaceService(
            WorkspaceScopeDescriptor scope)
        {
            var resolutionBlock = Interlocked.Exchange(
                ref nextResolutionBlock,
                null);
            if (resolutionBlock is not null)
            {
                resolutionBlock.Entered.TrySetResult(true);
                resolutionBlock.Release.Task.GetAwaiter().GetResult();
                throw resolutionBlock.Failure;
            }

            var profileId = ParseProfileId(scope);
            return services[profileId];
        }

        public WorkspaceScopeDescriptor GetOrganizationScope()
        {
            var profileId = runtimeAccessor
                .ResolveCurrentProfile()
                .Profile
                .Id;
            return WorkspaceScopeDescriptor.Organization(
                profileId.ToString("N"));
        }

        public string GetWorkspaceRoot()
        {
            return string.Empty;
        }

        public ResolutionBlock BlockNextResolution(Exception failure)
        {
            ArgumentNullException.ThrowIfNull(failure);
            var resolutionBlock = new ResolutionBlock(failure);
            Assert.Null(Interlocked.CompareExchange(
                ref nextResolutionBlock,
                resolutionBlock,
                null));
            return resolutionBlock;
        }

        private static Guid ParseProfileId(
            WorkspaceScopeDescriptor scope)
        {
            return scope.Kind == WorkspaceScopeKind.Organization &&
                Guid.TryParseExact(scope.Key, "N", out var profileId)
                    ? profileId
                    : throw new InvalidOperationException(
                        "The test workspace scope is invalid.");
        }
    }

    private sealed class ResolutionBlock(Exception failure)
    {
        public Exception Failure { get; } = failure;

        public TaskCompletionSource<bool> Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class TrackingAgentExecutionActivityCoordinator(
        AgentExecutionActivityCoordinator inner)
        : IAgentExecutionActivityCoordinator
    {
        private int operationDisposeCount;

        public AgentExecutionActivityCoordinator Inner { get; } = inner;

        public int OperationDisposeCount =>
            Volatile.Read(ref operationDisposeCount);

        public AgentExecutionActivityAdmission AdmitOperation(
            AgentExecutionActivityStreamId streamId,
            Guid? agentId,
            Guid? chatSessionId,
            string acceptedMessage)
        {
            return Inner.AdmitOperation(
                streamId,
                agentId,
                chatSessionId,
                acceptedMessage) switch
            {
                AgentExecutionActivityAdmitted admitted =>
                    new AgentExecutionActivityAdmitted(
                        admitted.StreamId,
                        new TrackingAgentExecutionActivityOperationLease(
                            admitted.Operation,
                            () => Interlocked.Increment(
                                ref operationDisposeCount))),
                var admission => admission
            };
        }
    }

    private sealed class TrackingAgentExecutionActivityOperationLease(
        IAgentExecutionActivityOperationLease inner,
        Action onDispose)
        : IAgentExecutionActivityOperationLease
    {
        public AgentExecutionActivityStreamId StreamId => inner.StreamId;

        public Guid? AgentId => inner.AgentId;

        public Guid? ChatSessionId => inner.ChatSessionId;

        public Guid? ExecutionRunId => inner.ExecutionRunId;

        public bool IsTerminal => inner.IsTerminal;

        public void BindAgent(Guid agentId)
        {
            inner.BindAgent(agentId);
        }

        public void BindContext(
            AgentChatContextSource source,
            long version)
        {
            inner.BindContext(source, version);
        }

        public void BindChatSession(Guid sessionId)
        {
            inner.BindChatSession(sessionId);
        }

        public void BindExecutionRun(
            Guid runId,
            Guid? sessionId = null)
        {
            inner.BindExecutionRun(runId, sessionId);
        }

        public void Report(
            AgentExecutionActivityPhase phase,
            string message)
        {
            inner.Report(phase, message);
        }

        public void Complete(string message)
        {
            inner.Complete(message);
        }

        public void Fail(
            string message,
            string? errorCode = null)
        {
            inner.Fail(message, errorCode);
        }

        public void Cancel(string message)
        {
            inner.Cancel(message);
        }

        public void Suspend(string message)
        {
            inner.Suspend(message);
        }

        public void Dispose()
        {
            try
            {
                inner.Dispose();
            }
            finally
            {
                onDispose();
            }
        }
    }

    private sealed class SwitchingDatabaseProfileRuntimeAccessor(
        Guid initialProfileId) : IDatabaseProfileRuntimeAccessor
    {
        private readonly Lock gate = new();
        private Guid profileId = initialProfileId;

        public ResolvedDatabaseProfile ResolveCurrentProfile()
        {
            lock (gate)
            {
                return CreateResolvedProfile(profileId);
            }
        }

        public ResolvedDatabaseProfile ResolveProfile(Guid requestedProfileId)
        {
            return CreateResolvedProfile(requestedProfileId);
        }

        public void SwitchTo(Guid nextProfileId)
        {
            lock (gate)
            {
                profileId = nextProfileId;
            }
        }
    }

    private sealed class SwitchingAgentOperationLease(
        IAgentExecutionActivityOperationLease inner,
        Action switchProfile) : IAgentExecutionActivityOperationLease
    {
        private int switched;

        public AgentExecutionActivityStreamId StreamId => inner.StreamId;

        public Guid? AgentId
        {
            get
            {
                if (Interlocked.Exchange(ref switched, 1) == 0)
                {
                    switchProfile();
                }

                return inner.AgentId;
            }
        }

        public Guid? ChatSessionId => inner.ChatSessionId;

        public Guid? ExecutionRunId => inner.ExecutionRunId;

        public bool IsTerminal => inner.IsTerminal;

        public void BindAgent(Guid agentId)
        {
            inner.BindAgent(agentId);
        }

        public void BindContext(
            AgentChatContextSource source,
            long version)
        {
            inner.BindContext(source, version);
        }

        public void BindChatSession(Guid sessionId)
        {
            inner.BindChatSession(sessionId);
        }

        public void BindExecutionRun(
            Guid runId,
            Guid? sessionId = null)
        {
            inner.BindExecutionRun(runId, sessionId);
        }

        public void Report(
            AgentExecutionActivityPhase phase,
            string message)
        {
            inner.Report(phase, message);
        }

        public void Complete(string message)
        {
            inner.Complete(message);
        }

        public void Fail(
            string message,
            string? errorCode = null)
        {
            inner.Fail(message, errorCode);
        }

        public void Cancel(string message)
        {
            inner.Cancel(message);
        }

        public void Suspend(string message)
        {
            inner.Suspend(message);
        }

        public void Dispose()
        {
        }
    }

    private sealed class NoOpReferenceDataCacheInvalidator :
        IAgentReferenceDataCacheInvalidator
    {
        public event EventHandler? Invalidated
        {
            add
            {
            }
            remove
            {
            }
        }

        public void Invalidate()
        {
        }
    }

    private class UnusedTechnicalAgentBridgeProxy : DispatchProxy
    {
        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            throw new NotSupportedException(
                $"Unexpected technical-agent bridge call '{targetMethod?.Name}'.");
        }
    }

    private static ResolvedDatabaseProfile CreateResolvedProfile(
        Guid profileId)
    {
        return new ResolvedDatabaseProfile(
            new DatabaseProfileRecord
            {
                Id = profileId,
                DisplayName = $"Test profile {profileId:N}",
                ProviderKind = DatabaseProviderKind.InMemory,
                SourceKind = DatabaseProfileSourceKind.InMemory
            },
            DatabaseProfileResolutionSource.ExplicitOverride,
            $"in-memory:{profileId:N}");
    }

    public enum DirectExecutionEntry
    {
        ExecuteRun,
        ExecuteSameSourceRun,
        ContinueExecutionRun,
        SendMessage,
        RespondToPendingApprovals
    }
}
