using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowLaunchServiceTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 12, 19, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(WorkflowLifecycleStatus.Draft)]
    [InlineData(WorkflowLifecycleStatus.Suspended)]
    public async Task LaunchAsync_ProductionNonActiveDefinition_RejectsBeforeRuntime(
        WorkflowLifecycleStatus status)
    {
        var definition = CreateDefinition(status: status);
        var fixture = CreateFixture([definition]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.LaunchAsync(
            CreateIntent(
                new WorkflowDefinitionSelection.ExactSavedVersion(definition.Id, definition.VersionId),
                WorkflowLaunchMode.Production)));

        Assert.Contains("Active", exception.Message, StringComparison.Ordinal);
        Assert.Empty(fixture.RunLauncher.Requests);
    }

    [Fact]
    public async Task LaunchAsync_DraftPreview_UsesDraftDirectlyAndBuildsResolvedRequest()
    {
        var draft = CreateDefinition(status: WorkflowLifecycleStatus.Draft);
        var fixture = CreateFixture([]);
        var origin = new WorkflowLaunchOrigin.Preview(CreateActor(), CreateCorrelation());

        var result = await fixture.Service.LaunchAsync(new WorkflowLaunchIntent(
            new WorkflowDefinitionSelection.DraftPreview(draft),
            WorkflowLaunchMode.Preview,
            origin,
            "  {\"prompt\":\"hello\"}  ",
            WorkflowLaunchCompletionPolicy.WaitForStopped,
            new WorkflowLaunchIdempotency.NotRequested())
        {
            RequestedBackend = WorkflowRuntimeBackendKind.InProcess
        });

        Assert.Empty(fixture.Catalog.Requests);
        var request = Assert.Single(fixture.RunLauncher.Requests);
        Assert.Same(draft, request.Definition);
        Assert.Equal("{\"prompt\":\"hello\"}", request.InputJson);
        Assert.Equal(WorkflowRuntimeBackendKind.InProcess, request.Backend.Kind);
        Assert.Equal(origin, request.Origin);
        Assert.Equal(FixedUtcNow, request.ResolvedAtUtc);
        Assert.Equal(request, result.ResolvedRequest);
        Assert.Single(fixture.Catalog.ValidationRequests);
    }

    [Fact]
    public async Task LaunchAsync_ExactAndLatestActiveSelections_ResolveTheirRequestedVersions()
    {
        var workflowId = WorkflowId.New();
        var first = CreateDefinition(workflowId, WorkflowVersionId.New(), WorkflowLifecycleStatus.Active);
        var latest = CreateDefinition(workflowId, WorkflowVersionId.New(), WorkflowLifecycleStatus.Active);
        var fixture = CreateFixture([first, latest]);

        var exactResult = await fixture.Service.LaunchAsync(CreateIntent(
            new WorkflowDefinitionSelection.ExactSavedVersion(workflowId, first.VersionId),
            WorkflowLaunchMode.Production));
        var latestResult = await fixture.Service.LaunchAsync(CreateIntent(
            new WorkflowDefinitionSelection.LatestActive(workflowId),
            WorkflowLaunchMode.Production));

        Assert.Equal(first.VersionId, exactResult.ResolvedRequest.Definition.VersionId);
        Assert.Equal(latest.VersionId, latestResult.ResolvedRequest.Definition.VersionId);
        Assert.Collection(
            fixture.Catalog.Requests,
            request => Assert.Null(request.VersionId),
            request => Assert.Equal(first.VersionId, request.VersionId));
        Assert.Collection(
            fixture.Catalog.LatestStatusRequests,
            request => Assert.Equal(WorkflowLifecycleStatus.Active, request.Status));
    }

    [Fact]
    public async Task LaunchAsync_LatestActiveSelectionWithNewerDraft_RunsNewestActiveVersion()
    {
        var workflowId = WorkflowId.New();
        var active = CreateDefinition(workflowId, WorkflowVersionId.New(), WorkflowLifecycleStatus.Active);
        var draft = CreateDefinition(workflowId, WorkflowVersionId.New(), WorkflowLifecycleStatus.Draft);
        var fixture = CreateFixture([active, draft]);

        var result = await fixture.Service.LaunchAsync(
            CreateIntent(
                new WorkflowDefinitionSelection.LatestActive(workflowId),
                WorkflowLaunchMode.Production));

        Assert.Equal(active.VersionId, result.ResolvedRequest.Definition.VersionId);
        Assert.NotEqual(draft.VersionId, result.ResolvedRequest.Definition.VersionId);
        Assert.Single(fixture.RunLauncher.Requests);
    }

    [Theory]
    [InlineData(WorkflowLifecycleStatus.Suspended)]
    [InlineData(WorkflowLifecycleStatus.Archived)]
    public async Task LaunchAsync_InactiveCurrentHead_BlocksHistoricalActiveSelections(
        WorkflowLifecycleStatus inactiveStatus)
    {
        var catalog = new InMemoryWorkflowCatalogService(new PassingWorkflowDefinitionValidator());
        var source = CreateDefinition(status: WorkflowLifecycleStatus.Active);
        var active = await catalog.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
            source.Id,
            ExpectedVersionId: null,
            source.Name,
            source.Description,
            WorkflowLifecycleStatus.Active,
            source.Graph,
            source.RuntimePolicy));
        var fixture = CreateLaunchFixture(catalog);

        var exactActive = await fixture.Service.LaunchAsync(CreateIntent(
            new WorkflowDefinitionSelection.ExactSavedVersion(active.Id, active.VersionId),
            WorkflowLaunchMode.Production));
        var latestActive = await fixture.Service.LaunchAsync(CreateIntent(
            new WorkflowDefinitionSelection.LatestActive(active.Id),
            WorkflowLaunchMode.Production));
        await catalog.ChangeDefinitionStatusAsync(new WorkflowDefinitionStatusChangeRequest(
            active.Id,
            active.VersionId,
            inactiveStatus));

        var latestException = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            fixture.Service.LaunchAsync(CreateIntent(
                new WorkflowDefinitionSelection.LatestActive(active.Id),
                WorkflowLaunchMode.Production)));
        var exactException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.LaunchAsync(CreateIntent(
                new WorkflowDefinitionSelection.ExactSavedVersion(active.Id, active.VersionId),
                WorkflowLaunchMode.Production)));

        Assert.Equal(active.VersionId, exactActive.ResolvedRequest.Definition.VersionId);
        Assert.Equal(active.VersionId, latestActive.ResolvedRequest.Definition.VersionId);
        Assert.Contains("Active", latestException.Message, StringComparison.Ordinal);
        Assert.Contains(inactiveStatus.ToString(), exactException.Message, StringComparison.Ordinal);
        Assert.Equal(2, fixture.RunLauncher.Requests.Count);
    }

    [Theory]
    [InlineData((int)WorkflowRuntimeBackendKind.DurableTask)]
    [InlineData(999)]
    public async Task LaunchAsync_UnsupportedRequestedBackend_DoesNotFallbackOrInvokeRuntime(int backendValue)
    {
        var definition = CreateDefinition(status: WorkflowLifecycleStatus.Active);
        var fixture = CreateFixture([definition]);
        var intent = CreateIntent(
            new WorkflowDefinitionSelection.ExactSavedVersion(definition.Id, definition.VersionId),
            WorkflowLaunchMode.Production) with
        {
            RequestedBackend = (WorkflowRuntimeBackendKind)backendValue
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.LaunchAsync(intent));

        Assert.Contains(backendValue.ToString(), exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.RunLauncher.Requests);
    }

    [Fact]
    public async Task LaunchAsync_MalformedInput_DoesNotResolveOrInvokeRuntime()
    {
        var definition = CreateDefinition(status: WorkflowLifecycleStatus.Active);
        var fixture = CreateFixture([definition]);
        var intent = CreateIntent(
            new WorkflowDefinitionSelection.ExactSavedVersion(definition.Id, definition.VersionId),
            WorkflowLaunchMode.Production) with
        {
            InputJson = "{not-json"
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.LaunchAsync(intent));

        Assert.Contains("valid JSON object", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Catalog.Requests);
        Assert.Empty(fixture.RunLauncher.Requests);
    }

    [Fact]
    public async Task LaunchAsync_EachTypedOrigin_PreservesConcreteLineageWithoutCallerSourceFields()
    {
        var definition = CreateDefinition(status: WorkflowLifecycleStatus.Active);
        var fixture = CreateFixture([definition]);
        var origins = new WorkflowLaunchOrigin[]
        {
            new WorkflowLaunchOrigin.Api(CreateActor(), CreateCorrelation()),
            new WorkflowLaunchOrigin.Preview(CreateActor(), CreateCorrelation()),
            new WorkflowLaunchOrigin.SchedulerPlanRun(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new WorkflowSchedulerFireId(Guid.NewGuid()),
                FixedUtcNow,
                CreateCorrelation()),
            new WorkflowLaunchOrigin.ProjectStructureNode(
                Guid.NewGuid(),
                new WorkflowProjectStructureNodeId("node-workflow-1"),
                CreateAgentActor(),
                CreateSession(),
                CreateCorrelation()),
            new WorkflowLaunchOrigin.AgentRuntimeInvocation(
                CreateAgentActor(),
                CreateSession(),
                "workflow-tool",
                CreateCorrelation()),
            new WorkflowLaunchOrigin.ProcessAssignment(
                Guid.NewGuid(),
                Guid.NewGuid(),
                CreateCorrelation())
        };

        foreach (var origin in origins)
        {
            var result = await fixture.Service.LaunchAsync(CreateIntent(
                new WorkflowDefinitionSelection.ExactSavedVersion(definition.Id, definition.VersionId),
                WorkflowLaunchMode.Production) with
            {
                Origin = origin
            });

            Assert.Equal(origin, result.ResolvedRequest.Origin);
            Assert.Equal(origin.Kind, result.ResolvedRequest.Origin.Kind);
        }

        Assert.Equal(
            [
                WorkflowLaunchOriginKind.Api,
                WorkflowLaunchOriginKind.Preview,
                WorkflowLaunchOriginKind.SchedulerPlanRun,
                WorkflowLaunchOriginKind.ProjectStructureNode,
                WorkflowLaunchOriginKind.AgentRuntimeInvocation,
                WorkflowLaunchOriginKind.ProcessAssignment
            ],
            fixture.RunLauncher.Requests.Select(request => request.Origin.Kind));
    }

    [Fact]
    public async Task LaunchAsync_InvalidCatalogValidation_IsHonoredOnceAndDoesNotInvokeRuntime()
    {
        var definition = CreateDefinition(status: WorkflowLifecycleStatus.Active);
        var fixture = CreateFixture([definition]);
        fixture.Catalog.Validation = new WorkflowValidationResult(
        [
            new WorkflowValidationIssue(
                WorkflowValidationIssueCode.InvalidWorkflowSettings,
                "Fixture definition is invalid.")
        ]);

        var exception = await Assert.ThrowsAsync<WorkflowLaunchValidationException>(() => fixture.Service.LaunchAsync(
            CreateIntent(
                new WorkflowDefinitionSelection.ExactSavedVersion(definition.Id, definition.VersionId),
                WorkflowLaunchMode.Production)));

        Assert.Equal(definition.Id, exception.WorkflowId);
        Assert.Equal(definition.VersionId, exception.VersionId);
        Assert.Same(fixture.Catalog.Validation, exception.Validation);
        Assert.Contains("Fixture definition is invalid", exception.Message, StringComparison.Ordinal);
        Assert.Single(fixture.Catalog.ValidationRequests);
        Assert.Empty(fixture.RunLauncher.Requests);
    }

    [Fact]
    public async Task LaunchAsync_CallerIdempotencyKey_ReplaysExistingRun()
    {
        var definition = CreateDefinition(status: WorkflowLifecycleStatus.Active);
        var fixture = CreateFixture([definition]);
        var idempotency = new WorkflowLaunchIdempotency.CallerSupplied(
            new WorkflowLaunchIdempotencyKey("scheduler-plan:42:occurrence:7"));
        var intent = CreateIntent(
            new WorkflowDefinitionSelection.ExactSavedVersion(definition.Id, definition.VersionId),
            WorkflowLaunchMode.Production) with
        {
            Idempotency = idempotency
        };

        var first = await fixture.Service.LaunchAsync(intent);
        var second = await fixture.Service.LaunchAsync(intent);

        Assert.Single(fixture.RunLauncher.Requests);
        Assert.Equal(first.Run.RunId, second.Run.RunId);
        Assert.Equal(idempotency, first.ResolvedRequest.Idempotency);
        Assert.Equal(idempotency, second.ResolvedRequest.Idempotency);
        Assert.Equal(WorkflowLaunchIdempotencyDisposition.EnforcedNewRun, first.IdempotencyDisposition);
        Assert.Equal(WorkflowLaunchIdempotencyDisposition.ReplayedExistingRun, second.IdempotencyDisposition);
    }

    [Fact]
    public async Task LaunchAsync_ConcurrentCallerIdempotencyKey_StartsOneRun()
    {
        var definition = CreateDefinition(status: WorkflowLifecycleStatus.Active);
        var fixture = CreateFixture([definition]);
        fixture.RunLauncher.BlockStart = true;
        var intent = CreateIntent(
            new WorkflowDefinitionSelection.ExactSavedVersion(definition.Id, definition.VersionId),
            WorkflowLaunchMode.Production) with
        {
            Idempotency = new WorkflowLaunchIdempotency.CallerSupplied(
                new WorkflowLaunchIdempotencyKey("concurrent-launch"))
        };

        var launches = Enumerable.Range(0, 8)
            .Select(_ => fixture.Service.LaunchAsync(intent))
            .ToArray();
        await fixture.RunLauncher.StartEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(fixture.RunLauncher.Requests);
        fixture.RunLauncher.AllowStartToComplete.TrySetResult();
        var results = await Task.WhenAll(launches);

        Assert.Single(fixture.RunLauncher.Requests);
        Assert.Single(results.Select(result => result.Run.RunId).Distinct());
        Assert.Single(results, result =>
            result.IdempotencyDisposition == WorkflowLaunchIdempotencyDisposition.EnforcedNewRun);
        Assert.Equal(7, results.Count(result =>
            result.IdempotencyDisposition == WorkflowLaunchIdempotencyDisposition.ReplayedExistingRun));
    }

    [Fact]
    public async Task LaunchAsync_SameScopeWithDifferentInput_ThrowsConflictWithoutSecondRun()
    {
        var definition = CreateDefinition(status: WorkflowLifecycleStatus.Active);
        var fixture = CreateFixture([definition]);
        var idempotency = new WorkflowLaunchIdempotency.CallerSupplied(
            new WorkflowLaunchIdempotencyKey("conflicting-launch"));
        var intent = CreateIntent(
            new WorkflowDefinitionSelection.ExactSavedVersion(definition.Id, definition.VersionId),
            WorkflowLaunchMode.Production) with
        {
            Idempotency = idempotency,
            InputJson = "{\"value\":1}"
        };
        await fixture.Service.LaunchAsync(intent);

        await Assert.ThrowsAsync<WorkflowLaunchIdempotencyConflictException>(() =>
            fixture.Service.LaunchAsync(intent with { InputJson = "{\"value\":2}" }));

        Assert.Single(fixture.RunLauncher.Requests);
    }

    [Fact]
    public async Task LaunchAsync_LaunchFailure_ReleasesClaimForExplicitRetry()
    {
        var definition = CreateDefinition(status: WorkflowLifecycleStatus.Active);
        var fixture = CreateFixture([definition]);
        fixture.RunLauncher.FailuresRemaining = 1;
        var intent = CreateIntent(
            new WorkflowDefinitionSelection.ExactSavedVersion(definition.Id, definition.VersionId),
            WorkflowLaunchMode.Production) with
        {
            Idempotency = new WorkflowLaunchIdempotency.CallerSupplied(
                new WorkflowLaunchIdempotencyKey("retry-after-launch-failure"))
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.LaunchAsync(intent));
        var retried = await fixture.Service.LaunchAsync(intent);

        Assert.Equal(2, fixture.RunLauncher.Requests.Count);
        Assert.Equal(WorkflowLaunchIdempotencyDisposition.EnforcedNewRun, retried.IdempotencyDisposition);
    }

    [Fact]
    public async Task LaunchAsync_FailureAfterRunPersistence_ReplaysReservedRunWithoutStartingAgain()
    {
        var definition = CreateDefinition(status: WorkflowLifecycleStatus.Active);
        var fixture = CreateFixture([definition]);
        fixture.RunLauncher.FailuresRemaining = 1;
        fixture.RunLauncher.PersistRunBeforeFailure = true;
        var intent = CreateIntent(
            new WorkflowDefinitionSelection.ExactSavedVersion(definition.Id, definition.VersionId),
            WorkflowLaunchMode.Production) with
        {
            Idempotency = new WorkflowLaunchIdempotency.CallerSupplied(
                new WorkflowLaunchIdempotencyKey("crash-window-reserved-run"))
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.LaunchAsync(intent));
        var replay = await fixture.Service.LaunchAsync(intent);

        var request = Assert.Single(fixture.RunLauncher.Requests);
        Assert.NotNull(request.RequestedRunId);
        Assert.Equal(request.RequestedRunId, replay.Run.RunId);
        Assert.Equal(
            WorkflowLaunchIdempotencyDisposition.ReplayedExistingRun,
            replay.IdempotencyDisposition);
    }

    [Fact]
    public async Task LaunchAsync_LatestActiveRetry_ReplaysOriginallyResolvedVersion()
    {
        var workflowId = WorkflowId.New();
        var definitions = new List<WorkflowDefinition>
        {
            CreateDefinition(workflowId, status: WorkflowLifecycleStatus.Active)
        };
        var fixture = CreateFixture(definitions);
        var intent = CreateIntent(
            new WorkflowDefinitionSelection.LatestActive(workflowId),
            WorkflowLaunchMode.Production) with
        {
            Idempotency = new WorkflowLaunchIdempotency.CallerSupplied(
                new WorkflowLaunchIdempotencyKey("latest-active-retry"))
        };
        var first = await fixture.Service.LaunchAsync(intent);
        definitions.Add(CreateDefinition(workflowId, status: WorkflowLifecycleStatus.Active));

        var replay = await fixture.Service.LaunchAsync(intent);

        Assert.Single(fixture.RunLauncher.Requests);
        Assert.Equal(first.Run.RunId, replay.Run.RunId);
        Assert.Equal(first.Run.VersionId, replay.Run.VersionId);
        Assert.Equal(
            WorkflowLaunchIdempotencyDisposition.ReplayedExistingRun,
            replay.IdempotencyDisposition);
    }

    [Fact]
    public async Task LaunchAsync_ReturnWhenAccepted_IsRejectedUntilLifecycleSupportsIt()
    {
        var definition = CreateDefinition(status: WorkflowLifecycleStatus.Active);
        var fixture = CreateFixture([definition]);
        var intent = CreateIntent(
            new WorkflowDefinitionSelection.ExactSavedVersion(definition.Id, definition.VersionId),
            WorkflowLaunchMode.Production) with
        {
            CompletionPolicy = WorkflowLaunchCompletionPolicy.ReturnWhenAccepted
        };

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => fixture.Service.LaunchAsync(intent));

        Assert.Contains("accepted", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.RunLauncher.Requests);
    }

    [Fact]
    public void AddWorkflowCoreServices_RegistersLaunchBoundaryAndSystemTimeProvider()
    {
        var services = new ServiceCollection();

        services.AddWorkflowCoreServices();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IWorkflowLaunchService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IWorkflowRunLauncher));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IWorkflowLaunchIdempotencyStore));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(TimeProvider));
    }

    [Fact]
    public async Task WorkflowRuntimeManagerRunLauncher_PreservesIdempotencyAndReservedRunId()
    {
        var definition = CreateDefinition(status: WorkflowLifecycleStatus.Active);
        var backend = new CapturingWorkflowExecutionBackend();
        var runtime = new WorkflowRuntimeManager([backend], new InMemoryWorkflowRunStore());
        var launcher = new WorkflowRuntimeManagerRunLauncher(runtime);
        var idempotency = new WorkflowLaunchIdempotency.CallerSupplied(
            new WorkflowLaunchIdempotencyKey("launcher-preserves-idempotency"));
        var reservedRunId = WorkflowRunId.New();
        var resolved = new WorkflowResolvedRuntimeRequest(
            definition,
            "{}",
            backend.Descriptor,
            WorkflowPreviewSimulationPlan.Empty,
            WorkflowLaunchMode.Production,
            new WorkflowLaunchOrigin.Api(CreateActor(), CreateCorrelation()),
            WorkflowLaunchCompletionPolicy.WaitForStopped,
            idempotency,
            FixedUtcNow)
        {
            RequestedRunId = reservedRunId
        };

        var run = await launcher.StartAsync(resolved);

        Assert.Equal(reservedRunId, run.RunId);
        Assert.Equal(idempotency, backend.Request?.Idempotency);
        Assert.Equal(reservedRunId, backend.Request?.RequestedRunId);
    }

    [Fact]
    public void PolymorphicLaunchContracts_AllClosedSubtypesRoundTripThroughBaseTypes()
    {
        var definition = CreateDefinition(status: WorkflowLifecycleStatus.Draft);
        var selections = new WorkflowDefinitionSelection[]
        {
            new WorkflowDefinitionSelection.ExactSavedVersion(definition.Id, definition.VersionId),
            new WorkflowDefinitionSelection.LatestActive(definition.Id),
            new WorkflowDefinitionSelection.DraftPreview(definition)
        };
        var origins = new WorkflowLaunchOrigin[]
        {
            new WorkflowLaunchOrigin.Api(CreateActor(), CreateCorrelation()),
            new WorkflowLaunchOrigin.Preview(CreateActor(), CreateCorrelation()),
            new WorkflowLaunchOrigin.SchedulerPlanRun(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new WorkflowSchedulerFireId(Guid.NewGuid()),
                FixedUtcNow,
                CreateCorrelation()),
            new WorkflowLaunchOrigin.ProjectStructureNode(
                Guid.NewGuid(),
                new WorkflowProjectStructureNodeId("node-round-trip"),
                CreateAgentActor(),
                CreateSession(),
                CreateCorrelation()),
            new WorkflowLaunchOrigin.AgentRuntimeInvocation(
                CreateAgentActor(),
                CreateSession(),
                "round-trip",
                CreateCorrelation()),
            new WorkflowLaunchOrigin.ProcessAssignment(
                Guid.NewGuid(),
                Guid.NewGuid(),
                CreateCorrelation())
        };
        var idempotencyValues = new WorkflowLaunchIdempotency[]
        {
            new WorkflowLaunchIdempotency.NotRequested(),
            new WorkflowLaunchIdempotency.CallerSupplied(new WorkflowLaunchIdempotencyKey("round-trip-key"))
        };

        foreach (var selection in selections)
        {
            var roundTrip = RoundTrip(selection);
            Assert.Equal(selection.Kind, roundTrip.Kind);
            Assert.Equal(selection.GetType(), roundTrip.GetType());
            switch (selection, roundTrip)
            {
                case (WorkflowDefinitionSelection.ExactSavedVersion expected, WorkflowDefinitionSelection.ExactSavedVersion actual):
                    Assert.Equal(expected, actual);
                    break;
                case (WorkflowDefinitionSelection.LatestActive expected, WorkflowDefinitionSelection.LatestActive actual):
                    Assert.Equal(expected, actual);
                    break;
                case (WorkflowDefinitionSelection.DraftPreview expected, WorkflowDefinitionSelection.DraftPreview actual):
                    Assert.Equal(expected.Definition.Id, actual.Definition.Id);
                    Assert.Equal(expected.Definition.VersionId, actual.Definition.VersionId);
                    Assert.Equal(expected.Definition.Status, actual.Definition.Status);
                    break;
            }
        }

        foreach (var origin in origins)
        {
            var json = JsonSerializer.Serialize<WorkflowLaunchOrigin>(origin, JsonOptions);
            var roundTrip = RoundTrip(origin);
            Assert.Equal(origin, roundTrip);
            Assert.Equal(origin.GetType(), roundTrip.GetType());
            Assert.DoesNotContain("\"correlation\":", json, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, json.Split("\"correlationId\":", StringSplitOptions.None).Length - 1);
        }

        foreach (var idempotency in idempotencyValues)
        {
            var roundTrip = RoundTrip(idempotency);
            Assert.Equal(idempotency, roundTrip);
            Assert.Equal(idempotency.GetType(), roundTrip.GetType());
        }
    }

    [Fact]
    public async Task InMemoryCatalog_GetLatestDefinitionByStatusAsync_ReturnsNewestMatchingVersion()
    {
        var workflowId = WorkflowId.New();
        var catalog = new InMemoryWorkflowCatalogService(new PassingWorkflowDefinitionValidator());
        var source = CreateDefinition(workflowId, status: WorkflowLifecycleStatus.Active);
        var active = await catalog.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
            workflowId,
            ExpectedVersionId: null,
            source.Name,
            source.Description,
            WorkflowLifecycleStatus.Active,
            source.Graph,
            source.RuntimePolicy));
        var draft = await catalog.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
            workflowId,
            active.VersionId,
            source.Name,
            source.Description,
            WorkflowLifecycleStatus.Draft,
            source.Graph,
            source.RuntimePolicy));

        var detail = await catalog.GetLatestDefinitionByStatusAsync(
            workflowId,
            WorkflowLifecycleStatus.Active);

        Assert.NotNull(detail);
        Assert.Equal(active.VersionId, detail.Definition.VersionId);
        Assert.NotEqual(draft.VersionId, detail.Definition.VersionId);
        Assert.Equal(WorkflowLifecycleStatus.Active, detail.Definition.Status);
    }

    private static WorkflowLaunchIntent CreateIntent(
        WorkflowDefinitionSelection selection,
        WorkflowLaunchMode mode)
        => new(
            selection,
            mode,
            new WorkflowLaunchOrigin.Api(CreateActor(), CreateCorrelation()),
            "{}",
            WorkflowLaunchCompletionPolicy.WaitForStopped,
            new WorkflowLaunchIdempotency.NotRequested());

    private static WorkflowLaunchActor CreateActor()
        => new(WorkflowLaunchActorKind.User, $"user-{Guid.NewGuid():N}");

    private static WorkflowLaunchActor CreateAgentActor()
        => new(WorkflowLaunchActorKind.Agent, $"agent-{Guid.NewGuid():N}");

    private static WorkflowLaunchCorrelationId CreateCorrelation()
        => new($"correlation-{Guid.NewGuid():N}");

    private static WorkflowLaunchSessionId CreateSession()
        => new($"session-{Guid.NewGuid():N}");

    private static T RoundTrip<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
               ?? throw new InvalidOperationException($"{typeof(T).Name} did not deserialize.");
    }

    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    private static TestFixture CreateFixture(IReadOnlyList<WorkflowDefinition> definitions)
    {
        var catalog = new RecordingWorkflowCatalog(definitions);
        var runStore = new InMemoryWorkflowRunStore();
        var runLauncher = new RecordingWorkflowRunLauncher(FixedUtcNow, runStore);
        var service = new WorkflowLaunchService(
            catalog,
            new WorkflowRuntimeBackendCatalog([WorkflowRuntimeBackendKind.InProcess]),
            runLauncher,
            new InMemoryWorkflowLaunchIdempotencyStore(),
            runStore,
            new FixedTimeProvider(FixedUtcNow));
        return new TestFixture(service, catalog, runLauncher, runStore);
    }

    private static (WorkflowLaunchService Service, RecordingWorkflowRunLauncher RunLauncher) CreateLaunchFixture(
        IWorkflowCatalogService catalog)
    {
        var runStore = new InMemoryWorkflowRunStore();
        var runLauncher = new RecordingWorkflowRunLauncher(FixedUtcNow, runStore);
        var service = new WorkflowLaunchService(
            catalog,
            new WorkflowRuntimeBackendCatalog([WorkflowRuntimeBackendKind.InProcess]),
            runLauncher,
            new InMemoryWorkflowLaunchIdempotencyStore(),
            runStore,
            new FixedTimeProvider(FixedUtcNow));
        return (service, runLauncher);
    }

    private static WorkflowDefinition CreateDefinition(
        WorkflowId? workflowId = null,
        WorkflowVersionId? versionId = null,
        WorkflowLifecycleStatus status = WorkflowLifecycleStatus.Active)
    {
        var startNodeId = new WorkflowNodeId("start");
        return new WorkflowDefinition(
            workflowId ?? WorkflowId.New(),
            versionId ?? WorkflowVersionId.New(),
            "Launch test",
            "Launch policy fixture.",
            status,
            new WorkflowGraph(
                startNodeId,
                [
                    new WorkflowNode(
                        startNodeId,
                        WorkflowNodeKind.Start,
                        "Start",
                        [],
                        new WorkflowNodeSettings(
                            ComponentId: null,
                            AgentId: null,
                            SubworkflowId: null,
                            ExternalRequestKind: null,
                            Instructions: string.Empty,
                            InputShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON input"),
                            ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON result")))
                ],
                []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            FixedUtcNow,
            FixedUtcNow);
    }

    private sealed record TestFixture(
        WorkflowLaunchService Service,
        RecordingWorkflowCatalog Catalog,
        RecordingWorkflowRunLauncher RunLauncher,
        InMemoryWorkflowRunStore RunStore);

    private sealed class RecordingWorkflowCatalog(IReadOnlyList<WorkflowDefinition> definitions) : IWorkflowCatalogService
    {
        public List<(WorkflowId WorkflowId, WorkflowVersionId? VersionId)> Requests { get; } = [];

        public List<(WorkflowId WorkflowId, WorkflowLifecycleStatus Status)> LatestStatusRequests { get; } = [];

        public List<WorkflowDefinition> ValidationRequests { get; } = [];

        public WorkflowValidationResult Validation { get; set; } = WorkflowValidationResult.Success;

        public Task<WorkflowDefinitionDetail?> GetDefinitionAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((workflowId, versionId));
            var definition = versionId is { } exactVersion
                ? definitions.LastOrDefault(candidate => candidate.Id == workflowId && candidate.VersionId == exactVersion)
                : definitions.LastOrDefault(candidate => candidate.Id == workflowId);
            return Task.FromResult(CreateDetail(definition));
        }

        public Task<WorkflowDefinitionDetail?> GetLatestDefinitionByStatusAsync(
            WorkflowId workflowId,
            WorkflowLifecycleStatus status,
            CancellationToken cancellationToken = default)
        {
            LatestStatusRequests.Add((workflowId, status));
            var current = definitions.LastOrDefault(candidate => candidate.Id == workflowId);
            var currentAllowsActiveLookup =
                status != WorkflowLifecycleStatus.Active ||
                current is { Status: WorkflowLifecycleStatus.Draft or WorkflowLifecycleStatus.Active };
            var definition = currentAllowsActiveLookup
                ? definitions.LastOrDefault(candidate => candidate.Id == workflowId && candidate.Status == status)
                : null;
            return Task.FromResult(CreateDetail(definition));
        }

        public Task<IReadOnlyList<WorkflowCatalogItem>> ListDefinitionsAsync(CancellationToken cancellationToken = default)
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
        {
            ValidationRequests.Add(definition);
            return Task.FromResult(Validation);
        }

        private WorkflowDefinitionDetail? CreateDetail(WorkflowDefinition? definition)
        {
            if (definition is null)
            {
                return null;
            }

            ValidationRequests.Add(definition);
            return new WorkflowDefinitionDetail(definition, Validation);
        }
    }

    private sealed class RecordingWorkflowRunLauncher(
        DateTimeOffset now,
        InMemoryWorkflowRunStore runStore) : IWorkflowRunLauncher
    {
        public List<WorkflowResolvedRuntimeRequest> Requests { get; } = [];

        public bool BlockStart { get; set; }

        public int FailuresRemaining { get; set; }

        public bool PersistRunBeforeFailure { get; set; }

        public TaskCompletionSource StartEntered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AllowStartToComplete { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<WorkflowRunSnapshot> StartAsync(
            WorkflowResolvedRuntimeRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            StartEntered.TrySetResult();
            if (BlockStart)
            {
                await AllowStartToComplete.Task.WaitAsync(cancellationToken);
            }

            var run = new WorkflowRunSnapshot(
                request.RequestedRunId ?? WorkflowRunId.New(),
                request.Definition.Id,
                request.Definition.VersionId,
                WorkflowRunState.Completed,
                request.Backend.Kind,
                $"fake-{Requests.Count}",
                "Completed by fake launcher.",
                now,
                now);
            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;
                if (PersistRunBeforeFailure)
                {
                    await runStore.SaveRunAsync(run, CancellationToken.None);
                }

                throw new InvalidOperationException("Fixture workflow launch failed.");
            }

            return run;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class CapturingWorkflowExecutionBackend : IWorkflowExecutionBackend
    {
        public WorkflowRunStartRequest? Request { get; private set; }

        public WorkflowRuntimeBackendDescriptor Descriptor { get; } =
            new WorkflowRuntimeBackendCatalog([WorkflowRuntimeBackendKind.InProcess])
                .GetRequiredBackend(WorkflowRuntimeBackendKind.InProcess);

        public Task<WorkflowBackendStartResult> StartAsync(
            WorkflowDefinition definition,
            WorkflowRunStartRequest request,
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            var completed = new WorkflowRunSnapshot(
                runId,
                definition.Id,
                definition.VersionId,
                WorkflowRunState.Completed,
                Descriptor.Kind,
                runId.ToString(),
                "Completed",
                FixedUtcNow,
                FixedUtcNow)
            {
                TerminalAtUtc = FixedUtcNow,
                Origin = request.Origin
            };
            return Task.FromResult(new WorkflowBackendStartResult(
                completed,
                Events: [],
                ExternalRequests: [],
                Artifacts: []));
        }
    }

    private sealed class PassingWorkflowDefinitionValidator : IWorkflowDefinitionValidator
    {
        public WorkflowValidationResult Validate(
            WorkflowDefinition definition,
            IReadOnlyList<LlmCallComponent> components)
            => WorkflowValidationResult.Success;
    }
}
