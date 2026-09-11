using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components;

internal sealed class ProjectWorkAssignmentTestCommands(IProjectPartyIntegrationBridge bridge) : IProjectWorkAssignmentCommands {
    public Task<Result> ReplaceAsync(Guid projectId, ProjectNodeReference node,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot>? expectedAssignments = null,
        ProjectWorkItemDirectAssignmentRevision? expectedRevision = null, CancellationToken cancellationToken = default) =>
        expectedAssignments is null
            ? bridge.ReplaceNodeAssignmentsAsync(projectId, node, desiredAssignments, [ProjectPartyAssignmentRole.WorkItemAssignee], cancellationToken)
            : bridge.ReplaceNodeAssignmentsIfCurrentAsync(projectId, node, desiredAssignments, [ProjectPartyAssignmentRole.WorkItemAssignee],
                expectedAssignments, expectedRevision, cancellationToken);

    public Task<IReadOnlyList<ProjectWorkAssignmentFact>> ListForProjectsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken = default) => throw Unused();
    public Task<IReadOnlyList<ProjectWorkAssignmentFact>> ListForPartiesAsync(IReadOnlyCollection<Guid> partyIds, CancellationToken cancellationToken = default) => throw Unused();
    public Task<IReadOnlyList<ProjectWorkAssignmentFact>> ListForProjectsForMutationAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken = default) => throw Unused();
    public Task<ProjectWorkAssignmentFact?> GetAsync(Guid assignmentId, CancellationToken cancellationToken = default) => throw Unused();
    public Task<ProjectWorkAssignmentFact?> GetForMutationAsync(Guid assignmentId, CancellationToken cancellationToken = default) => throw Unused();
    public Task<IReadOnlyList<Guid>> ListPartyMergeProjectsAsync(Guid retainedPartyId, Guid mergedPartyId,
        IReadOnlyCollection<Guid> affiliationIds, CancellationToken cancellationToken = default) => throw Unused();
    public Task<Result<Guid>> SaveAsync(ProjectPartyAssignmentUpsertRequest request, CancellationToken cancellationToken = default) => throw Unused();
    public Task<Result<Guid>> StageSaveAsync(ProjectPartyAssignmentUpsertRequest request, Guid newAssignmentId,
        ProjectWorkAssignmentCarryOver? carryOver = null, CancellationToken cancellationToken = default) => throw Unused();
    public Task<Result> StageReplaceAsync(Guid projectId, ProjectNodeReference node,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments, IReadOnlyList<Guid> newAssignmentIds,
        IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot>? expectedAssignments = null,
        ProjectWorkItemDirectAssignmentRevision? expectedRevision = null, CancellationToken cancellationToken = default) => throw Unused();
    public Task DeleteAsync(Guid assignmentId, CancellationToken cancellationToken = default) => throw Unused();
    public Task StageDeleteAsync(Guid assignmentId, CancellationToken cancellationToken = default) => throw Unused();
    public Task StageDeleteForNodesAsync(Guid projectId, IReadOnlyCollection<ProjectNodeReference> nodes, CancellationToken cancellationToken = default) => throw Unused();
    public Task StageDeleteForProjectAsync(Guid projectId, CancellationToken cancellationToken = default) => throw Unused();
    public Task StageMoveAsync(Guid sourceProjectId, Guid targetProjectId, IReadOnlyCollection<ProjectNodeReference> nodes, CancellationToken cancellationToken = default) => throw Unused();
    public Task StagePartyMergeAsync(ProjectWorkAssignmentPartyMerge merge, CancellationToken cancellationToken = default) => throw Unused();

    private static NotSupportedException Unused() => new("This component fixture only substitutes assignment replacement.");
}
