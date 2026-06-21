namespace CanDoItAll.AgentFramework.Tooling;

public sealed record AgentRuntimeToolProviderDescriptor
{
    public AgentRuntimeToolProviderDescriptor(
        string providerKey,
        string displayName,
        string description,
        IReadOnlyCollection<string>? domainTags = null,
        IReadOnlyCollection<AgentRuntimeToolProviderPurpose>? supportedPurposes = null)
    {
        ProviderKey = ValidateRequired(providerKey, nameof(providerKey));
        DisplayName = ValidateRequired(displayName, nameof(displayName));
        Description = description?.Trim() ?? string.Empty;
        DomainTags = NormalizeDomainTags(domainTags);
        SupportedPurposes = NormalizeSupportedPurposes(supportedPurposes);
    }

    public string ProviderKey { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public IReadOnlyList<string> DomainTags { get; }

    public IReadOnlySet<AgentRuntimeToolProviderPurpose> SupportedPurposes { get; }

    private static string ValidateRequired(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Runtime tool provider metadata requires a non-empty provider key and display name.", parameterName);
        }

        return value.Trim();
    }

    private static IReadOnlyList<string> NormalizeDomainTags(IReadOnlyCollection<string>? domainTags)
        => domainTags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray()
        ?? [];

    private static IReadOnlySet<AgentRuntimeToolProviderPurpose> NormalizeSupportedPurposes(
        IReadOnlyCollection<AgentRuntimeToolProviderPurpose>? supportedPurposes)
    {
        var values = supportedPurposes?
            .Distinct()
            .ToArray();

        return values is { Length: > 0 }
            ? new HashSet<AgentRuntimeToolProviderPurpose>(values)
            : Enum.GetValues<AgentRuntimeToolProviderPurpose>().ToHashSet();
    }
}
