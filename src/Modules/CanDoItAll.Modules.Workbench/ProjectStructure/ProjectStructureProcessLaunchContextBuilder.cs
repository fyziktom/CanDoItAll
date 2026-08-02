using System.Text;
using System.Text.RegularExpressions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureProcessLaunchContextBuilder
{
    private const int ContextRowLimit = 40;
    private const int VisualTargetAssetLimit = 8;

    public static ProjectStructureProcessLaunchContext Build(
        ProjectStructureSurface? surface,
        ProjectStructureNode? focusNode)
    {
        return new ProjectStructureProcessLaunchContext(
            BuildContextSummary(surface, focusNode),
            ProjectStructureOutputRootAuthorityResolver.ResolveProcessOutputRoot(
                surface,
                focusNode));
    }

    private static string BuildContextSummary(
        ProjectStructureSurface? surface,
        ProjectStructureNode? focusNode)
    {
        if (surface is null || focusNode is null)
        {
            return string.Empty;
        }

        var contextRows = EnumerateContextNodes(surface, focusNode);
        var rows = contextRows
            .Take(ContextRowLimit)
            .ToArray();
        if (rows.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Project structure source: {surface.ProjectName} ({surface.ProjectId:D}).");
        builder.AppendLine($"Selected node: {focusNode.Title} ({focusNode.Id}).");
        AppendVisualTargetAssetSummary(builder, contextRows);
        foreach (var (node, depth) in rows)
        {
            var marker = string.Equals(node.Id, focusNode.Id, StringComparison.Ordinal)
                ? " [selected]"
                : string.Empty;
            var subtype = string.IsNullOrWhiteSpace(node.ObjectSubtype)
                ? node.ObjectType.ToString()
                : $"{node.ObjectType}/{node.ObjectSubtype}";
            var notes = NormalizeContextText(string.Join(" ", node.Subtitle, node.Notes), 420);
            var indent = depth <= 0 ? string.Empty : new string(' ', Math.Min(depth, 8) * 2);

            builder.Append("- ");
            builder.Append(indent);
            builder.Append(node.Title);
            builder.Append(marker);
            builder.Append(" [");
            builder.Append(subtype);
            builder.Append("; ");
            builder.Append(string.IsNullOrWhiteSpace(node.Status) ? "Draft" : node.Status);
            builder.Append(']');
            if (!string.IsNullOrWhiteSpace(notes))
            {
                builder.Append(": ");
                builder.Append(notes);
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendVisualTargetAssetSummary(
        StringBuilder builder,
        IReadOnlyList<(ProjectStructureNode Node, int Depth)> contextRows)
    {
        var assets = contextRows
            .Select(row => row.Node)
            .Where(IsVisualTargetAsset)
            .Take(VisualTargetAssetLimit)
            .ToArray();
        if (assets.Length == 0)
        {
            return;
        }

        builder.AppendLine("Visual target assets:");
        foreach (var asset in assets)
        {
            var subtype = string.IsNullOrWhiteSpace(asset.ObjectSubtype)
                ? asset.ObjectType.ToString()
                : $"{asset.ObjectType}/{asset.ObjectSubtype}";
            var media = string.IsNullOrWhiteSpace(asset.MediaRelativePath)
                ? "no media path"
                : asset.MediaRelativePath;
            var fileName = string.IsNullOrWhiteSpace(asset.MediaOriginalFileName)
                ? "unknown file"
                : asset.MediaOriginalFileName;
            var contentType = string.IsNullOrWhiteSpace(asset.MediaContentType)
                ? "unknown content type"
                : asset.MediaContentType;
            var notes = NormalizeContextText(string.Join(" ", asset.Subtitle, asset.Notes), 360);

            builder.Append("- ");
            builder.Append(asset.Title);
            builder.Append(" (");
            builder.Append(asset.Id);
            builder.Append(") [");
            builder.Append(subtype);
            builder.Append("; ");
            builder.Append(contentType);
            builder.Append("; media=");
            builder.Append(media);
            builder.Append("; file=");
            builder.Append(fileName);
            builder.Append("; parent=");
            builder.Append(asset.ParentId ?? "none");
            builder.Append(']');
            if (!string.IsNullOrWhiteSpace(notes))
            {
                builder.Append(": ");
                builder.Append(notes);
            }

            builder.AppendLine();
        }

        builder.AppendLine("Visual target rule: implementation and QA must fetch or analyze the relevant asset content before accepting visual alignment; do not rely only on this text summary or on generated app screenshots in isolation.");
    }

    private static bool IsVisualTargetAsset(ProjectStructureNode node)
    {
        if (!ProjectStructureProcessContextNodeFilter.ShouldIncludeInProcessContext(node))
        {
            return false;
        }

        if (node.ObjectType != ProjectObjectType.ImageAsset)
        {
            return false;
        }

        if (string.Equals(node.ObjectSubtype, "screenshot", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(node.ArtifactKind, "process-run-screenshot", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(node.ObjectSubtype, "generated", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(node.ObjectSubtype, "layout-recommendation", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var searchableText = string.Join(" ", node.Title, node.Subtitle, node.Notes, node.ObjectSubtype, node.ArtifactKind);
        return ContainsVisualTargetKeyword(searchableText);
    }

    private static bool ContainsVisualTargetKeyword(string text)
        => text.Contains("visual", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("target", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("proposal", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("mockup", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("wireframe", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("layout", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("design", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("look", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("ui", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<(ProjectStructureNode Node, int Depth)> EnumerateContextNodes(
        ProjectStructureSurface surface,
        ProjectStructureNode focusNode)
    {
        var projectRootNodeId = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(surface.ProjectId);
        var contextNodes = surface.Nodes
            .Where(node =>
                string.Equals(node.Id, focusNode.Id, StringComparison.Ordinal) ||
                ProjectStructureProcessContextNodeFilter.ShouldIncludeInProcessContext(node))
            .ToArray();
        var childrenByParent = contextNodes
            .Where(node => !string.IsNullOrWhiteSpace(node.ParentId))
            .GroupBy(node => node.ParentId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(node => node.Y).ThenBy(node => node.X).ThenBy(node => node.Title, StringComparer.OrdinalIgnoreCase).ToArray(),
                StringComparer.Ordinal);
        var rows = new List<(ProjectStructureNode Node, int Depth)>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Visit(ProjectStructureNode node, int depth)
        {
            if (!visited.Add(node.Id))
            {
                return;
            }

            rows.Add((node, depth));
            if (!childrenByParent.TryGetValue(node.Id, out var children))
            {
                return;
            }

            foreach (var child in children)
            {
                Visit(child, depth + 1);
            }
        }

        if (childrenByParent.TryGetValue(projectRootNodeId, out var rootChildren))
        {
            foreach (var rootChild in rootChildren)
            {
                Visit(rootChild, 0);
            }
        }

        foreach (var node in contextNodes.OrderBy(node => node.Title, StringComparer.OrdinalIgnoreCase))
        {
            Visit(node, 0);
        }

        if (rows.Any(row => string.Equals(row.Node.Id, focusNode.Id, StringComparison.Ordinal)))
        {
            return rows;
        }

        return [(focusNode, 0), .. rows];
    }

    private static string NormalizeContextText(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = RedactNonCitableContextPaths(Regex.Replace(value, @"\s+", " ").Trim());
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength].TrimEnd() + "...";
    }

    private static string RedactNonCitableContextPaths(string value)
    {
        var withoutNativePaths = Regex.Replace(
            value,
            @"(?:file://[^\s""'<>]+|[A-Za-z]:\\[^\s""'<>|]+|\\\\[^\s""'<>|]+)",
            "[storage-path]");
        return Regex.Replace(
            withoutNativePaths,
            @"\b(?:artifacts/scopes|project-media|managed-files|tool-runs)[^\s""'<>]*",
            "[storage-path]",
            RegexOptions.IgnoreCase);
    }
}

internal sealed record ProjectStructureProcessLaunchContext(
    string ContextSummary,
    string OutputRoot)
{
    internal const string ContextSummaryVariableName = "ProjectStructureContextSummary";
    internal const string OutputRootVariableName = "OutputRoot";
    internal const string ProductRootVariableName = "ProductRoot";

    public void ApplyContextSummaryTo(
        IDictionary<string, string> variables,
        bool removeWhenEmpty = false)
    {
        ArgumentNullException.ThrowIfNull(variables);

        if (!string.IsNullOrWhiteSpace(ContextSummary))
        {
            variables[ContextSummaryVariableName] = ContextSummary;
            return;
        }

        if (removeWhenEmpty)
        {
            variables.Remove(ContextSummaryVariableName);
        }
    }

    public void ApplyOutputRootAliasesTo(IDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);

        if (string.IsNullOrWhiteSpace(OutputRoot))
        {
            return;
        }

        var normalizedOutputRoot = OutputRoot.Trim();
        variables[OutputRootVariableName] = normalizedOutputRoot;
        variables[ProductRootVariableName] = normalizedOutputRoot;
    }
}
