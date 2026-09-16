using System.Reflection;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Tests.Unit.Architecture;

namespace CanDoItAll.Tests.Unit.Projects;

// These checks read compiled symbols and IL instead of source text, so formatting, argument spelling, partial-file
// layout or interface declarations elsewhere in a file cannot make them pass or fail. The former source assertion that
// the page keeps captured action admission and restores saved launch preparations is covered behaviorally by
// ProjectStructurePageActionLifetimeTests and ProjectStructurePageProcessLaunchScopeTests (Components).
public sealed class ProjectStructurePageArchitectureTests
{
    private const BindingFlags DeclaredMembers = BindingFlags.Public | BindingFlags.NonPublic |
                                                 BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly string[] FormerLaunchContextPolicyMembers =
    [
        "BuildProjectStructureContextSummary",
        "AppendVisualTargetAssetSummary",
        "IsVisualTargetAsset",
        "ContainsVisualTargetKeyword",
        "EnumerateProjectStructureContextNodes",
        "ResolveOutputRoot",
        "ApplyProductRootLaunchVariables",
        "TryReadOutputRootFromMetadata",
        "TryReadOutputRootFromElement",
        "NormalizeProcessContextText",
        "RedactNonCitableProcessContextPaths",
        "NormalizeContextText",
        "RedactNonCitableContextPaths",
        "OutputRootMetadataKeys"
    ];

    private static readonly string[] FormerHierarchyPolicyMembers =
    [
        "CanAttachProjectAsSubproject",
        "CanReconnectProjectToParent",
        "ExpandReachableProjectIds"
    ];

    [Theory]
    [InlineData(typeof(ProjectStructurePage))]
    [InlineData(typeof(ProjectStructureProcessNodeService))]
    public void Process_launch_callers_delegate_to_the_shared_context_builder_and_result(Type caller)
    {
        var called = CompiledTypeReferences.CalledMethods(caller);

        Assert.Contains(called, method =>
            method.DeclaringType == typeof(ProjectStructureProcessLaunchContextBuilder) &&
            method.Name == nameof(ProjectStructureProcessLaunchContextBuilder.Build));
        Assert.Contains(called, method =>
            method.DeclaringType == typeof(ProjectStructureProcessLaunchContext) &&
            method.Name == nameof(ProjectStructureProcessLaunchContext.ApplyContextSummaryTo));
        Assert.Contains(called, method =>
            method.DeclaringType == typeof(ProjectStructureProcessLaunchContext) &&
            method.Name == nameof(ProjectStructureProcessLaunchContext.ApplyOutputRootAliasesTo));
    }

    [Theory]
    [InlineData(typeof(ProjectStructurePage))]
    [InlineData(typeof(ProjectStructureProcessNodeService))]
    public void Former_launch_context_policy_members_are_absent_from_the_callers(Type caller)
    {
        var findings = DeclaredMemberNames(caller)
            .Where(name => FormerLaunchContextPolicyMembers.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.True(
            findings.Length == 0,
            $"Launch-context policy must live only in the shared builder; {caller.Name} declares: {string.Join(", ", findings)}");
    }

    [Fact]
    public void Launch_context_boundary_is_a_top_level_internal_policy_without_page_or_service_location()
    {
        AssertTopLevelInternalStatic(typeof(ProjectStructureProcessLaunchContextBuilder));
        var context = typeof(ProjectStructureProcessLaunchContext);
        Assert.False(context.IsNested);
        Assert.False(context.IsPublic);
        Assert.True(context.IsSealed);
        Assert.NotNull(context.GetMethod("<Clone>$", DeclaredMembers));

        AssertNoPageUiOrServiceLocatorDependency(typeof(ProjectStructureProcessLaunchContextBuilder), context);
    }

    [Fact]
    public void Project_hierarchy_page_delegates_to_the_shared_selection_policy()
    {
        var called = CompiledTypeReferences.CalledMethods(typeof(ProjectStructurePage));

        Assert.Contains(called, method =>
            method.DeclaringType == typeof(ProjectStructureProjectHierarchySelectionPolicy) &&
            method.Name == nameof(ProjectStructureProjectHierarchySelectionPolicy.CanAttachProjectAsSubproject));
        Assert.Contains(called, method =>
            method.DeclaringType == typeof(ProjectStructureProjectHierarchySelectionPolicy) &&
            method.Name == nameof(ProjectStructureProjectHierarchySelectionPolicy.CanReconnectProjectToParent));
        var redeclared = DeclaredMemberNames(typeof(ProjectStructurePage))
            .Where(name => FormerHierarchyPolicyMembers.Contains(name, StringComparer.Ordinal))
            .ToArray();
        Assert.True(
            redeclared.Length == 0,
            "Hierarchy selection policy must live only in the shared policy: " + string.Join(", ", redeclared));
    }

    [Fact]
    public void Project_hierarchy_boundary_is_a_top_level_internal_policy_without_page_dependency()
    {
        var policy = typeof(ProjectStructureProjectHierarchySelectionPolicy);
        AssertTopLevelInternalStatic(policy);
        AssertNoPageUiOrServiceLocatorDependency(policy);

        Assert.DoesNotContain(
            CompiledTypeReferences.ReferencedTypes(policy),
            type => type.Name == "ProjectStructureProjectHierarchyDialogMode");
    }

    [Fact]
    public void Provider_prompt_execution_uses_the_AgentFramework_runtime_port()
    {
        var page = typeof(ProjectStructurePage);

        Assert.Contains(page.GetProperties(DeclaredMembers), property =>
            property.PropertyType.Name == "IProviderPromptExecutionService" &&
            property.GetCustomAttributes().Any(attribute => attribute.GetType().Name == "InjectAttribute"));
        Assert.Contains(CompiledTypeReferences.CalledMethods(page), method =>
            method.DeclaringType?.Name == "IProviderPromptExecutionService" &&
            method.Name == "ExecuteAsync");
        var referenced = CompiledTypeReferences.ReferencedTypes(page);
        Assert.DoesNotContain(referenced, type =>
            type.Name is "ProviderExecutionService" or "IProviderExecutionService" or "ProviderExecutionRequest");
        Assert.DoesNotContain(referenced, type =>
            type.Namespace?.StartsWith("CanDoItAll.Modules.Workspace.Providers", StringComparison.Ordinal) == true);
    }

    private static IEnumerable<string> DeclaredMemberNames(Type type)
        => CompiledTypeReferences.SelfAndNestedTypes(type)
            .SelectMany(candidate => candidate.GetMembers(DeclaredMembers))
            .Select(member => member.Name)
            .Distinct(StringComparer.Ordinal);

    private static void AssertTopLevelInternalStatic(Type type)
    {
        Assert.False(type.IsNested, $"{type.Name} must be a top-level type.");
        Assert.False(type.IsPublic, $"{type.Name} must stay internal to its owner.");
        Assert.True(type.IsAbstract && type.IsSealed, $"{type.Name} must be a static policy.");
    }

    private static void AssertNoPageUiOrServiceLocatorDependency(params Type[] types)
    {
        var referenced = types.SelectMany(CompiledTypeReferences.ReferencedTypes).ToHashSet();
        Assert.DoesNotContain(typeof(ProjectStructurePage), referenced);
        Assert.DoesNotContain(typeof(IServiceProvider), referenced);
        Assert.DoesNotContain(referenced, type =>
            type.Namespace?.StartsWith("Microsoft.AspNetCore.Components", StringComparison.Ordinal) == true);
    }
}
