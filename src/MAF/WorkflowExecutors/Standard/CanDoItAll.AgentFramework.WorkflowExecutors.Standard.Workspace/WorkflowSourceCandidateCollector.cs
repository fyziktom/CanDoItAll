using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

internal sealed class WorkflowSourceCandidateCollector
{
    private static readonly char[] PathTrimCharacters = [' ', '\t', '\r', '\n', '`', '\'', '"'];

    public IReadOnlyList<WorkflowSourceCandidate> Collect(
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
        ICollection<WorkflowSourceCandidate> candidates,
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
        ICollection<WorkflowSourceCandidate> candidates,
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

    private static bool ShouldIncludeKey(string key, IReadOnlySet<string> sourceKeys)
        => sourceKeys.Count == 0 || sourceKeys.Contains(key);

    private static bool IsPathSourceKind(string kind)
        => string.Equals(kind, "filePath", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(kind, "folderPath", StringComparison.OrdinalIgnoreCase);

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
        => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool LooksLikeFolderPath(string value)
    {
        var normalized = value.Trim(PathTrimCharacters);
        return normalized.EndsWith(Path.DirectorySeparatorChar) ||
               normalized.EndsWith(Path.AltDirectorySeparatorChar) ||
               string.IsNullOrWhiteSpace(Path.GetExtension(normalized));
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
