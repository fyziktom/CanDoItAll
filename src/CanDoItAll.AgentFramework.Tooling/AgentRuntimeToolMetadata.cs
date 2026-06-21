namespace CanDoItAll.AgentFramework.Tooling;

public sealed record AgentRuntimeToolMetadata
{
    public AgentRuntimeToolMetadata(
        string providerKey,
        string toolName,
        AgentRuntimeToolOperationKind operationKind,
        bool requiresApprovalByDefault,
        IReadOnlyCollection<string>? ownershipTags = null)
    {
        ProviderKey = ValidateRequired(providerKey, nameof(providerKey));
        ToolName = ValidateRequired(toolName, nameof(toolName));
        OperationKind = operationKind;
        RequiresApprovalByDefault = requiresApprovalByDefault;
        OwnershipTags = NormalizeOwnershipTags(ownershipTags);
    }

    public string ProviderKey { get; }

    public string ToolName { get; }

    public AgentRuntimeToolOperationKind OperationKind { get; }

    public bool RequiresApprovalByDefault { get; }

    public IReadOnlyList<string> OwnershipTags { get; }

    private static string ValidateRequired(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Runtime tool metadata requires non-empty provider and tool identifiers.", parameterName);
        }

        return value.Trim();
    }

    private static IReadOnlyList<string> NormalizeOwnershipTags(IReadOnlyCollection<string>? ownershipTags)
        => ownershipTags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray()
        ?? [];
}
