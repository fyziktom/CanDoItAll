using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureNonTaskWritePolicyTests
{
    [Theory]
    [InlineData(false, false, false, false, false)]
    [InlineData(false, false, true, true, false)]
    [InlineData(false, true, false, false, true)]
    [InlineData(false, true, true, true, true)]
    [InlineData(true, false, false, true, true)]
    [InlineData(true, false, true, true, true)]
    [InlineData(true, true, false, true, true)]
    [InlineData(true, true, true, true, true)]
    public void Capability_matrix_keeps_non_task_and_task_authority_independent_until_broad_write(
        bool canWrite,
        bool canWriteTasks,
        bool canWriteNonTaskStructure,
        bool expectedStructureMutationTools,
        bool expectedTaskMutationTools)
    {
        var access = new AgentProjectStructureAccessSettings
        {
            CanWrite = canWrite,
            CanWriteTasks = canWriteTasks,
            CanWriteNonTaskStructure = canWriteNonTaskStructure
        };

        Assert.Equal(
            expectedStructureMutationTools,
            ProjectStructureNonTaskWritePolicy.CanUseStructureMutationTools(access));
        Assert.Equal(
            expectedTaskMutationTools,
            ProjectStructureNonTaskWritePolicy.CanUseTaskMutationTools(access));
    }

    [Theory]
    [InlineData("task")]
    [InlineData(" Task ")]
    [InlineData("TASK")]
    public void EnsureNodeCreateAllowed_denies_task_creation_for_non_task_writer(string subtype)
    {
        AssertTaskWriteDenied(() => ProjectStructureNonTaskWritePolicy.EnsureNodeCreateAllowed(
            requiresNonTaskGuard: true,
            ProjectObjectType.WorkItem,
            subtype));
    }

    [Fact]
    public void EnsureNodeUpdateAllowed_denies_changes_to_existing_task()
    {
        var task = CreateNode("task-1", ProjectObjectType.WorkItem, ProjectObjectSubtypePolicy.Task);

        AssertTaskWriteDenied(() => ProjectStructureNonTaskWritePolicy.EnsureNodeUpdateAllowed(
            requiresNonTaskGuard: true,
            task,
            requestedObjectType: null,
            requestedObjectSubtype: null));
    }

    [Fact]
    public void EnsureNodeUpdateAllowed_denies_reclassification_to_task()
    {
        var note = CreateNode("note-1", ProjectObjectType.Note, string.Empty);

        AssertTaskWriteDenied(() => ProjectStructureNonTaskWritePolicy.EnsureNodeUpdateAllowed(
            requiresNonTaskGuard: true,
            note,
            ProjectObjectType.WorkItem,
            " Task "));
    }

    [Fact]
    public void EnsureNodeUpdateAllowed_denies_reclassification_from_task()
    {
        var task = CreateNode("task-1", ProjectObjectType.WorkItem, ProjectObjectSubtypePolicy.Task);

        AssertTaskWriteDenied(() => ProjectStructureNonTaskWritePolicy.EnsureNodeUpdateAllowed(
            requiresNonTaskGuard: true,
            task,
            ProjectObjectType.Note,
            string.Empty));
    }

    [Fact]
    public void EnsureNodesAllowed_denies_mixed_subtree_containing_task()
    {
        var nodes = new[]
        {
            CreateNode("note-1", ProjectObjectType.Note, string.Empty),
            CreateNode("task-1", ProjectObjectType.WorkItem, ProjectObjectSubtypePolicy.Task)
        };

        AssertTaskWriteDenied(() => ProjectStructureNonTaskWritePolicy.EnsureNodesAllowed(
            requiresNonTaskGuard: true,
            nodes));
    }

    [Fact]
    public void EnsureNodeUpdateAllowed_allows_non_task_child_under_task()
    {
        var child = CreateNode(
            "note-child",
            ProjectObjectType.Note,
            string.Empty,
            parentId: "task-parent");

        ProjectStructureNonTaskWritePolicy.EnsureNodeUpdateAllowed(
            requiresNonTaskGuard: true,
            child,
            ProjectObjectType.File,
            "text");
    }

    [Fact]
    public void RequiresFullStructureWrite_reserves_import_for_broad_writer()
    {
        Assert.True(ProjectStructureNonTaskWritePolicy.RequiresFullStructureWrite(
            AgentToolInvocationPolicyMetadata.ProjectStructureImport));
        Assert.True(ProjectStructureNonTaskWritePolicy.RequiresFullStructureWrite(
            AgentToolInvocationPolicyMetadata.ProjectStructureImport.ToUpperInvariant()));
        Assert.False(ProjectStructureNonTaskWritePolicy.RequiresFullStructureWrite(
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeCreate));
        Assert.False(ProjectStructureNonTaskWritePolicy.RequiresFullStructureWrite(
            AgentToolInvocationPolicyMetadata.ProjectTaskCreate));
    }

    [Fact]
    public void EnsureImportAllowed_denies_task_leaf_for_non_task_writer_but_not_broad_writer()
    {
        AssertTaskWriteDenied(() => ProjectStructureNonTaskWritePolicy.EnsureImportAllowed(
            requiresNonTaskGuard: true,
            " Task "));

        ProjectStructureNonTaskWritePolicy.EnsureImportAllowed(
            requiresNonTaskGuard: false,
            ProjectObjectSubtypePolicy.Task);
    }

    private static void AssertTaskWriteDenied(Action action)
    {
        var exception = Assert.Throws<ProjectStructureAgentException>(action);

        Assert.Equal(403, exception.StatusCode);
        Assert.Equal("ProjectTaskWriteDenied", exception.ErrorCode);
    }

    private static ProjectStructureNodeSummary CreateNode(
        string id,
        ProjectObjectType objectType,
        string objectSubtype,
        string? parentId = null)
    {
        return new ProjectStructureNodeSummary(
            Id: id,
            ParentId: parentId,
            ObjectType: objectType,
            ObjectSubtype: objectSubtype,
            Title: id,
            Subtitle: string.Empty,
            Status: "Draft",
            Notes: null,
            Route: string.Empty,
            ArtifactKind: string.Empty,
            ArtifactId: null,
            MediaRelativePath: null,
            MediaContentType: null,
            MediaOriginalFileName: null,
            Badges: [],
            ProgressMode: string.Empty,
            ProgressPercent: ProjectProgressPolicy.UntrackedPercent,
            MarkerIcon: string.Empty,
            MarkerTone: string.Empty,
            MarkerLabel: string.Empty,
            Priority: 0,
            EffectivePriority: 0,
            StartUtc: null,
            EndUtc: null,
            MetadataJson: null,
            ProjectRole: ProjectStructureProjectRole.None,
            RelatedProjectId: null,
            ParentProjectCount: 0,
            X: null,
            Y: null);
    }
}
