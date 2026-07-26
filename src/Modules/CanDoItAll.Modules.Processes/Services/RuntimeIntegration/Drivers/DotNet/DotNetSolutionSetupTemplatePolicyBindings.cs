using System.Text.Json;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed record DotNetSolutionSetupTemplatePolicyBindings(
    IReadOnlyDictionary<string, IReadOnlyList<string>> RequiredPathsByStep,
    IReadOnlyDictionary<string, IReadOnlyList<string>> RequiredToolReceiptsByStep,
    IReadOnlyDictionary<string, IReadOnlyList<DotNetSolutionSetupTemplateReadbackCheck>> RequiredFileContentChecksByStep,
    IReadOnlyDictionary<string, IReadOnlyList<string>> ScopedLaunchVariablePrefixesByStep)
{
    private const string TemplateValueResolutionKey = "DotNetSolutionSetupPolicyTemplateValue";
    internal const string SolutionFileCandidatesVariableKey = "DotNetSolutionFileCandidates";
    internal const string SolutionFileCandidatesTemplate = "${DotNetSolutionFileCandidates}";
    internal const string RequiredPathsSettingKey = "ProductCompletionRequiredPathsByStep";
    internal const string RequiredToolReceiptsSettingKey = "ProductCompletionRequiredToolReceiptsByStep";
    internal const string RequiredFileContentChecksSettingKey = "ProductCompletionRequiredFileContentChecksByStep";
    internal const string ScopedLaunchVariablePrefixesSettingKey = "ProcessStepScopedLaunchVariablePrefixesByStep";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly LaunchVariableTemplateResolver TemplateResolver = new();

    public void ApplyTo(IDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);

        var templateVariables = new Dictionary<string, string>(variables, StringComparer.Ordinal);
        DotNetProcessLaunchVariableWriter.SetIfNotEmpty(
            variables,
            ProcessRuntimeLaunchVariables.ProductCompletionRequiredPathsByStep,
            JsonSerializer.Serialize(
                ResolveStringMap(RequiredPathsByStep, RequiredPathsSettingKey, templateVariables),
                JsonOptions));
        DotNetProcessLaunchVariableWriter.SetIfNotEmpty(
            variables,
            ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep,
            JsonSerializer.Serialize(
                ResolveStringMap(RequiredToolReceiptsByStep, RequiredToolReceiptsSettingKey, templateVariables),
                JsonOptions));
        DotNetProcessLaunchVariableWriter.SetIfNotEmpty(
            variables,
            ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep,
            JsonSerializer.Serialize(
                ResolveReadbackChecks(RequiredFileContentChecksByStep, templateVariables),
                JsonOptions));
        DotNetProcessLaunchVariableWriter.SetIfNotEmpty(
            variables,
            ProcessRuntimeLaunchVariables.ProcessStepScopedLaunchVariablePrefixesByStep,
            JsonSerializer.Serialize(
                ResolveStringMap(
                    ScopedLaunchVariablePrefixesByStep,
                    ScopedLaunchVariablePrefixesSettingKey,
                    templateVariables),
                JsonOptions));
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveStringMap(
        IReadOnlyDictionary<string, IReadOnlyList<string>> source,
        string settingKey,
        IReadOnlyDictionary<string, string> variables)
    {
        return source.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<string>)item.Value
                .Select(value => ResolveTemplateValue(value, settingKey, item.Key, variables))
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<DotNetSolutionSetupTemplateReadbackCheck>> ResolveReadbackChecks(
        IReadOnlyDictionary<string, IReadOnlyList<DotNetSolutionSetupTemplateReadbackCheck>> source,
        IReadOnlyDictionary<string, string> variables)
    {
        return source.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<DotNetSolutionSetupTemplateReadbackCheck>)item.Value
                 .Select(check => new DotNetSolutionSetupTemplateReadbackCheck(
                     ResolveReadbackPathCandidates(check.PathCandidates, item.Key, variables),
                     check.RequiredTextAnyGroups
                        .Select(group => (IReadOnlyList<string>)group
                            .Select(value => ResolveTemplateValue(
                                value,
                                RequiredFileContentChecksSettingKey,
                                item.Key,
                                variables))
                            .ToArray())
                        .ToArray()))
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ResolveReadbackPathCandidates(
        IReadOnlyList<string> source,
        string stepKey,
        IReadOnlyDictionary<string, string> variables)
    {
        var resolved = new List<string>();
        foreach (var value in source)
        {
            if (string.Equals(value.Trim(), SolutionFileCandidatesTemplate, StringComparison.Ordinal))
            {
                if (!variables.TryGetValue(SolutionFileCandidatesVariableKey, out var candidates) ||
                    string.IsNullOrWhiteSpace(candidates))
                {
                    throw new InvalidOperationException(
                        $"The .NET launch driver setting '{RequiredFileContentChecksSettingKey}' for step '{stepKey}' requires non-empty launch variable '{SolutionFileCandidatesVariableKey}'.");
                }

                foreach (var candidate in candidates.Split(
                             ';',
                             StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!resolved.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                    {
                        resolved.Add(candidate);
                    }
                }

                continue;
            }

            var resolvedValue = ResolveTemplateValue(
                value,
                RequiredFileContentChecksSettingKey,
                stepKey,
                variables);
            if (!resolved.Contains(resolvedValue, StringComparer.OrdinalIgnoreCase))
            {
                resolved.Add(resolvedValue);
            }
        }

        return resolved;
    }

    private static string ResolveTemplateValue(
        string value,
        string settingKey,
        string stepKey,
        IReadOnlyDictionary<string, string> variables)
    {
        var resolutionVariables = new Dictionary<string, string>(variables, StringComparer.Ordinal)
        {
            [TemplateValueResolutionKey] = value
        };
        var resolution = TemplateResolver.Resolve(resolutionVariables);
        var resolutionDiagnostic = resolution.Diagnostics.FirstOrDefault(diagnostic =>
            string.Equals(
                diagnostic.VariableKey,
                TemplateValueResolutionKey,
                StringComparison.Ordinal));
        if (resolutionDiagnostic is not null)
        {
            throw new InvalidOperationException(
                $"The .NET launch driver setting '{settingKey}' for step '{stepKey}' could not resolve a template value: {resolutionDiagnostic.Message}");
        }

        return resolution.Variables[TemplateValueResolutionKey];
    }

    public static bool TryParse(
        ProcessLaunchDriverActivation activation,
        out DotNetSolutionSetupTemplatePolicyBindings bindings,
        out string issue)
    {
        ArgumentNullException.ThrowIfNull(activation);

        bindings = null!;
        if (!TryReadStringArrayMap(
                activation.Settings,
                RequiredPathsSettingKey,
                out var requiredPaths,
                out issue) ||
            !TryReadStringArrayMap(
                activation.Settings,
                RequiredToolReceiptsSettingKey,
                out var requiredToolReceipts,
                out issue) ||
            !TryReadReadbackCheckMap(
                activation.Settings,
                RequiredFileContentChecksSettingKey,
                out var requiredFileContentChecks,
                out issue) ||
            !TryReadStringArrayMap(
                activation.Settings,
                ScopedLaunchVariablePrefixesSettingKey,
                out var scopedLaunchVariablePrefixes,
                out issue))
        {
            return false;
        }

        bindings = new DotNetSolutionSetupTemplatePolicyBindings(
            requiredPaths,
            requiredToolReceipts,
            requiredFileContentChecks,
            scopedLaunchVariablePrefixes);
        issue = string.Empty;
        return true;
    }

    private static bool TryReadStringArrayMap(
        IReadOnlyDictionary<string, string> settings,
        string settingKey,
        out IReadOnlyDictionary<string, IReadOnlyList<string>> map,
        out string issue)
    {
        map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (!TryReadSettingObject(settings, settingKey, out var root, out issue))
        {
            return false;
        }

        var parsed = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in root.EnumerateObject())
        {
            if (!TryReadStepKey(property.Name, settingKey, out var stepKey, out issue) ||
                !TryReadNonEmptyStringArray(property.Value, settingKey, stepKey, out var values, out issue) ||
                !parsed.TryAdd(stepKey, values))
            {
                issue = string.IsNullOrWhiteSpace(issue)
                    ? $"The .NET launch driver setting '{settingKey}' declares duplicate step key '{property.Name}'."
                    : issue;
                return false;
            }
        }

        if (parsed.Count == 0)
        {
            issue = $"The .NET launch driver setting '{settingKey}' must declare at least one step policy.";
            return false;
        }

        map = parsed;
        issue = string.Empty;
        return true;
    }

    private static bool TryReadReadbackCheckMap(
        IReadOnlyDictionary<string, string> settings,
        string settingKey,
        out IReadOnlyDictionary<string, IReadOnlyList<DotNetSolutionSetupTemplateReadbackCheck>> map,
        out string issue)
    {
        map = new Dictionary<string, IReadOnlyList<DotNetSolutionSetupTemplateReadbackCheck>>(StringComparer.OrdinalIgnoreCase);
        if (!TryReadSettingObject(settings, settingKey, out var root, out issue))
        {
            return false;
        }

        var parsed = new Dictionary<string, IReadOnlyList<DotNetSolutionSetupTemplateReadbackCheck>>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in root.EnumerateObject())
        {
            if (!TryReadStepKey(property.Name, settingKey, out var stepKey, out issue) ||
                property.Value.ValueKind != JsonValueKind.Array ||
                property.Value.GetArrayLength() == 0)
            {
                issue = string.IsNullOrWhiteSpace(issue)
                    ? $"The .NET launch driver setting '{settingKey}' must declare a non-empty readback-check array for step '{property.Name}'."
                    : issue;
                return false;
            }

            var checks = new List<DotNetSolutionSetupTemplateReadbackCheck>();
            foreach (var check in property.Value.EnumerateArray())
            {
                if (check.ValueKind != JsonValueKind.Object ||
                    !check.TryGetProperty("pathCandidates", out var pathCandidates) ||
                    !TryReadNonEmptyStringArray(pathCandidates, settingKey, stepKey, out var paths, out issue) ||
                    !check.TryGetProperty("requiredTextAnyGroups", out var requiredTextAnyGroups) ||
                    !TryReadNonEmptyStringGroups(requiredTextAnyGroups, settingKey, stepKey, out var groups, out issue))
                {
                    issue = string.IsNullOrWhiteSpace(issue)
                        ? $"The .NET launch driver setting '{settingKey}' has an invalid readback check for step '{stepKey}'."
                        : issue;
                    return false;
                }

                checks.Add(new DotNetSolutionSetupTemplateReadbackCheck(paths, groups));
            }

            if (!parsed.TryAdd(stepKey, checks))
            {
                issue = $"The .NET launch driver setting '{settingKey}' declares duplicate step key '{stepKey}'.";
                return false;
            }
        }

        if (parsed.Count == 0)
        {
            issue = $"The .NET launch driver setting '{settingKey}' must declare at least one step policy.";
            return false;
        }

        map = parsed;
        issue = string.Empty;
        return true;
    }

    private static bool TryReadSettingObject(
        IReadOnlyDictionary<string, string> settings,
        string settingKey,
        out JsonElement root,
        out string issue)
    {
        root = default;
        issue = string.Empty;
        var setting = settings
            .FirstOrDefault(candidate => string.Equals(candidate.Key, settingKey, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(setting.Value))
        {
            issue = $"The .NET launch driver requires non-empty setting '{settingKey}'.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(setting.Value);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                issue = $"The .NET launch driver setting '{settingKey}' must be a JSON object.";
                return false;
            }

            root = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            issue = $"The .NET launch driver setting '{settingKey}' must be valid JSON.";
            return false;
        }
    }

    private static bool TryReadStepKey(
        string value,
        string settingKey,
        out string stepKey,
        out string issue)
    {
        stepKey = value?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(stepKey))
        {
            issue = string.Empty;
            return true;
        }

        issue = $"The .NET launch driver setting '{settingKey}' must use non-empty process step keys.";
        return false;
    }

    private static bool TryReadNonEmptyStringArray(
        JsonElement element,
        string settingKey,
        string stepKey,
        out IReadOnlyList<string> values,
        out string issue)
    {
        values = [];
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() == 0)
        {
            issue = $"The .NET launch driver setting '{settingKey}' must declare a non-empty string array for step '{stepKey}'.";
            return false;
        }

        var parsed = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            var value = item.ValueKind == JsonValueKind.String
                ? item.GetString()?.Trim()
                : null;
            if (string.IsNullOrWhiteSpace(value))
            {
                issue = $"The .NET launch driver setting '{settingKey}' must declare only non-empty strings for step '{stepKey}'.";
                return false;
            }

            parsed.Add(value);
        }

        values = parsed;
        issue = string.Empty;
        return true;
    }

    private static bool TryReadNonEmptyStringGroups(
        JsonElement element,
        string settingKey,
        string stepKey,
        out IReadOnlyList<IReadOnlyList<string>> groups,
        out string issue)
    {
        groups = [];
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() == 0)
        {
            issue = $"The .NET launch driver setting '{settingKey}' must declare non-empty requiredTextAnyGroups for step '{stepKey}'.";
            return false;
        }

        var parsed = new List<IReadOnlyList<string>>();
        foreach (var group in element.EnumerateArray())
        {
            if (!TryReadNonEmptyStringArray(group, settingKey, stepKey, out var values, out issue))
            {
                return false;
            }

            parsed.Add(values);
        }

        groups = parsed;
        issue = string.Empty;
        return true;
    }
}

internal sealed record DotNetSolutionSetupTemplateReadbackCheck(
    IReadOnlyList<string> PathCandidates,
    IReadOnlyList<IReadOnlyList<string>> RequiredTextAnyGroups);
