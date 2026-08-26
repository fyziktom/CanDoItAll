namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructurePageArchitectureTests
{
    private static readonly string[] FormerLaunchContextPolicyMembers =
    [
        "BuildProjectStructureContextSummary(",
        "AppendVisualTargetAssetSummary(",
        "IsVisualTargetAsset(",
        "ContainsVisualTargetKeyword(",
        "EnumerateProjectStructureContextNodes(",
        "ResolveOutputRoot(",
        "ApplyProductRootLaunchVariables(",
        "TryReadOutputRootFromMetadata(",
        "TryReadOutputRootFromElement(",
        "NormalizeProcessContextText(",
        "RedactNonCitableProcessContextPaths(",
        "NormalizeContextText(",
        "RedactNonCitableContextPaths(",
        "OutputRootMetadataKeys"
    ];

    private static readonly string[] FormerHierarchyPolicyMembers =
    [
        "private static bool CanAttachProjectAsSubproject(",
        "private static bool CanReconnectProjectToParent(",
        "private static HashSet<Guid> ExpandReachableProjectIds("
    ];

    [Fact]
    public void Process_launch_callers_delegate_to_the_shared_context_builder_and_result()
    {
        var root = FindRepositoryRoot();
        var pageSource = ReadSource(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Workbench",
            "Pages",
            "ProjectStructurePage.Processes.cs");
        var serviceSource = ReadSource(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Workbench",
            "ProjectStructure",
            "ProjectStructureProcessNodeService.cs");

        Assert.Contains(
            "ProjectStructureProcessLaunchContextBuilder.Build(surface, targetNode)",
            pageSource,
            StringComparison.Ordinal);
        Assert.Contains("launchContext.ApplyContextSummaryTo(variables)", pageSource, StringComparison.Ordinal);
        Assert.Contains("launchContext.ApplyOutputRootAliasesTo(variables)", pageSource, StringComparison.Ordinal);

        Assert.Contains(
            "ProjectStructureProcessLaunchContextBuilder.Build(surface, targetNode)",
            serviceSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "ProjectStructureProcessLaunchContextBuilder.Build(surface, projectNode)",
            serviceSource,
            StringComparison.Ordinal);
        Assert.Contains("launchContext.ApplyContextSummaryTo(variables)", serviceSource, StringComparison.Ordinal);
        Assert.Contains(
            "launchContext.ApplyContextSummaryTo(variables, removeWhenEmpty: true)",
            serviceSource,
            StringComparison.Ordinal);
        Assert.Contains("launchContext.ApplyOutputRootAliasesTo(variables)", serviceSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Former_launch_context_policy_members_are_absent_from_both_callers()
    {
        var root = FindRepositoryRoot();
        var callerSources = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ProjectStructurePage.Processes.cs"] = ReadSource(
                root,
                "src",
                "Modules",
                "CanDoItAll.Modules.Workbench",
                "Pages",
                "ProjectStructurePage.Processes.cs"),
            ["ProjectStructureProcessNodeService.cs"] = ReadSource(
                root,
                "src",
                "Modules",
                "CanDoItAll.Modules.Workbench",
                "ProjectStructure",
                "ProjectStructureProcessNodeService.cs")
        };

        var findings = callerSources
            .SelectMany(caller => FormerLaunchContextPolicyMembers
                .Where(member => caller.Value.Contains(member, StringComparison.Ordinal))
                .Select(member => $"{caller.Key}: {member}"))
            .ToArray();

        Assert.True(
            findings.Length == 0,
            "Launch-context policy must live only in the shared builder: " + string.Join(", ", findings));
    }

    [Fact]
    public void Launch_context_boundary_is_top_level_internal_and_adds_no_interface_or_partial()
    {
        var root = FindRepositoryRoot();
        var source = ReadSource(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Workbench",
            "ProjectStructure",
            "ProjectStructureProcessLaunchContextBuilder.cs");

        Assert.Contains(
            "internal static class ProjectStructureProcessLaunchContextBuilder",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "internal sealed record ProjectStructureProcessLaunchContext(",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(" interface ", source, StringComparison.Ordinal);
        Assert.DoesNotContain(" partial ", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectStructurePage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IServiceProvider", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_hierarchy_page_delegates_to_the_shared_selection_policy()
    {
        var root = FindRepositoryRoot();
        var source = ReadSource(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Workbench",
            "Pages",
            "ProjectStructurePage.ProjectHierarchy.cs");

        Assert.Contains(
            "ProjectStructureProjectHierarchySelectionPolicy.CanAttachProjectAsSubproject(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ProjectStructureProjectHierarchySelectionPolicy.CanReconnectProjectToParent(",
            source,
            StringComparison.Ordinal);

        var findings = FormerHierarchyPolicyMembers
            .Where(member => source.Contains(member, StringComparison.Ordinal))
            .ToArray();
        Assert.True(
            findings.Length == 0,
            "Hierarchy selection policy must live only in the shared policy: " +
            string.Join(", ", findings));
    }

    [Fact]
    public void Project_hierarchy_boundary_is_top_level_internal_and_has_no_page_dependency()
    {
        var root = FindRepositoryRoot();
        var source = ReadSource(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Workbench",
            "ProjectStructure",
            "ProjectStructureProjectHierarchySelectionPolicy.cs");

        Assert.Contains(
            "internal static class ProjectStructureProjectHierarchySelectionPolicy",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(" interface ", source, StringComparison.Ordinal);
        Assert.DoesNotContain(" partial ", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectStructurePage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectStructureProjectHierarchyDialogMode", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IServiceProvider", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_structure_page_partial_count_does_not_increase()
    {
        var root = FindRepositoryRoot();
        var pagesDirectory = Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Workbench",
            "Pages");
        var explicitPartialCount = Directory
            .EnumerateFiles(
                pagesDirectory,
                "ProjectStructurePage*.cs",
                SearchOption.TopDirectoryOnly)
            .Count(path => File.ReadAllText(path).Contains(
                "partial class ProjectStructurePage",
                StringComparison.Ordinal));

        Assert.Equal(22, explicitPartialCount);
    }

    [Fact]
    public void Provider_prompt_execution_uses_the_AgentFramework_runtime_port()
    {
        var root = FindRepositoryRoot();
        var pageSource = ReadSource(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Workbench",
            "Pages",
            "ProjectStructurePage.razor");
        var workflowSource = ReadSource(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.Workbench",
            "Pages",
            "ProjectStructurePage.Workflows.cs");
        var combinedSource = pageSource + Environment.NewLine + workflowSource;

        Assert.Contains(
            "@inject IProviderPromptExecutionService ProviderPromptExecutionService",
            pageSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "ProviderPromptExecutionService.ExecuteAsync(",
            workflowSource,
            StringComparison.Ordinal);
        Assert.Contains("new ProviderPromptExecutionRequest(", workflowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProviderExecutionService", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProviderExecutionRequest", combinedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.Workspace.Providers", combinedSource, StringComparison.Ordinal);
    }

    private static string ReadSource(string root, params string[] segments)
        => File.ReadAllText(Path.Combine([root, .. segments]));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
