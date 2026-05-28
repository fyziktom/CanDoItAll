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

public sealed record AgentCapabilityRequirement(
    string RoleKey,
    CapabilityKind Kind,
    string CapabilityKey,
    string Reason);

public enum AgentCapabilityDiagnosticSeverity
{
    Warning,
    Error
}

public enum AgentCapabilityDiagnosticCode
{
    MissingRequiredCapability,
    MissingCatalogCapability,
    StaleCapabilityAssignment,
    RetiredCapability
}

public sealed record AgentCapabilityDiagnostic(
    AgentCapabilityDiagnosticCode Code,
    AgentCapabilityDiagnosticSeverity Severity,
    Guid AgentId,
    string AgentName,
    string RoleKey,
    string RoleTitle,
    CapabilityKind Kind,
    string CapabilityKey,
    string Message);

public sealed record AgentCapabilityRequirementEvaluation(
    IReadOnlyList<AgentCapabilityDiagnostic> Diagnostics)
{
    public bool IsSatisfied => Diagnostics.All(item => item.Severity != AgentCapabilityDiagnosticSeverity.Error);
}
