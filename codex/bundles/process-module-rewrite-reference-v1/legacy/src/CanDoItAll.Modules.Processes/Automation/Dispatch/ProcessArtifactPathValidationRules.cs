using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactPathValidationRules
{
    public static string NormalizeManagedPathReference(string path)
    {
        return WorkspaceScopeDescriptor.NormalizeRelativePath(path)
            .Trim('`', '\'', '"', ',', ';', '.', ':', ')', ']', '}');
    }

    public static bool IsShallowSharedManagedArtifactPath(string path)
    {
        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return false;
        }

        var segments = normalizedPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Any(IsRedactedManagedPathSegment))
        {
            return false;
        }

        if (segments.Any(segment => string.Equals(segment, "process-runs", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (segments.Length == 2 &&
            IsManagedRootSegment(segments[0]) &&
            Path.HasExtension(segments[1]))
        {
            return true;
        }

        return segments.Length is 4 or 5 &&
               IsManagedRootSegment(segments[0]) &&
               string.Equals(segments[1], "scopes", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsManagedRootSegment(string segment)
    {
        return string.Equals(segment, "artifacts", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "output", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "integration-map", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "data", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryExtractExpectedArtifactRelativePath(string validationRequirementSummary, out string relativePath)
    {
        foreach (var marker in new[]
                 {
                     "Create this artifact at ",
                     "must exist at ",
                     "must be written at "
                 })
        {
            var markerIndex = validationRequirementSummary.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                continue;
            }

            var startIndex = markerIndex + marker.Length;
            var remainder = validationRequirementSummary[startIndex..].TrimStart();
            if (string.IsNullOrWhiteSpace(remainder))
            {
                continue;
            }

            var endIndex = remainder.IndexOfAny([' ', '\r', '\n', '\t']);
            var token = endIndex >= 0
                ? remainder[..endIndex]
                : remainder;
            token = token.Trim().TrimEnd('.', ',', ';', ':').Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            relativePath = token;
            return true;
        }

        relativePath = string.Empty;
        return false;
    }

    public static bool ExpectedArtifactExplicitlyTargetsPath(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string normalizedPath)
    {
        return TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var expectedRelativePath) &&
               string.Equals(
                   NormalizeManagedRelativePathForComparison(expectedRelativePath),
                   NormalizeManagedRelativePathForComparison(normalizedPath),
                   StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeManagedRelativePathForComparison(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var segments = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length >= 5 &&
            IsManagedRootSegment(segments[0]) &&
            string.Equals(segments[1], "scopes", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join('/', [segments[0], .. segments.Skip(4)]);
        }

        return normalized;
    }

    private static bool IsRedactedManagedPathSegment(string segment)
    {
        var trimmed = segment.Trim();
        return string.Equals(trimmed, "...", StringComparison.Ordinal) ||
               string.Equals(trimmed, "…", StringComparison.Ordinal) ||
               trimmed.Contains("...", StringComparison.Ordinal) ||
               trimmed.StartsWith("<", StringComparison.Ordinal) ||
               trimmed.EndsWith(">", StringComparison.Ordinal) ||
               trimmed.StartsWith("{", StringComparison.Ordinal) ||
               trimmed.EndsWith("}", StringComparison.Ordinal);
    }
}
