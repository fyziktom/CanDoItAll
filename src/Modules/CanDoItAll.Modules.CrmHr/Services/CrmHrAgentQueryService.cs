using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.SourceGateway;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public enum CrmHrAgentRecordKind
{
    Party,
    Workforce,
    CrmAccount,
    Opportunity,
    AiAgent
}

public enum CrmHrAgentBusinessTextTrust
{
    UntrustedBusinessData
}

public enum CrmHrAgentRedactionState
{
    None,
    InlineSensitiveValueRedacted,
    SensitiveRecordRedacted
}

public static class CrmHrAgentQueryLimits
{
    public const int DefaultTake = 20;
    public const int MinTake = 1;
    public const int MaxTake = 50;
    public const int MaxQueryLength = 200;
    public const int MaxDisplayLabelLength = 200;
    public const int MaxSummaryLength = 1_000;
    public const int MaxTagCount = 20;
    public const int MaxTagLength = 80;
    public const int MaxStatusLength = 100;
}

public static class CrmHrAgentQueryErrorCodes
{
    public const string SearchRequired = "crmhr.agent-query.search-required";
    public const string SearchTooLong = "crmhr.agent-query.search-too-long";
    public const string TakeOutOfRange = "crmhr.agent-query.take-out-of-range";
    public const string RecordKindInvalid = "crmhr.agent-query.record-kind-invalid";
    public const string RecordIdRequired = "crmhr.agent-query.record-id-required";
    public const string RecordNotFound = "crmhr.agent-query.record-not-found";
}

public sealed record CrmHrAgentSearchQuery(
    string SearchText,
    CrmHrAgentRecordKind? RecordKind = null,
    int Take = CrmHrAgentQueryLimits.DefaultTake);

public sealed record CrmHrAgentItemReference(
    CrmHrAgentRecordKind RecordKind,
    Guid Id);

public sealed record CrmHrAgentRecordStatus(
    PartyLifecycleStatus? LifecycleStatus = null,
    string WorkforceStatus = "",
    CrmAccountRelationshipStage? AccountRelationshipStage = null,
    OpportunityStage? OpportunityStage = null,
    AiValidationStatus? AiValidationStatus = null);

public sealed record CrmHrAgentAvailability(
    WorkforceAvailabilityState State,
    decimal AvailablePercent,
    DateOnly? NextAvailabilityOn);

public sealed record CrmHrAgentQueryItem(
    Guid Id,
    CrmHrAgentRecordKind RecordKind,
    string DisplayLabel,
    CrmHrAgentRecordStatus Status,
    string SafeSummary,
    IReadOnlyList<string> SafeTags,
    CrmHrAgentAvailability? Availability,
    CrmHrAgentRedactionState RedactionState,
    CrmHrAgentBusinessTextTrust BusinessTextTrust);

public interface ICrmHrAgentQueryService
{
    Task<Result<IReadOnlyList<CrmHrAgentQueryItem>>> SearchAsync(
        CrmHrAgentSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<CrmHrAgentQueryItem>> GetSummaryAsync(
        CrmHrAgentItemReference reference,
        CancellationToken cancellationToken = default);
}

public sealed class CrmHrAgentQueryService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock) : ICrmHrAgentQueryService
{
    private static readonly IReadOnlyList<CrmHrAgentRecordKind> SupportedRecordKinds =
    [
        CrmHrAgentRecordKind.Party,
        CrmHrAgentRecordKind.Workforce,
        CrmHrAgentRecordKind.CrmAccount,
        CrmHrAgentRecordKind.Opportunity,
        CrmHrAgentRecordKind.AiAgent
    ];

