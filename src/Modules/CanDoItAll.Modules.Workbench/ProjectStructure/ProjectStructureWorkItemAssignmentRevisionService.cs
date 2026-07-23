using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureWorkItemAssignmentRevisionService(IClock clock) :
    IProjectWorkItemAssignmentMutationBridge
{
    public async Task<ProjectWorkItemDirectAssignmentMutationResult>
        StageMutationAsync(
        AppDbContext dbContext,
        Guid projectId,
        ProjectNodeReference taskNode,
        IReadOnlyCollection<ProjectWorkItemDirectAssignmentState>
            finalAssignments,
        ProjectWorkItemDirectAssignmentRevision?
            expectedCurrentRevision = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(finalAssignments);
        foreach (var assignment in finalAssignments)
        {
            if (assignment.PartyType is not (
                    ProjectPartyType.Person or
                    ProjectPartyType.AiAgent) ||
                assignment.PartyId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "A direct task-assignment revision requires valid Person or Agent identities.");
            }
        }

        var workItemRecord = await dbContext.Set<ProjectObjectRecord>()
            .FirstOrDefaultAsync(
                item =>
                    item.ProjectId == projectId &&
                    item.NodeKey == taskNode.NodeKey &&
                    !item.IsSystemManaged,
                cancellationToken);
        if (workItemRecord is null ||
            workItemRecord.ObjectType != ProjectObjectType.WorkItem)
        {
            return new ProjectWorkItemDirectAssignmentMutationResult(
                ProjectWorkItemDirectAssignmentMutationStatus
                    .WorkItemNotFound,
                Revision: null);
        }

        var isCanonicalTask =
            ProjectStructureCanonicalTaskMutationPolicy.IsTask(
                workItemRecord.ObjectType,
                workItemRecord.ObjectSubtype);
        await ProjectNodeBindingStorage.LoadAsync(
            dbContext,
            [workItemRecord],
            cancellationToken);
        var metadata = ProjectObjectMetadataSerializer.Parse(
            workItemRecord.MetadataJson);
        metadata.WorkItem ??= new ProjectWorkItemMetadata
        {
            WorkItemKind = ProjectNodeKindRegistry.ResolveWorkItemKind(
                workItemRecord.ObjectSubtype)
        };
        var workItem = metadata.WorkItem;
        if (expectedCurrentRevision.HasValue &&
            workItem.DirectAssignmentRevision !=
                expectedCurrentRevision.Value.Value)
        {
            return new ProjectWorkItemDirectAssignmentMutationResult(
                ProjectWorkItemDirectAssignmentMutationStatus
                    .RevisionConflict,
                new ProjectWorkItemDirectAssignmentRevision(
                    workItem.DirectAssignmentRevision));
        }

        workItem.DirectAssignmentRevision = checked(
            workItem.DirectAssignmentRevision + 1);
        workItem.AssigneePartyDisplayName = ResolveDisplayName(finalAssignments);
        if (isCanonicalTask &&
            ShouldClearPricing(workItem, finalAssignments))
        {
            workItem.ExpectedCostAmount = null;
            workItem.ExpectedCostCurrencyCode = string.Empty;
            workItem.ExpectedCostBasis = null;
        }

        ProjectObjectMetadataSerializer.Validate(
            workItemRecord.ObjectType,
            workItemRecord.ObjectSubtype,
            metadata);
        workItemRecord.MetadataJson =
            ProjectWorkbenchObjectModeling.ResolveMetadataJson(
            workItemRecord.ObjectType,
            workItemRecord.ObjectSubtype,
            ProjectObjectMetadataSerializer.Serialize(metadata),
            workItemRecord.MetadataJson,
            workItemRecord.Notes,
            media: null);
        workItemRecord.UpdatedAtUtc = clock.GetUtcNow();
        await ProjectNodeBindingStorage.PersistAsync(
            dbContext,
            workItemRecord,
            cancellationToken);
        return new ProjectWorkItemDirectAssignmentMutationResult(
            ProjectWorkItemDirectAssignmentMutationStatus.Applied,
            new ProjectWorkItemDirectAssignmentRevision(
                workItem.DirectAssignmentRevision));
    }

    private static bool ShouldClearPricing(
        ProjectWorkItemMetadata workItem,
        IReadOnlyCollection<ProjectWorkItemDirectAssignmentState>
            finalAssignments)
    {
        if (workItem.ExecutionState != ProjectTaskExecutionState.NotStarted)
        {
            return false;
        }

        if (workItem.ExpectedCostBasis is not { } basis)
        {
            return workItem.ExpectedCostAmount.HasValue ||
                !string.IsNullOrWhiteSpace(workItem.ExpectedCostCurrencyCode);
        }

        if (basis.ResourceKind is not (
                ProjectStructureTaskResourceKind.Person or
                ProjectStructureTaskResourceKind.Agent))
        {
            return false;
        }

        var expectedPartyType = basis.ResourceKind switch
        {
            ProjectStructureTaskResourceKind.Person =>
                ProjectPartyType.Person,
            ProjectStructureTaskResourceKind.Agent =>
                ProjectPartyType.AiAgent,
            _ => (ProjectPartyType?)null
        };
        return !expectedPartyType.HasValue ||
            !finalAssignments.Any(assignment =>
                assignment.PartyType == expectedPartyType.Value &&
                assignment.PartyId == basis.ResourceId);
    }

    private static string ResolveDisplayName(
        IReadOnlyCollection<ProjectWorkItemDirectAssignmentState>
            finalAssignments)
    {
        var primaryAssignments = finalAssignments
            .Where(static assignment => assignment.IsPrimary)
            .ToArray();
        var representative = primaryAssignments.Length == 1
            ? primaryAssignments[0]
            : finalAssignments.Count == 1
                ? finalAssignments.Single()
                : null;
        return representative?.DisplayName.Trim() ?? string.Empty;
    }
}
