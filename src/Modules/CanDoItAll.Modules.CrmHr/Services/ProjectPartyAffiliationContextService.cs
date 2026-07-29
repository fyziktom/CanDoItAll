using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public sealed record ProjectPartyAffiliationReference(
    Guid ReferenceId,
    Guid PartyId,
    PartyType PartyType,
    Guid? AffiliationId,
    bool IsSensitive);

public sealed record ProjectPartyAffiliationValidation(
    Guid PartyId,
    Guid? AffiliationId,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc);

public static class ProjectPartyAffiliationErrorCodes
{
    public const string NotFound =
        "crmhr.project-assignment.affiliation-not-found";
    public const string PartyMismatch =
        "crmhr.project-assignment.affiliation-party-mismatch";
    public const string DateMismatch =
        "crmhr.project-assignment.affiliation-date-mismatch";
}

public sealed class ProjectPartyAffiliationContextService(IClock clock)
{
    public async Task<IReadOnlyDictionary<Guid, ProjectPartyAffiliationContext>>
        LoadPartyContextsAsync(
            AppDbContext dbContext,
            IReadOnlyDictionary<Guid, PartyType> partyTypes,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(partyTypes);
        if (partyTypes.Count == 0)
        {
            return new Dictionary<Guid, ProjectPartyAffiliationContext>();
        }

        var partyIds = partyTypes.Keys.ToArray();
        var todayUtc = ToUtcDate(
            DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        var affiliations = await (
                from affiliation in dbContext
                    .Set<PartyOrganizationAffiliation>()
                    .AsNoTracking()
                join organization in dbContext.Set<Party>().AsNoTracking()
                    on affiliation.OrganizationPartyId equals organization.Id
                where partyIds.Contains(affiliation.PersonPartyId) &&
                      (!affiliation.ValidFromUtc.HasValue ||
                      affiliation.ValidFromUtc.Value <= todayUtc) &&
                      (!affiliation.ValidToUtc.HasValue ||
                       affiliation.ValidToUtc.Value >= todayUtc)
                select new AffiliationRow(
                    affiliation.Id,
                    affiliation.PersonPartyId,
                    affiliation.AffiliationKind,
                    affiliation.IsPrimary,
                    organization.DisplayName,
                    affiliation.JobTitle,
                    affiliation.ValidFromUtc))
            .ToListAsync(cancellationToken);

        var contexts = affiliations
            .GroupBy(item => item.PersonPartyId)
            .ToDictionary(
                group => group.Key,
                group => BuildAffiliationContext(group.ToArray()));

        var missingPartyIds = partyTypes
            .Where(item =>
                !contexts.ContainsKey(item.Key) &&
                item.Value is PartyType.Person or
                    PartyType.Organization or
                    PartyType.OrganizationUnit)
            .Select(item => item.Key)
            .ToArray();
        if (missingPartyIds.Length > 0)
        {
            var legacyProfiles = await (
                    from profile in dbContext.Set<WorkforceProfile>()
                        .AsNoTracking()
                    join homeUnit in dbContext.Set<Party>().AsNoTracking()
                        on profile.HomeUnitPartyId equals
                            (Guid?)homeUnit.Id into homeUnits
                    from homeUnit in homeUnits.DefaultIfEmpty()
                    where missingPartyIds.Contains(profile.PartyId)
                    select new LegacyProfileRow(
                        profile.PartyId,
                        profile.WorkforceKind,
                        homeUnit == null
                            ? string.Empty
                            : homeUnit.DisplayName,
                        profile.JobTitle))
                .ToListAsync(cancellationToken);
            foreach (var profile in legacyProfiles)
            {
                contexts.TryAdd(
                    profile.PartyId,
                    new ProjectPartyAffiliationContext(
                        null,
                        ResolveLegacyLabel(profile.WorkforceKind),
                        profile.OrganizationName,
                        profile.JobTitle,
                        string.Empty));
            }
        }

        foreach (var party in partyTypes)
        {
            if (contexts.ContainsKey(party.Key))
            {
                continue;
            }

            var fallback = party.Value switch
            {
                PartyType.Person => new ProjectPartyAffiliationContext(
                    null,
                    "External contact",
                    string.Empty,
                    string.Empty,
                    string.Empty),
                PartyType.OrganizationUnit => new ProjectPartyAffiliationContext(
                    null,
                    "Delivery unit",
                    string.Empty,
                    string.Empty,
                    string.Empty),
                _ => null
            };
            if (fallback is not null)
            {
                contexts[party.Key] = fallback;
            }
        }

        return contexts;
    }

    public async Task<IReadOnlyDictionary<Guid, ProjectPartyAffiliationContext>>
        LoadAssignmentContextsAsync(
            AppDbContext dbContext,
            IReadOnlyCollection<ProjectPartyAffiliationReference> references,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(references);
        if (references.Count == 0)
        {
            return new Dictionary<Guid, ProjectPartyAffiliationContext>();
        }

        var visibleReferences = references
            .Where(item => !item.IsSensitive)
            .ToArray();
        var partyTypes = visibleReferences
            .GroupBy(item => item.PartyId)
            .ToDictionary(group => group.Key, group => group.First().PartyType);
        var fallbackContexts = await LoadPartyContextsAsync(
            dbContext,
            partyTypes,
            cancellationToken);

        var selectedAffiliationIds = visibleReferences
            .Where(item => item.AffiliationId.HasValue)
            .Select(item => item.AffiliationId!.Value)
            .Distinct()
            .ToArray();
        var selectedContexts = selectedAffiliationIds.Length == 0
            ? new Dictionary<Guid, ProjectPartyAffiliationContext>()
            : await (
                    from affiliation in dbContext
                        .Set<PartyOrganizationAffiliation>()
                        .AsNoTracking()
                    join organization in dbContext.Set<Party>().AsNoTracking()
                        on affiliation.OrganizationPartyId equals organization.Id
                    where selectedAffiliationIds.Contains(affiliation.Id)
                    select new
                    {
                        affiliation.Id,
                        Context = new ProjectPartyAffiliationContext(
                            affiliation.Id,
                            ResolveLabel(affiliation.AffiliationKind),
                            organization.DisplayName,
                            affiliation.JobTitle,
                            string.Empty)
                    })
                .ToDictionaryAsync(
                    item => item.Id,
                    item => item.Context,
                    cancellationToken);

        var result = new Dictionary<Guid, ProjectPartyAffiliationContext>();
        foreach (var reference in visibleReferences)
        {
            if (reference.AffiliationId is Guid affiliationId &&
                selectedContexts.TryGetValue(affiliationId, out var selected))
            {
                result[reference.ReferenceId] = selected;
                continue;
            }

            if (fallbackContexts.TryGetValue(
                    reference.PartyId,
                    out var fallback))
            {
                result[reference.ReferenceId] = fallback;
            }
        }

        return result;
    }

    public async Task<Error?> ValidateAsync(
        AppDbContext dbContext,
        Guid partyId,
        Guid? affiliationId,
        DateTimeOffset? startsAtUtc,
        DateTimeOffset? endsAtUtc,
        CancellationToken cancellationToken = default)
    {
        return await ValidateAsync(
            dbContext,
            [
                new ProjectPartyAffiliationValidation(
                    partyId,
                    affiliationId,
                    startsAtUtc,
                    endsAtUtc)
            ],
            cancellationToken);
    }

    public async Task<Error?> ValidateAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<ProjectPartyAffiliationValidation> requests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(requests);
        var scopedRequests = requests
            .Where(item => item.AffiliationId.HasValue)
            .ToArray();
        if (scopedRequests.Length == 0)
        {
            return null;
        }

        var affiliationIds = scopedRequests
            .Select(item => item.AffiliationId!.Value)
            .Distinct()
            .ToArray();
        var affiliations = await dbContext
            .Set<PartyOrganizationAffiliation>()
            .AsNoTracking()
            .Where(item => affiliationIds.Contains(item.Id))
            .Select(item => new
            {
                item.Id,
                item.PersonPartyId,
                item.ValidFromUtc,
                item.ValidToUtc
            })
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var todayUtc = ToUtcDate(
            DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        foreach (var request in scopedRequests)
        {
            var affiliationId = request.AffiliationId!.Value;
            if (!affiliations.TryGetValue(
                    affiliationId,
                    out var affiliation))
            {
                return Error.Validation(
                    "The selected party affiliation was not found.",
                    ProjectPartyAffiliationErrorCodes.NotFound);
            }

            if (affiliation.PersonPartyId != request.PartyId)
            {
                return Error.Validation(
                    "The selected affiliation belongs to another party.",
                    ProjectPartyAffiliationErrorCodes.PartyMismatch);
            }

            var effectiveStartUtc = request.StartsAtUtc ?? todayUtc;
            var effectiveEndUtc = request.EndsAtUtc ?? effectiveStartUtc;
            if ((affiliation.ValidFromUtc.HasValue &&
                 affiliation.ValidFromUtc.Value > effectiveStartUtc) ||
                (affiliation.ValidToUtc.HasValue &&
                 affiliation.ValidToUtc.Value < effectiveEndUtc))
            {
                return Error.Validation(
                    "The selected affiliation does not cover the assignment dates.",
                    ProjectPartyAffiliationErrorCodes.DateMismatch);
            }
        }

        return null;
    }

    private static ProjectPartyAffiliationContext BuildAffiliationContext(
        IReadOnlyCollection<AffiliationRow> affiliations)
    {
        var ordered = affiliations
            .OrderByDescending(item => item.IsPrimary)
            .ThenByDescending(item => item.ValidFromUtc)
            .ThenBy(item => item.OrganizationName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .ToArray();
        var primary = ordered[0];
        var others = ordered
            .Skip(1)
            .Select(item => FormatAffiliation(item))
            .ToArray();
        return new ProjectPartyAffiliationContext(
            primary.Id,
            ResolveLabel(primary.Kind),
            primary.OrganizationName,
            primary.JobTitle,
            string.Join("; ", others));
    }

    private static string FormatAffiliation(AffiliationRow affiliation)
    {
        var organizationAndTitle = string.Join(
            " · ",
            new[] { affiliation.OrganizationName, affiliation.JobTitle }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(organizationAndTitle)
            ? ResolveLabel(affiliation.Kind)
            : $"{ResolveLabel(affiliation.Kind)} — {organizationAndTitle}";
    }

    private static string ResolveLabel(
        PartyOrganizationAffiliationKind kind)
    {
        return kind switch
        {
            PartyOrganizationAffiliationKind.ExternalContact =>
                "External contact",
            _ => kind.ToString()
        };
    }

    private static string ResolveLegacyLabel(WorkforceKind kind)
    {
        return kind switch
        {
            WorkforceKind.DeliveryUnit => "Delivery unit",
            _ => kind.ToString()
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

    private sealed record AffiliationRow(
        Guid Id,
        Guid PersonPartyId,
        PartyOrganizationAffiliationKind Kind,
        bool IsPrimary,
        string OrganizationName,
        string JobTitle,
        DateTimeOffset? ValidFromUtc);

    private sealed record LegacyProfileRow(
        Guid PartyId,
        WorkforceKind WorkforceKind,
        string OrganizationName,
        string JobTitle);
}
