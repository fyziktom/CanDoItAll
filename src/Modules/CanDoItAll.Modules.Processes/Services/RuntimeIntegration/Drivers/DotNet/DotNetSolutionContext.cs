namespace CanDoItAll.Modules.Processes;

internal enum DotNetSolutionProvisioningMode
{
    Initialize,
    VerifyExisting
}

internal sealed record DotNetSolutionContext(
    DotNetSolutionProvisioningMode ProvisioningMode,
    string SolutionFile,
    IReadOnlyList<string> SolutionCandidateFiles,
    IReadOnlyList<string> RequiredProjectFiles,
    IReadOnlyList<string> TestProjectFiles,
    DotNetInitializationPlan? Initialization);

internal sealed record DotNetInitializationPlan(
    string SolutionName,
    DotNetInitializationApplication Application,
    DotNetInitializationTestProject TestProject,
    string TargetFramework);

internal sealed record DotNetInitializationApplication(
    string Name,
    string Directory,
    string File,
    string ApplicationTemplate,
    IReadOnlyList<string> ApplicationTemplateOptions,
    string Archetype);

internal sealed record DotNetInitializationTestProject(
    string Name,
    string Directory,
    string File,
    string TestProjectTemplate,
    string FrameworkPreference);
