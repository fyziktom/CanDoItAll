using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowBackendCheckpointPayloadStoreTests
{
    [Fact]
    public async Task Create_allocates_unique_checkpoint_ids_and_ordinals_under_concurrency()
    {
        var store = CreateStore();
        var session = CreateSession();

        var results = await Task.WhenAll(Enumerable.Range(0, 64)
            .Select(index => store.CreateAsync(CreateRequest(session, $"{{\"index\":{index}}}"))));

        Assert.All(results, result => Assert.Equal(
            WorkflowBackendCheckpointCreateOutcome.Created,
            result.Outcome));
        Assert.Equal(64, results.Select(result => result.Checkpoint!.Index.Link.CheckpointId).Distinct().Count());
        Assert.Equal(
            Enumerable.Range(0, 64).Select(index => (long)index),
            results.Select(result => result.Checkpoint!.Index.CommitOrdinal.Value).Order());
    }

    [Fact]
    public async Task List_index_returns_oldest_to_newest_commit_order()
    {
        var store = CreateStore();
        var session = CreateSession();
        var first = await store.CreateAsync(CreateRequest(session, "{\"value\":1}"));
        var second = await store.CreateAsync(new WorkflowBackendCheckpointCreateRequest(
            session,
            first.Checkpoint!.Index.Link,
            WorkflowBackendCheckpointPayload.Create("{\"value\":2}")));
        var third = await store.CreateAsync(new WorkflowBackendCheckpointCreateRequest(
            session,
            second.Checkpoint!.Index.Link,
            WorkflowBackendCheckpointPayload.Create("{\"value\":3}")));

        var result = await store.ListIndexAsync(session.Id);

        Assert.Equal(WorkflowBackendCheckpointListOutcome.Found, result.Outcome);
        Assert.Equal(
            [first.Checkpoint.Index.Link, second.Checkpoint.Index.Link, third.Checkpoint!.Index.Link],
            result.Checkpoints.Select(checkpoint => checkpoint.Link));
        Assert.Equal([0L, 1L, 2L], result.Checkpoints.Select(checkpoint => checkpoint.CommitOrdinal.Value));
    }

    [Fact]
    public async Task Read_returns_exact_payload_and_computed_hash()
    {
        var store = CreateStore();
        var session = CreateSession();
        const string payloadJson = "{\"state\":{\"step\":2}}";
        var created = await store.CreateAsync(CreateRequest(session, payloadJson));

        var result = await store.ReadAsync(created.Checkpoint!.Index.Link);

        Assert.Equal(WorkflowBackendCheckpointReadOutcome.Found, result.Outcome);
        Assert.Equal(payloadJson, result.Checkpoint!.Payload.Json);
        Assert.Equal(WorkflowBackendCheckpointPayloadHash.Compute(payloadJson), result.Checkpoint.Payload.Sha256);
        Assert.True(result.Checkpoint.Payload.HasValidHash);
    }

    [Fact]
    public async Task Create_preserves_typed_session_and_external_request_link()
    {
        var store = CreateStore();
        var session = CreateSession();
        var externalRequestLink = new WorkflowBackendExternalRequestLink(
            WorkflowExternalRequestId.New(),
            new WorkflowBackendRequestId("native-request"),
            new WorkflowBackendRequestPortId("native-port"));
        var request = CreateRequest(session, "{}") with
        {
            ExternalRequestLink = externalRequestLink
        };

        var created = await store.CreateAsync(request);
        var read = await store.ReadAsync(created.Checkpoint!.Index.Link);

        Assert.Equal(session, read.Checkpoint!.Session);
        Assert.Equal(externalRequestLink, read.Checkpoint.ExternalRequestLink);
    }

    [Fact]
    public async Task Boundary_save_links_checkpoint_created_before_request_and_fails_without_native_composition()
    {
        var now = DateTimeOffset.Parse("2026-08-21T02:30:00Z");
        var store = new InMemoryWorkflowBackendCheckpointPayloadStore(new FixedTimeProvider(now));
        var session = CreateSession();
        var created = await store.CreateAsync(CreateRequest(session, "{}"));
        var checkpoint = Assert.IsType<WorkflowBackendCheckpointPayloadRecord>(created.Checkpoint);
        var run = new WorkflowRunSnapshot(
            session.RunId,
            session.WorkflowId,
            session.WorkflowVersionId,
            WorkflowRunState.WaitingForInput,
            session.Backend,
            session.Id.Value,
            "Waiting for input.",
            now,
            now);
        var requestId = WorkflowExternalRequestId.New();
        var requestLink = new WorkflowBackendExternalRequestLink(
            requestId,
            new WorkflowBackendRequestId("native-request"),
            new WorkflowBackendRequestPortId("human-input"));
        var request = new WorkflowExternalRequestRecord(
            requestId,
            run.RunId,
            WorkflowExternalRequestKind.HumanInput,
            new WorkflowNodeId("human"),
            "human-input",
            "{\"prompt\":\"Continue?\"}",
            string.Empty,
            now,
            RespondedAtUtc: null)
        {
            Version = WorkflowExternalRequestVersion.Initial,
            State = WorkflowExternalRequestState.Pending,
            ResponseContract = new WorkflowExternalResponseContract(
                WorkflowExternalRequestKind.HumanInput,
                "test.human-input",
                1,
                "{}",
                4096),
            Continuation = new WorkflowExternalRequestContinuation(
                requestLink,
                checkpoint.Index.Link,
                session.CompilerContractVersion,
                session.TopologyFingerprint,
                checkpoint.Payload.Sha256)
        };
        var runStore = new InMemoryWorkflowRunStore();
        await runStore.SaveRunAsync(run);
        await runStore.SaveExternalRequestAsync(request);
        Assert.True(WorkflowExternalRequestBoundaryRecord.TryCreate(request, out var boundary));
        Assert.NotNull(boundary);

        var incompleteStore = new InMemoryWorkflowExternalRequestBoundaryStore(runStore);
        var unavailable = await incompleteStore.UpsertAsync(boundary);
        var unavailableRead = await incompleteStore.ReadAsync(request.Id);
        var linked = await new InMemoryWorkflowExternalRequestBoundaryStore(runStore, store)
            .UpsertAsync(boundary);
        var reloaded = await store.ReadAsync(checkpoint.Index.Link);

        Assert.Equal(
            WorkflowExternalRequestBoundarySaveOutcome.NativeCheckpointLinkUnavailable,
            unavailable.Outcome);
        Assert.Equal(
            WorkflowExternalRequestBoundaryReadOutcome.NativeCheckpointLinkUnavailable,
            unavailableRead.Outcome);
        Assert.Equal(WorkflowExternalRequestBoundarySaveOutcome.Created, linked.Outcome);
        Assert.Equal(requestLink, reloaded.Checkpoint?.ExternalRequestLink);
    }

    [Fact]
    public async Task Create_rejects_payload_hash_mismatch_without_allocating_an_ordinal()
    {
        var store = CreateStore();
        var session = CreateSession();
        var corruptPayload = new WorkflowBackendCheckpointPayload(
            "{\"value\":1}",
            WorkflowBackendCheckpointPayloadHash.Compute("{\"value\":2}"));

        var rejected = await store.CreateAsync(new WorkflowBackendCheckpointCreateRequest(
            session,
            Parent: null,
            corruptPayload));
        var created = await store.CreateAsync(CreateRequest(session, "{\"value\":3}"));

        Assert.Equal(WorkflowBackendCheckpointCreateOutcome.PayloadCorrupt, rejected.Outcome);
        Assert.Null(rejected.Checkpoint);
        Assert.Equal(0, created.Checkpoint!.Index.CommitOrdinal.Value);
    }

    [Fact]
    public async Task Read_distinguishes_missing_checkpoint_from_wrong_session()
    {
        var store = CreateStore();
        var session = CreateSession();
        var created = await store.CreateAsync(CreateRequest(session, "{}"));

        var missing = await store.ReadAsync(new WorkflowBackendCheckpointLink(
            session.Id,
            WorkflowBackendCheckpointId.New()));
        var wrongSession = await store.ReadAsync(new WorkflowBackendCheckpointLink(
            new WorkflowBackendSessionId("different-session"),
            created.Checkpoint!.Index.Link.CheckpointId));

        Assert.Equal(WorkflowBackendCheckpointReadOutcome.NotFound, missing.Outcome);
        Assert.Equal(WorkflowBackendCheckpointReadOutcome.SessionMismatch, wrongSession.Outcome);
        Assert.Null(missing.Checkpoint);
        Assert.Null(wrongSession.Checkpoint);
    }

    [Fact]
    public async Task List_index_returns_typed_not_found_for_unknown_session()
    {
        var store = CreateStore();

        var result = await store.ListIndexAsync(new WorkflowBackendSessionId("unknown-session"));

        Assert.Equal(WorkflowBackendCheckpointListOutcome.SessionNotFound, result.Outcome);
        Assert.Empty(result.Checkpoints);
    }

    [Fact]
    public async Task Create_rejects_parent_from_another_session()
    {
        var store = CreateStore();
        var firstSession = CreateSession("first-session");
        var secondSession = CreateSession("second-session");
        var parent = await store.CreateAsync(CreateRequest(firstSession, "{}"));

        var result = await store.CreateAsync(new WorkflowBackendCheckpointCreateRequest(
            secondSession,
            parent.Checkpoint!.Index.Link,
            WorkflowBackendCheckpointPayload.Create("{}")));

        Assert.Equal(WorkflowBackendCheckpointCreateOutcome.ParentSessionMismatch, result.Outcome);
        Assert.Null(result.Checkpoint);
    }

    [Fact]
    public async Task Create_rejects_unknown_parent_without_allocating_an_ordinal()
    {
        var store = CreateStore();
        var session = CreateSession();
        var unknownParent = new WorkflowBackendCheckpointLink(
            session.Id,
            WorkflowBackendCheckpointId.New());

        var rejected = await store.CreateAsync(new WorkflowBackendCheckpointCreateRequest(
            session,
            unknownParent,
            WorkflowBackendCheckpointPayload.Create("{}")));
        var created = await store.CreateAsync(CreateRequest(session, "{}"));

        Assert.Equal(WorkflowBackendCheckpointCreateOutcome.ParentNotFound, rejected.Outcome);
        Assert.Equal(0, created.Checkpoint!.Index.CommitOrdinal.Value);
    }

    [Fact]
    public async Task Create_rejects_session_metadata_drift()
    {
        var store = CreateStore();
        var session = CreateSession();
        await store.CreateAsync(CreateRequest(session, "{}"));
        var changedSession = session with
        {
            WorkflowVersionId = WorkflowVersionId.New()
        };

        var result = await store.CreateAsync(CreateRequest(changedSession, "{}"));

        Assert.Equal(WorkflowBackendCheckpointCreateOutcome.SessionMetadataMismatch, result.Outcome);
        Assert.Null(result.Checkpoint);
    }

    [Fact]
    public async Task Store_operations_honor_precancelled_tokens()
    {
        var store = CreateStore();
        var session = CreateSession();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.CreateAsync(CreateRequest(session, "{}"), cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.ListIndexAsync(session.Id, cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.ReadAsync(
                new WorkflowBackendCheckpointLink(session.Id, WorkflowBackendCheckpointId.New()),
                cancellation.Token));
    }

    private static InMemoryWorkflowBackendCheckpointPayloadStore CreateStore()
        => new(TimeProvider.System);

    private static WorkflowBackendCheckpointSession CreateSession(string sessionId = "checkpoint-session")
        => new(
            new WorkflowBackendSessionId(sessionId),
            WorkflowRunId.New(),
            WorkflowId.New(),
            WorkflowVersionId.New(),
            WorkflowRuntimeBackendKind.InProcess,
            new WorkflowBackendCheckpointFormat("maf-json"),
            new WorkflowBackendCheckpointFormatVersion(1),
            new WorkflowCompilerContractVersion(1),
            WorkflowTopologyFingerprint.Create("stable-topology"));

    private static WorkflowBackendCheckpointCreateRequest CreateRequest(
        WorkflowBackendCheckpointSession session,
        string payloadJson)
        => new(
            session,
            Parent: null,
            WorkflowBackendCheckpointPayload.Create(payloadJson));
}
