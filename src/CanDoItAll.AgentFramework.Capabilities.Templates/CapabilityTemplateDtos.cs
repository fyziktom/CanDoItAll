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

    public IReadOnlyList<string> AllowedTools { get; init; } = [];
}

public sealed record SetupTestTemplateDto
{
    public string? Kind { get; init; }

    public string? ExpectedToolName { get; init; }
}

public sealed record CapabilityAccessPolicyTemplateDto
{
    public string? DefaultEffect { get; init; }

    public IReadOnlyList<CapabilityAccessRuleTemplateDto> Rules { get; init; } = [];
}

public sealed record CapabilityAccessRuleTemplateDto
{
    public string? Id { get; init; }

    public string? Effect { get; init; }

    public string? Scope { get; init; }

    public CapabilitySelectorTemplateDto? Selector { get; init; }

    public string? Reason { get; init; }
}

public sealed record CapabilitySelectorTemplateDto
{
    public string? Kind { get; init; }

    public string? Value { get; init; }

    public string? ServerKey { get; init; }
}

public sealed record CapabilityAccessPolicyCompilationResult(
    CapabilityAccessPolicy? Policy,
    CapabilityValidationResult ValidationResult);
