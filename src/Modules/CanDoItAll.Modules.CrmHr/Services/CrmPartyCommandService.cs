using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public static class CrmPartyCommandLimits
{
    public const int MaximumDisplayNameLength = 200;
    public const int MaximumExternalCodeLength = 120;
    public const int MaximumSummaryLength = 1_000;
    public const int MaximumRegionLength = 120;
    public const int MaximumCountryCodeLength = 16;
    public const int MaximumTimeZoneLength = 80;
    public const int MaximumTagCount = 20;
    public const int MaximumTagLength = 80;
}

public static class CrmPartyCommandErrorCodes
{
    public const string PartyTypeInvalid =
        "crmhr.party-command.party-type-invalid";
    public const string LifecycleInvalid =
        "crmhr.party-command.lifecycle-invalid";
    public const string DisplayNameRequired =
        "crmhr.party-command.display-name-required";
    public const string FieldTooLong =
        "crmhr.party-command.field-too-long";
    public const string TagsInvalid =
        "crmhr.party-command.tags-invalid";
    public const string SensitiveRecordDenied =
        "crmhr.party-command.sensitive-record-denied";
    public const string AffiliationNotFound =
        "crmhr.party-command.affiliation-not-found";
}

public sealed record CrmPartyCreateCommand(
    PartyType PartyType,
    string DisplayName,
    PartyLifecycleStatus LifecycleStatus = PartyLifecycleStatus.Draft,
    string LegalName = "",
    string PreferredName = "",
    string ExternalCode = "",
    string Summary = "",
    IReadOnlyList<string>? Tags = null,
    string Region = "",
    string CountryCode = "",
    string TimeZone = "");

public sealed record CrmPartyCreateResult(
    Guid PartyId,
    PartyType PartyType,
    PartyLifecycleStatus LifecycleStatus,
    string DisplayName,
    string ExternalCode,
    IReadOnlyList<string> Tags);

public sealed record CrmPartyAffiliationUpsertCommand(
    Guid? AffiliationId,
    Guid PersonPartyId,
    Guid OrganizationPartyId,
    PartyOrganizationAffiliationKind AffiliationKind,
    bool IsPrimary,
    string JobTitle = "",
    Guid? OrganizationUnitPartyId = null,
    Guid? ManagerPartyId = null,
    DateOnly? ValidFrom = null,
    DateOnly? ValidTo = null,
    DateTimeOffset? ExpectedUpdatedAtUtc = null);

public sealed record CrmPartyAffiliationResult(
    Guid AffiliationId,
    Guid PersonPartyId,
    Guid OrganizationPartyId,
    string OrganizationDisplayName,
    PartyOrganizationAffiliationKind AffiliationKind,
    bool IsPrimary,
    string JobTitle,
    Guid? OrganizationUnitPartyId,
    Guid? ManagerPartyId,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    bool IsCurrent,
    DateTimeOffset UpdatedAtUtc);

