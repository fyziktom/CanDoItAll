using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public static class PartyOrganizationAffiliationLimits
{
    public const int MaximumAffiliationsPerPerson = 100;
    public const int MaximumActorLength = 160;
    public const int MaximumJobTitleLength = 160;
    public const int MaximumEmployeeCodeLength = 80;
}

public sealed class PartyOrganizationAffiliationEditorModel
{
    public Guid? Id { get; set; }
    public Guid PersonPartyId { get; set; }
    public Guid OrganizationPartyId { get; set; }
    public PartyOrganizationAffiliationKind AffiliationKind { get; set; }
        = PartyOrganizationAffiliationKind.ExternalContact;
    public bool IsPrimary { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public Guid? OrganizationUnitPartyId { get; set; }
    public Guid? ManagerPartyId { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTimeOffset? ExpectedUpdatedAtUtc { get; set; }
}

public sealed record PartyOrganizationAffiliationListItemModel(
    Guid Id,
    Guid PersonPartyId,
    string PersonDisplayName,
    Guid OrganizationPartyId,
    string OrganizationDisplayName,
    PartyOrganizationAffiliationKind AffiliationKind,
    bool IsPrimary,
    string JobTitle,
    string EmployeeCode,
    Guid? OrganizationUnitPartyId,
    string OrganizationUnitDisplayName,
    Guid? ManagerPartyId,
    string ManagerDisplayName,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    string Notes,
    string LastChangedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool IsCurrent);

public interface IPartyOrganizationAffiliationService
{
    Task<IReadOnlyList<PartyOrganizationAffiliationListItemModel>> ListAsync(
        Guid personPartyId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>> ReplaceAsync(
        Guid personPartyId,
        IReadOnlyCollection<PartyOrganizationAffiliationEditorModel> affiliations,
        string actor,
        CancellationToken cancellationToken = default);

    Task<Result<PartyOrganizationAffiliationListItemModel>> UpsertAsync(
        PartyOrganizationAffiliationEditorModel affiliation,
        string actor,
        DateTimeOffset? expectedUpdatedAtUtc = null,
        CancellationToken cancellationToken = default);
}

public sealed class PartyOrganizationAffiliationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock) : IPartyOrganizationAffiliationService
{
    public async Task<IReadOnlyList<PartyOrganizationAffiliationListItemModel>> ListAsync(
        Guid personPartyId,
        CancellationToken cancellationToken = default)
    {
        if (personPartyId == Guid.Empty)
        {
            throw new ArgumentException(
                "A person party identifier is required.",
                nameof(personPartyId));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await QueryListRows(dbContext, personPartyId)
            .Take(PartyOrganizationAffiliationLimits.MaximumAffiliationsPerPerson + 1)
            .ToListAsync(cancellationToken);
        if (rows.Count > PartyOrganizationAffiliationLimits.MaximumAffiliationsPerPerson)
        {
            throw new InvalidOperationException(
                $"Party '{personPartyId}' exceeds the supported affiliation limit of " +
                $"{PartyOrganizationAffiliationLimits.MaximumAffiliationsPerPerson}.");
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return rows
            .Select(row => MapListItem(row, today))
            .ToList();
    }

    public async Task<Result<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>> ReplaceAsync(
        Guid personPartyId,
        IReadOnlyCollection<PartyOrganizationAffiliationEditorModel> affiliations,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(affiliations);
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var errors = PartyOrganizationAffiliationPolicy.ValidateReplacement(
            personPartyId,
            affiliations,
            actor,
            today);
        if (errors.Count > 0)
        {
            return Result<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>.Failure(errors);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var endpointValidation = await ValidateEndpointsAsync(
            dbContext,
            personPartyId,
            affiliations,
            actor,
            cancellationToken);
        if (endpointValidation.Errors.Count > 0)
        {
            return Result<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>.Failure(
                endpointValidation.Errors);
        }

        var existing = await dbContext.Set<PartyOrganizationAffiliation>()
            .Where(item => item.PersonPartyId == personPartyId)
            .ToListAsync(cancellationToken);
        var existingById = existing.ToDictionary(item => item.Id);
        var submittedIds = affiliations
            .Where(item => item.Id.HasValue)
            .Select(item => item.Id!.Value)
            .ToHashSet();
        var missingSubmittedIds = submittedIds
            .Where(id => !existingById.ContainsKey(id))
            .ToList();
        if (missingSubmittedIds.Count > 0)
        {
            return Result<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>.Failure(
                Error.Validation(
                    "One or more affiliations no longer exist for the selected person.",
                    "crmhr.affiliation.replacement-id-invalid"));
        }

        var missingVersions = affiliations
            .Where(item => item.Id.HasValue && !item.ExpectedUpdatedAtUtc.HasValue)
            .Select(item => item.Id!.Value)
            .ToList();
        if (missingVersions.Count > 0)
        {
            return Result<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>.Failure(
                Error.Validation(
                    "Reload affiliations before saving because one or more concurrency " +
                    "versions are missing.",
                    "crmhr.affiliation.concurrency-version-required"));
        }

        var staleVersions = affiliations
            .Where(item =>
                item.Id is Guid id &&
                item.ExpectedUpdatedAtUtc.HasValue &&
                existingById[id].UpdatedAtUtc != item.ExpectedUpdatedAtUtc.Value)
            .Select(item => item.Id!.Value)
            .ToList();
        if (staleVersions.Count > 0)
        {
            return Result<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>.Failure(
                Error.Failure(
                    "One or more affiliations changed after they were loaded.",
                    "crmhr.affiliation.concurrency-conflict"));
        }

        var omittedExistingIds = existing
            .Where(item => !submittedIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToList();
        if (omittedExistingIds.Count > 0)
        {
            return Result<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>.Failure(
                Error.Validation(
                    "Existing affiliations cannot be removed by replacement. End-date the " +
                    "affiliation to preserve its audited history.",
                    "crmhr.affiliation.historical-removal-not-allowed"));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var requestedPrimaryId = affiliations
                .SingleOrDefault(item => item.IsPrimary)
                ?.Id;
            var primariesToClear = existing
                .Where(item => item.IsPrimary && item.Id != requestedPrimaryId)
                .ToList();
            if (primariesToClear.Count > 0)
            {
                foreach (var primary in primariesToClear)
                {
                    primary.IsPrimary = false;
                    primary.LastChangedBy = endpointValidation.Actor;
                    primary.UpdatedAtUtc = now;
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var replacementEntities = new List<PartyOrganizationAffiliation>(
                affiliations.Count);
            foreach (var editor in affiliations)
            {
                var entity = editor.Id is Guid id
                    ? existingById[id]
                    : new PartyOrganizationAffiliation
                    {
                        PersonPartyId = personPartyId,
                        CreatedAtUtc = now
                    };
                if (!editor.Id.HasValue)
                {
                    dbContext.Set<PartyOrganizationAffiliation>().Add(entity);
                }

                ApplyEditor(entity, editor, endpointValidation.Actor, now);
                replacementEntities.Add(entity);
            }

            CrmHrAuditWriter.AddEntry(
                dbContext,
                nameof(PartyOrganizationAffiliation),
                personPartyId,
                "PartyOrganizationAffiliationsReplaced",
                $"Replaced organization affiliations for '{endpointValidation.PersonDisplayName}'.",
                new
                {
                    PersonPartyId = personPartyId,
                    AffiliationCount = affiliations.Count,
                    AffiliationIds = replacementEntities
                        .Select(item => item.Id)
                        .ToArray(),
                    PrimaryAffiliationId = replacementEntities
                        .SingleOrDefault(item => item.IsPrimary)
                        ?.Id
                },
                endpointValidation.Actor,
                endpointValidation.PersonIsSensitive,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>.Failure(
                Error.Failure(
                    "The affiliation set changed concurrently or conflicts with a database constraint.",
                    "crmhr.affiliation.persistence-conflict"));
        }

        var saved = await ListAsync(personPartyId, cancellationToken);
        return Result<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>.Success(saved);
    }

    public async Task<Result<PartyOrganizationAffiliationListItemModel>> UpsertAsync(
        PartyOrganizationAffiliationEditorModel affiliation,
        string actor,
        DateTimeOffset? expectedUpdatedAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(affiliation);
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var errors = PartyOrganizationAffiliationPolicy.ValidateSingle(
            affiliation,
            actor,
            today);
        if (errors.Count > 0)
        {
            return Result<PartyOrganizationAffiliationListItemModel>.Failure(errors);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var endpointValidation = await ValidateEndpointsAsync(
            dbContext,
            affiliation.PersonPartyId,
            [affiliation],
            actor,
            cancellationToken);
        if (endpointValidation.Errors.Count > 0)
        {
            return Result<PartyOrganizationAffiliationListItemModel>.Failure(
                endpointValidation.Errors);
        }

        PartyOrganizationAffiliation? entity = null;
        if (affiliation.Id is Guid affiliationId)
        {
            entity = await dbContext.Set<PartyOrganizationAffiliation>()
                .SingleOrDefaultAsync(item => item.Id == affiliationId, cancellationToken);
            if (entity is null)
            {
                return Result<PartyOrganizationAffiliationListItemModel>.Failure(
                    Error.Failure(
                        "The selected affiliation could not be found.",
                        "crmhr.affiliation.not-found"));
            }

            if (entity.PersonPartyId != affiliation.PersonPartyId)
            {
                return Result<PartyOrganizationAffiliationListItemModel>.Failure(
                    Error.Validation(
                        "An affiliation cannot be moved to a different person.",
                        "crmhr.affiliation.person-immutable"));
            }

            var expectedVersion = expectedUpdatedAtUtc ??
                                  affiliation.ExpectedUpdatedAtUtc;
            if (expectedVersion.HasValue &&
                entity.UpdatedAtUtc != expectedVersion.Value)
            {
                return Result<PartyOrganizationAffiliationListItemModel>.Failure(
                    Error.Failure(
                        "The affiliation changed after it was loaded.",
                        "crmhr.affiliation.concurrency-conflict"));
            }
        }
        else
        {
            var existingCount = await dbContext
                .Set<PartyOrganizationAffiliation>()
                .AsNoTracking()
                .CountAsync(
                    item => item.PersonPartyId == affiliation.PersonPartyId,
                    cancellationToken);
            if (existingCount >=
                PartyOrganizationAffiliationLimits.MaximumAffiliationsPerPerson)
            {
                return Result<PartyOrganizationAffiliationListItemModel>.Failure(
                    Error.Validation(
                        $"A person cannot have more than " +
                        $"{PartyOrganizationAffiliationLimits.MaximumAffiliationsPerPerson} affiliations.",
                        "crmhr.affiliation.limit-exceeded"));
            }
        }

        var validFromUtc = ToUtcDate(affiliation.ValidFrom);
        var validToUtc = ToUtcDate(affiliation.ValidTo);
        var duplicateExists = await dbContext.Set<PartyOrganizationAffiliation>()
            .AsNoTracking()
            .AnyAsync(item =>
                    item.Id != affiliation.Id &&
                    item.PersonPartyId == affiliation.PersonPartyId &&
                    item.OrganizationPartyId == affiliation.OrganizationPartyId &&
                    item.AffiliationKind == affiliation.AffiliationKind &&
                    item.ValidFromUtc == validFromUtc &&
                    item.ValidToUtc == validToUtc,
                cancellationToken);
        if (duplicateExists)
        {
            return Result<PartyOrganizationAffiliationListItemModel>.Failure(
                Error.Validation(
                    "The same person, organization, kind, and effective interval already exists.",
                    "crmhr.affiliation.duplicate"));
        }

        var isNew = entity is null;
        entity ??= new PartyOrganizationAffiliation
        {
            PersonPartyId = affiliation.PersonPartyId,
            CreatedAtUtc = now
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (affiliation.IsPrimary)
            {
                var currentPrimaries = await dbContext.Set<PartyOrganizationAffiliation>()
                    .Where(item =>
                        item.PersonPartyId == affiliation.PersonPartyId &&
                        item.IsPrimary &&
                        item.Id != entity.Id)
                    .ToListAsync(cancellationToken);
                if (currentPrimaries.Count > 0)
                {
                    foreach (var currentPrimary in currentPrimaries)
                    {
                        currentPrimary.IsPrimary = false;
                        currentPrimary.LastChangedBy = endpointValidation.Actor;
                        currentPrimary.UpdatedAtUtc = now;
                    }

                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            ApplyEditor(entity, affiliation, endpointValidation.Actor, now);
            if (isNew)
            {
                dbContext.Set<PartyOrganizationAffiliation>().Add(entity);
            }

            CrmHrAuditWriter.AddEntry(
                dbContext,
                nameof(PartyOrganizationAffiliation),
                entity.PersonPartyId,
                isNew
                    ? "PartyOrganizationAffiliationCreated"
                    : "PartyOrganizationAffiliationUpdated",
                $"{(isNew ? "Created" : "Updated")} an organization affiliation for " +
                $"'{endpointValidation.PersonDisplayName}'.",
                new
                {
                    entity.PersonPartyId,
                    entity.OrganizationPartyId,
                    entity.OrganizationUnitPartyId,
                    entity.ManagerPartyId,
                    entity.AffiliationKind,
                    entity.IsPrimary,
                    entity.ValidFromUtc,
                    entity.ValidToUtc
                },
                endpointValidation.Actor,
                endpointValidation.PersonIsSensitive,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PartyOrganizationAffiliationListItemModel>.Failure(
                Error.Failure(
                    "The affiliation changed concurrently or conflicts with a database constraint.",
                    "crmhr.affiliation.persistence-conflict"));
        }

        var saved = (await ListAsync(affiliation.PersonPartyId, cancellationToken))
            .Single(item => item.Id == entity.Id);
        return Result<PartyOrganizationAffiliationListItemModel>.Success(saved);
    }

    private static IQueryable<AffiliationListRow> QueryListRows(
        AppDbContext dbContext,
        Guid personPartyId)
    {
        return
            from affiliation in dbContext.Set<PartyOrganizationAffiliation>().AsNoTracking()
            where affiliation.PersonPartyId == personPartyId
            join person in dbContext.Set<Party>().AsNoTracking()
                on affiliation.PersonPartyId equals person.Id
            join organization in dbContext.Set<Party>().AsNoTracking()
                on affiliation.OrganizationPartyId equals organization.Id
            join unitCandidate in dbContext.Set<Party>().AsNoTracking()
                on affiliation.OrganizationUnitPartyId equals (Guid?)unitCandidate.Id into unitCandidates
            from unit in unitCandidates.DefaultIfEmpty()
            join managerCandidate in dbContext.Set<Party>().AsNoTracking()
                on affiliation.ManagerPartyId equals (Guid?)managerCandidate.Id into managerCandidates
            from manager in managerCandidates.DefaultIfEmpty()
            orderby affiliation.IsPrimary descending,
                affiliation.ValidToUtc == null descending,
                affiliation.ValidFromUtc descending,
                organization.DisplayName,
                affiliation.Id
            select new AffiliationListRow(
                affiliation.Id,
                affiliation.PersonPartyId,
                person.DisplayName,
                affiliation.OrganizationPartyId,
                organization.DisplayName,
                affiliation.AffiliationKind,
                affiliation.IsPrimary,
                affiliation.JobTitle,
                affiliation.EmployeeCode,
                affiliation.OrganizationUnitPartyId,
                unit == null ? string.Empty : unit.DisplayName,
                affiliation.ManagerPartyId,
                manager == null ? string.Empty : manager.DisplayName,
                affiliation.ValidFromUtc,
                affiliation.ValidToUtc,
                affiliation.Notes,
                affiliation.LastChangedBy,
                affiliation.CreatedAtUtc,
                affiliation.UpdatedAtUtc);
    }

    private static PartyOrganizationAffiliationListItemModel MapListItem(
        AffiliationListRow row,
        DateOnly today)
    {
        var validFrom = ToDateOnly(row.ValidFromUtc);
        var validTo = ToDateOnly(row.ValidToUtc);
        return new PartyOrganizationAffiliationListItemModel(
            row.Id,
            row.PersonPartyId,
            row.PersonDisplayName,
            row.OrganizationPartyId,
            row.OrganizationDisplayName,
            row.AffiliationKind,
            row.IsPrimary,
            row.JobTitle,
            row.EmployeeCode,
            row.OrganizationUnitPartyId,
            row.OrganizationUnitDisplayName,
            row.ManagerPartyId,
            row.ManagerDisplayName,
            validFrom,
            validTo,
            row.Notes,
            row.LastChangedBy,
            row.CreatedAtUtc,
            row.UpdatedAtUtc,
            PartyOrganizationAffiliationPolicy.IsCurrent(validFrom, validTo, today));
    }

    private static async Task<EndpointValidationResult> ValidateEndpointsAsync(
        AppDbContext dbContext,
        Guid personPartyId,
        IReadOnlyCollection<PartyOrganizationAffiliationEditorModel> affiliations,
        string actor,
        CancellationToken cancellationToken)
    {
        var partyIds = affiliations
            .SelectMany(item => new Guid?[]
            {
                item.PersonPartyId,
                item.OrganizationPartyId,
                item.OrganizationUnitPartyId,
                item.ManagerPartyId
            })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Append(personPartyId)
            .Distinct()
            .ToArray();
        var parties = await dbContext.Set<Party>()
            .AsNoTracking()
            .Where(party => partyIds.Contains(party.Id))
            .Select(party => new EndpointParty(
                party.Id,
                party.PartyType,
                party.DisplayName,
                party.IsSensitive))
            .ToDictionaryAsync(party => party.Id, cancellationToken);

        var errors = new List<Error>();
        if (!parties.TryGetValue(personPartyId, out var person))
        {
            errors.Add(Error.Failure(
                "The selected person could not be found.",
                "crmhr.affiliation.person-not-found"));
        }
        else if (person.PartyType != PartyType.Person)
        {
            errors.Add(Error.Validation(
                "Affiliations can only be recorded for a person.",
                "crmhr.affiliation.person-type-invalid"));
        }

        foreach (var item in affiliations)
        {
            ValidateEndpointType(
                parties,
                item.OrganizationPartyId,
                PartyType.Organization,
                "Organization",
                "crmhr.affiliation.organization-invalid",
                errors);
            if (item.OrganizationUnitPartyId is Guid organizationUnitPartyId)
            {
                ValidateEndpointType(
                    parties,
                    organizationUnitPartyId,
                    PartyType.OrganizationUnit,
                    "Organization unit",
                    "crmhr.affiliation.organization-unit-invalid",
                    errors);
            }

            if (item.ManagerPartyId is Guid managerPartyId)
            {
                ValidateEndpointType(
                    parties,
                    managerPartyId,
                    PartyType.Person,
                    "Manager",
                    "crmhr.affiliation.manager-invalid",
                    errors);
            }
        }

        return new EndpointValidationResult(
            errors,
            NormalizeActor(actor),
            person?.DisplayName ?? string.Empty,
            person?.IsSensitive ?? false);
    }

    private static void ValidateEndpointType(
        IReadOnlyDictionary<Guid, EndpointParty> parties,
        Guid partyId,
        PartyType expectedType,
        string endpointName,
        string errorCode,
        ICollection<Error> errors)
    {
        if (!parties.TryGetValue(partyId, out var party) ||
            party.PartyType != expectedType)
        {
            errors.Add(Error.Validation(
                $"{endpointName} must reference an existing {FormatPartyType(expectedType)}.",
                errorCode));
        }
    }

    private static string FormatPartyType(PartyType partyType)
        => partyType switch
        {
            PartyType.OrganizationUnit => "organization unit",
            _ => partyType.ToString().ToLowerInvariant()
        };

    private static void ApplyEditor(
        PartyOrganizationAffiliation entity,
        PartyOrganizationAffiliationEditorModel editor,
        string actor,
        DateTimeOffset now)
    {
        entity.PersonPartyId = editor.PersonPartyId;
        entity.OrganizationPartyId = editor.OrganizationPartyId;
        entity.AffiliationKind = editor.AffiliationKind;
        entity.IsPrimary = editor.IsPrimary;
        entity.JobTitle = (editor.JobTitle ?? string.Empty).Trim();
        entity.EmployeeCode = (editor.EmployeeCode ?? string.Empty).Trim();
        entity.OrganizationUnitPartyId = editor.OrganizationUnitPartyId;
        entity.ManagerPartyId = editor.ManagerPartyId;
        entity.ValidFromUtc = ToUtcDate(editor.ValidFrom);
        entity.ValidToUtc = ToUtcDate(editor.ValidTo);
        entity.Notes = (editor.Notes ?? string.Empty).Trim();
        entity.LastChangedBy = actor;
        entity.UpdatedAtUtc = now;
    }

    private static DateTimeOffset? ToUtcDate(DateOnly? value)
        => value.HasValue
            ? new DateTimeOffset(
                value.Value.Year,
                value.Value.Month,
                value.Value.Day,
                0,
                0,
                0,
                TimeSpan.Zero)
            : null;

    private static DateOnly? ToDateOnly(DateTimeOffset? value)
        => value.HasValue
            ? DateOnly.FromDateTime(value.Value.UtcDateTime)
            : null;

    private static string NormalizeActor(string actor)
        => string.IsNullOrWhiteSpace(actor)
            ? "system"
            : actor.Trim();

    private sealed record AffiliationListRow(
        Guid Id,
        Guid PersonPartyId,
        string PersonDisplayName,
        Guid OrganizationPartyId,
        string OrganizationDisplayName,
        PartyOrganizationAffiliationKind AffiliationKind,
        bool IsPrimary,
        string JobTitle,
        string EmployeeCode,
        Guid? OrganizationUnitPartyId,
        string OrganizationUnitDisplayName,
        Guid? ManagerPartyId,
        string ManagerDisplayName,
        DateTimeOffset? ValidFromUtc,
        DateTimeOffset? ValidToUtc,
        string Notes,
        string LastChangedBy,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc);

    private sealed record EndpointParty(
        Guid Id,
        PartyType PartyType,
        string DisplayName,
        bool IsSensitive);

    private sealed record EndpointValidationResult(
        IReadOnlyList<Error> Errors,
        string Actor,
        string PersonDisplayName,
        bool PersonIsSensitive);
}

internal static class PartyOrganizationAffiliationPolicy
{
    public static IReadOnlyList<Error> ValidateReplacement(
        Guid personPartyId,
        IReadOnlyCollection<PartyOrganizationAffiliationEditorModel> affiliations,
        string actor,
        DateOnly today)
    {
        var errors = new List<Error>();
        if (personPartyId == Guid.Empty)
        {
            errors.Add(Error.Validation(
                "Choose a person before saving affiliations.",
                "crmhr.affiliation.person-required"));
        }

        if (affiliations.Count > PartyOrganizationAffiliationLimits.MaximumAffiliationsPerPerson)
        {
            errors.Add(Error.Validation(
                $"A person cannot have more than " +
                $"{PartyOrganizationAffiliationLimits.MaximumAffiliationsPerPerson} affiliations.",
                "crmhr.affiliation.limit-exceeded"));
        }

        ValidateActor(actor, errors);
        var seenIds = new HashSet<Guid>();
        var seenBusinessKeys = new HashSet<AffiliationBusinessKey>();
        foreach (var item in affiliations)
        {
            ValidateItem(item, today, errors);
            if (item.PersonPartyId != personPartyId)
            {
                errors.Add(Error.Validation(
                    "Every replacement affiliation must belong to the selected person.",
                    "crmhr.affiliation.person-mismatch"));
            }

            if (item.Id is Guid id && !seenIds.Add(id))
            {
                errors.Add(Error.Validation(
                    "The same affiliation identifier cannot be submitted more than once.",
                    "crmhr.affiliation.id-duplicate"));
            }

            if (!seenBusinessKeys.Add(AffiliationBusinessKey.From(item)))
            {
                errors.Add(Error.Validation(
                    "The same person, organization, kind, and effective interval cannot be repeated.",
                    "crmhr.affiliation.duplicate"));
            }
        }

        if (affiliations.Count(item => item.IsPrimary) > 1)
        {
            errors.Add(Error.Validation(
                "Only one current affiliation can be primary.",
                "crmhr.affiliation.primary-duplicate"));
        }

        return errors;
    }

    public static IReadOnlyList<Error> ValidateSingle(
        PartyOrganizationAffiliationEditorModel affiliation,
        string actor,
        DateOnly today)
    {
        var errors = new List<Error>();
        ValidateActor(actor, errors);
        ValidateItem(affiliation, today, errors);
        return errors;
    }

    public static bool IsCurrent(
        DateOnly? validFrom,
        DateOnly? validTo,
        DateOnly today)
        => (!validFrom.HasValue || validFrom.Value <= today) &&
           (!validTo.HasValue || validTo.Value >= today);

    private static void ValidateItem(
        PartyOrganizationAffiliationEditorModel item,
        DateOnly today,
        ICollection<Error> errors)
    {
        if (item.PersonPartyId == Guid.Empty)
        {
            errors.Add(Error.Validation(
                "Choose a person before saving an affiliation.",
                "crmhr.affiliation.person-required"));
        }

        if (item.OrganizationPartyId == Guid.Empty)
        {
            errors.Add(Error.Validation(
                "Choose an organization before saving an affiliation.",
                "crmhr.affiliation.organization-required"));
        }

        if (!Enum.IsDefined(item.AffiliationKind))
        {
            errors.Add(Error.Validation(
                "Choose a supported affiliation kind.",
                "crmhr.affiliation.kind-invalid"));
        }

        if (item.PersonPartyId != Guid.Empty &&
            (item.PersonPartyId == item.OrganizationPartyId ||
             item.PersonPartyId == item.OrganizationUnitPartyId ||
             item.PersonPartyId == item.ManagerPartyId))
        {
            errors.Add(Error.Validation(
                "An affiliation cannot reference its person as the organization, unit, or manager.",
                "crmhr.affiliation.self-reference"));
        }

        if (item.OrganizationUnitPartyId == Guid.Empty)
        {
            errors.Add(Error.Validation(
                "An organization unit identifier cannot be empty.",
                "crmhr.affiliation.organization-unit-empty"));
        }

        if (item.ManagerPartyId == Guid.Empty)
        {
            errors.Add(Error.Validation(
                "A manager identifier cannot be empty.",
                "crmhr.affiliation.manager-empty"));
        }

        if (item.ValidFrom.HasValue &&
            item.ValidTo.HasValue &&
            item.ValidTo.Value < item.ValidFrom.Value)
        {
            errors.Add(Error.Validation(
                "The affiliation end date cannot precede its start date.",
                "crmhr.affiliation.date-range-invalid"));
        }

        if (item.IsPrimary &&
            !IsCurrent(item.ValidFrom, item.ValidTo, today))
        {
            errors.Add(Error.Validation(
                "Only a current affiliation can be primary.",
                "crmhr.affiliation.primary-not-current"));
        }

        if ((item.JobTitle?.Trim().Length ?? 0) >
            PartyOrganizationAffiliationLimits.MaximumJobTitleLength)
        {
            errors.Add(Error.Validation(
                $"Job title cannot exceed " +
                $"{PartyOrganizationAffiliationLimits.MaximumJobTitleLength} characters.",
                "crmhr.affiliation.job-title-too-long"));
        }

        if ((item.EmployeeCode?.Trim().Length ?? 0) >
            PartyOrganizationAffiliationLimits.MaximumEmployeeCodeLength)
        {
            errors.Add(Error.Validation(
                $"Employee code cannot exceed " +
                $"{PartyOrganizationAffiliationLimits.MaximumEmployeeCodeLength} characters.",
                "crmhr.affiliation.employee-code-too-long"));
        }
    }

    private static void ValidateActor(string actor, ICollection<Error> errors)
    {
        if ((actor?.Trim().Length ?? 0) >
            PartyOrganizationAffiliationLimits.MaximumActorLength)
        {
            errors.Add(Error.Validation(
                $"Audit actor cannot exceed " +
                $"{PartyOrganizationAffiliationLimits.MaximumActorLength} characters.",
                "crmhr.affiliation.actor-too-long"));
        }
    }

    private readonly record struct AffiliationBusinessKey(
        Guid PersonPartyId,
        Guid OrganizationPartyId,
        PartyOrganizationAffiliationKind AffiliationKind,
        DateOnly? ValidFrom,
        DateOnly? ValidTo)
    {
        public static AffiliationBusinessKey From(
            PartyOrganizationAffiliationEditorModel item)
            => new(
                item.PersonPartyId,
                item.OrganizationPartyId,
                item.AffiliationKind,
                item.ValidFrom,
                item.ValidTo);
    }
}
