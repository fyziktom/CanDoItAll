namespace CanDoItAll.Modules.Processes;

internal sealed record DotNetProcessLaunchContract(
    string ProductRoot,
    string SolutionName,
    string SolutionFile,
    string SolutionFileAlias,
    IReadOnlyList<string> SolutionCandidatePaths,
    string AppProjectName,
    string AppProjectDirectory,
    string AppProjectFile,
    string AppProjectFileAlias,
    DotNetApplicationBootstrapShape AppArchetype,
    string TestProjectName,
    string TestProjectDirectory,
    string TestProjectFile,
    string TestProjectFileAlias,
    string TestTemplate,
    string TestFrameworkPreference,
    string TargetFramework,
    string WorkspaceAlias);

internal sealed record DotNetApplicationBootstrapShape(
    string Archetype,
    string Template,
    IReadOnlyList<string> TemplateOptions)
{
    public string TemplateOptionsText => string.Join(" ", TemplateOptions);
}
