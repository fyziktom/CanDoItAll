using CanDoItAll.AgentFramework.Capabilities.Abstractions;

namespace CanDoItAll.AgentFramework.Capabilities.Templates;

public sealed record CapabilityTemplateDescriptorDto
{
    public string? Kind { get; init; }

    public string? Key { get; init; }

    public string? DisplayName { get; init; }

    public string? Description { get; init; }

    public string? StableId { get; init; }

    public string? RuntimeToolName { get; init; }

    public string? ImplementationKey { get; init; }

    public string? McpServerKey { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<string> OperationClassifications { get; init; } = [];

    public CapabilitySideEffectTemplateDto? SideEffects { get; init; }

    public ExternalProcessToolTemplateDto? ExternalProcess { get; init; }

    public ExternalHttpToolTemplateDto? ExternalHttp { get; init; }

    public McpTransportTemplateDto? McpTransport { get; init; }

    public IReadOnlyList<SetupTestTemplateDto> SetupTests { get; init; } = [];

    public CapabilityAccessPolicyTemplateDto? CapabilityAccessPolicy { get; init; }
}

public sealed record CapabilitySideEffectTemplateDto
{
    public string? Kind { get; init; }

    public bool RequiresApprovalByDefault { get; init; }

    public bool IsStateChanging { get; init; }
}

public sealed record SecretBindingTemplateDto
{
    public string? BindingKey { get; init; }

    public string? DestinationName { get; init; }
}

public sealed record ExternalProcessToolTemplateDto
{
    public string? Command { get; init; }

    public string? WorkingDirectory { get; init; }

    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<SecretBindingTemplateDto> EnvironmentVariableBindings { get; init; } = [];

    public TimeSpan? Timeout { get; init; }
}

public sealed record ExternalHttpToolTemplateDto
{
    public string? Method { get; init; }

    public string? UrlTemplate { get; init; }

    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<SecretBindingTemplateDto> HeaderBindings { get; init; } = [];

    public TimeSpan? Timeout { get; init; }
}

public sealed record McpTransportTemplateDto
{
    public string? Transport { get; init; }

    public string? Command { get; init; }

    public IReadOnlyList<string> Arguments { get; init; } = [];

    public string? UrlTemplate { get; init; }

    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<SecretBindingTemplateDto> EnvironmentVariableBindings { get; init; } = [];

    public string? MessageFraming { get; init; }

    public IReadOnlyList<string> AllowedTools { get; init; } = [];
}

public sealed record SetupTestTemplateDto
{
    public string? Kind { get; init; }

    public string? ExpectedToolName { get; init; }
}

/// <summary>
/// Capability access policy written with text tokens. Tokens are member names matched ignoring case, hyphens and
/// underscores, so <c>denyAll</c>, <c>deny-all</c> and <c>DenyAll</c> are equal; numbers are not accepted. Every
/// problem is reported as a validation issue with the JSON path of the offending member.
/// </summary>
public sealed record CapabilityAccessPolicyTemplateDto
{
    /// <summary>
    /// What happens to candidates no rule decides: <c>denyAll</c> (a candidate needs a matching allow or require rule),
    /// or <c>inherit</c> (the default when blank) and <c>allowAssigned</c>, which both allow every candidate that no
    /// deny rule matches.
    /// </summary>
    public string? DefaultEffect { get; init; }

    /// <summary>The rules of the policy; empty for none.</summary>
    public IReadOnlyList<CapabilityAccessRuleTemplateDto> Rules { get; init; } = [];
}

/// <summary>
/// One rule of a capability access policy. A matching deny rule wins over any allow rule.
/// </summary>
public sealed record CapabilityAccessRuleTemplateDto
{
    /// <summary>Identifier of the rule in lower kebab case, unique within the policy ignoring case.</summary>
    public string? Id { get; init; }

    /// <summary>
    /// Effect of the rule: <c>allow</c>, <c>deny</c> or <c>require</c> (allow, and report a diagnostic when no allowed
    /// candidate matches). <c>inherit</c> is rejected for rules.
    /// </summary>
    public string? Effect { get; init; }

    /// <summary>
    /// Level at which the rule is declared: <c>system</c>, <c>agentDefault</c>, <c>workflowDefinition</c>,
    /// <c>workflowNode</c>, <c>processDefinition</c>, <c>processStep</c>, <c>runtimeOverride</c> or
    /// <c>uiPreview</c>. When several deny rules match, the scope decides which one is reported.
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>What the rule matches; required.</summary>
    public CapabilitySelectorTemplateDto? Selector { get; init; }

    /// <summary>Why the rule exists; required, and reported with the capabilities the rule suppresses.</summary>
    public string? Reason { get; init; }
}

/// <summary>
/// What a capability access rule matches.
/// </summary>
public sealed record CapabilitySelectorTemplateDto
{
    /// <summary>
    /// Selector kind: <c>all</c>, <c>kind</c>, <c>capabilityKey</c>, <c>tag</c>, <c>operationClassification</c>,
    /// <c>runtimeToolName</c>, <c>mcpServerKey</c>, <c>mcpToolName</c> or <c>implementationKey</c>.
    /// </summary>
    public string? Kind { get; init; }

    /// <summary>
    /// Value to match, in the form the selector kind needs: a capability kind name such as <c>tool</c> or
    /// <c>mcpServer</c> for <c>kind</c>; a lower kebab-case key or tag for <c>capabilityKey</c>, <c>tag</c> and
    /// <c>mcpServerKey</c>; an operation classification name such as <c>read</c> or <c>externalAction</c>; a lower
    /// snake_case name for <c>runtimeToolName</c>; the MCP tool name for <c>mcpToolName</c>; an implementation key for
    /// <c>implementationKey</c>. Ignored for <c>all</c>.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// Lower kebab-case key of the MCP server; required for <c>mcpToolName</c> selectors and ignored otherwise.
    /// </summary>
    public string? ServerKey { get; init; }
}

public sealed record CapabilityAccessPolicyCompilationResult(
    CapabilityAccessPolicy? Policy,
    CapabilityValidationResult ValidationResult);
