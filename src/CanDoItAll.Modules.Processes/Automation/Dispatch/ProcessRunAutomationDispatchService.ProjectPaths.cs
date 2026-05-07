using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static IReadOnlyList<ProjectStructureGroundingNodeData> ResolveProjectStructureAncestorPath(
        string? nodeId,
        IReadOnlyDictionary<string, ProjectStructureGroundingNodeData> nodesById)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return [];
        }

        var path = new List<ProjectStructureGroundingNodeData>();
        var cursor = NormalizeProjectStructureNodeId(nodeId);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrWhiteSpace(cursor) &&
               visited.Add(cursor) &&
               nodesById.TryGetValue(cursor, out var node))
        {
            path.Add(node);
            cursor = NormalizeProjectStructureNodeId(node.ParentId);
        }

        path.Reverse();
        return path;
    }

    private static IReadOnlyList<ProjectStructureGroundingNodeData> ResolveProjectStructureDescendants(
        string? nodeId,
        IReadOnlyDictionary<string, IReadOnlyList<ProjectStructureGroundingNodeData>> nodesByParentId,
        int maxDepth)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || maxDepth <= 0)
        {
            return [];
        }

        var descendants = new List<ProjectStructureGroundingNodeData>();
        var queue = new Queue<(string NodeId, int Depth)>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        queue.Enqueue((NormalizeProjectStructureNodeId(nodeId), 0));

        while (queue.Count > 0)
        {
            var (currentNodeId, depth) = queue.Dequeue();
            if (depth >= maxDepth ||
                !nodesByParentId.TryGetValue(currentNodeId, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                if (!visited.Add(child.Id))
                {
                    continue;
                }

                descendants.Add(child);
                queue.Enqueue((child.Id, depth + 1));
            }
        }

        return descendants;
    }

    private static bool TryResolveExternalTargetHintFromProjectStructureGrounding(
        string? groundingSummary,
        out string absolutePath,
        out string mappedAlias)
    {
        absolutePath = string.Empty;
        mappedAlias = string.Empty;

        if (string.IsNullOrWhiteSpace(groundingSummary))
        {
            return false;
        }

        foreach (var candidatePath in EnumerateAbsoluteExternalPathCandidates(groundingSummary))
        {
            if (!TryNormalizeAbsoluteExternalPathCandidate(candidatePath, out var normalizedPath))
            {
                continue;
            }

            if (!TryMapAbsoluteExternalPathToAlias(normalizedPath, out var alias))
            {
                continue;
            }

            absolutePath = normalizedPath;
            mappedAlias = alias;
            return true;
        }

        return false;
    }

    private static IReadOnlyList<ProjectStructureExternalTargetHint> ResolveProjectStructureExternalTargetHintsForFocus(
        IReadOnlyDictionary<string, ProjectStructureGroundingNodeData> nodesById,
        IReadOnlyDictionary<string, IReadOnlyList<ProjectStructureGroundingNodeData>> nodesByParentId,
        string targetNodeId,
        string selectedProcessNodeId)
    {
        var focusNodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in ResolveProjectStructureAncestorPath(targetNodeId, nodesById))
        {
            focusNodeIds.Add(node.Id);
        }

        if (!string.IsNullOrWhiteSpace(targetNodeId))
        {
            focusNodeIds.Add(targetNodeId);
        }

        if (!string.IsNullOrWhiteSpace(selectedProcessNodeId))
        {
            focusNodeIds.Add(selectedProcessNodeId);
        }

        foreach (var descendant in ResolveProjectStructureDescendants(targetNodeId, nodesByParentId, maxDepth: 2))
        {
            focusNodeIds.Add(descendant.Id);
        }

        foreach (var planningNode in ResolveProjectLevelPlanningNodesForTarget(
                     nodesById,
                     nodesByParentId,
                     targetNodeId,
                     selectedProcessNodeId))
        {
            focusNodeIds.Add(planningNode.Id);
            foreach (var descendant in ResolveProjectStructureDescendants(planningNode.Id, nodesByParentId, maxDepth: 3))
            {
                focusNodeIds.Add(descendant.Id);
            }
        }

        return focusNodeIds
            .Where(nodesById.ContainsKey)
            .Select(id => nodesById[id])
            .SelectMany(ResolveExternalTargetHintsFromProjectStructureNode)
            .GroupBy(hint => hint.MappedAlias, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(hint => hint.MappedAlias.Length)
            .ThenBy(hint => hint.SourceNodeTitle, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();
    }

    private static IReadOnlyList<ProjectStructureGroundingNodeData> ResolveProjectLevelPlanningNodesForTarget(
        IReadOnlyDictionary<string, ProjectStructureGroundingNodeData> nodesById,
        IReadOnlyDictionary<string, IReadOnlyList<ProjectStructureGroundingNodeData>> nodesByParentId,
        string targetNodeId,
        string selectedProcessNodeId)
    {
        if (string.IsNullOrWhiteSpace(targetNodeId) ||
            !nodesById.TryGetValue(targetNodeId, out var targetNode) ||
            string.IsNullOrWhiteSpace(targetNode.ParentId) ||
            !nodesByParentId.TryGetValue(NormalizeProjectStructureNodeId(targetNode.ParentId), out var siblings))
        {
            return [];
        }

        return siblings
            .Where(node =>
                !string.Equals(node.Id, targetNode.Id, StringComparison.Ordinal) &&
                !string.Equals(node.Id, selectedProcessNodeId, StringComparison.Ordinal) &&
                IsProjectLevelPlanningContextNode(node))
            .Select(node => new
            {
                Node = node,
                SignalScore = GetProjectStructureGroundingSignalScore(node)
            })
            .Where(item => item.SignalScore > 0 || !string.IsNullOrWhiteSpace(item.Node.Title))
            .OrderByDescending(item => item.SignalScore)
            .ThenBy(item => item.Node.Title, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .Select(item => item.Node)
            .ToList();
    }

    private static string ResolveProjectStructureGroundingTargetNodeId(
        ProcessProjectStructureContext context,
        IReadOnlyDictionary<string, ProjectStructureGroundingNodeData> nodesById)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nodesById);

        var resolvedTargetNodeId = NormalizeProjectStructureNodeId(context.ResolveTargetNodeId());
        var selectedNodeId = NormalizeProjectStructureNodeId(context.NodeId);
        var parentNodeId = NormalizeProjectStructureNodeId(context.ParentNodeId);

        if (!string.IsNullOrWhiteSpace(selectedNodeId) &&
            nodesById.ContainsKey(selectedNodeId) &&
            (IsProcessDefinitionNodeId(resolvedTargetNodeId) || !nodesById.ContainsKey(resolvedTargetNodeId)))
        {
            return selectedNodeId;
        }

        if (!string.IsNullOrWhiteSpace(resolvedTargetNodeId) &&
            nodesById.ContainsKey(resolvedTargetNodeId))
        {
            return resolvedTargetNodeId;
        }

        if (!string.IsNullOrWhiteSpace(parentNodeId) &&
            nodesById.ContainsKey(parentNodeId))
        {
            return parentNodeId;
        }

        return resolvedTargetNodeId;
    }

    private static bool IsProcessDefinitionNodeId(string? nodeId)
    {
        return !string.IsNullOrWhiteSpace(nodeId) &&
               nodeId.Trim().StartsWith("process-definition:", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ProjectStructureExternalTargetHint> ResolveExternalTargetHintsFromProjectStructureNode(
        ProjectStructureGroundingNodeData node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var searchText = string.Join(
            Environment.NewLine,
            node.Title,
            node.Subtitle,
            node.Notes,
            node.MetadataJson);
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return [];
        }

        var hints = new List<ProjectStructureExternalTargetHint>();
        foreach (var candidatePath in EnumerateAbsoluteExternalPathCandidates(searchText))
        {
            if (!TryNormalizeAbsoluteExternalPathCandidate(candidatePath, out var normalizedPath) ||
                !TryMapAbsoluteExternalPathToAlias(normalizedPath, out var alias))
            {
                continue;
            }

            if (hints.Any(item => string.Equals(item.MappedAlias, alias, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            hints.Add(new ProjectStructureExternalTargetHint(
                normalizedPath,
                alias,
                node.Id,
                node.Title));
        }

        return hints;
    }

    private static IEnumerable<string> EnumerateAbsoluteExternalPathCandidates(string text)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in WorkspacePathInToolRequestRegex.Matches(text))
        {
            var path = match.Groups["path"].Value;
            if (!string.IsNullOrWhiteSpace(path) &&
                path.Length >= 3 &&
                path[1] == ':' &&
                path[2] == '\\' &&
                seen.Add(path))
            {
                yield return path;
            }
        }

        foreach (Match match in Regex.Matches(
                     text,
                     @"\b(?<path>[A-Za-z]:\\\\[^\r\n`""']+)",
                     RegexOptions.CultureInvariant))
        {
            var path = match.Groups["path"].Value.Replace(@"\\", @"\");
            if (!string.IsNullOrWhiteSpace(path) && seen.Add(path))
            {
                yield return path;
            }
        }

        foreach (Match match in Regex.Matches(
                     text,
                     @"\b(?<path>[A-Za-z]:\\[^\r\n`""']+)",
                     RegexOptions.CultureInvariant))
        {
            var path = match.Groups["path"].Value;
            if (!string.IsNullOrWhiteSpace(path) && seen.Add(path))
            {
                yield return path;
            }
        }
    }

    private static bool TryNormalizeAbsoluteExternalPathCandidate(
        string? path,
        out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.Trim().Trim('`', '"', '\'');
        trimmed = Regex.Replace(
            trimmed,
            @"\\{2,}",
            "\\",
            RegexOptions.CultureInvariant);
        trimmed = StripEscapedLineBreakPathAnnotations(trimmed);
        trimmed = StripInlinePathAnnotations(trimmed);
        trimmed = Regex.Replace(
            trimmed,
            @"(?i)(?:\.\s+|\s+)(?:Acceptance|Accepted|Archetype|Deliverable|Exact|Requirement|Requirements|Required|Evidence|Validation|Validate|Tests?|Startup|Browser|Agents?|Use|The|This|Then|Next|No-go|Include|Includes)\b.*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        trimmed = Regex.Replace(
            trimmed,
            @"(?i)\s+(?:and|or)\s+(?:one|another|a|an|the|business|scenario|process|app|analysis|case)\b.*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        trimmed = Regex.Replace(
            trimmed,
            @"(?i)\s+(?:with|without)\s+(?:stack|process|business|scenario|analysis|app|application|tooling|assumptions)\b.*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        trimmed = trimmed.Trim().TrimEnd('\\', '/', '.', ',', ';', ':', ')', ']');

        if (trimmed.Length < 3 || trimmed[1] != ':' || trimmed[2] != '\\')
        {
            return false;
        }

        normalizedPath = trimmed;
        return true;
    }

    private static string StripEscapedLineBreakPathAnnotations(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(
                value,
                @"(?i)(?:\\|/)n(?:Acceptance|Accepted|Alias|Aliases|All|App|Application|Archetype|Code|Deliverable|Directory|Exact|Files?|Generated|Include|Includes|Mapped|Mapping|Node|No-go|Notes?|Output|Path|Product|Project|Requirement|Requirements|Required|Root|Source|Status|Workspace|Worksp|Evidence|Validation|Validate|Tests?|Startup|Browser|Agents?|Use|The|This|Then|Next)\b.*$",
                string.Empty,
                RegexOptions.CultureInvariant)
            .Trim();
    }

    private static string StripInlinePathAnnotations(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var stripped = Regex.Replace(
            value,
            @"(?i)(?:;\s*(?:notes?|type|status|subtitle|metadata|source|project|node|mapped)\b.*$|\s+\((?:maps?|mapped)\s+to\b.*$|\s+mapped\s+to\b.*$|\s+from\s+[^\\/]*$)",
            string.Empty,
            RegexOptions.CultureInvariant);
        stripped = Regex.Replace(
            stripped,
            @"(?i)\.[\\/]+n(?:all|generated|app(?:lication)?|archetype|deliverable|exact|include|includes|no-go|source|code|files?|root|directory)\b.*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        stripped = Regex.Replace(
            stripped,
            @"(?i)\.\s+(?:all|generated|app(?:lication)?|archetype|deliverable|exact|include|includes|no-go|source|code|files?|root|directory)\b.*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        stripped = Regex.Replace(
            stripped,
            @"(?i)\s+(?:Workspace\s+alias|Mapped\s+alias|Business-analysis|Business\s+analysis|All\s+generated|All\s+app(?:lication)?|Generated\s+app(?:lication)?|App(?:lication)?\s+source|Source\s+belongs|Code\s+belongs|Files?\s+belong|Output\s+directory|Acceptance|Archetype|Deliverable|Exact|Include|Includes|No-go|Preservation\s+rule|Agents?\s+must|Use\s+only|Do\s+not|The\s+app|This\s+app)\b.*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        stripped = Regex.Replace(
            stripped,
            @"(?i)\s+(?:and|or)\s*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        stripped = Regex.Replace(
            stripped,
            @"(?i)\s+(?:and|or)\s+(?:one|another|a|an|the|business|scenario|process|app|analysis|case)\b.*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        stripped = Regex.Replace(
            stripped,
            @"(?i)\s+(?:with|without)\s+(?:stack|process|business|scenario|analysis|app|application|tooling|assumptions)\b.*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        return stripped.Trim();
    }

    private static bool TrySplitExternalTargetAliasForScaffold(
        string? mappedAlias,
        out string parentAlias,
        out string leafName)
    {
        parentAlias = string.Empty;
        leafName = string.Empty;
        if (string.IsNullOrWhiteSpace(mappedAlias))
        {
            return false;
        }

        var normalized = NormalizeExternalTargetAlias(mappedAlias);
        if (!normalized.StartsWith($"{ExternalTargetAliasRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var lastSlashIndex = normalized.LastIndexOf('/');
        if (lastSlashIndex < ExternalTargetAliasRoot.Length + 2 ||
            lastSlashIndex >= normalized.Length - 1)
        {
            return false;
        }

        parentAlias = normalized[..lastSlashIndex];
        leafName = normalized[(lastSlashIndex + 1)..];
        return !string.IsNullOrWhiteSpace(parentAlias) &&
               !string.IsNullOrWhiteSpace(leafName);
    }

    private static void AppendProjectStructureGroundingNodes(
        StringBuilder builder,
        IReadOnlyList<ProjectStructureGroundingNodeData> nodes)
    {
        foreach (var node in nodes)
        {
            builder.AppendLine($"- {BuildProjectStructureGroundingNodeSummary(node)}");
        }
    }

    private static string BuildProjectStructureGroundingNodeSummary(ProjectStructureGroundingNodeData node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var segments = new List<string>
        {
            $"{node.Title} ({node.Id})",
            $"type: {node.ObjectType}/{NormalizeProjectStructureNodeSubtype(node.ObjectSubtype)}"
        };

        if (!string.IsNullOrWhiteSpace(node.Status))
        {
            segments.Add($"status: {CollapsePromptWhitespace(node.Status)}");
        }

        if (!string.IsNullOrWhiteSpace(node.Subtitle))
        {
            segments.Add($"subtitle: {TrimProjectStructureGroundingText(node.Subtitle, 140)}");
        }

        if (!string.IsNullOrWhiteSpace(node.Notes))
        {
            segments.Add($"notes: {TrimProjectStructureGroundingText(node.Notes, 320)}");
        }

        var metadataSummary = NormalizeProjectStructureMetadataSummary(node.MetadataJson);
        if (!string.IsNullOrWhiteSpace(metadataSummary))
        {
            segments.Add($"metadata: {metadataSummary}");
        }

        return string.Join("; ", segments);
    }

    private static bool HasProjectStructureGroundingSignal(ProjectStructureGroundingNodeData node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return GetProjectStructureGroundingSignalScore(node) > 0;
    }

    private static int GetProjectStructureGroundingSignalScore(ProjectStructureGroundingNodeData node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var score = 0;
        if (!string.IsNullOrWhiteSpace(node.Notes))
        {
            score += 4;
        }

        if (!string.IsNullOrWhiteSpace(node.Subtitle))
        {
            score += 3;
        }

        if (!string.IsNullOrWhiteSpace(NormalizeProjectStructureMetadataSummary(node.MetadataJson)))
        {
            score += 2;
        }

        if (LooksLikeProjectStructureConstraintTitle(node.Title))
        {
            score += 5;
        }

        if (LooksLikeProjectStructureFeatureTitle(node.Title))
        {
            score += 3;
        }

        return score;
    }

    private static bool LooksLikeProjectStructureConstraintTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var normalizedTitle = CollapsePromptWhitespace(title);
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return false;
        }

        return normalizedTitle.Contains("output", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("must", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("required", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("directory", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("path", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("place", StringComparison.OrdinalIgnoreCase) ||
               Regex.IsMatch(
                   normalizedTitle,
                   @"\b[a-zA-Z]:\\",
                RegexOptions.CultureInvariant) ||
               normalizedTitle.Contains("external-target/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeProjectStructureFeatureTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var normalizedTitle = CollapsePromptWhitespace(title);
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return false;
        }

        return normalizedTitle.Contains("feature", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("workflow", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("button", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("history", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("screen", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("page", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("form", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("ui", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("route", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProjectStructureGroundingNoiseNode(ProjectStructureGroundingNodeData node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return string.Equals(node.ObjectType, "ProcessRun", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(node.ObjectType, "File", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeProjectStructureNodeId(string? nodeId)
        => string.IsNullOrWhiteSpace(nodeId) ? string.Empty : nodeId.Trim();

    private static string NormalizeProjectStructureNodeSubtype(string? objectSubtype)
        => string.IsNullOrWhiteSpace(objectSubtype) ? "default" : CollapsePromptWhitespace(objectSubtype);

    private static string TrimProjectStructureGroundingText(string? value, int maxLength)
    {
        var collapsed = CollapsePromptWhitespace(value);
        if (collapsed.Length <= maxLength)
        {
            return collapsed;
        }

        return $"{collapsed[..Math.Max(0, maxLength - 3)].TrimEnd()}...";
    }

    private static string CollapsePromptWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(
                value,
                @"\s+",
                " ",
                RegexOptions.CultureInvariant)
            .Trim();
    }

    private static string NormalizeProjectStructureMetadataSummary(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && !root.EnumerateObject().MoveNext())
            {
                return string.Empty;
            }

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            return TrimProjectStructureGroundingText(JsonSerializer.Serialize(root), 320);
        }
        catch (JsonException)
        {
            return TrimProjectStructureGroundingText(metadataJson, 320);
        }
    }

    private static IReadOnlyList<ProjectStructureGroundingNodeData> ExtractProjectStructureGroundingNodes(object surface)
    {
        var nodesValue = surface.GetType().GetProperty("Nodes")?.GetValue(surface) as IEnumerable;
        if (nodesValue is null)
        {
            return [];
        }

        var nodes = new List<ProjectStructureGroundingNodeData>();
        foreach (var node in nodesValue.Cast<object>())
        {
            var id = GetProjectStructureGroundingString(node, "Id");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            nodes.Add(new ProjectStructureGroundingNodeData(
                id,
                GetProjectStructureGroundingString(node, "ParentId"),
                GetProjectStructureGroundingString(node, "ObjectType"),
                GetProjectStructureGroundingString(node, "ObjectSubtype"),
                GetProjectStructureGroundingString(node, "Title"),
                GetProjectStructureGroundingString(node, "Subtitle"),
                GetProjectStructureGroundingString(node, "Status"),
                GetProjectStructureGroundingString(node, "Notes"),
                GetProjectStructureGroundingString(node, "MetadataJson")));
        }

        return nodes;
    }

    private static string GetProjectStructureGroundingString(object source, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var value = source.GetType().GetProperty(propertyName)?.GetValue(source);
        return value?.ToString()?.Trim() ?? string.Empty;
    }

    private static bool IsSuccessfulUpstreamValidationReceipt(ToolExecutionReceiptRecord receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        if (IsFailedToolReceipt(receipt))
        {
            return false;
        }

        return IsImplementationValidationToolName(NormalizeToolToken(receipt.ToolName));
    }

    private static bool MentionsRepeatedToolInvocation(string? text)
    {
        return !string.IsNullOrWhiteSpace(text) &&
               text.Contains("repeated identical tool invocation", StringComparison.OrdinalIgnoreCase);
    }

}
