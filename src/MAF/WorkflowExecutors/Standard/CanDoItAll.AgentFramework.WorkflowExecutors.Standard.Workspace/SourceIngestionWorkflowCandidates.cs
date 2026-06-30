using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Xml.Linq;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using ExcelDataReader;
using UglyToad.PdfPig;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

public sealed partial class SourceIngestionWorkflowExecutor
{
    private static IReadOnlyList<WorkflowSourceCandidate> CollectCandidates(
        JsonElement root,
        WorkflowSourceIngestionExecutorSettings settings,
        IReadOnlySet<string> sourceKeys)
    {
        var candidates = new List<WorkflowSourceCandidate>();
        if (settings.IncludeAdditionalSources &&
            root.TryGetProperty("sources", out var sources) &&
            sources.ValueKind == JsonValueKind.Array)
        {
            foreach (var source in sources.EnumerateArray())
            {
                if (TryReadBoolean(source, "isEnabled", out var isEnabled) && !isEnabled)
                {
                    continue;
                }

                var kind = ReadString(source, "kind");
                if (!IsPathSourceKind(kind))
                {
                    continue;
                }

                var key = ReadString(source, "key");
                if (!ShouldIncludeKey(key, sourceKeys))
                {
                    continue;
                }

                var value = ReadString(source, "value");
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                candidates.Add(new WorkflowSourceCandidate(
                    key,
                    ReadString(source, "label"),
                    kind,
                    value,
                    "additional-source"));
            }
        }

        if (settings.IncludeAdditionalSources &&
            root.TryGetProperty("outputPath", out var outputPathProperty) &&
            outputPathProperty.ValueKind == JsonValueKind.String)
        {
            var outputPath = outputPathProperty.GetString();
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                candidates.Add(new WorkflowSourceCandidate(
                    "outputPath",
                    "Previous executor output",
                    "filePath",
                    outputPath,
                    "executor-output"));
            }
        }

        if (settings.IncludeParentNodePath && root.TryGetProperty("parentNode", out var parentNode))
        {
            AddNodeCandidate(candidates, parentNode, "parent-node", sourceKeys);
        }

        if (settings.IncludeSelectedNodePaths && root.TryGetProperty("selectedNodes", out var selectedNodes))
        {
            AddNodeCandidates(candidates, selectedNodes, "selected-node", sourceKeys);
        }

        if (settings.IncludeParentSubtreePaths && root.TryGetProperty("parentSubtree", out var parentSubtree))
        {
            AddNodeCandidates(candidates, parentSubtree, "parent-subtree", sourceKeys);
        }

        return candidates;
    }

    private static void AddNodeCandidates(
        List<WorkflowSourceCandidate> candidates,
        JsonElement nodes,
        string origin,
        IReadOnlySet<string> sourceKeys)
    {
        if (nodes.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var node in nodes.EnumerateArray())
        {
            AddNodeCandidate(candidates, node, origin, sourceKeys);
        }
    }

    private static void AddNodeCandidate(
        List<WorkflowSourceCandidate> candidates,
        JsonElement node,
        string origin,
        IReadOnlySet<string> sourceKeys)
    {
        var nodeId = ReadString(node, "id");
        if (!ShouldIncludeKey(nodeId, sourceKeys))
        {
            return;
        }

        var mediaPath = ReadString(node, "mediaRelativePath");
        var notes = ReadString(node, "notes");
        var candidatePath = !string.IsNullOrWhiteSpace(mediaPath)
            ? mediaPath
            : ExtractPathLine(notes);
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return;
        }

        var kind = LooksLikeFolderPath(candidatePath) ? "folderPath" : "filePath";
        candidates.Add(new WorkflowSourceCandidate(
            string.IsNullOrWhiteSpace(nodeId) ? origin : nodeId,
            ReadString(node, "title"),
            kind,
            candidatePath,
            origin));
    }

    private static JsonElement? TryClone(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.Clone();
    }

    private static string BuildSourceSummary(
        IReadOnlyList<WorkflowSourceIngestionDocument> loaded,
        IReadOnlyList<WorkflowSourceIngestionError> errors,
        bool truncated)
    {
        var sourceText = loaded.Count == 1 ? "source" : "sources";
        var summary = $"Loaded {loaded.Count} {sourceText}";
        if (errors.Count > 0)
        {
            summary += $" with {errors.Count} error(s)";
        }

        if (truncated)
        {
            summary += "; content was truncated to workflow limits";
        }

        return summary + ".";
    }

    private static IReadOnlySet<string> NormalizeKeys(IReadOnlyList<string> sourceKeys)
        => sourceKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlySet<string> NormalizeExtensions(IReadOnlyList<string> extensions)
        => extensions
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Select(extension => extension.Trim().StartsWith(".", StringComparison.Ordinal)
                ? extension.Trim().ToLowerInvariant()
                : "." + extension.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool ShouldIncludeKey(string key, IReadOnlySet<string> sourceKeys)
        => sourceKeys.Count == 0 || sourceKeys.Contains(key);

    private static bool IsPathSourceKind(string kind)
        => string.Equals(kind, "filePath", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(kind, "folderPath", StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowedExtension(string fullPath, IReadOnlySet<string> allowedExtensions)
        => allowedExtensions.Count == 0 || allowedExtensions.Contains(Path.GetExtension(fullPath));

    private static string NormalizeInputPath(string value)
        => value.Trim(PathTrimCharacters).Replace('/', Path.DirectorySeparatorChar);

    private static string NormalizeAbsoluteDisplayPath(string value)
        => Path.GetFullPath(value).Replace('\\', '/');

    private static string ToDisplayPath(string fullPath, WorkspaceResolvedPath directory)
    {
        if (directory.IsWorkspacePath)
        {
            return NormalizeAbsoluteDisplayPath(fullPath).StartsWith(NormalizeAbsoluteDisplayPath(directory.FullPath), StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(directory.RelativePath, Path.GetRelativePath(directory.FullPath, fullPath)).Replace('\\', '/')
                : NormalizeAbsoluteDisplayPath(fullPath);
        }

        return NormalizeAbsoluteDisplayPath(fullPath);
    }

    private static string ExtractPathLine(string value)
    {
        foreach (var line in value.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = ExtractEmbeddedPath(line.Trim(PathTrimCharacters));
            if (Path.IsPathRooted(candidate) ||
                candidate.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith("external-target\\", StringComparison.OrdinalIgnoreCase) ||
                LooksLikeRelativeFilePath(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static string ExtractEmbeddedPath(string value)
    {
        var index = FindWindowsPathStart(value);
        return index > 0
            ? value[index..].Trim(PathTrimCharacters)
            : value;
    }

    private static int FindWindowsPathStart(string value)
    {
        for (var index = 0; index < value.Length - 2; index++)
        {
            if (IsAsciiLetter(value[index]) &&
                value[index + 1] == ':' &&
                value[index + 2] is '\\' or '/')
            {
                return index;
            }
        }

        return value.IndexOf(@"\\", StringComparison.Ordinal);
    }

    private static bool IsAsciiLetter(char value)
    {
        return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }

    private static bool LooksLikeFolderPath(string value)
    {
        var normalized = value.Trim(PathTrimCharacters);
        if (Directory.Exists(normalized))
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(Path.GetExtension(normalized));
    }

    private static bool LooksLikeRelativeFilePath(string value)
        => value.Contains('/') || value.Contains('\\') || !string.IsNullOrWhiteSpace(Path.GetExtension(value));

    private static string ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return string.Empty;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : property.GetRawText();
    }

    private static bool TryReadBoolean(JsonElement element, string propertyName, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

}
