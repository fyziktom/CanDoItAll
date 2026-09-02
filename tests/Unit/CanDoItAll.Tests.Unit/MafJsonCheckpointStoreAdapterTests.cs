using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafJsonCheckpointStoreAdapterTests
{
    [Fact]
    public async Task Adapter_round_trips_json_and_preserves_explicit_commit_order()
    {
        var session = CreateSession("session-a");
        var store = new InMemoryWorkflowBackendCheckpointPayloadStore(TimeProvider.System);
        var adapter = new MafJsonCheckpointStoreAdapter(store, session);
        var first = await adapter.CreateCheckpointAsync(
            session.Id.Value,
            JsonSerializer.SerializeToElement(new { marker = 1 }));
        var second = await adapter.CreateCheckpointAsync(
            session.Id.Value,
            JsonSerializer.SerializeToElement(new { marker = 2 }),
            first);

        var index = (await adapter.RetrieveIndexAsync(session.Id.Value)).ToArray();
        var children = (await adapter.RetrieveIndexAsync(session.Id.Value, first)).ToArray();
        var restored = await adapter.RetrieveCheckpointAsync(session.Id.Value, second);

        Assert.Equal([first, second], index);
        Assert.Equal([second], children);
        Assert.Equal(2, restored.GetProperty("marker").GetInt32());
    }

    [Fact]
    public async Task Adapter_returns_empty_index_for_unknown_configured_session()
    {
        var session = CreateSession("session-empty");
        var adapter = new MafJsonCheckpointStoreAdapter(
            new InMemoryWorkflowBackendCheckpointPayloadStore(TimeProvider.System),
            session);

        var index = await adapter.RetrieveIndexAsync(session.Id.Value);

        Assert.Empty(index);
    }

    [Fact]
    public async Task Adapter_rejects_session_mismatch_before_store_access()
    {
        var session = CreateSession("session-a");
        var adapter = new MafJsonCheckpointStoreAdapter(
            new InMemoryWorkflowBackendCheckpointPayloadStore(TimeProvider.System),
            session);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await adapter.CreateCheckpointAsync(
                "session-b",
                JsonSerializer.SerializeToElement(new { marker = 1 })));
    }

    [Fact]
    public void Checkpoint_protocol_options_support_custom_types_and_jsonb_property_order()
    {
        var payload = JsonSerializer.SerializeToElement(
            new AdapterCustomPayload("value"),
            MafWorkflowCheckpointProtocol.JsonOptions);
        var restored = payload.Deserialize<AdapterCustomPayload>(MafWorkflowCheckpointProtocol.JsonOptions);

        Assert.True(MafWorkflowCheckpointProtocol.JsonOptions.IsReadOnly);
        Assert.True(MafWorkflowCheckpointProtocol.JsonOptions.AllowOutOfOrderMetadataProperties);
        Assert.Equal(new AdapterCustomPayload("value"), restored);
    }

    [Fact]
    public void Checkpoint_protocol_round_trips_approval_value_objects_without_defaulting_them()
    {
        var values = new ApprovalCheckpointValues(
            WorkflowExecutorApprovalRequestId.New(),
            WorkflowExecutorApprovalToken.New(),
            WorkflowExecutorInputHash.Compute(new WorkflowNodeInput("{\"value\":1}")));

        var payload = JsonSerializer.SerializeToElement(
            values,
            MafWorkflowCheckpointProtocol.JsonOptions);
        var restored = payload.Deserialize<ApprovalCheckpointValues>(
            MafWorkflowCheckpointProtocol.JsonOptions);

        Assert.Equal(values, restored);
    }

    private static WorkflowBackendCheckpointSession CreateSession(string sessionId)
        => new(
            new WorkflowBackendSessionId(sessionId),
            WorkflowRunId.New(),
            WorkflowId.New(),
            WorkflowVersionId.New(),
            WorkflowRuntimeBackendKind.InProcess,
            MafWorkflowCheckpointProtocol.Format,
            MafWorkflowCheckpointProtocol.FormatVersion,
            MafWorkflowCheckpointProtocol.CompilerContractVersion,
            WorkflowTopologyFingerprint.Create("topology"));

    private sealed record AdapterCustomPayload(string Value);

    private sealed record ApprovalCheckpointValues(
        WorkflowExecutorApprovalRequestId RequestId,
        WorkflowExecutorApprovalToken Token,
        WorkflowExecutorInputHash InputHash);
}
