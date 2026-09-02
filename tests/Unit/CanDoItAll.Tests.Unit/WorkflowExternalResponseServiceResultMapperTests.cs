using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowExternalResponseServiceResultMapperTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-21T12:00:00Z");

    [Theory]
    [InlineData(WorkflowExternalResponseOperationState.Accepted, WorkflowExternalResponseOperationOutcomeCode.None, WorkflowExternalResponseServiceOutcome.Resuming)]
    [InlineData(WorkflowExternalResponseOperationState.WaitingAgain, WorkflowExternalResponseOperationOutcomeCode.WaitingAgain, WorkflowExternalResponseServiceOutcome.WaitingAgain)]
    [InlineData(WorkflowExternalResponseOperationState.Completed, WorkflowExternalResponseOperationOutcomeCode.Completed, WorkflowExternalResponseServiceOutcome.Completed)]
    [InlineData(WorkflowExternalResponseOperationState.Denied, WorkflowExternalResponseOperationOutcomeCode.Denied, WorkflowExternalResponseServiceOutcome.Denied)]
    [InlineData(WorkflowExternalResponseOperationState.Cancelled, WorkflowExternalResponseOperationOutcomeCode.Cancelled, WorkflowExternalResponseServiceOutcome.Cancelled)]
    [InlineData(WorkflowExternalResponseOperationState.FailedRetryable, WorkflowExternalResponseOperationOutcomeCode.BackendUnavailable, WorkflowExternalResponseServiceOutcome.BackendUnavailable)]
    [InlineData(WorkflowExternalResponseOperationState.FailedRetryable, WorkflowExternalResponseOperationOutcomeCode.ResumeFailed, WorkflowExternalResponseServiceOutcome.RetryableFailure)]
    [InlineData(WorkflowExternalResponseOperationState.FailedTerminal, WorkflowExternalResponseOperationOutcomeCode.CheckpointCorrupt, WorkflowExternalResponseServiceOutcome.CheckpointCorrupt)]
    public void MapOperation_MapsStableDomainStateWithoutStoreOrHost(
        WorkflowExternalResponseOperationState state,
        WorkflowExternalResponseOperationOutcomeCode operationOutcome,
        WorkflowExternalResponseServiceOutcome expectedOutcome)
    {
        var context = CreateContext(state, operationOutcome);
        var mapper = new WorkflowExternalResponseServiceResultMapper();

        var result = mapper.MapOperation(
            context.Operation,
            context.Run,
            context.Request,
            nextRequest: null,
            replayed: false,
            safeMessage: null);

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Same(context.Operation, result.Operation);
        Assert.Same(context.Request, result.Request);
        Assert.False(result.Replayed);
        Assert.False(string.IsNullOrWhiteSpace(result.SafeMessage));
    }

    [Theory]
    [InlineData(WorkflowExternalRequestAuthorizationOutcome.Unauthenticated, WorkflowExternalResponseServiceOutcome.Unauthenticated)]
    [InlineData(WorkflowExternalRequestAuthorizationOutcome.AuthorizationContextUnavailable, WorkflowExternalResponseServiceOutcome.AuthorizationContextUnavailable)]
    [InlineData(WorkflowExternalRequestAuthorizationOutcome.ScopeMismatch, WorkflowExternalResponseServiceOutcome.Forbidden)]
    public void AuthorizationFailure_MapsOnlyThePublicOutcome(
        WorkflowExternalRequestAuthorizationOutcome authorizationOutcome,
        WorkflowExternalResponseServiceOutcome expectedOutcome)
    {
        var context = CreateContext();
        var mapper = new WorkflowExternalResponseServiceResultMapper();

        var result = mapper.AuthorizationFailure(
            new WorkflowExternalRequestAuthorizationDecision(
                authorizationOutcome,
                Authorization: null,
                "Safe authorization failure."),
            context.Run,
            context.Request);

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal("Safe authorization failure.", result.SafeMessage);
    }

    [Theory]
    [InlineData(WorkflowExternalResponseValidationOutcome.RequestVersionMismatch, WorkflowExternalResponseServiceOutcome.RequestVersionMismatch)]
    [InlineData(WorkflowExternalResponseValidationOutcome.BoundaryMismatch, WorkflowExternalResponseServiceOutcome.RequestMismatch)]
    [InlineData(WorkflowExternalResponseValidationOutcome.SchemaMismatch, WorkflowExternalResponseServiceOutcome.InvalidResponse)]
    public void ValidationFailure_MapsOnlyThePublicOutcome(
        WorkflowExternalResponseValidationOutcome validationOutcome,
        WorkflowExternalResponseServiceOutcome expectedOutcome)
    {
        var context = CreateContext();
        var mapper = new WorkflowExternalResponseServiceResultMapper();

        var result = mapper.ValidationFailure(
            new WorkflowExternalResponseValidationResult(
                validationOutcome,
                CanonicalPayload: null,
                Action: null,
                "Safe validation failure."),
            context.Run,
            context.Request);

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal("Safe validation failure.", result.SafeMessage);
    }

    [Fact]
    public void ContinuationFailure_PreservesSafeContextWithoutAnOperation()
    {
        var context = CreateContext();
        var mapper = new WorkflowExternalResponseServiceResultMapper();

        var result = mapper.ContinuationFailure(
            new WorkflowExternalResponseContinuationResult(
                WorkflowExternalResponseContinuationOutcome.FailedRetryable,
                Operation: null,
                Run: null,
                NextRequest: null,
                "Retry safely."),
            context.Run,
            context.Request);

        Assert.Equal(WorkflowExternalResponseServiceOutcome.RetryableFailure, result.Outcome);
        Assert.Same(context.Run, result.Run);
        Assert.Same(context.Request, result.Request);
        Assert.Equal("Retry safely.", result.SafeMessage);
    }

    private static MappingContext CreateContext(
        WorkflowExternalResponseOperationState state = WorkflowExternalResponseOperationState.Completed,
        WorkflowExternalResponseOperationOutcomeCode outcome = WorkflowExternalResponseOperationOutcomeCode.Completed)
    {
        var run = new WorkflowRunSnapshot(
            WorkflowRunId.New(),
            WorkflowId.New(),
            WorkflowVersionId.New(),
            WorkflowRunState.Completed,
            WorkflowRuntimeBackendKind.InProcess,
            "mapper-run",
            "Mapped safely.",
            Now.AddMinutes(-1),
            Now)
        {
            TerminalAtUtc = Now
        };
        var request = new WorkflowExternalRequestRecord(
            WorkflowExternalRequestId.New(),
            run.RunId,
            WorkflowExternalRequestKind.HumanInput,
            new WorkflowNodeId("mapper-node"),
            "HumanInputRequested",
            "{}",
            "{}",
            Now.AddMinutes(-1),
            Now)
        {
            Version = WorkflowExternalRequestVersion.Initial,
            State = WorkflowExternalRequestState.Responded
        };
        var operation = new WorkflowExternalResponseOperationRecord(
            WorkflowExternalResponseOperationId.New(),
            request.Id,
            run.RunId,
            request.Version,
            new WorkflowExternalResponseIdempotencyKeyHash(new string('1', 64)),
            new WorkflowExternalResponsePayloadHash(new string('2', 64)),
            new WorkflowExternalResponseActorScopeFingerprint(new string('3', 64)),
            new WorkflowExternalResponsePayload("{}"),
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "mapper-user"),
            new WorkflowLaunchCorrelationId("mapper-correlation"),
            state,
            Attempt: 1,
            WorkflowExternalResponseOperationConcurrencyVersion.Initial,
            Now.AddSeconds(-5))
        {
            OutcomeCode = outcome,
            CompletedAtUtc = state is WorkflowExternalResponseOperationState.Accepted or
                WorkflowExternalResponseOperationState.Claimed or
                WorkflowExternalResponseOperationState.Resuming
                    ? null
                    : Now
        };
        return new MappingContext(run, request, operation);
    }

    private sealed record MappingContext(
        WorkflowRunSnapshot Run,
        WorkflowExternalRequestRecord Request,
        WorkflowExternalResponseOperationRecord Operation);
}
