using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureCanonicalTaskMutationPolicyTests
{
    [Theory]
    [InlineData("task")]
    [InlineData(" Task ")]
    [InlineData("TASK")]
    public void Generic_create_rejects_canonical_task(string subtype)
    {
        AssertTypedPathRequired(() =>
            ProjectStructureCanonicalTaskMutationPolicy.EnsureGenericCreateAllowed(
                ProjectObjectType.WorkItem,
                subtype));
    }

    [Fact]
    public void Generic_update_rejects_existing_task_even_when_reclassifying_it()
    {
        var task = CreateNode(
            "task-1",
            ProjectObjectType.WorkItem,
            ProjectObjectSubtypePolicy.Task);

        AssertTypedPathRequired(() =>
            ProjectStructureCanonicalTaskMutationPolicy.EnsureGenericUpdateAllowed(
                task,
                ProjectObjectType.Note,
                string.Empty));
    }

    [Fact]
    public void Generic_update_rejects_reclassification_into_task()
    {
        var note = CreateNode("note-1", ProjectObjectType.Note, string.Empty);

        AssertTypedPathRequired(() =>
            ProjectStructureCanonicalTaskMutationPolicy.EnsureGenericUpdateAllowed(
                note,
                ProjectObjectType.WorkItem,
                ProjectObjectSubtypePolicy.Task));
    }

    [Fact]
    public void Generic_metadata_update_rejects_canonical_task()
    {
        var task = CreateNode(
            "task-1",
            ProjectObjectType.WorkItem,
            ProjectObjectSubtypePolicy.Task);

        AssertTypedPathRequired(() =>
            ProjectStructureCanonicalTaskMutationPolicy.EnsureGenericMetadataUpdateAllowed(task));
    }

    [Fact]
    public void Generic_paths_allow_non_task_nodes()
    {
        var note = CreateNode("note-1", ProjectObjectType.Note, string.Empty);

        ProjectStructureCanonicalTaskMutationPolicy.EnsureGenericCreateAllowed(
            ProjectObjectType.Note,
            string.Empty);
        ProjectStructureCanonicalTaskMutationPolicy.EnsureGenericUpdateAllowed(
            note,
            ProjectObjectType.File,
            "text");
        ProjectStructureCanonicalTaskMutationPolicy.EnsureGenericMetadataUpdateAllowed(note);
    }

    private static void AssertTypedPathRequired(Action action)
    {
        var exception = Assert.Throws<ProjectStructureAgentException>(action);

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(ProjectStructureCanonicalTaskMutationPolicy.ErrorCode, exception.ErrorCode);
    }

    private static ProjectStructureNodeSummary CreateNode(
        string id,
        ProjectObjectType objectType,
        string objectSubtype)
    {
        return new ProjectStructureNodeSummary(
            Id: id,
            ParentId: null,
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
