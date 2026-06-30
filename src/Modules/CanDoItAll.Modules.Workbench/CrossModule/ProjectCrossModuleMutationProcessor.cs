using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed record DeleteSubtreeMutationPayload(
    string RootNodeKey,
    IReadOnlyList<string> DeletedNodeKeys,
    int LinkCount);

internal sealed record MoveDescendantsMutationPayload(
    Guid SourceProjectId,
    Guid TargetProjectId,
    string SourceNodeKey,
    IReadOnlyList<string> MovedNodeKeys,
    IReadOnlyList<string> MovedRootKeys);

public sealed class ProjectCrossModuleMutationProcessor(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IProjectPartyIntegrationBridge projectPartyIntegrationBridge,
    ProjectCrossModuleMutationCoordinator mutationCoordinator)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal async Task<ProjectCrossModuleMutationStatus?> ProcessAsync(
        Guid mutationId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var mutation = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .FirstOrDefaultAsync(item => item.Id == mutationId, cancellationToken);
        if (mutation is null)
        {
            return null;
        }

        if (mutation.Status == ProjectCrossModuleMutationStatus.Completed)
        {
            return mutation.Status;
        }

        if (mutation.ApprovalState is ProjectCrossModuleMutationApprovalState.Pending or ProjectCrossModuleMutationApprovalState.Rejected)
        {
            return mutation.Status;
        }

        if (mutation.Status is not ProjectCrossModuleMutationStatus.WorkbenchCommitted and not ProjectCrossModuleMutationStatus.Failed)
        {
            return mutation.Status;
        }

        mutationCoordinator.MarkAttempt(mutation);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await ExecuteCommittedMutationAsync(mutation, cancellationToken);
            mutationCoordinator.MarkCompleted(mutation);
        }
        catch (Exception ex)
        {
            mutationCoordinator.MarkFailed(mutation, ex.Message);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return mutation.Status;
    }

    private Task ExecuteCommittedMutationAsync(
        ProjectCrossModuleMutationRecord mutation,
        CancellationToken cancellationToken)
    {
        return mutation.MutationKind switch
        {
            ProjectCrossModuleMutationKind.DeleteSubtree => DeleteAssignmentsAsync(
                mutation.ProjectId,
                Deserialize<DeleteSubtreeMutationPayload>(mutation.PayloadJson).DeletedNodeKeys,
                cancellationToken),
            ProjectCrossModuleMutationKind.MoveDescendants => MoveAssignmentsAsync(
                Deserialize<MoveDescendantsMutationPayload>(mutation.PayloadJson),
                cancellationToken),
            ProjectCrossModuleMutationKind.MoveSelectedNodes => MoveAssignmentsAsync(
                Deserialize<MoveDescendantsMutationPayload>(mutation.PayloadJson),
                cancellationToken),
            _ => Task.CompletedTask
        };
    }

    private Task DeleteAssignmentsAsync(
        Guid projectId,
        IReadOnlyList<string> deletedNodeKeys,
        CancellationToken cancellationToken)
    {
        return projectPartyIntegrationBridge.DeleteAssignmentsForNodesAsync(
            projectId,
            BuildNodeReferences(deletedNodeKeys),
            cancellationToken);
    }

    private Task MoveAssignmentsAsync(
        MoveDescendantsMutationPayload payload,
        CancellationToken cancellationToken)
    {
        return projectPartyIntegrationBridge.MoveAssignmentsToProjectAsync(
            payload.SourceProjectId,
            BuildNodeReferences(payload.MovedNodeKeys),
            payload.TargetProjectId,
            cancellationToken);
    }

    private static IReadOnlyList<ProjectNodeReference> BuildNodeReferences(IReadOnlyList<string> nodeKeys)
    {
        return nodeKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => new ProjectNodeReference(key))
            .ToList();
    }

    private static TPayload Deserialize<TPayload>(string payloadJson)
    {
        var payload = JsonSerializer.Deserialize<TPayload>(
            string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            JsonOptions);
        return payload
            ?? throw new InvalidOperationException($"Unable to deserialize {typeof(TPayload).Name}.");
    }
}
