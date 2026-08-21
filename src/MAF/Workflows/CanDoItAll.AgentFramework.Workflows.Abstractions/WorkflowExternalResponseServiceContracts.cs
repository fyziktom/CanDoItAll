using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public static class WorkflowExternalResponseAuthorizationPolicy
{
    public const string CurrentFingerprint =
        "40146b007ac7b3385ed97b80b57f8c6e4604e4025972e7b2a65bcd148baf41f6";

    public const int ResponseLifetimeSeconds = 900;
}

[Flags]
public enum WorkflowExternalResponseCallerCapabilities
{
    None = 0,
    SubmitHumanInput = 1,
    SubmitApprovalDecision = 2
}

public enum WorkflowExternalResponseTrustedChannel
{
    Api,
    LocalOperator,
    AgentTool
}

public sealed record WorkflowExternalResponseCallerAccess
{
    public WorkflowExternalResponseCallerAccess(
        Guid databaseProfileId,
        DatabaseProfileGeneration databaseProfileGeneration,
        WorkspaceScopeDescriptor currentProfileScope,
        IEnumerable<WorkspaceScopeDescriptor> grantedScopes,
        WorkflowExternalResponseCallerCapabilities capabilities,
        string policyFingerprint,
        DateTimeOffset authenticatedAtUtc,
        DateTimeOffset? authenticationExpiresAtUtc = null)
    {
        if (databaseProfileId == Guid.Empty)
        {
            throw new ArgumentException("A database profile id is required.", nameof(databaseProfileId));
        }

        ArgumentNullException.ThrowIfNull(currentProfileScope);
        ArgumentNullException.ThrowIfNull(grantedScopes);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyFingerprint);
        if (authenticatedAtUtc == default)
        {
            throw new ArgumentException("An authentication timestamp is required.", nameof(authenticatedAtUtc));
        }

        if (authenticationExpiresAtUtc.HasValue &&
            authenticationExpiresAtUtc.Value <= authenticatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(authenticationExpiresAtUtc),
                "Authentication expiry must be later than authentication time.");
        }

        DatabaseProfileId = databaseProfileId;
        DatabaseProfileGeneration = databaseProfileGeneration;
        CurrentProfileScope = currentProfileScope;
        GrantedScopes = grantedScopes.ToImmutableArray();
        Capabilities = capabilities;
        PolicyFingerprint = policyFingerprint.Trim();
        AuthenticatedAtUtc = authenticatedAtUtc;
        AuthenticationExpiresAtUtc = authenticationExpiresAtUtc;
    }

    public Guid DatabaseProfileId { get; }

    public DatabaseProfileGeneration DatabaseProfileGeneration { get; }

    public WorkspaceScopeDescriptor CurrentProfileScope { get; }

    public ImmutableArray<WorkspaceScopeDescriptor> GrantedScopes { get; }

    public WorkflowExternalResponseCallerCapabilities Capabilities { get; }

    public string PolicyFingerprint { get; }

    public DateTimeOffset AuthenticatedAtUtc { get; }

    public DateTimeOffset? AuthenticationExpiresAtUtc { get; }
}

public abstract record WorkflowExternalResponseActorContext
{
    private WorkflowExternalResponseActorContext()
    {
    }

    public sealed record Unauthenticated(string SafeReason = "Authentication is required.") :
        WorkflowExternalResponseActorContext;

    public sealed record Authenticated(
        WorkflowLaunchActor Actor,
        WorkflowExternalResponseTrustedChannel Channel,
        WorkflowExternalResponseCallerAccess Access) :
        WorkflowExternalResponseActorContext;
}

public sealed record WorkflowExternalResponseCommand
{
    public WorkflowExternalResponseCommand(
        WorkflowExternalResponseActorContext actorContext,
        WorkflowExternalRequestId requestId,
        WorkflowExternalRequestVersion expectedRequestVersion,
        JsonElement response,
        WorkflowExternalResponseIdempotencyKey idempotencyKey,
        WorkflowLaunchCorrelationId correlationId)
    {
        ActorContext = actorContext ?? throw new ArgumentNullException(nameof(actorContext));
        if (response.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException("A workflow external response JSON value is required.", nameof(response));
        }

        RequestId = requestId;
        ExpectedRequestVersion = expectedRequestVersion;
        Response = response.Clone();
        IdempotencyKey = idempotencyKey;
        CorrelationId = correlationId;
    }

    public WorkflowExternalResponseActorContext ActorContext { get; }

    public WorkflowExternalRequestId RequestId { get; }

    public WorkflowExternalRequestVersion ExpectedRequestVersion { get; }