    public async Task<Result<IReadOnlyList<CrmHrAgentQueryItem>>> SearchAsync(
        CrmHrAgentSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var validationError = Validate(query);
        if (validationError is not null)
        {
            return Result<IReadOnlyList<CrmHrAgentQueryItem>>.Failure(validationError);
        }

        var searchText = query.SearchText.Trim();
        var normalizedSearchText = searchText.ToUpperInvariant();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = query.RecordKind is CrmHrAgentRecordKind recordKind
            ? await SearchKindAsync(dbContext, recordKind, normalizedSearchText, query.Take, cancellationToken)
            : await SearchAllKindsAsync(dbContext, normalizedSearchText, query.Take, cancellationToken);
        var availabilityByPartyId = await LoadAvailabilityAsync(dbContext, candidates, cancellationToken);

        IReadOnlyList<CrmHrAgentQueryItem> items = candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Item = Map(candidate, availabilityByPartyId.GetValueOrDefault(candidate.Id)),
                Rank = Rank(candidate, searchText)
            })
            .OrderBy(item => item.Rank)
            .ThenBy(item => item.Candidate.DisplayLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Candidate.RecordKind)
            .ThenBy(item => item.Candidate.Id)
            .Select(item => item.Item)
            .DistinctBy(item => (item.RecordKind, item.Id))
            .Take(query.Take)
            .ToArray();

        return Result<IReadOnlyList<CrmHrAgentQueryItem>>.Success(items);
    }

    public async Task<Result<CrmHrAgentQueryItem>> GetSummaryAsync(
        CrmHrAgentItemReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (reference.Id == Guid.Empty)
        {
            return Result<CrmHrAgentQueryItem>.Failure(Error.Validation(
                "CRM/HR record id is required.",
                CrmHrAgentQueryErrorCodes.RecordIdRequired));
        }

        if (!IsSupported(reference.RecordKind))
        {
            return Result<CrmHrAgentQueryItem>.Failure(InvalidRecordKindError());
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidate = await CandidateQuery(dbContext, reference.RecordKind)
            .SingleOrDefaultAsync(item => item.Id == reference.Id, cancellationToken);
        if (candidate is null)
        {
            return Result<CrmHrAgentQueryItem>.Failure(Error.Failure(
                "The requested CRM/HR record was not found for the supplied record kind.",
                CrmHrAgentQueryErrorCodes.RecordNotFound));
        }

        var availabilityByPartyId = await LoadAvailabilityAsync(dbContext, [candidate], cancellationToken);
        return Result<CrmHrAgentQueryItem>.Success(
            Map(candidate, availabilityByPartyId.GetValueOrDefault(candidate.Id)));
    }

    private static Error? Validate(CrmHrAgentSearchQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.SearchText))
        {
            return Error.Validation(
                "CRM/HR search text is required.",
                CrmHrAgentQueryErrorCodes.SearchRequired);
        }

        if (query.SearchText.Trim().Length > CrmHrAgentQueryLimits.MaxQueryLength)
        {
            return Error.Validation(
                $"CRM/HR search text cannot exceed {CrmHrAgentQueryLimits.MaxQueryLength} characters.",
                CrmHrAgentQueryErrorCodes.SearchTooLong);
        }

        if (query.Take is < CrmHrAgentQueryLimits.MinTake or > CrmHrAgentQueryLimits.MaxTake)
        {
            return Error.Validation(
                $"CRM/HR search take must be between {CrmHrAgentQueryLimits.MinTake} and {CrmHrAgentQueryLimits.MaxTake}.",
                CrmHrAgentQueryErrorCodes.TakeOutOfRange);
        }

        return query.RecordKind is CrmHrAgentRecordKind recordKind && !IsSupported(recordKind)
            ? InvalidRecordKindError()
            : null;
    }

    private static Error InvalidRecordKindError()
        => Error.Validation(
            "The supplied CRM/HR record kind is not supported.",
            CrmHrAgentQueryErrorCodes.RecordKindInvalid);

    private static bool IsSupported(CrmHrAgentRecordKind recordKind)
        => recordKind is
            CrmHrAgentRecordKind.Party or
            CrmHrAgentRecordKind.Workforce or
            CrmHrAgentRecordKind.CrmAccount or
            CrmHrAgentRecordKind.Opportunity or
            CrmHrAgentRecordKind.AiAgent;

    private static async Task<IReadOnlyList<Candidate>> SearchAllKindsAsync(
        AppDbContext dbContext,
        string normalizedSearchText,
        int take,
        CancellationToken cancellationToken)
    {
        var results = new List<Candidate>(take * 5);
        foreach (var recordKind in SupportedRecordKinds)
        {
            results.AddRange(await SearchKindAsync(
                dbContext,
                recordKind,
                normalizedSearchText,
                take,
                cancellationToken));
        }

        return results;
    }

    private static Task<List<Candidate>> SearchKindAsync(
        AppDbContext dbContext,
        CrmHrAgentRecordKind recordKind,
        string normalizedSearchText,
        int take,
        CancellationToken cancellationToken)
    {
        return CandidateQuery(dbContext, recordKind)
            .Where(item => !item.IsSensitive)
            .Where(item =>
                item.DisplayLabel.ToUpper().Contains(normalizedSearchText) ||
                item.SearchContext.ToUpper().Contains(normalizedSearchText))
            .OrderBy(item => item.DisplayLabel.ToUpper() == normalizedSearchText
                ? 0
                : item.DisplayLabel.ToUpper().StartsWith(normalizedSearchText)
                    ? 1
                    : item.DisplayLabel.ToUpper().Contains(normalizedSearchText)
                        ? 2
                        : 3)
            .ThenBy(item => item.DisplayLabel)
            .ThenBy(item => item.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<Candidate> CandidateQuery(
        AppDbContext dbContext,
        CrmHrAgentRecordKind recordKind)
    {
        return recordKind switch
        {
            CrmHrAgentRecordKind.Party => PartyCandidates(dbContext),
            CrmHrAgentRecordKind.Workforce => WorkforceCandidates(dbContext),
            CrmHrAgentRecordKind.CrmAccount => AccountCandidates(dbContext),
            CrmHrAgentRecordKind.Opportunity => OpportunityCandidates(dbContext),
            CrmHrAgentRecordKind.AiAgent => AiAgentCandidates(dbContext),
            _ => throw new ArgumentOutOfRangeException(nameof(recordKind), recordKind, "Unsupported CRM/HR record kind.")
        };
    }

    private static IQueryable<Candidate> PartyCandidates(AppDbContext dbContext)
    {
        return dbContext.Set<Party>()
            .AsNoTracking()
            .Select(party => new Candidate(
                party.Id,
                CrmHrAgentRecordKind.Party,
                party.DisplayName,
                party.Summary,
                party.TagsJson,
                string.Empty,
                party.IsSensitive,
                party.LifecycleStatus,
                string.Empty,
                null,
                null,
                null));
    }

    private static IQueryable<Candidate> WorkforceCandidates(AppDbContext dbContext)
    {
        return
            from profile in dbContext.Set<WorkforceProfile>().AsNoTracking()
            join party in dbContext.Set<Party>().AsNoTracking()
                on profile.PartyId equals party.Id
            where party.PartyType != PartyType.AiAgent
            select new Candidate(
                party.Id,
                CrmHrAgentRecordKind.Workforce,
                party.DisplayName,
                party.Summary + " Role: " + profile.JobTitle + "; discipline: " + profile.Discipline,
                party.TagsJson,
                profile.JobTitle + " " + profile.Discipline + " " + profile.Status,
                party.IsSensitive,
                party.LifecycleStatus,
                profile.Status,
                null,
                null,
                null);
    }

    private static IQueryable<Candidate> AccountCandidates(AppDbContext dbContext)
    {
        return
            from party in dbContext.Set<Party>().AsNoTracking()
            join profile in dbContext.Set<CrmAccountProfile>().AsNoTracking()
                on party.Id equals profile.AccountPartyId
            where party.PartyType == PartyType.Organization
            select new Candidate(
                party.Id,
                CrmHrAgentRecordKind.CrmAccount,
                party.DisplayName,
                party.Summary,
                party.TagsJson,
                string.Empty,
                party.IsSensitive,
                party.LifecycleStatus,
                string.Empty,
                profile.RelationshipStage,
                null,
                null);
    }

    private static IQueryable<Candidate> OpportunityCandidates(AppDbContext dbContext)
    {
        return
            from opportunity in dbContext.Set<Opportunity>().AsNoTracking()
            join account in dbContext.Set<Party>().AsNoTracking()
                on opportunity.AccountPartyId equals account.Id into accounts
            from account in accounts.DefaultIfEmpty()
            select new Candidate(
                opportunity.Id,
                CrmHrAgentRecordKind.Opportunity,
                opportunity.Title,
                opportunity.Summary,
                string.Empty,
                string.Empty,
                true,
                account == null ? null : account.LifecycleStatus,
                string.Empty,
                null,
                opportunity.Stage,
                null);
    }

    private static IQueryable<Candidate> AiAgentCandidates(AppDbContext dbContext)
    {
        return
            from party in dbContext.Set<Party>().AsNoTracking()
            where party.PartyType == PartyType.AiAgent
            join profile in dbContext.Set<AiAgentProfile>().AsNoTracking()
                on party.Id equals profile.PartyId into profiles
            from profile in profiles.DefaultIfEmpty()
            select new Candidate(
                party.Id,
                CrmHrAgentRecordKind.AiAgent,
                party.DisplayName,
                party.Summary,
                party.TagsJson,
                string.Empty,
                party.IsSensitive,
                party.LifecycleStatus,
                string.Empty,
                null,
                null,
                profile == null ? null : profile.ValidationStatus);
    }

    private async Task<IReadOnlyDictionary<Guid, CrmHrAgentAvailability>> LoadAvailabilityAsync(
        AppDbContext dbContext,
        IReadOnlyList<Candidate> candidates,
        CancellationToken cancellationToken)
    {
        var workforcePartyIds = candidates
            .Where(item => item.RecordKind == CrmHrAgentRecordKind.Workforce)
            .Select(item => item.Id)
            .Distinct()
            .ToArray();
        if (workforcePartyIds.Length == 0)
        {
            return new Dictionary<Guid, CrmHrAgentAvailability>();
        }

        var allocations = await dbContext.Set<ProjectPartyAssignment>()
            .AsNoTracking()
            .Where(item =>
                workforcePartyIds.Contains(item.PartyId) &&
                item.AllocationPercent.HasValue)
            .Select(item => new
            {
                item.PartyId,
                AllocationPercent = item.AllocationPercent!.Value,
                item.StartsAtUtc,
                item.EndsAtUtc
            })
            .ToListAsync(cancellationToken);
        var capacityBlocks = await dbContext.Set<CapacityBlock>()
            .AsNoTracking()
            .Where(item => workforcePartyIds.Contains(item.PartyId))
            .Select(item => new
            {
                item.PartyId,
                item.Percentage,
                item.StartDateUtc,
                item.EndDateUtc
            })
            .ToListAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        return workforcePartyIds.ToDictionary(
            partyId => partyId,
            partyId =>
            {
                var partyAllocations = allocations.Where(item => item.PartyId == partyId).ToArray();
                var partyBlocks = capacityBlocks.Where(item => item.PartyId == partyId).ToArray();
                var activeAllocationPercent = partyAllocations
                    .Where(item =>
                        (!item.StartsAtUtc.HasValue || item.StartsAtUtc.Value <= now) &&
                        (!item.EndsAtUtc.HasValue || item.EndsAtUtc.Value >= now))
                    .Sum(item => item.AllocationPercent);
                var activeBlockedPercent = partyBlocks
                    .Where(item => item.StartDateUtc <= now && item.EndDateUtc >= now)
                    .Sum(item => item.Percentage);
                var nextAvailabilityOn = partyAllocations
                    .Where(item => item.EndsAtUtc.HasValue && item.EndsAtUtc.Value >= now)
                    .Select(item => DateOnly.FromDateTime(item.EndsAtUtc!.Value.UtcDateTime))
                    .Concat(partyBlocks
                        .Where(item => item.EndDateUtc >= now)
                        .Select(item => DateOnly.FromDateTime(item.EndDateUtc.UtcDateTime)))
                    .OrderBy(item => item)
                    .Cast<DateOnly?>()
                    .FirstOrDefault();
                var state = ResolveAvailabilityState(
                    activeAllocationPercent,
                    activeBlockedPercent,
                    nextAvailabilityOn,
                    today);

                return new CrmHrAgentAvailability(
                    state,
                    Math.Max(0m, 100m - activeAllocationPercent - activeBlockedPercent),
                    nextAvailabilityOn);
            });
    }

    private static WorkforceAvailabilityState ResolveAvailabilityState(
        decimal activeAllocationPercent,
        decimal activeBlockedPercent,
        DateOnly? nextAvailabilityOn,
        DateOnly today)
    {
        if (activeAllocationPercent + activeBlockedPercent > 100m)
        {
            return WorkforceAvailabilityState.Overallocated;
        }

        if (activeAllocationPercent <= 10m && activeBlockedPercent < 25m)
        {
            return WorkforceAvailabilityState.Bench;
        }

        return nextAvailabilityOn.HasValue && nextAvailabilityOn.Value <= today.AddDays(30)
            ? WorkforceAvailabilityState.NearAvailable
            : WorkforceAvailabilityState.Allocated;
    }

    private static CrmHrAgentQueryItem Map(
        Candidate candidate,
        CrmHrAgentAvailability? availability)
    {
        if (candidate.IsSensitive)
        {
            return new CrmHrAgentQueryItem(
                candidate.Id,
                candidate.RecordKind,
                MemorySourceSnapshotSecurity.RedactedValue,
                new CrmHrAgentRecordStatus(),
                MemorySourceSnapshotSecurity.RedactedValue,
                [],
                null,
                CrmHrAgentRedactionState.SensitiveRecordRedacted,
                CrmHrAgentBusinessTextTrust.UntrustedBusinessData);
        }

        var inlineValueRedacted = false;
        var displayLabel = SanitizeText(
            candidate.DisplayLabel,
            CrmHrAgentQueryLimits.MaxDisplayLabelLength,
            ref inlineValueRedacted);
        var summary = SanitizeText(
            candidate.Summary,
            CrmHrAgentQueryLimits.MaxSummaryLength,
            ref inlineValueRedacted);
        var workforceStatus = SanitizeText(
            candidate.WorkforceStatus,
            CrmHrAgentQueryLimits.MaxStatusLength,
            ref inlineValueRedacted);
        var tags = ParseTags(candidate.TagsJson, candidate.Id)
            .Select(tag => SanitizeText(tag, CrmHrAgentQueryLimits.MaxTagLength, ref inlineValueRedacted))
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(CrmHrAgentQueryLimits.MaxTagCount)
            .ToArray();

        return new CrmHrAgentQueryItem(
            candidate.Id,
            candidate.RecordKind,
            displayLabel,
            new CrmHrAgentRecordStatus(
                candidate.LifecycleStatus,
                workforceStatus,
                candidate.AccountRelationshipStage,
                candidate.OpportunityStage,
                candidate.AiValidationStatus),
            summary,
            tags,
            availability,
            inlineValueRedacted
                ? CrmHrAgentRedactionState.InlineSensitiveValueRedacted
                : CrmHrAgentRedactionState.None,
            CrmHrAgentBusinessTextTrust.UntrustedBusinessData);
    }

    private static IReadOnlyList<string> ParseTags(string tagsJson, Guid recordId)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(tagsJson) ?? [];
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"CRM/HR record '{recordId:D}' contains invalid tags JSON.",
                exception);
        }
    }

    private static string SanitizeText(
        string? value,
        int maxLength,
        ref bool inlineValueRedacted)
    {
        var normalized = value?.Trim() ?? string.Empty;
        var sanitized = MemorySourceSnapshotSecurity.RedactSensitiveInlineValues(normalized).Trim();
        inlineValueRedacted |= !string.Equals(normalized, sanitized, StringComparison.Ordinal);

        return sanitized.Length <= maxLength
            ? sanitized
            : sanitized[..(maxLength - 1)] + "…";
    }

    private static int Rank(Candidate candidate, string searchText)
    {
        if (string.Equals(candidate.DisplayLabel, searchText, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (candidate.DisplayLabel.StartsWith(searchText, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return candidate.DisplayLabel.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            ? 2
            : 3;
    }

    private sealed record Candidate(
        Guid Id,
        CrmHrAgentRecordKind RecordKind,
        string DisplayLabel,
        string Summary,
        string TagsJson,
        string SearchContext,
        bool IsSensitive,
        PartyLifecycleStatus? LifecycleStatus,
        string WorkforceStatus,
        CrmAccountRelationshipStage? AccountRelationshipStage,
        OpportunityStage? OpportunityStage,
        AiValidationStatus? AiValidationStatus);
}
