using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public enum WorkforceRecordClassification
{
    Employee,
    Contractor,
    Freelancer,
    ExternalContact,
    DeliveryUnit
}

public static class WorkforceRecordQueryLimits
{
    public const int DefaultPageSize = 12;
    public const int MaximumPageSize = 100;
    public const int MaximumSearchLength = 200;
}

public sealed record WorkforceRecordQuery(
    string SearchText = "",
    WorkforceRecordClassification? Classification = null,
    PartyLifecycleStatus? LifecycleStatus = null,
    int PageIndex = 0,
    int PageSize = WorkforceRecordQueryLimits.DefaultPageSize,
    bool IncludeArchived = false);

public sealed record WorkforceRecordAffiliationSummaryModel(
    Guid Id,
    PartyOrganizationAffiliationKind AffiliationKind,
    Guid OrganizationPartyId,
    string OrganizationName,
    string JobTitle,
    bool IsPrimary,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    string DisplayText);

public sealed record WorkforceRecordQueryItem(
    Guid PartyId,
    string DisplayName,
    PartyType PartyType,
    PartyLifecycleStatus LifecycleStatus,
    bool IsSensitive,
    string Summary,
    WorkforceRecordClassification Classification,
    bool HasWorkforceProfile,
    DateTimeOffset UpdatedAtUtc,
    WorkforceRecordAffiliationSummaryModel? PrimaryAffiliation,
    string PrimaryAffiliationText,
    IReadOnlyList<WorkforceRecordAffiliationSummaryModel> OtherCurrentAffiliations);

public sealed record WorkforceRecordPage(
    IReadOnlyList<WorkforceRecordQueryItem> Items,
    int PageIndex,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static WorkforceRecordPage Empty(
        int pageSize = WorkforceRecordQueryLimits.DefaultPageSize)
        => new([], 0, pageSize, 0);
}

