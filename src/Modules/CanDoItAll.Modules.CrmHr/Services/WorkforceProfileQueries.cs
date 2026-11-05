using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

internal static class WorkforceProfileQueries {
    internal static async Task<WorkforceProfileWorkspaceModel?> ReadWorkspaceAsync(
        CrmHrDbContext dbContext,
        Guid partyId,
        CancellationToken cancellationToken) {
        var party = await dbContext.Set<Party>()
            .Select(item => new {
                item.Id,
                item.DisplayName,
                item.Summary,
                item.PartyType,
                item.LifecycleStatus,
                item.IsSensitive,
                item.LastChangedBy,
                item.UpdatedAtUtc
            })
            .SingleOrDefaultAsync(item => item.Id == partyId, cancellationToken);
        if (party is null || party.PartyType == PartyType.AiAgent) {
            return null;
        }

        var roles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => item.PartyId == partyId)
            .OrderBy(item => item.RoleKind)
            .Select(item => item.RoleKind)
            .ToListAsync(cancellationToken);
        var contactPoints = await dbContext.Set<PartyContactPoint>()
            .Where(item => item.PartyId == partyId && item.IsPublic)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => new CrmPartyContactValue(item.PartyId, item.ContactType, item.Value, item.IsPrimary))
            .ToListAsync(cancellationToken);
        var profile = await dbContext.Set<WorkforceProfile>()
            .SingleOrDefaultAsync(item => item.PartyId == partyId, cancellationToken);
        var relatedPartyIds = new[] { profile?.HomeUnitPartyId, profile?.ManagerPartyId }
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToList();
        var namesByPartyId = relatedPartyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<Party>()
                .Where(item => relatedPartyIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);
        var skillCatalog = await ReadCatalogAsync(dbContext, cancellationToken);
        var partySkills = (await ReadSkillsAsync(dbContext, [partyId], cancellationToken)).GetValueOrDefault(partyId) ?? [];

        return new WorkforceProfileWorkspaceModel(
            party.Id,
            party.DisplayName,
            party.Summary,
            party.PartyType,
            party.LifecycleStatus,
            party.IsSensitive,
            string.IsNullOrWhiteSpace(party.LastChangedBy) ? "crm-hr-ui" : party.LastChangedBy,
            party.UpdatedAtUtc,
            roles,
            party.IsSensitive ? string.Empty : PrimaryContact(contactPoints, PartyContactType.Email),
            party.IsSensitive ? string.Empty : PrimaryContact(contactPoints, PartyContactType.Phone),
            profile?.HomeUnitPartyId is Guid homeUnitPartyId ? namesByPartyId.GetValueOrDefault(homeUnitPartyId) ?? string.Empty : string.Empty,
            profile?.ManagerPartyId is Guid managerPartyId ? namesByPartyId.GetValueOrDefault(managerPartyId) ?? string.Empty : string.Empty,
            new WorkforceProfileEditorModel {
                Id = profile?.Id,
                Original = WorkforceEditValues.Profile(party.Id, party.PartyType, profile),
                PartyId = party.Id,
                WorkforceKind = WorkforceEditValues.DefaultKind(party.PartyType, roles, profile),
                EmployeeCode = profile?.EmployeeCode ?? string.Empty,
                JobTitle = profile?.JobTitle ?? string.Empty,
                Discipline = profile?.Discipline ?? string.Empty,
                Seniority = profile?.Seniority ?? string.Empty,
                HomeUnitPartyId = profile?.HomeUnitPartyId,
                ManagerPartyId = profile?.ManagerPartyId,
                StartDate = profile?.StartDateUtc is DateTimeOffset startDateUtc ? DateOnly.FromDateTime(startDateUtc.UtcDateTime) : null,
                EndDate = profile?.EndDateUtc is DateTimeOffset endDateUtc ? DateOnly.FromDateTime(endDateUtc.UtcDateTime) : null,
                Location = profile?.Location ?? string.Empty,
                TimeZone = profile?.TimeZone ?? string.Empty,
                InternalCostRate = profile?.InternalCostRate,
                ExternalBillingRate = profile?.ExternalBillingRate,
                RateUnit = profile?.RateUnit ?? ProjectResourceRateUnit.Hour,
                RateCurrencyCode = string.IsNullOrWhiteSpace(profile?.RateCurrencyCode) ? "USD" : profile.RateCurrencyCode,
                CapacityHoursPerWeek = profile?.CapacityHoursPerWeek ?? 40m,
                Status = string.IsNullOrWhiteSpace(profile?.Status) ? WorkforceEditValues.DefaultStatus(party.LifecycleStatus) : profile.Status,
                Notes = profile?.Notes ?? string.Empty,
                LastChangedBy = string.IsNullOrWhiteSpace(party.LastChangedBy) ? "crm-hr-ui" : party.LastChangedBy
            },
            skillCatalog,
            partySkills);
    }

    internal static async Task<(WorkforceProfileEditorModel Profile, IReadOnlyDictionary<Guid, string> Names)?> ReadCurrentAsync(
        CrmHrDbContext context, Guid partyId, CancellationToken cancellationToken) {
        var party = await context.Set<Party>().AsNoTracking().Where(row => row.Id == partyId)
            .Select(row => new { row.Id, row.PartyType, row.LifecycleStatus, row.LastChangedBy }).SingleOrDefaultAsync(cancellationToken);
        if (party is null || party.PartyType == PartyType.AiAgent) {
            return null;
        }
        var saved = await context.Set<WorkforceProfile>().AsNoTracking().SingleOrDefaultAsync(row => row.PartyId == partyId, cancellationToken);
        if (saved is null) {
            var roles = await context.Set<PartyRoleAssignment>().Where(row => row.PartyId == partyId).Select(row => row.RoleKind).ToListAsync(cancellationToken);
            return (new() {
                PartyId = partyId, WorkforceKind = WorkforceEditValues.DefaultKind(party.PartyType, roles, null),
                Status = WorkforceEditValues.DefaultStatus(party.LifecycleStatus), LastChangedBy = party.LastChangedBy,
                Original = WorkforceEditValues.Profile(partyId, party.PartyType, null)
            }, new Dictionary<Guid, string>());
        }
        var model = WorkforceEditValues.AcceptedProfile(new() { Id = party.Id, PartyType = party.PartyType, LastChangedBy = party.LastChangedBy }, saved);
        var ids = new Guid?[] { saved.HomeUnitPartyId, saved.ManagerPartyId }.OfType<Guid>().Distinct().ToArray();
        var names = await context.Set<Party>().AsNoTracking().Where(row => ids.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, row => row.DisplayName, cancellationToken);
        return (model, names);
    }

    internal static async Task<IReadOnlyList<SkillCatalogItemModel>> ReadCatalogAsync(CrmHrDbContext dbContext, CancellationToken cancellationToken) {
        return await dbContext.Set<SkillDefinition>()
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.Category)
            .ThenBy(item => item.Name)
            .Select(item => new SkillCatalogItemModel(item.Id, item.Name, item.Category, item.Description, item.IsActive))
            .ToListAsync(cancellationToken);
    }

    internal static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<PartySkillItemModel>>> ReadSkillsAsync(
        CrmHrDbContext dbContext,
        IReadOnlyList<Guid> partyIds,
        CancellationToken cancellationToken) {
        if (partyIds.Count == 0) {
            return new Dictionary<Guid, IReadOnlyList<PartySkillItemModel>>();
        }

        var items = await dbContext.Set<PartySkill>()
            .Where(item => partyIds.Contains(item.PartyId))
            .Join(
                dbContext.Set<SkillDefinition>(),
                partySkill => partySkill.SkillId,
                skill => skill.Id,
                (partySkill, skill) => new {
                    partySkill.PartyId,
                    partySkill.Id,
                    SkillId = skill.Id,
                    SkillName = skill.Name,
                    SkillCategory = skill.Category,
                    partySkill.Proficiency,
                    partySkill.YearsExperience,
                    partySkill.CertificationStatus,
                    partySkill.LastValidatedAtUtc,
                    partySkill.Notes
                })
            .OrderBy(item => item.SkillCategory)
            .ThenBy(item => item.SkillName)
            .ToListAsync(cancellationToken);

        return items
            .GroupBy(item => item.PartyId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PartySkillItemModel>)group.Select(item =>
                    new PartySkillItemModel(
                        item.Id,
                        item.SkillId,
                        item.SkillName,
                        item.SkillCategory,
                        item.Proficiency,
                        item.YearsExperience,
                        item.CertificationStatus,
                        WorkforceEditValues.Date(item.LastValidatedAtUtc),
                        item.Notes) { Original = new(item.Id, item.PartyId, item.SkillId, item.Proficiency,
                            item.YearsExperience, item.CertificationStatus, WorkforceEditValues.Date(item.LastValidatedAtUtc), item.Notes) }).ToList());
    }


    internal static string PrimaryContact(IEnumerable<CrmPartyContactValue> contacts, PartyContactType contactType) {
        return contacts
            .Where(item => item.ContactType == contactType)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => item.Value)
            .FirstOrDefault()
            ?? string.Empty;
    }
}