    public JsonElement Response { get; }

    public WorkflowExternalResponseIdempotencyKey IdempotencyKey { get; }

    public WorkflowLaunchCorrelationId CorrelationId { get; }
}

public sealed record WorkflowExternalResponseStatusQuery(
    WorkflowExternalResponseActorContext ActorContext,
    WorkflowExternalResponseOperationId OperationId,
    WorkflowLaunchCorrelationId CorrelationId);

public enum WorkflowExternalResponseServiceOutcome
{
    Completed,
    WaitingAgain,
    Denied,
    Resuming,
    Unauthenticated,
    Forbidden,
    InvalidResponse,
    RequestNotFound,
    RunNotFound,
    OperationNotFound,
    RequestVersionMismatch,
    RequestNotPending,
    RunNotWaiting,
    IdempotencyConflict,
    ActiveOperationConflict,
    Cancelled,
    Superseded,
    LegacyNonResumable,
    AuthorizationContextUnavailable,
    CheckpointMissing,
    CheckpointCorrupt,
    CheckpointIncompatible,
    TopologyMismatch,
    WorkflowVersionMismatch,
    RequestMismatch,
    BackendUnavailable,
    RetryableFailure,
    TerminalFailure
}

public sealed record WorkflowExternalResponseServiceResult(
    WorkflowExternalResponseServiceOutcome Outcome,
    WorkflowExternalResponseOperationRecord? Operation,
    WorkflowRunSnapshot? Run,
    WorkflowExternalRequestRecord? Request,
    WorkflowExternalRequestRecord? NextRequest,
    bool Replayed,
    string SafeMessage);

public interface IWorkflowExternalResponseService
{
    Task<WorkflowExternalResponseServiceResult> SubmitAsync(
        WorkflowExternalResponseCommand command,
        CancellationToken cancellationToken = default);

    Task<WorkflowExternalResponseServiceResult> GetStatusAsync(
        WorkflowExternalResponseStatusQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowExternalRequestAuthorizationRequest(
    WorkflowExternalResponseActorContext ActorContext,
    WorkflowRunSnapshot Run,
    WorkflowExternalRequestRecord Request,
    WorkflowExternalRequestBoundaryRecord Boundary,
    DateTimeOffset EvaluatedAtUtc);

public enum WorkflowExternalRequestAuthorizationOutcome
{
    Authorized,
    Unauthenticated,
    ProfileMismatch,
    ScopeMismatch,
    CapabilityMissing,
    AssignmentMismatch,
    SelfApprovalDenied,
    AuthenticationExpired,
    AuthorizationContextUnavailable
}

public sealed record WorkflowAuthorizedExternalResponseActor(
    WorkflowLaunchActor Actor,
    WorkspaceScopeDescriptor AuthorizationScope,
    string AuthorizationPolicyFingerprint,
    DateTimeOffset AuthorizedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record WorkflowExternalRequestAuthorizationDecision(
    WorkflowExternalRequestAuthorizationOutcome Outcome,
    WorkflowAuthorizedExternalResponseActor? Authorization,
    string SafeMessage)
{
    public bool Succeeded =>
        Outcome == WorkflowExternalRequestAuthorizationOutcome.Authorized &&
        Authorization is not null;
}

public interface IWorkflowExternalRequestAuthorizer
{
    Task<WorkflowExternalRequestAuthorizationDecision> AuthorizeAsync(
        WorkflowExternalRequestAuthorizationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowExternalResponseValidationRequest(
    WorkflowRunSnapshot Run,
    WorkflowExternalRequestRecord Request,
    WorkflowExternalRequestBoundaryRecord Boundary,
    WorkflowExternalRequestVersion ExpectedRequestVersion,
    JsonElement Response);

public enum WorkflowExternalResponseValidationOutcome
{
    Valid,
    RequestVersionMismatch,
    BoundaryMismatch,
    InvalidContract,
    PayloadTooLarge,
    MaximumDepthExceeded,
    SchemaMismatch,
    UnsupportedRequestKind
}

public sealed record WorkflowExternalResponseValidationResult(
    WorkflowExternalResponseValidationOutcome Outcome,
    WorkflowExternalResponsePayload? CanonicalPayload,
    WorkflowExternalResponseAction? Action,
    string SafeMessage)
{
    public bool Succeeded =>
        Outcome == WorkflowExternalResponseValidationOutcome.Valid &&
        CanonicalPayload is not null &&
        Action is not null;
}

public interface IWorkflowExternalResponseValidator
{
    WorkflowExternalResponseValidationResult Validate(
        WorkflowExternalResponseValidationRequest request);
}
