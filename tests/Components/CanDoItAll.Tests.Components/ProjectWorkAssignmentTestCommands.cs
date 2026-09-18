using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components;

internal class ProjectWorkAssignmentTestCommands(IProjectWorkAssignmentCommands inner) : IProjectWorkAssignmentCommands {
    public Task<Result> ReplaceAsync(Guid projectId, ProjectNodeReference node,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot>? expectedAssignments = null,
        ProjectWorkItemDirectAssignmentRevision? expectedRevision = null, CancellationToken cancellationToken = default,
        ProjectWriteAdmission? expectedProjectAdmission = null) =>
        inner.ReplaceAsync(projectId, node, desiredAssignments, expectedAssignments, expectedRevision,
            cancellationToken, expectedProjectAdmission);

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
    public virtual Task<Result> StageReplaceAsync(Guid projectId, ProjectNodeReference node,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments, IReadOnlyList<Guid> newAssignmentIds,
        IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot>? expectedAssignments = null,
        ProjectWorkItemDirectAssignmentRevision? expectedRevision = null, CancellationToken cancellationToken = default,
        ProjectWriteAdmission? expectedProjectAdmission = null) =>
        inner.StageReplaceAsync(projectId, node, desiredAssignments, newAssignmentIds, expectedAssignments,
            expectedRevision, cancellationToken, expectedProjectAdmission);
    public Task DeleteAsync(Guid assignmentId, CancellationToken cancellationToken = default,
        ProjectAssignmentReference? expectedReference = null) => throw Unused();
    public Task StageDeleteAsync(Guid assignmentId, CancellationToken cancellationToken = default,
        ProjectAssignmentReference? expectedReference = null) => throw Unused();
    public Task StageDeleteForNodesAsync(Guid projectId, IReadOnlyCollection<ProjectNodeReference> nodes, CancellationToken cancellationToken = default,
        ProjectAssignmentReference? expectedReference = null) => throw Unused();
    public Task StageDeleteForProjectAsync(Guid projectId, CancellationToken cancellationToken = default,
        ProjectAssignmentReference? expectedReference = null) => throw Unused();
    public Task StageMoveAsync(Guid sourceProjectId, Guid targetProjectId, IReadOnlyCollection<ProjectNodeReference> nodes, CancellationToken cancellationToken = default,
        ProjectAssignmentReference? sourceReference = null,
        ProjectWriteAdmission? expectedTargetAdmission = null) => throw Unused();
    public Task StagePartyMergeAsync(ProjectWorkAssignmentPartyMerge merge, CancellationToken cancellationToken = default) => throw Unused();

    private static NotSupportedException Unused() => new("This component fixture only substitutes assignment replacement.");
}
