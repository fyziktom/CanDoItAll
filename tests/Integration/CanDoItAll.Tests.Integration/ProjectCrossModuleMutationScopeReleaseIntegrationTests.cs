using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectCrossModuleMutationScopeReleaseIntegrationTests
{
    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task New_subtree_deletion_releases_structure_scope_before_actual_CRM_cleanup()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var partyIntegration = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = await CreateProjectAsync(projects, "Subtree scope release");
        var node = await CreateAssignableWorkItemAsync(workbench, projectId, "Delete with CRM");
        await SeedAssignmentAsync(partyIntegration, projectId, node.Id);

        var deletedCount = await workbench.DeleteObjectAsync(projectId, node.Id)
            .WaitAsync(CompletionTimeout);

        Assert.Equal(1, deletedCount);
        await AssertMutationCompletedAndAssignmentAbsentAsync(
            dbContextFactory,
            projectId,
            node.Id,
            ProjectCrossModuleMutationKind.DeleteSubtree);
    }

    [Fact]
    public async Task Subtree_replay_releases_structure_scope_before_actual_CRM_cleanup()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var partyIntegration = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var mutationCoordinator = scope.ServiceProvider
            .GetRequiredService<ProjectCrossModuleMutationCoordinator>();
        var projectId = await CreateProjectAsync(projects, "Replay scope release");
        var node = await CreateAssignableWorkItemAsync(workbench, projectId, "Replay with CRM");
        await SeedAssignmentAsync(partyIntegration, projectId, node.Id);
        var mutationId = Guid.NewGuid();

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        await using (var mutationScope =
                     await ProjectStructureSerializableMutationScope.BeginBindingWriteAsync(
                         dbContext,
                         ProjectStructureSerializableMutationScope.ForProject(projectId),
                         CancellationToken.None))
        {
            var projectObject = await dbContext.Set<ProjectObjectRecord>()
                .SingleAsync(record =>
                    record.ProjectId == projectId &&
                    record.NodeKey == node.Id);
            var binding = await dbContext.Set<ProjectNodeBindingRecord>()
                .SingleAsync(record => record.ProjectObjectId == projectObject.Id);
            var mutation = mutationCoordinator.Begin(
                projectId,
                node.Id,
                ProjectCrossModuleMutationKind.DeleteSubtree,
                JsonSerializer.Serialize(new DeleteSubtreeMutationPayload(
                    node.Id,
                    [node.Id],
                    0)));
            mutation.Id = mutationId;
            mutationCoordinator.MarkWorkbenchCommitted(mutation);
            dbContext.Remove(binding);
            dbContext.Remove(projectObject);
            dbContext.Set<ProjectCrossModuleMutationRecord>().Add(mutation);
            await dbContext.SaveChangesAsync();
            await mutationScope.CommitAsync(CancellationToken.None);
        }

        var replay = await workbench.RetryDeletionCleanupDetailedAsync(
                projectId,
                node.Id,
                mutationId)
            .WaitAsync(CompletionTimeout);

        Assert.Equal(1, replay.DeletedNodeCount);
        Assert.Empty(replay.DeletionWarnings);
        await AssertMutationCompletedAndAssignmentAbsentAsync(
            dbContextFactory,
            projectId,
            node.Id,
            ProjectCrossModuleMutationKind.DeleteSubtree);
    }

    [Fact]
    public async Task Node_transfer_releases_both_project_scopes_before_actual_CRM_move()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var partyIntegration = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var sourceProjectId = await CreateProjectAsync(projects, "Transfer source");
        var targetProjectId = await CreateProjectAsync(projects, "Transfer target");
        var node = await CreateAssignableWorkItemAsync(workbench, sourceProjectId, "Move with CRM");
        var assignmentId = await SeedAssignmentAsync(
            partyIntegration,
            sourceProjectId,
            node.Id);

        var result = await workbench.MoveNodesToProjectAsync(
                sourceProjectId,
                [node.Id],
                targetProjectId)
            .WaitAsync(CompletionTimeout);

        Assert.NotNull(result);
        Assert.Equal([node.Id], result.MovedNodeIds);
        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Equal(
            targetProjectId,
            (await verificationContext.Set<ProjectObjectRecord>()
                .AsNoTracking()
                .SingleAsync(record => record.NodeKey == node.Id)).ProjectId);
        var assignment = await verificationContext.Set<ProjectPartyAssignment>()
            .AsNoTracking()
            .SingleAsync(record => record.Id == assignmentId);
        Assert.Equal(targetProjectId, assignment.ProjectId);
        var mutation = await verificationContext.Set<ProjectCrossModuleMutationRecord>()
            .AsNoTracking()
            .SingleAsync(record =>
                record.ProjectId == sourceProjectId &&
                record.MutationKind == ProjectCrossModuleMutationKind.MoveSelectedNodes);
        Assert.Equal(ProjectCrossModuleMutationStatus.Completed, mutation.Status);
        Assert.True(await verificationContext.Set<ProjectPartyAssignmentMoveReceipt>()
            .AnyAsync(receipt => receipt.OperationId == mutation.Id));
    }

    private static async Task<Guid> CreateProjectAsync(
        ProjectsService projects,
        string name)
    {
        var result = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Objective = "Prove committed mutation scopes are released before reconciliation.",
            CurrentPhase = "Validation"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static Task<ProjectStructureNode> CreateAssignableWorkItemAsync(
        ProjectWorkbenchService workbench,
        Guid projectId,
        string title)
    {
        return workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                title,
                string.Empty,
                "Exercise actual CRM reconciliation after the Workbench commit.",
                $"project:{projectId:D}",
                200,
                160,
                null,
                null,
                "task"));
    }

    private static async Task<Guid> SeedAssignmentAsync(
        IProjectPartyIntegrationBridge partyIntegration,
        Guid projectId,
        string nodeKey)
    {
        var party = await partyIntegration.CreatePartyAsync(new ProjectPartyQuickCreateRequest
        {
            ProjectId = projectId,
            PartyKind = ProjectPartyQuickCreateKind.Person,
            DisplayName = $"Mutation scope party {Guid.NewGuid():N}",
            Summary = "Exercises actual CRM assignment reconciliation."
        });
        Assert.True(
            party.IsSuccess,
            string.Join(" ", party.Errors.Select(error => error.Message)));
        var createdParty = Assert.IsType<ProjectPartyQuickCreateResult>(party.Value);
        var assignment = await partyIntegration.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = createdParty.PartyId,
            Role = ProjectPartyAssignmentRole.WorkItemAssignee,
            NodeKey = nodeKey,
            IsPrimary = true,
            Source = "cross-module-scope-release-integration"
        });
        Assert.True(
            assignment.IsSuccess,
            string.Join(" ", assignment.Errors.Select(error => error.Message)));
        return assignment.Value;
    }

    private static async Task AssertMutationCompletedAndAssignmentAbsentAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid projectId,
        string nodeKey,
        ProjectCrossModuleMutationKind mutationKind)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.False(await dbContext.Set<ProjectPartyAssignment>()
            .AnyAsync(record =>
                record.ProjectId == projectId &&
                record.NodeKey == nodeKey));
        var mutation = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .AsNoTracking()
            .SingleAsync(record =>
                record.ProjectId == projectId &&
                record.ScopeNodeKey == nodeKey &&
                record.MutationKind == mutationKind);
        Assert.Equal(ProjectCrossModuleMutationStatus.Completed, mutation.Status);
    }
}
