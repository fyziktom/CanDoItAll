using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

internal static class ProviderProfileTreeNodeBuilder
{
    private const string ProviderNodePrefix = "providers-provider:";
    private const string TagNodePrefix = "providers-tag:";
    private const string UntaggedTag = "uncategorized";

    public static IReadOnlyList<TreeViewNode> Build(
        IReadOnlyList<ProviderProfile> providers,
        Guid? selectedProviderId,
        IReadOnlySet<string> expandedNodeIds)
    {
        return providers
            .SelectMany(provider => ResolveTags(provider).Select(tag => new ProviderTagAssignment(tag, provider)))
            .GroupBy(item => item.Tag, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildTagNode(group, selectedProviderId, expandedNodeIds))
            .ToArray();
    }

    public static IReadOnlyList<string> BuildTagNodeIds(IReadOnlyList<ProviderProfile> providers)
        => providers
            .SelectMany(ResolveTags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .Select(BuildTagNodeId)
            .ToArray();

    public static bool TryReadProviderId(string nodeId, out Guid providerId)
    {
        providerId = Guid.Empty;
        if (!nodeId.StartsWith(ProviderNodePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var separatorIndex = nodeId.LastIndexOf(':');
        return separatorIndex > ProviderNodePrefix.Length &&
               separatorIndex < nodeId.Length - 1 &&
               Guid.TryParseExact(nodeId[(separatorIndex + 1)..], "N", out providerId);
    }

    private static TreeViewNode BuildTagNode(
        IGrouping<string, ProviderTagAssignment> group,
        Guid? selectedProviderId,
        IReadOnlySet<string> expandedNodeIds)
    {
        var providers = group
            .GroupBy(item => item.Provider.Id)
            .Select(item => item.First().Provider)
            .OrderBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var tagNodeId = BuildTagNodeId(group.Key);
        var containsSelectedProvider = selectedProviderId.HasValue &&
                                       providers.Any(provider => provider.Id == selectedProviderId.Value);

        return new TreeViewNode
        {
            Id = tagNodeId,
            Text = group.Key,
            Icon = ResolveTagIcon(group.Key),
            BadgeText = providers.Length.ToString(),
            Children = providers.Select(provider => BuildProviderNode(group.Key, provider, selectedProviderId)).ToArray(),
            IsExpanded = expandedNodeIds.Contains(tagNodeId) || containsSelectedProvider,
            IsSelectable = false,
            Tooltip = $"{providers.Length} provider profile(s) tagged '{group.Key}'.",
            DataTestId = $"providers-tree-tag-{NormalizeNodeToken(group.Key)}",
            ChildrenDataTestId = $"providers-tree-tag-children-{NormalizeNodeToken(group.Key)}"
        };
    }

    private static TreeViewNode BuildProviderNode(
        string tag,
        ProviderProfile provider,
        Guid? selectedProviderId)
    {
        return new TreeViewNode
        {
            Id = BuildProviderNodeId(tag, provider.Id),
            Text = provider.Name,
            Icon = ResolveProviderIcon(provider),
            Tooltip = $"{provider.Name}. {provider.Kind}, {provider.Transport}, {provider.DefaultModel}.",
            BadgeText = provider.IsEnabled ? "enabled" : "disabled",
            IsSelected = selectedProviderId == provider.Id,
            DataTestId = "providers-tree-provider"
        };
    }

    private static IReadOnlyList<string> ResolveTags(ProviderProfile provider)
    {
        var tags = provider.Tags
            .Select(NormalizeTag)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return tags.Length == 0 ? [UntaggedTag] : tags;
    }

    private static string BuildTagNodeId(string tag)
        => $"{TagNodePrefix}{NormalizeNodeToken(tag)}";

    private static string BuildProviderNodeId(string tag, Guid providerId)
        => $"{ProviderNodePrefix}{NormalizeNodeToken(tag)}:{providerId:N}";

    private static string NormalizeTag(string tag)
        => string.IsNullOrWhiteSpace(tag)
            ? string.Empty
            : tag.Trim().TrimStart('#').ToLowerInvariant();

    private static string NormalizeNodeToken(string value)
    {
        var normalized = NormalizeTag(value);
        var builder = new System.Text.StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '-');
        }

        return builder.ToString().Trim('-');
    }

    private static string ResolveTagIcon(string tag)
        => tag switch
        {
            "local" => "dns",
            "remote" => "cloud_sync",
            "cloud" => "cloud",
            "openai" => "auto_awesome",
            "ollama" => "memory",
            "image" or "image-generation" => "image",
            "fallback" => "alt_route",
            UntaggedTag => "sell",
            _ => "label"
        };

    private static string ResolveProviderIcon(ProviderProfile provider)
        => provider.Kind switch
        {
            ProviderKind.Ollama => "memory",
            ProviderKind.OpenAi or ProviderKind.AzureOpenAi => provider.Purpose == ProviderProfilePurpose.ImageGeneration
                ? "image"
                : "auto_awesome",
            _ => "hub"
        };

    private sealed record ProviderTagAssignment(string Tag, ProviderProfile Provider);
}
