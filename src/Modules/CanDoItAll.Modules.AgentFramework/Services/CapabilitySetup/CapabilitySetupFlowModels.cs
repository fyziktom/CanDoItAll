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

public sealed class CapabilityToolSetupTestRequest
{
    public CapabilityEditorModel Capability { get; set; } = new();

    public string JsonInput { get; set; } = "{}";

    public string CorrelationId { get; set; } = string.Empty;
}

public sealed class CapabilityMcpSetupTestRequest
{
    public CapabilityEditorModel Capability { get; set; } = new();

    public string CorrelationId { get; set; } = string.Empty;
}

public sealed class CapabilityAccessPreviewRequest
{
    public IReadOnlyList<Guid> CapabilityIds { get; set; } = [];

    public IReadOnlyList<CapabilityEditorModel> DraftCapabilities { get; set; } = [];

    public CapabilityAccessPolicyTemplateDto Policy { get; set; } = new();

    public IReadOnlyList<CapabilityIdentityEditorModel> RequiredCapabilities { get; set; } = [];

    public string CorrelationId { get; set; } = string.Empty;
}

public sealed class CapabilityIdentityEditorModel
{
    public AccessCapabilityKind Kind { get; set; } = AccessCapabilityKind.Tool;

    public string Key { get; set; } = string.Empty;
}

public sealed record CapabilityAccessPreviewResult(
    CapabilityValidationResult ValidationResult,
    EffectiveCapabilitySet EffectiveSet,
    IReadOnlyList<CapabilityAccessPreviewCapabilityRow> Capabilities);

public sealed record CapabilityAccessPreviewCapabilityRow(
    CapabilityIdentity Identity,
    string DisplayName,
    bool IsAllowed,
    IReadOnlyList<SuppressedCapabilityDiagnostic> Diagnostics);
