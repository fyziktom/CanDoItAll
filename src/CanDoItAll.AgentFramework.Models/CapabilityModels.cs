namespace CanDoItAll.AgentFramework.Models;

public sealed record CapabilityCatalogItem(
    Guid Id,
    CapabilityKind Kind,
    string Key,
    string Name,
    string Description,
    string EndpointOrPath,
    string ConfigurationJson,
    CapabilityProofStatus ProofStatus,
    string ProofNotes,
    DateTimeOffset? LastVerifiedAtUtc,
    bool IsBuiltIn);

public sealed record CapabilityVerificationResult(
    CapabilityProofStatus Status,
    string Notes,
    DateTimeOffset CheckedAtUtc);
