using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Web.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record WorkflowExternalResponseApiRequest(
    long ExpectedRequestVersion,
    JsonElement Response);

internal sealed record WorkflowExternalResponseApiResponse(
    Guid? OperationId,
    Guid? RequestId,
    long? ExpectedRequestVersion,
    Guid? RunId,
    WorkflowExternalResponseServiceOutcome Outcome,
    WorkflowExternalResponseOperationState? OperationState,
    WorkflowExternalResponseOperationOutcomeCode? OperationOutcome,
    WorkflowRunState? RunState,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    bool Replayed,
    string Message,
    WorkflowPendingExternalRequestApiResponse? NextPendingRequest);

internal sealed record WorkflowPendingExternalRequestApiResponse(
    Guid Id,
    Guid RunId,
    WorkflowExternalRequestKind Kind,
    string NodeId,
    string EventName,
    long Version,
    WorkflowExternalRequestState State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RespondedAtUtc,
    string? Prompt,
    WorkflowExternalResponseContractApiResponse? ResponseContract);

internal sealed record WorkflowExternalResponseContractApiResponse(
    string SchemaId,
    int SchemaVersion,
    JsonElement? Schema,
    bool SchemaAvailable,
    int MaximumPayloadBytes);

internal sealed record WorkflowRunApiResponse(
    Guid RunId,
    Guid WorkflowId,
    Guid VersionId,
    WorkflowRunState State,
    WorkflowRuntimeBackendKind Backend,
    string Summary,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? TerminalAtUtc);

internal sealed record WorkflowEventApiResponse(
    Guid Id,
    Guid RunId,
    WorkflowEventKind Kind,
    string? NodeId,
    string Message,
    DateTimeOffset CreatedAtUtc);

internal sealed record WorkflowArtifactApiResponse(
    Guid Id,
    Guid RunId,
    WorkflowArtifactKind Kind,
    string? NodeId,
    string Name,
    string ContentType,
    string Summary,
    DateTimeOffset CreatedAtUtc);

internal sealed record WorkflowCheckpointApiResponse(
    Guid Id,
    Guid RunId,
    Guid WorkflowId,
    Guid VersionId,
    WorkflowRuntimeBackendKind Backend,
    WorkflowCheckpointKind Kind,
    WorkflowCheckpointTrustBoundary TrustBoundary,
    WorkflowResumeAvailability ResumeAvailability,
    string? NodeId,
    Guid? ExternalRequestId,
    string Summary,
    string ResumeUnavailableReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ResumedAtUtc);

internal sealed record WorkflowRunDetailApiResponse(
    WorkflowRunApiResponse Run,
    IReadOnlyList<WorkflowEventApiResponse> Events,
    IReadOnlyList<WorkflowArtifactApiResponse> Artifacts,
    IReadOnlyList<WorkflowPendingExternalRequestApiResponse> PendingExternalRequests,
    IReadOnlyList<WorkflowCheckpointApiResponse> Checkpoints);

internal sealed record WorkflowRunStartApiResponse(
    WorkflowRunApiResponse Run,
    IReadOnlyList<WorkflowEventApiResponse> Events,
    IReadOnlyList<WorkflowArtifactApiResponse> Artifacts,
    IReadOnlyList<WorkflowPendingExternalRequestApiResponse> PendingExternalRequests,
    IReadOnlyList<WorkflowCheckpointApiResponse> Checkpoints,
    WorkflowLaunchIdempotencyDisposition IdempotencyDisposition,
    bool Created,
    bool Replayed)
{
    public static WorkflowRunStartApiResponse From(
        WorkflowRunDetailApiResponse detail,
        WorkflowLaunchIdempotencyDisposition disposition) => new(
            detail.Run,
            detail.Events,
            detail.Artifacts,
            detail.PendingExternalRequests,
            detail.Checkpoints,
            disposition,
            Created: disposition is not WorkflowLaunchIdempotencyDisposition.ReplayedExistingRun,
            Replayed: disposition is WorkflowLaunchIdempotencyDisposition.ReplayedExistingRun);
}
