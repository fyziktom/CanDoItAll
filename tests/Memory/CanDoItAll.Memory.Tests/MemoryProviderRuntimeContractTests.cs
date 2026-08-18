using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Memory.Tests.Contracts;

public sealed class MemoryProviderRuntimeContractTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-06T02:30:00Z");

    [Fact]
    public void Generic_mock_profiles_are_explicit_and_cover_required_shapes()
    {
        var profiles = new[]
        {
            GenericMockMemoryProviderFixture.ImmediateContextProfile(),
            GenericMockMemoryProviderFixture.DelayedContextProfile(),
            GenericMockMemoryProviderFixture.EventfulProfile(),
            GenericMockMemoryProviderFixture.UiSurfaceProfile(),
            GenericMockMemoryProviderFixture.FailingProfile()
        };

        Assert.All(profiles, profile =>
        {
            Assert.Equal(MemoryProviderDriverKind.Mock, profile.DriverKind);
            Assert.Equal(MemoryProviderFallbackBehavior.DenyImplicitFallback, profile.DefaultPolicy.FallbackBehavior);
            Assert.Equal(MemoryProviderKind.Parse("memory.mock"), profile.Manifest.ProviderKind);
        });
        Assert.Contains(profiles[0].Manifest.Capabilities, capability => capability.Id == MemoryCapabilityIds.ContextQuerySync);
        Assert.Contains(profiles[1].Manifest.Capabilities, capability => capability.Id == MemoryCapabilityIds.OperationStatus);
        Assert.True(profiles[2].Manifest.InteractionSupport.SupportsProviderEvents);
        Assert.Equal(2, profiles[3].Manifest.UiSurfaces.Count);
        Assert.DoesNotContain(profiles[3].Manifest.UiSurfaces, surface =>
            (surface.ComponentKey ?? string.Empty).Contains("native.cognitiveMemory", StringComparison.Ordinal));
    }

    [Fact]
    public void Explicit_mock_providers_select_by_role_without_fallback()
    {
        var developer = GenericMockMemoryProviderFixture.ImmediateContextProfile(
            "provider.mock.developer",
            tags: ["developer"]);
        var analyst = GenericMockMemoryProviderFixture.ImmediateContextProfile(
            "provider.mock.analyst",
            tags: ["business-analyst"]);
        var disabled = GenericMockMemoryProviderFixture.ImmediateContextProfile(
            "provider.mock.disabled",
            isEnabled: false);
        var registry = new InMemoryMemoryProviderRegistry([developer, analyst, disabled]);
        var policy = MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQuerySync) with
        {
            Assignments =
            [
                new MemoryProviderAssignment(MemoryProviderAssignmentScope.AgentRole, "developer", developer.InstanceId),
                new MemoryProviderAssignment(MemoryProviderAssignmentScope.AgentRole, "business-analyst", analyst.InstanceId)
            ]
        };

        var developerResult = registry.SelectProvider(
            policy,
            new MemoryProviderSelectionContext(
                AgentId: "agent-dev",
                AgentRole: "developer",
                WorkflowId: null,
                WorkflowNodeId: null,
                ProcessId: null));
        var analystResult = registry.SelectProvider(
            policy,
            new MemoryProviderSelectionContext(
                AgentId: "agent-ba",
                AgentRole: "business-analyst",
                WorkflowId: null,
                WorkflowNodeId: null,
                ProcessId: null));
        var disabledResult = registry.SelectProvider(
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQuerySync) with
            {
                ExplicitProviderId = disabled.InstanceId
            },
            MemoryProviderSelectionContext.None);

        Assert.Equal(developer.InstanceId, developerResult.SelectedProvider?.InstanceId);
        Assert.Equal(analyst.InstanceId, analystResult.SelectedProvider?.InstanceId);
        Assert.Equal(MemoryProviderSelectionStatus.ProviderDisabled, disabledResult.Status);
        Assert.False(disabledResult.DispatchAllowed);
        Assert.Null(disabledResult.SelectedProvider);
    }

    [Fact]
    public async Task Immediate_mock_provider_dispatches_through_generic_runtime()
    {
        var fixture = new GenericMockMemoryProviderFixture();
        using var rootProvider = CreateServiceProvider(fixture);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        await UpsertProfileAsync(provider, GenericMockMemoryProviderFixture.ImmediateContextProfile());

        var result = await provider.GetRequiredService<IMemoryRuntimeService>()
            .ExecuteContextQueryAsync(
                CreateRuntimeRequest(MemoryCapabilityIds.ContextQuerySync),
                CreateQueryRequest(MemoryCapabilityIds.ContextQuerySync));

        Assert.Equal(MemoryProviderSelectionStatus.Selected, result.Selection.Status);
        Assert.True(result.DriverDispatchAttempted);
        Assert.Equal(1, fixture.QueryCalls);
        Assert.Equal(MemoryLedgerStatus.Completed, result.OperationRecord?.Status);
        Assert.NotNull(result.ContextPack?.FeedbackHandle);
        Assert.Contains("provider.mock.immediate", result.ContextPack?.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delayed_mock_provider_completes_status_poll_and_delivers_feedback()
    {
        var fixture = new GenericMockMemoryProviderFixture(GenericMockMemoryQueryMode.AcceptedOperation);
        fixture.CompleteNextOperationWithPayload("delayed context ready");
        using var rootProvider = CreateServiceProvider(fixture);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        await UpsertProfileAsync(provider, GenericMockMemoryProviderFixture.DelayedContextProfile());

        var runtimeResult = await provider.GetRequiredService<IMemoryRuntimeService>()
            .ExecuteContextQueryAsync(
                CreateRuntimeRequest(
                    MemoryCapabilityIds.ContextQueryAsync,
                    MemoryProviderInstanceId.Parse("provider.mock.delayed")),
                CreateQueryRequest(MemoryCapabilityIds.ContextQueryAsync));
        Assert.IsType<FixedTimeProvider>(provider.GetRequiredService<TimeProvider>())
            .Advance(runtimeResult.AcceptedOperation!.PollAfter);
        var workerResult = await provider.GetRequiredService<IMemoryAsyncOperationWorker>()
            .PollOperationsAsync();
        var persisted = await provider.GetRequiredService<IMemoryOperationLedgerStore>()
            .GetAsync(runtimeResult.OperationRecord!.OperationId);
        var feedbackResult = await provider.GetRequiredService<IMemoryOperationHandler>()
            .SubmitFeedbackAsync(MemoryOperationRequestBuilder.Feedback(
                CreateCaller(),
                MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.FeedbackDelayed) with
                {
                    ExplicitProviderId = MemoryProviderInstanceId.Parse("provider.mock.delayed")
                },
                new MemoryFeedbackRequest(
                    MemoryContextPackId.New(),
                    MemoryFeedbackOutcome.Useful,
                    Comment: "useful delayed result",
                    EconomicImpact: null),
                CreateRetentionPolicy()));
        var feedbackWorkerResult = await provider.GetRequiredService<IMemoryFeedbackWorker>()
            .DeliverPendingFeedbackAsync();

        Assert.Equal(MemoryOperationHandlerStatus.Accepted, ToHandlerStatus(runtimeResult.AcceptedOperation));
        Assert.Equal(1, workerResult.Completed);
        Assert.Equal(MemoryLedgerStatus.Completed, persisted?.Status);
        Assert.Equal(1, fixture.OperationStatusCalls);
        Assert.Equal(MemoryOperationHandlerStatus.Accepted, feedbackResult.Status);
        Assert.Equal(1, feedbackWorkerResult.Completed);
        Assert.Equal(1, fixture.FeedbackDeliveryCalls);
    }

    [Fact]
    public async Task Eventful_mock_provider_dedupes_events_and_drains_ack_outbox()
    {
        var fixture = new GenericMockMemoryProviderFixture();
        var providerEvent = GenericMockMemoryProviderFixture.CreateProviderEvent();
        fixture.EnqueueEvents(providerEvent, providerEvent);
        using var rootProvider = CreateServiceProvider(fixture);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        await UpsertProfileAsync(provider, GenericMockMemoryProviderFixture.EventfulProfile());

        var worker = provider.GetRequiredService<IMemoryProviderEventWorker>();
        var pollResult = await worker.PollProviderEventsAsync();
        var drainInboxResult = await worker.DrainInboxAsync();
        var drainOutboxResult = await worker.DrainOutboxAsync();

        Assert.Equal(1, pollResult.Enqueued);
        Assert.Equal(1, pollResult.Duplicates);
        Assert.Equal(1, drainInboxResult.Completed);
        Assert.Equal(1, drainInboxResult.Enqueued);
        Assert.Equal(1, drainOutboxResult.Completed);
        Assert.Equal(1, fixture.EventPollCalls);
        Assert.Equal(1, fixture.OutboxDeliveryCalls);
    }

    [Fact]
    public void Generic_memory_tests_do_not_import_native_memory_or_qdrant_modules()
    {
        var forbiddenImports = new[]
        {
            "CanDoItAll.Modules.CognitiveMemory",
            "CognitiveMemoryModuleAssemblyMarker",
            "AddCognitiveMemoryModule",
            "CanDoItAll.AgentFramework.Rag.Qdrant",
            "CanDoItAll.AgentFramework.SemanticCompletion.Driver"
        };
        var testRoot = Path.Combine(RepoRoot, "tests", "Memory", "CanDoItAll.Memory.Tests");
        var sourceImportViolations = Directory.EnumerateFiles(testRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new
                {
                    Path = path,
                    LineNumber = index + 1,
                    Line = line.Trim()
                }))
            .Where(candidate => candidate.Line.StartsWith("using ", StringComparison.Ordinal))
            .SelectMany(candidate => forbiddenImports
                .Where(pattern => candidate.Line.Contains(pattern, StringComparison.Ordinal))
                .Select(pattern => $"{Path.GetRelativePath(RepoRoot, candidate.Path)}:{candidate.LineNumber} imports {pattern}"))
            .ToArray();
        var projectReferenceViolations = File.ReadLines(Path.Combine(testRoot, "CanDoItAll.Memory.Tests.csproj"))
            .Select((line, index) => new
            {
                LineNumber = index + 1,
                Line = line
            })
            .SelectMany(candidate => forbiddenImports
                .Where(pattern => candidate.Line.Contains(pattern, StringComparison.Ordinal))
                .Select(pattern => $"CanDoItAll.Memory.Tests.csproj:{candidate.LineNumber} references {pattern}"))
            .ToArray();

        Assert.Empty(sourceImportViolations.Concat(projectReferenceViolations));
    }

    private static ServiceProvider CreateServiceProvider(GenericMockMemoryProviderFixture fixture)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"memory-suite-rebalance-{Guid.NewGuid():N}"));
        services.AddSingleton(fixture);
        services.AddSingleton<IMemoryProviderDriver>(provider =>
            provider.GetRequiredService<GenericMockMemoryProviderFixture>());
        services.AddSingleton<IMemoryProviderOperationStatusDriver>(provider =>
            provider.GetRequiredService<GenericMockMemoryProviderFixture>());
        services.AddSingleton<IMemoryProviderFeedbackDeliveryDriver>(provider =>
            provider.GetRequiredService<GenericMockMemoryProviderFixture>());
        services.AddSingleton<IMemoryProviderEventPollDriver>(provider =>
            provider.GetRequiredService<GenericMockMemoryProviderFixture>());
        services.AddSingleton<IMemoryProviderEventOutboxDriver>(provider =>
            provider.GetRequiredService<GenericMockMemoryProviderFixture>());
        services.AddGenericMemoryModule(options =>
        {
            options.WorkerOptions = MemoryAsyncWorkerOptions.Default with
            {
                MaxBatchSize = 10,
                MaxRetryAttempts = 2,
                PollingStaleAfter = TimeSpan.Zero
            };
        });
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task UpsertProfileAsync(
        IServiceProvider provider,
        MemoryProviderProfile profile)
    {
        await provider.GetRequiredService<IMemoryProviderProfileStore>()
            .UpsertAsync(profile, Now);
    }

    private static MemoryRuntimeOperationRequest CreateRuntimeRequest(
        MemoryCapabilityId capability,
        MemoryProviderInstanceId? providerInstanceId = null)
    {
        return new MemoryRuntimeOperationRequest(
            MemoryProviderSelectionPolicy.RequireCapability(capability) with
            {
                ExplicitProviderId = providerInstanceId ?? MemoryProviderInstanceId.Parse("provider.mock.immediate")
            },
            MemoryProviderSelectionContext.None,
            MemoryOperationKind.ContextQuery,
            CreateRequester(),
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            [MemorySourceSnapshotId.Parse("snapshot.project.1")],
            CreateRetentionPolicy());
    }

    private static MemoryContextQueryRequest CreateQueryRequest(
        MemoryCapabilityId capability)
    {
        return new MemoryContextQueryRequest(
            "payment integration",
            [capability],
            new MemorySourceProvenance(
                MemorySourceSnapshotId.Parse("snapshot.project.1"),
                SourceModule: nameof(MemorySourceKind.Project),
                SourceRecordIds: ["project-1"],
                Citations: ["Project 1"]));
    }

    private static MemoryOperationCaller CreateCaller()
    {
        return MemoryOperationCaller.UiAction("memory.test.rebalance", CreateRequester());
    }

    private static MemoryLedgerRequester CreateRequester()
    {
        return new MemoryLedgerRequester(
            RequesterId: "user-42",
            AgentId: "agent-dev",
            AgentRole: "developer",
            SessionId: "session-32",
            WorkflowId: "workflow-32",
            WorkflowNodeId: "node-32",
            ProcessId: "process-32",
            ProcessStepId: "step-32");
    }

    private static MemoryLedgerRetentionPolicy CreateRetentionPolicy()
    {
        return MemoryLedgerRetentionPolicy.Expiring(Now.AddDays(7), Now.AddDays(30));
    }

    private static MemoryOperationHandlerStatus ToHandlerStatus(MemoryOperationAccepted? acceptedOperation)
    {
        return acceptedOperation is null
            ? MemoryOperationHandlerStatus.Failed
            : MemoryOperationHandlerStatus.Accepted;
    }

    private static string RepoRoot => FindRepoRoot();

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing CanDoItAll.slnx.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset initialNow) : TimeProvider
    {
        private DateTimeOffset now = initialNow;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan elapsed)
        {
            now += elapsed;
        }
    }
}
