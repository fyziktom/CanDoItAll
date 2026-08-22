using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowExternalResponseValidatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-21T12:00:00Z");

    [Theory]
    [InlineData("{\"approved\":true}", WorkflowExternalResponseAction.Approve)]
    [InlineData("{\"MESSAGE\":\"Reviewed\",\"APPROVED\":false}", WorkflowExternalResponseAction.Deny)]
    public void Validate_ApprovalPayload_DerivesTypedAction(
        string responseJson,
        WorkflowExternalResponseAction expectedAction)
    {
        var context = CreateContext(WorkflowExternalRequestKind.Approval);

        var result = Validate(context, responseJson);

        Assert.True(result.Succeeded, result.SafeMessage);
        Assert.Equal(expectedAction, result.Action);
        Assert.NotNull(result.CanonicalPayload);
    }

    [Theory]
    [InlineData("{\"approved\":true,\"Approved\":false}")]
    [InlineData("{\"approved\":true,\"unexpected\":1}")]
    [InlineData("{\"approved\":\"yes\"}")]
    [InlineData("{\"message\":\"missing decision\"}")]
    [InlineData("[]")]
    public void Validate_ApprovalPayload_RejectsAmbiguousOrInvalidShape(string responseJson)
    {
        var result = Validate(
            CreateContext(WorkflowExternalRequestKind.ToolApproval),
            responseJson);

        Assert.Equal(WorkflowExternalResponseValidationOutcome.SchemaMismatch, result.Outcome);
        Assert.Null(result.Action);
        Assert.Null(result.CanonicalPayload);
    }

    [Fact]
    public void Validate_ApprovalMessageOverBound_IsRejected()
    {
        var response = JsonSerializer.Serialize(new
        {
            approved = true,
            message = new string('a', WorkflowExternalResponseValidator.MaximumApprovalMessageLength + 1)
        });

        var result = Validate(
            CreateContext(
                WorkflowExternalRequestKind.Approval,
                maximumPayloadBytes: 8_192),
            response);

        Assert.Equal(WorkflowExternalResponseValidationOutcome.SchemaMismatch, result.Outcome);
    }

    [Fact]
    public void Validate_HumanInput_UsesStrictPersistedSchema()
    {
        const string schema = """
            {
              "type": "object",
              "properties": {
                "answer": { "type": "string" }
              },
              "required": ["answer"],
              "additionalProperties": false
            }
            """;
        var context = CreateContext(WorkflowExternalRequestKind.HumanInput, schema);

        var valid = Validate(context, "{\"answer\":\"yes\"}");
        var invalid = Validate(context, "{\"answer\":1}");

        Assert.True(valid.Succeeded, valid.SafeMessage);
        Assert.Equal(WorkflowExternalResponseAction.SubmitInput, valid.Action);
        Assert.Equal(WorkflowExternalResponseValidationOutcome.SchemaMismatch, invalid.Outcome);
    }

    [Fact]
    public void Validate_ExpectedVersionMismatch_IsRejected()
    {
        var context = CreateContext(WorkflowExternalRequestKind.Approval);
        var validation = CreateValidationRequest(context, "{\"approved\":true}") with
        {
            ExpectedRequestVersion = new WorkflowExternalRequestVersion(2)
        };

        var result = new WorkflowExternalResponseValidator().Validate(validation);

        Assert.Equal(WorkflowExternalResponseValidationOutcome.RequestVersionMismatch, result.Outcome);
    }

    [Fact]
    public void Validate_BoundaryRequestLinkMismatch_IsRejected()
    {
        var context = CreateContext(WorkflowExternalRequestKind.Approval);
        context = context with
        {
            Boundary = context.Boundary with { RequestId = WorkflowExternalRequestId.New() }
        };

        var result = Validate(context, "{\"approved\":true}");

        Assert.Equal(WorkflowExternalResponseValidationOutcome.BoundaryMismatch, result.Outcome);
    }

    [Fact]
    public void Validate_ContractVersionMismatch_IsRejected()
    {
        var context = CreateContext(WorkflowExternalRequestKind.HumanInput);
        context = context with
        {
            Boundary = context.Boundary with
            {
                ResponseContract = new WorkflowExternalResponseContract(
                    WorkflowExternalRequestKind.HumanInput,
                    context.Boundary.ResponseContract.SchemaId,
                    schemaVersion: 2,
                    context.Boundary.ResponseContract.SchemaJson,
                    context.Boundary.ResponseContract.MaximumPayloadBytes)
            }
        };

        var result = Validate(context, "{}");

        Assert.Equal(WorkflowExternalResponseValidationOutcome.InvalidContract, result.Outcome);
    }

    [Fact]
    public void Validate_Utf8PayloadLimit_IsEnforced()
    {
        var context = CreateContext(
            WorkflowExternalRequestKind.HumanInput,
            maximumPayloadBytes: 8);

        var result = Validate(context, "{\"value\":1}");

        Assert.Equal(WorkflowExternalResponseValidationOutcome.PayloadTooLarge, result.Outcome);
    }

    [Fact]
    public void Validate_MaximumJsonDepth_IsEnforced()
    {
        var nested = string.Concat(
            Enumerable.Repeat("{\"value\":", WorkflowExternalResponseValidator.MaximumJsonDepth + 1)) +
            "true" +
            new string('}', WorkflowExternalResponseValidator.MaximumJsonDepth + 1);
        var context = CreateContext(
            WorkflowExternalRequestKind.HumanInput,
            maximumPayloadBytes: 8_192);

        var result = Validate(context, nested, maximumDocumentDepth: 128);

        Assert.Equal(WorkflowExternalResponseValidationOutcome.MaximumDepthExceeded, result.Outcome);
    }

    private static WorkflowExternalResponseValidationResult Validate(
        ValidationContext context,
        string responseJson,
        int maximumDocumentDepth = 64)
        => new WorkflowExternalResponseValidator().Validate(
            CreateValidationRequest(context, responseJson, maximumDocumentDepth));

    private static WorkflowExternalResponseValidationRequest CreateValidationRequest(
        ValidationContext context,
        string responseJson,
        int maximumDocumentDepth = 64)
    {
        using var document = JsonDocument.Parse(
            responseJson,
            new JsonDocumentOptions { MaxDepth = maximumDocumentDepth });
        return new WorkflowExternalResponseValidationRequest(
            context.Run,
            context.Request,
            context.Boundary,
            context.Request.Version,
            document.RootElement.Clone());
    }

    private static ValidationContext CreateContext(
        WorkflowExternalRequestKind kind,
        string schemaJson = "{}",
        int maximumPayloadBytes = 1_024)
    {
        var requestId = WorkflowExternalRequestId.New();
        var runId = WorkflowRunId.New();
        var continuation = new WorkflowExternalRequestContinuation(
            new WorkflowBackendExternalRequestLink(
                requestId,
                new WorkflowBackendRequestId("backend-request"),
                new WorkflowBackendRequestPortId("response")),
            new WorkflowBackendCheckpointLink(
                new WorkflowBackendSessionId("session"),
                new WorkflowBackendCheckpointId("checkpoint")),
            new WorkflowCompilerContractVersion(1),
            WorkflowTopologyFingerprint.Create("test-topology"),
            WorkflowBackendCheckpointPayloadHash.Compute("{}"));
        var responseContract = new WorkflowExternalResponseContract(
            kind,
            $"CanDoItAll.Workflow{kind}Response/v1",
            schemaVersion: 1,
            schemaJson,
            maximumPayloadBytes);
        var request = new WorkflowExternalRequestRecord(
            requestId,
            runId,
            kind,
            new WorkflowNodeId("external-request"),
            "ExternalRequest",
            "{}",
            string.Empty,
            Now,
            RespondedAtUtc: null)
        {
            Version = WorkflowExternalRequestVersion.Initial,
            State = WorkflowExternalRequestState.Pending,
            ResponseContract = responseContract,
            Continuation = continuation
        };
        Assert.True(WorkflowExternalRequestBoundaryRecord.TryCreate(request, out var boundary));
        var run = new WorkflowRunSnapshot(
            runId,
            WorkflowId.New(),
            WorkflowVersionId.New(),
            WorkflowRunState.WaitingForInput,
            WorkflowRuntimeBackendKind.InProcess,
            "backend-run",
            "Waiting",
            Now,
            Now);
        return new ValidationContext(run, request, boundary!);
    }

    private sealed record ValidationContext(
        WorkflowRunSnapshot Run,
        WorkflowExternalRequestRecord Request,
        WorkflowExternalRequestBoundaryRecord Boundary);
}
