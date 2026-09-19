using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Web.Api;

/// <summary>
/// Typed failure of a capability verification request
/// (<c>POST /api/agents/{agentId}/capabilities/{capabilityId}/verify</c>), returned with HTTP 400 when the attempt was
/// rejected before the diagnostic and with HTTP 409 when it did not publish a proof. It replaces the general
/// <c>errors</c> envelope for this operation and never contains internal exception details. A successful verification
/// returns <c>{ "ok": true }</c> instead.
/// </summary>
/// <param name="AgentId">Identifier of the agent from the request route.</param>
/// <param name="CapabilityId">Identifier of the catalog capability from the request route.</param>
/// <param name="Outcome">
/// Why no proof was published, as text: <c>Rejected</c> (HTTP 400; the agent, the capability, its single assignment
/// or the agent's provider could not be resolved) or, with HTTP 409, <c>InfrastructureUnavailable</c>,
/// <c>DiagnosticInterrupted</c>, <c>Superseded</c>, <c>PublicationNotStarted</c>, <c>Unconfirmed</c> (the proof may
/// have been stored), <c>CanceledBeforeDiagnostic</c> or <c>PublicationCanceled</c>. <c>Committed</c> never appears in
/// this response.
/// </param>
/// <param name="ProofAttemptId">
/// Server-generated identifier of this verification attempt, present once the diagnostic has produced a proof (outcomes
/// <c>Superseded</c>, <c>PublicationCanceled</c>, <c>PublicationNotStarted</c> and <c>Unconfirmed</c>); null when the
/// attempt stopped earlier. It identifies the attempt only; it is not a retry key.
/// </param>
/// <param name="ProofCheckedAtUtc">
/// Instant, with offset, at which the diagnostic produced its proof; present together with <c>proofAttemptId</c>.
/// </param>
/// <param name="AutomaticReplaySafe">
/// Always false: do not repeat the verification automatically. Read the agent's capability assignment
/// (<c>GET /api/agents</c>) to see whether a proof was stored, and start another verification only as a deliberate new
/// request.
/// </param>
public sealed record CapabilityVerificationApiResponse(
    Guid AgentId,
    Guid CapabilityId,
    [property: JsonConverter(typeof(JsonStringEnumConverter<CapabilityVerificationDisposition>))]
    CapabilityVerificationDisposition Outcome,
    Guid? ProofAttemptId,
    DateTimeOffset? ProofCheckedAtUtc,
    bool AutomaticReplaySafe);
