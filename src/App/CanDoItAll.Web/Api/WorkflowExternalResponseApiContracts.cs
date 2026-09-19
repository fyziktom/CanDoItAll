using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Web.Api;

/// <summary>
/// Body of <c>POST /api/workflows/external-requests/{requestId}/response</c>: the request version the caller read and
/// the response value. Exactly these two members are accepted, with these case-sensitive names; unknown, duplicate or
/// missing members are rejected with HTTP 400 before anything changes.
/// </summary>
/// <param name="ExpectedRequestVersion">
/// <c>version</c> of the pending request as last read, a positive JSON integer. When the request has moved to another
/// version, the submission is rejected with HTTP 409 (<c>outcome</c> 10 RequestVersionMismatch).
/// </param>
/// <param name="Response">
/// The answer as a JSON value, not a string containing encoded JSON. For approval and tool-approval requests an object
/// with the boolean <c>approved</c> and an optional <c>message</c> string of at most 4,096 characters, for example
/// <c>{ "approved": false, "message": "Budget exceeded." }</c>; for human-input requests a value that satisfies the
/// request's <c>responseContract.schema</c>. Its JSON text must fit <c>responseContract.maximumPayloadBytes</c> UTF-8
/// bytes.
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record WorkflowExternalResponseApiRequest(
    long ExpectedRequestVersion,
    JsonElement Response);

/// <summary>
/// Status of an attempt to answer a workflow external request, returned for every outcome, success or failure, by
/// <c>POST /api/workflows/external-requests/{requestId}/response</c> and
/// <c>GET /api/workflows/external-response-operations/{operationId}</c>. Branch on <c>outcome</c>; members that do not
/// apply are null, so a request rejected before any lookup carries only <c>outcome</c>, <c>replayed</c> and
/// <c>message</c>. It is an allowlisted projection: it never contains the raw request or response JSON, event
/// payloads, checkpoint references or hashes, artifact storage paths, the idempotency key, leases or authorization
/// material.
/// </summary>
/// <param name="OperationId">
/// Identifier of the answer attempt, for <c>GET /api/workflows/external-response-operations/{operationId}</c>. Null
/// when no attempt was recorded or found, for example when the body was rejected.
/// </param>
/// <param name="RequestId">Identifier of the external request that was answered; null when it is unknown.</param>
/// <param name="ExpectedRequestVersion">
/// Request version the attempt was made for or, when no attempt was recorded, the request's current version; null when
/// unknown.
/// </param>
/// <param name="RunId">Identifier of the workflow run the request belongs to; null when unknown.</param>
/// <param name="Outcome">
/// Result of this call, as a JSON integer: 0 Completed, 1 WaitingAgain, 2 Denied, 3 Resuming, 4 Unauthenticated,
/// 5 Forbidden, 6 InvalidResponse, 7 RequestNotFound, 8 RunNotFound, 9 OperationNotFound, 10 RequestVersionMismatch,
/// 11 RequestNotPending, 12 RunNotWaiting, 13 IdempotencyConflict, 14 ActiveOperationConflict, 15 Cancelled,
/// 16 Superseded, 17 LegacyNonResumable, 18 AuthorizationContextUnavailable, 19 CheckpointMissing,
/// 20 CheckpointCorrupt, 21 CheckpointIncompatible, 22 TopologyMismatch, 23 WorkflowVersionMismatch,
/// 24 RequestMismatch, 25 BackendUnavailable, 26 RetryableFailure, 27 TerminalFailure. The HTTP status follows from
/// it; the two operations describe the mapping.
/// </param>
/// <param name="OperationState">
/// Stored state of the attempt, as a JSON integer: 0 Accepted, 1 Claimed, 2 Resuming (processing has not finished),
/// 3 WaitingAgain, 4 Completed, 5 Denied, 6 FailedRetryable (resend the submission with the same key to continue),
/// 7 FailedTerminal, 8 Cancelled. Null when no attempt was recorded.
/// </param>
/// <param name="OperationOutcome">
/// Detailed result code of the attempt, as a JSON integer: 0 None (no final result yet), 1 WaitingAgain,
/// 2 Completed, 3 Denied, 4 Cancelled, 5 BackendUnavailable, 6 ResumeFailed, 7 CheckpointMissing,
/// 8 CheckpointCorrupt, 9 CheckpointIncompatible, 10 TopologyMismatch, 11 WorkflowVersionMismatch,
/// 12 RequestMismatch, 13 ResponseRejected, 14 AttemptLimitReached. Null when no attempt was recorded.
/// </param>
/// <param name="RunState">
/// State of the run when this answer was produced, as a JSON integer: 0 NotStarted, 1 Running, 2 WaitingForInput,
/// 3 Idle, 4 Completed, 5 Failed, 6 Cancelled. Null when the run is unknown.
/// </param>
/// <param name="AcceptedAtUtc">
/// Time the attempt was accepted, as an instant with offset; null without an attempt.
/// </param>
/// <param name="StartedAtUtc">Time processing of the attempt started; null until then or without an attempt.</param>
/// <param name="CompletedAtUtc">
/// Time the attempt reached its final state; null while it has not or without an attempt.
/// </param>
/// <param name="Replayed">
/// True when this submission repeated an earlier one with the same key and body and returned its attempt instead of
/// creating one. Always false when the attempt is read with the operation read.
/// </param>
/// <param name="Message">
/// Human-readable explanation, redacted and at most 4,096 characters; a fixed text for unclassified failures. Do not
/// parse it.
/// </param>
/// <param name="NextPendingRequest">
/// The next external request the run waits for after this answer, typically with <c>outcome</c> 1 WaitingAgain;
/// answer it in the same way. Null otherwise.
/// </param>
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

