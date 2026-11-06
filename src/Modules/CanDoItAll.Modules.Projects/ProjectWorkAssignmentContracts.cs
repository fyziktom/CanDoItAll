using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

public sealed record ProjectWorkAssignmentFact(
    Guid Id,
    Guid ProjectId,
    Guid PartyId,
    Guid? PartyOrganizationAffiliationId,
    string NodeKey,
    string PhaseName,
    Guid? OpportunityId,
    decimal? AllocationPercent,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    bool IsPrimary,
    string Source,
    string Notes,
    Guid? ProjectLifetimeId = null) {
    public ProjectPartyAssignmentRole Role => ProjectPartyAssignmentRole.WorkItemAssignee;

    public ProjectAssignmentObservation Original => new(Id, ProjectId, ProjectLifetimeId, PartyId, PartyOrganizationAffiliationId,
        Role, NodeKey, PhaseName, OpportunityId, IsPrimary, AllocationPercent, StartsAtUtc, EndsAtUtc, Source, Notes);
}

public sealed record ProjectWorkAssignmentCarryOver(Guid Id, string PhaseName, Guid? OpportunityId, Guid? ProjectLifetimeId);

public sealed record ProjectWorkAssignmentPartyFact(Guid PartyId, ProjectPartyType PartyType, string DisplayName);

public sealed record ProjectWorkAssignmentAffiliationRequirement(
    Guid PartyId,
    Guid? AffiliationId,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc);

public sealed record ProjectWorkAssignmentPartyMerge(
    Guid MergedPartyId,
    ProjectWorkAssignmentPartyFact RetainedParty,
    IReadOnlyDictionary<Guid, Guid> ReplacedAffiliations,
    IReadOnlyCollection<Guid> LockedProjectIds,
    IReadOnlyCollection<Guid> ObservedAffiliationIds,
    ProjectWorkPartyMergeObservation Original);

public sealed record ProjectWorkAssignmentPartyMergeChange(Guid AssignmentId, Guid ProjectId, string NodeKey);

public interface IProjectWorkAssignmentPartyFacts {
    Task<IReadOnlyDictionary<Guid, ProjectWorkAssignmentPartyFact>> ReadForMutationAsync(
        IReadOnlyCollection<Guid> partyIds, CancellationToken cancellationToken = default);

    Task<Error?> ValidateAffiliationsForMutationAsync(
        IReadOnlyCollection<ProjectWorkAssignmentAffiliationRequirement> requirements,
        CancellationToken cancellationToken = default);

    Task<Guid?> FindParticipationProjectForMutationAsync(Guid assignmentId, CancellationToken cancellationToken = default);

    Task<ProjectAssignmentObservation?> ReadParticipationForMutationAsync(Guid assignmentId, CancellationToken cancellationToken = default);
}

public interface IProjectWorkAssignmentQueries {
    Task<ProjectAssignmentPrimaryObservation> ReadPrimaryObservationAsync(ProjectWriteAdmission project, string nodeKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectWorkAssignmentFact>> ListForProjectsAsync(
        IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectWorkAssignmentFact>> ListForPartiesAsync(
        IReadOnlyCollection<Guid> partyIds, CancellationToken cancellationToken = default);

    Task<ProjectWorkAssignmentFact?> GetAsync(Guid assignmentId, CancellationToken cancellationToken = default);
}

public interface IProjectWorkAssignmentCommands : IProjectWorkAssignmentQueries {
    Task<IReadOnlyList<ProjectWorkAssignmentNodeChange>> ReadExistingNodeChangesForMutationAsync(
        IReadOnlyCollection<(Guid ProjectId, string NodeKey)> nodes, CancellationToken cancellationToken = default);

    void PublishCommittedNodeChanges(IReadOnlyList<ProjectWorkAssignmentNodeChange> nodes);

    Task<IReadOnlyList<ProjectWorkAssignmentFact>> ListForProjectsForMutationAsync(
        IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken = default);

    Task<ProjectWorkAssignmentFact?> GetForMutationAsync(Guid assignmentId, CancellationToken cancellationToken = default);

    Task<ProjectWorkPartyMergeObservation> ReadPartyMergeForMutationAsync(Guid retainedPartyId, Guid mergedPartyId,
        IReadOnlyCollection<Guid> affiliationIds, int maximumRows, CancellationToken cancellationToken = default);

    Task<Result<Guid>> SaveAsync(ProjectPartyAssignmentUpsertRequest request, CancellationToken cancellationToken = default,
        Action<ProjectAssignmentSaveReceipt>? onCommitted = null);

    Task<Result<Guid>> StageSaveAsync(ProjectPartyAssignmentUpsertRequest request, Guid newAssignmentId,
        ProjectWorkAssignmentCarryOver? carryOver = null, CancellationToken cancellationToken = default);

    Task<Result> ReplaceAsync(Guid projectId, ProjectNodeReference node,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot>? expectedAssignments = null,
        ProjectWorkItemDirectAssignmentRevision? expectedRevision = null,
        CancellationToken cancellationToken = default,
        ProjectWriteAdmission? expectedProjectAdmission = null);

    Task<Result> StageReplaceAsync(Guid projectId, ProjectNodeReference node,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<Guid> newAssignmentIds,
        IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot>? expectedAssignments = null,
        ProjectWorkItemDirectAssignmentRevision? expectedRevision = null,
        CancellationToken cancellationToken = default,
        ProjectWriteAdmission? expectedProjectAdmission = null);

    Task DeleteAsync(Guid assignmentId, CancellationToken cancellationToken = default,
        ProjectAssignmentReference? expectedReference = null, ProjectAssignmentObservation? original = null,
        Action<ProjectAssignmentDeleteReceipt>? onCommitted = null);

    Task StageDeleteAsync(Guid assignmentId, CancellationToken cancellationToken = default,
        ProjectAssignmentReference? expectedReference = null, ProjectAssignmentObservation? original = null);

    Task StageDeleteForNodesAsync(Guid projectId, IReadOnlyCollection<ProjectNodeReference> nodes,
        CancellationToken cancellationToken = default,
        ProjectAssignmentReference? expectedReference = null);

    Task StageDeleteForProjectAsync(Guid projectId, CancellationToken cancellationToken = default,
        ProjectAssignmentReference? expectedReference = null);

    Task StageMoveAsync(Guid sourceProjectId, Guid targetProjectId, IReadOnlyCollection<ProjectNodeReference> nodes,
        CancellationToken cancellationToken = default,
        ProjectAssignmentReference? sourceReference = null,
        ProjectWriteAdmission? expectedTargetAdmission = null);

    Task<IReadOnlyList<ProjectWorkAssignmentPartyMergeChange>> StagePartyMergeAsync(ProjectWorkAssignmentPartyMerge merge, CancellationToken cancellationToken = default);
}

public static class ProjectAssignmentMutationKeys {
    public static string ForAssignment(Guid assignmentId) => $"project-assignment:{assignmentId:D}";
}
