using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Processes;

internal sealed class DotNetProcessLaunchContractFactory(
    DotNetSolutionContextPathResolver pathResolver,
    IExternalTargetPathRegistry externalTargetPathRegistry)
{
    public bool TryCreate(
        DotNetSolutionContext context,
        IDictionary<string, string> variables,
        out DotNetProcessLaunchContract contract,
        out string issue)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(variables);

        contract = null!;
        if (context.ProvisioningMode != DotNetSolutionProvisioningMode.Initialize ||
            context.Initialization is not { } initialization)
        {
            issue = "The .NET initialization contract requires provisioningMode 'initialize' with an initialization plan.";
            return false;
        }

        if (!pathResolver.TryResolve(context, variables, out var resolved, out issue) ||
            !pathResolver.TryResolveRelativePath(
                resolved.ProductRoot,
                initialization.Application.Directory,
                "initialization.application.directory",
                out var appDirectory,
                out issue) ||
            !pathResolver.TryResolveRelativePath(
                resolved.ProductRoot,
                initialization.Application.File,
                "initialization.application.file",
                out var appProjectFile,
                out issue) ||
            !pathResolver.TryResolveRelativePath(
                resolved.ProductRoot,
                initialization.TestProject.Directory,
                "initialization.tests.directory",
                out var testDirectory,
                out issue) ||
            !pathResolver.TryResolveRelativePath(
                resolved.ProductRoot,
                initialization.TestProject.File,
                "initialization.tests.file",
                out var testProjectFile,
                out issue) ||
            !TryValidateInitialization(
                initialization,
                resolved,
                appDirectory,
                appProjectFile,
                testDirectory,
                testProjectFile,
                out var applicationTemplate,
                out var testProjectTemplate,
                out var templateOptions,
                out var targetFramework,
                out issue))
        {
            return false;
        }

        contract = new DotNetProcessLaunchContract(
            resolved.ProductRoot,
            initialization.SolutionName,
            resolved.SolutionFile,
            resolved.SolutionFileAlias,
            resolved.SolutionCandidatePaths,
            initialization.Application.Name,
            appDirectory,
            appProjectFile,
            Alias(appProjectFile),
            new DotNetApplicationBootstrapShape(
                string.IsNullOrWhiteSpace(initialization.Application.Archetype)
                    ? applicationTemplate
                    : initialization.Application.Archetype,
                applicationTemplate,
                templateOptions),
            initialization.TestProject.Name,
            testDirectory,
            testProjectFile,
            Alias(testProjectFile),
            testProjectTemplate,
            initialization.TestProject.FrameworkPreference,
            targetFramework,
            resolved.WorkspaceAlias);
        issue = string.Empty;
        return true;
    }

    private static bool TryValidateInitialization(
        DotNetInitializationPlan initialization,
        DotNetResolvedSolutionContext resolved,
        string appDirectory,
        string appProjectFile,
        string testDirectory,
        string testProjectFile,
        out string applicationTemplate,
        out string testProjectTemplate,
        out IReadOnlyList<string> templateOptions,
        out string targetFramework,
        out string issue)
    {
        applicationTemplate = string.Empty;
        testProjectTemplate = string.Empty;
        templateOptions = [];
        targetFramework = string.Empty;
        if (!TryGetRequiredValue(initialization.SolutionName, "initialization.solutionName", out var solutionName, out issue) ||
            !TryGetRequiredValue(initialization.Application.Name, "initialization.application.name", out var applicationName, out issue) ||
            !TryValidateApplicationTemplateIdentifier(initialization.Application.ApplicationTemplate, out applicationTemplate, out issue) ||
            !TryGetRequiredValue(initialization.TestProject.Name, "initialization.tests.name", out var testProjectName, out issue) ||
            !TryValidateTestTemplateIdentifier(initialization.TestProject.TestProjectTemplate, out testProjectTemplate, out issue) ||
            !TryGetRequiredValue(initialization.TestProject.FrameworkPreference, "initialization.tests.frameworkPreference", out _, out issue) ||
            !TryValidateTargetFramework(initialization.TargetFramework, out targetFramework, out issue) ||
            !TryValidateTemplateOptions(applicationTemplate, initialization.Application.ApplicationTemplateOptions, out templateOptions, out issue))
        {
            return false;
        }

        if (!resolved.RequiredProjectFiles.Contains(appProjectFile, StringComparer.OrdinalIgnoreCase) ||
            !resolved.RequiredProjectFiles.Contains(testProjectFile, StringComparer.OrdinalIgnoreCase))
        {
            issue = "An initialize .NET solution context must declare its application and test project files in requiredProjectFiles.";
            return false;
        }

        if (!IsWithinDirectory(appProjectFile, appDirectory) ||
            !IsWithinDirectory(testProjectFile, testDirectory))
        {
            issue = "The .NET initialization plan project files must remain inside their declared directories.";
            return false;
        }

        if (!HasExactFileName(resolved.SolutionFile, solutionName, ".sln", ".slnx") ||
            resolved.SolutionCandidatePaths.Any(candidate => !HasExactFileName(candidate, solutionName, ".sln", ".slnx")) ||
            !HasExactProjectFile(appProjectFile, appDirectory, applicationName) ||
            !string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(appDirectory)), applicationName, StringComparison.OrdinalIgnoreCase) ||
            !HasExactProjectFile(testProjectFile, testDirectory, testProjectName))
        {
            issue = "The initialization plan does not describe a topology that the runtime-owned .NET initializer can create safely.";
            return false;
        }

        issue = string.Empty;
        return true;
    }

    private static bool TryValidateTemplateOptions(
        string applicationTemplate,
        IReadOnlyList<string> options,
        out IReadOnlyList<string> normalized,
        out string issue)
    {
        normalized = options
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim())
            .ToArray();
        if (normalized.Any(option => !option.StartsWith("--", StringComparison.Ordinal)))
        {
            issue = "The .NET initialization plan application.templateOptions must contain option tokens beginning with '--'.";
            return false;
        }

        var unapprovedOption = normalized.FirstOrDefault(
            option => !WorkspaceDotnetNewTemplateCatalog.IsApprovedTemplateOption(applicationTemplate, option));
        if (unapprovedOption is not null)
        {
            var supportedOptions = WorkspaceDotnetNewTemplateCatalog.GetApprovedTemplateOptions(applicationTemplate);
            issue = supportedOptions.Count == 0
                ? $"The .NET initialization plan application.templateOptions must be empty because template '{applicationTemplate}' does not support approved options."
                : $"The .NET initialization plan application.templateOptions contains unsupported option '{unapprovedOption}' for template '{applicationTemplate}'. Allowed options: {string.Join(", ", supportedOptions)}.";
            return false;
        }

        issue = string.Empty;
        return true;
    }

    private static bool TryValidateApplicationTemplateIdentifier(
        string value,
        out string normalized,
        out string issue)
        => TryValidateTemplateIdentifier(
            value,
            "initialization.application.template",
            WorkspaceDotnetNewTemplateCatalog.IsApprovedApplicationTemplate,
            WorkspaceDotnetNewTemplateCatalog.ApprovedApplicationTemplates,
            out normalized,
            out issue);

    private static bool TryValidateTestTemplateIdentifier(
        string value,
        out string normalized,
        out string issue)
        => TryValidateTemplateIdentifier(
            value,
            "initialization.tests.template",
            WorkspaceDotnetNewTemplateCatalog.IsApprovedTestTemplate,
            WorkspaceDotnetNewTemplateCatalog.ApprovedTestTemplates,
            out normalized,
            out issue);

    private static bool TryValidateTemplateIdentifier(
        string value,
        string fieldName,
        Func<string?, bool> isApproved,
        IReadOnlyList<string> approvedTemplates,
        out string normalized,
        out string issue)
    {
        if (!TryGetRequiredValue(value, fieldName, out normalized, out issue))
        {
            return false;
        }

        if (normalized.Split([' ', '\t', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Length != 1 ||
            !isApproved(normalized))
        {
            issue = $"The .NET initialization plan '{fieldName}' must be one approved dotnet new template identifier, not a display name or an inline option. Allowed identifiers: {string.Join(", ", approvedTemplates)}.";
            return false;
        }

        return true;
    }

    private static bool TryValidateTargetFramework(
        string value,
        out string normalized,
        out string issue)
    {
        if (!TryGetRequiredValue(value, "initialization.targetFramework", out normalized, out issue))
        {
            return false;
        }

        if (!WorkspaceDotnetNewTemplateCatalog.TryNormalizeTargetFramework(normalized, out var normalizedTargetFramework))
        {
            issue = "The .NET initialization plan 'initialization.targetFramework' must be a supported target-framework value such as 'net8.0'.";
            return false;
        }

        normalized = normalizedTargetFramework;
        issue = string.Empty;
        return true;
    }

    private static bool TryGetRequiredValue(
        string value,
        string fieldName,
        out string normalized,
        out string issue)
    {
        normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            issue = $"The .NET initialization plan requires non-empty '{fieldName}'.";
            return false;
        }

        issue = string.Empty;
        return true;
    }

    private static bool IsWithinDirectory(string filePath, string directory)
        => filePath.StartsWith(EnsureTrailingSeparator(directory), StringComparison.OrdinalIgnoreCase);

    private static bool HasExactFileName(string path, string expectedName, params string[] allowedExtensions)
        => allowedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase) &&
           string.Equals(Path.GetFileNameWithoutExtension(path), expectedName, StringComparison.OrdinalIgnoreCase);

    private static bool HasExactProjectFile(string file, string directory, string expectedName)
        => string.Equals(Path.GetExtension(file), ".csproj", StringComparison.OrdinalIgnoreCase) &&
           string.Equals(Path.GetDirectoryName(file), directory, StringComparison.OrdinalIgnoreCase) &&
           string.Equals(Path.GetFileNameWithoutExtension(file), expectedName, StringComparison.OrdinalIgnoreCase);

    private static string EnsureTrailingSeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private string Alias(string path)
        => AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
            path,
            externalTargetPathRegistry) ?? string.Empty;
}
