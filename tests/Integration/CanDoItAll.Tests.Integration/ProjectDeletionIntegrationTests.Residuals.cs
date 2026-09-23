using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class ProjectDeletionIntegrationTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Direct_participant_completion_retains_residual_rows_and_original_pending_or_terminal_evidence(
        bool completed,
        bool boundOriginalReference) {
        await using var application = await CreateObservedDeletionApplicationAsync(failFirstFileSystemDelete: false);
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var dbContextFactory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var source = boundOriginalReference
            ? await CreateRetiredProjectReferenceAsync(services, "Retained original cleanup")
            : null;
        var projectId = source?.ProjectId ?? Guid.NewGuid();
        var dependencyId = await SeedProjectDeletionMutationAsync(dbContextFactory, projectId,
            ProjectCrossModuleMutationStatus.WorkbenchCommitted,
            new DeleteProjectMutationPayload([], [], SourceReference: source));
        var recoveryId = await SeedProjectDeletionMutationAsync(dbContextFactory, projectId,
            completed ? ProjectCrossModuleMutationStatus.Completed : ProjectCrossModuleMutationStatus.WorkbenchCommitted,
            new DeleteProjectMutationPayload([], [], OutstandingMutationIds: [dependencyId], SourceReference: source));
        await using (var setup = await dbContextFactory.CreateDbContextAsync()) {
            setup.Add(new ProjectObjectRecord {
                ProjectId = projectId,
                NodeKey = $"custom:{Guid.NewGuid():N}",
                ObjectType = ProjectObjectType.Note,
                Title = "Retained human note",
                Notes = "Newly discovered rows are not part of the original deletion receipt.",
                MetadataJson = "{\"retained\":true}",
                ParentNodeKey = BuildProjectRootNodeKey(projectId),
                PositionX = 73,
                PositionY = 91,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await setup.SaveChangesAsync();
        }
        var original = await ReadDeletionStateAsync(dbContextFactory, projectId);
        var participant = services.GetServices<IProjectDeletionParticipant>()
            .Single(candidate => candidate.Id == WorkbenchParticipantId);

        var failure = await Assert.ThrowsAsync<ProjectDeletionParticipantCleanupException>(() =>
            participant.CompleteAsync(new(projectId, recoveryId)));

        Assert.Equal(recoveryId, failure.RecoveryId);
        Assert.Contains("require reconciliation", failure.Message, StringComparison.Ordinal);
        Assert.Equal(original, await ReadDeletionStateAsync(dbContextFactory, projectId));
        Assert.Equal(0, services.GetRequiredService<ObservedStorageDriverRegistry>().FileSystemDeleteCalls);
        await using var restartScope = application.Services.CreateAsyncScope();
        var projects = restartScope.ServiceProvider.GetRequiredService<ProjectsService>();
        if (completed) {
            var notice = Assert.Single(await projects.ListDeletionCompletionNoticesAsync(),
                candidate => candidate.RecoveryId == recoveryId);
            Assert.Equal(ProjectDeletionCompletionOperation.ProjectDeletion, notice.Operation);
            var readback = await projects.RetryDeletionCleanupAsync(projectId, WorkbenchParticipantId, recoveryId);
            Assert.Equal(projectId, readback.ProjectId);
            Assert.Empty(readback.Warnings);
        } else {
            var retryFailure = await Assert.ThrowsAsync<ProjectDeletionPartialCommitException>(() =>
                projects.RetryDeletionCleanupAsync(projectId, WorkbenchParticipantId, recoveryId));
            Assert.Equal(recoveryId,
                Assert.IsType<ProjectDeletionParticipantCleanupException>(retryFailure.InnerException).RecoveryId);
        }
        Assert.Equal(original, await ReadDeletionStateAsync(dbContextFactory, projectId));
        await using var verification = await dbContextFactory.CreateDbContextAsync();
        var dependency = await verification.Set<ProjectCrossModuleMutationRecord>().SingleAsync(row => row.Id == dependencyId);
        Assert.Equal(ProjectCrossModuleMutationStatus.WorkbenchCommitted, dependency.Status);
        Assert.Equal(0, dependency.AttemptCount);
        Assert.Equal(0, services.GetRequiredService<ObservedStorageDriverRegistry>().FileSystemDeleteCalls);
    }

    [Fact]
    public async Task Original_pending_cleanup_cannot_inherit_recreated_project_rows_or_change_its_receipt() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var originalReference = await CreateRetiredProjectReferenceAsync(services, "Deleted original project");
        var projects = services.GetRequiredService<ProjectsService>();
        Assert.True((await projects.CreateAsync(originalReference.ProjectId,
            new ProjectEditorModel { Name = "Recreated human project" })).IsSuccess);
        var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        var current = Assert.IsType<ProjectWriteAdmission>(await admissions.CaptureAsync(originalReference.ProjectId));
        Assert.NotEqual(originalReference.LifetimeId, current.LifetimeId);
        var node = await services.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(current.ProjectId,
            new ProjectObjectCreateRequest(ProjectObjectType.Note, "Human replacement note", string.Empty,
                "Keep the replacement lifetime's content.", BuildProjectRootNodeKey(current.ProjectId), 50, 70) {
                ExpectedProjectAdmission = current
            });
        var dbContextFactory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var dependencyId = await SeedProjectDeletionMutationAsync(dbContextFactory, current.ProjectId,
            ProjectCrossModuleMutationStatus.WorkbenchCommitted,
            new DeleteProjectMutationPayload([], [], SourceReference: originalReference));
        var recoveryId = await SeedProjectDeletionMutationAsync(dbContextFactory, current.ProjectId,
            ProjectCrossModuleMutationStatus.WorkbenchCommitted,
            new DeleteProjectMutationPayload([], [], OutstandingMutationIds: [dependencyId], SourceReference: originalReference));
        var original = await ReadDeletionStateAsync(dbContextFactory, current.ProjectId);

        await using var restartScope = application.Services.CreateAsyncScope();
        var restartedProjects = restartScope.ServiceProvider.GetRequiredService<ProjectsService>();
        var failure = await Assert.ThrowsAsync<ProjectDeletionPartialCommitException>(() =>
            restartedProjects.RetryDeletionCleanupAsync(current.ProjectId, WorkbenchParticipantId, recoveryId));

        Assert.Equal(recoveryId,
            Assert.IsType<ProjectDeletionParticipantCleanupException>(failure.InnerException).RecoveryId);
        Assert.Equal(current, await restartScope.ServiceProvider.GetRequiredService<ProjectWriteAdmissionService>()
            .CaptureAsync(current.ProjectId));
        Assert.Equal("Recreated human project", (await restartedProjects.GetAsync(current.ProjectId)).Name);
        Assert.Equal(original, await ReadDeletionStateAsync(dbContextFactory, current.ProjectId));
        await using var verification = await dbContextFactory.CreateDbContextAsync();
        var retainedNode = await verification.Set<ProjectObjectRecord>().SingleAsync(row => row.NodeKey == node.Id);
        Assert.Equal("Human replacement note", retainedNode.Title);
        var receipt = await verification.Set<ProjectCrossModuleMutationRecord>().SingleAsync(row => row.Id == recoveryId);
        Assert.Equal(originalReference, Deserialize<DeleteProjectMutationPayload>(receipt.PayloadJson).SourceReference);
        Assert.Equal(ProjectCrossModuleMutationStatus.WorkbenchCommitted, receipt.Status);
        Assert.Equal(0, receipt.AttemptCount);
    }

    private static async Task<ProjectAssignmentReference> CreateRetiredProjectReferenceAsync(
        IServiceProvider services,
        string title) {
        var projects = services.GetRequiredService<ProjectsService>();
        var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        var projectId = await CreateProjectAsync(projects, title);
        var original = Assert.IsType<ProjectWriteAdmission>(await admissions.CaptureAsync(projectId));
        var deletion = await projects.DeleteAsync(projectId, expectedProjectAdmission: original);
        Assert.Equal(projectId, deletion.ProjectId);
        Assert.Empty(deletion.Warnings);
        Assert.Null(await admissions.CaptureAsync(projectId));
        await using var database = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        var retirement = await database.Set<ProjectRetirementRecord>().SingleAsync(row => row.LifetimeId == original.LifetimeId);
        Assert.Equal(original.ProjectId, retirement.ProjectId);
        return ProjectAssignmentReference.From(original);
    }

    private static async Task<(string Objects, string Receipts)> ReadDeletionStateAsync(
        IDbContextFactory<AppDbContext> factory,
        Guid projectId) {
        await using var database = await factory.CreateDbContextAsync();
        var objects = await database.Set<ProjectObjectRecord>().AsNoTracking()
            .Where(row => row.ProjectId == projectId).OrderBy(row => row.Id).ToArrayAsync();
        var receipts = await database.Set<ProjectCrossModuleMutationRecord>().AsNoTracking()
            .Where(row => row.ProjectId == projectId).OrderBy(row => row.Id).ToArrayAsync();
        return (JsonSerializer.Serialize(objects, JsonOptions), JsonSerializer.Serialize(receipts, JsonOptions));
    }
}