public interface IWorkforceRecordQueryService
{
    Task<WorkforceRecordPage> SearchAsync(
        WorkforceRecordQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class WorkforceRecordQueryService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    CanDoItAll.SharedKernel.IClock clock) : IWorkforceRecordQueryService
{
    public async Task<WorkforceRecordPage> SearchAsync(
        WorkforceRecordQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var normalized = Normalize(query);
        var todayUtc = ToUtcDate(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<Party> candidates = dbContext.Set<Party>()
            .AsNoTracking()
            .Where(party =>
                party.PartyType == PartyType.Person ||
                party.PartyType == PartyType.OrganizationUnit ||
                (party.PartyType == PartyType.Organization &&
                 (dbContext.Set<WorkforceProfile>().Any(profile =>
                      profile.PartyId == party.Id &&
                      profile.WorkforceKind == WorkforceKind.DeliveryUnit) ||
                  dbContext.Set<PartyRoleAssignment>().Any(role =>
                      role.PartyId == party.Id &&
                      role.RoleKind == PartyRoleKind.DeliveryUnit))));

        if (normalized.LifecycleStatus.HasValue)
        {
            candidates = candidates.Where(party =>
                party.LifecycleStatus == normalized.LifecycleStatus.Value);
        }
        else if (!normalized.IncludeArchived)
        {
            candidates = candidates.Where(party =>
                party.LifecycleStatus != PartyLifecycleStatus.Archived);
        }

        if (!string.IsNullOrEmpty(normalized.SearchText))
        {
            var searchPattern =
                $"%{EscapeLikePattern(normalized.SearchText)}%";
            candidates = candidates.Where(party =>
                EF.Functions.ILike(
                    party.DisplayName,
                    searchPattern,
                    "\\") ||
                (!party.IsSensitive &&
                 (EF.Functions.ILike(
                      party.ExternalCode,
                      searchPattern,
                      "\\") ||
                  EF.Functions.ILike(
                      party.Summary,
                      searchPattern,
                      "\\") ||
                  dbContext.Set<PartyOrganizationAffiliation>().Any(affiliation =>
                      affiliation.PersonPartyId == party.Id &&
                      (!affiliation.ValidFromUtc.HasValue ||
                       affiliation.ValidFromUtc.Value <= todayUtc) &&
                      (!affiliation.ValidToUtc.HasValue ||
                       affiliation.ValidToUtc.Value >= todayUtc) &&
                      (EF.Functions.ILike(
                           affiliation.JobTitle,
                           searchPattern,
                           "\\") ||
                       dbContext.Set<Party>().Any(organization =>
                           organization.Id == affiliation.OrganizationPartyId &&
                           EF.Functions.ILike(
                               organization.DisplayName,
                               searchPattern,
                               "\\")))) ||
                  dbContext.Set<WorkforceProfile>().Any(profile =>
                      profile.PartyId == party.Id &&
                      (EF.Functions.ILike(
                           profile.JobTitle,
                           searchPattern,
                           "\\") ||
                       EF.Functions.ILike(
                           profile.Discipline,
                           searchPattern,
                           "\\") ||
                       EF.Functions.ILike(
                           profile.Location,
                           searchPattern,
                           "\\"))))));
        }

        if (normalized.Classification.HasValue)
        {
            candidates = ApplyClassificationFilter(
                dbContext,
                candidates,
                normalized.Classification.Value,
                todayUtc);
        }

        var totalCount = await candidates.CountAsync(cancellationToken);
        var pageRows = await candidates
            .OrderBy(party => party.DisplayName)
            .ThenBy(party => party.Id)
            .Skip(normalized.PageIndex * normalized.PageSize)
            .Take(normalized.PageSize)
            .Select(party => new WorkforcePartyRow(
                party.Id,
                party.DisplayName,
                party.PartyType,
                party.LifecycleStatus,
                party.IsSensitive,
                party.Summary,
                party.UpdatedAtUtc,
                dbContext.Set<PartyRoleAssignment>().Any(role =>
                    role.PartyId == party.Id &&
                    role.RoleKind == PartyRoleKind.DeliveryUnit)))
            .ToListAsync(cancellationToken);
        if (pageRows.Count == 0)
        {
            return new WorkforceRecordPage(
                [],
                normalized.PageIndex,
                normalized.PageSize,
                totalCount);
        }

        var partyIds = pageRows
            .Select(party => party.PartyId)
            .ToArray();
        var affiliationRows = await QueryCurrentAffiliations(dbContext, partyIds, todayUtc)
            .ToListAsync(cancellationToken);
        var profileRows = await QueryProfiles(dbContext, partyIds)
            .ToListAsync(cancellationToken);
        var relationshipRows = await QueryCurrentOrganizationRelationships(
                dbContext,
                partyIds,
                todayUtc)
            .ToListAsync(cancellationToken);

        var affiliationsByPerson = affiliationRows
            .GroupBy(item => item.PersonPartyId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.IsPrimary)
                    .ThenByDescending(item => item.ValidFromUtc)
                    .ThenByDescending(item => item.UpdatedAtUtc)
                    .ThenBy(item => item.Id)
                    .ToList());
        var profilesByParty = profileRows.ToDictionary(item => item.PartyId);
        var relationshipsByPerson = relationshipRows
            .GroupBy(item => item.PersonPartyId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.IsPrimary)
                    .ThenByDescending(item => item.StartDateUtc)
                    .ThenBy(item => item.OrganizationName)
                    .ThenBy(item => item.RelationshipId)
                    .First());

        var items = pageRows
            .Select(party =>
            {
                affiliationsByPerson.TryGetValue(
                    party.PartyId,
                    out var currentAffiliations);
                profilesByParty.TryGetValue(party.PartyId, out var profile);
                relationshipsByPerson.TryGetValue(
                    party.PartyId,
                    out var relationship);
                var selectedAffiliation = currentAffiliations?.FirstOrDefault();
                var classification = WorkforceRecordClassificationPolicy.Resolve(
                    selectedAffiliation?.AffiliationKind,
                    profile?.WorkforceKind,
                    party.PartyType,
                    party.HasDeliveryUnitRole);
                var primaryAffiliation = selectedAffiliation is null
                    ? null
                    : MapAffiliation(selectedAffiliation);
                var otherAffiliations = currentAffiliations is null
                    ? []
                    : currentAffiliations
                        .Skip(1)
                        .Select(MapAffiliation)
                        .ToList();

                return new WorkforceRecordQueryItem(
                    party.PartyId,
                    party.DisplayName,
                    party.PartyType,
                    party.LifecycleStatus,
                    party.IsSensitive,
                    party.IsSensitive ? string.Empty : party.Summary,
                    classification,
                    profile is not null,
                    party.UpdatedAtUtc,
                    primaryAffiliation,
                    BuildPrimaryAffiliationText(
                        primaryAffiliation,
                        profile,
                        relationship,
                        classification),
                    otherAffiliations);
            })
            .ToList();

        return new WorkforceRecordPage(
            items,
            normalized.PageIndex,
            normalized.PageSize,
            totalCount);
    }

