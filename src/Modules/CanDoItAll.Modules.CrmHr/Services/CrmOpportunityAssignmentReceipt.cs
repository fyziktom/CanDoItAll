using System.Collections.Immutable;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CrmHr;

internal sealed class CrmOpportunityAssignmentReceipt(ProjectWriteAdmission project,
    IEnumerable<ProjectPartyAssignmentKind> kinds, IEnumerable<CrmOpportunityAssignmentSnapshot> before,
    IEnumerable<CrmOpportunityAssignmentSnapshot> after) {
    internal ProjectWriteAdmission Project { get; } = project;
    internal ImmutableArray<ProjectPartyAssignmentKind> Kinds { get; } = kinds.ToImmutableArray();
    internal ImmutableArray<CrmOpportunityAssignmentSnapshot> Before { get; } = before.ToImmutableArray();
    internal ImmutableArray<CrmOpportunityAssignmentSnapshot> After { get; } = after.ToImmutableArray();
}

internal sealed record CrmOpportunityAssignmentSnapshot(Guid Id, Guid ProjectId, Guid? ProjectLifetimeId, Guid PartyId,
    Guid? PartyOrganizationAffiliationId, ProjectPartyAssignmentKind AssignmentKind, string NodeKey, string PhaseName,
    Guid? OpportunityId, decimal? AllocationPercent, DateTimeOffset? StartsAtUtc, DateTimeOffset? EndsAtUtc,
    bool IsPrimary, string Source, string Notes) {
    internal static CrmOpportunityAssignmentSnapshot From(ProjectPartyAssignment row) => new(row.Id, row.ProjectId,
        row.ProjectLifetimeId, row.PartyId, row.PartyOrganizationAffiliationId, row.AssignmentKind, row.NodeKey,
        row.PhaseName, row.OpportunityId, row.AllocationPercent, row.StartsAtUtc, row.EndsAtUtc, row.IsPrimary, row.Source, row.Notes);

    internal ProjectPartyAssignment ToRecord() => new() {
        Id = Id, ProjectId = ProjectId, ProjectLifetimeId = ProjectLifetimeId, PartyId = PartyId,
        PartyOrganizationAffiliationId = PartyOrganizationAffiliationId, AssignmentKind = AssignmentKind,
        NodeKey = NodeKey, PhaseName = PhaseName, OpportunityId = OpportunityId, AllocationPercent = AllocationPercent,
        StartsAtUtc = StartsAtUtc, EndsAtUtc = EndsAtUtc, IsPrimary = IsPrimary, Source = Source, Notes = Notes
    };
}

public sealed class CrmOpportunityConversionRequiresObservationException(ProjectWriteAdmission project, Exception innerException,
    IEnumerable<Error>? originalErrors = null)
    : InvalidOperationException($"Opportunity conversion requires review for project '{project.ProjectId:D}'. Automatic compensation could not be confirmed. Review its project and assignment state before retrying.", innerException) {
    public const string ErrorCode = "crmhr.crm.opportunity-conversion-requires-observation";
    public ProjectWriteAdmission Project { get; } = project;
    public ImmutableArray<Error> OriginalErrors { get; } = originalErrors?.ToImmutableArray() ?? [];
}
