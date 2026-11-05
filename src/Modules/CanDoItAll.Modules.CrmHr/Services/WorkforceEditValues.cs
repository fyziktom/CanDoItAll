using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

internal static class WorkforceEditValues {
    internal const string SkillCatalogScopeKey = "crmhr:workforce-skill-catalog";
    internal static string ScopeKey(Guid partyId) => $"crmhr:workforce:{partyId:D}";
    internal static DateOnly? Date(DateTimeOffset? value) => value is { } date ? DateOnly.FromDateTime(date.UtcDateTime) : null;

    internal static WorkforceProfileObservation Profile(Guid partyId, PartyType partyType, WorkforceProfile? row) =>
        new(partyId, partyType, row?.Id, row is null ? null : new(row.WorkforceKind, row.EmployeeCode, row.JobTitle,
            row.Discipline, row.Seniority, row.HomeUnitPartyId, row.ManagerPartyId, Date(row.StartDateUtc), Date(row.EndDateUtc),
            row.Location, row.TimeZone, row.InternalCostRate, row.ExternalBillingRate, row.RateUnit, row.RateCurrencyCode,
            row.CapacityHoursPerWeek, row.Status, row.Notes));

    internal static WorkforceProfileEditorModel AcceptedProfile(Party party, WorkforceProfile row) {
        var original = Profile(party.Id, party.PartyType, row);
        var model = original.Values!.ToEditor(party.Id, row.Id, party.LastChangedBy);
        model.Original = original;
        return model;
    }

    internal static SkillCatalogItemModel SkillDefinition(SkillDefinition row) => new(row.Id, row.Name, row.Category, row.Description, row.IsActive);

    internal static PartySkillObservation Skill(PartySkill row) => new(row.Id, row.PartyId, row.SkillId, row.Proficiency,
        row.YearsExperience, row.CertificationStatus, Date(row.LastValidatedAtUtc), row.Notes);

    internal static PartySkillEditorModel AcceptedSkill(PartySkill row) => new() {
        Id = row.Id, PartyId = row.PartyId, SkillId = row.SkillId, Proficiency = row.Proficiency,
        YearsExperience = row.YearsExperience, CertificationStatus = row.CertificationStatus,
        LastValidatedOn = Date(row.LastValidatedAtUtc), Notes = row.Notes, Original = Skill(row)
    };

    internal static CapacityBlockObservation Capacity(CapacityBlock row) => new(row.Id, row.PartyId, row.BlockKind,
        Date(row.StartDateUtc)!.Value, Date(row.EndDateUtc)!.Value, row.Percentage, row.RelatedProjectId, row.Notes);

    internal static CapacityBlockEditorModel AcceptedCapacity(CapacityBlock row) => new() {
        Id = row.Id, PartyId = row.PartyId, BlockKind = row.BlockKind, StartDate = Date(row.StartDateUtc),
        EndDate = Date(row.EndDateUtc), Percentage = row.Percentage, RelatedProjectId = row.RelatedProjectId,
        Notes = row.Notes, Original = Capacity(row)
    };

    internal static Error Conflict(string code) => Error.Failure("The saved workforce record changed. Read and review its current values before saving.", code);

    internal static bool IsConflict(Exception exception) => exception is DbUpdateConcurrencyException || SerializableMutationScope.IsConflict(exception);

    internal static WorkforceKind DefaultKind(PartyType partyType, IReadOnlyList<PartyRoleKind> roles, WorkforceProfile? profile) {
        if (profile is not null) {
            return profile.WorkforceKind;
        }

        if (partyType == PartyType.Organization || partyType == PartyType.OrganizationUnit) {
            return WorkforceKind.DeliveryUnit;
        }

        if (roles.Contains(PartyRoleKind.Contractor)) {
            return WorkforceKind.Contractor;
        }

        if (roles.Contains(PartyRoleKind.Freelancer)) {
            return WorkforceKind.Freelancer;
        }

        return WorkforceKind.Employee;
    }

    internal static string DefaultStatus(PartyLifecycleStatus lifecycleStatus) {
        return lifecycleStatus switch {
            PartyLifecycleStatus.Active => "Active",
            PartyLifecycleStatus.Inactive => "Inactive",
            _ => "Planned"
        };
    }

    internal static PartyRoleKind Role(WorkforceKind workforceKind) {
        return workforceKind switch {
            WorkforceKind.Contractor => PartyRoleKind.Contractor,
            WorkforceKind.Freelancer => PartyRoleKind.Freelancer,
            WorkforceKind.DeliveryUnit => PartyRoleKind.DeliveryUnit,
            _ => PartyRoleKind.Employee
        };
    }

    internal static DateTimeOffset? UtcDate(DateOnly? value) => value is { } date ? new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null;
}
