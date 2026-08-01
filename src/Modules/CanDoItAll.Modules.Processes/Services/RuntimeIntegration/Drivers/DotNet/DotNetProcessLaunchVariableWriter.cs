using System.Text;
using System.Text.Json;
namespace CanDoItAll.Modules.Processes;

internal static class DotNetProcessLaunchVariableWriter
{
    public static void ApplyCore(
        DotNetProcessLaunchContract contract,
        IDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(variables);

        SetAuthoritative(variables, "DotNetProvisioningMode", "initialize");
        SetAuthoritative(variables, "DotNetSolutionName", contract.SolutionName);
        SetAuthoritative(variables, "DotNetSolutionFile", contract.SolutionFile);
        SetAuthoritative(variables, "DotNetSolutionFileAlias", contract.SolutionFileAlias);
        SetAuthoritative(variables, "DotNetSolutionFileCandidates", string.Join("; ", contract.SolutionCandidatePaths));
        SetAuthoritative(variables, "DotNetAppProjectName", contract.AppProjectName);
        SetAuthoritative(variables, "DotNetAppProjectDirectory", contract.AppProjectDirectory);
        SetAuthoritative(variables, "DotNetAppProjectFile", contract.AppProjectFile);
        SetAuthoritative(variables, "DotNetAppProjectFileAlias", contract.AppProjectFileAlias);
        SetAuthoritative(variables, "DotNetAppArchetype", contract.AppArchetype.Archetype);
        SetAuthoritative(variables, "DotNetAppTemplate", contract.AppArchetype.Template);
        SetAuthoritative(variables, "DotNetAppTemplateOptions", contract.AppArchetype.TemplateOptionsText);
        SetAuthoritative(variables, "DotNetAllowedTemplateSwitches", contract.AppArchetype.TemplateOptionsText);
        SetAuthoritative(variables, "DotNetTestProjectName", contract.TestProjectName);
        SetAuthoritative(variables, "DotNetTestProjectDirectory", contract.TestProjectDirectory);
        SetAuthoritative(variables, "DotNetTestProjectFile", contract.TestProjectFile);
        SetAuthoritative(variables, "DotNetTestProjectFileAlias", contract.TestProjectFileAlias);
        SetAuthoritative(variables, "DotNetTestTemplate", contract.TestTemplate);
        SetAuthoritative(variables, "DotNetTestFrameworkPreference", contract.TestFrameworkPreference);
        SetAuthoritative(variables, "DotNetTargetFramework", contract.TargetFramework);
        SetAuthoritative(variables, "DotNetWorkspaceAlias", contract.WorkspaceAlias);
        SetForwardSlashPath(variables, "DotNetSolutionFileForwardSlash", contract.SolutionFile);
        SetForwardSlashPath(variables, "DotNetAppProjectFileForwardSlash", contract.AppProjectFile);
        SetForwardSlashPath(variables, "DotNetTestProjectFileForwardSlash", contract.TestProjectFile);
        SetReadbackPathAlternatives(
            variables,
            "DotNetAppProjectSolutionRelativePath",
            "DotNetAppProjectSolutionRelativePathWindows",
            contract.ProductRoot,
            contract.AppProjectFile);
        SetReadbackPathAlternatives(
            variables,
            "DotNetTestProjectSolutionRelativePath",
            "DotNetTestProjectSolutionRelativePathWindows",
            contract.ProductRoot,
            contract.TestProjectFile);
        SetReadbackPathAlternatives(
            variables,
            "DotNetAppProjectReferenceRelativePath",
            "DotNetAppProjectReferenceRelativePathWindows",
            contract.TestProjectDirectory,
            contract.AppProjectFile);
    }

    public static void ApplyExistingSolution(
        DotNetExistingSolutionVerificationContract contract,
        IDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(variables);

        SetAuthoritative(variables, "DotNetProvisioningMode", "verify-existing");
        SetAuthoritative(variables, "DotNetSolutionFile", contract.SolutionFile);
        SetAuthoritative(variables, "DotNetSolutionFileAlias", contract.SolutionFileAlias);
        SetAuthoritative(variables, "DotNetSolutionFileCandidates", string.Join("; ", contract.SolutionCandidatePaths));
        SetAuthoritative(variables, "DotNetRequiredProjectFiles", JsonSerializer.Serialize(contract.RequiredProjectFiles));
        SetAuthoritative(variables, "DotNetTestProjectFiles", JsonSerializer.Serialize(contract.TestProjectFiles));
        SetAuthoritative(variables, "DotNetWorkspaceAlias", contract.WorkspaceAlias);
    }

    public static void SetIfNotEmpty(
        IDictionary<string, string> variables,
        string key,
        string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            variables[key] = value;
        }
    }

    public static void SetAuthoritative(
        IDictionary<string, string> variables,
        string key,
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (variables.TryGetValue(key, out var existing) &&
            !string.IsNullOrWhiteSpace(existing) &&
            !string.Equals(existing.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Authoritative .NET bootstrap value '{key}' conflicts with an existing launch variable value.");
        }

        variables[key] = value;
    }

    private static void SetReadbackPathAlternatives(
        IDictionary<string, string> variables,
        string forwardSlashKey,
        string backslashKey,
        string basePath,
        string path)
    {
        var relativePath = Path.GetRelativePath(basePath, path);
        SetAuthoritative(
            variables,
            forwardSlashKey,
            relativePath.Replace('\\', '/'));
        SetAuthoritative(
            variables,
            backslashKey,
            relativePath.Replace('/', '\\'));
    }

    private static void SetForwardSlashPath(
        IDictionary<string, string> variables,
        string key,
        string path)
        => SetAuthoritative(
            variables,
            key,
            Path.GetFullPath(path).Replace('\\', '/'));
}
