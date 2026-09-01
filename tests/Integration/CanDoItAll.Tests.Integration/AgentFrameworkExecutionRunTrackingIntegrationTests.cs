using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel.Streaming;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

using CanDoItAll.AgentFramework.Runtime.Abstractions;
namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentFrameworkExecutionRunTrackingIntegrationTests(ITestOutputHelper output)
{
    private static readonly TimeSpan AsyncObservationTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task ExecutionUpdated_subscriber_failure_is_isolated_after_persistence()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-startup-subscriber-failure");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: UseStartupBaselineHarness);

        await using var scope = provider.CreateAsyncScope();
        var milestones = scope.ServiceProvider.GetRequiredService<StartupMilestoneRecorder>();
        var runtime = scope.ServiceProvider.GetRequiredService<StartupBarrierAgentRuntime>();
        var eventSink = scope.ServiceProvider.GetRequiredService<RecordingAgentExecutionEventSink>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
        var planningObserved = new TaskCompletionSource<ExecutionLogEntry>(TaskCreationOptions.RunContinuationsAsynchronously);
        var laterSubscriberObserved = new TaskCompletionSource<ExecutionLogEntry>(TaskCreationOptions.RunContinuationsAsynchronously);

        milestones.Reset();
        workspaceService.ExecutionUpdated += (_, entry) =>
        {
            if (!string.Equals(entry.Phase, "Planning", StringComparison.Ordinal))
            {
                return;
            }

            milestones.Record(StartupMilestone.ExecutionUpdated);
            planningObserved.TrySetResult(entry);
            throw new InvalidOperationException("Intentional startup subscriber failure.");
        };
        workspaceService.ExecutionUpdated += (_, entry) =>
        {
            if (string.Equals(entry.Phase, "Planning", StringComparison.Ordinal))
            {
                laterSubscriberObserved.TrySetResult(entry);
            }
        };

        var sendTask = workspaceService.SendMessageAsync(
            agent.Id,
            session.Id,
            "Record deterministic startup milestones without invoking a provider.",
            options: new AgentChatRunOptions(AgentExecutionOperationId.New()));
        ExecutionLogEntry planningEntry;
        try
        {
            planningEntry = await WaitForObservationOrPropagateAsync(
                planningObserved.Task,
                sendTask,
                "Planning execution update");
            var laterEntry = await laterSubscriberObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var publishedEvent = await eventSink.PlanningPublished.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var runtimeExecutionRunId = await runtime.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var persistedDetail = await executionRunStore.GetExecutionRunDetailAsync(planningEntry.ExecutionRunId);

            Assert.Equal(planningEntry.Id, laterEntry.Id);
            Assert.Equal(planningEntry.ExecutionRunId, publishedEvent.ExecutionRunId);
            Assert.Equal(planningEntry.ExecutionRunId, runtimeExecutionRunId);
            Assert.NotNull(persistedDetail);
            Assert.Contains(
                persistedDetail.ExecutionLog,
                entry => entry.ExecutionRunId == planningEntry.ExecutionRunId &&
                         entry.State == ExecutionState.Preparing &&
                         entry.Phase == "Planning");
        }
        finally
        {
            runtime.Release.TrySetResult(true);
        }

        await sendTask;

        var completedDetail = await executionRunStore.GetExecutionRunDetailAsync(planningEntry.ExecutionRunId);

        Assert.NotNull(completedDetail);
        Assert.Equal(ExecutionState.Completed, completedDetail.Run.State);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SendMessageAsync_records_current_startup_baseline(
        bool preparationWarmed,
        bool existingSession)
    {
        var scenario = $"{(preparationWarmed ? "warm" : "cold")}-{(existingSession ? "existing" : "new")}";
        await using var testEnvironment = CanDoItAllTestEnvironment.Create(
            $"integration-agentframework-startup-baseline-{scenario}");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: UseStartupBaselineHarness);

        await using var scope = provider.CreateAsyncScope();
        var milestones = scope.ServiceProvider.GetRequiredService<StartupMilestoneRecorder>();
        var runtime = scope.ServiceProvider.GetRequiredService<StartupBarrierAgentRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var preparationCache = scope.ServiceProvider.GetRequiredService<IAgentExecutionPreparationCache>();
        Guid? chatSessionId = null;
        if (existingSession)
        {
            var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
            chatSessionId = session.Id;
        }

        if (preparationWarmed)
        {
            var preparationService = new AgentExecutionPreparationService(
                scope.ServiceProvider.GetRequiredService<IStartupBaselineWorkspaceStore>(),
                scope.ServiceProvider.GetRequiredService<
                    IProviderRuntimeProfileSnapshotSource>(),
                preparationCache,
                scope.ServiceProvider.GetRequiredService<IAgentExecutionProfileGenerationSource>(),
                scope.ServiceProvider.GetRequiredService<AgentExecutionActivityWorkspaceIdentity>());
            var warmedPreparation = await preparationService.AcquireForAtomicConsumerAsync(agent.Id);

            Assert.Equal(AgentExecutionPreparationSource.Refreshed, warmedPreparation.Source);
            Assert.Equal(agent.Id, warmedPreparation.Blueprint.Agent.Id);
        }

        milestones.Reset();
        var cacheBeforeSend = preparationCache.Snapshot();
        var planningObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        workspaceService.ExecutionUpdated += (_, entry) =>
        {
            if (string.Equals(entry.Phase, "Planning", StringComparison.Ordinal))
            {
                milestones.Record(StartupMilestone.ExecutionUpdated);
                planningObserved.TrySetResult();
            }
        };

        var sendTask = workspaceService.SendMessageAsync(
            agent.Id,
            chatSessionId,
            "Record deterministic startup milestones without invoking a provider.",
            options: new AgentChatRunOptions(AgentExecutionOperationId.New()));

        await WaitForObservationOrPropagateAsync(
            runtime.Entered.Task,
            sendTask,
            "Runtime entry");
        await planningObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var cacheAfterSend = preparationCache.Snapshot();

        Assert.Equal(0, milestones.Count(StartupMilestone.CatalogLoad));
        Assert.Equal(1, milestones.Count(StartupMilestone.CatalogSnapshotLoad));
        Assert.Equal(
            1,
            milestones.Count(
                StartupMilestone.ActivityAcceptedPublished));
        Assert.Equal(0, milestones.Count(StartupMilestone.ProviderProfileGet));
        Assert.Equal(1, milestones.Count(StartupMilestone.ProviderSnapshotAcquire));
        Assert.Equal(3, milestones.Count(StartupMilestone.ProviderSnapshotCapture));
        Assert.Equal(0, milestones.Count(StartupMilestone.ChatSessionGet));
        Assert.Equal(0, milestones.Count(StartupMilestone.ChatRunSummariesList));
        Assert.Equal(1, milestones.Count(StartupMilestone.AtomicChatRunStart));
        Assert.Equal(0, milestones.Count(StartupMilestone.ExecutionRunDetailGet));
        Assert.Equal(0, milestones.Count(StartupMilestone.ExecutionRunDetailSave));
        Assert.Equal(
            1,
            milestones.Count(StartupMilestone.ExecutionRunDetailUpdate));
        Assert.Equal(
            preparationWarmed ? 0 : 1,
            cacheAfterSend.RefreshedCount - cacheBeforeSend.RefreshedCount);
        Assert.Equal(
            preparationWarmed ? 1 : 0,
            cacheAfterSend.ReusedCount - cacheBeforeSend.ReusedCount);
        Assert.Equal(
            0,
            cacheAfterSend.RejectedCount - cacheBeforeSend.RejectedCount);
        AssertMilestoneOrder(
            milestones,
            StartupMilestone.ActivityAcceptedPublished,
            StartupMilestone.CatalogSnapshotLoad,
            StartupMilestone.ProviderSnapshotAcquire,
            StartupMilestone.ExecutionEventPublished,
            StartupMilestone.RuntimeEntered);

        output.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{milestones.CreateDiagnosticLine(scenario)} preparation-cache-refreshed-delta={cacheAfterSend.RefreshedCount - cacheBeforeSend.RefreshedCount} preparation-cache-reused-delta={cacheAfterSend.ReusedCount - cacheBeforeSend.ReusedCount}"));

        runtime.Release.TrySetResult(true);
        await sendTask;
    }

    [Fact]
    public async Task ExecuteRunAsync_refreshes_run_header_while_progress_logs_are_streaming()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-run-tracking");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton<FakeProgressAgentRuntime>();
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<FakeProgressAgentRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<FakeProgressAgentRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);

        var executionTask = workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Implement the current process step.",
                AgentExecutionOperationId.New(),
                session.Id,
                new ExecutionInvocationContext(
                    SourceKind: "process-step",
                    SourceId: "step-001",
                    CorrelationId: "corr-001",
                    CausationId: "cause-001",
                    RequestedBy: "process-automation-dispatch",
                    RequestedByKind: "system",
                    MetadataJson: "{}",
                    ProcessRunId: "run-001",
                    ProcessStepId: "step-001"),
                AutoApprovePendingToolCalls: true));

        var executionRunId = await WaitForExecutionRunIdAsync(runtime, executionTask);
        await runtime.ProgressPersisted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var detail = await executionRunStore.GetExecutionRunDetailAsync(executionRunId);

        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Running, detail.Run.State);
        Assert.NotEmpty(detail.ExecutionLog);
        Assert.True(detail.Run.UpdatedAtUtc >= detail.ExecutionLog.Max(item => item.CreatedAtUtc));
        Assert.NotNull(detail.ChatSession);
        Assert.Equal(executionRunId, detail.ChatSession!.LatestExecutionRunId);
        Assert.Contains(
            detail.ExecutionLog,
            entry => entry.ExecutionRunId == executionRunId &&
                     entry.State == ExecutionState.Running &&
                     entry.Phase == "Implementation");

        runtime.AllowCompletion.TrySetResult(true);

        var result = await executionTask;
        Assert.Equal(executionRunId, result.ExecutionRunId);
    }

    [Fact]
    public async Task SendMessageAsync_persists_provider_request_compatibility_evidence()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create(
            "integration-agentframework-request-compatibility-evidence");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton<FakeProgressAgentRuntime>();
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider =>
                    serviceProvider.GetRequiredService<FakeProgressAgentRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<FakeProgressAgentRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
        runtime.RequestCompatibilityEvidence = CreateRequestCompatibilityEvidence(
            agent.ProviderProfileId);

        var sendTask = workspaceService.SendMessageAsync(
            agent.Id,
            session.Id,
            "Read the canonical project structure.",
            options: new AgentChatRunOptions(AgentExecutionOperationId.New()));
        var executionRunId = await WaitForExecutionRunIdAsync(runtime, sendTask);
        runtime.AllowCompletion.TrySetResult(true);
        await sendTask;

        var detail = await executionRunStore.GetExecutionRunDetailAsync(executionRunId);

        var evidence = Assert.IsType<ProviderRequestCompatibilityEvidence>(
            detail?.Run.EntryAgentRequestCompatibilityEvidence);
        Assert.Equal(ProviderTransportKind.ChatCompletions, evidence.Transport);
        Assert.Equal(AgentReasoningEffortLevel.Medium, evidence.RequestedEffort);
        Assert.Equal(AgentReasoningEffortLevel.None, evidence.EffectiveEffort);
        Assert.Equal(ProviderRequestCompatibilityDisposition.Adjusted, evidence.Disposition);
    }

    [Fact]
    public async Task SendMessageAsync_persists_compatibility_evidence_from_failed_dispatch()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create(
            "integration-agentframework-failed-request-compatibility-evidence");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton<FakeProgressAgentRuntime>();
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider =>
                    serviceProvider.GetRequiredService<FakeProgressAgentRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<FakeProgressAgentRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
        var expectedEvidence = CreateRequestCompatibilityEvidence(agent.ProviderProfileId);
        runtime.Failure = new AgentRuntimeUsageException(
            "Provider dispatch failed after compatibility adjustment.",
            new InvalidOperationException("Synthetic provider rejection."),
            [],
            entryAgentRequestCompatibilityEvidence: expectedEvidence,
            failureOrigin: AgentRuntimeFailureOrigin.Provider);

        var sendTask = workspaceService.SendMessageAsync(
            agent.Id,
            session.Id,
            "Read the canonical project structure.",
            options: new AgentChatRunOptions(AgentExecutionOperationId.New()));
        var executionRunId = await WaitForExecutionRunIdAsync(runtime, sendTask);
        runtime.AllowCompletion.TrySetResult(true);

        await Assert.ThrowsAsync<AgentChatRunFailedException>(() => sendTask);
        var detail = await executionRunStore.GetExecutionRunDetailAsync(executionRunId);

        Assert.Equal(expectedEvidence, detail?.Run.EntryAgentRequestCompatibilityEvidence);
        Assert.Equal(ExecutionState.Failed, detail?.Run.State);
    }

    [Theory]
    [InlineData(AgentRuntimeFailureOrigin.Provider, true)]
    [InlineData(AgentRuntimeFailureOrigin.Tool, false)]
    public async Task Runtime_failure_origin_controls_provider_attribution_end_to_end(
        AgentRuntimeFailureOrigin failureOrigin,
        bool expectedProviderFailure)
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create(
            $"integration-agentframework-failure-origin-{failureOrigin}");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton<FakeProgressAgentRuntime>();
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider =>
                    serviceProvider.GetRequiredService<FakeProgressAgentRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<FakeProgressAgentRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
        runtime.Failure = new AgentRuntimeUsageException(
            "Runtime boundary failure.",
            new InvalidOperationException("api_key=failure-origin-secret"),
            [],
            failureOrigin: failureOrigin);

        var operation = workspaceService.SendMessageAsync(
            agent.Id,
            session.Id,
            "Classify the runtime failure origin.",
            options: new AgentChatRunOptions(AgentExecutionOperationId.New()));
        var executionRunId = await WaitForExecutionRunIdAsync(runtime, operation);
        runtime.AllowCompletion.TrySetResult(true);

        var exception = await Assert.ThrowsAsync<AgentChatRunFailedException>(() => operation);
        var detail = await executionRunStore.GetExecutionRunDetailAsync(executionRunId);

        Assert.Equal(
            expectedProviderFailure
                ? AgentProviderFailureCategory.ProviderError
                : null,
            exception.FailureCategory);
        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Failed, detail.Run.State);
        Assert.DoesNotContain("failure-origin-secret", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("failure-origin-secret", detail.Run.ResultSummary, StringComparison.Ordinal);
        if (expectedProviderFailure)
        {
            Assert.Contains("provider", detail.Run.ResultSummary, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Contains(
                "outside a confirmed provider failure",
                detail.Run.ResultSummary,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendMessageAsync_runtime_failure_persists_failed_log_and_activity_after_store_reopen(bool failDuringStartup) {
        const string prompt = "Preserve this private prompt marker: startup-failure-prompt-sentinel.";
        const string promptSentinel = "startup-failure-prompt-sentinel";
        const string secretSentinel = "startup-failure-secret-sentinel";
        await using var testEnvironment = CanDoItAllTestEnvironment.Create(
            $"integration-agentframework-durable-runtime-failure-{failDuringStartup}");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services => {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton<FakeProgressAgentRuntime>();
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider =>
                    serviceProvider.GetRequiredService<FakeProgressAgentRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<FakeProgressAgentRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var activities = scope.ServiceProvider.GetRequiredService<AgentExecutionActivityCoordinator>();
        var workspaceIdentity = scope.ServiceProvider.GetRequiredService<AgentExecutionActivityWorkspaceIdentity>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
        var originalFailure = new InvalidOperationException($"Runtime failure with api_key={secretSentinel}");
        Exception expectedFailure = failDuringStartup
            ? originalFailure
            : new AgentRuntimeUsageException(
                "The simulated provider boundary rejected the request.",
                originalFailure,
                [],
                failureOrigin: AgentRuntimeFailureOrigin.Provider);
        if (failDuringStartup) {
            runtime.StartupFailure = expectedFailure;
        } else {
            runtime.Failure = expectedFailure;
        }
        runtime.AllowCompletion.TrySetResult(true);
        var operationId = AgentExecutionOperationId.New();

        var exception = await Assert.ThrowsAsync<AgentChatRunFailedException>(() =>
            workspaceService.SendMessageAsync(
                agent.Id,
                session.Id,
                prompt,
                options: new AgentChatRunOptions(operationId)).WaitAsync(AsyncObservationTimeout));
        var executionRunId = await runtime.ExecutionRunIdObserved.Task.WaitAsync(AsyncObservationTimeout);

        Assert.Same(expectedFailure, exception.InnerException);
        Assert.Same(originalFailure, failDuringStartup ? exception.InnerException : exception.InnerException?.InnerException);
        Assert.Equal(agent.Id, exception.AgentId);
        Assert.Equal(session.Id, exception.ChatSessionId);
        Assert.Equal(executionRunId, exception.ExecutionRunId);
        Assert.Equal(
            failDuringStartup ? null : AgentProviderFailureCategory.ProviderError,
            exception.FailureCategory);
        Assert.Equal(!failDuringStartup, runtime.ProgressPersisted.Task.IsCompletedSuccessfully);

        var persisted = Assert.IsType<ExecutionRunDetail>(
            await executionRunStore.GetExecutionRunDetailAsync(executionRunId));
        var persistedFailureLog = Assert.Single(persisted.ExecutionLog, entry => entry.Phase == "Failed");
        var reopenedStore = new FileSandboxWorkspaceStore(
            scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
            ResolveWorkspaceScope(scope.ServiceProvider));
        var reopened = Assert.IsType<ExecutionRunDetail>(
            await reopenedStore.GetExecutionRunDetailAsync(executionRunId));
        var failedLog = Assert.Single(reopened.ExecutionLog, entry => entry.Phase == "Failed");

        Assert.Equal(ExecutionState.Failed, reopened.Run.State);
        Assert.Equal(RunOutcome.Failed, reopened.Run.Outcome);
        Assert.NotNull(reopened.Run.CompletedAtUtc);
        Assert.Equal(operationId, reopened.Run.InitialActivityOperationId);
        Assert.Equal(persistedFailureLog, failedLog);
        Assert.Equal(executionRunId, failedLog.ExecutionRunId);
        Assert.Equal(ExecutionState.Failed, failedLog.State);
        Assert.Equal(!failDuringStartup, reopened.ExecutionLog.Any(entry => entry.Phase == "Implementation"));
        Assert.DoesNotContain(reopened.ExecutionLog, entry => entry.Phase == "Completed");
        var reopenedSession = Assert.IsType<ChatSessionRecord>(reopened.ChatSession);
        var savedMessage = Assert.Single(reopenedSession.Messages);
        Assert.Equal(ChatMessageRole.User, savedMessage.Role);
        Assert.Equal(prompt, savedMessage.Content);
        Assert.Equal(executionRunId, reopenedSession.LatestExecutionRunId);
        Assert.Empty(reopened.ToolReceipts);
        Assert.Empty(reopened.Artifacts);
        Assert.Empty(reopened.Run.PendingApprovals);
        Assert.NotEmpty(reopened.Metrics);
        Assert.All(reopened.Metrics, metric => {
            Assert.Equal(RunOutcome.Failed, metric.Outcome);
            Assert.Equal(0, metric.OutputTokens);
            Assert.Equal(0, metric.ToolCalls);
        });

        await using var reader = activities.OpenReader(
            workspaceIdentity.CreateStreamId(operationId),
            StreamSequence.Beginning);
        using var readTimeout = new CancellationTokenSource(AsyncObservationTimeout);
        var replay = Assert.IsType<SequencedStreamEvents<AgentExecutionActivity>>(
            await reader.ReadAsync(readTimeout.Token));
        var terminal = Assert.Single(replay.Items, item => item.Event.IsTerminal).Event;
        Assert.Equal(AgentExecutionActivityPhase.Failed, terminal.Phase);
        Assert.Equal(AgentExecutionActivityTerminalOutcome.Failed, terminal.TerminalOutcome);
        Assert.Equal(AgentExecutionActivityFailureCodes.UnhandledExecutionFailure, terminal.ErrorCode);
        Assert.Equal(agent.Id, terminal.AgentId);
        Assert.Equal(session.Id, terminal.ChatSessionId);
        Assert.Equal(executionRunId, terminal.ExecutionRunId);
        Assert.DoesNotContain(replay.Items, item => item.Event.Phase == AgentExecutionActivityPhase.Completed);

        var publicDiagnostics = reopened.ExecutionLog.Select(entry => entry.Message)
            .Concat(replay.Items.Select(item => item.Event.Message))
            .Append(exception.Message)
            .Append(exception.SanitizedDisplayMessage)
            .Append(reopened.Run.ResultSummary);
        Assert.All(publicDiagnostics, message => {
            Assert.DoesNotContain(secretSentinel, message, StringComparison.Ordinal);
            Assert.DoesNotContain(promptSentinel, message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Handoff_participant_failure_preserves_actual_provider_model_and_split_usage()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create(
            "integration-agentframework-handoff-provider-attribution");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton<FakeProgressAgentRuntime>();
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider =>
                    serviceProvider.GetRequiredService<FakeProgressAgentRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<FakeProgressAgentRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var entryProvider = (await workspaceService.ListProvidersAsync())
            .Single(item => item.Id == agent.ProviderProfileId);
        const string participantModel = "participant-priced-model";
        var participantProviderId = await workspaceService.SaveProviderAsync(
            new ProviderProfileEditorModel
            {
                Name = "Handoff participant provider",
                Kind = ProviderKind.Ollama,
                BaseUrl = "http://127.0.0.1:11434",
                DefaultModel = participantModel,
                Transport = ProviderTransportKind.ChatCompletions,
                Purpose = ProviderProfilePurpose.Chat,
                IsEnabled = true,
                SupportsStreaming = true,
                SupportsTools = true,
                PreferFrameworkManagedChatHistory = true,
                ConfigurationJson = "{\"timeoutSeconds\":45}",
                ModelPrices =
                [
                    new ProviderModelTokenPriceEditorModel
                    {
                        Model = participantModel,
                        InputPerMillionTokensUsd = 1m,
                        OutputPerMillionTokensUsd = 2m
                    }
                ]
            });
        var participantProvider = (await workspaceService.ListProvidersAsync())
            .Single(item => item.Id == participantProviderId);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
        runtime.Failure = new AgentRuntimeUsageException(
            "The handoff participant provider failed.",
            new HttpRequestException("participant transport failed"),
            [
                CreateUsageObservation(
                    entryProvider,
                    entryProvider.DefaultModel,
                    inputTokens: 17,
                    outputTokens: 3),
                CreateUsageObservation(
                    participantProvider,
                    participantModel,
                    inputTokens: 1000,
                    outputTokens: 2000)
            ],
            failureOrigin: AgentRuntimeFailureOrigin.Provider,
            providerFailureIdentity: new AgentRuntimeProviderFailureIdentity(
                participantProvider.Id,
                participantProvider.Name,
                participantProvider.Kind,
                participantProvider.Transport,
                participantModel));

        var operation = workspaceService.SendMessageAsync(
            agent.Id,
            session.Id,
            "Exercise a secondary handoff participant failure.",
            options: new AgentChatRunOptions(AgentExecutionOperationId.New()));
        var executionRunId = await WaitForExecutionRunIdAsync(runtime, operation);
        runtime.AllowCompletion.TrySetResult(true);

        var exception = await Assert.ThrowsAsync<AgentChatRunFailedException>(() => operation);
        var detail = Assert.IsType<ExecutionRunDetail>(
            await executionRunStore.GetExecutionRunDetailAsync(executionRunId));

        Assert.Equal(participantProvider.Name, exception.ProviderName);
        Assert.Equal(participantModel, exception.ModelName);
        Assert.Equal(participantProvider.Id, detail.Run.FailureProviderProfileId);
        Assert.Equal(participantProvider.Name, detail.Run.FailureProviderName);
        Assert.Equal(participantModel, detail.Run.FailureModel);
        Assert.Equal(entryProvider.Id, detail.Run.ProviderProfileId);
        Assert.Equal(entryProvider.Name, detail.Run.ProviderName);
        Assert.Contains(
            detail.Metrics,
            metric => metric.ProviderName == participantProvider.Name &&
                      metric.Model == participantModel);

        var entryUsage = Assert.Single(
            detail.UsageObservations,
            observation => observation.ProviderProfileId == entryProvider.Id);
        Assert.Equal(entryProvider.Name, entryUsage.ProviderName);
        Assert.Equal(entryProvider.DefaultModel, entryUsage.Model);
        Assert.Null(entryUsage.CalculatedCostUsd);

        var participantUsages = detail.UsageObservations
            .Where(observation => observation.ProviderProfileId == participantProvider.Id)
            .ToList();
        Assert.Equal(2, participantUsages.Count);
        var observedParticipantUsage = Assert.Single(
            participantUsages,
            observation => observation.UsageStatus == ProviderUsageObservationStatus.Observed);
        Assert.Equal(participantProvider.Name, observedParticipantUsage.ProviderName);
        Assert.Equal(participantModel, observedParticipantUsage.Model);
        Assert.Equal(0.005m, observedParticipantUsage.CalculatedCostUsd);
        Assert.Contains(
            participantUsages,
            observation => observation.UsageStatus == ProviderUsageObservationStatus.MissingAfterProviderActivity &&
                           observation.TotalTokens == 0);
    }

    private static ProviderUsageObservation CreateUsageObservation(
        ProviderProfile provider,
        string model,
        int inputTokens,
        int outputTokens)
    {
        return new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            ProviderName: provider.Name,
            ProviderKind: provider.Kind,
            Model: model,
            TransportKind: provider.Transport,
            SourcePhase: ProviderUsageSourcePhases.AgentRuntime,
            UsageStatus: ProviderUsageObservationStatus.Observed,
            InputTokens: inputTokens,
            CachedInputTokens: 0,
            OutputTokens: outputTokens,
            ReasoningTokens: 0,
            TotalTokens: inputTokens + outputTokens,
            ToolCallCount: 0)
        {
            ProviderProfileId = provider.Id
        };
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Terminal_failure_persistence_failure_preserves_typed_run_identity(
        bool chatBacked,
        bool failOnTerminalLog)
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create(
            $"integration-agentframework-terminal-persistence-{chatBacked}-{failOnTerminalLog}");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services => UseTerminalFailurePersistenceHarness(
                services,
                failOnTerminalLog));

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<FakeProgressAgentRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var processLeaseCleaner = scope.ServiceProvider.GetRequiredService<RecordingTerminalProcessLeaseCleaner>();
        var logProvider = scope.ServiceProvider.GetRequiredService<TerminalFailureLogProvider>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = chatBacked
            ? await workspaceService.GetOrCreateChatSessionAsync(agent.Id)
            : null;
        runtime.Failure = new InvalidOperationException("api_key=runtime-failure-secret");

        Task operation = chatBacked
            ? workspaceService.SendMessageAsync(
                agent.Id,
                session!.Id,
                "Exercise the terminal persistence boundary.",
                options: new AgentChatRunOptions(AgentExecutionOperationId.New()))
            : workspaceService.ExecuteRunAsync(
                new ExecutionRunRequest(
                    agent.Id,
                    "Exercise the terminal persistence boundary.",
                    AgentExecutionOperationId.New()));
        var executionRunId = await WaitForExecutionRunIdAsync(runtime, operation);
        runtime.AllowCompletion.TrySetResult(true);

        if (chatBacked)
        {
            var exception = await Assert.ThrowsAsync<AgentChatRunFailedException>(() => operation);

            Assert.Equal(agent.Id, exception.AgentId);
            Assert.Equal(executionRunId, exception.ExecutionRunId);
            Assert.Equal(session!.Id, exception.ChatSessionId);
            Assert.Null(exception.FailureCategory);
            Assert.DoesNotContain("runtime-failure-secret", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(
                TerminalFailureWorkspaceStoreProxy.PersistenceSecret,
                exception.Message,
                StringComparison.Ordinal);
        }
        else
        {
            var exception = await Assert.ThrowsAsync<AgentRunFailedException>(() => operation);

            Assert.Equal(agent.Id, exception.AgentId);
            Assert.Equal(executionRunId, exception.ExecutionRunId);
            Assert.Null(exception.ChatSessionId);
            Assert.Null(exception.FailureCategory);
            Assert.DoesNotContain("runtime-failure-secret", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(
                TerminalFailureWorkspaceStoreProxy.PersistenceSecret,
                exception.Message,
                StringComparison.Ordinal);
        }

        var capturedLogs = string.Join(Environment.NewLine, logProvider.Entries);
        Assert.DoesNotContain("runtime-failure-secret", capturedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain(
            TerminalFailureWorkspaceStoreProxy.PersistenceSecret,
            capturedLogs,
            StringComparison.Ordinal);

        var detail = await executionRunStore.GetExecutionRunDetailAsync(executionRunId);
        Assert.NotNull(detail);
        if (failOnTerminalLog)
        {
            Assert.Equal(ExecutionState.Failed, detail.Run.State);
            Assert.Contains(executionRunId, processLeaseCleaner.ExecutionRunIds);
        }
        else
        {
            Assert.NotEqual(ExecutionState.Failed, detail.Run.State);
            Assert.DoesNotContain(executionRunId, processLeaseCleaner.ExecutionRunIds);
        }
    }

    [Fact]
    public async Task ExecuteRunAsync_finalizes_run_when_caller_cancels_after_runtime_started()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-run-caller-cancel");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton<FakeProgressAgentRuntime>();
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<FakeProgressAgentRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<FakeProgressAgentRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
        using var callerCancellation = new CancellationTokenSource();

        var executionTask = workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Run a slow provider-backed validation.",
                AgentExecutionOperationId.New(),
                session.Id,
                new ExecutionInvocationContext(
                    SourceKind: "chat-session",
                    SourceId: session.Id.ToString("N"),
                    CorrelationId: "corr-caller-cancel",
                    CausationId: "cause-caller-cancel",
                    RequestedBy: "integration-test",
                    RequestedByKind: "test",
                    MetadataJson: "{}"),
                AutoApprovePendingToolCalls: true),
            callerCancellation.Token);

        var executionRunId = await WaitForExecutionRunIdAsync(runtime, executionTask);
        await runtime.ProgressPersisted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        callerCancellation.Cancel();

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => executionTask);
        var detail = await executionRunStore.GetExecutionRunDetailAsync(executionRunId);

        Assert.Contains("caller request was cancelled", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Failed, detail.Run.State);
        Assert.Equal(RunOutcome.Cancelled, detail.Run.Outcome);
        Assert.Contains("caller request was cancelled", detail.Run.ResultSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            detail.ExecutionLog,
            entry => entry.ExecutionRunId == executionRunId &&
                     entry.State == ExecutionState.Failed &&
                     entry.Phase == "Cancelled" &&
                     entry.Message.Contains("caller request was cancelled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteRunAsync_starts_chat_backed_run_without_loading_unrelated_run_slices()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-chat-split-store");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton<FakeProgressAgentRuntime>();
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<FakeProgressAgentRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<FakeProgressAgentRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var workspaceRoot = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        var workspaceScope = ResolveWorkspaceScope(scope.ServiceProvider);
        var layout = new FileSandboxWorkspaceStorageLayout(workspaceRoot, workspaceScope);
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var unrelatedRunId = Guid.NewGuid();
        await executionRunStore.SaveExecutionRunDetailAsync(CreateCompletedRunDetail(unrelatedRunId, agent.Id, "Unrelated corrupt receipt run"));
        var unrelatedReceiptsRoot = layout.RunReceiptsRoot(unrelatedRunId);
        Directory.CreateDirectory(unrelatedReceiptsRoot);
        await File.WriteAllTextAsync(
            Path.Combine(unrelatedReceiptsRoot, "corrupt-receipt.json"),
            "{ this is not valid json",
            CancellationToken.None);

        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
        runtime.AllowCompletion.TrySetResult(true);

        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Confirm that chat start avoids unrelated run slices.",
                AgentExecutionOperationId.New(),
                session.Id,
                new ExecutionInvocationContext(
                    SourceKind: "chat-session",
                    SourceId: session.Id.ToString("N"),
                    CorrelationId: Guid.NewGuid().ToString("N"),
                    CausationId: session.Id.ToString("N"),
                    RequestedBy: "integration-test",
                    RequestedByKind: "test",
                    MetadataJson: "{}")));

        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);

        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Completed, detail!.Run.State);
        Assert.NotNull(detail.ChatSession);
        Assert.Equal(result.ExecutionRunId, detail.ChatSession!.LatestExecutionRunId);
        Assert.Contains(detail.ChatSession.Messages, message => message.Role == ChatMessageRole.Assistant);
    }

    [Fact]
    public async Task SendMessageAsync_starts_chat_run_without_loading_unrelated_run_slices()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-send-message-split-store");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton<FakeProgressAgentRuntime>();
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<FakeProgressAgentRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<FakeProgressAgentRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var workspaceRoot = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        var workspaceScope = ResolveWorkspaceScope(scope.ServiceProvider);
        var layout = new FileSandboxWorkspaceStorageLayout(workspaceRoot, workspaceScope);
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var unrelatedRunId = Guid.NewGuid();
        await executionRunStore.SaveExecutionRunDetailAsync(CreateCompletedRunDetail(unrelatedRunId, agent.Id, "Unrelated corrupt send receipt run"));
        var unrelatedReceiptsRoot = layout.RunReceiptsRoot(unrelatedRunId);
        Directory.CreateDirectory(unrelatedReceiptsRoot);
        await File.WriteAllTextAsync(
            Path.Combine(unrelatedReceiptsRoot, "corrupt-receipt.json"),
            "{ this is not valid json",
            CancellationToken.None);

        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
        runtime.AllowCompletion.TrySetResult(true);

        var result = await workspaceService.SendMessageAsync(
            agent.Id,
            session.Id,
            "Confirm that SendMessageAsync avoids unrelated run slices.",
            options: new AgentChatRunOptions(AgentExecutionOperationId.New()));
        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);

        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Completed, detail!.Run.State);
        Assert.Equal(session.Id, result.ChatSessionId);
        Assert.Equal(result.ExecutionRunId, detail.ChatSession!.LatestExecutionRunId);
        Assert.Equal(ChatMessageRole.Assistant, result.AssistantMessage.Role);
        Assert.Contains(detail.ChatSession.Messages, message => message.Role == ChatMessageRole.Assistant);
    }

    [Fact]
    public async Task RespondToPendingApprovalsAsync_continues_chat_run_without_loading_unrelated_run_slices()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-approval-split-store");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton<StructuredOutputApprovalRuntime>();
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var runtime = scope.ServiceProvider.GetRequiredService<StructuredOutputApprovalRuntime>();
        var workspaceRoot = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        var workspaceScope = ResolveWorkspaceScope(scope.ServiceProvider);
        var layout = new FileSandboxWorkspaceStorageLayout(workspaceRoot, workspaceScope);
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var expectedEvidence = CreateRequestCompatibilityEvidence(agent.ProviderProfileId);
        runtime.InitialRequestCompatibilityEvidence = expectedEvidence;
        var unrelatedRunId = Guid.NewGuid();
        await executionRunStore.SaveExecutionRunDetailAsync(CreateCompletedRunDetail(unrelatedRunId, agent.Id, "Unrelated corrupt approval receipt run"));
        var unrelatedReceiptsRoot = layout.RunReceiptsRoot(unrelatedRunId);
        Directory.CreateDirectory(unrelatedReceiptsRoot);
        await File.WriteAllTextAsync(
            Path.Combine(unrelatedReceiptsRoot, "corrupt-receipt.json"),
            "{ this is not valid json",
            CancellationToken.None);

        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);
        var pendingResult = await workspaceService.SendMessageAsync(
            agent.Id,
            session.Id,
            "Request approval before completing this chat run.",
            options: new AgentChatRunOptions(AgentExecutionOperationId.New()));
        var pendingDetail = await executionRunStore.GetExecutionRunDetailAsync(pendingResult.ExecutionRunId);

        Assert.NotNull(pendingDetail);
        Assert.Equal(ExecutionState.WaitingOnTool, pendingDetail!.Run.State);
        Assert.NotEmpty(pendingDetail.Run.PendingApprovals);
        Assert.Equal(expectedEvidence, pendingDetail.Run.EntryAgentRequestCompatibilityEvidence);

        var completedResult = await workspaceService.RespondToPendingApprovalsAsync(
            agent.Id,
            session.Id,
            AgentExecutionOperationId.New(),
            decisions: pendingDetail.Run.PendingApprovals
                .Select(item => new PendingToolApprovalDecision(item.ApprovalId, Approved: true))
                .ToArray(),
            autoApprovePendingToolCalls: true);
        var completedDetail = await executionRunStore.GetExecutionRunDetailAsync(completedResult.ExecutionRunId);

        Assert.NotNull(completedDetail);
        Assert.Equal(pendingResult.ExecutionRunId, completedResult.ExecutionRunId);
        Assert.Equal(ExecutionState.Completed, completedDetail!.Run.State);
        Assert.Empty(completedDetail.Run.PendingApprovals);
        Assert.Equal(expectedEvidence, completedDetail.Run.EntryAgentRequestCompatibilityEvidence);
        Assert.Contains(completedDetail.ChatSession!.Messages, message => message.Role == ChatMessageRole.Assistant);
        Assert.False(Assert.Single(runtime.ContinuationSuppressApprovalRequirements));
        Assert.Equal(
            AgentRuntimeContextPurpose.InteractiveChat,
            Assert.Single(runtime.ContinuationExecutionOptions)?.ContextIntent?.Purpose);
    }

    [Fact]
    public async Task ContinueExecutionRunAsync_preserves_structured_output_contract_after_pending_approval()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-structured-output-continuation");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton<StructuredOutputApprovalRuntime>();
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<StructuredOutputApprovalRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);

        var initialResult = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Implement the process step and request approval for the write.",
                AgentExecutionOperationId.New(),
                session.Id,
                CreateProcessStepContext(),
                AutoApprovePendingToolCalls: false,
                StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult));

        var pendingDetail = await executionRunStore.GetExecutionRunDetailAsync(initialResult.ExecutionRunId);
        Assert.NotNull(pendingDetail);
        Assert.Equal(ExecutionState.WaitingOnTool, pendingDetail.Run.State);
        Assert.NotEmpty(pendingDetail.Run.PendingApprovals);
        Assert.Equal(AgentStructuredOutputContracts.ProcessStepOutcomeResultKey, pendingDetail.Run.StructuredOutputContractKey);
        Assert.NotEmpty(pendingDetail.Run.StructuredOutputTypeName);

        var continuationResult = await workspaceService.ContinueExecutionRunAsync(
            initialResult.ExecutionRunId,
            AgentExecutionOperationId.New(),
            decisions: pendingDetail.Run.PendingApprovals
                .Select(item => new PendingToolApprovalDecision(item.ApprovalId, Approved: true))
                .ToArray(),
            autoApprovePendingToolCalls: false);

        var completedDetail = await executionRunStore.GetExecutionRunDetailAsync(continuationResult.ExecutionRunId);
        Assert.NotNull(completedDetail);
        Assert.Equal(ExecutionState.Completed, completedDetail.Run.State);
        Assert.Equal(RunOutcome.Succeeded, completedDetail.Run.Outcome);
        Assert.Single(runtime.RunStructuredOutputs);
        Assert.Single(runtime.ContinuationStructuredOutputs);
        Assert.Equal(
            AgentStructuredOutputContracts.ProcessStepOutcomeResultKey,
            runtime.RunStructuredOutputs[0]?.ContractKey);
        Assert.Equal(
            AgentStructuredOutputContracts.ProcessStepOutcomeResultKey,
            runtime.ContinuationStructuredOutputs[0]?.ContractKey);
        Assert.Contains(
            completedDetail.ExecutionLog,
            entry => entry.Phase == "Output validation" &&
                     entry.Message.Contains("Validated structured output contract", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ContinueExecutionRunAsync_preserves_governance_failure_reason_after_pending_approval()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create(
            "integration-agentframework-structured-output-continuation-failure");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    ContinuationResponseText = "This continuation is prose, not machine JSON."
                });
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider =>
                    serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);

        var initialResult = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Request approval before returning invalid structured output.",
                AgentExecutionOperationId.New(),
                session.Id,
                CreateProcessStepContext(),
                AutoApprovePendingToolCalls: false,
                StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult));

        var pendingDetail = await executionRunStore.GetExecutionRunDetailAsync(
            initialResult.ExecutionRunId);
        Assert.NotNull(pendingDetail);
        var exception = await Assert.ThrowsAsync<AgentChatRunFailedException>(() =>
            workspaceService.ContinueExecutionRunAsync(
                initialResult.ExecutionRunId,
                AgentExecutionOperationId.New(),
                decisions: pendingDetail.Run.PendingApprovals
                    .Select(item => new PendingToolApprovalDecision(item.ApprovalId, Approved: true))
                    .ToArray(),
                autoApprovePendingToolCalls: false));
        var failedDetail = await executionRunStore.GetExecutionRunDetailAsync(
            initialResult.ExecutionRunId);

        Assert.Contains("failed validation", exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.FailureCategory);
        Assert.NotNull(failedDetail);
        Assert.Equal(ExecutionState.Failed, failedDetail.Run.State);
        Assert.Equal(RunOutcome.Failed, failedDetail.Run.Outcome);
        Assert.Contains("failed validation", failedDetail.Run.ResultSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteRunAsync_persists_provider_usage_without_prompt_double_counting()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-provider-usage-metrics");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialCachedInputTokens = 3,
                    ContinuationCachedInputTokens = 2,
                    InitialToolInvocationTraces =
                    [
                        CreateToolInvocationTrace(
                            "workspace_list_files",
                            ToolInvocationClassification.Read,
                            sequence: 1)
                    ],
                    ContinuationToolInvocationTraces =
                    [
                        CreateToolInvocationTrace(
                            "workspace_read_file",
                            ToolInvocationClassification.Read,
                            sequence: 1)
                    ]
                });
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);

        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Complete the process step after the approval is automatically granted.",
                AgentExecutionOperationId.New(),
                session.Id,
                CreateProcessStepContext(),
                AutoApprovePendingToolCalls: true,
                StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult));
        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);

        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Completed, detail.Run.State);

        var metric = Assert.Single(detail.Metrics);
        Assert.Equal(12, metric.InputTokens);
        Assert.Equal(5, metric.CachedInputTokens);
        Assert.Equal(24, metric.OutputTokens);
        Assert.Equal(1, metric.ToolCalls);
        Assert.Equal(result.ExecutionRunId, metric.ExecutionRunId);

        var usage = Assert.Single(detail.UsageObservations);
        Assert.Equal(ProviderUsageObservationStatus.ObservedFromMetric, usage.UsageStatus);
        Assert.Equal(ProviderUsageSourcePhases.AgentRuntime, usage.SourcePhase);
        Assert.Equal(12, usage.InputTokens);
        Assert.Equal(5, usage.CachedInputTokens);
        Assert.Equal(24, usage.OutputTokens);
        Assert.Equal(36, usage.TotalTokens);
        Assert.Equal("run-001", usage.ProcessRunId);
        Assert.Equal("step-001", usage.ProcessStepId);
        Assert.Equal(result.ExecutionRunId, usage.ExecutionRunId);

        var traceReceipts = detail.ToolReceipts
            .Where(receipt => receipt.ToolFamily == "agent-tool-trace")
            .ToArray();
        Assert.Equal(2, traceReceipts.Length);
        Assert.Contains(traceReceipts, receipt => receipt.ToolName == "workspace_list_files");
        Assert.Contains(traceReceipts, receipt => receipt.ToolName == "workspace_read_file");
    }

    [Fact]
    public async Task ExecuteRunAsync_persists_successful_auto_approval_continuation_traces_when_later_continuation_fails()
    {
        var expectedEvidence = CreateRequestCompatibilityEvidence(providerProfileId: null);
        var continuationResponse = new AgentRuntimeResponse(
            ResponseText: "Another approval is pending.",
            InputTokens: 3,
            OutputTokens: 4,
            ToolCalls: 1,
            RuntimeSessionKey: "runtime-session-key-2",
            SerializedSessionStateJson: "{}",
            PendingApprovals:
            [
                new PendingToolApprovalRecord(
                    "approval-002",
                    "call-002",
                    "workspace_read_file",
                    "function",
                    "Read the project file.",
                    "{\"path\":\"src/App.csproj\"}")
            ])
        {
            ToolInvocationTraces =
            [
                CreateToolInvocationTrace(
                    "workspace_list_files",
                    ToolInvocationClassification.Read,
                    sequence: 1)
            ]
        };
        await using var testEnvironment = CanDoItAllTestEnvironment.Create(
            "integration-agentframework-auto-approval-continuation-failure-receipts");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialRequestCompatibilityEvidence = expectedEvidence,
                    InitialToolInvocationTraces =
                    [
                        CreateToolInvocationTrace(
                            "workspace_stat_path",
                            ToolInvocationClassification.Read,
                            sequence: 1)
                    ],
                    ContinuationResponses = [continuationResponse],
                    ContinuationException = new InvalidOperationException(
                        "Fake provider failed during the later continuation.")
                });
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider =>
                    serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<StructuredOutputApprovalRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        await Assert.ThrowsAsync<AgentRunFailedException>(() =>
            workspaceService.ExecuteRunAsync(
                new ExecutionRunRequest(
                    agent.Id,
                    "Continue approvals until the provider fails.",
                    AgentExecutionOperationId.New(),
                    ChatSessionId: null,
                    Context: CreateProcessStepContext(),
                    AutoApprovePendingToolCalls: true)));

        var executionRunId = Assert.Single(runtime.ObservedExecutionRunIds);
        var detail = await executionRunStore.GetExecutionRunDetailAsync(executionRunId);

        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Failed, detail.Run.State);
        Assert.Equal(expectedEvidence, detail.Run.EntryAgentRequestCompatibilityEvidence);
        Assert.Equal(2, runtime.ContinuationSuppressApprovalRequirements.Count);
        Assert.All(runtime.ContinuationSuppressApprovalRequirements, Assert.False);
        Assert.Collection(
            runtime.ContinuationSessionPendingApprovals,
            pending => Assert.Equal("approval-001", Assert.Single(pending).ApprovalId),
            pending => Assert.Equal("approval-002", Assert.Single(pending).ApprovalId));
        var traceReceipts = detail.ToolReceipts
            .Where(receipt => receipt.ToolFamily == "agent-tool-trace")
            .ToArray();
        Assert.Equal(2, traceReceipts.Length);
        Assert.Contains(traceReceipts, receipt => receipt.ToolName == "workspace_stat_path");
        Assert.Contains(traceReceipts, receipt => receipt.ToolName == "workspace_list_files");
    }

    [Fact]
    public async Task ExecuteRunAsync_persists_context_manifest_in_usage_diagnostics()
    {
        var contextManifest = CreateContextManifest();
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-context-manifest");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialPendingApprovals = [],
                    InitialResponseText = "Completed successfully.",
                    InitialContextAssemblyManifest = contextManifest
                });
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);

        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Run with context manifest.",
                AgentExecutionOperationId.New(),
                session.Id,
                CreateProcessStepContext(),
                AutoApprovePendingToolCalls: true));

        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);
        Assert.NotNull(detail);
        var usage = Assert.Single(detail.UsageObservations);
        using var diagnostics = JsonDocument.Parse(usage.DiagnosticsJson);
        var manifest = diagnostics.RootElement.GetProperty("contextAssemblyManifest");
        Assert.Equal(contextManifest.Id, manifest.GetProperty("id").GetGuid());
        Assert.Equal(7, manifest.GetProperty("totals").GetProperty("estimatedInputTokens").GetInt32());
        Assert.Equal("workspace-tools", manifest.GetProperty("sources")[0].GetProperty("category").GetString());
    }

    [Fact]
    public async Task ExecuteRunAsync_passes_process_context_intent_for_two_agent_steps()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-two-step-context-intent");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialPendingApprovals = [],
                    InitialResponseText = "Completed successfully."
                });
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<StructuredOutputApprovalRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);

        await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Read the process context.",
                AgentExecutionOperationId.New(),
                session.Id,
                CreateProcessStepContext(
                    CreateProcessStepMetadata(
                        ProcessOperationContractNames.ExternalProductTargetReadOnly,
                        allowsProductMutation: false,
                        ProcessOperationContractNames.ReadProcessContext),
                    sourceId: "step-read",
                    processRunId: "run-two-step",
                    processStepId: "step-read"),
                AutoApprovePendingToolCalls: true));
        await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Run validation.",
                AgentExecutionOperationId.New(),
                session.Id,
                CreateProcessStepContext(
                    CreateProcessStepMetadata(
                        ProcessOperationContractNames.ExternalProductTargetReadOnly,
                        allowsProductMutation: false,
                        ProcessOperationContractNames.RunValidation),
                    sourceId: "step-validate",
                    processRunId: "run-two-step",
                    processStepId: "step-validate"),
                AutoApprovePendingToolCalls: true));

        Assert.Equal(2, runtime.RunExecutionOptions.Count);
        var readIntent = runtime.RunExecutionOptions[0]?.ContextIntent;
        var validationIntent = runtime.RunExecutionOptions[1]?.ContextIntent;
        Assert.NotNull(readIntent);
        Assert.NotNull(validationIntent);
        Assert.True(readIntent!.IsGovernedProcessStep);
        Assert.True(validationIntent!.IsGovernedProcessStep);
        Assert.Equal("step-read", readIntent.SourceId);
        Assert.Equal("step-validate", validationIntent.SourceId);
        Assert.Contains(ProcessOperationContractNames.ReadProcessContext, readIntent.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.RunValidation, readIntent.AllowedOperations);
        Assert.Contains(ProcessOperationContractNames.RunValidation, validationIntent.AllowedOperations);
        Assert.False(readIntent.AllowsProductMutation);
        Assert.False(validationIntent.AllowsProductMutation);
    }

    [Fact]
    public async Task ExecuteRunAsync_preserves_usage_when_runtime_fails_after_provider_call()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-runtime-failure-usage");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    ThrowUsageExceptionOnRun = true,
                    InitialPendingApprovals = [],
                    InitialUsageObservations =
                    [
                        CreateUsageObservation(ProviderUsageSourcePhases.AgentRuntime, 31, 4, 9)
                    ],
                    InitialToolInvocationTraces =
                    [
                        CreateToolInvocationTrace(
                            "workspace_read_file",
                            ToolInvocationClassification.Read,
                            sequence: 1)
                    ]
                });
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<StructuredOutputApprovalRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        await Assert.ThrowsAsync<AgentRunFailedException>(() =>
            workspaceService.ExecuteRunAsync(
                new ExecutionRunRequest(
                    agent.Id,
                    "Fail after provider usage is available.",
                    InitialActivityOperationId: AgentExecutionOperationId.New(),
                    ChatSessionId: null,
                    Context: CreateProcessStepContext())));

        var executionRunId = Assert.Single(runtime.ObservedExecutionRunIds);
        var failedDetail = await executionRunStore.GetExecutionRunDetailAsync(executionRunId);

        Assert.NotNull(failedDetail);
        Assert.Equal(ExecutionState.Failed, failedDetail.Run.State);
        var usage = Assert.Single(failedDetail.UsageObservations);
        Assert.Equal(ProviderUsageObservationStatus.Observed, usage.UsageStatus);
        Assert.Equal(31, usage.InputTokens);
        Assert.Equal(4, usage.CachedInputTokens);
        Assert.Equal(9, usage.OutputTokens);
        Assert.Equal(executionRunId, usage.ExecutionRunId);
        Assert.Equal("run-001", usage.ProcessRunId);
        var receipt = Assert.Single(
            failedDetail.ToolReceipts,
            item => item.ToolName == "workspace_read_file");
        Assert.Equal(executionRunId, receipt.ExecutionRunId);
        Assert.Equal("agent-tool-trace", receipt.ToolFamily);
    }

    [Fact]
    public async Task ExecuteRunAsync_links_structured_output_repair_usage_to_execution_run()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-repair-usage");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialResponseText = "not machine json",
                    InitialPendingApprovals = []
                });
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                services.RemoveAll<IAgentOutputRepairService>();
                services.AddSingleton<IAgentOutputRepairService>(new UsageReportingRepairService());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Repair invalid structured output.",
                InitialActivityOperationId: AgentExecutionOperationId.New(),
                ChatSessionId: null,
                Context: CreateProcessStepContext(),
                StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult));
        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);

        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Completed, detail.Run.State);
        Assert.Contains(detail.UsageObservations, item => item.SourcePhase == ProviderUsageSourcePhases.AgentRuntime);
        var repairUsage = Assert.Single(detail.UsageObservations, item => item.SourcePhase == ProviderUsageSourcePhases.StructuredOutputRepair);
        Assert.Equal(ProviderUsageObservationStatus.Observed, repairUsage.UsageStatus);
        Assert.Equal(3, repairUsage.InputTokens);
        Assert.Equal(1, repairUsage.CachedInputTokens);
        Assert.Equal(2, repairUsage.OutputTokens);
        Assert.Equal(result.ExecutionRunId, repairUsage.ExecutionRunId);
        Assert.Equal("run-001", repairUsage.ProcessRunId);
    }

    [Fact]
    public async Task ExecuteRunAsync_fails_governed_run_when_structured_output_is_invalid()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-invalid-structured-output");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialResponseText = "This is prose, not machine JSON.",
                    InitialPendingApprovals = []
                });
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<StructuredOutputApprovalRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var exception = await Assert.ThrowsAsync<AgentRunFailedException>(() =>
            workspaceService.ExecuteRunAsync(
                new ExecutionRunRequest(
                    agent.Id,
                    "Return invalid machine output for the process step.",
                    InitialActivityOperationId: AgentExecutionOperationId.New(),
                    ChatSessionId: null,
                    Context: CreateProcessStepContext(),
                    AutoApprovePendingToolCalls: false,
                    StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult)));

        var executionRunId = Assert.Single(runtime.ObservedExecutionRunIds);
        var failedDetail = await executionRunStore.GetExecutionRunDetailAsync(executionRunId);

        Assert.Contains("failed validation", exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.FailureCategory);
        Assert.NotNull(failedDetail);
        Assert.Equal(ExecutionState.Failed, failedDetail.Run.State);
        Assert.Equal(RunOutcome.Failed, failedDetail.Run.Outcome);
        Assert.Contains("failed validation", failedDetail.Run.ResultSummary, StringComparison.Ordinal);
        var usage = Assert.Single(failedDetail.UsageObservations);
        Assert.Equal(ProviderUsageObservationStatus.ObservedFromMetric, usage.UsageStatus);
        Assert.Equal(7, usage.InputTokens);
        Assert.Equal(11, usage.OutputTokens);
        Assert.Equal(executionRunId, usage.ExecutionRunId);
        Assert.Contains(
            failedDetail.ExecutionLog,
            entry => entry.Phase == "Output validation" &&
                     entry.Message.Contains("failed validation", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteRunAsync_records_shadow_finalizer_status_when_present()
    {
        var outcome = CreateCompletedOutcome("The finalizer and structured output agree.");
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-shadow-finalizer");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialResponseText = SerializeOutcome(outcome),
                    InitialPendingApprovals = [],
                    InitialFinalizerInvocations = [CreateFinalizerInvocation(outcome)]
                });
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Complete the process step and submit the shadow finalizer.",
                InitialActivityOperationId: AgentExecutionOperationId.New(),
                ChatSessionId: null,
                Context: CreateProcessStepContext(),
                AutoApprovePendingToolCalls: false,
                StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult));
        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);

        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Completed, detail.Run.State);
        Assert.Contains(
            detail.ExecutionLog,
            entry => entry.Phase == "Finalizer validation" &&
                     entry.Message.Contains("Shadow finalizer tool", StringComparison.Ordinal) &&
                     entry.Message.Contains("matched structured output", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteRunAsync_records_finalizer_short_circuit_usage_when_metrics_are_zero()
    {
        var outcome = CreateCompletedOutcome("The finalizer short-circuit has no metric tokens but still records provider activity.");
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-finalizer-short-circuit-usage");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialResponseText = SerializeOutcome(outcome),
                    InitialPendingApprovals = [],
                    InitialInputTokens = 0,
                    InitialOutputTokens = 0,
                    InitialFinalizerInvocations = [CreateFinalizerInvocation(outcome)],
                    InitialUsageObservations =
                    [
                        CreateUsageObservation(
                            ProviderUsageSourcePhases.FinalizerShortCircuit,
                            0,
                            0,
                            0,
                            ProviderUsageObservationStatus.MissingAfterProviderActivity)
                    ]
                });
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Complete through a finalizer short-circuit with unavailable usage.",
                InitialActivityOperationId: AgentExecutionOperationId.New(),
                ChatSessionId: null,
                Context: CreateProcessStepContext(),
                AutoApprovePendingToolCalls: false,
                StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult));
        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);

        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Completed, detail.Run.State);
        var metric = Assert.Single(detail.Metrics);
        Assert.Equal(0, metric.InputTokens);
        Assert.Equal(0, metric.OutputTokens);
        var usage = Assert.Single(detail.UsageObservations);
        Assert.Equal(ProviderUsageObservationStatus.MissingAfterProviderActivity, usage.UsageStatus);
        Assert.Equal(ProviderUsageSourcePhases.FinalizerShortCircuit, usage.SourcePhase);
        Assert.Equal(result.ExecutionRunId, usage.ExecutionRunId);
        Assert.Equal("run-001", usage.ProcessRunId);
        Assert.Equal("step-001", usage.ProcessStepId);
    }

    [Fact]
    public async Task ExecuteRunAsync_required_finalizer_overrides_assistant_text()
    {
        const string longReason = "The required finalizer result is authoritative and intentionally long so the persisted execution result remains a parseable process-step outcome with all blocker, evidence, and next-action details preserved.";
        var outcome = CreateCompletedOutcome(longReason);
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-required-finalizer");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialResponseText = "Display-only assistant text.",
                    InitialPendingApprovals = [],
                    InitialFinalizerInvocations = [CreateFinalizerInvocation(outcome)]
                });
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Complete the process step through the required finalizer.",
                InitialActivityOperationId: AgentExecutionOperationId.New(),
                ChatSessionId: null,
                Context: CreateProcessStepContext(CreateRequiredFinalizerMetadata()),
                AutoApprovePendingToolCalls: false,
                StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult));
        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);

        Assert.StartsWith("{", result.ResponseText);
        Assert.Contains("authoritative", result.ResponseText, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Completed, detail.Run.State);
        Assert.Equal(result.ResponseText, detail.Run.ResultSummary);
        Assert.Contains("parseable process-step outcome", detail.Run.ResultSummary, StringComparison.Ordinal);
        Assert.Contains(
            detail.ExecutionLog,
            entry => entry.Phase == "Finalizer validation" &&
                     entry.Message.Contains("Required finalizer tool", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteRunAsync_required_finalizer_fails_when_validation_tool_runs_after_finalizer()
    {
        var outcome = CreateCompletedOutcome("The required finalizer cannot precede later validation.");
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-required-finalizer-sequence");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialResponseText = "Display-only assistant text.",
                    InitialPendingApprovals = [],
                    InitialFinalizerInvocations = [CreateFinalizerInvocation(outcome)],
                    InitialToolInvocationTraces =
                    [
                        CreateToolInvocationTrace(
                            AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName,
                            ToolInvocationClassification.Read,
                            sequence: 1),
                        CreateToolInvocationTrace(
                            "workspace_dotnet_test",
                            ToolInvocationClassification.Validation,
                            sequence: 2)
                    ]
                });
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var runtime = scope.ServiceProvider.GetRequiredService<StructuredOutputApprovalRuntime>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var exception = await Assert.ThrowsAsync<AgentRunFailedException>(() =>
            workspaceService.ExecuteRunAsync(
                new ExecutionRunRequest(
                    agent.Id,
                    "Complete the process step through the required finalizer.",
                    InitialActivityOperationId: AgentExecutionOperationId.New(),
                    ChatSessionId: null,
                    Context: CreateProcessStepContext(CreateRequiredFinalizerMetadata()),
                    AutoApprovePendingToolCalls: false,
                    StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult)));
        var detail = await executionRunStore.GetExecutionRunDetailAsync(Assert.Single(runtime.ObservedExecutionRunIds));

        Assert.Contains("last significant tool invocation", exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.FailureCategory);
        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Failed, detail.Run.State);
        Assert.Contains("last significant tool invocation", detail.Run.ResultSummary, StringComparison.Ordinal);
        var traceReceipts = detail.ToolReceipts
            .Where(receipt => receipt.ToolFamily == "agent-tool-trace")
            .ToArray();
        Assert.Equal(2, traceReceipts.Length);
        Assert.Contains(
            traceReceipts,
            receipt => receipt.ToolName == AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName);
        Assert.Contains(traceReceipts, receipt => receipt.ToolName == "workspace_dotnet_test");
        Assert.Contains(
            detail.ExecutionLog,
            entry => entry.Phase == "Finalizer sequencing" &&
                     entry.Message.Contains("workspace_dotnet_test#2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteRunAsync_persists_required_finalizer_output_as_assistant_transcript()
    {
        var outcome = CreateCompletedOutcome("The transcript must persist the authoritative finalizer output.");
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-required-finalizer-transcript");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialResponseText = "Display-only assistant text.",
                    InitialPendingApprovals = [],
                    InitialFinalizerInvocations = [CreateFinalizerInvocation(outcome)]
                });
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);
        var session = await workspaceService.GetOrCreateChatSessionAsync(agent.Id);

        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Complete the process step through the required finalizer.",
                InitialActivityOperationId: AgentExecutionOperationId.New(),
                ChatSessionId: session.Id,
                Context: CreateProcessStepContext(CreateRequiredFinalizerMetadata()),
                AutoApprovePendingToolCalls: false,
                StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult));
        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);

        Assert.NotNull(detail?.ChatSession);
        var assistantMessage = Assert.Single(detail.ChatSession!.Messages, message => message.Role == ChatMessageRole.Assistant);
        Assert.Equal(result.ResponseText, assistantMessage.Content);
        Assert.Contains("authoritative", assistantMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Display-only assistant text", assistantMessage.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteRunAsync_repairs_wrapped_structured_output_before_completion()
    {
        var outcome = CreateCompletedOutcome("The wrapped structured output was repaired before persistence.");
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-output-repair");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialResponseText = $"The result follows:{Environment.NewLine}{SerializeOutcome(outcome)}",
                    InitialPendingApprovals = []
                });
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var result = await workspaceService.ExecuteRunAsync(
            new ExecutionRunRequest(
                agent.Id,
                "Complete the process step with repairable machine output.",
                InitialActivityOperationId: AgentExecutionOperationId.New(),
                ChatSessionId: null,
                Context: CreateProcessStepContext(),
                AutoApprovePendingToolCalls: false,
                StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult));
        var detail = await executionRunStore.GetExecutionRunDetailAsync(result.ExecutionRunId);

        Assert.StartsWith("{", result.ResponseText, StringComparison.Ordinal);
        Assert.Contains("repaired before persistence", result.ResponseText, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(detail);
        Assert.Equal(ExecutionState.Completed, detail.Run.State);
        Assert.Contains(
            detail.ExecutionLog,
            entry => entry.Phase == "Output repair" &&
                     entry.Message.Contains("succeeded", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteRunAsync_required_finalizer_missing_prevents_completion()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-agentframework-required-finalizer-missing");
        var profile = testEnvironment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            profile,
            "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full,
            configureServices: services =>
            {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton(new StructuredOutputApprovalRuntime
                {
                    InitialResponseText = SerializeOutcome(CreateCompletedOutcome("Structured output alone is not enough in required mode.")),
                    InitialPendingApprovals = []
                });
                services.AddSingleton<IFakeAgentRuntime>(serviceProvider => serviceProvider.GetRequiredService<StructuredOutputApprovalRuntime>());
                UseDirectWorkspaceService(services);
            });

        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<StructuredOutputApprovalRuntime>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var executionRunStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue);

        var exception = await Assert.ThrowsAsync<AgentRunFailedException>(() =>
            workspaceService.ExecuteRunAsync(
                new ExecutionRunRequest(
                    agent.Id,
                    "Return structured output without the required finalizer.",
                    InitialActivityOperationId: AgentExecutionOperationId.New(),
                    ChatSessionId: null,
                    Context: CreateProcessStepContext(CreateRequiredFinalizerMetadata()),
                    AutoApprovePendingToolCalls: false,
                    StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult)));
        var executionRunId = Assert.Single(runtime.ObservedExecutionRunIds);
        var failedDetail = await executionRunStore.GetExecutionRunDetailAsync(executionRunId);

        Assert.Contains("failed validation", exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.FailureCategory);
        Assert.NotNull(failedDetail);
        Assert.Equal(ExecutionState.Failed, failedDetail.Run.State);
        Assert.Contains("failed validation", failedDetail.Run.ResultSummary, StringComparison.Ordinal);
        Assert.Contains(
            failedDetail.ExecutionLog,
            entry => entry.Phase == "Finalizer validation" &&
                     entry.Message.Contains("failed validation", StringComparison.Ordinal));
    }

    private static ExecutionInvocationContext CreateProcessStepContext(
        string metadataJson = "{}",
        string sourceId = "step-001",
        string processRunId = "run-001",
        string processStepId = "step-001")
    {
        return new ExecutionInvocationContext(
            SourceKind: "process-step",
            SourceId: sourceId,
            CorrelationId: "corr-001",
            CausationId: processStepId,
            RequestedBy: "process-automation-dispatch",
            RequestedByKind: "system",
            MetadataJson: metadataJson,
            ProcessRunId: processRunId,
            ProcessStepId: processStepId);
    }

    private static string CreateProcessStepMetadata(
        string targetScope,
        bool allowsProductMutation,
        params string[] allowedOperations)
    {
        var metadata = new Dictionary<string, object?>
        {
            [ExecutionInvocationMetadata.ProcessStepTargetScopeMetadataKey] = targetScope,
            [ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey] = allowsProductMutation,
            [ExecutionInvocationMetadata.ProcessStepAllowedOperationsMetadataKey] = allowedOperations
        };
        return JsonSerializer.Serialize(metadata, AgentOutputJson.SerializerOptions);
    }

    private static string CreateRequiredFinalizerMetadata()
    {
        return $$"""{"{{AgentFinalizerPolicies.FinalizerModeMetadataKey}}":"{{AgentFinalizerPolicies.RequiredFinalizerModeValue}}"}""";
    }

    private static ProcessStepOutcomeResult CreateCompletedOutcome(string reason)
    {
        return new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = reason,
            EvidenceRefs = ["execution://run-001"],
            NextActions = [],
            HumanReadableSummaryMarkdown = "Completed."
        };
    }

    private static ProviderUsageObservation CreateUsageObservation(
        string sourcePhase,
        int inputTokens,
        int cachedInputTokens,
        int outputTokens,
        ProviderUsageObservationStatus status = ProviderUsageObservationStatus.Observed)
    {
        return new ProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            ProviderName: "OpenAI default",
            ProviderKind: ProviderKind.OpenAi,
            Model: "gpt-5.4-mini",
            TransportKind: ProviderTransportKind.Responses,
            SourcePhase: sourcePhase,
            UsageStatus: status,
            InputTokens: inputTokens,
            CachedInputTokens: cachedInputTokens,
            OutputTokens: outputTokens,
            ReasoningTokens: 0,
            TotalTokens: inputTokens + outputTokens,
            ToolCallCount: 0);
    }

    private static ProviderRequestCompatibilityEvidence CreateRequestCompatibilityEvidence(
        Guid? providerProfileId)
    {
        return new ProviderRequestCompatibilityEvidence(
            ProviderRequestCompatibilityEvidence.CurrentSchemaVersion,
            ProviderKind.OpenAi,
            providerProfileId,
            ProviderTransportKind.ChatCompletions,
            OpenAiModelIds.Gpt56Luna,
            OpenAiModelIds.Gpt56Luna,
            ProviderInvocationFeatures.FunctionTools,
            AgentReasoningEffortLevel.Medium,
            AgentReasoningEffortLevel.None,
            ProviderRequestCompatibilityDisposition.Adjusted,
            ProviderModelParameterAdjustment.ReasoningDisabledForFunctionTools);
    }

    private static AgentRuntimeContextAssemblyManifest CreateContextManifest()
    {
        return new AgentRuntimeContextAssemblyManifest(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "test-agent",
            "OpenAI default",
            ProviderKind.OpenAi,
            "gpt-5.4-mini",
            ProviderTransportKind.Responses,
            AgentRuntimeContextIntent.Empty with
            {
                SourceKind = "process-step",
                SourceId = "step-001",
                ProcessRunId = "run-001",
                ProcessStepId = "step-001",
                IsGovernedProcessStep = true,
                AllowedOperations = [ProcessOperationContractNames.RunValidation]
            },
            new AgentRuntimeContextManifestTotals(
                InputMessageCount: 1,
                InputMessageChars: 12,
                InputMessageEstimatedTokens: 3,
                ToolCount: 2,
                ToolSchemaEstimatedChars: 16,
                ToolSchemaEstimatedTokens: 4,
                ContextProviderCount: 0,
                FrameworkToolCount: 0,
                RuntimeToolProviderCount: 0,
                EstimatedInputTokens: 7),
            [
                AgentRuntimeContextManifestSource.Included(
                    AgentRuntimeContextSourceCategories.WorkspaceTools,
                    "configured-workspace-tools",
                    "validation operation requires test tools",
                    itemCount: 2,
                    estimatedChars: 16)
            ]);
    }

    private static AgentFinalizerInvocation CreateFinalizerInvocation(ProcessStepOutcomeResult outcome)
    {
        return new AgentFinalizerInvocation(
            AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName,
            SerializeOutcome(outcome),
            Sequence: 1);
    }

    private static AgentToolInvocationTrace CreateToolInvocationTrace(
        string toolName,
        ToolInvocationClassification classification,
        int sequence)
    {
        var timestamp = DateTimeOffset.UtcNow;
        return new AgentToolInvocationTrace(
            toolName,
            classification,
            sequence,
            StartedAtUtc: timestamp,
            CompletedAtUtc: timestamp,
            Succeeded: true,
            FailureMessage: string.Empty);
    }

    private static string SerializeOutcome(ProcessStepOutcomeResult outcome)
    {
        return JsonSerializer.Serialize(outcome, AgentOutputJson.SerializerOptions);
    }

    private static void UseStartupBaselineHarness(IServiceCollection services)
    {
        UseDirectWorkspaceService(services);

        var storeDescriptor = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(ISandboxWorkspaceStore))
            ?? throw new InvalidOperationException("The real sandbox workspace store registration was not found.");
        var providerRegistryDescriptor = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(IProviderProfileRegistry))
            ?? throw new InvalidOperationException("The real provider profile registry registration was not found.");
        var providerRuntimeSourceDescriptor = services.LastOrDefault(
                descriptor => descriptor.ServiceType == typeof(IProviderRuntimeProfileSource))
            ?? throw new InvalidOperationException(
                "The real runtime provider source registration was not found.");
        var providerSnapshotSourceDescriptor = services.LastOrDefault(
                descriptor => descriptor.ServiceType ==
                    typeof(IProviderRuntimeProfileSnapshotSource))
            ?? throw new InvalidOperationException(
                "The real runtime provider snapshot source registration was not found.");

        services.AddSingleton<StartupMilestoneRecorder>();
        services.RemoveAll<IAgentExecutionActivityCoordinator>();
        services.AddSingleton<IAgentExecutionActivityCoordinator>(
            serviceProvider => new RecordingAgentExecutionActivityCoordinator(
                serviceProvider.GetRequiredService<
                    AgentExecutionActivityCoordinator>(),
                serviceProvider.GetRequiredService<
                    StartupMilestoneRecorder>()));

        services.RemoveAll<ISandboxWorkspaceStore>();
        services.AddScoped<IStartupBaselineWorkspaceStore>(serviceProvider =>
        {
            var realStore = CreateRegisteredService<ISandboxWorkspaceStore>(serviceProvider, storeDescriptor);
            if (realStore is not FileSandboxWorkspaceStore)
            {
                throw new InvalidOperationException(
                    $"The startup baseline requires {nameof(FileSandboxWorkspaceStore)}, but resolved {realStore.GetType().Name}.");
            }

            return RecordingWorkspaceStoreProxy.Create(
                realStore,
                serviceProvider.GetRequiredService<StartupMilestoneRecorder>());
        });
        services.AddScoped<ISandboxWorkspaceStore>(
            serviceProvider => serviceProvider.GetRequiredService<IStartupBaselineWorkspaceStore>());

        services.RemoveAll<IProviderProfileRegistry>();
        services.RemoveAll<IProviderRuntimeProfileSource>();
        services.RemoveAll<IProviderRuntimeProfileSnapshotSource>();
        services.AddScoped<RecordingProviderProfileRegistry>(serviceProvider =>
            new RecordingProviderProfileRegistry(
                CreateRegisteredService<IProviderProfileRegistry>(serviceProvider, providerRegistryDescriptor),
                CreateRegisteredService<IProviderRuntimeProfileSource>(
                    serviceProvider,
                    providerRuntimeSourceDescriptor),
                CreateRegisteredService<IProviderRuntimeProfileSnapshotSource>(
                    serviceProvider,
                    providerSnapshotSourceDescriptor),
                serviceProvider.GetRequiredService<StartupMilestoneRecorder>()));
        services.AddScoped<IProviderProfileRegistry>(serviceProvider =>
            serviceProvider.GetRequiredService<RecordingProviderProfileRegistry>());
        services.AddScoped<IProviderRuntimeProfileSource>(serviceProvider =>
            serviceProvider.GetRequiredService<RecordingProviderProfileRegistry>());
        services.AddScoped<IProviderRuntimeProfileSnapshotSource>(
            serviceProvider =>
                serviceProvider.GetRequiredService<
                    RecordingProviderProfileRegistry>());

        services.RemoveAll<IAgentExecutionEventSink>();
        services.AddScoped<RecordingAgentExecutionEventSink>();
        services.AddScoped<IAgentExecutionEventSink>(
            serviceProvider => serviceProvider.GetRequiredService<RecordingAgentExecutionEventSink>());

        services.RemoveAll<IFakeAgentRuntime>();
        services.RouteRuntimePortsThroughAgentRuntime();
        services.AddSingleton<StartupBarrierAgentRuntime>();
        services.AddSingleton<IFakeAgentRuntime>(
            serviceProvider => serviceProvider.GetRequiredService<StartupBarrierAgentRuntime>());
    }

    private static void UseTerminalFailurePersistenceHarness(
        IServiceCollection services,
        bool failOnTerminalLog)
    {
        UseDirectWorkspaceService(services);

        var storeDescriptor = services.LastOrDefault(descriptor =>
                descriptor.ServiceType == typeof(ISandboxWorkspaceStore))
            ?? throw new InvalidOperationException(
                "The real sandbox workspace store registration was not found.");
        services.RemoveAll<ISandboxWorkspaceStore>();
        services.AddScoped<IStartupBaselineWorkspaceStore>(serviceProvider =>
        {
            var realStore = CreateRegisteredService<ISandboxWorkspaceStore>(
                serviceProvider,
                storeDescriptor);
            return TerminalFailureWorkspaceStoreProxy.Create(
                realStore,
                failOnTerminalLog);
        });
        services.AddScoped<ISandboxWorkspaceStore>(serviceProvider =>
            serviceProvider.GetRequiredService<IStartupBaselineWorkspaceStore>());

        services.AddSingleton<TerminalFailureLogProvider>();
        services.AddSingleton<ILoggerProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<TerminalFailureLogProvider>());

        services.RemoveAll<IWorkspaceExecutionRunProcessLeaseCleaner>();
        services.AddSingleton<RecordingTerminalProcessLeaseCleaner>();
        services.AddSingleton<IWorkspaceExecutionRunProcessLeaseCleaner>(serviceProvider =>
            serviceProvider.GetRequiredService<RecordingTerminalProcessLeaseCleaner>());

        services.RemoveAll<IFakeAgentRuntime>();
        services.RouteRuntimePortsThroughAgentRuntime();
        services.AddSingleton<FakeProgressAgentRuntime>();
        services.AddSingleton<IFakeAgentRuntime>(serviceProvider =>
            serviceProvider.GetRequiredService<FakeProgressAgentRuntime>());
    }

    private static TService CreateRegisteredService<TService>(
        IServiceProvider serviceProvider,
        ServiceDescriptor descriptor)
    {
        object? service = descriptor.ImplementationInstance;
        if (service is null && descriptor.ImplementationFactory is not null)
        {
            service = descriptor.ImplementationFactory(serviceProvider);
        }

        if (service is null && descriptor.ImplementationType is not null)
        {
            service = ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType);
        }

        return service is TService typedService
            ? typedService
            : throw new InvalidOperationException($"Unable to create the registered {typeof(TService).Name} implementation.");
    }

    private static void AssertMilestoneOrder(
        StartupMilestoneRecorder milestones,
        params StartupMilestone[] expectedOrder)
    {
        long? priorSequence = null;
        foreach (var milestone in expectedOrder)
        {
            var sequence = milestones.FirstSequence(milestone);
            Assert.True(sequence.HasValue, $"Startup milestone {milestone} was not observed.");
            if (priorSequence.HasValue)
            {
                Assert.True(
                    priorSequence.Value < sequence.Value,
                    $"Startup milestone {milestone} was observed out of order.");
            }

            priorSequence = sequence;
        }
    }

    private static async Task<TObservation> WaitForObservationOrPropagateAsync<TObservation>(
        Task<TObservation> observationTask,
        Task operationTask,
        string observationName)
    {
        ArgumentNullException.ThrowIfNull(observationTask);
        ArgumentNullException.ThrowIfNull(operationTask);
        ArgumentException.ThrowIfNullOrWhiteSpace(observationName);

        var timeoutTask = Task.Delay(AsyncObservationTimeout);
        var completedTask = await Task.WhenAny(observationTask, operationTask, timeoutTask);
        if (completedTask == observationTask)
        {
            return await observationTask;
        }

        if (completedTask == operationTask)
        {
            await operationTask;
            throw new InvalidOperationException(
                $"{observationName} was not observed before the operation completed.");
        }

        throw new TimeoutException(
            $"{observationName} was not observed within {AsyncObservationTimeout.TotalSeconds:0} seconds.");
    }

    private static void UseDirectWorkspaceService(IServiceCollection services)
    {
        services.RemoveAll<IAgentFrameworkWorkspaceService>();
        services.RemoveAll<IAgentPackageService>();
        services.RemoveAll<IProviderDiagnosticsService>();
        services.RemoveAll<IAgentExecutionCheckpointBridge>();
        services.RemoveAll<IAgentExecutionGovernanceBridge>();
        services.RemoveAll<IAgentExecutionEventSink>();
        services.AddScoped<IAgentPackageService>(serviceProvider => new ZipAgentPackageService(
            serviceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
            ResolveWorkspaceScope(serviceProvider)));
        services.AddScoped<IProviderDiagnosticsService>(serviceProvider =>
        {
            var portFacade = new FakeAgentRuntimePortAdapter(
                serviceProvider.GetRequiredService<IFakeAgentRuntime>());
            return new ProviderDiagnosticsService(portFacade, portFacade);
        });
        services.AddScoped<IAgentExecutionCheckpointBridge>(serviceProvider => new WorkflowBackedAgentExecutionCheckpointBridge(
            serviceProvider.GetRequiredService<ISandboxWorkspaceStore>(),
            serviceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
            ResolveWorkspaceScope(serviceProvider)));
        services.AddScoped<IAgentExecutionGovernanceBridge>(serviceProvider => new DurableAgentExecutionGovernanceBridge(
            serviceProvider.GetRequiredService<IAgentExecutionCheckpointBridge>()));
        services.AddScoped<IAgentExecutionEventSink, NullAgentExecutionEventSink>();
        services.AddScoped(serviceProvider =>
        {
            var profile = serviceProvider
                .GetRequiredService<IDatabaseProfileRuntimeAccessor>()
                .ResolveCurrentProfile()
                .Profile;
            return new AgentExecutionActivityWorkspaceIdentity(
                profile.Id,
                WorkspaceScopeDescriptor.Organization(
                    profile.Id.ToString("N")),
                serviceProvider
                    .GetRequiredService<IAgentExecutionProfileGenerationSource>()
                    .GetGeneration());
        });
        services.AddScoped<IAgentFrameworkWorkspaceService, AgentFrameworkWorkspaceService>();
    }

    private static WorkspaceScopeDescriptor ResolveWorkspaceScope(IServiceProvider serviceProvider)
    {
        var profile = serviceProvider.GetRequiredService<IDatabaseProfileRuntimeAccessor>().ResolveCurrentProfile();
        return WorkspaceScopeDescriptor.Organization(profile.Profile.Id.ToString("N"));
    }

    private static async Task<Guid> WaitForExecutionRunIdAsync(
        FakeProgressAgentRuntime runtime,
        Task executionTask)
    {
        var timeoutTask = Task.Delay(AsyncObservationTimeout);
        var completedTask = await Task.WhenAny(runtime.ExecutionRunIdObserved.Task, executionTask, timeoutTask);
        if (completedTask == executionTask)
        {
            await executionTask;
        }

        if (completedTask == timeoutTask)
        {
            throw new TimeoutException(
                $"Timed out after {AsyncObservationTimeout.TotalSeconds:0} seconds waiting for the fake runtime to observe the execution run id.");
        }

        return await runtime.ExecutionRunIdObserved.Task;
    }

    private static ExecutionRunDetail CreateCompletedRunDetail(Guid executionRunId, Guid agentId, string title)
    {
        var createdAtUtc = DateTimeOffset.UtcNow;
        return new ExecutionRunDetail(
            new ExecutionRunRecord(
                executionRunId,
                agentId,
                null,
                title,
                "manual",
                "split-store-regression",
                Guid.NewGuid().ToString("N"),
                string.Empty,
                "integration-test",
                "integration-test",
                "{}",
                "Verify chat-backed run creation does not load unrelated run slices.",
                "Completed",
                "OpenAI default",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                createdAtUtc,
                createdAtUtc,
                createdAtUtc,
                createdAtUtc,
                string.Empty,
                null,
                []),
            null,
            [
                new ExecutionLogEntry(
                    Guid.NewGuid(),
                    agentId,
                    null,
                    createdAtUtc,
                    ExecutionState.Completed,
                    "validation",
                    "Unrelated run slice should not be loaded.")
                {
                    ExecutionRunId = executionRunId
                }
            ],
            []);
    }

    public interface IStartupBaselineWorkspaceStore :
        ISandboxWorkspaceStore,
        ISandboxWorkspaceChatQueryStore,
        ISandboxWorkspaceChatProjectionQueryStore,
        ISandboxWorkspaceChatSessionStore,
        ISandboxWorkspaceChatRunStartStore,
        ISandboxWorkspaceExecutionRunStore,
        ISandboxWorkspaceExecutionRunMutationStore,
        ISandboxWorkspaceExecutionRunReservationStore,
        IAgentRecruitingEvidenceStore
    {
    }

    internal enum StartupMilestone
    {
        CatalogLoad,
        CatalogSnapshotLoad,
        ActivityAcceptedPublished,
        ProviderProfileGet,
        ProviderSnapshotAcquire,
        ProviderSnapshotCapture,
        ChatSessionGet,
        ChatRunSummariesList,
        AtomicChatRunStart,
        ExecutionRunDetailGet,
        ExecutionRunDetailSave,
        ExecutionRunDetailUpdate,
        ExecutionUpdated,
        ExecutionEventPublished,
        RuntimeEntered
    }

    private sealed record StartupMilestoneRecord(
        long Sequence,
        long Timestamp,
        StartupMilestone Milestone);

    internal sealed class StartupMilestoneRecorder
    {
        private readonly Lock gate = new();
        private readonly List<StartupMilestoneRecord> records = [];
        private long sequence;

        public void Record(StartupMilestone milestone)
        {
            var record = new StartupMilestoneRecord(
                Interlocked.Increment(ref sequence),
                Stopwatch.GetTimestamp(),
                milestone);

            lock (gate)
            {
                records.Add(record);
            }
        }

        public int Count(StartupMilestone milestone)
        {
            lock (gate)
            {
                return records.Count(record => record.Milestone == milestone);
            }
        }

        public long? FirstSequence(StartupMilestone milestone)
        {
            lock (gate)
            {
                return records
                    .Where(record => record.Milestone == milestone)
                    .Select(record => (long?)record.Sequence)
                    .FirstOrDefault();
            }
        }

        public string CreateDiagnosticLine(string scenario)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scenario);

            lock (gate)
            {
                var activityAcceptedPublished = records.FirstOrDefault(
                    record => record.Milestone ==
                        StartupMilestone.ActivityAcceptedPublished);
                var runtimeEntered = records.FirstOrDefault(
                    record => record.Milestone == StartupMilestone.RuntimeEntered);
                if (activityAcceptedPublished is null || runtimeEntered is null)
                {
                    throw new InvalidOperationException(
                        "Accepted-activity-to-runtime diagnostic timing requires both startup milestones.");
                }

                var elapsedMilliseconds = Stopwatch
                    .GetElapsedTime(
                        activityAcceptedPublished.Timestamp,
                        runtimeEntered.Timestamp)
                    .TotalMilliseconds;

                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"startup-baseline scenario={scenario} measurement=diagnostic accepted-publications={CountCore(StartupMilestone.ActivityAcceptedPublished)} catalog-loads={CountCore(StartupMilestone.CatalogLoad)} catalog-snapshot-loads={CountCore(StartupMilestone.CatalogSnapshotLoad)} provider-gets={CountCore(StartupMilestone.ProviderProfileGet)} provider-snapshot-acquires={CountCore(StartupMilestone.ProviderSnapshotAcquire)} provider-snapshot-captures={CountCore(StartupMilestone.ProviderSnapshotCapture)} session-gets={CountCore(StartupMilestone.ChatSessionGet)} run-summary-lists={CountCore(StartupMilestone.ChatRunSummariesList)} atomic-chat-starts={CountCore(StartupMilestone.AtomicChatRunStart)} run-detail-gets={CountCore(StartupMilestone.ExecutionRunDetailGet)} run-detail-saves={CountCore(StartupMilestone.ExecutionRunDetailSave)} run-detail-updates={CountCore(StartupMilestone.ExecutionRunDetailUpdate)} accepted-published-to-runtime-entered-ms={elapsedMilliseconds:F3}");
            }
        }

        public void Reset()
        {
            lock (gate)
            {
                records.Clear();
                Interlocked.Exchange(ref sequence, 0);
            }
        }

        private int CountCore(StartupMilestone milestone)
        {
            return records.Count(record => record.Milestone == milestone);
        }
    }

    private sealed class RecordingAgentExecutionActivityCoordinator(
        AgentExecutionActivityCoordinator inner,
        StartupMilestoneRecorder milestones) :
        IAgentExecutionActivityCoordinator
    {
        public AgentExecutionActivityAdmission AdmitOperation(
            AgentExecutionActivityStreamId streamId,
            Guid? agentId,
            Guid? chatSessionId,
            string acceptedMessage)
        {
            var admission = inner.AdmitOperation(
                streamId,
                agentId,
                chatSessionId,
                acceptedMessage);
            if (admission is AgentExecutionActivityAdmitted)
            {
                milestones.Record(
                    StartupMilestone.ActivityAcceptedPublished);
            }

            return admission;
        }
    }

    public class RecordingWorkspaceStoreProxy : DispatchProxy
    {
        private object? target;
        private StartupMilestoneRecorder? milestones;

        internal static IStartupBaselineWorkspaceStore Create(
            ISandboxWorkspaceStore target,
            StartupMilestoneRecorder milestones)
        {
            var proxy = Create<IStartupBaselineWorkspaceStore, RecordingWorkspaceStoreProxy>();
            var recordingProxy = (RecordingWorkspaceStoreProxy)(object)proxy;
            recordingProxy.target = target;
            recordingProxy.milestones = milestones;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
            {
                throw new InvalidOperationException("The proxied workspace-store method was not supplied.");
            }

            var resolvedTarget = target
                ?? throw new InvalidOperationException("The proxied workspace store was not initialized.");
            var resolvedMilestones = milestones
                ?? throw new InvalidOperationException("The startup milestone recorder was not initialized.");

            RecordWorkspaceCall(resolvedMilestones, targetMethod.Name);

            try
            {
                return targetMethod.Invoke(resolvedTarget, args);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        private static void RecordWorkspaceCall(
            StartupMilestoneRecorder milestones,
            string methodName)
        {
            var milestone = methodName switch
            {
                nameof(ISandboxWorkspaceCatalogStore.LoadCatalogAsync) => StartupMilestone.CatalogLoad,
                nameof(ISandboxWorkspaceCatalogStore.LoadCatalogSnapshotAsync) => StartupMilestone.CatalogSnapshotLoad,
                nameof(ISandboxWorkspaceChatQueryStore.GetChatSessionAsync) => StartupMilestone.ChatSessionGet,
                nameof(ISandboxWorkspaceChatQueryStore.ListChatRunSummariesAsync) => StartupMilestone.ChatRunSummariesList,
                nameof(ISandboxWorkspaceChatRunStartStore.BeginChatBackedRunAsync) => StartupMilestone.AtomicChatRunStart,
                nameof(ISandboxWorkspaceExecutionRunStore.GetExecutionRunDetailAsync) => StartupMilestone.ExecutionRunDetailGet,
                nameof(ISandboxWorkspaceExecutionRunStore.SaveExecutionRunDetailAsync) => StartupMilestone.ExecutionRunDetailSave,
                nameof(ISandboxWorkspaceExecutionRunMutationStore.UpdateExecutionRunDetailAsync) => StartupMilestone.ExecutionRunDetailUpdate,
                _ => (StartupMilestone?)null
            };

            if (milestone.HasValue)
            {
                milestones.Record(milestone.Value);
            }
        }
    }

    public class TerminalFailureWorkspaceStoreProxy : DispatchProxy
    {
        internal const string PersistenceSecret = "terminal-persistence-secret";

        private object? target;
        private bool failOnTerminalLog;
        private int terminalMutationObserved;

        internal static IStartupBaselineWorkspaceStore Create(
            ISandboxWorkspaceStore target,
            bool failOnTerminalLog)
        {
            var proxy = Create<
                IStartupBaselineWorkspaceStore,
                TerminalFailureWorkspaceStoreProxy>();
            var failureProxy = (TerminalFailureWorkspaceStoreProxy)(object)proxy;
            failureProxy.target = target;
            failureProxy.failOnTerminalLog = failOnTerminalLog;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null || args is null)
            {
                throw new InvalidOperationException(
                    "The proxied workspace-store invocation was incomplete.");
            }

            var resolvedTarget = target
                ?? throw new InvalidOperationException(
                    "The terminal-failure workspace store was not initialized.");
            WrapTerminalUpdate(targetMethod, args);
            try
            {
                return targetMethod.Invoke(resolvedTarget, args);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        private void WrapTerminalUpdate(MethodInfo targetMethod, object?[] args)
        {
            if (!string.Equals(
                    targetMethod.Name,
                    nameof(ISandboxWorkspaceExecutionRunMutationStore.UpdateExecutionRunDetailAsync),
                    StringComparison.Ordinal))
            {
                return;
            }

            if (args[1] is Func<ExecutionRunDetail, ExecutionRunDetail> update)
            {
                args[1] = new Func<ExecutionRunDetail, ExecutionRunDetail>(current =>
                    FailAtConfiguredTerminalWrite(update(current)));
                return;
            }

            if (args[1] is Func<SandboxWorkspaceCatalog, ExecutionRunDetail, ExecutionRunDetail> catalogUpdate)
            {
                args[1] = new Func<SandboxWorkspaceCatalog, ExecutionRunDetail, ExecutionRunDetail>(
                    (catalog, current) => FailAtConfiguredTerminalWrite(
                        catalogUpdate(catalog, current)));
            }
        }

        private ExecutionRunDetail FailAtConfiguredTerminalWrite(
            ExecutionRunDetail updated)
        {
            if (updated.Run.State != ExecutionState.Failed)
            {
                return updated;
            }

            var terminalWriteIndex = Interlocked.Increment(
                ref terminalMutationObserved);
            var shouldFail = !failOnTerminalLog || terminalWriteIndex >= 2;
            return shouldFail
                ? throw new InvalidOperationException(
                    $"Terminal persistence failed with api_key={PersistenceSecret}.")
                : updated;
        }
    }

    private sealed class RecordingTerminalProcessLeaseCleaner :
        IWorkspaceExecutionRunProcessLeaseCleaner
    {
        private readonly ConcurrentQueue<Guid> executionRunIds = new();

        public IReadOnlyList<Guid> ExecutionRunIds => executionRunIds.ToArray();

        public Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(Guid executionRunId)
        {
            executionRunIds.Enqueue(executionRunId);
            return Task.FromResult(
                WorkspaceExecutionRunProcessCleanupResult.Empty(executionRunId));
        }
    }

    private sealed class TerminalFailureLogProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName)
            => new TerminalFailureLogger(Entries);

        public void Dispose()
        {
        }

        private sealed class TerminalFailureLogger(
            ConcurrentQueue<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                entries.Enqueue(
                    $"{formatter(state, exception)} {exception?.ToString() ?? string.Empty}");
            }
        }
    }

    private sealed class RecordingProviderProfileRegistry(
        IProviderProfileRegistry inner,
        IProviderRuntimeProfileSource runtimeSource,
        IProviderRuntimeProfileSnapshotSource snapshotSource,
        StartupMilestoneRecorder milestones) :
        IProviderProfileRegistry,
        IProviderRuntimeProfileSource,
        IProviderRuntimeProfileSnapshotSource
    {
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(
            CancellationToken cancellationToken = default)
        {
            return inner.ListProvidersAsync(cancellationToken);
        }

        public Task<ProviderProfile?> GetProviderAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
        {
            milestones.Record(StartupMilestone.ProviderProfileGet);
            return inner.GetProviderAsync(providerId, cancellationToken);
        }

        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(
            Guid? providerId = null,
            CancellationToken cancellationToken = default)
        {
            return inner.GetProviderEditorAsync(providerId, cancellationToken);
        }

        public Task<Guid> SaveProviderAsync(
            ProviderProfileEditorModel model,
            CancellationToken cancellationToken = default)
        {
            return inner.SaveProviderAsync(model, cancellationToken);
        }

        public Task DeleteProviderAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
        {
            return inner.DeleteProviderAsync(providerId, cancellationToken);
        }

        public Task<ProviderProfile> UpdateProviderAsync(
            Guid providerId,
            Func<ProviderProfile, ProviderProfile> update,
            CancellationToken cancellationToken = default)
        {
            return inner.UpdateProviderAsync(providerId, update, cancellationToken);
        }

        Task<IReadOnlyList<ProviderProfile>>
            IProviderRuntimeProfileSource.ListProvidersAsync(
            CancellationToken cancellationToken)
        {
            return runtimeSource.ListProvidersAsync(cancellationToken);
        }

        Task<ProviderProfile?>
            IProviderRuntimeProfileSource.GetProviderAsync(
            Guid providerId,
            CancellationToken cancellationToken)
        {
            milestones.Record(StartupMilestone.ProviderProfileGet);
            return runtimeSource.GetProviderAsync(
                providerId,
                cancellationToken);
        }

        public Task<ProviderRuntimeProfileSnapshotLease?> AcquireProviderAsync(
            Guid providerId,
            SandboxWorkspaceCatalogSnapshot catalogSnapshot,
            CancellationToken cancellationToken = default)
        {
            milestones.Record(
                StartupMilestone.ProviderSnapshotAcquire);
            return snapshotSource.AcquireProviderAsync(
                providerId,
                catalogSnapshot,
                cancellationToken);
        }

        public ProviderRuntimeProfileSnapshotLease? CaptureProvider(
            Guid providerId,
            SandboxWorkspaceCatalogSnapshot catalogSnapshot)
        {
            milestones.Record(
                StartupMilestone.ProviderSnapshotCapture);
            return snapshotSource.CaptureProvider(
                providerId,
                catalogSnapshot);
        }
    }

    private sealed class RecordingAgentExecutionEventSink(
        StartupMilestoneRecorder milestones) : IAgentExecutionEventSink
    {
        public TaskCompletionSource<ExecutionEvent> PlanningPublished { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task PublishAsync(
            ExecutionEvent executionEvent,
            CancellationToken cancellationToken = default)
        {
            if (string.Equals(executionEvent.Phase, "Planning", StringComparison.Ordinal))
            {
                milestones.Record(StartupMilestone.ExecutionEventPublished);
                PlanningPublished.TrySetResult(executionEvent);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class StartupBarrierAgentRuntime(
        StartupMilestoneRecorder milestones) : IFakeAgentRuntime
    {
        public TaskCompletionSource<Guid> Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ProviderHealthResult> TestProviderAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Provider tests are not part of the startup baseline.");
        }

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Provider test chats are not part of the startup baseline.");
        }

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
            ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Provider model maintenance is not part of the startup baseline.");
        }

        public async Task<AgentRuntimeResponse> RunAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            string prompt,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
        {
            var executionRunId = session.LatestExecutionRunId
                ?? throw new InvalidOperationException("The startup runtime requires a chat-backed execution run.");

            milestones.Record(StartupMilestone.RuntimeEntered);
            Entered.TrySetResult(executionRunId);
            await Release.Task.WaitAsync(cancellationToken);

            return new AgentRuntimeResponse(
                ResponseText: "Startup baseline completed.",
                InputTokens: 0,
                OutputTokens: 0,
                ToolCalls: 0,
                RuntimeSessionKey: string.Empty,
                SerializedSessionStateJson: null,
                PendingApprovals: []);
        }

        public Task<AgentRuntimeResponse> RespondToPendingApprovalsAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            bool approved,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
        {
            throw new NotSupportedException("Pending approval continuation is not part of the startup baseline.");
        }
    }

    private sealed class FakeProgressAgentRuntime : IFakeAgentRuntime
    {
        public TaskCompletionSource<Guid> ExecutionRunIdObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ProgressPersisted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> AllowCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ProviderRequestCompatibilityEvidence? RequestCompatibilityEvidence { get; set; }

        public Exception? Failure { get; set; }

        public Exception? StartupFailure { get; set; }

        public Task<ProviderHealthResult> TestProviderAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderHealthResult(true, "ok", []));
        }

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderTestChatResult(provider.DefaultModel, "ok", 1, 1));
        }

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
            ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderModelMaintenanceEditorResult(request.TargetModel, request.BaseModel, request.SystemPrompt, request.ContextLength, string.Empty, "ok"));
        }

        public async Task<AgentRuntimeResponse> RunAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            string prompt,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
        {
            if (!session.LatestExecutionRunId.HasValue)
            {
                throw new InvalidOperationException("Expected a chat-backed execution run to populate the latest execution run id.");
            }

            ExecutionRunIdObserved.TrySetResult(session.LatestExecutionRunId.Value);
            if (StartupFailure is not null) {
                throw StartupFailure;
            }
            await progressCallback(ExecutionState.Running, "Implementation", "Applying the current implementation plan.");
            ProgressPersisted.TrySetResult(true);
            await AllowCompletion.Task.WaitAsync(cancellationToken);

            if (Failure is not null)
            {
                throw Failure;
            }

            return new AgentRuntimeResponse(
                ResponseText: "Completed successfully.",
                InputTokens: 10,
                OutputTokens: 20,
                ToolCalls: 0,
                RuntimeSessionKey: string.Empty,
                SerializedSessionStateJson: null,
                PendingApprovals: [])
            {
                EntryAgentRequestCompatibilityEvidence = RequestCompatibilityEvidence
            };
        }

        public Task<AgentRuntimeResponse> RespondToPendingApprovalsAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            bool approved,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
        {
            throw new NotSupportedException("Pending approval continuation is not used by this regression test.");
        }
    }

    private sealed class StructuredOutputApprovalRuntime : IFakeAgentRuntime
    {
        public List<AgentStructuredOutputContract?> RunStructuredOutputs { get; } = [];

        public List<AgentStructuredOutputContract?> ContinuationStructuredOutputs { get; } = [];

        public List<AgentRuntimeExecutionOptions?> RunExecutionOptions { get; } = [];

        public List<AgentRuntimeExecutionOptions?> ContinuationExecutionOptions { get; } = [];

        public List<bool> ContinuationSuppressApprovalRequirements { get; } = [];

        public List<IReadOnlyList<PendingToolApprovalRecord>> ContinuationSessionPendingApprovals { get; } = [];

        public List<Guid> ObservedExecutionRunIds { get; } = [];

        public string InitialResponseText { get; init; } = "Pending approval.";

        public int InitialInputTokens { get; init; } = 7;

        public int InitialOutputTokens { get; init; } = 11;

        public int InitialCachedInputTokens { get; init; }

        public IReadOnlyList<PendingToolApprovalRecord> InitialPendingApprovals { get; init; } =
        [
            new PendingToolApprovalRecord(
                "approval-001",
                "call-001",
                "workspace_write_file",
                "function",
                "Write artifacts/result.md.",
                """{"path":"artifacts/result.md"}""")
        ];

        public IReadOnlyList<AgentFinalizerInvocation> InitialFinalizerInvocations { get; init; } = [];

        public IReadOnlyList<AgentToolInvocationTrace> InitialToolInvocationTraces { get; init; } = [];

        public IReadOnlyList<ProviderUsageObservation> InitialUsageObservations { get; init; } = [];

        public AgentRuntimeContextAssemblyManifest? InitialContextAssemblyManifest { get; init; }

        public ProviderRequestCompatibilityEvidence? InitialRequestCompatibilityEvidence { get; set; }

        public bool ThrowUsageExceptionOnRun { get; init; }

        public string ContinuationResponseText { get; init; } = JsonSerializer.Serialize(
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "The process step implementation was completed after approval.",
                EvidenceRefs = ["execution://run-001"],
                NextActions = [],
                HumanReadableSummaryMarkdown = "Completed."
            },
            AgentOutputJson.SerializerOptions);

        public int ContinuationCachedInputTokens { get; init; }

        public IReadOnlyList<AgentFinalizerInvocation> ContinuationFinalizerInvocations { get; init; } = [];

        public IReadOnlyList<AgentToolInvocationTrace> ContinuationToolInvocationTraces { get; init; } = [];

        public IReadOnlyList<AgentRuntimeResponse> ContinuationResponses { get; init; } = [];

        public Exception? ContinuationException { get; init; }

        private int continuationResponseIndex;

        public Task<ProviderHealthResult> TestProviderAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderHealthResult(true, "ok", []));
        }

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderTestChatResult(provider.DefaultModel, "ok", 1, 1));
        }

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
            ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderModelMaintenanceEditorResult(request.TargetModel, request.BaseModel, request.SystemPrompt, request.ContextLength, string.Empty, "ok"));
        }

        public Task<AgentRuntimeResponse> RunAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            string prompt,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
        {
            RunStructuredOutputs.Add(structuredOutput);
            RunExecutionOptions.Add(executionOptions);
            if (!session.LatestExecutionRunId.HasValue)
            {
                throw new InvalidOperationException("Expected the runtime session to carry the execution run id.");
            }

            ObservedExecutionRunIds.Add(session.LatestExecutionRunId.Value);
            if (ThrowUsageExceptionOnRun)
            {
                throw new AgentRuntimeUsageException(
                    "Fake runtime failed after provider usage.",
                    new InvalidOperationException("Fake provider failure."),
                    InitialUsageObservations,
                    InitialToolInvocationTraces);
            }

            return Task.FromResult(new AgentRuntimeResponse(
                ResponseText: InitialResponseText,
                InputTokens: InitialInputTokens,
                OutputTokens: InitialOutputTokens,
                ToolCalls: InitialPendingApprovals.Count,
                RuntimeSessionKey: "runtime-session-key",
                SerializedSessionStateJson: """{"state":"pending"}""",
                PendingApprovals: InitialPendingApprovals)
            {
                CachedInputTokens = InitialCachedInputTokens,
                FinalizerInvocations = InitialFinalizerInvocations,
                ToolInvocationTraces = InitialToolInvocationTraces.Count == 0
                    ? CreateFinalizerToolInvocationTraces(InitialFinalizerInvocations)
                    : InitialToolInvocationTraces,
                UsageObservations = InitialUsageObservations,
                ContextAssemblyManifest = InitialContextAssemblyManifest,
                EntryAgentRequestCompatibilityEvidence = InitialRequestCompatibilityEvidence
            });
        }

        public Task<AgentRuntimeResponse> RespondToPendingApprovalsAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            bool approved,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
        {
            if (!approved)
            {
                throw new InvalidOperationException("This fake runtime expects the continuation approval to be granted.");
            }

            ContinuationStructuredOutputs.Add(structuredOutput);
            ContinuationExecutionOptions.Add(executionOptions);
            ContinuationSuppressApprovalRequirements.Add(suppressApprovalRequirements);
            ContinuationSessionPendingApprovals.Add(session.Compatibility?.PendingApprovals ?? []);
            if (continuationResponseIndex < ContinuationResponses.Count)
            {
                return Task.FromResult(ContinuationResponses[continuationResponseIndex++]);
            }

            if (ContinuationException is not null)
            {
                throw ContinuationException;
            }

            return Task.FromResult(new AgentRuntimeResponse(
                ResponseText: ContinuationResponseText,
                InputTokens: 5,
                OutputTokens: 13,
                ToolCalls: 0,
                RuntimeSessionKey: string.Empty,
                SerializedSessionStateJson: null,
                PendingApprovals: [])
            {
                CachedInputTokens = ContinuationCachedInputTokens,
                FinalizerInvocations = ContinuationFinalizerInvocations,
                ToolInvocationTraces = ContinuationToolInvocationTraces.Count == 0
                    ? CreateFinalizerToolInvocationTraces(ContinuationFinalizerInvocations)
                    : ContinuationToolInvocationTraces
            });
        }

        private static IReadOnlyList<AgentToolInvocationTrace> CreateFinalizerToolInvocationTraces(
            IReadOnlyList<AgentFinalizerInvocation> finalizerInvocations)
        {
            var timestamp = DateTimeOffset.UtcNow;
            return finalizerInvocations
                .Select(invocation => new AgentToolInvocationTrace(
                    invocation.ToolName,
                    ToolInvocationClassification.Read,
                    invocation.Sequence,
                    StartedAtUtc: timestamp,
                    CompletedAtUtc: timestamp,
                    Succeeded: true,
                    FailureMessage: string.Empty))
                .ToList();
        }
    }

    private sealed class UsageReportingRepairService : IAgentOutputRepairService
    {
        public Task<AgentOutputRepairAttemptResult> TryRepairAsync(
            AgentOutputRepairRequest repairRequest,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new AgentOutputRepairAttemptResult
            {
                Succeeded = true,
                RepairedRawOutput = SerializeOutcome(CreateCompletedOutcome("The repair service produced valid machine output.")),
                RemainingErrors = [],
                UsageObservations =
                [
                    CreateUsageObservation(ProviderUsageSourcePhases.StructuredOutputRepair, 3, 1, 2)
                ]
            });
        }
    }
}
