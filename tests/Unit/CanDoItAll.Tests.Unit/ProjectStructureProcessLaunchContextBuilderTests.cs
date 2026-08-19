using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureProcessLaunchContextBuilderTests
{
    [Fact]
    public void Build_handles_missing_inputs_and_resolves_a_direct_root_without_a_surface()
    {
        var empty = ProjectStructureProcessLaunchContextBuilder.Build(null, null);
        var focusNode = CreateNode(
            "focus",
            null,
            "Focus",
            metadataJson: CreateProjectBlockMetadata(outputRoot: @" C:\products\direct "));

        var direct = ProjectStructureProcessLaunchContextBuilder.Build(null, focusNode);
        var missingFocus = ProjectStructureProcessLaunchContextBuilder.Build(
            CreateSurface(Guid.NewGuid(), focusNode),
            null);

        Assert.Equal(string.Empty, empty.ContextSummary);
        Assert.Equal(string.Empty, empty.OutputRoot);
        Assert.Equal(string.Empty, direct.ContextSummary);
        Assert.Equal(@"C:\products\direct", direct.OutputRoot);
        Assert.Equal(string.Empty, missingFocus.ContextSummary);
        Assert.Equal(string.Empty, missingFocus.OutputRoot);
    }

    [Fact]
    public void Build_orders_the_hierarchy_marks_the_focus_and_redacts_storage_paths()
    {
        var projectId = Guid.NewGuid();
        var rootNodeId = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId);
        var parent = CreateNode(
            "parent",
            rootNodeId,
            "Parent",
            notes: @"Native C:\private\source and file://private/source plus artifacts/scopes/project/run.json.",
            y: 0);
        var child = CreateNode(
            "child",
            parent.Id,
            "Child",
            notes: @"Managed project-media/images/private.png.",
            y: 0);
        var focus = CreateNode(
            "focus",
            rootNodeId,
            "Focus",
            status: string.Empty,
            y: 10);
        var surface = CreateSurface(projectId, focus, child, parent);

        var result = ProjectStructureProcessLaunchContextBuilder.Build(surface, focus);

        Assert.Contains($"Project structure source: Launch context test ({projectId:D}).", result.ContextSummary, StringComparison.Ordinal);
        Assert.Contains("Selected node: Focus (focus).", result.ContextSummary, StringComparison.Ordinal);
        Assert.Contains("- Focus [selected] [ProjectBlock/requirements; Draft]", result.ContextSummary, StringComparison.Ordinal);
        Assert.True(
            result.ContextSummary.IndexOf("- Parent", StringComparison.Ordinal) <
            result.ContextSummary.IndexOf("-   Child", StringComparison.Ordinal));
        Assert.True(
            result.ContextSummary.IndexOf("-   Child", StringComparison.Ordinal) <
            result.ContextSummary.IndexOf("- Focus", StringComparison.Ordinal));
        Assert.DoesNotContain(@"C:\private\source", result.ContextSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("file://private/source", result.ContextSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("artifacts/scopes/project/run.json", result.ContextSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("project-media/images/private.png", result.ContextSummary, StringComparison.Ordinal);
        Assert.Contains("[storage-path]", result.ContextSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_includes_authored_visual_targets_and_excludes_generated_run_evidence()
    {
        var projectId = Guid.NewGuid();
        var rootNodeId = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId);
        var focus = CreateNode("focus", rootNodeId, "Feature brief");
        var visualTarget = CreateNode(
            "visual-target",
            rootNodeId,
            "Calculator UI proposal",
            objectType: ProjectObjectType.ImageAsset,
            objectSubtype: "generated",
            notes: "Source visual target for implementation and QA.",
            mediaRelativePath: "managed-files/project-media/images/calculator-proposal.png",
            mediaContentType: "image/png",
            mediaOriginalFileName: "calculator-proposal.png",
            y: 1);
        var screenshot = CreateNode(
            "old-screenshot",
            rootNodeId,
            "Earlier run screenshot",
            objectType: ProjectObjectType.ImageAsset,
            objectSubtype: "screenshot",
            artifactKind: "process-run-screenshot",
            mediaRelativePath: "artifacts/scopes/project/process-runs/run/browser/screenshot.png",
            mediaContentType: "image/png",
            mediaOriginalFileName: "screenshot.png",
            y: 2);
        var runEvidence = CreateNode(
            ProjectStructureProcessNodeKeys.BuildProcessRunSummaryNodeKey(Guid.NewGuid()),
            rootNodeId,
            "Earlier execution report",
            objectType: ProjectObjectType.File,
            artifactKind: "execution-report",
            y: 3);
        var surface = CreateSurface(projectId, focus, visualTarget, screenshot, runEvidence);

        var result = ProjectStructureProcessLaunchContextBuilder.Build(surface, focus);

        Assert.Contains("Visual target assets:", result.ContextSummary, StringComparison.Ordinal);
        Assert.Contains("Calculator UI proposal", result.ContextSummary, StringComparison.Ordinal);
        Assert.Contains("Visual target rule:", result.ContextSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("Earlier run screenshot", result.ContextSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("Earlier execution report", result.ContextSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_limits_context_rows_to_forty()
    {
        var projectId = Guid.NewGuid();
        var rootNodeId = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId);
        var nodes = Enumerable.Range(0, 41)
            .Select(index => CreateNode(
                $"row-{index:00}",
                rootNodeId,
                $"Row {index:00}",
                y: index))
            .ToArray();
        var surface = CreateSurface(projectId, nodes);

        var result = ProjectStructureProcessLaunchContextBuilder.Build(surface, nodes[0]);

        var contextRowCount = result.ContextSummary
            .Split(Environment.NewLine)
            .Count(line => line.StartsWith("- ", StringComparison.Ordinal));
        Assert.Equal(40, contextRowCount);
        Assert.Contains("Row 39", result.ContextSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("Row 40", result.ContextSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_limits_visual_target_assets_to_eight()
    {
        var projectId = Guid.NewGuid();
        var rootNodeId = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId);
        var focus = CreateNode("focus", rootNodeId, "Feature brief", y: -1);
        var assets = Enumerable.Range(0, 9)
            .Select(index => CreateNode(
                $"asset-{index:00}",
                rootNodeId,
                $"Visual target {index:00}",
                objectType: ProjectObjectType.ImageAsset,
                objectSubtype: "generated",
                mediaRelativePath: $"managed-files/project-media/images/target-{index:00}.png",
                mediaContentType: "image/png",
                mediaOriginalFileName: $"target-{index:00}.png",
                y: index))
            .ToArray();
        var surface = CreateSurface(projectId, [focus, .. assets]);

        var result = ProjectStructureProcessLaunchContextBuilder.Build(surface, focus);

        var visualSectionStart = result.ContextSummary.IndexOf("Visual target assets:", StringComparison.Ordinal);
        var visualSectionEnd = result.ContextSummary.IndexOf("Visual target rule:", StringComparison.Ordinal);
        var visualSection = result.ContextSummary[visualSectionStart..visualSectionEnd];
        var visualAssetCount = visualSection
            .Split(Environment.NewLine)
            .Count(line => line.StartsWith("- ", StringComparison.Ordinal));
        Assert.Equal(8, visualAssetCount);
        Assert.Contains("Visual target 07", visualSection, StringComparison.Ordinal);
        Assert.DoesNotContain("Visual target 08", visualSection, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_prefers_direct_typed_metadata_over_ancestor_typed_metadata()
    {
        var projectId = Guid.NewGuid();
        var rootNodeId = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId);
        var ancestor = CreateNode(
            "ancestor",
            rootNodeId,
            "Ancestor",
            metadataJson: CreateProjectBlockMetadata(outputRoot: @"C:\products\ancestor"));
        var focus = CreateNode(
            "focus",
            ancestor.Id,
            "Focus",
            metadataJson: CreateProjectBlockMetadata(productRoot: @" C:\products\direct "));
        var surface = CreateSurface(projectId, ancestor, focus);

        var result = ProjectStructureProcessLaunchContextBuilder.Build(surface, focus);

        Assert.Equal(@"C:\products\direct", result.OutputRoot);
    }

    [Fact]
    public void Build_ignores_malformed_direct_metadata_and_falls_back_to_an_ancestor()
    {
        var projectId = Guid.NewGuid();
        var rootNodeId = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId);
        var ancestor = CreateNode(
            "ancestor",
            rootNodeId,
            "Ancestor",
            metadataJson: CreateProjectBlockMetadata(workspaceRoot: @" C:\products\ancestor "));
        var focus = CreateNode(
            "focus",
            ancestor.Id,
            "Focus",
            metadataJson: "{");
        var surface = CreateSurface(projectId, ancestor, focus);

        var result = ProjectStructureProcessLaunchContextBuilder.Build(surface, focus);

        Assert.Equal(@"C:\products\ancestor", result.OutputRoot);
    }

    [Fact]
    public void Build_resolves_output_root_only_from_the_selected_ancestor_branch()
    {
        var projectId = Guid.NewGuid();
        var rootNodeId = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId);
        var unrelatedBranch = CreateNode(
            "unrelated-branch",
            rootNodeId,
            "Unrelated branch",
            y: 0,
            metadataJson: CreateProjectBlockMetadata(outputRoot: @"C:\products\unrelated"));
        var selectedBranch = CreateNode(
            "selected-branch",
            rootNodeId,
            "Selected branch",
            y: 10,
            metadataJson: CreateProjectBlockMetadata(outputRoot: @"C:\products\selected"));
        var focus = CreateNode(
            "runtime",
            selectedBranch.Id,
            "PowerShell runtime",
            objectType: ProjectObjectType.Environment,
            objectSubtype: "powershell-runtime");
        var surface = CreateSurface(projectId, unrelatedBranch, selectedBranch, focus);

        var result = ProjectStructureProcessLaunchContextBuilder.Build(surface, focus);

        Assert.Equal(@"C:\products\selected", result.OutputRoot);
    }

    [Fact]
    public void CTX_AUTH_001_Output_root_authority_resolver_uses_only_the_current_selected_branch()
    {
        var projectId = Guid.NewGuid();
        var rootNodeId = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId);
        var unrelatedBranch = CreateNode(
            "unrelated-branch",
            rootNodeId,
            "Unrelated branch",
            metadataJson: CreateProjectBlockMetadata(outputRoot: @"C:\products\unrelated"));
        var selectedBranch = CreateNode(
            "selected-branch",
            rootNodeId,
            "Selected branch",
            metadataJson: CreateProjectBlockMetadata(outputRoot: @"C:\products\selected"));
        var focus = CreateNode(
            "runtime",
            selectedBranch.Id,
            "PowerShell runtime",
            objectType: ProjectObjectType.Environment,
            objectSubtype: "powershell-runtime",
            metadataJson: ProjectObjectMetadataSerializer.Serialize(
                new ProjectObjectMetadataEnvelope
                {
                    Environment = new ProjectEnvironmentMetadata
                    {
                        WorkingDirectory = @"C:\products\unrelated"
                    }
                }));
        var surface = CreateSurface(projectId, unrelatedBranch, selectedBranch, focus);

        var outputRoot = ProjectStructureOutputRootAuthorityResolver.ResolveProcessOutputRoot(
            surface,
            focus);
        var missingSelectionRoot = ProjectStructureOutputRootAuthorityResolver.ResolveProcessOutputRoot(
            surface,
            focus with { Id = "runtime-not-in-current-surface" });

        Assert.Equal(@"C:\products\selected", outputRoot);
        Assert.Equal(string.Empty, missingSelectionRoot);
    }

    [Fact]
    public void Chat_discovery_requires_the_current_surface()
    {
        var focus = CreateNode(
            "focus",
            null,
            "Focus",
            metadataJson: CreateProjectBlockMetadata(
                outputRoot: @"C:\programovani\dotnet\calculator-e2e-test"));

        var roots = ProjectStructureOutputRootAuthorityResolver.ResolveChatDiscoveryRoots(
            surface: null,
            [new AgentChatContextEntityReference("project-node", focus.Id, focus.Title)],
            new ExternalTargetPathRegistry());

        Assert.Empty(roots);
    }

    [Fact]
    public void Chat_discovery_accepts_the_bounded_calculator_root()
    {
        var projectId = Guid.NewGuid();
        using var fixture = new TemporaryFixtureDirectory(@"C:\programovani\dotnet\calculator-e2e-test");
        var expectedRoot = fixture.Path;
        var focus = CreateNode(
            "focus",
            null,
            "Focus",
            metadataJson: CreateProjectBlockMetadata(
                outputRoot: expectedRoot));
        var surface = CreateSurface(projectId, focus);

        var registry = new ExternalTargetPathRegistry();
        var roots = ProjectStructureOutputRootAuthorityResolver.ResolveChatDiscoveryRoots(
            surface,
            [new AgentChatContextEntityReference("project-node", focus.Id, focus.Title)],
            registry);

        AssertSingleBoundAlias(
            roots,
            registry,
            expectedRoot);
    }

    [Fact]
    public void Chat_discovery_returns_no_grant_when_project_block_root_fields_conflict()
    {
        var projectId = Guid.NewGuid();
        var focus = CreateNode(
            "focus",
            null,
            "Focus",
            metadataJson: CreateProjectBlockMetadata(
                outputRoot: @"C:\programovani\dotnet\calculator-e2e-test",
                repositoryRoot: @"C:\programovani\dotnet\unrelated-app"));
        var surface = CreateSurface(projectId, focus);

        var roots = ProjectStructureOutputRootAuthorityResolver.ResolveChatDiscoveryRoots(
            surface,
            [new AgentChatContextEntityReference("project-node", focus.Id, focus.Title)],
            new ExternalTargetPathRegistry());

        Assert.Empty(roots);
    }

    [Fact]
    public void Chat_discovery_uses_the_nearest_canonical_typed_owner()
    {
        var projectId = Guid.NewGuid();
        using var expectedFixture = new TemporaryFixtureDirectory(@"C:\programovani\dotnet\calculator-e2e-test");
        using var outerFixture = new TemporaryFixtureDirectory(@"C:\programovani\dotnet\outer-app");
        var expectedRoot = expectedFixture.Path;
        var outer = CreateNode(
            "outer",
            null,
            "Outer",
            metadataJson: CreateProjectBlockMetadata(
                outputRoot: outerFixture.Path));
        var owner = CreateNode(
            "owner",
            outer.Id,
            "Owner",
            metadataJson: CreateProjectBlockMetadata(
                outputRoot: expectedRoot));
        var focus = CreateNode(
            "focus",
            owner.Id,
            "Runtime",
            objectType: ProjectObjectType.Environment,
            objectSubtype: "dotnet-watch");
        var surface = CreateSurface(projectId, outer, owner, focus);

        var registry = new ExternalTargetPathRegistry();
        var roots = ProjectStructureOutputRootAuthorityResolver.ResolveChatDiscoveryRoots(
            surface,
            [new AgentChatContextEntityReference("project-node", focus.Id, focus.Title)],
            registry);

        AssertSingleBoundAlias(
            roots,
            registry,
            expectedRoot);
    }

    [Fact]
    public void Chat_discovery_returns_no_grant_for_missing_declared_parent()
    {
        var projectId = Guid.NewGuid();
        var focus = CreateNode(
            "focus",
            "missing-owner",
            "Focus",
            objectType: ProjectObjectType.Note,
            objectSubtype: "context");
        var surface = CreateSurface(projectId, focus);

        var roots = ProjectStructureOutputRootAuthorityResolver.ResolveChatDiscoveryRoots(
            surface,
            [new AgentChatContextEntityReference("project-node", focus.Id, focus.Title)],
            new ExternalTargetPathRegistry());

        Assert.Empty(roots);
    }

    [Fact]
    public void Chat_discovery_ignores_generic_hierarchy_links_for_authority()
    {
        var projectId = Guid.NewGuid();
        using var fixture = new TemporaryFixtureDirectory(@"C:\programovani\dotnet\calculator-e2e-test");
        var expectedRoot = fixture.Path;
        var focus = CreateNode(
            "focus",
            null,
            "Focus",
            metadataJson: CreateProjectBlockMetadata(
                outputRoot: expectedRoot));
        var surface = new ProjectStructureSurface(
            projectId,
            "Launch context test",
            [focus],
            [
                new ProjectStructureLink(
                    "missing-owner",
                    focus.Id,
                    ProjectObjectLinkKind.Contains,
                    IsUserAuthored: false)
            ],
            ViewStateJson: null);

        var registry = new ExternalTargetPathRegistry();
        var roots = ProjectStructureOutputRootAuthorityResolver.ResolveChatDiscoveryRoots(
            surface,
            [new AgentChatContextEntityReference("project-node", focus.Id, focus.Title)],
            registry);

        AssertSingleBoundAlias(
            roots,
            registry,
            expectedRoot);
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"D:\")]
    [InlineData(@"C:\Users")]
    [InlineData(@"C:\Users\another-user")]
    [InlineData(@"C:\Documents and Settings\another-user\project")]
    [InlineData(@"C:\Windows\System32\drivers")]
    [InlineData(@"C:\Program Files\Vendor\Product")]
    public void Chat_discovery_rejects_overly_broad_implicit_roots(string root)
    {
        var projectId = Guid.NewGuid();
        var focus = CreateNode(
            "focus",
            null,
            "Focus",
            metadataJson: CreateProjectBlockMetadata(outputRoot: root));
        var surface = CreateSurface(projectId, focus);

        var roots = ProjectStructureOutputRootAuthorityResolver.ResolveChatDiscoveryRoots(
            surface,
            [new AgentChatContextEntityReference("project-node", focus.Id, focus.Title)],
            new ExternalTargetPathRegistry());

        Assert.Empty(roots);
    }

    [Theory]
    [InlineData(@"C:\src")]
    [InlineData(@"C:\projects")]
    [InlineData(@"D:\Calculator")]
    [InlineData(@"C:\src\Product")]
    [InlineData(@"D:\repos\App")]
    public void Chat_discovery_accepts_bounded_non_protected_project_roots(string fixturePath)
    {
        using var fixture = new TemporaryFixtureDirectory(fixturePath);
        var root = fixture.Path;
        var projectId = Guid.NewGuid();
        var focus = CreateNode(
            "focus",
            null,
            "Focus",
            metadataJson: CreateProjectBlockMetadata(outputRoot: root));
        var surface = CreateSurface(projectId, focus);

        var registry = new ExternalTargetPathRegistry();
        var roots = ProjectStructureOutputRootAuthorityResolver.ResolveChatDiscoveryRoots(
            surface,
            [new AgentChatContextEntityReference("project-node", focus.Id, focus.Title)],
            registry);

        AssertSingleBoundAlias(roots, registry, root);
    }

    [Fact]
    public void Chat_discovery_rejects_the_user_profile_root()
    {
        var projectId = Guid.NewGuid();
        var focus = CreateNode(
            "focus",
            null,
            "Focus",
            metadataJson: CreateProjectBlockMetadata(
                outputRoot: Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile)));
        var surface = CreateSurface(projectId, focus);

        var roots = ProjectStructureOutputRootAuthorityResolver.ResolveChatDiscoveryRoots(
            surface,
            [new AgentChatContextEntityReference("project-node", focus.Id, focus.Title)],
            new ExternalTargetPathRegistry());

        Assert.Empty(roots);
    }

    [Theory]
    [InlineData(ProjectBlockRootField.OutputRoot)]
    [InlineData(ProjectBlockRootField.ProductRoot)]
    [InlineData(ProjectBlockRootField.TargetRoot)]
    [InlineData(ProjectBlockRootField.RepositoryRoot)]
    [InlineData(ProjectBlockRootField.WorkspaceRoot)]
    public void Build_reads_typed_project_block_root_fields(ProjectBlockRootField field)
    {
        var expectedRoot = $@"C:\products\{field}";
        var focus = CreateNode(
            "focus",
            null,
            "Focus",
            metadataJson: CreateProjectBlockMetadata(field, $" {expectedRoot} "));

        var result = ProjectStructureProcessLaunchContextBuilder.Build(null, focus);

        Assert.Equal(expectedRoot, result.OutputRoot);
    }

    [Theory]
    [InlineData("outputRoot")]
    [InlineData("productRoot")]
    [InlineData("targetRoot")]
    [InlineData("targetPath")]
    [InlineData("repositoryRoot")]
    [InlineData("workspaceRoot")]
    public void Build_does_not_promote_arbitrary_nested_metadata_keys_to_output_root(string key)
    {
        var metadataJson = JsonSerializer.Serialize(new
        {
            nested = new Dictionary<string, string>
            {
                [key] = $@"C:\products\{key}"
            }
        });
        var focus = CreateNode("focus", null, "Focus", metadataJson: metadataJson);

        var result = ProjectStructureProcessLaunchContextBuilder.Build(null, focus);

        Assert.Equal(string.Empty, result.OutputRoot);
    }

    [Theory]
    [InlineData("outputRoot")]
    [InlineData("productRoot")]
    [InlineData("targetRoot")]
    [InlineData("targetPath")]
    [InlineData("repositoryRoot")]
    [InlineData("workspaceRoot")]
    public void Build_does_not_promote_arbitrary_top_level_metadata_keys_to_output_root(string key)
    {
        var metadataJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            [key] = $@"C:\products\{key}"
        });
        var focus = CreateNode("focus", null, "Focus", metadataJson: metadataJson);

        var result = ProjectStructureProcessLaunchContextBuilder.Build(null, focus);

        Assert.Equal(string.Empty, result.OutputRoot);
    }

    [Fact]
    public void Build_does_not_promote_title_subtitle_or_notes_paths_to_output_root()
    {
        var focus = CreateNode(
            "focus",
            null,
            @"Title C:\products\title",
            subtitle: @"Subtitle C:\products\subtitle",
            notes: @"Notes C:\products\notes");

        var result = ProjectStructureProcessLaunchContextBuilder.Build(null, focus);

        Assert.Equal(string.Empty, result.OutputRoot);
    }

    [Fact]
    public void Build_requires_project_block_node_type_for_typed_project_block_root_metadata()
    {
        var focus = CreateNode(
            "focus",
            null,
            "Runtime",
            objectType: ProjectObjectType.Environment,
            objectSubtype: "dotnet-runtime",
            metadataJson: CreateProjectBlockMetadata(outputRoot: @"C:\products\unexpected"));

        var result = ProjectStructureProcessLaunchContextBuilder.Build(null, focus);

        Assert.Equal(string.Empty, result.OutputRoot);
    }

    [Fact]
    public void Result_applies_the_summary_and_all_output_root_aliases()
    {
        var result = new ProjectStructureProcessLaunchContext(
            "Context summary",
            @" C:\products\calculator ");
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);

        result.ApplyContextSummaryTo(variables);
        result.ApplyOutputRootAliasesTo(variables);

        Assert.Equal("Context summary", variables["ProjectStructureContextSummary"]);
        Assert.Equal(@"C:\products\calculator", variables["OutputRoot"]);
        Assert.Equal(@"C:\products\calculator", variables["ProductRoot"]);
    }

    [Fact]
    public void Result_preserves_add_only_values_and_removes_only_an_empty_summary_when_requested()
    {
        var result = new ProjectStructureProcessLaunchContext(string.Empty, string.Empty);
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ProjectStructureContextSummary"] = "Inherited summary",
            ["OutputRoot"] = @"C:\inherited\output",
            ["ProductRoot"] = @"C:\inherited\product"
        };

        result.ApplyContextSummaryTo(variables);
        result.ApplyOutputRootAliasesTo(variables);

        Assert.Equal("Inherited summary", variables["ProjectStructureContextSummary"]);
        Assert.Equal(@"C:\inherited\output", variables["OutputRoot"]);
        Assert.Equal(@"C:\inherited\product", variables["ProductRoot"]);

        result.ApplyContextSummaryTo(variables, removeWhenEmpty: true);

        Assert.DoesNotContain("ProjectStructureContextSummary", variables);
        Assert.Equal(@"C:\inherited\output", variables["OutputRoot"]);
        Assert.Equal(@"C:\inherited\product", variables["ProductRoot"]);
    }

    private static ProjectStructureSurface CreateSurface(
        Guid projectId,
        params ProjectStructureNode[] nodes)
        => new(projectId, "Launch context test", nodes, [], null);

    private static string CreateProjectBlockMetadata(
        string outputRoot = "",
        string productRoot = "",
        string targetRoot = "",
        string repositoryRoot = "",
        string workspaceRoot = "")
    {
        return ProjectObjectMetadataSerializer.Serialize(
            new ProjectObjectMetadataEnvelope
            {
                ProjectBlock = new ProjectBlockMetadata
                {
                    OutputRoot = outputRoot,
                    ProductRoot = productRoot,
                    TargetRoot = targetRoot,
                    RepositoryRoot = repositoryRoot,
                    WorkspaceRoot = workspaceRoot
                }
            });
    }

    private static string CreateProjectBlockMetadata(
        ProjectBlockRootField field,
        string value)
    {
        return field switch
        {
            ProjectBlockRootField.OutputRoot => CreateProjectBlockMetadata(outputRoot: value),
            ProjectBlockRootField.ProductRoot => CreateProjectBlockMetadata(productRoot: value),
            ProjectBlockRootField.TargetRoot => CreateProjectBlockMetadata(targetRoot: value),
            ProjectBlockRootField.RepositoryRoot => CreateProjectBlockMetadata(repositoryRoot: value),
            ProjectBlockRootField.WorkspaceRoot => CreateProjectBlockMetadata(workspaceRoot: value),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unsupported project-block root field.")
        };
    }

    private static ProjectStructureNode CreateNode(
        string id,
        string? parentId,
        string title,
        ProjectObjectType objectType = ProjectObjectType.ProjectBlock,
        string objectSubtype = "requirements",
        string subtitle = "",
        string status = "Draft",
        string notes = "",
        string artifactKind = "",
        string mediaRelativePath = "",
        string mediaContentType = "",
        string mediaOriginalFileName = "",
        double x = 0,
        double y = 0,
        string metadataJson = "{}")
        => new(
            id,
            parentId,
            objectType,
            objectSubtype,
            title,
            subtitle,
            status,
            notes,
            string.Empty,
            artifactKind,
            null,
            mediaRelativePath,
            mediaContentType,
            mediaOriginalFileName,
            x,
            y,
            new ProjectObjectVisualProfile("rect", "accent", "image", string.Empty),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0,
            MetadataJson: metadataJson);

    private static void AssertSingleBoundAlias(
        IReadOnlyList<string> roots,
        IExternalTargetPathRegistry registry,
        string expectedRoot)
    {
        var alias = Assert.Single(roots);
        Assert.StartsWith("external-target/v1/", alias, StringComparison.Ordinal);
        Assert.Equal(
            ExternalTargetAliasResolutionKind.Resolved,
            registry.TryResolve(alias, out var resolvedRoot, out _));
        Assert.Equal(
            Path.GetFullPath(expectedRoot),
            resolvedRoot,
            ignoreCase: OperatingSystem.IsWindows());
    }

    private sealed class TemporaryFixtureDirectory : IDisposable
    {
        private readonly string rootPath;

        public TemporaryFixtureDirectory(string fixturePath)
        {
            rootPath = System.IO.Path.Combine(
                AppContext.BaseDirectory,
                $"candoitall-launch-context-{Guid.NewGuid():N}");
            var relativeFixturePath = fixturePath.Length >= 2 && fixturePath[1] == ':'
                ? fixturePath[2..]
                : fixturePath;
            var segments = relativeFixturePath.Split(
                ['\\', '/'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Path = System.IO.Path.Combine([rootPath, .. segments]);
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    public enum ProjectBlockRootField
    {
        OutputRoot,
        ProductRoot,
        TargetRoot,
        RepositoryRoot,
        WorkspaceRoot
    }
}
