namespace CanDoItAll.AgentFramework.Models;

internal sealed class AgentMemoryConfigurationDto
{
    public string? InvocationMode { get; set; }

    public bool? CanUseContextContributions { get; set; }

    public bool CanUseMemoryTools { get; set; }

    public bool RequireContextContributions { get; set; }

    public bool AllowAsyncContextContributions { get; set; }

    public bool CanIngestSources { get; set; }

    public string? PreferredProviderInstanceId { get; set; }

    public string? DefaultProviderInstanceId { get; set; }

    public IReadOnlyList<string>? AllowedProviderInstanceIds { get; set; }

    public IReadOnlyList<AgentMemoryProviderBindingDto>? ProviderBindings { get; set; }

    public IReadOnlyList<string>? AllowedCapabilityIds { get; set; }

    public IReadOnlyList<string>? DeniedCapabilityIds { get; set; }

    public IReadOnlyList<string>? AllowedSourceScopes { get; set; }

    public IReadOnlyList<AgentMemoryProviderAssignmentDto>? ProviderAssignments { get; set; }
}

internal sealed class AgentMemoryProviderBindingDto
{
    public string? Alias { get; set; }

    public string? ProviderInstanceId { get; set; }

    public bool IncludeInAutomaticContext { get; set; } = true;

    public string? Requirement { get; set; }
}

internal sealed class AgentMemoryProviderAssignmentDto
{
    public string? Scope { get; set; }

    public string? Key { get; set; }

    public string? ProviderInstanceId { get; set; }
}
