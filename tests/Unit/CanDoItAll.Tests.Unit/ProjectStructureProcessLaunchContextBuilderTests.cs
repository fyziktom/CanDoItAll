using System.Text.Json;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

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
    public void Build_prefers_direct_metadata_over_direct_text_and_ancestor_metadata()
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
            notes: @"Text fallback C:\products\text.",
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

    [Theory]
    [InlineData("outputRoot")]
    [InlineData("productRoot")]
    [InlineData("targetRoot")]
    [InlineData("targetPath")]
    [InlineData("repositoryRoot")]
    [InlineData("workspaceRoot")]
    public void Build_reads_established_output_root_metadata_keys(string key)
    {
        var expectedRoot = $@"C:\products\{key}";
        var metadataJson = JsonSerializer.Serialize(new
        {
            nested = new Dictionary<string, string>
            {
                [key] = $" {expectedRoot} "
            }
        });
        var focus = CreateNode("focus", null, "Focus", metadataJson: metadataJson);

        var result = ProjectStructureProcessLaunchContextBuilder.Build(null, focus);

        Assert.Equal(expectedRoot, result.OutputRoot);
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
}
