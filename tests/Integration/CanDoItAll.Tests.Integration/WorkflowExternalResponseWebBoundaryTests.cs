using System.Text;
using System.Text.Json;
using System.Security.Claims;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Http;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkflowExternalResponseWebBoundaryTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    public static IEnumerable<object[]> OutcomeStatusCases()
    {
        foreach (var outcome in Enum.GetValues<WorkflowExternalResponseServiceOutcome>())
        {
            yield return [outcome, ExpectedStatus(outcome)];
        }
    }

    [Fact]
    public async Task RequestReader_AcceptsTypedJsonAndPreservesResponseValue()
    {
        var context = CreateJsonContext(
            """
            {
              "expectedRequestVersion": 3,
              "response": {
                "approved": true,
                "message": "Reviewed."
              }
            }
            """);

        var result = await WorkflowExternalResponseRequestReader.ReadAsync(
            context.Request,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Request!.ExpectedRequestVersion);
        Assert.True(result.Request.Response.GetProperty("approved").GetBoolean());
    }

    [Theory]
    [InlineData("{}", (int)WorkflowExternalResponseRequestReadFailure.InvalidContract)]
    [InlineData("[]", (int)WorkflowExternalResponseRequestReadFailure.InvalidContract)]
    [InlineData("{\"expectedRequestVersion\":0,\"response\":{}}", (int)WorkflowExternalResponseRequestReadFailure.InvalidContract)]
    [InlineData("{\"expectedRequestVersion\":1,\"response\":{},\"actor\":\"spoof\"}", (int)WorkflowExternalResponseRequestReadFailure.InvalidContract)]
    [InlineData("{\"expectedRequestVersion\":1,\"expectedRequestVersion\":2,\"response\":{}}", (int)WorkflowExternalResponseRequestReadFailure.DuplicateProperty)]
    [InlineData("{\"expectedRequestVersion\":1,\"response\":{\"approved\":true,\"Approved\":false}}", (int)WorkflowExternalResponseRequestReadFailure.DuplicateProperty)]
    [InlineData("{\"expectedRequestVersion\":1,\"response\":", (int)WorkflowExternalResponseRequestReadFailure.InvalidJson)]
    public async Task RequestReader_RejectsAmbiguousOrInvalidBodies(
        string json,
        int expectedFailure)
    {
        var context = CreateJsonContext(json);

        var result = await WorkflowExternalResponseRequestReader.ReadAsync(
            context.Request,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal((WorkflowExternalResponseRequestReadFailure)expectedFailure, result.Failure);
    }

    [Fact]
    public async Task RequestReader_RejectsUnsupportedContentType()
    {
        var context = CreateJsonContext(
            "{\"expectedRequestVersion\":1,\"response\":{}}",
            "text/plain");

        var result = await WorkflowExternalResponseRequestReader.ReadAsync(
            context.Request,
            CancellationToken.None);

        Assert.Equal(
            WorkflowExternalResponseRequestReadFailure.UnsupportedContentType,
            result.Failure);
    }

    [Fact]
    public async Task RequestReader_RejectsOverLimitContentLengthBeforeReading()
    {
        var context = CreateJsonContext("{}");
        context.Request.ContentLength = WorkflowExternalResponseRequestReader.MaximumBodyBytes + 1;

        var result = await WorkflowExternalResponseRequestReader.ReadAsync(
            context.Request,
            CancellationToken.None);

        Assert.Equal(WorkflowExternalResponseRequestReadFailure.BodyTooLarge, result.Failure);
    }

    [Fact]
    public async Task RequestReader_RejectsOverLimitChunkedBody()
    {
        var context = CreateJsonContext(
            new string(' ', WorkflowExternalResponseRequestReader.MaximumBodyBytes + 1));
        context.Request.ContentLength = null;

        var result = await WorkflowExternalResponseRequestReader.ReadAsync(
            context.Request,
            CancellationToken.None);

        Assert.Equal(WorkflowExternalResponseRequestReadFailure.BodyTooLarge, result.Failure);
    }

    [Fact]
    public async Task RequestReader_RejectsExcessiveJsonDepth()
    {
        var response = string.Concat(
            new string('[', 34),
            "0",
            new string(']', 34));
        var context = CreateJsonContext(
            $"{{\"expectedRequestVersion\":1,\"response\":{response}}}");

        var result = await WorkflowExternalResponseRequestReader.ReadAsync(
            context.Request,
            CancellationToken.None);

        Assert.Equal(WorkflowExternalResponseRequestReadFailure.InvalidJson, result.Failure);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("one,two")]
    public void IdempotencyParser_RejectsMissingOrAmbiguousValues(string? value)
    {
        var context = new DefaultHttpContext();
        if (value is not null)
        {
            context.Request.Headers[WorkflowExternalResponseIdempotencyKeyParser.HeaderName] = value;
        }

        var result = WorkflowExternalResponseIdempotencyKeyParser.Parse(context.Request);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void IdempotencyParser_RejectsMultipleHeaderValues()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Append(
            WorkflowExternalResponseIdempotencyKeyParser.HeaderName,
            "first");
        context.Request.Headers.Append(
            WorkflowExternalResponseIdempotencyKeyParser.HeaderName,
            "second");

        var result = WorkflowExternalResponseIdempotencyKeyParser.Parse(context.Request);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void IdempotencyParser_AcceptsOneBoundedValue()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[WorkflowExternalResponseIdempotencyKeyParser.HeaderName] = "stable-key";

        var result = WorkflowExternalResponseIdempotencyKeyParser.Parse(context.Request);

        Assert.True(result.Succeeded);
        Assert.Equal("stable-key", result.Key!.Value.Value);
    }

    [Theory]
    [MemberData(nameof(OutcomeStatusCases))]
    public void ResultMapper_MapsEveryServiceOutcomeWithoutBadGateway(
        WorkflowExternalResponseServiceOutcome outcome,
        int expectedStatus)
    {
        var result = WorkflowExternalResponseApiMapper.Map(
            new WorkflowExternalResponseServiceResult(
                outcome,
                Operation: null,
                Run: null,
                Request: null,
                NextRequest: null,
                Replayed: false,
                "Safe status."));

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(expectedStatus, statusResult.StatusCode);
        Assert.NotEqual(StatusCodes.Status502BadGateway, statusResult.StatusCode);
    }

    [Fact]
    public void SafeProjections_ExcludeRawAndProtectedDomainFields()
    {
        var runId = WorkflowRunId.New();
        var workflowId = WorkflowId.New();
        var versionId = WorkflowVersionId.New();
        var requestId = WorkflowExternalRequestId.New();
        var nodeId = new WorkflowNodeId("review");
        var run = new WorkflowRunSnapshot(
            runId,
            workflowId,
            versionId,
            WorkflowRunState.WaitingForInput,
            WorkflowRuntimeBackendKind.InProcess,
            "secret-backend-run-id",
            "Waiting safely. Authorization: Bearer super-secret-run",
            FixedUtcNow,
            FixedUtcNow)
        {
            Origin = new WorkflowLaunchOrigin.Api(
                new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "secret-origin-actor"),
                new WorkflowLaunchCorrelationId("secret-origin-correlation"))
        };
        var request = new WorkflowExternalRequestRecord(
            requestId,
            runId,
            WorkflowExternalRequestKind.Approval,
            nodeId,
            "approval-requested token=super-secret-request-event",
            "{\"secretRequest\":true}",
            "{\"secretResponse\":true}",
            FixedUtcNow,
            RespondedAtUtc: null)
        {
            Version = WorkflowExternalRequestVersion.Initial,
            State = WorkflowExternalRequestState.Pending
        };
        var operation = new WorkflowExternalResponseOperationRecord(
            WorkflowExternalResponseOperationId.New(),
            requestId,
            runId,
            WorkflowExternalRequestVersion.Initial,
            new WorkflowExternalResponseIdempotencyKeyHash(new string('a', 64)),
            new WorkflowExternalResponsePayloadHash(new string('b', 64)),
            new WorkflowExternalResponseActorScopeFingerprint(new string('c', 64)),
            new WorkflowExternalResponsePayload("{\"approved\":true,\"secret\":\"payload\"}"),
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "secret-operation-actor"),
            new WorkflowLaunchCorrelationId("secret-operation-correlation"),
            WorkflowExternalResponseOperationState.Completed,
            Attempt: 1,
            WorkflowExternalResponseOperationConcurrencyVersion.Initial,
            FixedUtcNow);
        var checkpoint = new WorkflowCheckpointRecord(
            WorkflowCheckpointId.New(),
            runId,
            workflowId,
            versionId,
            WorkflowRuntimeBackendKind.InProcess,
            WorkflowCheckpointKind.WaitingForInput,
            WorkflowCheckpointTrustBoundary.TrustedRuntimeState,
            WorkflowResumeAvailability.Available,
            nodeId,
            requestId,
            "secret-native-checkpoint",
            "secret-payload-reference",
            new string('d', 64),
            "Checkpoint safe summary. password=super-secret-checkpoint",
            "connectionString=super-secret-connection",
            FixedUtcNow,
            ResumedAtUtc: null);
        var artifact = new WorkflowArtifactRecord(
            WorkflowArtifactId.New(),
            runId,
            WorkflowArtifactKind.Json,
            nodeId,
            "result-token=super-secret-name.json",
            "application/json",
            "secret/storage/path",
            "Artifact safe summary. api_key=super-secret-artifact",
            FixedUtcNow);
        var workflowEvent = new WorkflowEventRecord(
            Guid.NewGuid(),
            runId,
            WorkflowEventKind.WaitingForInput,
            nodeId,
            "Safe event message. Bearer super-secret-event",
            "{\"secretEventPayload\":true}",
            FixedUtcNow);

        var json = JsonSerializer.Serialize(new
        {
            Run = WorkflowApiSafeProjection.Map(run),
            Request = WorkflowApiSafeProjection.Map(request),
            Operation = WorkflowApiSafeProjection.Map(
                new WorkflowExternalResponseServiceResult(
                    WorkflowExternalResponseServiceOutcome.Completed,
                    operation,
                    run,
                    request,
                    NextRequest: null,
                    Replayed: true,
                    "Completed safely.")),
            Checkpoint = WorkflowApiSafeProjection.Map(checkpoint),
            Artifact = WorkflowApiSafeProjection.Map(artifact),
            Event = WorkflowApiSafeProjection.Map(workflowEvent)
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.DoesNotContain("secret-backend-run-id", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-origin", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secretRequest", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secretResponse", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-operation", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-native-checkpoint", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-payload-reference", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret/storage/path", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secretEventPayload", json, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('a', 64), json, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('b', 64), json, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('c', 64), json, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", json, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ActorResolver_FailsClosedForMalformedOrImpossibleJwtTimestamps()
    {
        var invalidClaims = new[]
        {
            new Claim("iat", "not-a-timestamp"),
            new Claim("exp", long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim("iat", FixedUtcNow.AddMinutes(1).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim("exp", FixedUtcNow.AddMinutes(-1).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture))
        };

        foreach (var invalidClaim in invalidClaims)
        {
            var identity = new ClaimsIdentity(
                [
                    new Claim("sub", "workflow-user"),
                    new Claim("scope", ApiAccessScopeNames.RespondWorkflows),
                    invalidClaim
                ],
                authenticationType: "test");
            var resolver = new WorkflowExternalResponseApiActorResolver(
                new ThrowingDatabaseProfileRuntimeAccessor(),
                new FixedAgentExecutionProfileGenerationSource(new DatabaseProfileGeneration(1)),
                new FixedTimeProvider(FixedUtcNow));

            var actor = resolver.Resolve(new ClaimsPrincipal(identity));

            Assert.IsType<WorkflowExternalResponseActorContext.Unauthenticated>(actor);
        }
    }

    private static DefaultHttpContext CreateJsonContext(
        string json,
        string contentType = "application/json")
    {
        var body = Encoding.UTF8.GetBytes(json);
        var context = new DefaultHttpContext();
        context.Request.ContentType = contentType;
        context.Request.ContentLength = body.Length;
        context.Request.Body = new MemoryStream(body);
        return context;
    }

    private static int ExpectedStatus(WorkflowExternalResponseServiceOutcome outcome)
        => outcome switch
        {
            WorkflowExternalResponseServiceOutcome.Completed or
            WorkflowExternalResponseServiceOutcome.WaitingAgain or
            WorkflowExternalResponseServiceOutcome.Denied => StatusCodes.Status200OK,
            WorkflowExternalResponseServiceOutcome.Resuming => StatusCodes.Status202Accepted,
            WorkflowExternalResponseServiceOutcome.InvalidResponse => StatusCodes.Status400BadRequest,
            WorkflowExternalResponseServiceOutcome.Unauthenticated => StatusCodes.Status401Unauthorized,
            WorkflowExternalResponseServiceOutcome.Forbidden or
            WorkflowExternalResponseServiceOutcome.AuthorizationContextUnavailable => StatusCodes.Status403Forbidden,
            WorkflowExternalResponseServiceOutcome.RequestNotFound or
            WorkflowExternalResponseServiceOutcome.RunNotFound or
            WorkflowExternalResponseServiceOutcome.OperationNotFound => StatusCodes.Status404NotFound,
            WorkflowExternalResponseServiceOutcome.RequestVersionMismatch or
            WorkflowExternalResponseServiceOutcome.RequestNotPending or
            WorkflowExternalResponseServiceOutcome.RunNotWaiting or
            WorkflowExternalResponseServiceOutcome.IdempotencyConflict or
            WorkflowExternalResponseServiceOutcome.ActiveOperationConflict => StatusCodes.Status409Conflict,
            WorkflowExternalResponseServiceOutcome.Cancelled or
            WorkflowExternalResponseServiceOutcome.Superseded => StatusCodes.Status410Gone,
            WorkflowExternalResponseServiceOutcome.LegacyNonResumable or
            WorkflowExternalResponseServiceOutcome.CheckpointMissing or
            WorkflowExternalResponseServiceOutcome.CheckpointCorrupt or
            WorkflowExternalResponseServiceOutcome.CheckpointIncompatible or
            WorkflowExternalResponseServiceOutcome.TopologyMismatch or
            WorkflowExternalResponseServiceOutcome.WorkflowVersionMismatch or
            WorkflowExternalResponseServiceOutcome.RequestMismatch => StatusCodes.Status422UnprocessableEntity,
            WorkflowExternalResponseServiceOutcome.BackendUnavailable or
            WorkflowExternalResponseServiceOutcome.RetryableFailure => StatusCodes.Status503ServiceUnavailable,
            WorkflowExternalResponseServiceOutcome.TerminalFailure => StatusCodes.Status500InternalServerError,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown outcome.")
        };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class ThrowingDatabaseProfileRuntimeAccessor : IDatabaseProfileRuntimeAccessor
    {
        public ResolvedDatabaseProfile ResolveCurrentProfile()
            => throw new InvalidOperationException("Invalid timestamp claims must fail before profile resolution.");

        public ResolvedDatabaseProfile ResolveProfile(Guid profileId)
            => throw new InvalidOperationException("Invalid timestamp claims must fail before profile resolution.");
    }
}