/// <summary>
/// External request of a workflow run: what a waiting run asks a person or approver for and how to answer it. Returned
/// by the pending-request, run-detail and start operations and as <c>nextPendingRequest</c>. It is an allowlisted
/// projection that never contains the request's prior-node context, raw request JSON, authorization policy, executor
/// arguments or checkpoint material.
/// </summary>
/// <param name="Id">
/// Identifier of the external request; use it in <c>POST /api/workflows/external-requests/{requestId}/response</c>.
/// </param>
/// <param name="RunId">Identifier of the run that waits for the request.</param>
/// <param name="Kind">
/// What the request asks for, as a JSON integer: 0 HumanInput (a value that satisfies
/// <c>responseContract.schema</c>), 1 Approval or 2 ToolApproval (both answered with <c>approved</c> and an optional
/// <c>message</c>).
/// </param>
/// <param name="NodeId">Identifier of the definition node that raised the request.</param>
/// <param name="EventName">
/// Name of the request event recorded by the runtime; informational, redacted and at most 4,096 characters.
/// </param>
/// <param name="Version">
/// Current version of the request, starting at 1. Send it as <c>expectedRequestVersion</c> when answering.
/// </param>
/// <param name="State">
/// State of the request, as a JSON integer: 0 Pending (accepts a response), 1 ResponseClaimed (an answer is being
/// processed), 2 Responded, 3 Denied, 4 Superseded (replaced by a newer request), 5 Cancelled, 6 LegacyNonResumable
/// (an old wait that this API cannot resume).
/// </param>
/// <param name="CreatedAtUtc">Time the run raised the request, as an instant with offset.</param>
/// <param name="RespondedAtUtc">Time a response was recorded; null while none was.</param>
/// <param name="Prompt">
/// The request's prompt text for the responder, redacted and at most 4,096 characters; null when the request has no
/// prompt text.
/// </param>
/// <param name="ResponseContract">
/// What a response must satisfy; null when the request has no usable response contract, for example a legacy request,
/// which cannot be answered through the API.
/// </param>
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

/// <summary>
/// Response contract of an external request, taken from the request's persisted boundary: the schema a response must
/// satisfy and its size limit. The schema itself is returned only when it is small enough for the public projection.
/// </summary>
/// <param name="SchemaId">
/// Identifier of the response schema, for example <c>CanDoItAll.WorkflowApprovalResponse/v1</c> or
/// <c>CanDoItAll.WorkflowHumanInputResponse/v1</c>; redacted and at most 4,096 characters.
/// </param>
/// <param name="SchemaVersion">Version of the response schema; 1 is the only version the server accepts.</param>
/// <param name="Schema">
/// The JSON Schema that the response value must satisfy, as a JSON value (not a string). Null when
/// <c>schemaAvailable</c> is false.
/// </param>
/// <param name="SchemaAvailable">
/// False when the schema is longer than 32,768 characters and was deliberately omitted; responses are still validated
/// against it.
/// </param>
/// <param name="MaximumPayloadBytes">
/// Maximum size of the response value's JSON text, in UTF-8 bytes, for example 65,536. A larger response is rejected
/// with HTTP 400.
/// </param>
internal sealed record WorkflowExternalResponseContractApiResponse(
    string SchemaId,
    int SchemaVersion,
    JsonElement? Schema,
    bool SchemaAvailable,
    int MaximumPayloadBytes);