    private static string EscapeLikePattern(string value)
        => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static IQueryable<Party> ApplyClassificationFilter(
        AppDbContext dbContext,
        IQueryable<Party> candidates,
        WorkforceRecordClassification classification,
        DateTimeOffset todayUtc)
    {
        if (classification == WorkforceRecordClassification.DeliveryUnit)
        {
            return candidates.Where(party =>
                (party.PartyType == PartyType.Person &&
                 !dbContext.Set<PartyOrganizationAffiliation>().Any(affiliation =>
                     affiliation.PersonPartyId == party.Id &&
                     (!affiliation.ValidFromUtc.HasValue ||
                      affiliation.ValidFromUtc.Value <= todayUtc) &&
                     (!affiliation.ValidToUtc.HasValue ||
                      affiliation.ValidToUtc.Value >= todayUtc)) &&
                 dbContext.Set<WorkforceProfile>().Any(profile =>
                     profile.PartyId == party.Id &&
                     profile.WorkforceKind == WorkforceKind.DeliveryUnit)) ||
                (party.PartyType != PartyType.Person &&
                 (party.PartyType == PartyType.OrganizationUnit ||
                  dbContext.Set<WorkforceProfile>().Any(profile =>
                      profile.PartyId == party.Id &&
                      profile.WorkforceKind == WorkforceKind.DeliveryUnit) ||
                  dbContext.Set<PartyRoleAssignment>().Any(role =>
                      role.PartyId == party.Id &&
                      role.RoleKind == PartyRoleKind.DeliveryUnit))));
        }

        var affiliationKind = classification switch
        {
            WorkforceRecordClassification.Employee =>
                PartyOrganizationAffiliationKind.Employee,
            WorkforceRecordClassification.Contractor =>
                PartyOrganizationAffiliationKind.Contractor,
            WorkforceRecordClassification.Freelancer =>
                PartyOrganizationAffiliationKind.Freelancer,
            WorkforceRecordClassification.ExternalContact =>
                PartyOrganizationAffiliationKind.ExternalContact,
            _ => throw new ArgumentOutOfRangeException(
                nameof(classification),
                classification,
                "Unsupported workforce classification.")
        };

        var selectedAffiliationMatches = candidates.Where(party =>
            party.PartyType == PartyType.Person &&
            dbContext.Set<PartyOrganizationAffiliation>()
                .Where(affiliation =>
                    affiliation.PersonPartyId == party.Id &&
                    (!affiliation.ValidFromUtc.HasValue ||
                     affiliation.ValidFromUtc.Value <= todayUtc) &&
                    (!affiliation.ValidToUtc.HasValue ||
                     affiliation.ValidToUtc.Value >= todayUtc))
                .OrderByDescending(affiliation => affiliation.IsPrimary)
                .ThenByDescending(affiliation => affiliation.ValidFromUtc)
                .ThenByDescending(affiliation => affiliation.UpdatedAtUtc)
                .ThenBy(affiliation => affiliation.Id)
                .Select(affiliation =>
                    (PartyOrganizationAffiliationKind?)affiliation.AffiliationKind)
                .FirstOrDefault() == affiliationKind);

        if (classification == WorkforceRecordClassification.ExternalContact)
        {
            return candidates.Where(party =>
                selectedAffiliationMatches.Any(match => match.Id == party.Id) ||
                (party.PartyType == PartyType.Person &&
                 !dbContext.Set<PartyOrganizationAffiliation>().Any(affiliation =>
                     affiliation.PersonPartyId == party.Id &&
                     (!affiliation.ValidFromUtc.HasValue ||
                      affiliation.ValidFromUtc.Value <= todayUtc) &&
                     (!affiliation.ValidToUtc.HasValue ||
                      affiliation.ValidToUtc.Value >= todayUtc)) &&
                 !dbContext.Set<WorkforceProfile>().Any(profile =>
                     profile.PartyId == party.Id)));
        }

        var workforceKind = classification switch
        {
            WorkforceRecordClassification.Employee => WorkforceKind.Employee,
            WorkforceRecordClassification.Contractor => WorkforceKind.Contractor,
            WorkforceRecordClassification.Freelancer => WorkforceKind.Freelancer,
            _ => throw new ArgumentOutOfRangeException(
                nameof(classification),
                classification,
                "Unsupported workforce classification.")
        };
        return candidates.Where(party =>
            selectedAffiliationMatches.Any(match => match.Id == party.Id) ||
            (party.PartyType == PartyType.Person &&
             !dbContext.Set<PartyOrganizationAffiliation>().Any(affiliation =>
                 affiliation.PersonPartyId == party.Id &&
                 (!affiliation.ValidFromUtc.HasValue ||
                  affiliation.ValidFromUtc.Value <= todayUtc) &&
                 (!affiliation.ValidToUtc.HasValue ||
                  affiliation.ValidToUtc.Value >= todayUtc)) &&
             dbContext.Set<WorkforceProfile>().Any(profile =>
                 profile.PartyId == party.Id &&
                 profile.WorkforceKind == workforceKind)));
    }

