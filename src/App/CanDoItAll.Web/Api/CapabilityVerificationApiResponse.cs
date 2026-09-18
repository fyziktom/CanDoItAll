using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Web.Api;

public sealed record CapabilityVerificationApiResponse(
    Guid AgentId,
    Guid CapabilityId,
    [property: JsonConverter(typeof(JsonStringEnumConverter<CapabilityVerificationDisposition>))]
    CapabilityVerificationDisposition Outcome,
    Guid? ProofAttemptId,
    DateTimeOffset? ProofCheckedAtUtc,
    bool AutomaticReplaySafe);
