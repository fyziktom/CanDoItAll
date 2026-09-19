using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Templates;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using AccessCapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;

namespace CanDoItAll.Modules.AgentFramework;

public interface IAgentCapabilitySetupFlowService
{
    Task<CapabilitySetupTestResult> TestToolSetupAsync(
        CapabilityToolSetupTestRequest request,
        CancellationToken cancellationToken = default);

    Task<McpSetupTestResult> TestMcpSetupAsync(
        CapabilityMcpSetupTestRequest request,
        CancellationToken cancellationToken = default);

    Task<CapabilityAccessPreviewResult> PreviewAccessAsync(
        CapabilityAccessPreviewRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Tool capability definition to test once, sent to <c>POST /api/agents/capabilities/setup-tests/tool</c>. The
/// definition does not have to be saved; nothing is written to the catalog.
/// </summary>
public sealed class CapabilityToolSetupTestRequest
{
    /// <summary>
    /// The tool capability form to test; the tool is read from its <c>configurationJson</c> and its kind is ignored.
    /// </summary>
    public CapabilityEditorModel Capability { get; set; } = new();

    /// <summary>
    /// Input to send to the tool, as the text of a JSON object; blank is treated as <c>{}</c>. Any other JSON value is
    /// reported as a diagnostic.
    /// </summary>
    public string JsonInput { get; set; } = "{}";

    /// <summary>
    /// Correlation identifier to put on the diagnostics; blank generates <c>tool-setup-</c> followed by a new GUID.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;
}

/// <summary>
/// MCP server capability definition to test by starting or connecting to the server and listing its tools, sent to
/// <c>POST /api/agents/capabilities/setup-tests/mcp</c>. The definition does not have to be saved; nothing is written
/// to the catalog.
/// </summary>
public sealed class CapabilityMcpSetupTestRequest
{
    /// <summary>
    /// The MCP server capability form to test; the server is read from its <c>configurationJson</c> and its kind is
    /// ignored.
    /// </summary>
    public CapabilityEditorModel Capability { get; set; } = new();

    /// <summary>
    /// Correlation identifier to put on the diagnostics; blank generates <c>mcp-setup-</c> followed by a new GUID.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;
}

/// <summary>
/// Candidate capabilities and an access policy to evaluate without saving anything, sent to
/// <c>POST /api/agents/capabilities/access-preview</c>. Send empty arrays rather than null lists.
/// </summary>
public sealed class CapabilityAccessPreviewRequest
{
    /// <summary>
    /// Identifiers of saved catalog capabilities to use as candidates; empty uses every saved capability.
    /// </summary>
    public IReadOnlyList<Guid> CapabilityIds { get; set; } = [];

    /// <summary>
    /// Unsaved capability forms to add as candidates. A draft replaces the saved candidate with the same <c>id</c>,
    /// or with the same key (ignoring case) when its <c>id</c> is null. Their <c>kind</c> uses the catalog numbering:
    /// 0 McpServer, 1 Skill, 2 Tool, 3 Plugin, 4 Rag, 5 AiContext.
    /// </summary>
    public IReadOnlyList<CapabilityEditorModel> DraftCapabilities { get; set; } = [];

    /// <summary>The access policy to compile and evaluate against the candidates.</summary>
    public CapabilityAccessPolicyTemplateDto Policy { get; set; } = new();

    /// <summary>
    /// Capabilities the policy is expected to allow; a Require rule that matches no candidate produces a diagnostic.
    /// </summary>
    public IReadOnlyList<CapabilityIdentityEditorModel> RequiredCapabilities { get; set; } = [];

    /// <summary>
    /// Correlation identifier to put on the diagnostics; blank generates <c>access-preview-</c> followed by a new
    /// GUID.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;
}

/// <summary>
/// Kind and key identifying a capability that an access policy is expected to allow.
/// </summary>
public sealed class CapabilityIdentityEditorModel
{
    /// <summary>
    /// Identity kind, as a JSON integer in the identity numbering (not the catalog numbering): 0 Skill, 1 Tool (the
    /// default), 2 McpServer, 3 McpTool, 4 Plugin, 5 Rag, 6 AiContext, 7 Memory.
    /// </summary>
    public AccessCapabilityKind Kind { get; set; } = AccessCapabilityKind.Tool;

    /// <summary>
    /// Capability key in lower kebab case, for example <c>release-notes</c>; an invalid key is reported as a validation
    /// issue.
    /// </summary>
    public string Key { get; set; } = string.Empty;
}

/// <summary>
/// Result of an access policy preview. When the policy or any candidate is invalid, <c>validationResult</c> lists the
/// issues, the effective set is empty and no row is allowed.
/// </summary>
/// <param name="ValidationResult">Validation issues of the candidates and the policy.</param>
/// <param name="EffectiveSet">The candidates the policy allows and the ones it suppresses, with reasons.</param>
/// <param name="Capabilities">One row per candidate, in candidate order.</param>
public sealed record CapabilityAccessPreviewResult(
    CapabilityValidationResult ValidationResult,
    EffectiveCapabilitySet EffectiveSet,
    IReadOnlyList<CapabilityAccessPreviewCapabilityRow> Capabilities);

/// <summary>
/// Preview result for one candidate capability. An MCP server candidate produces a row for the server and one row
/// per allowed MCP tool.
/// </summary>
/// <param name="Identity">Kind and key of the candidate, in the identity numbering.</param>
/// <param name="DisplayName">Display name of the candidate; the key when the name is blank.</param>
/// <param name="IsAllowed">True when the policy allows the candidate.</param>
/// <param name="Diagnostics">Reasons the candidate is suppressed; empty when it is allowed.</param>
public sealed record CapabilityAccessPreviewCapabilityRow(
    CapabilityIdentity Identity,
    string DisplayName,
    bool IsAllowed,
    IReadOnlyList<SuppressedCapabilityDiagnostic> Diagnostics);
