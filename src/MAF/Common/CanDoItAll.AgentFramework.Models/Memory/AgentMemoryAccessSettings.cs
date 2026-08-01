using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.AgentFramework.Models;

public enum AgentMemoryInvocationMode
{
    Disabled = 0,
    Automatic = 1,
    ExplicitDirective = 2
}

public enum AgentMemoryProviderRequirement
{
    Optional = 0,
    Required = 1
}

public readonly record struct AgentMemoryProviderAlias
{
    private const int MaximumLength = 64;

    private AgentMemoryProviderAlias(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static AgentMemoryProviderAlias Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > MaximumLength ||
            !char.IsAsciiLetterOrDigit(normalized[0]) ||
            normalized.Any(character => !IsAllowedCharacter(character)))
        {
            throw new ArgumentException(
                "Memory provider aliases must start with a letter or digit and contain only letters, digits, '.', '_' or '-'.",
                nameof(value));
        }

        return new AgentMemoryProviderAlias(normalized);
    }

    public static bool TryParse(string? value, out AgentMemoryProviderAlias alias)
    {
        try
        {
            alias = Parse(value ?? string.Empty);
            return true;
        }
        catch (ArgumentException)
        {
            alias = default;
            return false;
        }
    }

    public override string ToString() => Value;

    private static bool IsAllowedCharacter(char character)
    {
        return char.IsAsciiLetterOrDigit(character) ||
               character is '.' or '_' or '-';
    }
}

public sealed record AgentMemoryProviderBindingSetting(
    AgentMemoryProviderAlias Alias,
    MemoryProviderInstanceId ProviderInstanceId,
    bool IncludeInAutomaticContext = true,
    AgentMemoryProviderRequirement Requirement = AgentMemoryProviderRequirement.Optional);

public sealed record AgentMemoryProviderAssignmentSetting(
    MemoryProviderAssignmentScope Scope,
    string Key,
    MemoryProviderInstanceId ProviderInstanceId);

public sealed class AgentMemoryAccessSettings
{
    public AgentMemoryInvocationMode InvocationMode { get; set; }

    public bool CanUseMemoryTools { get; set; }

    public bool CanUseContextContributions => InvocationMode != AgentMemoryInvocationMode.Disabled;

    public bool RequireContextContributions { get; set; }

    public bool AllowAsyncContextContributions { get; set; }

    public bool CanIngestSources { get; set; }

    public MemoryProviderInstanceId? PreferredProviderInstanceId { get; set; }

    public MemoryProviderInstanceId? DefaultProviderInstanceId { get; set; }

    public IReadOnlyList<MemoryProviderInstanceId> AllowedProviderInstanceIds { get; set; } = [];

    public IReadOnlyList<AgentMemoryProviderBindingSetting> ProviderBindings { get; set; } = [];

    public IReadOnlyList<MemoryCapabilityId> AllowedCapabilityIds { get; set; } = [];

    public IReadOnlyList<MemoryCapabilityId> DeniedCapabilityIds { get; set; } = [];

    public IReadOnlyList<MemorySourceScope> AllowedSourceScopes { get; set; } = [];

    public IReadOnlyList<AgentMemoryProviderAssignmentSetting> ProviderAssignments { get; set; } = [];
}

public sealed class AgentMemoryConfigurationException : InvalidOperationException
{
    public AgentMemoryConfigurationException(string message)
        : base(message)
    {
    }

    public AgentMemoryConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
