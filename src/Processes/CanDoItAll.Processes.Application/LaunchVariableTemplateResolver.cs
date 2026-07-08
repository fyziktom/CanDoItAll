using System.Text.RegularExpressions;

namespace CanDoItAll.Processes.Application;

public interface ILaunchVariableTemplateResolver
{
    LaunchVariableTemplateResolutionResult Resolve(IReadOnlyDictionary<string, string> variables);
}

public sealed class LaunchVariableTemplateResolver : ILaunchVariableTemplateResolver
{
    private const int MaximumResolutionPasses = 8;
    private const int MaximumValuePreviewLength = 160;
    private static readonly Regex PlaceholderRegex = new(
        @"\{\{(?<key>[A-Za-z_][A-Za-z0-9_.:-]*)\}\}|\$\{(?<key>[A-Za-z_][A-Za-z0-9_.:-]*)\}|\{(?<key>[A-Za-z_][A-Za-z0-9_.:-]*)\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public LaunchVariableTemplateResolutionResult Resolve(IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);

        var resolvedVariables = NormalizeVariables(variables);
        var diagnostics = new List<LaunchVariableTemplateDiagnostic>();
        var cycleRootKeys = DetectCycles(resolvedVariables, diagnostics);
        var passCount = ResolveKnownPlaceholders(resolvedVariables, cycleRootKeys);

        AddRemainingPlaceholderDiagnostics(resolvedVariables, diagnostics, cycleRootKeys, passCount);

        return new LaunchVariableTemplateResolutionResult(
            resolvedVariables,
            DeduplicateDiagnostics(diagnostics),
            passCount);
    }

