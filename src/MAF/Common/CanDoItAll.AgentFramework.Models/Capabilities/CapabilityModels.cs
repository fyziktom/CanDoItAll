namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// A capability in the workspace catalog, listed by <c>GET /api/agents/capabilities</c>: a declared MCP server, skill,
/// tool, plugin, retrieval source or context message that agents can select. It is a declaration only: a run grants
/// tools from it only as the runtime's permissions and invocation policy allow.
/// </summary>
/// <param name="Id">Identifier of the capability; agents select it by this value.</param>
/// <param name="Kind">
/// Kind, as a JSON integer: 0 McpServer, 1 Skill, 2 Tool, 3 Plugin, 4 Rag, 5 AiContext, 6 Memory (retired).
/// </param>
/// <param name="Key">Stable normalized key, unique per kind.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Description; may be empty.</param>
/// <param name="EndpointOrPath">
/// Endpoint URL, command or path the capability uses, depending on its kind; may be empty.
/// </param>
/// <param name="ConfigurationJson">Kind-specific settings as stored, returned without redaction.</param>
/// <param name="ProofStatus">
/// Result of the latest verification through any agent, as a JSON integer: 0 NotRun, 1 Verified, 2 Failed,
/// 3 PendingReview. It is informational: the runtime does not use it to decide what a run may use.
/// </param>
/// <param name="ProofNotes">Notes of the latest verification; empty when never verified.</param>
/// <param name="LastVerifiedAtUtc">When the latest verification ran, in UTC, or null when never verified.</param>
/// <param name="IsBuiltIn">True for a capability shipped with the product.</param>
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
    bool IsBuiltIn)
{
    /// <summary>Labels of the capability, lower-case and sorted.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}

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