    private static IQueryable<WorkforceAffiliationRow> QueryCurrentAffiliations(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> partyIds,
        DateTimeOffset todayUtc)
    {
        return
            from affiliation in dbContext.Set<PartyOrganizationAffiliation>().AsNoTracking()
            where partyIds.Contains(affiliation.PersonPartyId) &&
                  (!affiliation.ValidFromUtc.HasValue ||
                   affiliation.ValidFromUtc.Value <= todayUtc) &&
                  (!affiliation.ValidToUtc.HasValue ||
                   affiliation.ValidToUtc.Value >= todayUtc)
            join organization in dbContext.Set<Party>().AsNoTracking()
                on affiliation.OrganizationPartyId equals organization.Id
            select new WorkforceAffiliationRow(
                affiliation.Id,
                affiliation.PersonPartyId,
                affiliation.AffiliationKind,
                affiliation.OrganizationPartyId,
                organization.DisplayName,
                affiliation.JobTitle,
                affiliation.IsPrimary,
                affiliation.ValidFromUtc,
                affiliation.ValidToUtc,
                affiliation.UpdatedAtUtc);
    }

    private static IQueryable<WorkforceProfileRow> QueryProfiles(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> partyIds)
    {
        return
            from profile in dbContext.Set<WorkforceProfile>().AsNoTracking()
            where partyIds.Contains(profile.PartyId)
            join homeUnitCandidate in dbContext.Set<Party>().AsNoTracking()
                on profile.HomeUnitPartyId equals (Guid?)homeUnitCandidate.Id
                into homeUnitCandidates
            from homeUnit in homeUnitCandidates.DefaultIfEmpty()
            select new WorkforceProfileRow(
                profile.PartyId,
                profile.WorkforceKind,
                profile.JobTitle,
                profile.HomeUnitPartyId,
                homeUnit == null ? string.Empty : homeUnit.DisplayName);
    }

