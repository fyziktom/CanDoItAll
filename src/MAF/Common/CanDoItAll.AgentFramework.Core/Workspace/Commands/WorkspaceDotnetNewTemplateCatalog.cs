using System.Text.RegularExpressions;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkspaceDotnetNewTemplateCatalog
{
    private static readonly IReadOnlySet<string> NoTemplateOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> ProgressiveWebAppOptions = new HashSet<string>(
        ["--pwa"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly TemplateDefinition[] TemplateDefinitions =
    [
        new("blazor", WorkspaceDotnetNewTemplateKind.Application, NoTemplateOptions),
        new("blazorserver", WorkspaceDotnetNewTemplateKind.Application, NoTemplateOptions),
        new("blazorserver-empty", WorkspaceDotnetNewTemplateKind.Application, NoTemplateOptions),
        new("blazorwasm", WorkspaceDotnetNewTemplateKind.Application, ProgressiveWebAppOptions),
        new("blazorwasm-empty", WorkspaceDotnetNewTemplateKind.Application, ProgressiveWebAppOptions),
        new("classlib", WorkspaceDotnetNewTemplateKind.Application, NoTemplateOptions),
        new("console", WorkspaceDotnetNewTemplateKind.Application, NoTemplateOptions),
        new("mstest", WorkspaceDotnetNewTemplateKind.Test, NoTemplateOptions),
        new("mvc", WorkspaceDotnetNewTemplateKind.Application, NoTemplateOptions),
        new("nunit", WorkspaceDotnetNewTemplateKind.Test, NoTemplateOptions),
        new("razor", WorkspaceDotnetNewTemplateKind.Application, NoTemplateOptions),
        new("sln", WorkspaceDotnetNewTemplateKind.Solution, NoTemplateOptions),
        new("web", WorkspaceDotnetNewTemplateKind.Application, NoTemplateOptions),
        new("webapp", WorkspaceDotnetNewTemplateKind.Application, NoTemplateOptions),
        new("webapi", WorkspaceDotnetNewTemplateKind.Application, NoTemplateOptions),
        new("worker", WorkspaceDotnetNewTemplateKind.Application, NoTemplateOptions),
        new("xunit", WorkspaceDotnetNewTemplateKind.Test, NoTemplateOptions)
    ];

    private static readonly IReadOnlyList<string> approvedTemplates = Array.AsReadOnly(
        TemplateDefinitions
            .Select(definition => definition.Name)
            .ToArray());

    private static readonly IReadOnlyList<string> approvedApplicationTemplates = Array.AsReadOnly(
        TemplateDefinitions
            .Where(definition => definition.Kind == WorkspaceDotnetNewTemplateKind.Application)
            .Select(definition => definition.Name)
            .ToArray());

    private static readonly IReadOnlyList<string> approvedTestTemplates = Array.AsReadOnly(
        TemplateDefinitions
            .Where(definition => definition.Kind == WorkspaceDotnetNewTemplateKind.Test)
            .Select(definition => definition.Name)
            .ToArray());

    private static readonly IReadOnlyList<string> approvedTemplateOptions = Array.AsReadOnly(
        TemplateDefinitions
            .SelectMany(definition => definition.SupportedOptions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(option => option, StringComparer.OrdinalIgnoreCase)
            .ToArray());

    private static readonly IReadOnlyDictionary<string, TemplateDefinition> templatesByName =
        TemplateDefinitions.ToDictionary(definition => definition.Name, StringComparer.OrdinalIgnoreCase);

    private static readonly Regex TargetFrameworkPattern = new(
        @"^(?:net(?:[1-9]\d*)(?:\.\d+)?|netcoreapp\d+\.\d+|netstandard\d+\.\d+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static IReadOnlyList<string> ApprovedTemplates => approvedTemplates;

    public static IReadOnlyList<string> ApprovedApplicationTemplates => approvedApplicationTemplates;

    public static IReadOnlyList<string> ApprovedTestTemplates => approvedTestTemplates;

    public static IReadOnlyList<string> ApprovedTemplateOptions => approvedTemplateOptions;

    public static bool IsApprovedTemplate(string? template)
        => TryGetDefinition(template, out _);

    public static bool IsApprovedApplicationTemplate(string? template)
        => HasKind(template, WorkspaceDotnetNewTemplateKind.Application);

    public static bool IsApprovedTestTemplate(string? template)
        => HasKind(template, WorkspaceDotnetNewTemplateKind.Test);

    public static bool IsSolutionTemplate(string? template)
        => HasKind(template, WorkspaceDotnetNewTemplateKind.Solution);

    public static bool IsApprovedTemplateOption(string? template, string? option)
        => TryGetDefinition(template, out var definition) &&
           !string.IsNullOrWhiteSpace(option) &&
           definition.SupportedOptions.Contains(option.Trim());

    public static IReadOnlyList<string> GetApprovedTemplateOptions(string? template)
        => TryGetDefinition(template, out var definition)
            ? definition.SupportedOptions
                .OrderBy(option => option, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

    public static bool TryNormalizeTargetFramework(string? targetFramework, out string normalizedTargetFramework)
    {
        normalizedTargetFramework = targetFramework?.Trim() ?? string.Empty;
        return string.IsNullOrEmpty(normalizedTargetFramework) ||
               TargetFrameworkPattern.IsMatch(normalizedTargetFramework);
    }

    private static bool HasKind(string? template, WorkspaceDotnetNewTemplateKind expectedKind)
        => TryGetDefinition(template, out var definition) && definition.Kind == expectedKind;

    private static bool TryGetDefinition(string? template, out TemplateDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(template) ||
            !templatesByName.TryGetValue(template.Trim(), out var candidate) ||
            candidate is null)
        {
            definition = null!;
            return false;
        }

        definition = candidate;
        return true;
    }

    private sealed record TemplateDefinition(
        string Name,
        WorkspaceDotnetNewTemplateKind Kind,
        IReadOnlySet<string> SupportedOptions);

    private enum WorkspaceDotnetNewTemplateKind
    {
        Application,
        Test,
        Solution
    }
}
