using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Memory.Tests.Runtime;

public sealed class MemoryEndToEndObservabilityProofTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-06T03:45:00Z");
    private static readonly MemoryProviderInstanceId ProgrammingProviderId = MemoryProviderInstanceId.Parse("provider.regression.programming");
    private static readonly MemoryProviderInstanceId BusinessProviderId = MemoryProviderInstanceId.Parse("provider.regression.business");
    private static readonly MemoryProviderInstanceId FailingProviderId = MemoryProviderInstanceId.Parse("provider.regression.failing");

    [Fact]
    public async Task E2E_observability_proof_exercises_runtime_workers_ledgers_and_zero_provider_contracts()
    {
        var driver = new EndToEndProofMemoryProviderDriver();
        using var rootProvider = CreateServiceProvider(driver);
        using var scope = rootProvider.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var runtime = serviceProvider.GetRequiredService<IMemoryRuntimeService>();
        var handler = serviceProvider.GetRequiredService<IMemoryOperationHandler>();

        var zeroProviderRuntime = await runtime.ExecuteContextQueryAsync(
            CreateRuntimeRequest(
                MemoryCapabilityIds.ContextQuerySync,
                MemoryProviderSelectionContext.None),
            CreateQueryRequest("zero-provider check", MemoryCapabilityIds.ContextQuerySync));
        var zeroProviderTool = await handler.ExecuteQueryAsync(MemoryOperationRequestBuilder.Query(
            MemoryOperationCaller.Tool("regression.zero-provider.tool", CreateRequester()),
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQuerySync),
            CreateQueryRequest("zero-provider tool", MemoryCapabilityIds.ContextQuerySync),
            CreateRetentionPolicy()));

        Assert.Equal(MemoryProviderSelectionStatus.NoProviderConfigured, zeroProviderRuntime.Selection.Status);
        Assert.Equal(MemoryOperationHandlerStatus.NoProviderConfigured, zeroProviderTool.Status);
        Assert.False(zeroProviderRuntime.DriverDispatchAttempted);
        Assert.False(zeroProviderTool.DriverDispatchAttempted);
        Assert.Equal(0, driver.QueryCalls);

        await SeedProfilesAsync(serviceProvider);

        var programmingResult = await runtime.ExecuteContextQueryAsync(
            CreateRuntimeRequest(
                MemoryCapabilityIds.ContextQuerySync,
                CreateSelectionContext("agent-programming", "developer"),
                CreateAssignedPolicy(MemoryCapabilityIds.ContextQuerySync, "developer", ProgrammingProviderId)),
            CreateQueryRequest("generic memory boundaries", MemoryCapabilityIds.ContextQuerySync));
        var businessAccepted = await runtime.ExecuteContextQueryAsync(
            CreateRuntimeRequest(
                MemoryCapabilityIds.ContextQueryAsync,
                CreateSelectionContext("agent-business", "business-analyst"),
                CreateAssignedPolicy(MemoryCapabilityIds.ContextQueryAsync, "business-analyst", BusinessProviderId)),
            CreateQueryRequest("customer renewal context", MemoryCapabilityIds.ContextQueryAsync));

        Assert.Equal(ProgrammingProviderId, programmingResult.Selection.SelectedProvider?.InstanceId);
        Assert.Equal(BusinessProviderId, businessAccepted.Selection.SelectedProvider?.InstanceId);
        Assert.NotNull(programmingResult.ContextPack?.FeedbackHandle);
        Assert.NotNull(businessAccepted.AcceptedOperation);

        Assert.IsType<FixedTimeProvider>(serviceProvider.GetRequiredService<TimeProvider>())
            .Advance(businessAccepted.AcceptedOperation.PollAfter);
        var operationWorkerResult = await serviceProvider.GetRequiredService<IMemoryAsyncOperationWorker>()
            .PollOperationsAsync();
        var completedBusinessOperation = await serviceProvider.GetRequiredService<IMemoryOperationLedgerStore>()
            .GetAsync(businessAccepted.OperationRecord!.OperationId);
        var statusResult = await handler.GetStatusAsync(MemoryOperationRequestBuilder.Status(
            MemoryOperationCaller.ApiEndpoint("regression.operation-status", CreateRequester()),
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.OperationStatus),
            new MemoryOperationStatusRequest(businessAccepted.OperationRecord.OperationId),
            CreateRetentionPolicy()));

        Assert.Equal(1, operationWorkerResult.Completed);
        Assert.Equal(MemoryLedgerStatus.Completed, completedBusinessOperation?.Status);
        Assert.Equal(MemoryOperationHandlerStatus.Completed, statusResult.Status);

        var feedbackResult = await handler.SubmitFeedbackAsync(MemoryOperationRequestBuilder.Feedback(
            MemoryOperationCaller.UiAction("regression.feedback", CreateRequester()),
            CreateExplicitPolicy(MemoryCapabilityIds.FeedbackDelayed, BusinessProviderId),
            new MemoryFeedbackRequest(
                programmingResult.ContextPack!.ContextPackId,
                MemoryFeedbackOutcome.Useful,
                "regression delayed feedback proof",
                new MemoryEconomicImpact("USD", 25)),
            CreateRetentionPolicy()));
        var feedbackWorkerResult = await serviceProvider.GetRequiredService<IMemoryFeedbackWorker>()
            .DeliverPendingFeedbackAsync();

        Assert.Equal(MemoryOperationHandlerStatus.Accepted, feedbackResult.Status);
        Assert.Equal(1, feedbackWorkerResult.Completed);
        Assert.Equal(1, driver.FeedbackDeliveryCalls);

        var manualIngestionResult = await serviceProvider.GetRequiredService<ManualMemorySourceIngestionService>()
            .EnqueueAsync(new ManualMemorySourceIngestionRequest(
                BusinessProviderId,
                ManualMemorySourcePayload.Text(
                    "regression manual note",
                    "Generic source snapshots stay outside provider EF boundaries.",
                    "proof",
                    ["regression", "manual"]),
                RequestedBy: "user-regression",
                CreateRequester(),
                CreateRetentionPolicy()));
        var sourceRecords = await serviceProvider.GetRequiredService<IMemorySourceRequestLedgerStore>()
            .ListByProviderAsync(BusinessProviderId);

        Assert.NotEqual(Guid.Empty, manualIngestionResult.JobId);
        Assert.Contains(sourceRecords, record =>
            record.OperationId == manualIngestionResult.OperationId &&
            record.Status == MemorySourceIngestionJobStatus.SnapshotCaptured);

        driver.ProviderEvents.Add(CreateProviderEvent());
        driver.ProviderEvents.Add(driver.ProviderEvents[0]);
        var eventWorker = serviceProvider.GetRequiredService<IMemoryProviderEventWorker>();
        var eventPollResult = await eventWorker.PollProviderEventsAsync();
        var pendingInboxBeforeDrain = await serviceProvider.GetRequiredService<IMemoryEventLedgerStore>()
            .ListPendingInboxAsync(BusinessProviderId);
        var drainInboxResult = await eventWorker.DrainInboxAsync();
        var pendingOutboxBeforeDrain = await serviceProvider.GetRequiredService<IMemoryEventLedgerStore>()
            .ListPendingOutboxAsync(BusinessProviderId);
        var drainOutboxResult = await eventWorker.DrainOutboxAsync();

        Assert.Equal(1, eventPollResult.Enqueued);
        Assert.Equal(1, eventPollResult.Duplicates);
        Assert.Single(pendingInboxBeforeDrain);
        Assert.Equal(1, drainInboxResult.Completed);
        Assert.Single(pendingOutboxBeforeDrain);
        Assert.Equal(1, drainOutboxResult.Completed);
        Assert.Equal(1, driver.OutboxDeliveryCalls);

        var failingResult = await runtime.ExecuteContextQueryAsync(
            CreateRuntimeRequest(
                MemoryCapabilityIds.ContextQuerySync,
                MemoryProviderSelectionContext.None,
                CreateExplicitPolicy(MemoryCapabilityIds.ContextQuerySync, FailingProviderId)),
            CreateQueryRequest("provider error state", MemoryCapabilityIds.ContextQuerySync));
        var businessHealth = await driver.GetHealthAsync((await serviceProvider.GetRequiredService<IMemoryProviderProfileStore>()
            .ListAsync()).Single(profile => profile.InstanceId == BusinessProviderId));
        var failingHealth = await driver.GetHealthAsync((await serviceProvider.GetRequiredService<IMemoryProviderProfileStore>()
            .ListAsync()).Single(profile => profile.InstanceId == FailingProviderId));

        Assert.Equal(MemoryLedgerStatus.Failed, failingResult.OperationRecord?.Status);
        Assert.Equal(MemoryProviderHealthStatus.Reachable, businessHealth.Status);
        Assert.Equal(MemoryProviderHealthStatus.Degraded, failingHealth.Status);

        var snapshotPath = await WriteProofSnapshotAsync(
            serviceProvider,
            zeroProviderRuntime,
            zeroProviderTool,
            operationWorkerResult,
            feedbackWorkerResult,
            eventPollResult,
            drainInboxResult,
            drainOutboxResult,
            businessHealth,
            failingHealth,
            driver);

        Assert.True(File.Exists(snapshotPath));
    }

    private static ServiceProvider CreateServiceProvider(EndToEndProofMemoryProviderDriver driver)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"memory-regression-e2e-{Guid.NewGuid():N}"));
        services.AddSingleton(driver);
        services.AddSingleton<IMemoryProviderDriver>(provider =>
            provider.GetRequiredService<EndToEndProofMemoryProviderDriver>());
        services.AddSingleton<IMemoryProviderOperationStatusDriver>(provider =>
            provider.GetRequiredService<EndToEndProofMemoryProviderDriver>());
        services.AddSingleton<IMemoryProviderFeedbackDeliveryDriver>(provider =>
            provider.GetRequiredService<EndToEndProofMemoryProviderDriver>());
        services.AddSingleton<IMemoryProviderEventPollDriver>(provider =>
            provider.GetRequiredService<EndToEndProofMemoryProviderDriver>());
        services.AddSingleton<IMemoryProviderEventOutboxDriver>(provider =>
            provider.GetRequiredService<EndToEndProofMemoryProviderDriver>());
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

    private static async Task SeedProfilesAsync(IServiceProvider serviceProvider)
    {
        var store = serviceProvider.GetRequiredService<IMemoryProviderProfileStore>();
        await store.UpsertAsync(CreateProviderProfile(
            ProgrammingProviderId,
            "regression programming memory",
            MemoryProviderHealthState.Healthy,
            ["developer"],
            [
                MemoryCapabilityIds.ContextQuerySync,
                MemoryCapabilityIds.FeedbackImmediate
            ]), Now);
        await store.UpsertAsync(CreateProviderProfile(
            BusinessProviderId,
            "regression business memory",
            MemoryProviderHealthState.Healthy,
            ["business-analyst"],
            [
                MemoryCapabilityIds.ContextQueryAsync,
                MemoryCapabilityIds.OperationStatus,
                MemoryCapabilityIds.FeedbackDelayed,
                MemoryCapabilityIds.IngestionSnapshot,
                MemoryCapabilityIds.EventsHostPoll,
                MemoryCapabilityIds.EventsProviderPush
            ]), Now);
        await store.UpsertAsync(CreateProviderProfile(
            FailingProviderId,
            "regression failing memory",
            MemoryProviderHealthState.Degraded,
            ["failing"],
            [MemoryCapabilityIds.ContextQuerySync]), Now);
    }

    private static MemoryProviderProfile CreateProviderProfile(
        MemoryProviderInstanceId providerInstanceId,
        string displayName,
        MemoryProviderHealthState healthState,
        IReadOnlyList<string> selectionTags,
        IReadOnlyList<MemoryCapabilityId> capabilities)
    {
        return new MemoryProviderProfile(
            providerInstanceId,
            displayName,
            MemoryProviderDriverKind.Mock,
            IsEnabled: true,
            healthState,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            selectionTags,
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse("memory.mock.regression"),
                MemoryProtocolVersion.Current,
                capabilities
                    .Select(capability => new MemoryCapabilityDescriptor(capability, Version: "1", Supported: true))
                    .ToArray(),
                new MemoryProviderInteractionSupport(
                    capabilities.Contains(MemoryCapabilityIds.ContextQuerySync),
                    capabilities.Contains(MemoryCapabilityIds.ContextQueryAsync),
                    capabilities.Contains(MemoryCapabilityIds.IngestionSnapshot) ||
                    capabilities.Contains(MemoryCapabilityIds.IngestionProviderRequestedSource),
                    capabilities.Contains(MemoryCapabilityIds.FeedbackImmediate) ||
                    capabilities.Contains(MemoryCapabilityIds.FeedbackDelayed),
                    capabilities.Contains(MemoryCapabilityIds.EventsHostPoll) ||
                    capabilities.Contains(MemoryCapabilityIds.EventsProviderPush)),
                UiSurfaces: [],
                MemoryProviderLimits.Default,
                MemoryExtensionData.Empty));
    }

    private static MemoryRuntimeOperationRequest CreateRuntimeRequest(
        MemoryCapabilityId capability,
        MemoryProviderSelectionContext selectionContext,
        MemoryProviderSelectionPolicy? selectionPolicy = null)
    {
        return new MemoryRuntimeOperationRequest(
            selectionPolicy ?? MemoryProviderSelectionPolicy.RequireCapability(capability),
            selectionContext,
            MemoryOperationKind.ContextQuery,
            CreateRequester(),
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            [MemorySourceSnapshotId.Parse("snapshot.regression.manual")],
            CreateRetentionPolicy());
    }

    private static MemoryContextQueryRequest CreateQueryRequest(
        string query,
        MemoryCapabilityId capability)
    {
        return new MemoryContextQueryRequest(
            query,
            [capability],
            new MemorySourceProvenance(
                MemorySourceSnapshotId.Parse("snapshot.regression.manual"),
                SourceModule: nameof(MemorySourceKind.ManualPayload),
                SourceRecordIds: ["manual-regression"],
                Citations: ["regression manual proof"]));
    }

    private static MemoryProviderSelectionPolicy CreateAssignedPolicy(
        MemoryCapabilityId capability,
        string agentRole,
        MemoryProviderInstanceId providerInstanceId)
    {
        return MemoryProviderSelectionPolicy.RequireCapability(capability) with
        {
            Assignments =
            [
                new MemoryProviderAssignment(
                    MemoryProviderAssignmentScope.AgentRole,
                    agentRole,
                    providerInstanceId)
            ]
        };
    }

    private static MemoryProviderSelectionPolicy CreateExplicitPolicy(
        MemoryCapabilityId capability,
        MemoryProviderInstanceId providerInstanceId)
    {
        return MemoryProviderSelectionPolicy.RequireCapability(capability) with
        {
            ExplicitProviderId = providerInstanceId
        };
    }

    private static MemoryProviderSelectionContext CreateSelectionContext(
        string agentId,
        string agentRole)
    {
        return new MemoryProviderSelectionContext(
            agentId,
            agentRole,
            WorkflowId: "workflow-regression",
            WorkflowNodeId: "node-regression",
            ProcessId: "process-regression");
    }

    private static MemoryLedgerRequester CreateRequester()
    {
        return new MemoryLedgerRequester(
            RequesterId: "user-regression",
            AgentId: "agent-regression",
            AgentRole: "developer",
            SessionId: "session-regression",
            WorkflowId: "workflow-regression",
            WorkflowNodeId: "node-regression",
            ProcessId: "process-regression",
            ProcessStepId: "step-regression");
    }

    private static MemoryLedgerRetentionPolicy CreateRetentionPolicy()
    {
        return MemoryLedgerRetentionPolicy.Expiring(Now.AddDays(7), Now.AddDays(30));
    }

    private static MemoryProviderEvent CreateProviderEvent()
    {
        return new MemoryProviderEvent(
            MemoryProviderEventId.New(),
            MemoryProviderEventKind.SourceRequest,
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            "regression provider requested a source refresh.",
            MemoryPayload.FromText("source refresh requested"));
    }

    private static async Task<string> WriteProofSnapshotAsync(
        IServiceProvider serviceProvider,
        MemoryRuntimeOperationResult zeroProviderRuntime,
        MemoryOperationHandlerResult<MemoryContextPack> zeroProviderTool,
        MemoryAsyncWorkerRunResult operationWorkerResult,
        MemoryAsyncWorkerRunResult feedbackWorkerResult,
        MemoryAsyncWorkerRunResult eventPollResult,
        MemoryAsyncWorkerRunResult drainInboxResult,
        MemoryAsyncWorkerRunResult drainOutboxResult,
        MemoryProviderHealth businessHealth,
        MemoryProviderHealth failingHealth,
        EndToEndProofMemoryProviderDriver driver)
    {
        var repositoryRoot = FindRepoRoot();
        var artifactDirectory = Path.Combine(
            repositoryRoot,
            "codex",
            "bundles",
            "candoitall-memory-provider-extraction-bundle",
            "proof",
            "regression",
            "artifacts");
        Directory.CreateDirectory(artifactDirectory);

        await using var dbContext = await serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync();
        var operations = await dbContext.Set<MemoryOperationLedgerEntity>()
            .AsNoTracking()
            .OrderBy(record => record.CreatedAtUtc)
            .ToArrayAsync();
        var feedback = await dbContext.Set<MemoryFeedbackLedgerEntity>()
            .AsNoTracking()
            .OrderBy(record => record.CreatedAtUtc)
            .ToArrayAsync();
        var inbox = await dbContext.Set<MemoryEventInboxLedgerEntity>()
            .AsNoTracking()
            .OrderBy(record => record.ReceivedAtUtc)
            .ToArrayAsync();
        var outbox = await dbContext.Set<MemoryEventOutboxLedgerEntity>()
            .AsNoTracking()
            .OrderBy(record => record.CreatedAtUtc)
            .ToArrayAsync();
        var sourceRequests = await dbContext.Set<MemorySourceRequestLedgerEntity>()
            .AsNoTracking()
            .OrderBy(record => record.CreatedAtUtc)
            .ToArrayAsync();
        var providers = await serviceProvider.GetRequiredService<IMemoryProviderProfileStore>()
            .ListAsync();

        var snapshot = new
        {
            Scenario = "regression end-to-end regression and observability proof",
            GeneratedAtUtc = Now,
            ZeroProvider = new
            {
                RuntimeStatus = zeroProviderRuntime.Selection.Status.ToString(),
                RuntimeDispatchAttempted = zeroProviderRuntime.DriverDispatchAttempted,
                ToolStatus = zeroProviderTool.Status.ToString(),
                ToolDispatchAttempted = zeroProviderTool.DriverDispatchAttempted
            },
            Providers = providers.Select(profile => new
            {
                InstanceId = profile.InstanceId.Value,
                profile.DisplayName,
                HealthState = profile.HealthState.ToString(),
                Capabilities = profile.Manifest.Capabilities.Select(capability => capability.Id.Value).ToArray()
            }),
            DriverCalls = new
            {
                driver.QueryCalls,
                driver.OperationStatusCalls,
                driver.FeedbackDeliveryCalls,
                driver.EventPollCalls,
                driver.OutboxDeliveryCalls,
                driver.HealthCalls,
                QueryProviderIds = driver.QueryProviderIds.ToArray()
            },
            Workers = new
            {
                Operation = WorkerSummary(operationWorkerResult),
                Feedback = WorkerSummary(feedbackWorkerResult),
                EventPoll = WorkerSummary(eventPollResult),
                EventInbox = WorkerSummary(drainInboxResult),
                EventOutbox = WorkerSummary(drainOutboxResult)
            },
            Health = new
            {
                Business = businessHealth.Status.ToString(),
                Failing = failingHealth.Status.ToString(),
                FailingError = failingHealth.LastErrorCategory
            },
            Ledgers = new
            {
                Operations = operations.Select(row =>
                {
                    var record = row.ToRecord();
                    return new
                    {
                        OperationId = record.OperationId.Value,
                        Provider = record.ProviderInstanceId.Value,
                        Capability = record.RequestedCapability.Value,
                        Kind = record.OperationKind.ToString(),
                        Status = record.Status.ToString(),
                        Caller = record.Extensions.GetMemoryOperationCaller()?.Kind.ToString(),
                        SourceSnapshotIds = record.SourceSnapshotIds.Select(snapshotId => snapshotId.Value).ToArray(),
                        record.StatusReason
                    };
                }),
                Feedback = feedback.Select(row =>
                {
                    var record = row.ToRecord();
                    return new
                    {
                        FeedbackRecordId = record.FeedbackRecordId.Value,
                        Provider = record.ProviderInstanceId.Value,
                        Stage = record.Stage.ToString(),
                        Outcome = record.Outcome.ToString(),
                        Status = record.Status.ToString(),
                        MatchState = record.MatchState.ToString()
                    };
                }),
                EventInbox = inbox.Select(row =>
                {
                    var record = row.ToRecord();
                    return new
                    {
                        InboxRecordId = record.InboxRecordId.Value,
                        Provider = record.ProviderInstanceId.Value,
                        EventKind = record.EventKind.ToString(),
                        Status = record.Status.ToString(),
                        record.StatusReason
                    };
                }),
                EventOutbox = outbox.Select(row =>
                {
                    var record = row.ToRecord();
                    return new
                    {
                        OutboxRecordId = record.OutboxRecordId.Value,
                        Provider = record.ProviderInstanceId.Value,
                        Status = record.Status.ToString(),
                        record.PayloadKind
                    };
                }),
                SourceRequests = sourceRequests.Select(row =>
                {
                    var record = row.ToRecord();
                    return new
                    {
                        record.JobId,
                        Provider = record.ProviderInstanceId.Value,
                        Status = record.Status.ToString(),
                        CapturedSnapshotId = record.CapturedSnapshotId?.Value,
                        OperationId = record.OperationId?.Value
                    };
                })
            }
        };

        var snapshotPath = Path.Combine(artifactDirectory, "memory-e2e-ledger-snapshot.json");
        await File.WriteAllTextAsync(
            snapshotPath,
            JsonSerializer.Serialize(
                snapshot,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    WriteIndented = true
                }));
        return snapshotPath;
    }

    private static object WorkerSummary(MemoryAsyncWorkerRunResult result)
    {
        return new
        {
            result.Scanned,
            result.Completed,
            result.Retried,
            result.DeadLettered,
            result.TimedOut,
            result.Cancelled,
            result.Enqueued,
            result.Duplicates,
            result.LoopRejected,
            result.IpfsUnpinRequests,
            Diagnostics = result.Diagnostics.ToArray()
        };
    }

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

        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }

        public void Advance(TimeSpan elapsed)
        {
            now += elapsed;
        }
    }

    private sealed class EndToEndProofMemoryProviderDriver :
        IMemoryProviderDriver,
        IMemoryProviderOperationStatusDriver,
        IMemoryProviderFeedbackDeliveryDriver,
        IMemoryProviderEventPollDriver,
        IMemoryProviderEventOutboxDriver,
        IMemoryProviderHealthDriver
    {
        public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Mock;

        public int QueryCalls { get; private set; }

        public int OperationStatusCalls { get; private set; }

        public int FeedbackDeliveryCalls { get; private set; }

        public int EventPollCalls { get; private set; }

        public int OutboxDeliveryCalls { get; private set; }

        public int HealthCalls { get; private set; }

        public List<string> QueryProviderIds { get; } = [];

        public List<MemoryProviderEvent> ProviderEvents { get; } = [];

        public Task<MemoryProviderDriverResult> ExecuteContextQueryAsync(
            MemoryProviderProfile provider,
            MemoryOperationRecord operation,
            MemoryContextQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            QueryCalls++;
            QueryProviderIds.Add(provider.InstanceId.Value);

            if (provider.InstanceId == FailingProviderId)
            {
                return Task.FromResult(MemoryProviderDriverResult.Failed(
                    MemoryProviderDriverResultKind.ProviderError,
                    "regression provider returned a deliberate error state."));
            }

            if (provider.InstanceId == BusinessProviderId)
            {
                return Task.FromResult(MemoryProviderDriverResult.Accepted(
                    new MemoryOperationAccepted(
                        operation.OperationId,
                        $"/memory/providers/{provider.InstanceId.Value}/operations/{operation.OperationId.Value:D}",
                        operation.CreatedAtUtc.AddMinutes(5),
                        TimeSpan.FromMilliseconds(10),
                        CallbackAvailable: false),
                    "regression business provider accepted async context query."));
            }

            return Task.FromResult(MemoryProviderDriverResult.ContextPackResult(
                new MemoryContextPack(
                    MemoryContextPackId.New(),
                    $"regression context from {provider.InstanceId.Value}: {request.Query}",
                    [
                        new MemoryContextSection(
                            "Generic provider boundary",
                            "The query was handled through the generic memory runtime.",
                            [new MemoryCitation("memory://regression/manual", "regression manual source")],
                            0.96m)
                    ],
                    Warnings: [],
                    ProviderConfidence: 0.96m,
                    FeedbackHandle: null),
                "regression programming provider returned sync context."));
        }

        public Task<MemoryProviderOperationPollResult> PollOperationAsync(
            MemoryProviderProfile provider,
            MemoryOperationRecord operation,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OperationStatusCalls++;
            return Task.FromResult(MemoryProviderOperationPollResult.FromResult(
                new MemoryOperationResult(
                    operation.OperationId,
                    MemoryOperationStatus.Succeeded,
                    MemoryPayload.FromText("regression async business context completed."),
                    Warnings: [],
                    FeedbackHandles: [],
                    SourceRefs: ["memory://regression/business"]),
                "regression async operation completed through status polling."));
        }

        public Task<MemoryProviderQueueDispatchResult> DeliverFeedbackAsync(
            MemoryProviderProfile provider,
            MemoryFeedbackRecord feedback,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FeedbackDeliveryCalls++;
            return Task.FromResult(MemoryProviderQueueDispatchResult.Succeeded(
                "regression feedback delivered to provider."));
        }

        public Task<MemoryProviderEventPollResult> PollEventsAsync(
            MemoryProviderProfile provider,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EventPollCalls++;
            return Task.FromResult(MemoryProviderEventPollResult.FromEvents(
                ProviderEvents,
                "regression provider event poll returned events."));
        }

        public Task<MemoryProviderQueueDispatchResult> DeliverOutboxAsync(
            MemoryProviderProfile provider,
            MemoryEventOutboxRecord outbox,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OutboxDeliveryCalls++;
            return Task.FromResult(MemoryProviderQueueDispatchResult.Succeeded(
                "regression event acknowledgement delivered."));
        }

        public Task<MemoryProviderHealth> GetHealthAsync(
            MemoryProviderProfile provider,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HealthCalls++;
            return Task.FromResult(provider.InstanceId == FailingProviderId
                ? new MemoryProviderHealth(
                    MemoryProviderHealthStatus.Degraded,
                    "regression-deliberate-provider-error",
                    provider.Manifest)
                : new MemoryProviderHealth(
                    MemoryProviderHealthStatus.Reachable,
                    LastErrorCategory: null,
                    provider.Manifest));
        }
    }
}
