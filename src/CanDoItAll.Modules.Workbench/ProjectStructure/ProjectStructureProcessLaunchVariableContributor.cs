using System.Text;
using System.Text.RegularExpressions;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

public interface IProjectStructureProcessLaunchVariableContributor
{
    void Enrich(ProjectStructureProcessLaunchVariableContext context, IDictionary<string, string> variables);
}

public sealed record ProjectStructureProcessLaunchVariableContext(
    Guid ProjectId,
    ProjectStructureSurface Surface,
    ProjectStructureNode ProjectNode,
    string? DefinitionKey,
    Guid? ProcessDefinitionId,
    ProcessRunId? ParentRunId,
    ProcessStepInstanceId? ParentStepId,
    ProcessRuntimeStepAssignment? ParentAssignment,
    bool IsSubprocess);

internal sealed partial class DotNetProcessLaunchVariableContributor : IProjectStructureProcessLaunchVariableContributor
{
    private const string DefaultTargetFramework = "net10.0";
    private const string DefaultTestTemplate = "xunit";
    private const string DefaultTestFramework = "xUnit";
    private static readonly HashSet<string> SupportedDefinitionKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "dotnet-architecture-design-review",
        "dotnet-development-slice",
        "dotnet-feature-function-implementation",
        "dotnet-solution-setup"
    };

    public void Enrich(ProjectStructureProcessLaunchVariableContext context, IDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(variables);

        if (!context.IsSubprocess ||
            string.IsNullOrWhiteSpace(context.DefinitionKey) ||
            !SupportedDefinitionKeys.Contains(context.DefinitionKey))
        {
            return;
        }

        if (!TryResolveProductRoot(variables, out var productRoot))
        {
            return;
        }

        var contextText = BuildContextText(context, variables);
        var solutionName = ResolveProjectIdentifier(context, variables);
        var appArchetype = ResolveAppArchetype(contextText);
        if (appArchetype is null)
        {
            return;
        }

        var appProjectName = ResolveVariable(variables, "DotNetAppProjectName");
        if (string.IsNullOrWhiteSpace(appProjectName))
        {
            appProjectName = solutionName;
        }

        var testProjectName = ResolveVariable(variables, "DotNetTestProjectName");
        if (string.IsNullOrWhiteSpace(testProjectName))
        {
            testProjectName = $"{appProjectName}.Tests";
        }

        var appProjectDirectory = CombinePath(productRoot, "src", appProjectName);
        var testProjectDirectory = CombinePath(productRoot, "tests", testProjectName);
        var targetFramework = ResolveTargetFramework(contextText);
        var solutionCandidates = string.Join(
            "; ",
            CombinePath(productRoot, $"{solutionName}.slnx"),
            CombinePath(productRoot, $"{solutionName}.sln"));

        AddIfMissing(variables, "DotNetSolutionName", solutionName);
        AddIfMissing(variables, "DotNetSolutionFileCandidates", solutionCandidates);
        AddIfMissing(variables, "DotNetAppProjectName", appProjectName);
        AddIfMissing(variables, "DotNetAppProjectDirectory", appProjectDirectory);
        AddIfMissing(variables, "DotNetAppArchetype", appArchetype.Archetype);
        AddIfMissing(variables, "DotNetAppTemplate", appArchetype.Template);
        AddIfMissing(variables, "DotNetAppTemplateOptions", appArchetype.TemplateOptions);
        AddIfMissing(variables, "DotNetAllowedTemplateSwitches", appArchetype.AllowedTemplateSwitches);
        AddIfMissing(variables, "DotNetTestProjectName", testProjectName);
        AddIfMissing(variables, "DotNetTestProjectDirectory", testProjectDirectory);
        AddIfMissing(variables, "DotNetTestTemplate", DefaultTestTemplate);
        AddIfMissing(variables, "DotNetTestFrameworkPreference", DefaultTestFramework);
        AddIfMissing(variables, "DotNetTargetFramework", targetFramework);
        AddIfMissing(
            variables,
            "DotNetScaffoldContractSource",
            "Inferred from current project-structure .NET target, product root, and CanDoItAll repository test-framework convention.");
        AddIfMissing(
            variables,
            "DotNetScaffoldContract",
            BuildContract(
                solutionName,
                solutionCandidates,
                appProjectName,
                appProjectDirectory,
                appArchetype,
                testProjectName,
                testProjectDirectory,
                targetFramework,
                productRoot));
    }

    private static bool TryResolveProductRoot(IDictionary<string, string> variables, out string productRoot)
    {
        productRoot = FirstNonEmpty(
            ResolveVariable(variables, "ProductRoot"),
            ResolveVariable(variables, "OutputRoot"),
            ResolveVariable(variables, "ExternalTargetRoot"));

        return !string.IsNullOrWhiteSpace(productRoot);
    }

    private static string ResolveProjectIdentifier(
        ProjectStructureProcessLaunchVariableContext context,
        IDictionary<string, string> variables)
    {
        var candidates = new[]
        {
            ResolveVariable(variables, "DotNetSolutionName"),
            ResolveVariable(variables, "ProjectName"),
            context.Surface.ProjectName,
            context.ProjectNode.Title,
            ResolveProductRootLeaf(ResolveVariable(variables, "ProductRoot")),
            ResolveProductRootLeaf(ResolveVariable(variables, "OutputRoot"))
        };

        foreach (var candidate in candidates)
        {
            var identifier = ToIdentifier(candidate);
            if (!string.IsNullOrWhiteSpace(identifier))
            {
                return identifier;
            }
        }

        return "GeneratedApp";
    }

    private static DotNetScaffoldArchetype? ResolveAppArchetype(string contextText)
    {
        if (ContainsAny(contextText, "Blazor WebAssembly", "Blazor WASM"))
        {
            var isPwa = ContainsAny(contextText, "PWA", "Progressive Web App", "offline-friendly", "static-host");
            return new DotNetScaffoldArchetype(
                isPwa ? "Blazor WebAssembly PWA" : "Blazor WebAssembly",
                "blazorwasm",
                isPwa ? "--pwa" : string.Empty,
                isPwa ? "--pwa" : string.Empty);
        }

        if (ContainsAny(contextText, "Blazor SSR", "Blazor Server", "Blazor Web App"))
        {
            return new DotNetScaffoldArchetype("Blazor Web App", "blazor", string.Empty, string.Empty);
        }

        if (ContainsAny(contextText, "web api", "webapi", "http api", "backend api"))
        {
            return new DotNetScaffoldArchetype("ASP.NET Core Web API", "webapi", string.Empty, string.Empty);
        }

        if (ContainsAny(contextText, "worker service", "background worker"))
        {
            return new DotNetScaffoldArchetype(".NET worker service", "worker", string.Empty, string.Empty);
        }

        if (ContainsAny(contextText, "console app", "command-line", "cli"))
        {
            return new DotNetScaffoldArchetype(".NET console app", "console", string.Empty, string.Empty);
        }

        if (ContainsAny(contextText, "class library", "library"))
        {
            return new DotNetScaffoldArchetype(".NET class library", "classlib", string.Empty, string.Empty);
        }

        return null;
    }

    private static string ResolveTargetFramework(string contextText)
    {
        var match = TargetFrameworkRegex().Match(contextText);
        return match.Success
            ? match.Value.ToLowerInvariant()
            : DefaultTargetFramework;
    }

    private static string BuildContextText(
        ProjectStructureProcessLaunchVariableContext context,
        IDictionary<string, string> variables)
    {
        var builder = new StringBuilder();
        AppendLine(builder, ResolveVariable(variables, "ProjectStructureContextSummary"));
        AppendLine(builder, context.Surface.ProjectName);
        AppendLine(builder, context.ProjectNode.Title);
        AppendLine(builder, context.ProjectNode.Subtitle);
        AppendLine(builder, context.ProjectNode.Notes);

        foreach (var node in context.Surface.Nodes)
        {
            AppendLine(builder, node.Title);
            AppendLine(builder, node.Subtitle);
            AppendLine(builder, node.Notes);
            AppendLine(builder, node.ObjectSubtype);
        }

        return builder.ToString();
    }

    private static string BuildContract(
        string solutionName,
        string solutionCandidates,
        string appProjectName,
        string appProjectDirectory,
        DotNetScaffoldArchetype appArchetype,
        string testProjectName,
        string testProjectDirectory,
        string targetFramework,
        string productRoot)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"SolutionName: {solutionName}");
        builder.AppendLine($"SolutionFileCandidates: {solutionCandidates}");
        builder.AppendLine($"AppProjectName: {appProjectName}");
        builder.AppendLine($"AppProjectDirectory: {appProjectDirectory}");
        builder.AppendLine($"AppArchetype: {appArchetype.Archetype}");
        builder.AppendLine($"AppTemplate: {appArchetype.Template}");
        builder.AppendLine($"AppTemplateOptions: {appArchetype.TemplateOptions}");
        builder.AppendLine($"AllowedTemplateSwitches: {appArchetype.AllowedTemplateSwitches}");
        builder.AppendLine($"TestProjectName: {testProjectName}");
        builder.AppendLine($"TestProjectDirectory: {testProjectDirectory}");
        builder.AppendLine($"TestTemplate: {DefaultTestTemplate}");
        builder.AppendLine($"TestFrameworkPreference: {DefaultTestFramework}");
        builder.AppendLine($"TargetFramework: {targetFramework}");
        builder.AppendLine($"ProductRoot: {productRoot}");
        builder.Append("Layout: solution file at ProductRoot, app under ProductRoot/src, tests under ProductRoot/tests.");
        return builder.ToString();
    }

    private static void AddIfMissing(IDictionary<string, string> variables, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!variables.TryGetValue(key, out var existing) || string.IsNullOrWhiteSpace(existing))
        {
            variables[key] = value;
        }
    }

    private static string ResolveVariable(IDictionary<string, string> variables, string key)
        => variables.TryGetValue(key, out var value)
            ? value?.Trim() ?? string.Empty
            : string.Empty;

    private static string ResolveProductRootLeaf(string productRoot)
    {
        if (string.IsNullOrWhiteSpace(productRoot))
        {
            return string.Empty;
        }

        var normalized = productRoot.Trim().TrimEnd('\\', '/');
        var slashIndex = normalized.LastIndexOfAny(['\\', '/']);
        return slashIndex < 0
            ? normalized
            : normalized[(slashIndex + 1)..];
    }

    private static string ToIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var parts = IdentifierPartRegex()
            .Matches(value)
            .Select(match => match.Value)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        var identifier = string.Concat(parts.Select(ToPascalPart));
        return char.IsLetter(identifier[0])
            ? identifier
            : $"App{identifier}";
    }

    private static string ToPascalPart(string value)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static string CombinePath(string root, params string[] segments)
    {
        var separator = root.Contains('/') && !root.Contains('\\')
            ? "/"
            : "\\";
        var builder = new StringBuilder(root.TrimEnd('\\', '/'));
        foreach (var segment in segments.Where(segment => !string.IsNullOrWhiteSpace(segment)))
        {
            builder.Append(separator);
            builder.Append(segment.Trim('\\', '/'));
        }

        return builder.ToString();
    }

    private static bool ContainsAny(string text, params string[] values)
        => values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static void AppendLine(StringBuilder builder, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.AppendLine(value);
        }
    }

    private sealed record DotNetScaffoldArchetype(
        string Archetype,
        string Template,
        string TemplateOptions,
        string AllowedTemplateSwitches);

    [GeneratedRegex(@"\bnet\d+(?:\.\d+)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex TargetFrameworkRegex();

    [GeneratedRegex(@"[A-Za-z0-9]+")]
    private static partial Regex IdentifierPartRegex();
}
