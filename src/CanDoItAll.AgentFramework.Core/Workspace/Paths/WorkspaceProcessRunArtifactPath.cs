using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkspaceProcessRunArtifactPath
{
    private const string DotnetRunReceiptNamespace = "dotnet-run";

    public static bool TryResolveRunId(
        string? path,
        out string processRunId,
        out string artifactSuffix)
    {
        processRunId = string.Empty;
        artifactSuffix = string.Empty;

        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(path ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return false;
        }

        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length >= 3 &&
            string.Equals(segments[0], "artifacts", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[1], "process-runs", StringComparison.OrdinalIgnoreCase))
        {
            return TryResolveRunIdSegment(segments[2], segments, 3, out processRunId, out artifactSuffix);
        }

        if (segments.Length >= 2 &&
            string.Equals(segments[0], "process-runs", StringComparison.OrdinalIgnoreCase))
        {
            return TryResolveRunIdSegment(segments[1], segments, 2, out processRunId, out artifactSuffix);
        }

        if (segments.Length >= 6 &&
            string.Equals(segments[0], "artifacts", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[1], "scopes", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[4], "process-runs", StringComparison.OrdinalIgnoreCase))
        {
            return TryResolveRunIdSegment(segments[5], segments, 6, out processRunId, out artifactSuffix);
        }

        return false;
    }

    public static bool IsMalformedRunId(string processRunId)
        => ContainsEllipsis(processRunId) ||
           !Guid.TryParse(processRunId, out _);

    public static bool IsRecoverableCurrentRunAlias(string referencedRunId, string? currentRunId)
    {
        if (string.IsNullOrWhiteSpace(referencedRunId) ||
            string.IsNullOrWhiteSpace(currentRunId) ||
            !Guid.TryParse(currentRunId.Trim(), out var currentRunGuid))
        {
            return false;
        }

        if (Guid.TryParse(referencedRunId.Trim(), out var referencedRunGuid))
        {
            return referencedRunGuid == currentRunGuid;
        }

        var compactReferenced = CompactHex(referencedRunId);
        if (string.IsNullOrWhiteSpace(compactReferenced))
        {
            return false;
        }

        var compactCurrent = currentRunGuid.ToString("N");
        if (ContainsEllipsis(referencedRunId))
        {
            return compactReferenced.Length >= 2 &&
                   compactCurrent.StartsWith(compactReferenced, StringComparison.OrdinalIgnoreCase);
        }

        return compactReferenced.Length >= 16 &&
               (compactCurrent.StartsWith(compactReferenced, StringComparison.OrdinalIgnoreCase) ||
                compactReferenced.StartsWith(compactCurrent, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsRecoverableMalformedCurrentRunPath(string? path, string? currentRunId)
    {
        return TryResolveRunId(path, out var referencedRunId, out _) &&
               IsMalformedRunId(referencedRunId) &&
               IsRecoverableCurrentRunAlias(referencedRunId, currentRunId);
    }

    public static bool TryBuildRecoverableCurrentRunPath(
        string? path,
        string? currentRunId,
        WorkspaceScopeDescriptor? workspaceScope,
        out string currentRunPath)
    {
        currentRunPath = string.Empty;
        if (!TryResolveRunId(path, out var referencedRunId, out var artifactSuffix) ||
            !IsMalformedRunId(referencedRunId) ||
            !IsRecoverableCurrentRunAlias(referencedRunId, currentRunId) ||
            !Guid.TryParse(currentRunId?.Trim(), out var currentRunGuid))
        {
            return false;
        }

        var scope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;
        var normalizedRunId = currentRunGuid.ToString("D");
        currentRunPath = string.IsNullOrWhiteSpace(artifactSuffix)
            ? scope.CombineArtifactPath("process-runs", normalizedRunId)
            : scope.CombineArtifactPath("process-runs", normalizedRunId, artifactSuffix);
        return true;
    }

    private static bool ContainsEllipsis(string value)
        => value.Contains("...", StringComparison.Ordinal) ||
           value.Contains('…');

    private static string CompactHex(string value)
    {
        var characters = value
            .Where(Uri.IsHexDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();
        return new string(characters);
    }

    private static string JoinSuffix(string[] segments, int startIndex)
    {
        return startIndex >= segments.Length
            ? string.Empty
            : WorkspaceScopeDescriptor.NormalizeRelativePath(string.Join('/', segments.Skip(startIndex)));
    }

    private static bool TryResolveRunIdSegment(
        string candidateRunId,
        string[] segments,
        int suffixStartIndex,
        out string processRunId,
        out string artifactSuffix)
    {
        processRunId = string.Empty;
        artifactSuffix = string.Empty;

        if (IsReservedProcessRunArtifactNamespace(candidateRunId))
        {
            return false;
        }

        processRunId = candidateRunId;
        artifactSuffix = JoinSuffix(segments, suffixStartIndex);
        return true;
    }

    private static bool IsReservedProcessRunArtifactNamespace(string segment)
        => string.Equals(segment, DotnetRunReceiptNamespace, StringComparison.OrdinalIgnoreCase);
}
