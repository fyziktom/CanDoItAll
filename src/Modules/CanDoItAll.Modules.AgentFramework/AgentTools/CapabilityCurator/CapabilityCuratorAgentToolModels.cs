using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using ModelCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;

namespace CanDoItAll.Modules.AgentFramework;

public sealed record CapabilityCuratorCatalogSearchInput
{
    [JsonConstructor]
    public CapabilityCuratorCatalogSearchInput(
        string? text = null,
        ModelCapabilityKind? kind = null,
        IReadOnlyList<string>? tags = null,
        int pageIndex = 0,
        int pageSize = 25)
    {
        if (pageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "Page index cannot be negative.");
        }

        if (pageSize is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 50.");
        }

        Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        Kind = kind;
        Tags = NormalizeValues(tags);
        PageIndex = pageIndex;
        PageSize = pageSize;
    }

    public string? Text { get; }

    public ModelCapabilityKind? Kind { get; }

    public IReadOnlyList<string> Tags { get; }

    public int PageIndex { get; }

    public int PageSize { get; }

    private static IReadOnlyList<string> NormalizeValues(IEnumerable<string>? values)
        => values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().TrimStart('#').ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
}

public sealed record CapabilityCuratorEditorGetInput(Guid CapabilityId);

public sealed record CapabilityCuratorSaveInput(
    Guid? CapabilityId,
    string? ExpectedFingerprint,
    ModelCapabilityKind Kind,
    string Key,
    string Name,
    string Description,
    IReadOnlyList<string>? Tags = null,
    CapabilityCuratorSkillConfigurationInput? SkillConfiguration = null,
    CapabilityCuratorToolConfigurationInput? ToolConfiguration = null,
    CapabilityCuratorMcpConfigurationInput? McpConfiguration = null,
    JsonElement? OtherConfiguration = null,
    string? EndpointOrPath = null,
    string? SetupAttestationToken = null);

public enum CapabilityCuratorSkillSource
{
    File,
    Registered,
    Inline
}

public enum CapabilityCuratorSkillTrustLevel
{
    WorkspaceSkillRoot,
    ExternalSkillRoot,
    InlineSkill
}

public sealed record CapabilityCuratorSkillConfigurationInput(
    CapabilityCuratorSkillSource Source,
    string? SkillRoot = null,
    IReadOnlyList<string>? AllowedExternalRoots = null,
    string? RegisteredSkillServiceType = null,
    string? InlineName = null,
    string? InlineDescription = null,
    string? InlineInstructions = null,
    IReadOnlyList<CapabilityCuratorInlineSkillResourceInput>? InlineResources = null,
    bool ScriptApprovalRequired = true,
    CapabilityCuratorSkillTrustLevel? ScriptTrustLevel = null);

public sealed record CapabilityCuratorInlineSkillResourceInput(
    string Name,
    string Content,
    string? Description = null);

public enum CapabilityCuratorToolKind
{
    ExternalProcess,
    ExternalHttp
}

public sealed record CapabilityCuratorToolConfigurationInput(
    CapabilityCuratorToolKind ToolKind,
    string RuntimeToolName,
    string ImplementationKey,
    CapabilityCuratorExternalProcessToolInput? ExternalProcess = null,
    CapabilityCuratorExternalHttpToolInput? ExternalHttp = null,
    IReadOnlyList<CapabilityOperationClassification>? OperationClassifications = null,
    CapabilitySideEffectKind SideEffectKind = CapabilitySideEffectKind.ExternalAction,
    bool RequiresApprovalByDefault = true,
    bool IsStateChanging = true);

public sealed record CapabilityCuratorExternalProcessToolInput(
    string Command,
    IReadOnlyList<string>? Arguments = null,
    string WorkingDirectory = ".",
    IReadOnlyList<string>? AllowedExecutableNames = null,
    IReadOnlyList<string>? RequiredOutputProperties = null,
    int TimeoutSeconds = 30,
    int MaxOutputBytes = 4096);

public sealed record CapabilityCuratorExternalHttpToolInput(
    string Method,
    string Endpoint,
    IReadOnlyDictionary<string, string>? HeaderBindings = null,
    IReadOnlyList<string>? RequiredOutputProperties = null,
    int TimeoutSeconds = 30,
    int MaxResponseBytes = 4096);

