using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

internal enum ProcessProductReadbackFailureKind
{
    Unknown,
    ForbiddenTextPresent,
    RequiredTextMissing
}

internal sealed record ProcessProductReadbackFailure(
    ProcessProductReadbackFailureKind Kind,
    string Description);

internal static class ProcessProductReadbackFailureParser
{
    private const string FailureListMarker = " failed:";
    private const string ForbiddenTextMarker = " contains forbidden text ";
    private const string RequiredTextMissingMarker = " does not contain ";

    public static IReadOnlyList<ProcessProductReadbackFailure> Parse(
        string summary,
        IReadOnlyDictionary<string, string> launchVariables)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(launchVariables);

        var normalized = NormalizeProductRootAliases(summary, launchVariables);
        var markerIndex = normalized.IndexOf(FailureListMarker, StringComparison.OrdinalIgnoreCase);
        var failureList = markerIndex >= 0
            ? normalized[(markerIndex + FailureListMarker.Length)..]
            : normalized;

        return failureList
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(failure => !string.IsNullOrWhiteSpace(failure))
            .Select(NormalizeSentence)
            .Select(failure => new ProcessProductReadbackFailure(
                ResolveFailureKind(failure),
                failure))
            .DistinctBy(failure => failure.Description, StringComparer.Ordinal)
            .ToArray();
    }

    private static ProcessProductReadbackFailureKind ResolveFailureKind(string failure)
    {
        if (failure.Contains(ForbiddenTextMarker, StringComparison.OrdinalIgnoreCase))
        {
            return ProcessProductReadbackFailureKind.ForbiddenTextPresent;
        }

        return failure.Contains(RequiredTextMissingMarker, StringComparison.OrdinalIgnoreCase)
            ? ProcessProductReadbackFailureKind.RequiredTextMissing
            : ProcessProductReadbackFailureKind.Unknown;
    }

    private static string NormalizeProductRootAliases(
        string summary,
        IReadOnlyDictionary<string, string> launchVariables)
    {
        var normalized = ReplaceRoot(
            summary,
            launchVariables,
            ProcessRuntimeLaunchVariables.ProductRoot,
            ProcessRuntimeLaunchVariables.ProductRootAlias,
            ProcessRuntimeLaunchVariables.ExternalTargetRoot,
            ProcessRuntimeLaunchVariables.WorkspaceAlias);
        normalized = ReplaceRoot(
            normalized,
            launchVariables,
            ProcessRuntimeLaunchVariables.OutputRoot,
            ProcessRuntimeLaunchVariables.OutputRootAlias,
            ProcessRuntimeLaunchVariables.ExternalTargetRoot,
            ProcessRuntimeLaunchVariables.WorkspaceAlias);
        return normalized.Replace('\\', '/');
    }

    private static string ReplaceRoot(
        string value,
        IReadOnlyDictionary<string, string> launchVariables,
        string rootVariable,
        params string[] aliasVariables)
    {
        if (!TryGetResolvedVariable(launchVariables, rootVariable, out var root) ||
            !TryGetFirstResolvedVariable(launchVariables, aliasVariables, out var alias))
        {
            return value;
        }

        return value.Replace(
            root.TrimEnd('\\', '/'),
            alias.TrimEnd('\\', '/'),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetFirstResolvedVariable(
        IReadOnlyDictionary<string, string> launchVariables,
        IReadOnlyList<string> variableNames,
        out string value)
    {
        foreach (var variableName in variableNames)
        {
            if (TryGetResolvedVariable(launchVariables, variableName, out value))
            {
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetResolvedVariable(
        IReadOnlyDictionary<string, string> launchVariables,
        string variableName,
        out string value)
    {
        value = string.Empty;
        if (!launchVariables.TryGetValue(variableName, out var candidate) ||
            string.IsNullOrWhiteSpace(candidate) ||
            candidate.Contains('{', StringComparison.Ordinal) ||
            candidate.Contains('}', StringComparison.Ordinal))
        {
            return false;
        }

        value = candidate.Trim();
        return true;
    }

    private static string NormalizeSentence(string value)
    {
        var normalized = value.Trim();
        return normalized.EndsWith(".", StringComparison.Ordinal)
            ? normalized
            : $"{normalized}.";
    }
}
