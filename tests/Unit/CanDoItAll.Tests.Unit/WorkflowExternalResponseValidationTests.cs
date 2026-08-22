using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowExternalResponseValidationTests
{
    [Theory]
    [InlineData("{\"approved\":false,\"Approved\":true}")]
    [InlineData("{\"approved\":true,\"unexpected\":1}")]
    [InlineData("{\"approved\":\"yes\"}")]
    [InlineData("{\"message\":\"missing decision\"}")]
    [InlineData("[]")]
    public void ApprovalResponse_InvalidOrAmbiguousPayload_IsRejectedBeforeAcceptance(
        string responseJson)
    {
        var request = CreateRequest(WorkflowExternalRequestKind.Approval, maximumPayloadBytes: 1_024);

        var result = Validate(request, responseJson);

        Assert.Equal(WorkflowExternalResponseValidationOutcome.SchemaMismatch, result.Outcome);
    }

    [Fact]
    public void ApprovalResponse_ValidatedDecision_IsDerivedFromProtectedPayload()
    {
        var request = CreateRequest(WorkflowExternalRequestKind.Approval, maximumPayloadBytes: 1_024);
        const string responseJson = "{\"message\":\"No\",\"approved\":false}";

        var result = Validate(request, responseJson);

        Assert.True(result.Succeeded, result.SafeMessage);
        Assert.Equal(WorkflowExternalResponseAction.Deny, result.Action);
    }

    [Fact]
    public void HumanInput_ValidJsonRoot_IsAcceptedWithinPersistedSizeBoundary()
    {
        var request = CreateRequest(WorkflowExternalRequestKind.HumanInput, maximumPayloadBytes: 32);

        var result = Validate(request, "[1,true,null]");

        Assert.True(result.Succeeded, result.SafeMessage);
        Assert.Equal(WorkflowExternalResponseAction.SubmitInput, result.Action);
    }

    [Fact]
    public void Response_ExceedingPersistedSizeBoundary_IsRejected()
    {
        var request = CreateRequest(WorkflowExternalRequestKind.HumanInput, maximumPayloadBytes: 4);

        var result = Validate(request, "{\"value\":1}");

        Assert.Equal(WorkflowExternalResponseValidationOutcome.PayloadTooLarge, result.Outcome);
    }

    private static WorkflowExternalResponseValidationResult Validate(
        WorkflowExternalRequestRecord request,
        string responseJson)
    {
        Assert.True(WorkflowExternalRequestBoundaryRecord.TryCreate(request, out var boundary));
        using var response = JsonDocument.Parse(responseJson);
        var run = new WorkflowRunSnapshot(
            request.RunId,
            WorkflowId.New(),
            WorkflowVersionId.New(),
            WorkflowRunState.WaitingForInput,
            WorkflowRuntimeBackendKind.InProcess,
            "backend-run",
            "Waiting",
            request.CreatedAtUtc,
            request.CreatedAtUtc);
        return new WorkflowExternalResponseValidator().Validate(
            new WorkflowExternalResponseValidationRequest(
                run,
                request,
                boundary!,
                request.Version,
                response.RootElement.Clone()));
    }

    private static WorkflowExternalRequestRecord CreateRequest(
        WorkflowExternalRequestKind kind,
        int maximumPayloadBytes)
    {
        var requestId = WorkflowExternalRequestId.New();
        return new WorkflowExternalRequestRecord(
            requestId,
            WorkflowRunId.New(),
            kind,
            new WorkflowNodeId("external-request"),
            "external-request",
            "{}",
            string.Empty,
            DateTimeOffset.Parse("2026-08-20T12:00:00Z"),
            RespondedAtUtc: null)
        {
            State = WorkflowExternalRequestState.Pending,
            ResponseContract = new WorkflowExternalResponseContract(
                kind,
                kind is WorkflowExternalRequestKind.Approval or WorkflowExternalRequestKind.ToolApproval
                    ? "CanDoItAll.WorkflowApprovalResponse/v1"
                    : "CanDoItAll.WorkflowHumanInputResponse/v1",
                schemaVersion: 1,
                "{}",
                maximumPayloadBytes),
            Continuation = new WorkflowExternalRequestContinuation(
                new WorkflowBackendExternalRequestLink(
                    requestId,
                    new WorkflowBackendRequestId("backend-request"),
                    new WorkflowBackendRequestPortId("response")),
                new WorkflowBackendCheckpointLink(
                    new WorkflowBackendSessionId("session"),
                    new WorkflowBackendCheckpointId("checkpoint")),
                new WorkflowCompilerContractVersion(1),
                WorkflowTopologyFingerprint.Create("validation-baseline"),
                WorkflowBackendCheckpointPayloadHash.Compute("{}"))
        };
    }
}