    private static IQueryable<WorkforceRelationshipRow> QueryCurrentOrganizationRelationships(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> partyIds,
        DateTimeOffset todayUtc)
    {
        return
            from relationship in dbContext.Set<PartyRelationship>().AsNoTracking()
            where (relationship.RelationshipKind == PartyRelationshipKind.MemberOf ||
                   relationship.RelationshipKind == PartyRelationshipKind.Represents) &&
                  (!relationship.StartDateUtc.HasValue ||
                   relationship.StartDateUtc.Value <= todayUtc) &&
                  (!relationship.EndDateUtc.HasValue ||
                   relationship.EndDateUtc.Value >= todayUtc) &&
                  (partyIds.Contains(relationship.SourcePartyId) ||
                   partyIds.Contains(relationship.TargetPartyId))
            join source in dbContext.Set<Party>().AsNoTracking()
                on relationship.SourcePartyId equals source.Id
            join target in dbContext.Set<Party>().AsNoTracking()
                on relationship.TargetPartyId equals target.Id
            where (source.PartyType == PartyType.Person &&
                   target.PartyType == PartyType.Organization) ||
                  (target.PartyType == PartyType.Person &&
                   source.PartyType == PartyType.Organization)
            select new WorkforceRelationshipRow(
                relationship.Id,
                source.PartyType == PartyType.Person ? source.Id : target.Id,
                source.PartyType == PartyType.Organization
                    ? source.DisplayName
                    : target.DisplayName,
                relationship.IsPrimary,
                relationship.StartDateUtc);
    }

    private static WorkforceRecordAffiliationSummaryModel MapAffiliation(
        WorkforceAffiliationRow affiliation)
    {
        var validFrom = ToDateOnly(affiliation.ValidFromUtc);
        var validTo = ToDateOnly(affiliation.ValidToUtc);
        return new WorkforceRecordAffiliationSummaryModel(
            affiliation.Id,
            affiliation.AffiliationKind,
            affiliation.OrganizationPartyId,
            affiliation.OrganizationName,
            affiliation.JobTitle,
            affiliation.IsPrimary,
            validFrom,
            validTo,
            FormatOrganizationAndTitle(
                affiliation.OrganizationName,
                affiliation.JobTitle));
    }

    private static string BuildPrimaryAffiliationText(
        WorkforceRecordAffiliationSummaryModel? affiliation,
        WorkforceProfileRow? profile,
        WorkforceRelationshipRow? relationship,
        WorkforceRecordClassification classification)
    {
        if (affiliation is not null)
        {
            return affiliation.DisplayText;
        }

        if (profile is not null)
        {
            var legacyText = FormatOrganizationAndTitle(
                profile.HomeUnitName,
                profile.JobTitle);
            if (!string.IsNullOrWhiteSpace(legacyText))
            {
                return legacyText;
            }
        }

        if (relationship is not null)
        {
            return $"Related to {relationship.OrganizationName}";
        }

        return classification == WorkforceRecordClassification.DeliveryUnit
            ? "Delivery unit"
            : "No current organization affiliation";
    }

    private static string FormatOrganizationAndTitle(
        string organizationName,
        string jobTitle)
    {
        var organization = organizationName?.Trim() ?? string.Empty;
        var title = jobTitle?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(organization))
        {
            return title;
        }

