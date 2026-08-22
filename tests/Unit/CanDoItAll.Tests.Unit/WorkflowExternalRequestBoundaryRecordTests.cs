using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowExternalRequestBoundaryRecordTests
{
    [Fact]
    public void TryCreate_NativeRequest_PreservesTrustedBoundaryAndHashesImmutableRequestJson()
    {
        var authorization = new WorkflowExternalRequestAuthorizationPolicySnapshot(
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "origin-actor"),
            new WorkflowExecutorId("governed.executor"),
            WorkflowExecutorCapabilityFlags.WritesExternalData,
            WorkflowExecutorApprovalRequirement.RequiredForExternalEffect,
            "approver");
        var request = CreateNativeRequest() with
        {
            AuthorizationPolicy = authorization
        };

        var created = WorkflowExternalRequestBoundaryRecord.TryCreate(request, out var boundary);

        Assert.True(created);
        Assert.NotNull(boundary);
        Assert.Equal(request.Id, boundary.RequestId);
        Assert.Equal(request.Version, boundary.RequestVersion);
        Assert.Equal(request.State, boundary.State);
        Assert.Same(request.ResponseContract, boundary.ResponseContract);
        Assert.Same(request.Continuation, boundary.Continuation);
        Assert.Same(authorization, boundary.AuthorizationPolicy);
        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(request.RequestJson))),
            boundary.RequestPayloadHash.Value);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void TryCreate_IncompleteOrLegacyRequest_ReturnsFalse(
        bool omitResponseContract,
        bool omitContinuation)
    {
        var native = CreateNativeRequest();
        var request = native with
        {
            ResponseContract = omitResponseContract ? null : native.ResponseContract,
            Continuation = omitContinuation ? null : native.Continuation
        };

        var created = WorkflowExternalRequestBoundaryRecord.TryCreate(request, out var boundary);

        Assert.False(created);
        Assert.Null(boundary);
    }

    [Fact]
    public void TryCreate_LegacyStateWithIncidentalMetadata_ReturnsFalse()
    {
        var native = CreateNativeRequest();
        var legacy = native with
        {
            State = WorkflowExternalRequestState.LegacyNonResumable
        };

        var created = WorkflowExternalRequestBoundaryRecord.TryCreate(legacy, out var boundary);

        Assert.False(created);
        Assert.Null(boundary);
    }

    [Fact]
    public void Continuation_JsonRoundTrip_PreservesStrongNativeIdentifiers()
    {
        var continuation = CreateNativeRequest().Continuation!;
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var json = JsonSerializer.Serialize(continuation, options);
        var restored = JsonSerializer.Deserialize<WorkflowExternalRequestContinuation>(json, options);

        Assert.Equal(continuation, restored);
    }

    private static WorkflowExternalRequestRecord CreateNativeRequest()
    {
        var requestId = WorkflowExternalRequestId.New();
        var runId = WorkflowRunId.New();
        var workflowId = WorkflowId.New();
        var versionId = WorkflowVersionId.New();
        var sessionId = new WorkflowBackendSessionId("session-1");
        var checkpointId = new WorkflowBackendCheckpointId("checkpoint-1");
        return new WorkflowExternalRequestRecord(
            requestId,
            runId,
            WorkflowExternalRequestKind.HumanInput,
            new WorkflowNodeId("human-input"),
            "request",
            """{"prompt":"Continue?"}""",
            string.Empty,
            DateTimeOffset.Parse("2026-08-20T12:00:00Z"),
            RespondedAtUtc: null)
        {
            Version = new WorkflowExternalRequestVersion(3),
            State = WorkflowExternalRequestState.Pending,
            ResponseContract = new WorkflowExternalResponseContract(
                WorkflowExternalRequestKind.HumanInput,
                "human-input-response",
                1,
                """{"type":"string"}""",
                4_096),
            Continuation = new WorkflowExternalRequestContinuation(
                new WorkflowBackendExternalRequestLink(
                    requestId,
                    new WorkflowBackendRequestId("native-request-1"),
                    new WorkflowBackendRequestPortId("native-port-1")),
                new WorkflowBackendCheckpointLink(sessionId, checkpointId),
                new WorkflowCompilerContractVersion(1),
                WorkflowTopologyFingerprint.Create($"{workflowId}:{versionId}"),
                WorkflowBackendCheckpointPayloadHash.Compute("{}"))
        };
    }
}
