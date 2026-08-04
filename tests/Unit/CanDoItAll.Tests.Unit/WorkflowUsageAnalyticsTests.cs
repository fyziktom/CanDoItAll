using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowUsageAnalyticsTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 12, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProviderMappingPreservesEveryFactAndSeparatesUsageFromPricingKnowledge()
    {
        var context = CreateContext();
        var provider = CreateProvider();
        var knownId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var unknownId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        ProviderUsageObservation[] observations =
        [
            new ProviderUsageObservation(
                knownId,
                FixedUtcNow,
                provider.Name,
                provider.Kind,
                "model-a",
                provider.Transport,
                "agent-runtime",
                ProviderUsageObservationStatus.Observed,
                InputTokens: 11,
                CachedInputTokens: 3,
                OutputTokens: 5,
                ReasoningTokens: 2,
                TotalTokens: 16,
                ToolCallCount: 1)
            {
                ProviderCostUsd = 0m
            },
            new ProviderUsageObservation(
                unknownId,
                FixedUtcNow.AddSeconds(1),
                provider.Name,
                provider.Kind,
                "unpriced-model",
                provider.Transport,
                "structured-output-repair",
                ProviderUsageObservationStatus.Observed,
                InputTokens: 7,
                CachedInputTokens: 0,
                OutputTokens: 4,
                ReasoningTokens: 1,
                TotalTokens: 11,
                ToolCallCount: 0)
        ];

        var mapped = WorkflowUsageObservationFactory.FromProviderObservations(
            context,
            provider,
            provider.DefaultModel,
            observations);

        Assert.Equal(2, mapped.Count);
        Assert.Equal(knownId, mapped[0].Id.Value);
        Assert.Equal(unknownId, mapped[1].Id.Value);
        Assert.Equal(16, mapped[0].TotalTokens);
        Assert.Equal(2, mapped[0].ReasoningTokens);
        Assert.Equal(WorkflowUsageStatus.Observed, mapped[0].UsageStatus);
        Assert.Equal(WorkflowPricingStatus.Known, mapped[0].PricingStatus);
        Assert.Equal(0m, mapped[0].CostUsd);
        Assert.Equal(WorkflowUsagePricingProvenance.ProviderReported, mapped[0].PricingProvenance);
        Assert.Equal(WorkflowUsageStatus.Observed, mapped[1].UsageStatus);
        Assert.Equal(WorkflowPricingStatus.Unknown, mapped[1].PricingStatus);
        Assert.Null(mapped[1].CostUsd);
    }

    [Fact]
    public void SyntheticMappingUsesStableIdsAndRejectsCorruptDimensions()
    {
        var context = CreateContext();
        var provider = CreateProvider();
        var first = WorkflowUsageObservationFactory.FromProviderResponseMetrics(
            context,
            provider,
            provider.DefaultModel,
            inputTokens: 3,
            cachedInputTokens: 1,
            outputTokens: 2,
            reasoningTokens: 0,
            totalTokens: 5,
            toolCallCount: 0,
            FixedUtcNow);
        var second = WorkflowUsageObservationFactory.FromProviderResponseMetrics(
            context,
            provider,
            provider.DefaultModel,
            inputTokens: 3,
            cachedInputTokens: 1,
            outputTokens: 2,
            reasoningTokens: 0,
            totalTokens: 5,
            toolCallCount: 0,
            FixedUtcNow);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first, second);
        Assert.Throws<InvalidOperationException>(() => WorkflowUsageObservationFactory.FromProviderResponseMetrics(
            context,
            provider,
            provider.DefaultModel,
            inputTokens: 1,
            cachedInputTokens: 2,
            outputTokens: 0,
            reasoningTokens: 0,
            totalTokens: 1,
            toolCallCount: 0,
            FixedUtcNow));
        Assert.Throws<InvalidOperationException>(() => WorkflowUsageObservationFactory.FromProviderResponseMetrics(
            context,
            provider,
            provider.DefaultModel,
            inputTokens: 3,
            cachedInputTokens: 0,
            outputTokens: 2,
            reasoningTokens: 0,
            totalTokens: 4,
            toolCallCount: 0,
            FixedUtcNow));

        var corruptProviderFact = new ProviderUsageObservation(
            Guid.Parse("10000000-0000-0000-0000-000000000003"),
            FixedUtcNow,
            provider.Name,
            provider.Kind,
            provider.DefaultModel,
            provider.Transport,
            "agent-runtime",
            ProviderUsageObservationStatus.Observed,
            InputTokens: -1,
            CachedInputTokens: 0,
            OutputTokens: 1,
            ReasoningTokens: 0,
            TotalTokens: 0,
            ToolCallCount: 0);
        Assert.Throws<InvalidOperationException>(() => WorkflowUsageObservationFactory.FromProviderObservation(
            context,
            provider,
            provider.DefaultModel,
            corruptProviderFact));
    }

    [Fact]
    public async Task InMemoryStoreIsIdempotentAndRejectsImmutableFactConflictsAtomically()
    {
        var store = new InMemoryWorkflowUsageObservationStore();
        var runId = WorkflowRunId.New();
        var first = CreateObservation(
            new WorkflowUsageObservationId(Guid.Parse("20000000-0000-0000-0000-000000000001")),
            runId);
        var second = CreateObservation(
            new WorkflowUsageObservationId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
            runId);

        await Assert.ThrowsAsync<WorkflowUsageObservationCorrelationException>(() => store.AppendAsync(
            first with { Id = WorkflowUsageObservationId.New(), RunId = null }));
        await store.AppendAsync(first);
        await store.AppendAsync(first);
        await Assert.ThrowsAsync<WorkflowUsageObservationConflictException>(() => store.AppendRangeAsync(
        [
            second,
            first with { ProviderRequestId = "conflicting-request" }
        ]));

        var stored = await store.ListAsync(new WorkflowUsageObservationQuery());
        Assert.Single(stored);
        Assert.Equal(first, stored[0]);
    }

    [Fact]
    public async Task AnalyticsUsesAllFilteredRunsWhileRecentRunsRemainBounded()
    {
        var workflowId = new WorkflowId(Guid.Parse("30000000-0000-0000-0000-000000000001"));
        var versionId = new WorkflowVersionId(Guid.Parse("30000000-0000-0000-0000-000000000002"));
        var runStore = new InMemoryWorkflowRunStore();
        var observationStore = new InMemoryWorkflowUsageObservationStore();
        WorkflowCatalogItem[] definitions =
        [
            new WorkflowCatalogItem(
                workflowId,
                versionId,
                "Analytics workflow",
                "Analytics workflow",
                WorkflowLifecycleStatus.Active,
                WorkflowRuntimeBackendKind.InProcess,
                FixedUtcNow)
        ];
        var runs = new List<WorkflowRunSnapshot>();
        for (var index = 0; index < 10; index++)
        {
            var createdAtUtc = FixedUtcNow.AddMinutes(-20 + index);
            var state = index == 9 ? WorkflowRunState.Running : WorkflowRunState.Completed;
            var run = new WorkflowRunSnapshot(
                new WorkflowRunId(Guid.Parse($"30000000-0000-0000-0000-{index + 10:D12}")),
                workflowId,
                versionId,
                state,
                WorkflowRuntimeBackendKind.InProcess,
                $"backend-{index}",
                $"Run {index}",
                createdAtUtc,
                createdAtUtc.AddMinutes(1))
            {
                TerminalAtUtc = state == WorkflowRunState.Completed ? createdAtUtc.AddMinutes(2) : null
            };
            runs.Add(run);
            await runStore.SaveRunAsync(run);
            await observationStore.AppendAsync(CreateObservation(
                new WorkflowUsageObservationId(Guid.Parse($"40000000-0000-0000-0000-{index + 10:D12}")),
                run.RunId,
                workflowId,
                versionId,
                inputTokens: index + 1));
        }

        var service = new WorkflowAnalyticsQueryService(
            new WorkflowAnalyticsCatalogStub(definitions),
            runStore,
            new WorkflowUsageAnalyticsStore(observationStore),
            new WorkflowUsageFixedTimeProvider(FixedUtcNow));

        var result = await service.QueryAsync(new WorkflowAnalyticsQuery(RecentTake: 8));

        Assert.Equal(10, result.RunCount);
        Assert.Equal(10, result.Runs.Count);
        Assert.Equal(8, result.RecentRuns.Count);
        Assert.Equal(10, result.Usage.ObservationCount);
        Assert.Equal(55, result.Usage.InputTokens);
        Assert.Equal(10, result.Duration.AvailableRunCount);
        Assert.Equal(9, result.Duration.FinalRunCount);
        Assert.Equal(1, result.Duration.ActiveRunCount);
    }

    [Fact]
    public async Task HistoricalTerminalRunWithoutTerminalTimestampHasUnavailableDuration()
    {
        var workflowId = WorkflowId.New();
        var versionId = WorkflowVersionId.New();
        var runStore = new InMemoryWorkflowRunStore();
        var run = new WorkflowRunSnapshot(
            WorkflowRunId.New(),
            workflowId,
            versionId,
            WorkflowRunState.Completed,
            WorkflowRuntimeBackendKind.InProcess,
            "historical-run",
            "Historical run",
            FixedUtcNow.AddHours(-1),
            FixedUtcNow.AddMinutes(-30));
        await runStore.SaveRunAsync(run);
        var service = new WorkflowAnalyticsQueryService(
            new WorkflowAnalyticsCatalogStub([]),
            runStore,
            new WorkflowUsageAnalyticsStore(new InMemoryWorkflowUsageObservationStore()),
            new WorkflowUsageFixedTimeProvider(FixedUtcNow));

        var result = await service.QueryAsync(new WorkflowAnalyticsQuery());

        Assert.Null(Assert.Single(result.Runs).Duration);
        Assert.Equal(1, result.Duration.UnavailableRunCount);
    }

    [Fact]
    public async Task RuntimePersistsOneCorrelatedFactWhenProgressAndBackendReturnTheSameObservation()
    {
        var definition = CreateDefinition();
        var observation = CreateObservation(
            new WorkflowUsageObservationId(Guid.Parse("50000000-0000-0000-0000-000000000001")),
            runId: null,
            definition.Id,
            definition.VersionId);
        var runStore = new InMemoryWorkflowRunStore();
        var usageStore = new InMemoryWorkflowUsageObservationStore();
        var backend = new WorkflowUsageCompletingBackend(observation, FixedUtcNow);
        var manager = new WorkflowRuntimeManager(
            [backend],
            runStore,
            new WorkflowActiveRunRegistry(),
            new WorkflowUsageFixedTimeProvider(FixedUtcNow),
            usageStore: usageStore);
        var origin = new WorkflowLaunchOrigin.ProcessAssignment(
            Guid.Parse("50000000-0000-0000-0000-000000000002"),
            Guid.Parse("50000000-0000-0000-0000-000000000003"),
            new WorkflowLaunchCorrelationId("workflow-usage-test"));

        var run = await manager.StartAsync(
            definition,
            CreateStartRequest(definition, origin));
        var stored = await usageStore.ListAsync(new WorkflowUsageObservationQuery());

        var fact = Assert.Single(stored);
        Assert.Equal(run.RunId, fact.RunId);
        Assert.Equal(origin, fact.Origin);
        Assert.Equal(origin, run.Origin);
    }

    [Fact]
    public async Task RuntimePersistsFailureFactsBeforeRethrowingBackendFailure()
    {
        var definition = CreateDefinition();
        var observation = CreateObservation(
            new WorkflowUsageObservationId(Guid.Parse("60000000-0000-0000-0000-000000000001")),
            runId: null,
            definition.Id,
            definition.VersionId);
        var runStore = new InMemoryWorkflowRunStore();
        var usageStore = new InMemoryWorkflowUsageObservationStore();
        var manager = new WorkflowRuntimeManager(
            [new WorkflowUsageFailingBackend(observation)],
            runStore,
            new WorkflowActiveRunRegistry(),
            new WorkflowUsageFixedTimeProvider(FixedUtcNow),
            usageStore: usageStore);

        await Assert.ThrowsAsync<WorkflowUsageObservationException>(() => manager.StartAsync(
            definition,
            CreateStartRequest(definition, origin: null)));

        var failedRun = Assert.Single(await runStore.ListRunsAsync());
        var fact = Assert.Single(await usageStore.ListAsync(new WorkflowUsageObservationQuery()));
        Assert.Equal(WorkflowRunState.Failed, failedRun.State);
        Assert.Equal(failedRun.RunId, fact.RunId);
    }

    [Fact]
    public async Task PersistentStoreRoundTripsImmutableFactsAndAggregatesWithoutDoubleCounting()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(PersistentWorkflowUsageObservationStore).Assembly]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"workflow-usage-{Guid.NewGuid():N}")
            .Options;
        var store = new PersistentWorkflowUsageObservationStore(
            new WorkflowUsageTestDbContextFactory(options));
        var runId = new WorkflowRunId(Guid.Parse("70000000-0000-0000-0000-000000000001"));
        var processRunId = Guid.Parse("70000000-0000-0000-0000-000000000002");
        var origin = new WorkflowLaunchOrigin.ProcessAssignment(
            processRunId,
            Guid.Parse("70000000-0000-0000-0000-000000000003"),
            new WorkflowLaunchCorrelationId("persistent-workflow-usage"));
        var observation = CreateObservation(
            new WorkflowUsageObservationId(Guid.Parse("70000000-0000-0000-0000-000000000004")),
            runId) with
        {
            Origin = origin
        };

        await store.AppendRangeAsync([observation, observation]);
        var roundTripped = Assert.Single(await store.ListAsync(new WorkflowUsageObservationQuery
        {
            RunIds = [runId],
            OriginProcessRunIds = [new WorkflowProcessRunId(processRunId)]
        }));
        var aggregate = await store.AggregateAsync(new WorkflowUsageAnalyticsStoreQuery([runId]));

        Assert.Equal(observation, roundTripped);
        Assert.Equal(1, aggregate.Usage.ObservationCount);
        Assert.Equal(observation.InputTokens, aggregate.Usage.InputTokens);
        Assert.Equal(origin, roundTripped.Origin);
        await Assert.ThrowsAsync<WorkflowUsageObservationCorrelationException>(() => store.AppendAsync(
            observation with { Id = WorkflowUsageObservationId.New(), RunId = null }));
        await Assert.ThrowsAsync<WorkflowUsageObservationConflictException>(() => store.AppendAsync(
            observation with { ProviderRequestId = "conflicting-request" }));
    }

    [Fact]
    public void ModuleCompositionUsesOnePersistentStoreForRawFactsAndDatabaseAggregates()
    {
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"workflow-usage-composition-{Guid.NewGuid():N}")
            .Options;
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<AppDbContext>>(
            new WorkflowUsageTestDbContextFactory(options));
        services.AddAgentFrameworkModule(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var observationStore = scope.ServiceProvider.GetRequiredService<IWorkflowUsageObservationStore>();
        var analyticsStore = scope.ServiceProvider.GetRequiredService<IWorkflowUsageAnalyticsStore>();
        var launchIdempotencyStore = scope.ServiceProvider.GetRequiredService<IWorkflowLaunchIdempotencyStore>();

        Assert.IsType<PersistentWorkflowUsageObservationStore>(observationStore);
        Assert.Same(observationStore, analyticsStore);
        Assert.IsType<PersistentWorkflowLaunchIdempotencyStore>(launchIdempotencyStore);
        Assert.Equal(
            ServiceLifetime.Scoped,
            Assert.Single(services, descriptor =>
                descriptor.ServiceType == typeof(IWorkflowUsageObservationStore)).Lifetime);
        Assert.Equal(
            ServiceLifetime.Scoped,
            Assert.Single(services, descriptor =>
                descriptor.ServiceType == typeof(IWorkflowUsageAnalyticsStore)).Lifetime);
        Assert.Equal(
            ServiceLifetime.Scoped,
            Assert.Single(services, descriptor =>
                descriptor.ServiceType == typeof(IWorkflowLaunchIdempotencyStore)).Lifetime);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IWorkflowAnalyticsQueryService));
    }

    private static WorkflowUsageObservationContext CreateContext()
        => new(
            new WorkflowRunId(Guid.Parse("80000000-0000-0000-0000-000000000001")),
            new WorkflowId(Guid.Parse("80000000-0000-0000-0000-000000000002")),
            new WorkflowVersionId(Guid.Parse("80000000-0000-0000-0000-000000000003")),
            new WorkflowNodeId("usage-node"),
            ExecutorId: null,
            new WorkflowComponentId(Guid.Parse("80000000-0000-0000-0000-000000000004")),
            WorkflowUsageProducerKind.LlmComponent,
            Guid.Parse("80000000-0000-0000-0000-000000000005"),
            Attempt: 1,
            FixedUtcNow.AddSeconds(-1),
            FixedUtcNow);

    private static ProviderProfile CreateProvider()
        => new(
            Guid.Parse("90000000-0000-0000-0000-000000000001"),
            "workflow-usage-provider",
            ProviderKind.OpenAi,
            "https://example.invalid/v1",
            "WORKFLOW_USAGE_API_KEY",
            "model-a",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: ["model-a"],
            Purpose: ProviderProfilePurpose.Chat)
        {
            ModelPrices = [new ProviderModelTokenPrice("model-a", 1m, 0.5m, 2m)]
        };

    private static WorkflowUsageObservation CreateObservation(
        WorkflowUsageObservationId id,
        WorkflowRunId? runId = null,
        WorkflowId? workflowId = null,
        WorkflowVersionId? versionId = null,
        int inputTokens = 5)
        => new(
            id,
            runId,
            workflowId ?? new WorkflowId(Guid.Parse("91000000-0000-0000-0000-000000000001")),
            versionId ?? new WorkflowVersionId(Guid.Parse("91000000-0000-0000-0000-000000000002")),
            new WorkflowNodeId("usage-node"),
            new WorkflowExecutorId("usage-executor"),
            ComponentId: null,
            WorkflowUsageProducerKind.Executor,
            Guid.Parse("91000000-0000-0000-0000-000000000003"),
            Attempt: 1,
            ProviderProfileId: null,
            "workflow-usage-provider",
            ProviderKind.OpenAi,
            ProviderTransportKind.ChatCompletions,
            "model-a",
            "executor",
            WorkflowUsageStatus.Observed,
            WorkflowPricingStatus.Known,
            WorkflowUsagePricingProvenance.ProviderReported,
            inputTokens,
            CachedInputTokens: 1,
            OutputTokens: 2,
            ReasoningTokens: 0,
            TotalTokens: inputTokens + 2,
            ToolCallCount: 1,
            CostUsd: 0.01m,
            PricingProfileHash: "profile-hash",
            PricingVersion: "v1",
            ProviderRequestId: "request-id",
            ProviderResponseId: "response-id",
            FixedUtcNow.AddSeconds(-1),
            FixedUtcNow,
            FixedUtcNow,
            Origin: null);

    private static WorkflowDefinition CreateDefinition()
        => new(
            new WorkflowId(Guid.Parse("92000000-0000-0000-0000-000000000001")),
            new WorkflowVersionId(Guid.Parse("92000000-0000-0000-0000-000000000002")),
            "Usage runtime workflow",
            "Usage runtime workflow",
            WorkflowLifecycleStatus.Active,
            new WorkflowGraph(new WorkflowNodeId("usage-node"), [], []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            FixedUtcNow,
            FixedUtcNow);

    private static WorkflowRunStartRequest CreateStartRequest(
        WorkflowDefinition definition,
        WorkflowLaunchOrigin? origin)
        => new(
            definition.Id,
            definition.VersionId,
            "{}",
            WorkflowRuntimeBackendKind.InProcess,
            SourceProcessRunId: null,
            SourceProcessAssignmentId: null)
        {
            Origin = origin
        };
}