        return string.IsNullOrEmpty(title)
            ? organization
            : $"{organization} — {title}";
    }

    private static WorkforceRecordQuery Normalize(WorkforceRecordQuery query)
    {
        if (query.PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "Workforce record page index cannot be negative.");
        }

        if (query.PageSize is < 1 or > WorkforceRecordQueryLimits.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageSize,
                $"Workforce record page size must be between 1 and " +
                $"{WorkforceRecordQueryLimits.MaximumPageSize}.");
        }

        if (query.PageIndex > int.MaxValue / query.PageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "Workforce record page offset is too large.");
        }

        var searchText = query.SearchText?.Trim() ?? string.Empty;
        if (searchText.Length > WorkforceRecordQueryLimits.MaximumSearchLength)
        {
            throw new ArgumentException(
                $"Workforce record search cannot exceed " +
                $"{WorkforceRecordQueryLimits.MaximumSearchLength} characters.",
                nameof(query));
        }

        if (query.Classification.HasValue &&
            !Enum.IsDefined(query.Classification.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.Classification,
                "Workforce record classification must be supported.");
        }

        if (query.LifecycleStatus.HasValue &&
            !Enum.IsDefined(query.LifecycleStatus.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.LifecycleStatus,
                "Party lifecycle status must be supported.");
        }

        return query with
        {
            SearchText = searchText
        };
    }

    private static DateTimeOffset ToUtcDate(DateOnly value)
        => new(
            value.Year,
            value.Month,
            value.Day,
            0,
            0,
            0,
            TimeSpan.Zero);

    private static DateOnly? ToDateOnly(DateTimeOffset? value)
        => value.HasValue
            ? DateOnly.FromDateTime(value.Value.UtcDateTime)
            : null;

    private sealed record WorkforcePartyRow(
        Guid PartyId,
        string DisplayName,
        PartyType PartyType,
        PartyLifecycleStatus LifecycleStatus,
        bool IsSensitive,
        string Summary,
        DateTimeOffset UpdatedAtUtc,
        bool HasDeliveryUnitRole);

    private sealed record WorkforceAffiliationRow(
        Guid Id,
        Guid PersonPartyId,
        PartyOrganizationAffiliationKind AffiliationKind,
        Guid OrganizationPartyId,
        string OrganizationName,
        string JobTitle,
        bool IsPrimary,
        DateTimeOffset? ValidFromUtc,
        DateTimeOffset? ValidToUtc,
        DateTimeOffset UpdatedAtUtc);

    private sealed record WorkforceProfileRow(
        Guid PartyId,
        WorkforceKind WorkforceKind,
        string JobTitle,
        Guid? HomeUnitPartyId,
        string HomeUnitName);

    private sealed record WorkforceRelationshipRow(
        Guid RelationshipId,
        Guid PersonPartyId,
        string OrganizationName,
        bool IsPrimary,
        DateTimeOffset? StartDateUtc);
}

internal static class WorkforceRecordClassificationPolicy
{
    public static WorkforceRecordClassification Resolve(
        PartyOrganizationAffiliationKind? currentAffiliationKind,
        WorkforceKind? legacyWorkforceKind,
        PartyType partyType,
        bool hasDeliveryUnitRole)
    {
        if (currentAffiliationKind.HasValue)
        {
            return currentAffiliationKind.Value switch
            {
                PartyOrganizationAffiliationKind.Employee =>
                    WorkforceRecordClassification.Employee,
                PartyOrganizationAffiliationKind.Contractor =>
                    WorkforceRecordClassification.Contractor,
                PartyOrganizationAffiliationKind.Freelancer =>
                    WorkforceRecordClassification.Freelancer,
                PartyOrganizationAffiliationKind.ExternalContact =>
                    WorkforceRecordClassification.ExternalContact,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(currentAffiliationKind),
                    currentAffiliationKind,
                    "Unsupported organization affiliation kind.")
            };
        }

        if (partyType == PartyType.Person && legacyWorkforceKind.HasValue)
        {
            return legacyWorkforceKind.Value switch
            {
                WorkforceKind.Employee => WorkforceRecordClassification.Employee,
                WorkforceKind.Contractor => WorkforceRecordClassification.Contractor,
                WorkforceKind.Freelancer => WorkforceRecordClassification.Freelancer,
                WorkforceKind.DeliveryUnit => WorkforceRecordClassification.DeliveryUnit,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(legacyWorkforceKind),
                    legacyWorkforceKind,
                    "Unsupported legacy workforce kind.")
            };
        }

        if (partyType == PartyType.OrganizationUnit ||
            (partyType == PartyType.Organization &&
             (legacyWorkforceKind == WorkforceKind.DeliveryUnit ||
              hasDeliveryUnitRole)))
        {
            return WorkforceRecordClassification.DeliveryUnit;
        }

        return WorkforceRecordClassification.ExternalContact;
    }
}