/// <summary>
/// Safe projection of a workflow run: one execution of one workflow version. Returned by the run reads and lists and
/// inside start and detail responses. It deliberately omits the backend run identifier and the run's launch origin
/// (caller, authority and source details).
/// </summary>
/// <param name="RunId">Identifier of the run; use it with the <c>/api/workflows/runs/{runId}</c> operations.</param>
/// <param name="WorkflowId">Identifier of the workflow that runs.</param>
/// <param name="VersionId">
/// Identifier of the exact definition version that runs; read it with
/// <c>GET /api/workflows/definitions/{workflowId}/versions/{versionId}</c>.
/// </param>
/// <param name="State">
/// Run state, as a JSON integer: 0 NotStarted, 1 Running, 2 WaitingForInput (an external request must be answered),
/// 3 Idle, 4 Completed, 5 Failed, 6 Cancelled. Completed, Failed and Cancelled are final.
/// </param>
/// <param name="Backend">
/// Runtime backend that executes the run, as a JSON integer: 0 InProcess, 1 DurableTask, 2 AzureFunctions.
/// </param>
/// <param name="Summary">
/// Short human-readable status of the run, for example why it failed; redacted and at most 4,096 characters.
/// </param>
/// <param name="CreatedAtUtc">Time the run was created, as an instant with offset.</param>
/// <param name="UpdatedAtUtc">Time the run record last changed.</param>
/// <param name="TerminalAtUtc">Time the run reached a final state; null while it has not.</param>
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