public enum CapabilityCuratorMcpTransport
{
    Logical,
    Stdio,
    Http
}

public sealed record CapabilityCuratorMcpConfigurationInput(
    CapabilityCuratorMcpTransport Transport,
    string? ServerName = null,
    string? Endpoint = null,
    string? Command = null,
    IReadOnlyList<string>? Arguments = null,
    string WorkingDirectory = ".",
    McpStdioMessageFraming MessageFraming = McpStdioMessageFraming.ContentLength,
    IReadOnlyList<string>? AllowedWorkingDirectories = null,
    IReadOnlyDictionary<string, string>? EnvironmentVariableBindings = null,
    IReadOnlyDictionary<string, string>? HeaderBindings = null,
    IReadOnlyList<string>? AllowedTools = null,
    McpApprovalMode ApprovalMode = McpApprovalMode.NeverRequire,
    int TimeoutSeconds = 30,
    IReadOnlyList<CapabilityOperationClassification>? OperationClassifications = null);

public sealed record CapabilityCuratorCapabilitySetupTestInput(
    CapabilityCuratorSaveInput Candidate,
    string JsonInput = "{}",
    string? CorrelationId = null);

public enum CapabilityCuratorSetupKind
{
    Tool,
    Mcp
}

public sealed record CapabilityCuratorSetupAttestation(
    string Token,
    string CandidateFingerprint,
    DateTimeOffset ExpiresAtUtc);

public sealed record CapabilityCuratorToolSetupTestResult(
    CapabilitySetupTestResult SetupResult,
    CapabilityCuratorSetupAttestation? Attestation);

public sealed record CapabilityCuratorMcpSetupTestResult(
    McpSetupTestResult SetupResult,
    CapabilityCuratorSetupAttestation? Attestation);

public enum CapabilityCuratorAssignmentAction
{
    Attach,
    Detach
}

public sealed record CapabilityCuratorAssignmentUpdateInput(
    Guid AgentId,
    Guid CapabilityId,
    CapabilityCuratorAssignmentAction Action,
    DateTimeOffset ExpectedUpdatedAtUtc);

public sealed record CapabilityCuratorAssignmentEditorGetInput(Guid AgentId);

public sealed record CapabilityCuratorVerifyInput(Guid AgentId, Guid CapabilityId);

public sealed record CapabilityCuratorCatalogItem(
    Guid CapabilityId,
    ModelCapabilityKind Kind,
    string Key,
    string Name,
    string Description,
    string EndpointOrPath,
    CapabilityProofStatus ProofStatus,
    DateTimeOffset? LastVerifiedAtUtc,
    bool IsBuiltIn,
    IReadOnlyList<string> Tags);

public sealed record CapabilityCuratorCatalogSearchResult(
    IReadOnlyList<CapabilityCuratorCatalogItem> Items,
    int PageIndex,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record CapabilityCuratorConfiguration(
    CapabilityCuratorSkillConfigurationInput? Skill,
    CapabilityCuratorToolConfigurationInput? Tool,
    CapabilityCuratorMcpConfigurationInput? Mcp,
    JsonElement? Other);

public sealed record CapabilityCuratorEditorResult(
    Guid CapabilityId,
    ModelCapabilityKind Kind,
    string Key,
    string Name,
    string Description,
    string EndpointOrPath,
    bool IsBuiltIn,
    IReadOnlyList<string> Tags,
    CapabilityProofStatus ProofStatus,
    string ProofNotes,
    DateTimeOffset? LastVerifiedAtUtc,
    string Fingerprint,
    CapabilityCuratorConfiguration Configuration);

public sealed record CapabilityCuratorAssignmentUpdateResult(
    Guid AgentId,
    Guid CapabilityId,
    bool IsAttached,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<Guid> SelectedCapabilityIds);

public sealed record CapabilityCuratorAssignmentEditorResult(
    Guid AgentId,
    string Name,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<Guid> SelectedCapabilityIds);

public sealed record CapabilityCuratorVerifyResult(
    Guid AgentId,
    Guid CapabilityId,
    CapabilityProofStatus ProofStatus,
    string ProofNotes,
    DateTimeOffset? LastVerifiedAtUtc);
