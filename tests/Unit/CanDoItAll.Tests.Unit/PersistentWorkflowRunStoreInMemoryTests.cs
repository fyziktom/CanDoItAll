using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit.AgentFramework;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class PersistentWorkflowRunStoreInMemoryTests
{
    private static readonly DateTimeOffset StartedAtUtc =
        new(2026, 8, 21, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateRunWithStartedEventAsync_PersistsRunAndStartedEvent()
    {
        var store = CreateStore();
        var run = CreateRun(WorkflowRunState.Running, StartedAtUtc);
        var startedEvent = CreateEvent(run.RunId, WorkflowEventKind.Started, StartedAtUtc);

        await store.CreateRunWithStartedEventAsync(run, startedEvent);

        var persistedRun = await store.GetRunAsync(run.RunId);
        var persistedEvent = Assert.Single(await store.ListEventsAsync(run.RunId));
        Assert.Equal(run, persistedRun);
        Assert.Equal(startedEvent, persistedEvent);

        var exception = await Assert.ThrowsAsync<WorkflowRunAlreadyExistsException>(() =>
            store.CreateRunWithStartedEventAsync(run, startedEvent));
        Assert.Equal(run.RunId, exception.RunId);
        Assert.Single(await store.ListEventsAsync(run.RunId));
    }

    [Fact]
    public async Task TryTransitionRunAsync_PersistsMatchingTransitionAndRejectsStaleTransition()
    {
        var store = CreateStore();
        var running = CreateRun(WorkflowRunState.Running, StartedAtUtc);
        await store.CreateRunWithStartedEventAsync(
            running,
            CreateEvent(running.RunId, WorkflowEventKind.Started, StartedAtUtc));
        var completedAtUtc = StartedAtUtc.AddSeconds(5);
        var completed = running with
        {
            State = WorkflowRunState.Completed,
            Summary = "Completed.",
            UpdatedAtUtc = completedAtUtc,
            TerminalAtUtc = completedAtUtc
        };
        var completedEvent = CreateEvent(
            running.RunId,
            WorkflowEventKind.Completed,
            completedAtUtc);

        var transition = await store.TryTransitionRunAsync(
            running.RunId,
            [WorkflowRunState.Running],
            completed,
            completedEvent);
        var staleAtUtc = completedAtUtc.AddSeconds(1);
        var stale = completed with
        {
            State = WorkflowRunState.Cancelled,
            Summary = "Stale cancellation.",
            UpdatedAtUtc = staleAtUtc,
            TerminalAtUtc = staleAtUtc
        };
        var staleTransition = await store.TryTransitionRunAsync(
            running.RunId,
            [WorkflowRunState.Running],
            stale,
            CreateEvent(running.RunId, WorkflowEventKind.Cancelled, staleAtUtc));

        Assert.True(transition.Transitioned);
        Assert.Equal(completed, transition.Run);
        Assert.False(staleTransition.Transitioned);
        Assert.Equal(completed, staleTransition.Run);
        Assert.Equal(completed, await store.GetRunAsync(running.RunId));
        var events = await store.ListEventsAsync(running.RunId);
        Assert.Single(events, workflowEvent => workflowEvent.Kind == WorkflowEventKind.Started);
        Assert.Single(events, workflowEvent => workflowEvent.Kind == WorkflowEventKind.Completed);
        Assert.DoesNotContain(events, workflowEvent => workflowEvent.Kind == WorkflowEventKind.Cancelled);
    }

    [Fact]
    public async Task TryAcceptExternalResponseAsync_ConcurrentResponsesAcceptsExactlyOneAndReplaysWinner()
    {
        var store = CreateStore();
        var run = CreateRun(WorkflowRunState.WaitingForInput, StartedAtUtc);
        var request = new WorkflowExternalRequestRecord(
            WorkflowExternalRequestId.New(),
            run.RunId,
            WorkflowExternalRequestKind.HumanInput,
            new WorkflowNodeId("human-input"),
            "human-input",
            "{\"question\":\"What is your hobby?\"}",
            string.Empty,
            StartedAtUtc,
            RespondedAtUtc: null);
        await store.SaveRunAsync(run);
        await store.SaveExternalRequestAsync(request);

        var firstResponse = store.TryAcceptExternalResponseAsync(
            request.Id,
            "{\"answer\":\"photography\"}",
            StartedAtUtc.AddSeconds(1));
        var secondResponse = store.TryAcceptExternalResponseAsync(
            request.Id,
            "{\"answer\":\"cycling\"}",
            StartedAtUtc.AddSeconds(2));
        var results = await Task.WhenAll(firstResponse, secondResponse);

        var accepted = Assert.Single(
            results,
            result => result.Outcome == WorkflowExternalResponseAcceptanceOutcome.Accepted);
        var replay = Assert.Single(
            results,
            result => result.Outcome == WorkflowExternalResponseAcceptanceOutcome.AlreadyResponded);
        Assert.NotNull(accepted.Request);
        Assert.NotNull(replay.Request);
        Assert.Equal(accepted.Request.ResponseJson, replay.Request.ResponseJson);
        Assert.Equal(accepted.Request.RespondedAtUtc, replay.Request.RespondedAtUtc);
        Assert.Equal(accepted.Request, await store.GetExternalRequestAsync(request.Id));
    }

    [Fact]
    public async Task GetAndListPendingExternalRequests_HydrateNativeBoundaryContract()
    {
        var harness = CreateHarness();
        var nativeRequest = CreateNativeRequest();
        await SeedNativeRequestAsync(harness, nativeRequest);

        var loaded = await harness.Store.GetExternalRequestAsync(nativeRequest.Request.Id);
        var pending = Assert.Single(
            await harness.Store.ListPendingExternalRequestsAsync(nativeRequest.Run.RunId));

        Assert.NotNull(loaded);
        AssertNativeBoundary(nativeRequest.Boundary, loaded);
        AssertNativeBoundary(nativeRequest.Boundary, pending);
    }

    [Fact]
    public async Task TryAcceptExternalResponseAsync_ReturnsHydratedNativeBoundaryForWinnerAndReplay()
    {
        var harness = CreateHarness();
        var nativeRequest = CreateNativeRequest();
        await SeedNativeRequestAsync(harness, nativeRequest);
        var respondedAtUtc = StartedAtUtc.AddSeconds(1);
        const string acceptedResponse = "{\"answer\":\"photography\"}";

        var accepted = await harness.Store.TryAcceptExternalResponseAsync(
            nativeRequest.Request.Id,
            acceptedResponse,
            respondedAtUtc);
        var replay = await harness.Store.TryAcceptExternalResponseAsync(
            nativeRequest.Request.Id,
            "{\"answer\":\"cycling\"}",
            respondedAtUtc.AddSeconds(1));

        Assert.Equal(WorkflowExternalResponseAcceptanceOutcome.Accepted, accepted.Outcome);
        Assert.Equal(WorkflowExternalResponseAcceptanceOutcome.AlreadyResponded, replay.Outcome);
        Assert.NotNull(accepted.Request);
        Assert.NotNull(replay.Request);
        AssertNativeBoundary(nativeRequest.Boundary, accepted.Request);
        AssertNativeBoundary(nativeRequest.Boundary, replay.Request);
        Assert.Equal(acceptedResponse, accepted.Request.ResponseJson);
        Assert.Equal(acceptedResponse, replay.Request.ResponseJson);
        Assert.Equal(respondedAtUtc, accepted.Request.RespondedAtUtc);
        Assert.Equal(respondedAtUtc, replay.Request.RespondedAtUtc);
    }

    [Fact]
    public async Task MarkRespondedAsync_ReturnsHydratedNativeBoundary()
    {
        var harness = CreateHarness();
        var nativeRequest = CreateNativeRequest();
        await SeedNativeRequestAsync(harness, nativeRequest);
        var respondedAtUtc = StartedAtUtc.AddSeconds(1);
        const string responseJson = "{\"answer\":\"photography\"}";

        var responded = await harness.Store.MarkRespondedAsync(
            nativeRequest.Request.Id,
            responseJson,
            respondedAtUtc);

        AssertNativeBoundary(nativeRequest.Boundary, responded);
        Assert.Equal(responseJson, responded.ResponseJson);
        Assert.Equal(respondedAtUtc, responded.RespondedAtUtc);
    }

    private static PersistentWorkflowRunStore CreateStore()
        => CreateHarness().Store;

    private static StoreHarness CreateHarness()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([
            typeof(AgentFrameworkModuleAssemblyMarker).Assembly
        ]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"workflow-run-store-{Guid.NewGuid():N}")
            .Options;
        var factory = new TestDbContextFactory(options);
        return new StoreHarness(
            new PersistentWorkflowRunStore(factory),
            factory);
    }

    private static async Task SeedNativeRequestAsync(
        StoreHarness harness,
        NativeRequest nativeRequest)
    {
        await harness.Store.SaveRunAsync(nativeRequest.Run);
        await harness.Store.SaveExternalRequestAsync(nativeRequest.Request);
        await using var dbContext = await harness.Factory.CreateDbContextAsync();
        var boundaryEntity = new WorkflowExternalRequestBoundaryEntity
        {
            RequestId = nativeRequest.Request.Id.Value
        };
        PersistentWorkflowExternalRequestBoundaryStore.Apply(
            boundaryEntity,
            nativeRequest.Boundary);
        dbContext.Set<WorkflowExternalRequestBoundaryEntity>().Add(boundaryEntity);
        await dbContext.SaveChangesAsync();
    }

    private static NativeRequest CreateNativeRequest()
    {
        var run = CreateRun(WorkflowRunState.WaitingForInput, StartedAtUtc);
        var request = new WorkflowExternalRequestRecord(
            WorkflowExternalRequestId.New(),
            run.RunId,
            WorkflowExternalRequestKind.HumanInput,
            new WorkflowNodeId("human-input"),
            "human-input",
            "{\"question\":\"What is your hobby?\"}",
            string.Empty,
            StartedAtUtc,
            RespondedAtUtc: null);
        const string schemaJson =
            "{\"type\":\"object\",\"required\":[\"answer\"],\"properties\":{\"answer\":{\"type\":\"string\"}}}";
        var responseContract = new WorkflowExternalResponseContract(
            WorkflowExternalRequestKind.HumanInput,
            "sample.hobby-answer",
            3,
            schemaJson,
            4_096);
        var continuation = new WorkflowExternalRequestContinuation(
            new WorkflowBackendExternalRequestLink(
                request.Id,
                new WorkflowBackendRequestId("backend-request-1"),
                new WorkflowBackendRequestPortId("answer")),
            new WorkflowBackendCheckpointLink(
                new WorkflowBackendSessionId("session-1"),
                new WorkflowBackendCheckpointId("checkpoint-1")),
            new WorkflowCompilerContractVersion(2),
            WorkflowTopologyFingerprint.Create($"{run.WorkflowId}:{run.VersionId}"),
            WorkflowBackendCheckpointPayloadHash.Compute("{}"));
        var authorization = new WorkflowExternalRequestAuthorizationPolicySnapshot(
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "request-owner"),
            WorkflowExecutorIds.HttpFetch,
            WorkflowExecutorCapabilityFlags.ReadsExternalData |
            WorkflowExecutorCapabilityFlags.UsesNetwork,
            WorkflowExecutorApprovalRequirement.RequiredForExternalEffect,
            "workflow-reviewer")
        {
            AuthorizationScope = WorkspaceScopeDescriptor.Sandbox,
            AuthorizationPolicyFingerprint =
                WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            ResponseAuthorizationLifetimeSeconds =
                WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds
        };
        var requestPayloadHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(request.RequestJson)));
        var boundary = new WorkflowExternalRequestBoundaryRecord(
            request.Id,
            new WorkflowExternalRequestVersion(4),
            WorkflowExternalRequestState.Pending,
            responseContract,
            continuation,
            new WorkflowExternalRequestPayloadHash(requestPayloadHash),
            StartedAtUtc)
        {
            AuthorizationPolicy = authorization
        };
        return new NativeRequest(run, request, boundary);
    }

    private static void AssertNativeBoundary(
        WorkflowExternalRequestBoundaryRecord expected,
        WorkflowExternalRequestRecord actual)
    {
        Assert.Equal(expected.RequestVersion, actual.Version);
        Assert.Equal(expected.State, actual.State);
        Assert.Equal(expected.ResponseContract, actual.ResponseContract);
        Assert.Equal(expected.ResponseContract.SchemaId, actual.ResponseContract!.SchemaId);
        Assert.Equal(expected.ResponseContract.SchemaVersion, actual.ResponseContract.SchemaVersion);
        Assert.Equal(expected.ResponseContract.SchemaJson, actual.ResponseContract.SchemaJson);
        Assert.Equal(expected.ResponseContract.SchemaHash, actual.ResponseContract.SchemaHash);
        Assert.Equal(expected.Continuation, actual.Continuation);
        Assert.Equal(expected.AuthorizationPolicy, actual.AuthorizationPolicy);
    }

    private static WorkflowRunSnapshot CreateRun(
        WorkflowRunState state,
        DateTimeOffset timestamp)
        => new(
            WorkflowRunId.New(),
            WorkflowId.New(),
            WorkflowVersionId.New(),
            state,
            WorkflowRuntimeBackendKind.InProcess,
            "in-memory-test",
            $"Workflow is {state}.",
            timestamp,
            timestamp);

    private static WorkflowEventRecord CreateEvent(
        WorkflowRunId runId,
        WorkflowEventKind kind,
        DateTimeOffset timestamp)
        => new(
            Guid.NewGuid(),
            runId,
            kind,
            NodeId: null,
            kind.ToString(),
            "{}",
            timestamp);

    private sealed record StoreHarness(
        PersistentWorkflowRunStore Store,
        TestDbContextFactory Factory);

    private sealed record NativeRequest(
        WorkflowRunSnapshot Run,
        WorkflowExternalRequestRecord Request,
        WorkflowExternalRequestBoundaryRecord Boundary);

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) :
        IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
}