internal sealed class WorkflowAnalyticsCatalogStub(
    IReadOnlyList<WorkflowCatalogItem> definitions) : IWorkflowCatalogService
{
    public Task<IReadOnlyList<WorkflowCatalogItem>> ListDefinitionsAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(definitions);

    public Task<WorkflowDefinitionDetail?> GetDefinitionAsync(
        WorkflowId workflowId,
        WorkflowVersionId? versionId = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<WorkflowDefinitionDetail?> GetLatestDefinitionByStatusAsync(
        WorkflowId workflowId,
        WorkflowLifecycleStatus status,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<WorkflowDefinition> SaveDefinitionAsync(
        WorkflowDefinitionSaveRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<WorkflowDefinition> ChangeDefinitionStatusAsync(
        WorkflowDefinitionStatusChangeRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<WorkflowDefinitionExportEnvelope?> ExportDefinitionAsync(
        WorkflowId workflowId,
        WorkflowVersionId? versionId = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<WorkflowDefinition> ImportDefinitionAsync(
        WorkflowDefinitionImportRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task DeleteDefinitionAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<WorkflowValidationResult> ValidateDefinitionAsync(
        WorkflowDefinition definition,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

internal sealed class WorkflowUsageFixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

internal sealed class WorkflowUsageCompletingBackend(
    WorkflowUsageObservation observation,
    DateTimeOffset completedAtUtc) : IWorkflowExecutionBackend
{
    public WorkflowRuntimeBackendDescriptor Descriptor { get; } = new(
        WorkflowRuntimeBackendKind.InProcess,
        "Usage test backend",
        IsDurable: false,
        SupportsStreaming: false,
        SupportsExternalRequests: false,
        SupportsDashboardObservability: false,
        OperationalNotes: string.Empty);

    public async Task<WorkflowBackendStartResult> StartAsync(
        WorkflowDefinition definition,
        WorkflowRunStartRequest request,
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        var progress = WorkflowNodeExecutionProgressScope.Current
            ?? throw new InvalidOperationException("Workflow progress observer is required.");
        await progress.RecordAsync(new WorkflowNodeExecutionProgress(
            definition.Id,
            definition.VersionId,
            RunId: null,
            observation.NodeId,
            WorkflowNodeExecutionProgressState.Completed,
            completedAtUtc)
        {
            ExecutorId = observation.ExecutorId,
            UsageObservations = [observation]
        }, cancellationToken);
        var run = new WorkflowRunSnapshot(
            runId,
            definition.Id,
            definition.VersionId,
            WorkflowRunState.Completed,
            WorkflowRuntimeBackendKind.InProcess,
            runId.ToString(),
            "Completed",
            completedAtUtc.AddSeconds(-1),
            completedAtUtc);
        return new WorkflowBackendStartResult(run, [], [], [])
        {
            UsageObservations = [observation]
        };
    }
}

internal sealed class WorkflowUsageFailingBackend(
    WorkflowUsageObservation observation) : IWorkflowExecutionBackend
{
    public WorkflowRuntimeBackendDescriptor Descriptor { get; } = new(
        WorkflowRuntimeBackendKind.InProcess,
        "Failing usage test backend",
        IsDurable: false,
        SupportsStreaming: false,
        SupportsExternalRequests: false,
        SupportsDashboardObservability: false,
        OperationalNotes: string.Empty);

    public Task<WorkflowBackendStartResult> StartAsync(
        WorkflowDefinition definition,
        WorkflowRunStartRequest request,
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
        => throw new WorkflowUsageObservationException(
            "Provider failed after activity.",
            new InvalidOperationException("Provider failed."),
            [observation]);
}

internal sealed class WorkflowUsageTestDbContextFactory(
    DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => new(options);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());
}
