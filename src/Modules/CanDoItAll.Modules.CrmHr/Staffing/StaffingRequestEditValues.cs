using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CrmHr;

internal static class StaffingRequestEditValues {
    internal static string ScopeKey(Guid id) => $"crmhr:staffing-request:{id:D}";

    internal static StaffingRequestObservation Observe(StaffingRequest row) => new(row.Id, row.ProjectId, row.ProjectLifetimeId,
        row.RequestedByPartyId, row.DeliveryUnitPartyId, row.Title, row.NeededRole, Skills(row.NeededSkillsJson, row.Id),
        row.StartDateUtc, row.EndDateUtc, row.AllocationPercent, row.Status, row.Notes);

    internal static ImmutableArray<Guid> Skills(string json, Guid id) {
        try {
            return string.IsNullOrWhiteSpace(json) ? [] :
                (JsonSerializer.Deserialize<Guid[]>(json) ?? throw new InvalidOperationException($"Staffing request '{id:D}' has null skill data."))
                    .Distinct().Order().ToImmutableArray();
        } catch (JsonException exception) {
            throw new InvalidOperationException($"Staffing request '{id:D}' has invalid skill data.", exception);
        }
    }

    internal static bool Same(StaffingRequestObservation left, StaffingRequestObservation right) =>
        !left.SkillIds.IsDefault && !right.SkillIds.IsDefault &&
        (left with { SkillIds = default }) == (right with { SkillIds = default }) &&
        left.SkillIds.AsSpan().SequenceEqual(right.SkillIds.AsSpan());

    internal static StaffingRequestEditorModel Editor(StaffingRequest row, Guid profileId) => new() {
        Id = row.Id, ProjectId = row.ProjectId,
        ExpectedSourceProjectReference = row.ProjectId is { } project ? new(profileId, project, row.ProjectLifetimeId) : null,
        RequestedByPartyId = row.RequestedByPartyId, DeliveryUnitPartyId = row.DeliveryUnitPartyId,
        Title = row.Title, NeededRole = row.NeededRole, SkillIds = Skills(row.NeededSkillsJson, row.Id).ToList(),
        StartDate = Date(row.StartDateUtc), EndDate = Date(row.EndDateUtc), AllocationPercent = row.AllocationPercent,
        Status = row.Status, Notes = row.Notes, Original = Observe(row)
    };

    internal static DateTimeOffset? Utc(DateOnly? date) => date is { } value
        ? new DateTimeOffset(value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)) : null;
    private static DateOnly? Date(DateTimeOffset? date) => date is { } value ? DateOnly.FromDateTime(value.UtcDateTime) : null;
    internal static Error Conflict() => Error.Validation("The staffing request changed or was removed. Refresh its shown demand before replacing it.", StaffingRequestEditingErrors.Conflict);
}
