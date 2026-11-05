using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.CrmHr;

internal static class ProjectAssignmentEditValues {
    internal static ProjectAssignmentObservation Observe(ProjectPartyAssignment row) => new(
        row.Id, row.ProjectId, row.ProjectLifetimeId, row.PartyId, row.PartyOrganizationAffiliationId,
        ProjectAssignmentRoleMapping.MapRole(row.AssignmentKind), row.NodeKey, row.PhaseName, row.OpportunityId,
        row.IsPrimary, row.AllocationPercent, row.StartsAtUtc, row.EndsAtUtc, row.Source, row.Notes);
}
