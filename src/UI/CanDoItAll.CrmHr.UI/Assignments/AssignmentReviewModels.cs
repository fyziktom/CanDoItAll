using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.CrmHr.UI.Assignments;

public static class AssignmentReviewModels {
    public static ProjectPartyAssignmentUpsertRequest From(ProjectAssignmentObservation row, ProjectWriteAdmission project) => new() {
        AssignmentId = row.Id,
        Original = row,
        ProjectId = row.ProjectId,
        ExpectedProjectAdmission = project,
        PartyId = row.PartyId,
        PartyAffiliationId = row.PartyAffiliationId,
        Role = row.Role,
        NodeKey = row.NodeKey,
        IsPrimary = row.IsPrimary,
        AllocationPercent = row.AllocationPercent,
        StartsOn = Date(row.StartsAtUtc),
        EndsOn = Date(row.EndsAtUtc),
        Source = row.Source,
        Notes = row.Notes
    };

    public static StaffingRequestEditorModel From(StaffingRequestObservation row, ProjectWriteAdmission project) => new() {
        Id = row.Id,
        Original = row,
        ProjectId = row.ProjectId,
        ExpectedProjectAdmission = project,
        ExpectedSourceProjectReference = row.ProjectId is { } id ? new(project.DatabaseProfileId, id, row.ProjectLifetimeId) : null,
        RequestedByPartyId = row.RequestedByPartyId,
        DeliveryUnitPartyId = row.DeliveryUnitPartyId,
        Title = row.Title,
        NeededRole = row.NeededRole,
        SkillIds = [.. row.SkillIds],
        StartDate = Date(row.StartDateUtc),
        EndDate = Date(row.EndDateUtc),
        AllocationPercent = row.AllocationPercent,
        Status = row.Status,
        Notes = row.Notes
    };

    private static DateOnly? Date(DateTimeOffset? value) => value is { } date ? DateOnly.FromDateTime(date.UtcDateTime) : null;
}
