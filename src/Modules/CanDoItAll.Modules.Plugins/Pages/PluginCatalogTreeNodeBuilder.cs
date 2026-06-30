using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins.Pages;

internal static class PluginCatalogTreeNodeBuilder
{
    private const string PluginNodePrefix = "plugins-plugin:";
    private const string TagNodePrefix = "plugins-tag:";
    private const string UntaggedTag = "uncategorized";

    public static IReadOnlyList<TreeViewNode> Build(
        IReadOnlyList<PluginCatalogItem> plugins,
        PluginId? selectedPluginId,
        IReadOnlySet<string> expandedNodeIds)
    {
        if (plugins.Count == 0)
        {
            return [];
        }

        return plugins
            .SelectMany(plugin => ResolveTags(plugin).Select(tag => new PluginTagAssignment(tag, plugin)))
            .GroupBy(item => item.Tag, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildTagNode(group, selectedPluginId, expandedNodeIds))
            .ToArray();
    }

    public static IReadOnlyList<string> BuildTagNodeIds(IReadOnlyList<PluginCatalogItem> plugins)
        => plugins
            .SelectMany(ResolveTags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .Select(BuildTagNodeId)
            .ToArray();

    public static bool TryReadPluginId(string nodeId, out PluginId pluginId)
    {
        pluginId = default;
        if (!nodeId.StartsWith(PluginNodePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var separatorIndex = nodeId.LastIndexOf(':');
        if (separatorIndex <= PluginNodePrefix.Length ||
            separatorIndex == nodeId.Length - 1)
        {
            return false;
        }

        try
        {
            pluginId = new PluginId(nodeId[(separatorIndex + 1)..]);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static TreeViewNode BuildTagNode(
        IGrouping<string, PluginTagAssignment> group,
        PluginId? selectedPluginId,
        IReadOnlySet<string> expandedNodeIds)
    {
        var tagNodeId = BuildTagNodeId(group.Key);
        var plugins = group
            .GroupBy(item => item.Plugin.PluginId)
            .Select(item => item.First().Plugin)
            .OrderBy(plugin => plugin.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var childNodes = plugins
            .Select(plugin => BuildPluginNode(group.Key, plugin, selectedPluginId))
            .ToArray();
        var containsSelectedPlugin = selectedPluginId.HasValue &&
            plugins.Any(plugin => plugin.PluginId == selectedPluginId.Value);

        return new TreeViewNode
        {
            Id = tagNodeId,
            Text = group.Key,
            Icon = ResolveTagIcon(group.Key),
            BadgeText = plugins.Length.ToString(),
            Children = childNodes,
            IsExpanded = expandedNodeIds.Contains(tagNodeId) || containsSelectedPlugin,
            IsSelectable = false,
            Tooltip = $"{plugins.Length} plugin(s) tagged '{group.Key}'.",
            DataTestId = BuildTagTestId(group.Key),
            ChildrenDataTestId = BuildTagChildrenTestId(group.Key)
        };
    }

    private static TreeViewNode BuildPluginNode(
        string tag,
        PluginCatalogItem plugin,
        PluginId? selectedPluginId)
    {
        var primaryTag = ResolveTags(plugin)[0];
        return new TreeViewNode
        {
            Id = BuildPluginNodeId(tag, plugin.PluginId),
            Text = plugin.DisplayName,
            Icon = ResolvePluginIcon(plugin),
            Tooltip = BuildPluginTooltip(plugin),
            BadgeText = ResolvePluginBadge(plugin),
            IsSelected = selectedPluginId == plugin.PluginId,
            DataTestId = string.Equals(tag, primaryTag, StringComparison.OrdinalIgnoreCase)
                ? PluginsPageHelpers.BuildPluginListTestId(plugin)
                : BuildTaggedPluginTestId(tag, plugin)
        };
    }

    private static IReadOnlyList<string> ResolveTags(PluginCatalogItem plugin)
    {
        var tags = plugin.Descriptor.Tags
            .Select(NormalizeTag)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return tags.Length == 0 ? [UntaggedTag] : tags;
    }

    private static string BuildTagNodeId(string tag)
        => $"{TagNodePrefix}{PluginsPageHelpers.NormalizeTestId(tag)}";

    private static string BuildPluginNodeId(string tag, PluginId pluginId)
        => $"{PluginNodePrefix}{PluginsPageHelpers.NormalizeTestId(tag)}:{pluginId.Value}";

    private static string BuildTagTestId(string tag)
        => $"plugins-tree-tag-{PluginsPageHelpers.NormalizeTestId(tag)}";

    private static string BuildTagChildrenTestId(string tag)
        => $"plugins-tree-tag-children-{PluginsPageHelpers.NormalizeTestId(tag)}";

    private static string BuildTaggedPluginTestId(string tag, PluginCatalogItem plugin)
        => $"plugins-list-item-{PluginsPageHelpers.NormalizeTestId(tag)}-{PluginsPageHelpers.NormalizeTestId(plugin.PluginId.Value)}";

    private static string NormalizeTag(string tag)
        => tag.Trim().ToLowerInvariant();

    private static string ResolveTagIcon(string tag)
        => tag switch
        {
            PluginDescriptorTags.Docker => "deployed_code",
            PluginDescriptorTags.Email => "mail",
            PluginDescriptorTags.HostCommand => "terminal",
            PluginDescriptorTags.OAuth => "key",
            PluginDescriptorTags.Workflow => "account_tree",
            UntaggedTag => "sell",
            _ => "label"
        };

    private static string ResolvePluginIcon(PluginCatalogItem plugin)
        => plugin.Icon is { Kind: UiIconKind.MaterialIcon } icon &&
           !string.IsNullOrWhiteSpace(icon.Value)
            ? icon.Value
            : "extension";

    private static string ResolvePluginBadge(PluginCatalogItem plugin)
        => plugin.InstallationState switch
        {
            PluginInstallationStateKind.InstalledEnabled => "enabled",
            PluginInstallationStateKind.InstalledDisabled => "disabled",
            _ => "available"
        };

    private static string BuildPluginTooltip(PluginCatalogItem plugin)
        => $"{plugin.DisplayName} - {plugin.InstallationState}, {plugin.Description}";

    private sealed record PluginTagAssignment(string Tag, PluginCatalogItem Plugin);
}