/// <summary>
/// Safe projection of one recorded event of a workflow run: what happened, where and when, without the event payload.
/// </summary>
/// <param name="Id">
/// Identifier of the event; server-sent notifications carry the same value as <c>eventId</c>.
/// </param>
/// <param name="RunId">Identifier of the run the event belongs to.</param>
/// <param name="Kind">
/// What happened, as a JSON integer: 0 Started, 1 ExecutorInvoked, 2 ExecutorCompleted, 3 ExecutorFailed,
/// 4 SuperStep, 5 Output, 6 Warning, 7 Error, 8 WaitingForInput, 9 Completed, 10 Cancelled, 11 Unknown.
/// </param>
/// <param name="NodeId">Identifier of the definition node the event belongs to; null for run-level events.</param>
/// <param name="Message">Human-readable event message, redacted and at most 4,096 characters.</param>
/// <param name="CreatedAtUtc">Time the event was recorded, as an instant with offset.</param>
internal sealed record WorkflowEventApiResponse(
    Guid Id,
    Guid RunId,
    WorkflowEventKind Kind,
    string? NodeId,
    string Message,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Safe projection of an artifact recorded by a workflow run: its metadata without its storage location or content.
/// Read the content with <c>GET /api/workflows/runs/{runId}/artifacts/{artifactId}/content</c>.
/// </summary>
/// <param name="Id">Identifier of the artifact within its run.</param>
/// <param name="RunId">Identifier of the run that recorded the artifact.</param>
/// <param name="Kind">
/// What the artifact holds, as a JSON integer: 0 Text, 1 Json, 2 File (a workspace file written by the run), 3 Image,
/// 4 Binary, 5 ToolReceipt, 6 PreviewSimulation.
/// </param>
/// <param name="NodeId">Identifier of the definition node that produced it; null when not tied to a node.</param>
/// <param name="Name">Display name, such as a file name; redacted and at most 4,096 characters.</param>
/// <param name="ContentType">
/// Media type of the content, for example <c>application/json</c>; the content download uses it as its
/// <c>Content-Type</c>.
/// </param>
/// <param name="Summary">Short description of the artifact; redacted and at most 4,096 characters.</param>
/// <param name="CreatedAtUtc">Time the artifact was recorded, as an instant with offset.</param>
internal sealed record WorkflowArtifactApiResponse(
    Guid Id,
    Guid RunId,
    WorkflowArtifactKind Kind,
    string? NodeId,
    string Name,
    string ContentType,
    string Summary,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Safe projection of a checkpoint recorded by the runtime for a workflow run: where and why it was taken and whether
/// the run could resume from it. Payload references, hashes and backend checkpoint identifiers are never returned.
/// </summary>
/// <param name="Id">Identifier of the checkpoint.</param>
/// <param name="RunId">Identifier of the run.</param>
/// <param name="WorkflowId">Identifier of the workflow of the run.</param>
/// <param name="VersionId">Identifier of the definition version the run executes.</param>
/// <param name="Backend">
/// Runtime backend that took the checkpoint, as a JSON integer: 0 InProcess, 1 DurableTask, 2 AzureFunctions.
/// </param>
/// <param name="Kind">
/// Why the checkpoint was taken, as a JSON integer: 0 RuntimeBoundary, 1 SuperStep, 2 WaitingForInput, 3 Completed,
/// 4 Failed, 5 Cancelled.
/// </param>
/// <param name="TrustBoundary">
/// What the checkpoint holds, as a JSON integer: 0 MetadataOnly (bookkeeping only, not resumable state) or
/// 1 TrustedRuntimeState (runtime state the host can resume from).
/// </param>
/// <param name="ResumeAvailability">
/// Whether the run can resume from it, as a JSON integer: 0 NotSupported, 1 BlockedByPolicy, 2 Available. Resuming
/// happens only through an answer to the run's external request.
/// </param>
/// <param name="NodeId">Identifier of the node at which the checkpoint was taken; null when not tied to a node.</param>
/// <param name="ExternalRequestId">
/// Identifier of the external request the checkpoint waits for; null when it is not a wait for input.
/// </param>
/// <param name="Summary">Short description; redacted and at most 4,096 characters.</param>
/// <param name="ResumeUnavailableReason">
/// Why the run cannot resume from this checkpoint; empty when it can. Redacted and at most 4,096 characters.
/// </param>
/// <param name="CreatedAtUtc">Time the checkpoint was recorded, as an instant with offset.</param>
/// <param name="ResumedAtUtc">Time the run resumed from it; null when it has not.</param>
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

/// <summary>
/// A workflow run with everything recorded for it, as returned by <c>GET /api/workflows/runs/{runId}/detail</c>. All
/// parts are safe projections, read one after another.
/// </summary>
/// <param name="Run">State of the run, as <c>GET /api/workflows/runs/{runId}</c> returns it.</param>
/// <param name="Events">Every recorded event of the run, oldest first.</param>
/// <param name="Artifacts">Metadata of every artifact of the run, oldest first.</param>
/// <param name="PendingExternalRequests">
/// The run's external requests without a recorded response; answer those whose <c>state</c> is Pending.
/// </param>
/// <param name="Checkpoints">Metadata of every checkpoint of the run, in creation order.</param>
internal sealed record WorkflowRunDetailApiResponse(
    WorkflowRunApiResponse Run,
    IReadOnlyList<WorkflowEventApiResponse> Events,
    IReadOnlyList<WorkflowArtifactApiResponse> Artifacts,
    IReadOnlyList<WorkflowPendingExternalRequestApiResponse> PendingExternalRequests,
    IReadOnlyList<WorkflowCheckpointApiResponse> Checkpoints);

/// <summary>
/// Result of a production start: the run after it stopped, with its events, artifacts, open external requests and
/// checkpoints, plus how the <c>Idempotency-Key</c> was applied. All run parts are safe projections.
/// </summary>
/// <param name="Run">
/// The run. In a replay it shows the state recorded when the original start returned; read the run again for its
/// current state.
/// </param>
/// <param name="Events">Recorded events of the run, oldest first; empty when <c>detailsComplete</c> is false.</param>
/// <param name="Artifacts">Artifact metadata of the run; empty when <c>detailsComplete</c> is false.</param>
/// <param name="PendingExternalRequests">
/// External requests the run waits for when it stopped in state WaitingForInput; empty when there are none or when
/// <c>detailsComplete</c> is false.
/// </param>
/// <param name="Checkpoints">Checkpoint metadata of the run; empty when <c>detailsComplete</c> is false.</param>
/// <param name="IdempotencyDisposition">
/// How the request's <c>Idempotency-Key</c> was applied, as a JSON integer: 0 NotRequested (no key was sent; a new
/// run was started), 1 EnforcedNewRun (the key was recorded with a new run), 2 ReplayedExistingRun (the key was
/// known and its original run is returned; nothing new was started).
/// </param>
/// <param name="Created">True when this request started the run; false for a replay.</param>
/// <param name="Replayed">True when the run was started by an earlier request with the same key.</param>
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
    /// <summary>
    /// False when the run was admitted but its details could not be read for this response; the lists are then empty.
    /// Read <c>GET /api/workflows/runs/{runId}/detail</c> to get them. True otherwise.
    /// </summary>
    public bool DetailsComplete { get; init; } = true;

    /// <summary>
    /// How reliably the server observed the start, as a JSON integer: 0 Confirmed (the run was admitted and its
    /// admission recorded normally), 1 RecoveredAfterObserverFailure (the start reported an error after the run had
    /// been admitted, for example because the run failed; the response was rebuilt from the stored run), 2
    /// AdmissionReceiptPending (the run was admitted but the idempotency record could not be completed yet; a repeated
    /// request with the same key waits for it or takes it over).
    /// </summary>
    public WorkflowLaunchObservation Observation { get; init; }

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
