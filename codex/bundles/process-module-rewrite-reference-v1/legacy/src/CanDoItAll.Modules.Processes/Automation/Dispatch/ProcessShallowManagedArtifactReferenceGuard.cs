using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessShallowManagedArtifactReferenceGuard
{
    public static string ResolveSummary(
        IReadOnlyList<string> observedPaths,
        string? responseText,
        IReadOnlyList<string> allowedExternalTargetAliases,
        Regex managedWorkspacePathRegex,
        Action<ISet<string>, string, IReadOnlyList<string>> addShallowPath)
    {
        var shallowPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in observedPaths)
        {
            addShallowPath(shallowPaths, path, allowedExternalTargetAliases);
            if (shallowPaths.Count >= 3)
            {
                break;
            }
        }

        if (shallowPaths.Count < 3 && !string.IsNullOrWhiteSpace(responseText))
        {
            foreach (Match match in managedWorkspacePathRegex.Matches(responseText))
            {
                addShallowPath(shallowPaths, match.Groups["path"].Value, allowedExternalTargetAliases);
                if (shallowPaths.Count >= 3)
                {
                    break;
                }
            }
        }

        return shallowPaths.Count == 0
            ? string.Empty
            : $"the run used shallow shared managed artifact paths instead of run-specific artifact paths: {string.Join(", ", shallowPaths)}";
    }
}

