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

        var match = Regex.Match(
            groundingSummary,
            @"\b(?<path>[A-Za-z]:\\[A-Za-z0-9 _.\-\\]+)",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        var candidatePath = match.Groups["path"].Value.Trim().TrimEnd('\\');
        if (candidatePath.Length < 3 || candidatePath[1] != ':' || candidatePath[2] != '\\')
        {
            return false;
        }

        var driveLetter = char.ToUpperInvariant(candidatePath[0]);
        var remainder = candidatePath.Length == 3
            ? string.Empty
            : candidatePath[3..].Replace('\\', '/');
        absolutePath = candidatePath;
        mappedAlias = string.IsNullOrWhiteSpace(remainder)
            ? $"external-target/{driveLetter}"
            : $"external-target/{driveLetter}/{remainder}";
        return true;
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
               normalizedTitle.Contains("keypad", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("keyboard", StringComparison.OrdinalIgnoreCase) ||
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