    public static bool IsToolCriticalKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return key.EndsWith("ScriptRef", StringComparison.Ordinal) ||
            key.EndsWith("ExecutionPlan", StringComparison.Ordinal) ||
            key.EndsWith("SideEffectManifest", StringComparison.Ordinal) ||
            key.StartsWith("ProductCompletionRequired", StringComparison.Ordinal) ||
            key.StartsWith("RequiredRuntimeTool", StringComparison.Ordinal) ||
            (key.StartsWith("Subprocess", StringComparison.Ordinal) &&
                key.Contains("Evidence", StringComparison.Ordinal)) ||
            key.Contains("ManagedArtifactRoot", StringComparison.Ordinal) ||
            key.Contains("ArtifactRef", StringComparison.Ordinal);
    }

    private static Dictionary<string, string> NormalizeVariables(IReadOnlyDictionary<string, string> variables)
    {
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var variable in variables)
        {
            normalized[variable.Key] = variable.Value ?? string.Empty;
        }

        return normalized;
    }

    private static HashSet<string> DetectCycles(
        IReadOnlyDictionary<string, string> variables,
        List<LaunchVariableTemplateDiagnostic> diagnostics)
    {
        var cycleRootKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variableKey in variables.Keys)
        {
            if (!TryFindCycle(variableKey, variables, out var cycle))
            {
                continue;
            }

            cycleRootKeys.Add(variableKey);
            diagnostics.Add(CreateCycleDiagnostic(variableKey, cycle));
        }

        return cycleRootKeys;
    }

    private static int ResolveKnownPlaceholders(
        Dictionary<string, string> variables,
        IReadOnlySet<string> cycleRootKeys)
    {
        var passCount = 0;
        for (var pass = 0; pass < MaximumResolutionPasses; pass++)
        {
            var changed = false;
            foreach (var variableKey in variables.Keys.ToArray())
            {
                if (cycleRootKeys.Contains(variableKey))
                {
                    continue;
                }

                var currentValue = variables[variableKey];
                var resolvedValue = PlaceholderRegex.Replace(
                    currentValue,
                    match => ResolvePlaceholderMatch(match, variables, cycleRootKeys));
                if (string.Equals(currentValue, resolvedValue, StringComparison.Ordinal))
                {
                    continue;
                }

                variables[variableKey] = resolvedValue;
                changed = true;
            }

            passCount = pass + 1;
            if (!changed)
            {
                break;
            }
        }

        return passCount;
    }

    private static string ResolvePlaceholderMatch(
        Match match,
        IReadOnlyDictionary<string, string> variables,
        IReadOnlySet<string> cycleRootKeys)
    {
        var placeholderKey = GetPlaceholderKey(match);
        return !cycleRootKeys.Contains(placeholderKey) && variables.TryGetValue(placeholderKey, out var replacement)
            ? replacement
            : match.Value;
    }

    private static void AddRemainingPlaceholderDiagnostics(
        IReadOnlyDictionary<string, string> variables,
        List<LaunchVariableTemplateDiagnostic> diagnostics,
        IReadOnlySet<string> cycleRootKeys,
        int passCount)
    {
        foreach (var variable in variables)
        {
            if (cycleRootKeys.Contains(variable.Key))
            {
                continue;
            }

            foreach (var placeholderKey in EnumeratePlaceholderKeys(variable.Value).Distinct(StringComparer.Ordinal))
            {
                var kind = variables.ContainsKey(placeholderKey) && passCount >= MaximumResolutionPasses
                    ? LaunchVariableTemplateDiagnosticKind.MaximumPassesExceeded
                    : LaunchVariableTemplateDiagnosticKind.UnresolvedPlaceholder;
                diagnostics.Add(CreateUnresolvedDiagnostic(kind, variable.Key, placeholderKey, variable.Value));
            }
        }
    }

    private static LaunchVariableTemplateDiagnostic CreateCycleDiagnostic(string variableKey, IReadOnlyList<string> cycle)
    {
        var placeholderKey = cycle.FirstOrDefault() ?? variableKey;
        var isToolCritical = IsToolCriticalKey(variableKey);
        var cyclePath = string.Join(" -> ", cycle);
        var message = $"Launch variable '{variableKey}' contains a placeholder cycle through '{placeholderKey}'. Cycle: {cyclePath}.";
        return new LaunchVariableTemplateDiagnostic(
            LaunchVariableTemplateDiagnosticKind.Cycle,
            variableKey,
            placeholderKey,
            isToolCritical,
            isToolCritical,
            message)
        {
            Cycle = cycle
        };
    }

    private static LaunchVariableTemplateDiagnostic CreateUnresolvedDiagnostic(
        LaunchVariableTemplateDiagnosticKind kind,
        string variableKey,
        string placeholderKey,
        string variableValue)
    {
        var isToolCritical = IsToolCriticalKey(variableKey);
        var message = kind == LaunchVariableTemplateDiagnosticKind.MaximumPassesExceeded
            ? $"Launch variable '{variableKey}' still contains placeholder '{placeholderKey}' after {MaximumResolutionPasses} resolution passes. Value preview: {CreateValuePreview(variableKey, variableValue)}."
            : $"Launch variable '{variableKey}' contains unresolved placeholder '{placeholderKey}'. Value preview: {CreateValuePreview(variableKey, variableValue)}.";
        return new LaunchVariableTemplateDiagnostic(
            kind,
            variableKey,
            placeholderKey,
            isToolCritical,
            isToolCritical,
            message);
    }

    private static IReadOnlyList<LaunchVariableTemplateDiagnostic> DeduplicateDiagnostics(
        IReadOnlyList<LaunchVariableTemplateDiagnostic> diagnostics)
    {
        return diagnostics
            .GroupBy(
                diagnostic => new
                {
                    diagnostic.Kind,
                    diagnostic.VariableKey,
                    diagnostic.PlaceholderKey
                })
            .Select(group => group.First())
            .ToArray();
    }

    private static bool TryFindCycle(
        string rootKey,
        IReadOnlyDictionary<string, string> variables,
        out IReadOnlyList<string> cycle)
    {
        var path = new List<string>();
        var activeKeys = new HashSet<string>(StringComparer.Ordinal);
        var visitedKeys = new HashSet<string>(StringComparer.Ordinal);
        return TryVisit(rootKey, variables, path, activeKeys, visitedKeys, out cycle);
    }

    private static bool TryVisit(
        string variableKey,
        IReadOnlyDictionary<string, string> variables,
        List<string> path,
        HashSet<string> activeKeys,
        HashSet<string> visitedKeys,
        out IReadOnlyList<string> cycle)
    {
        if (activeKeys.Contains(variableKey))
        {
            var cycleStart = path.FindIndex(key => string.Equals(key, variableKey, StringComparison.Ordinal));
            cycle = path
                .Skip(cycleStart < 0 ? 0 : cycleStart)
                .Append(variableKey)
                .ToArray();
            return true;
        }

        if (!visitedKeys.Add(variableKey) || !variables.TryGetValue(variableKey, out var value))
        {
            cycle = [];
            return false;
        }

        activeKeys.Add(variableKey);
        path.Add(variableKey);
        foreach (var dependencyKey in EnumeratePlaceholderKeys(value).Distinct(StringComparer.Ordinal))
        {
            if (!variables.ContainsKey(dependencyKey))
            {
                continue;
            }

            if (TryVisit(dependencyKey, variables, path, activeKeys, visitedKeys, out cycle))
            {
                return true;
            }
        }

        path.RemoveAt(path.Count - 1);
        activeKeys.Remove(variableKey);
        cycle = [];
        return false;
    }

    private static IEnumerable<string> EnumeratePlaceholderKeys(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            yield break;
        }

        foreach (Match match in PlaceholderRegex.Matches(value))
        {
            yield return GetPlaceholderKey(match);
        }
    }

    private static string GetPlaceholderKey(Match match)
    {
        return match.Groups["key"].Value;
    }

    private static string CreateValuePreview(string variableKey, string value)
    {
        if (IsSensitiveKey(variableKey))
        {
            return "<masked>";
        }

        return value.Length <= MaximumValuePreviewLength
            ? value
            : value[..MaximumValuePreviewLength] + "...";
    }

    private static bool IsSensitiveKey(string key)
    {
        return key.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("ApiKey", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record LaunchVariableTemplateResolutionResult(
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlyList<LaunchVariableTemplateDiagnostic> Diagnostics,
    int PassCount)
{
    public bool HasBlockingDiagnostics => Diagnostics.Any(diagnostic => diagnostic.IsBlocking);
}

public sealed record LaunchVariableTemplateDiagnostic(
    LaunchVariableTemplateDiagnosticKind Kind,
    string VariableKey,
    string PlaceholderKey,
    bool IsToolCritical,
    bool IsBlocking,
    string Message)
{
    public IReadOnlyList<string> Cycle { get; init; } = [];
}

public enum LaunchVariableTemplateDiagnosticKind
{
    UnresolvedPlaceholder,
    Cycle,
    MaximumPassesExceeded
}