public interface ICrmPartyCommandService
{
    Task<Result<CrmPartyCreateResult>> CreatePartyAsync(
        CrmPartyCreateCommand command,
        string actor,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<CrmPartyAffiliationResult>>> ListAffiliationsAsync(
        Guid personPartyId,
        CancellationToken cancellationToken = default);

    Task<Result<CrmPartyAffiliationResult>> UpsertAffiliationAsync(
        CrmPartyAffiliationUpsertCommand command,
        string actor,
        CancellationToken cancellationToken = default);
}

public sealed class CrmPartyCommandService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    PartyDirectoryService partyDirectoryService,
    IPartyOrganizationAffiliationService affiliationService)
    : ICrmPartyCommandService
{
    private static readonly IReadOnlySet<PartyType> CreatablePartyTypes =
        new HashSet<PartyType>
        {
            PartyType.Person,
            PartyType.Organization,
            PartyType.OrganizationUnit
        };

    private static readonly IReadOnlySet<PartyLifecycleStatus>
        CreatableLifecycleStatuses = new HashSet<PartyLifecycleStatus>
        {
            PartyLifecycleStatus.Draft,
            PartyLifecycleStatus.Active,
            PartyLifecycleStatus.Candidate,
            PartyLifecycleStatus.Prospect
        };

    public async Task<Result<CrmPartyCreateResult>> CreatePartyAsync(
        CrmPartyCreateCommand command,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var errors = ValidateCreate(command, actor);
        if (errors.Count > 0)
        {
            return Result<CrmPartyCreateResult>.Failure(errors);
        }

        var tags = NormalizeTags(command.Tags);
        var editor = new PartyEditorModel
        {
            PartyType = command.PartyType,
            LifecycleStatus = command.LifecycleStatus,
            DisplayName = command.DisplayName.Trim(),
            LegalName = command.LegalName?.Trim() ?? string.Empty,
            PreferredName = command.PreferredName?.Trim() ?? string.Empty,
            ExternalCode = command.ExternalCode?.Trim() ?? string.Empty,
            Summary = command.Summary?.Trim() ?? string.Empty,
            Tags = tags,
            Region = command.Region?.Trim() ?? string.Empty,
            CountryCode = command.CountryCode?.Trim() ?? string.Empty,
            TimeZone = command.TimeZone?.Trim() ?? string.Empty,
            IsSensitive = false,
            LastChangedBy = NormalizeActor(actor)
        };
        var saveResult = await partyDirectoryService.SavePartyAsync(
            editor,
            cancellationToken);
        if (saveResult.IsFailure)
        {
            return Result<CrmPartyCreateResult>.Failure(saveResult.Errors);
        }

        return Result<CrmPartyCreateResult>.Success(
            new CrmPartyCreateResult(
                saveResult.Value,
                editor.PartyType,
                editor.LifecycleStatus,
                editor.DisplayName,
                editor.ExternalCode,
                tags));
    }

    public async Task<Result<IReadOnlyList<CrmPartyAffiliationResult>>>
        ListAffiliationsAsync(
            Guid personPartyId,
            CancellationToken cancellationToken = default)
    {
        if (personPartyId == Guid.Empty)
        {
            return Result<IReadOnlyList<CrmPartyAffiliationResult>>.Failure(
                Error.Validation(
                    "A person party identifier is required.",
                    "crmhr.party-command.person-required"));
        }

        var affiliations = await affiliationService.ListAsync(
            personPartyId,
            cancellationToken);
        var visibleEndpointIds = await LoadVisibleEndpointIdsAsync(
            personPartyId,
            affiliations,
            cancellationToken);
        if (!visibleEndpointIds.Contains(personPartyId))
        {
            return Result<IReadOnlyList<CrmPartyAffiliationResult>>.Failure(
                SensitiveRecordDeniedError());
        }

        IReadOnlyList<CrmPartyAffiliationResult> visible = affiliations
            .Where(item =>
                visibleEndpointIds.Contains(item.OrganizationPartyId) &&
                (!item.OrganizationUnitPartyId.HasValue ||
                 visibleEndpointIds.Contains(
                     item.OrganizationUnitPartyId.Value)) &&
                (!item.ManagerPartyId.HasValue ||
                 visibleEndpointIds.Contains(item.ManagerPartyId.Value)))
            .Select(MapSafe)
            .ToArray();
        return Result<IReadOnlyList<CrmPartyAffiliationResult>>.Success(
            visible);
    }

    public async Task<Result<CrmPartyAffiliationResult>>
        UpsertAffiliationAsync(
            CrmPartyAffiliationUpsertCommand command,
            string actor,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var endpointIds = new[]
            {
                (Guid?)command.PersonPartyId,
                command.OrganizationPartyId,
                command.OrganizationUnitPartyId,
                command.ManagerPartyId
            }
            .Where(item => item.HasValue && item.Value != Guid.Empty)
            .Select(item => item!.Value)
            .Distinct()
            .ToArray();
        var visibleEndpointIds = await LoadVisiblePartyIdsAsync(
            endpointIds,
            cancellationToken);
        if (endpointIds.Any(id => !visibleEndpointIds.Contains(id)))
        {
            return Result<CrmPartyAffiliationResult>.Failure(
                SensitiveRecordDeniedError());
        }

        PartyOrganizationAffiliationListItemModel? existing = null;
        if (command.AffiliationId.HasValue)
        {
            existing = (await affiliationService.ListAsync(
                    command.PersonPartyId,
                    cancellationToken))
                .SingleOrDefault(item =>
                    item.Id == command.AffiliationId.Value);
            if (existing is null)
            {
                return Result<CrmPartyAffiliationResult>.Failure(
                    Error.Failure(
                        "The selected affiliation was not found for the person.",
                        CrmPartyCommandErrorCodes.AffiliationNotFound));
            }
        }

        var editor = new PartyOrganizationAffiliationEditorModel
        {
            Id = command.AffiliationId,
            PersonPartyId = command.PersonPartyId,
            OrganizationPartyId = command.OrganizationPartyId,
            AffiliationKind = command.AffiliationKind,
            IsPrimary = command.IsPrimary,
            JobTitle = command.JobTitle?.Trim() ?? string.Empty,
            EmployeeCode = existing?.EmployeeCode ?? string.Empty,
            OrganizationUnitPartyId = command.OrganizationUnitPartyId,
            ManagerPartyId = command.ManagerPartyId,
            ValidFrom = command.ValidFrom,
            ValidTo = command.ValidTo,
            Notes = existing?.Notes ?? string.Empty,
            ExpectedUpdatedAtUtc = command.ExpectedUpdatedAtUtc
        };
        var upsertResult = await affiliationService.UpsertAsync(
            editor,
            NormalizeActor(actor),
            command.ExpectedUpdatedAtUtc,
            cancellationToken);
        return upsertResult.IsFailure
            ? Result<CrmPartyAffiliationResult>.Failure(upsertResult.Errors)
            : Result<CrmPartyAffiliationResult>.Success(
                MapSafe(upsertResult.Value!));
    }

    private async Task<HashSet<Guid>> LoadVisibleEndpointIdsAsync(
        Guid personPartyId,
        IReadOnlyCollection<PartyOrganizationAffiliationListItemModel>
            affiliations,
        CancellationToken cancellationToken)
    {
        var ids = affiliations
            .SelectMany(item => new Guid?[]
            {
                item.PersonPartyId,
                item.OrganizationPartyId,
                item.OrganizationUnitPartyId,
                item.ManagerPartyId
            })
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Append(personPartyId)
            .Distinct()
            .ToArray();
        return await LoadVisiblePartyIdsAsync(ids, cancellationToken);
    }

    private async Task<HashSet<Guid>> LoadVisiblePartyIdsAsync(
        IReadOnlyCollection<Guid> partyIds,
        CancellationToken cancellationToken)
    {
        if (partyIds.Count == 0)
        {
            return [];
        }

        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return (await dbContext.Set<Party>()
                .AsNoTracking()
                .Where(party =>
                    partyIds.Contains(party.Id) &&
                    !party.IsSensitive &&
                    party.LifecycleStatus != PartyLifecycleStatus.Archived)
                .Select(party => party.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();
    }

    private static IReadOnlyList<Error> ValidateCreate(
        CrmPartyCreateCommand command,
        string actor)
    {
        var errors = new List<Error>();
        if (!CreatablePartyTypes.Contains(command.PartyType))
        {
            errors.Add(Error.Validation(
                "Create a person, organization, or organization unit.",
                CrmPartyCommandErrorCodes.PartyTypeInvalid));
        }

        if (!CreatableLifecycleStatuses.Contains(command.LifecycleStatus))
        {
            errors.Add(Error.Validation(
                "A new CRM party must be Draft, Active, Candidate, or Prospect.",
                CrmPartyCommandErrorCodes.LifecycleInvalid));
        }

        if (string.IsNullOrWhiteSpace(command.DisplayName))
        {
            errors.Add(Error.Validation(
                "Display name is required.",
                CrmPartyCommandErrorCodes.DisplayNameRequired));
        }

        ValidateLength(
            command.DisplayName,
            CrmPartyCommandLimits.MaximumDisplayNameLength,
            "Display name",
            errors);
        ValidateLength(
            command.LegalName,
            CrmPartyCommandLimits.MaximumDisplayNameLength,
            "Legal name",
            errors);
        ValidateLength(
            command.PreferredName,
            CrmPartyCommandLimits.MaximumDisplayNameLength,
            "Preferred name",
            errors);
        ValidateLength(
            command.ExternalCode,
            CrmPartyCommandLimits.MaximumExternalCodeLength,
            "External code",
            errors);
        ValidateLength(
            command.Summary,
            CrmPartyCommandLimits.MaximumSummaryLength,
            "Summary",
            errors);
        ValidateLength(
            command.Region,
            CrmPartyCommandLimits.MaximumRegionLength,
            "Region",
            errors);
        ValidateLength(
            command.CountryCode,
            CrmPartyCommandLimits.MaximumCountryCodeLength,
            "Country code",
            errors);
        ValidateLength(
            command.TimeZone,
            CrmPartyCommandLimits.MaximumTimeZoneLength,
            "Time zone",
            errors);
        ValidateLength(
            actor,
            PartyOrganizationAffiliationLimits.MaximumActorLength,
            "Actor",
            errors);

        var tags = command.Tags ?? [];
        if (tags.Count > CrmPartyCommandLimits.MaximumTagCount ||
            tags.Any(tag =>
                string.IsNullOrWhiteSpace(tag) ||
                tag.Trim().Length >
                CrmPartyCommandLimits.MaximumTagLength))
        {
            errors.Add(Error.Validation(
                $"Tags are limited to {CrmPartyCommandLimits.MaximumTagCount} " +
                $"non-empty values of at most " +
                $"{CrmPartyCommandLimits.MaximumTagLength} characters.",
                CrmPartyCommandErrorCodes.TagsInvalid));
        }

        return errors;
    }

    private static List<string> NormalizeTags(
        IReadOnlyList<string>? tags)
    {
        return (tags ?? [])
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ValidateLength(
        string? value,
        int maximumLength,
        string fieldName,
        ICollection<Error> errors)
    {
        if ((value?.Trim().Length ?? 0) <= maximumLength)
        {
            return;
        }

        errors.Add(Error.Validation(
            $"{fieldName} cannot exceed {maximumLength} characters.",
            CrmPartyCommandErrorCodes.FieldTooLong));
    }

    private static string NormalizeActor(string actor)
        => string.IsNullOrWhiteSpace(actor)
            ? "system"
            : actor.Trim();

    private static Error SensitiveRecordDeniedError()
        => Error.Failure(
            "Sensitive, archived, or unavailable CRM records cannot be " +
            "changed through this bounded command.",
            CrmPartyCommandErrorCodes.SensitiveRecordDenied);

    private static CrmPartyAffiliationResult MapSafe(
        PartyOrganizationAffiliationListItemModel item)
    {
        return new CrmPartyAffiliationResult(
            item.Id,
            item.PersonPartyId,
            item.OrganizationPartyId,
            item.OrganizationDisplayName,
            item.AffiliationKind,
            item.IsPrimary,
            item.JobTitle,
            item.OrganizationUnitPartyId,
            item.ManagerPartyId,
            item.ValidFrom,
            item.ValidTo,
            item.IsCurrent,
            item.UpdatedAtUtc);
    }
}
