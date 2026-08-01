using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureProcessNodeKeys
{
    public const string ProcessDefinitionPrefix = "process-definition:";
    public const string ProcessRunPrefix = "process-run:";
    public const string ProcessRunOutputPrefix = "process-run-output:";
    public const string ProcessRunSummaryPrefix = "process-run-summary:";
    public const string ProcessRunScreenshotPrefix = "process-run-screenshot:";
    public const string ProcessRunRuntimePrefix = "process-run-runtime:";
    public const string ProcessRunScreenshotArtifactKind = "process-run-screenshot";

    public static string BuildProcessDefinitionNodeKey(Guid definitionId)
    {
        return $"{ProcessDefinitionPrefix}{definitionId:D}";
    }

    public static string BuildProcessRunNodeKey(Guid runId)
    {
        return $"{ProcessRunPrefix}{runId:D}";
    }

    public static string BuildProcessRunOutputNodeKey(Guid runId, string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        return BuildHashedProcessRunChildNodeKey(ProcessRunOutputPrefix, runId, directoryPath);
    }

    public static string BuildProcessRunSummaryNodeKey(Guid runId)
    {
        return $"{ProcessRunSummaryPrefix}{runId:D}";
    }

    public static string BuildProcessRunScreenshotNodeKey(Guid runId, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        return BuildHashedProcessRunChildNodeKey(ProcessRunScreenshotPrefix, runId, relativePath);
    }

    public static string BuildProcessRunRuntimeNodeKey(Guid runId, string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        return BuildHashedProcessRunChildNodeKey(ProcessRunRuntimePrefix, runId, projectPath);
    }

    public static bool TryParseProcessRunSummaryNodeKey(string nodeKey, out Guid runId)
    {
        return TryParsePrefixedGuidNodeKey(nodeKey, ProcessRunSummaryPrefix, out runId);
    }

    public static bool TryParseProcessRunScreenshotNodeKey(string nodeKey, out Guid runId)
    {
        return TryParseHashedProcessRunChildNodeKey(nodeKey, ProcessRunScreenshotPrefix, out runId);
    }

    public static bool TryParseProcessRunRuntimeNodeKey(string nodeKey, out Guid runId)
    {
        return TryParseHashedProcessRunChildNodeKey(nodeKey, ProcessRunRuntimePrefix, out runId);
    }

    private static string BuildHashedProcessRunChildNodeKey(string prefix, Guid runId, string value)
    {
        var normalizedPath = value
            .Trim()
            .Replace('\\', '/')
            .Trim('/');
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath)))
            .ToLowerInvariant()[..16];
        return $"{prefix}{runId:D}:{hash}";
    }

    public static bool TryParseProcessDefinitionNodeKey(string nodeKey, out Guid definitionId)
    {
        return TryParsePrefixedGuidNodeKey(nodeKey, ProcessDefinitionPrefix, out definitionId);
    }

    public static bool TryParseProcessRunNodeKey(string nodeKey, out Guid runId)
    {
        return TryParsePrefixedGuidNodeKey(nodeKey, ProcessRunPrefix, out runId);
    }

    public static bool TryParseProcessRunOutputNodeKey(string nodeKey, out Guid runId)
    {
        return TryParseHashedProcessRunChildNodeKey(nodeKey, ProcessRunOutputPrefix, out runId);
    }

    private static bool TryParsePrefixedGuidNodeKey(string nodeKey, string prefix, out Guid value)
    {
        if (nodeKey.StartsWith(prefix, StringComparison.Ordinal) &&
            Guid.TryParse(nodeKey[prefix.Length..], out value))
        {
            return true;
        }

        value = Guid.Empty;
        return false;
    }

    private static bool TryParseHashedProcessRunChildNodeKey(string nodeKey, string prefix, out Guid runId)
    {
        runId = Guid.Empty;
        if (!nodeKey.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var remaining = nodeKey[prefix.Length..];
        var separatorIndex = remaining.IndexOf(':', StringComparison.Ordinal);
        var runIdText = separatorIndex < 0
            ? remaining
            : remaining[..separatorIndex];
        return Guid.TryParse(runIdText, out runId);
    }
}
