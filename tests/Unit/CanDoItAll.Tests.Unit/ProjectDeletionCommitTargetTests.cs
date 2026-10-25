using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectDeletionCommitTargetTests {
    [Fact]
    public void Ordinary_deletion_requires_the_original_target_lifetime_and_existing_write_purpose() {
        var project = Project();
        var deletion = new ProjectDeletionCommitTarget(project, null);
        deletion.RequireOriginalTarget(new(ProjectMutationPurpose.ExistingWrite, project.ProjectId, [project]));

        Assert.Throws<InvalidOperationException>(() => deletion.RequireOriginalTarget(
            new(ProjectMutationPurpose.ExistingWrite, project.ProjectId, [])));
        Assert.Throws<InvalidOperationException>(() => deletion.RequireOriginalTarget(
            new(ProjectMutationPurpose.ExistingWrite, project.ProjectId,
                [new(project.DatabaseProfileId, project.ProjectId, Guid.NewGuid())])));
        Assert.Throws<InvalidOperationException>(() => deletion.RequireOriginalTarget(
            new(ProjectMutationPurpose.HierarchyWrite, project.ProjectId, [project])));
        Assert.Throws<InvalidOperationException>(() => deletion.RequireOriginalTarget(
            new(ProjectMutationPurpose.ExistingWrite, project.ProjectId, default)));
    }

    [Fact]
    public void Compensation_requires_the_original_complete_reservation_and_parent_shape() {
        var project = Project();
        var parent = new ProjectWriteAdmission(project.DatabaseProfileId, Guid.NewGuid(), Guid.NewGuid());
        var reservation = new ProjectCreationReservation(Guid.NewGuid(), project.DatabaseProfileId, project.ProjectId,
            project.LifetimeId, Guid.NewGuid(), parent.ProjectId, parent.LifetimeId);
        var deletion = new ProjectDeletionCommitTarget(project, reservation);
        deletion.RequireOriginalTarget(new(ProjectMutationPurpose.CreateChild, project.ProjectId, [parent], reservation));

        Assert.Throws<InvalidOperationException>(() => deletion.RequireOriginalTarget(
            new(ProjectMutationPurpose.CreateChild, project.ProjectId, [parent], reservation with { Id = Guid.NewGuid() })));
        Assert.Throws<InvalidOperationException>(() => deletion.RequireOriginalTarget(
            new(ProjectMutationPurpose.CreateChild, project.ProjectId, [parent], reservation with { RequesterId = Guid.NewGuid() })));
        Assert.Throws<InvalidOperationException>(() => deletion.RequireOriginalTarget(
            new(ProjectMutationPurpose.CreateRoot, project.ProjectId, [parent], reservation)));
        Assert.Throws<InvalidOperationException>(() => deletion.RequireOriginalTarget(
            new(ProjectMutationPurpose.CreateChild, project.ProjectId, [], reservation)));
        Assert.Throws<ArgumentException>(() => new ProjectDeletionCommitTarget(project,
            reservation with { LifetimeId = Guid.NewGuid() }));
    }

    [Fact]
    public void Native_unreserved_compensation_does_not_accept_an_invented_reservation() {
        var project = Project();
        var deletion = new ProjectDeletionCommitTarget(project, null);
        deletion.RequireOriginalTarget(new(ProjectMutationPurpose.CreateRoot, project.ProjectId, []));
        var reservation = new ProjectCreationReservation(Guid.NewGuid(), project.DatabaseProfileId, project.ProjectId,
            project.LifetimeId, Guid.NewGuid(), null, null);

        Assert.Throws<InvalidOperationException>(() => deletion.RequireOriginalTarget(
            new(ProjectMutationPurpose.CreateRoot, project.ProjectId, [], reservation)));
        Assert.Throws<InvalidOperationException>(() => deletion.RequireOriginalTarget(
            new(ProjectMutationPurpose.CreateChild, project.ProjectId, [])));
    }

    [Fact]
    public async Task An_unknown_source_lease_does_not_inherit_permission_to_commit_a_deletion() {
        await using IProjectMutationSourceLease source = new ExistingWriteOnlySource();
        await source.RequireForMutationAsync();
        await Assert.ThrowsAsync<NotSupportedException>(() => source.RequireForDeletionCommitAsync(new(Project(), null)));
    }

    private static ProjectWriteAdmission Project() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private sealed class ExistingWriteOnlySource : IProjectMutationSourceLease {
        public IReadOnlyCollection<ProjectWriteAdmission> SourceProjects => [];
        public Task RequireForMutationAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
