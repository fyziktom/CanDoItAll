using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureProcessContextNodeFilterTests
{
    [Fact]
    public void ShouldIncludeInProcessContext_excludes_process_run_keyed_nodes()
    {
        var runId = Guid.NewGuid();
        var node = CreateNode(
            ProjectStructureProcessNodeKeys.BuildProcessRunRuntimeNodeKey(runId, "src/App/App.csproj"),
            $"project:{Guid.NewGuid():D}",
            ProjectObjectType.Environment,
            "dotnet-runtime",
            "Run app",
            "Runtime command from an earlier run.",
            string.Empty,
            "process-run-runtime");

        var include = ProjectStructureProcessContextNodeFilter.ShouldIncludeInProcessContext(node);

        Assert.False(include);
    }

    [Fact]
    public void ShouldIncludeInProcessContext_excludes_orphaned_process_run_descendants()
    {
        var runId = Guid.NewGuid();
        var node = CreateNode(
            "custom:old-run-output",
            ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(runId),
            ProjectObjectType.ProjectBlock,
            "operations",
            "Output folder",
            @"Prior output root C:\stale\old-run must not seed a new launch.",
            string.Empty,
            string.Empty);

        var include = ProjectStructureProcessContextNodeFilter.ShouldIncludeInProcessContext(node);

        Assert.False(include);
    }

    [Fact]
    public void ShouldIncludeInProcessContext_keeps_authored_visual_source_assets()
    {
        var node = CreateNode(
            "custom:proposal",
            $"custom:{Guid.NewGuid():N}",
            ProjectObjectType.ImageAsset,
            "generated",
            "Calculator UI proposal",
            "Source visual target for implementation and QA.",
            "managed-files/project-media/images/project/proposal.png",
            "image/png");

        var include = ProjectStructureProcessContextNodeFilter.ShouldIncludeInProcessContext(node);

        Assert.True(include);
    }

    private static ProjectStructureNode CreateNode(
        string id,
        string? parentId,
        ProjectObjectType objectType,
        string objectSubtype,
        string title,
        string notes,
        string mediaRelativePath,
        string artifactKind)
        => new(
            id,
            parentId,
            objectType,
            objectSubtype,
            title,
            string.Empty,
            "Draft",
            notes,
            string.Empty,
            artifactKind,
            null,
            mediaRelativePath,
            string.Empty,
            string.IsNullOrWhiteSpace(mediaRelativePath) ? string.Empty : Path.GetFileName(mediaRelativePath),
            0,
            0,
            new ProjectObjectVisualProfile("rect", "accent", "image", string.Empty),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0);
}
