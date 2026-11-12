using System.Collections.Immutable;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.CrmHr;

public enum PartyMergeMember {
    Party,
    Role,
    Contact,
    Address,
    ConfidentialNote,
    Relationship,
    Affiliation,
    WorkforceProfile,
    Skill,
    Capacity,
    AiProfile,
    AiBinding,
    AccountProfile,
    AccountConnection,
    AccountConnectionProject,
    Interaction,
    InteractionParty,
    Opportunity,
    OpportunityParty,
    RecruitmentApplication,
    RecruitmentInterview,
    OnboardingTask,
    StaffingRequest,
    ProjectAssignment
}

public sealed record PartyMergeRowVersion(PartyMergeMember Member, Guid Id, long Version);

public sealed record PartyMergePartyModel(Guid Id, string DisplayName, PartyType PartyType,
    PartyLifecycleStatus LifecycleStatus, bool IsSensitive);

public sealed record PartyMergeObservation(Guid DatasetId, long ActivationGeneration, Guid RetainedPartyId,
    Guid MergedPartyId, ImmutableArray<PartyMergeRowVersion> Rows, ImmutableArray<Guid> AffectedPersonIds,
    ImmutableArray<Guid> ProjectIds, ImmutableArray<Guid> AffiliationIds, ProjectWorkPartyMergeObservation Work) {
    public bool Matches(PartyMergeObservation current) => DatasetId == current.DatasetId &&
        ActivationGeneration == current.ActivationGeneration && RetainedPartyId == current.RetainedPartyId &&
        MergedPartyId == current.MergedPartyId && !Rows.IsDefault && Rows.SequenceEqual(current.Rows) &&
        !AffectedPersonIds.IsDefault && AffectedPersonIds.SequenceEqual(current.AffectedPersonIds) &&
        !ProjectIds.IsDefault && ProjectIds.SequenceEqual(current.ProjectIds) &&
        !AffiliationIds.IsDefault && AffiliationIds.SequenceEqual(current.AffiliationIds) &&
        Work is { } work && current.Work is { } currentWork && work.Matches(currentWork);
}

public sealed record PartyMergePreviewModel(PartyMergePartyModel Retained, PartyMergePartyModel Duplicate,
    PartyMergeObservation Original);
