using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CanDoItAll.Modules.CrmHr;

internal enum CrmRouteSelectionResolutionFailure
{
    None,
    OpportunityNotFound,
    InteractionNotFound,
    InteractionAccountAmbiguous,
    ConflictingAccount
}

internal readonly record struct CrmRouteSelectionResolution(
    Guid? AccountPartyId,
    CrmRouteSelectionResolutionFailure Failure)
{
    public bool IsResolved => Failure == CrmRouteSelectionResolutionFailure.None;

    public static CrmRouteSelectionResolution Resolved(Guid? accountPartyId)
        => new(accountPartyId, CrmRouteSelectionResolutionFailure.None);

    public static CrmRouteSelectionResolution Unresolved(
        CrmRouteSelectionResolutionFailure failure)
    {
        if (failure == CrmRouteSelectionResolutionFailure.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failure),
                failure,
                "An unresolved CRM route selection requires a failure reason.");
        }

        return new CrmRouteSelectionResolution(null, failure);
    }
}

internal sealed record StaffingCapacityCounts(
    int BenchCount,
    int OverallocatedCount);

internal sealed record CrmPartyContactValue(Guid PartyId, PartyContactType ContactType, string Value, bool IsPrimary);

internal sealed class CrmOpportunityExtendedDataModel
{
    public string CompetitorName { get; set; } = string.Empty;

    public string PartnerContributionSummary { get; set; } = string.Empty;
}

public sealed partial class PartyDirectoryService(
    IDbContextFactory<CrmHrDbContext> dbContextFactory,
    IClock clock,
    IActivityStream activityStream,
    ISearchIndexService searchIndexService,
    CrmHrChanges changes)
{
    private const string PartyNotFoundErrorCode = "crmhr.party.not-found";
    private const string MultiplePrimaryContactsErrorCode = "crmhr.party.multiple-primary-contacts";

    public async Task<IReadOnlyList<PartySummaryModel>> ListPartiesAsync(CancellationToken cancellationToken = default)
    {
        return (await ListDirectoryAsync(cancellationToken))
            .Select(item => new PartySummaryModel(
                item.Id,
                item.DisplayName,
                item.PartyType,
                item.LifecycleStatus,
                item.IsSensitive,
                item.ExternalCode,
                item.UpdatedAtUtc))
            .ToList();
    }

    public async Task<IReadOnlyList<PartyDirectoryListItemModel>> ListDirectoryAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var parties = await dbContext.Set<Party>()
            .OrderBy(party => party.DisplayName)
            .Select(party => new
            {
                party.Id,
                party.DisplayName,
                party.PartyType,
                party.LifecycleStatus,
                party.IsSensitive,
                party.ExternalCode,
                party.Summary,
                party.TagsJson,
                party.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var partyIds = parties.Select(item => item.Id).ToList();
        var roles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => partyIds.Contains(item.PartyId))
            .OrderBy(item => item.RoleKind)
            .Select(item => new { item.PartyId, item.RoleKind })
            .ToListAsync(cancellationToken);
        var contactPoints = await dbContext.Set<PartyContactPoint>()
            .Where(item => partyIds.Contains(item.PartyId))
            .OrderBy(item => item.PartyId)
            .ThenBy(item => item.ContactType)
            .ThenByDescending(item => item.IsPrimary)
            .ThenBy(item => item.Id)
            .Select(item => new PartyDirectoryContactValue(
                item.Id,
                item.PartyId,
                item.ContactType,
                item.Value,
                item.IsPrimary))
            .ToListAsync(cancellationToken);

        var rolesByPartyId = roles
            .GroupBy(item => item.PartyId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PartyRoleKind>)group.Select(item => item.RoleKind).Distinct().ToList());
        var contactsByPartyId = contactPoints
            .GroupBy(item => item.PartyId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return parties
            .Select(party =>
            {
                var tags = PartyEditorContent.ReadTags(party.TagsJson, party.Id);
                var partyContacts = contactsByPartyId.GetValueOrDefault(party.Id) ?? [];
                return new PartyDirectoryListItemModel(
                    party.Id,
                    party.DisplayName,
                    party.PartyType,
                    party.LifecycleStatus,
                    party.IsSensitive,
                    party.ExternalCode,
                    party.Summary,
                    tags,
                    rolesByPartyId.GetValueOrDefault(party.Id) ?? [],
                    ResolvePrimaryContact(partyContacts, PartyContactType.Email),
                    ResolvePrimaryContact(partyContacts, PartyContactType.Phone),
                    party.UpdatedAtUtc);
            })
            .ToList();
    }

    public async Task<PartyEditorModel?> GetPartyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var observation = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            : null;
        var party = await dbContext.Set<Party>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (party is null)
        {
            return null;
        }

        var roles = await dbContext.Set<PartyRoleAssignment>().Where(item => item.PartyId == id).OrderBy(item => item.RoleKind).ToListAsync(cancellationToken);
        var contactPoints = await dbContext.Set<PartyContactPoint>()
            .Where(item => item.PartyId == id)
            .OrderBy(item => item.ContactType)
            .ThenByDescending(item => item.IsPrimary)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var addresses = await dbContext.Set<PartyAddress>().Where(item => item.PartyId == id).OrderByDescending(item => item.IsPrimary).ToListAsync(cancellationToken);
        var confidentialNotes = (await dbContext.Set<PartyConfidentialNote>()
            .Where(item => item.PartyId == id)
            .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .ToList();
        return PartyEditorContent.Map(party, roles, contactPoints, addresses, confidentialNotes);
    }

    public Task<Result<Guid>> SavePartyAsync(PartyEditorModel model, CancellationToken cancellationToken = default)
        => SavePartyObservedAsync(model, null, cancellationToken);

    internal async Task<Result<Guid>> SavePartyObservedAsync(PartyEditorModel model, Action<Guid>? onCommitted,
        CancellationToken cancellationToken = default, Func<CancellationToken, ValueTask>? authorizeCommit = null) {
        var result = await SavePartyWithReceiptAsync(model, receipt => onCommitted?.Invoke(receipt.PartyId), cancellationToken, authorizeCommit);
        return result.IsSuccess
            ? Result<Guid>.Success(result.Value!.PartyId)
            : Result<Guid>.Failure(result.Errors);
    }

    public async Task<Result<PartySaveReceipt>> SavePartyWithReceiptAsync(PartyEditorModel model,
        Action<PartySaveReceipt>? onCommitted = null, CancellationToken cancellationToken = default,
        Func<CancellationToken, ValueTask>? authorizeCommit = null) {
        ArgumentNullException.ThrowIfNull(model);
        if (authorizeCommit is not null) {
            await authorizeCommit(cancellationToken);
        }
        model = model.Snapshot();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var typeMutation = model.Id is Guid id && await dbContext.Set<Party>().AsNoTracking()
            .AnyAsync(party => party.Id == id && party.PartyType != model.PartyType, cancellationToken)
                ? await SerializableMutationScope.BeginAsync(dbContext, [PartyAffiliationEndpoints.ScopeKey(id), PartyRelationshipService.ScopeKey(id), OpportunityPartyReferences.ScopeKey(id)], cancellationToken)
                : null;
        await using var partyTransaction = typeMutation is null && dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken) : null;
        PartySaveOperation operation;
        PartySaveReceipt receipt;
        try {
            var saveResult = await SavePartyCoreAsync(dbContext, model, cancellationToken);
            if (saveResult.IsFailure || saveResult.Value is null) {
                return Result<PartySaveReceipt>.Failure(saveResult.Errors);
            }
            operation = saveResult.Value;
            await changes.DemandCommitAsync(cancellationToken);
            if (authorizeCommit is not null) {
                await authorizeCommit(cancellationToken);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            receipt = new PartySaveReceipt(operation.PartyId, PartyEditorContent.Accepted(dbContext, operation.PartyId));
            await changes.DemandCommitAsync(cancellationToken);
            if (authorizeCommit is not null) {
                await authorizeCommit(cancellationToken);
            }
            if (typeMutation is not null) {
                await typeMutation.CommitAsync(cancellationToken);
            } else if (partyTransaction is not null) {
                await partyTransaction.CommitAsync(cancellationToken);
            }
        } catch (Exception exception) when (exception is DbUpdateConcurrencyException || SerializableMutationScope.IsConflict(exception)) {
            return Result<PartySaveReceipt>.Failure(PartyChanged());
        }
        onCommitted?.Invoke(receipt);
        changes.PublishParty(receipt.PartyId, operation.IsNew ? EditingChangeKind.Created : EditingChangeKind.EditableContentChanged);
        await PublishPartySavedAsync(operation, cancellationToken);
        return Result<PartySaveReceipt>.Success(receipt);
    }

    public async Task<Result<int>> ImportPartiesAtomicallyAsync(
        IReadOnlyList<PartyEditorModel> models,
        string actor,
        CancellationToken cancellationToken = default,
        Action<int>? onCommitted = null)
    {
        ArgumentNullException.ThrowIfNull(models);
        if (models.Count > PartyDuplicateAnalysis.MaximumInputs) {
            return Result<int>.Failure(Error.Validation($"Split the import into batches of at most {PartyDuplicateAnalysis.MaximumInputs} rows.",
                CrmHrEditingErrorCodes.PartyImportIntentInvalid));
        }
        if (models.Count == 0)
        {
            return Result<int>.Failure(Error.Validation(
                "At least one party is required for import.",
                "crmhr.party.import-no-ready-rows"));
        }

        models = models.Select(model => model.Snapshot()).ToArray();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(dbContext,
            models.Where(model => model.Id.HasValue).SelectMany(model => new[] {
                PartyAffiliationEndpoints.ScopeKey(model.Id!.Value), PartyRelationshipService.ScopeKey(model.Id.Value), OpportunityPartyReferences.ScopeKey(model.Id.Value)
            }).ToArray(), cancellationToken);
        var operations = new List<PartySaveOperation>(models.Count);
        try {
            var duplicateError = await PartyDuplicateAnalysis.ValidateNewImportsAsync(dbContext, models, cancellationToken);
            if (duplicateError is not null) {
                return Result<int>.Failure(duplicateError);
            }
            foreach (var model in models) {
                model.LastChangedBy = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim();
                var saveResult = await SavePartyCoreAsync(dbContext, model, cancellationToken);
                if (saveResult.IsFailure || saveResult.Value is null) {
                    return Result<int>.Failure(saveResult.Errors);
                }
                operations.Add(saveResult.Value);
            }
            dbContext.Set<CrmHrAuditEntry>().Add(new CrmHrAuditEntry {
                EntityType = nameof(Party),
                EntityId = Guid.Empty,
                Action = "CsvImport",
                Summary = $"Imported {operations.Count} party row(s) from CSV.",
                DetailJson = JsonSerializer.Serialize(new { ImportedCount = operations.Count }),
                Actor = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim(),
                CreatedAtUtc = clock.GetUtcNow()
            });
            await changes.DemandCommitAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await changes.DemandCommitAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        } catch (Exception exception) when (exception is DbUpdateConcurrencyException || SerializableMutationScope.IsConflict(exception)) {
            return Result<int>.Failure(PartyChanged());
        }

        try {
            foreach (var operation in operations) {
                changes.PublishParty(operation.PartyId, operation.IsNew ? EditingChangeKind.Created : EditingChangeKind.EditableContentChanged);
            }
        } finally {
            onCommitted?.Invoke(operations.Count);
        }
        foreach (var operation in operations) {
            await PublishPartySavedAsync(operation, cancellationToken);
        }

        return Result<int>.Success(operations.Count);
    }

    internal async Task<Result<PartySaveOperation>> SavePartyCoreAsync(
        CrmHrDbContext dbContext,
        PartyEditorModel model,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (string.IsNullOrWhiteSpace(model.DisplayName))
        {
            return Result<PartySaveOperation>.Failure([Error.Validation("Display name is required.", "crmhr.party.display-name-required")]);
        }

        if (model.Id.HasValue && model.UpdatedAtUtc is null or { Ticks: 0 }) {
            return Result<PartySaveOperation>.Failure(Error.Validation(
                "The original UpdatedAtUtc is required when replacing a party.", CrmHrEditingErrorCodes.PartyOriginalRequired));
        }
        if (PartyEditorChildren.HasInvalidIdentities(model)) {
            return Result<PartySaveOperation>.Failure(Error.Validation(
                "Child rows must have distinct, nonempty identities.", CrmHrEditingErrorCodes.PartyChildIdentityInvalid));
        }

        var duplicatePrimaryContactTypes = model.ContactPoints
            .Where(item => item.IsPrimary && !string.IsNullOrWhiteSpace(item.Value))
            .GroupBy(item => item.ContactType)
            .Where(group => group.Skip(1).Any())
            .Select(group => group.Key)
            .OrderBy(contactType => contactType)
            .ToArray();
        if (duplicatePrimaryContactTypes.Length > 0)
        {
            return Result<PartySaveOperation>.Failure([Error.Validation(
                $"Only one primary contact is allowed for each contact type. Conflicting types: {string.Join(", ", duplicatePrimaryContactTypes)}.",
                MultiplePrimaryContactsErrorCode)]);
        }

        var confidentialNotes = model.ConfidentialNotes
            .Where(item => !string.IsNullOrWhiteSpace(item.NoteText))
            .ToList();
        if (!model.IsSensitive && confidentialNotes.Count > 0)
        {
            return Result<PartySaveOperation>.Failure([Error.Validation(
                "Mark the party as sensitive before saving confidential notes.",
                "crmhr.party.confidential-notes-require-sensitive")]);
        }

        var now = clock.GetUtcNow();
        Party party;
        var isNew = !model.Id.HasValue;
        if (model.Id is Guid existingPartyId)
        {
            var existingParty = await dbContext.Set<Party>()
                .SingleOrDefaultAsync(item => item.Id == existingPartyId, cancellationToken);
            if (existingParty is null)
            {
                return Result<PartySaveOperation>.Failure([Error.Failure(
                    "The selected party was not found.",
                    PartyNotFoundErrorCode)]);
            }

            if (existingParty.UpdatedAtUtc != model.UpdatedAtUtc) {
                return Result<PartySaveOperation>.Failure(PartyChanged());
            }
            if (existingParty.PartyType != model.PartyType &&
                await PartyAffiliationEndpoints.WouldInvalidateAsync(dbContext, existingPartyId, model.PartyType, cancellationToken)) {
                return Result<PartySaveOperation>.Failure(Error.Validation(
                    "This party's type is required by saved affiliation history. Keep its current type.",
                    CrmHrEditingErrorCodes.PartyAffiliationTypeConflict));
            }
            if (existingParty.PartyType != model.PartyType &&
                await OpportunityPartyReferences.WouldInvalidateAsync(dbContext, existingPartyId, model.PartyType, cancellationToken)) {
                return Result<PartySaveOperation>.Failure(Error.Validation(
                    "This party's type is required by saved opportunity account, owner or delivery references.", CrmHrEditingErrorCodes.OpportunityPartyTypeConflict));
            }
            if (existingParty.PartyType != model.PartyType &&
                await AccountPartyReferences.WouldInvalidateAsync(dbContext, existingPartyId, model.PartyType, cancellationToken)) {
                return Result<PartySaveOperation>.Failure(Error.Validation(
                    "This organization's type is required by its saved CRM profile, connections or interaction history.",
                    CrmHrEditingErrorCodes.AccountPartyTypeConflict));
            }
            if (existingParty.PartyType != model.PartyType &&
                await WorkforcePartyReferences.WouldInvalidateAsync(dbContext, existingPartyId, model.PartyType, cancellationToken)) {
                return Result<PartySaveOperation>.Failure(Error.Validation(
                    "This party's type is required by saved workforce profiles, managers or home units.",
                    CrmHrEditingErrorCodes.WorkforcePartyTypeConflict));
            }
            if (existingParty.PartyType != model.PartyType &&
                await RecruitmentPartyReferences.WouldInvalidateAsync(dbContext, existingPartyId, model.PartyType, cancellationToken)) {
                return Result<PartySaveOperation>.Failure(Error.Validation(
                    "This party's type is required by saved recruitment applications, interviews or lifecycle tasks.", RecruitmentEditingErrors.PartyTypeConflict));
            }
            if (existingParty.PartyType != model.PartyType && (
                await dbContext.Set<AiAgentProfile>().AnyAsync(profile =>
                    (profile.PartyId == existingPartyId && model.PartyType != PartyType.AiAgent) ||
                    (profile.OwnerPartyId == existingPartyId && model.PartyType != PartyType.Person), cancellationToken) ||
                (model.PartyType != PartyType.AiAgent && await dbContext.Set<AiResourceBinding>()
                    .AnyAsync(binding => binding.PartyId == existingPartyId, cancellationToken)))) {
                return Result<PartySaveOperation>.Failure(Error.Validation(
                    "This party's type is required by saved AI profiles or stewardship assignments.", AiStewardEditingErrors.PartyTypeConflict));
            }
            if (existingParty.PartyType != model.PartyType &&
                await PartyRelationshipService.WouldInvalidateSupportPersonAsync(dbContext, existingPartyId, model.PartyType, cancellationToken)) {
                return Result<PartySaveOperation>.Failure(Error.Validation(
                    "This person's type is required by saved recruiting support assignments.", CrmHrEditingErrorCodes.RelationshipSupportTypeConflict));
            }
            dbContext.Entry(existingParty).Property(item => item.UpdatedAtUtc).OriginalValue = model.UpdatedAtUtc.Value;
            party = existingParty;
        }
        else
        {
            party = new Party
            {
                CreatedAtUtc = now
            };
            dbContext.Set<Party>().Add(party);
        }

        PartyLifecycleStatus? previousLifecycleStatus = isNew
            ? null
            : party.LifecycleStatus;

        var normalizedExtendedDataResult = TryNormalizeJson(model.ExtendedDataJson, "{}");
        if (normalizedExtendedDataResult is null)
        {
            return Result<PartySaveOperation>.Failure([Error.Validation("Extended data must be valid JSON.", "crmhr.party.extended-data-invalid")]);
        }

        party.PartyType = model.PartyType;
        party.LifecycleStatus = model.LifecycleStatus;
        party.DisplayName = model.DisplayName.Trim();
        party.LegalName = model.LegalName.Trim();
        party.PreferredName = model.PreferredName.Trim();
        party.ExternalCode = model.ExternalCode.Trim();
        party.Summary = model.Summary.Trim();
        party.Notes = model.Notes.Trim();
        party.TagsJson = JsonSerializer.Serialize(model.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        party.Region = model.Region.Trim();
        party.CountryCode = model.CountryCode.Trim();
        party.TimeZone = model.TimeZone.Trim();
        party.IsSensitive = model.IsSensitive;
        party.ExtendedDataJson = normalizedExtendedDataResult;
        party.LastChangedBy = string.IsNullOrWhiteSpace(model.LastChangedBy) ? "system" : model.LastChangedBy.Trim();
        party.UpdatedAtUtc = CrmHrEditVersions.Next(party.UpdatedAtUtc, now);

        await PartyEditorChildren.ReplaceAsync(dbContext, party.Id, model, party.LastChangedBy, now, cancellationToken);
        var auditAction = isNew
            ? "PartyCreated"
            : previousLifecycleStatus == PartyLifecycleStatus.Archived && party.LifecycleStatus != PartyLifecycleStatus.Archived
                ? "PartyReactivated"
                : previousLifecycleStatus != PartyLifecycleStatus.Archived && party.LifecycleStatus == PartyLifecycleStatus.Archived
                    ? "PartyArchived"
                    : "PartyUpdated";
        var auditSummary = auditAction switch
        {
            "PartyCreated" => $"Created party '{party.DisplayName}'.",
            "PartyArchived" => $"Archived party '{party.DisplayName}'.",
            "PartyReactivated" => $"Reactivated party '{party.DisplayName}'.",
            _ => $"Updated party '{party.DisplayName}'."
        };
        CrmHrAuditWriter.AddEntry(
            dbContext,
            nameof(Party),
            party.Id,
            auditAction,
            auditSummary,
            new
            {
                party.PartyType,
                party.LifecycleStatus,
                party.ExternalCode,
                party.Region,
                party.CountryCode,
                party.TimeZone,
                RoleCount = model.Roles.Count,
                ContactCount = model.ContactPoints.Count,
                AddressCount = model.Addresses.Count,
                ConfidentialNoteCount = confidentialNotes.Count
            },
            party.LastChangedBy,
            party.IsSensitive,
            now);
        return Result<PartySaveOperation>.Success(new PartySaveOperation(
            party.Id,
            auditAction,
            auditSummary,
            party.PartyType,
            party.LifecycleStatus,
            party.LastChangedBy,
            isNew));
    }

    internal async Task PublishPartySavedAsync(PartySaveOperation operation, CancellationToken cancellationToken)
    {
        await UpsertPartySearchDocumentAsync(operation.PartyId, cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                operation.AuditAction,
                operation.AuditSummary,
                $"{operation.PartyType} / {operation.LifecycleStatus}",
                ArtifactKind: nameof(Party),
                ArtifactId: operation.PartyId,
                Route: $"/crm-hr/directory?partyId={operation.PartyId}",
                Actor: operation.Actor),
            cancellationToken);
    }

    internal sealed record PartySaveOperation(
        Guid PartyId,
        string AuditAction,
        string AuditSummary,
        PartyType PartyType,
        PartyLifecycleStatus LifecycleStatus,
        string Actor,
        bool IsNew);

    private static Error PartyChanged() => Error.Failure(
        "The party changed since it was opened. Review the current values before saving.",
        CrmHrEditingErrorCodes.PartyConflict);

    private static string? TryNormalizeJson(string json, string fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ResolvePrimaryContact(
        IReadOnlyList<PartyDirectoryContactValue> contactPoints,
        PartyContactType contactType)
    {
        return contactPoints
            .Where(item => item.ContactType == contactType)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.Id)
            .Select(item => item.Value)
            .FirstOrDefault()
            ?? string.Empty;
    }

    private sealed record PartyDirectoryContactValue(
        Guid Id,
        Guid PartyId,
        PartyContactType ContactType,
        string Value,
        bool IsPrimary);
}

public sealed partial class CrmService(
    IDbContextFactory<CrmHrDbContext> dbContextFactory,
    IClock clock,
    IActivityStream activityStream,
    ISearchIndexService searchIndexService,
    ProjectsService projectsService,
    IProjectRecordQueryService projectRecordQueryService,
    ProjectWriteAdmissionService admissions,
    ProjectPartyIntegrationService assignmentService,
    CrmHrChanges changes,
    CoordinatedDatabaseTransaction opportunityTransaction) : ICrmOpportunityEditorService, ICrmOpportunityConversionService {
    private const string CrmAccountEntityType = "CrmAccount";
    private const string CrmAccountSearchSourceType = "crm-account";
    private const string CrmOpportunityEntityType = "Opportunity";
    private const string CrmOpportunitySearchSourceType = "crm-opportunity";

    internal async Task<CrmRouteSelectionResolution> ResolveRouteSelectionAsync(
        Guid? requestedAccountId,
        Guid? requestedOpportunityId,
        Guid? requestedInteractionId,
        CancellationToken cancellationToken = default)
    {
        if (!requestedOpportunityId.HasValue && !requestedInteractionId.HasValue)
        {
            return CrmRouteSelectionResolution.Resolved(requestedAccountId);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        Guid? opportunityAccountId = null;
        if (requestedOpportunityId.HasValue)
        {
            opportunityAccountId = await dbContext.Set<Opportunity>()
                .Where(item => item.Id == requestedOpportunityId.Value)
                .Select(item => (Guid?)item.AccountPartyId)
                .SingleOrDefaultAsync(cancellationToken);
            if (!opportunityAccountId.HasValue)
            {
                return CrmRouteSelectionResolution.Unresolved(
                    CrmRouteSelectionResolutionFailure.OpportunityNotFound);
            }
        }

        Guid? interactionAccountId = null;
        if (requestedInteractionId.HasValue)
        {
            var interactionAccountIds = await (
                    from interaction in dbContext.Set<InteractionRecord>()
                    where interaction.Id == requestedInteractionId.Value
                    join link in dbContext.Set<InteractionPartyLink>()
                            .Where(item => item.Role == InteractionPartyRole.Account)
                        on interaction.Id equals link.InteractionId
                    select link.PartyId)
                .Distinct()
                .Take(2)
                .ToListAsync(cancellationToken);
            if (interactionAccountIds.Count == 0)
            {
                return CrmRouteSelectionResolution.Unresolved(
                    CrmRouteSelectionResolutionFailure.InteractionNotFound);
            }

            if (interactionAccountIds.Count != 1)
            {
                return CrmRouteSelectionResolution.Unresolved(
                    CrmRouteSelectionResolutionFailure.InteractionAccountAmbiguous);
            }

            interactionAccountId = interactionAccountIds[0];
        }

        var resolvedAccountIds = new[]
            {
                requestedAccountId,
                opportunityAccountId,
                interactionAccountId
            }
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .Take(2)
            .ToList();
        return resolvedAccountIds.Count <= 1
            ? CrmRouteSelectionResolution.Resolved(resolvedAccountIds.SingleOrDefault())
            : CrmRouteSelectionResolution.Unresolved(
                CrmRouteSelectionResolutionFailure.ConflictingAccount);
    }

    public async Task<IReadOnlyList<OpportunitySummaryModel>> ListOpportunitiesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var opportunities = await dbContext.Set<Opportunity>()
            .Select(item => new
            {
                item.Id,
                item.Title,
                item.Stage,
                item.AccountPartyId,
                item.OwnerPartyId,
                item.OpportunitySource,
                item.Amount,
                item.ProbabilityPercent,
                item.ExpectedCloseDateUtc,
                item.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
        var partyIds = opportunities
            .Select(item => item.AccountPartyId)
            .Concat(opportunities.Select(item => item.OwnerPartyId))
            .Distinct()
            .ToList();
        var partiesById = partyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await dbContext.Set<Party>()
                .Where(item => partyIds.Contains(item.Id))
                .Select(item => new
                {
                    item.Id,
                    item.DisplayName
                })
                .ToListAsync(cancellationToken))
                .ToDictionary(item => item.Id, item => item.DisplayName);

        return opportunities
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => new OpportunitySummaryModel(
                item.Id,
                item.Title,
                item.Stage,
                item.AccountPartyId,
                item.OwnerPartyId,
                partiesById.GetValueOrDefault(item.AccountPartyId) ?? "Unknown account",
                partiesById.GetValueOrDefault(item.OwnerPartyId) ?? "Unknown owner",
                item.OpportunitySource,
                item.Amount,
                item.ProbabilityPercent,
                ToDateOnly(item.ExpectedCloseDateUtc),
                item.UpdatedAtUtc))
            .ToList();
    }

    public Task<CrmOpportunityDetailModel?> GetOpportunityAsync(Guid opportunityId, CancellationToken cancellationToken = default) =>
        ReadOpportunityAsync(opportunityId, cancellationToken, includeHistory: true);

    public Task<CrmOpportunityDetailModel?> GetOpportunityForEditingAsync(Guid opportunityId, CancellationToken cancellationToken = default,
        int? maximumPartyLinks = null) {
        if (maximumPartyLinks is < 1 or int.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(maximumPartyLinks));
        }
        return ReadOpportunityAsync(opportunityId, cancellationToken, includeHistory: false, maximumPartyLinks);
    }

    private async Task<CrmOpportunityDetailModel?> ReadOpportunityAsync(Guid opportunityId, CancellationToken cancellationToken,
        bool includeHistory, int? maximumPartyLinks = null) {
        if (opportunityId == Guid.Empty)
        {
            return null;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var observation = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            : null;
        var detail = (await LoadOpportunityDetailsAsync(
                dbContext,
                [opportunityId],
                cancellationToken, includeHistory, maximumPartyLinks))
            .SingleOrDefault();
        if (observation is not null) {
            await observation.CommitAsync(cancellationToken);
        }
        return detail;
    }

    public async Task<int> CountAccountsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<Party>()
            .AsNoTracking()
            .CountAsync(
                party =>
                    party.PartyType == PartyType.Organization &&
                    party.LifecycleStatus != PartyLifecycleStatus.Archived,
                cancellationToken);
    }

    public async Task<CrmAccountProfileEditorModel?> GetAccountProfileAsync(Guid accountPartyId, CancellationToken cancellationToken = default) {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var observation = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            : null;
        var party = await context.Set<Party>().AsNoTracking()
            .Where(item => item.Id == accountPartyId && item.PartyType == PartyType.Organization)
            .Select(item => new { item.Id, item.LifecycleStatus, item.UpdatedAtUtc }).SingleOrDefaultAsync(cancellationToken);
        if (party is null) {
            return null;
        }
        var profile = await context.Set<CrmAccountProfile>().AsNoTracking().SingleOrDefaultAsync(item => item.AccountPartyId == accountPartyId, cancellationToken);
        var roles = profile is null
            ? await context.Set<PartyRoleAssignment>().Where(item => item.PartyId == accountPartyId).Select(item => item.RoleKind).Distinct().ToListAsync(cancellationToken)
            : [];
        return CrmAccountProfileContent.Map(party.Id, party.UpdatedAtUtc, ResolveRelationshipStage(party.LifecycleStatus, roles, profile), profile);
    }

    public async Task<CrmAccountWorkspaceModel?> GetAccountWorkspaceAsync(Guid accountPartyId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var observation = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            : null;
        var account = await dbContext.Set<Party>()
            .Select(item => new
            {
                item.Id,
                item.DisplayName,
                item.Summary,
                item.LifecycleStatus,
                item.TagsJson,
                item.PartyType,
                item.UpdatedAtUtc
            })
            .SingleOrDefaultAsync(item => item.Id == accountPartyId, cancellationToken);

        if (account is null || account.PartyType != PartyType.Organization)
        {
            return null;
        }

        var roleValues = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => item.PartyId == accountPartyId)
            .OrderBy(item => item.RoleKind)
            .Select(item => item.RoleKind)
            .Distinct()
            .ToListAsync(cancellationToken);
        var contactValues = await dbContext.Set<PartyContactPoint>()
            .Where(item => item.PartyId == accountPartyId)
            .Select(item => new CrmPartyContactValue(item.PartyId, item.ContactType, item.Value, item.IsPrimary))
            .ToListAsync(cancellationToken);
        var profile = await dbContext.Set<CrmAccountProfile>()
            .SingleOrDefaultAsync(item => item.AccountPartyId == accountPartyId, cancellationToken);
        var connections = await dbContext.Set<CrmAccountConnection>()
            .AsNoTracking()
            .Where(item => item.AccountPartyId == accountPartyId)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.Role)
            .ToListAsync(cancellationToken);
        var relatedPartyIds = connections.Select(item => item.RelatedPartyId).Distinct().ToList();
        var relatedParties = relatedPartyIds.Count == 0
            ? new Dictionary<Guid, PartyOptionModel>()
            : (await dbContext.Set<Party>()
                .AsNoTracking()
                .Where(item => relatedPartyIds.Contains(item.Id))
                .Select(item => new PartyOptionModel(item.Id, item.DisplayName, item.PartyType))
                .ToListAsync(cancellationToken))
                .ToDictionary(item => item.Id);
        var connectedParties = relatedParties.Values
            .OrderBy(item => item.DisplayName)
            .ToList();
        var connectionIds = connections.Select(item => item.Id).ToList();
        var connectionProjectLinks = connectionIds.Count == 0
            ? []
            : await dbContext.Set<CrmAccountConnectionProjectLink>()
                .AsNoTracking()
                .Where(item => connectionIds.Contains(item.AccountConnectionId))
                .ToListAsync(cancellationToken);
        var projectIds = connectionProjectLinks
            .Select(item => item.ProjectId)
            .Where(projectId => projectId != Guid.Empty)
            .Distinct()
            .ToList();
        var projects = projectIds.Count == 0
            ? new Dictionary<Guid, CrmAccountConnectionProjectItemModel>()
            : (await projectRecordQueryService.GetManyAsync(projectIds, cancellationToken))
                .Select(item => new CrmAccountConnectionProjectItemModel(item.Id, item.Name, item.Status) {
                    Admission = item.LifetimeId is Guid lifetime
                        ? new(admissions.DatabaseProfileId, item.Id, lifetime)
                        : throw new InvalidOperationException($"Connection project '{item.Id}' has no observed lifetime.")
                })
                .ToDictionary(item => item.Id);
        var projectIdsByConnectionId = connectionProjectLinks
            .GroupBy(item => item.AccountConnectionId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.ProjectId).Distinct().ToList());

        var opportunityCount = await dbContext.Set<Opportunity>()
            .AsNoTracking()
            .CountAsync(item => item.AccountPartyId == accountPartyId, cancellationToken);

        var connectedRecords = connections
            .Select(item =>
            {
                var relatedParty = relatedParties.GetValueOrDefault(item.RelatedPartyId)
                    ?? new PartyOptionModel(item.RelatedPartyId, "Unknown party", PartyType.Person);
                var relatedProjects = projectIdsByConnectionId
                    .GetValueOrDefault(item.Id, [])
                    .Select(projectId => projects.GetValueOrDefault(projectId)
                        ?? new CrmAccountConnectionProjectItemModel(
                            projectId,
                            "Unavailable project",
                            ProjectStatus.Archived))
                    .OrderBy(project => project.Name)
                    .ToList();
                return new CrmAccountConnectedRecordItemModel(
                    item.Id,
                    item.RelatedPartyId,
                    relatedParty.DisplayName,
                    relatedParty.PartyType,
                    item.Role,
                    item.IsPrimary,
                    item.Notes,
                    relatedProjects);
            })
            .ToList();
        return new CrmAccountWorkspaceModel(
            account.Id,
            account.DisplayName,
            account.Summary,
            account.LifecycleStatus,
            roleValues,
            TryDeserializeTags(account.TagsJson),
            ResolvePrimaryContact(contactValues, PartyContactType.Email),
            ResolvePrimaryContact(contactValues, PartyContactType.Phone),
            CrmAccountProfileContent.Map(account.Id, account.UpdatedAtUtc, ResolveRelationshipStage(account.LifecycleStatus, roleValues, profile), profile),
            connectedRecords,
            connectedParties,
            opportunityCount) { ConnectionsOriginal = CrmAccountConnectionsContent.Capture(accountPartyId, connections, connectionProjectLinks) };
    }

    public async Task<CrmInteractionDetailModel?> GetAccountInteractionAsync(
        Guid accountPartyId,
        Guid interactionId,
        CancellationToken cancellationToken = default)
    {
        if (accountPartyId == Guid.Empty || interactionId == Guid.Empty)
        {
            return null;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await (
                from interaction in dbContext.Set<InteractionRecord>().AsNoTracking()
                join link in dbContext.Set<InteractionPartyLink>().AsNoTracking()
                    on interaction.Id equals link.InteractionId
                where interaction.Id == interactionId &&
                      link.PartyId == accountPartyId &&
                      link.Role == InteractionPartyRole.Account
                select new CrmInteractionDetailModel(
                    interaction.Id,
                    interaction.InteractionType,
                    interaction.Subject,
                    interaction.RelatedOpportunityId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<CrmActivityHistoryPage> SearchAccountActivityAsync(
        CrmActivityHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var interactions = dbContext.Set<InteractionRecord>()
            .AsNoTracking()
            .Where(interaction => dbContext.Set<InteractionPartyLink>().Any(link =>
                link.PartyId == query.PartyId &&
                link.Role == InteractionPartyRole.Account &&
                link.InteractionId == interaction.Id));
        var auditEntries = dbContext.Set<CrmHrAuditEntry>()
            .AsNoTracking()
            .Where(item =>
                item.EntityType == CrmAccountEntityType &&
                item.EntityId == query.PartyId);

        return await CrmActivityHistoryQueryComposer.SearchAsync(
            dbContext,
            interactions,
            auditEntries,
            query,
            includeParticipantNames: true,
            clock.GetUtcNow(),
            cancellationToken);
    }

    public async Task<Result<Guid>> SaveAccountProfileAsync(CrmAccountProfileEditorModel model, CancellationToken cancellationToken = default) {
        var result = await SaveAccountProfileWithReceiptAsync(model, cancellationToken: cancellationToken);
        return result.IsSuccess
            ? Result<Guid>.Success(result.Value!.AccountPartyId)
            : Result<Guid>.Failure(result.Errors);
    }

    public async Task<Result<CrmAccountProfileSaveReceipt>> SaveAccountProfileWithReceiptAsync(CrmAccountProfileEditorModel model,
        Action<CrmAccountProfileSaveReceipt>? onCommitted = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        model = model.Snapshot();

        if (model.AccountPartyId == Guid.Empty)
        {
            return Result<CrmAccountProfileSaveReceipt>.Failure(Error.Validation("Choose an account before saving CRM details.", "crmhr.crm.account-required"));
        }

        if (model.Original is not { } original || original.PartyUpdatedAtUtc == default ||
            original.ProfileId.HasValue != original.ProfileUpdatedAtUtc.HasValue || model.Id != original.ProfileId) {
            return Result<CrmAccountProfileSaveReceipt>.Failure(Error.Validation(
                "The original account and profile observation is required before saving CRM details.",
                CrmHrEditingErrorCodes.AccountOriginalRequired));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var profileTransaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken) : null;
        var party = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(item => item.Id == model.AccountPartyId && item.PartyType == PartyType.Organization, cancellationToken);
        if (party is null)
        {
            return Result<CrmAccountProfileSaveReceipt>.Failure(Error.Failure("The selected account no longer exists.", "crmhr.crm.account-missing"));
        }

        if (party.UpdatedAtUtc != original.PartyUpdatedAtUtc) {
            return Result<CrmAccountProfileSaveReceipt>.Failure(AccountChanged());
        }
        dbContext.Entry(party).Property(item => item.UpdatedAtUtc).OriginalValue = original.PartyUpdatedAtUtc;

        var now = clock.GetUtcNow();
        var profile = await dbContext.Set<CrmAccountProfile>()
            .SingleOrDefaultAsync(item => item.AccountPartyId == model.AccountPartyId, cancellationToken);
        if (profile?.Id != original.ProfileId || profile?.UpdatedAtUtc != original.ProfileUpdatedAtUtc) {
            return Result<CrmAccountProfileSaveReceipt>.Failure(AccountChanged());
        }
        if (profile is not null) {
            dbContext.Entry(profile).Property(item => item.UpdatedAtUtc).OriginalValue = original.ProfileUpdatedAtUtc!.Value;
        }
        if (profile is null)
        {
            profile = new CrmAccountProfile
            {
                AccountPartyId = model.AccountPartyId,
                CreatedAtUtc = now
            };
            dbContext.Set<CrmAccountProfile>().Add(profile);
        }

        profile.RelationshipStage = model.RelationshipStage;
        profile.CommercialNotes = model.CommercialNotes.Trim();
        profile.ConstraintNotes = model.ConstraintNotes.Trim();
        profile.TimingRiskNotes = model.TimingRiskNotes.Trim();
        profile.LastChangedBy = string.IsNullOrWhiteSpace(model.LastChangedBy) ? "crm-hr-ui" : model.LastChangedBy.Trim();
        profile.UpdatedAtUtc = CrmHrEditVersions.Next(profile.UpdatedAtUtc, now);

        party.LastChangedBy = profile.LastChangedBy;
        party.UpdatedAtUtc = CrmHrEditVersions.Next(party.UpdatedAtUtc, now);
        party.LifecycleStatus = model.RelationshipStage switch
        {
            CrmAccountRelationshipStage.Prospect => PartyLifecycleStatus.Prospect,
            CrmAccountRelationshipStage.ActiveCustomer => PartyLifecycleStatus.Active,
            CrmAccountRelationshipStage.DormantCustomer => PartyLifecycleStatus.Inactive,
            CrmAccountRelationshipStage.LostCustomer => PartyLifecycleStatus.Inactive,
            _ => party.LifecycleStatus
        };

        var existingRoles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => item.PartyId == party.Id)
            .ToListAsync(cancellationToken);
        if (model.RelationshipStage == CrmAccountRelationshipStage.ActiveCustomer &&
            existingRoles.All(item => item.RoleKind != PartyRoleKind.Customer))
        {
            dbContext.Set<PartyRoleAssignment>().Add(new PartyRoleAssignment
            {
                PartyId = party.Id,
                RoleKind = PartyRoleKind.Customer,
                Title = "Customer",
                IsPrimary = existingRoles.Count == 0,
                Notes = "Promoted from CRM relationship stage.",
                ValidFromUtc = now
            });
        }

        AddAuditEntry(
            dbContext,
            party.Id,
            "AccountProfileUpdated",
            $"Updated CRM account profile for '{party.DisplayName}'.",
            new
            {
                model.RelationshipStage,
                profile.CommercialNotes,
                profile.ConstraintNotes,
                profile.TimingRiskNotes
            },
            profile.LastChangedBy,
            party.IsSensitive);

        try {
            await changes.DemandCommitAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await changes.DemandCommitAsync(cancellationToken);
            if (profileTransaction is not null) {
                await profileTransaction.CommitAsync(cancellationToken);
            }
        } catch (DbUpdateConcurrencyException) {
            return Result<CrmAccountProfileSaveReceipt>.Failure(AccountChanged());
        } catch (DbUpdateException exception) when (exception.InnerException is PostgresException {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: AccountProfileUniqueIndex }) {
            return Result<CrmAccountProfileSaveReceipt>.Failure(AccountChanged());
        }
        var accepted = CrmAccountProfileContent.Map(party.Id, party.UpdatedAtUtc, profile.RelationshipStage, profile);
        var receipt = new CrmAccountProfileSaveReceipt(party.Id, accepted);
        onCommitted?.Invoke(receipt);
        changes.PublishParty(receipt.AccountPartyId);
        await UpsertAccountSearchDocumentAsync(party.Id, cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "AccountProfileUpdated",
                $"Updated account profile for {party.DisplayName}",
                $"Relationship stage: {model.RelationshipStage}",
                ArtifactKind: CrmAccountEntityType,
                ArtifactId: party.Id,
                Route: $"/crm-hr/crm?accountId={party.Id}",
                Actor: profile.LastChangedBy),
            cancellationToken);

        return Result<CrmAccountProfileSaveReceipt>.Success(receipt);
    }

    private const string AccountProfileUniqueIndex = "IX_CrmHr_AccountProfiles_AccountPartyId";

    private static Error AccountChanged() => Error.Failure(
        "The account or profile changed since it was opened. Review the current values before saving.",
        CrmHrEditingErrorCodes.AccountConflict);

    public async Task<Result> SaveConnectedRecordsAsync(
        Guid accountPartyId,
        IReadOnlyList<CrmAccountConnectionEditorModel> connectedRecords,
        string actor,
        CancellationToken cancellationToken = default,
        CrmAccountConnectionsObservation? original = null,
        Action<CrmAccountConnectionsSaveReceipt>? onCommitted = null)
    {
        ArgumentNullException.ThrowIfNull(connectedRecords);
        connectedRecords = connectedRecords.Select(row => row.Snapshot()).ToArray();
        if (original is null) {
            return Result.Failure(Error.Validation(
                "The complete original connection observation is required, including an explicitly observed empty set.",
                CrmHrEditingErrorCodes.ConnectionOriginalRequired));
        }
        original = original.Snapshot();
        if (accountPartyId == Guid.Empty)
        {
            return Result.Failure(Error.Validation(
                "Choose an account before saving connected records.",
                "crmhr.crm.account-required"));
        }

        if (original.AccountPartyId != accountPartyId || original.Rows.Any(row => row.Id == Guid.Empty) ||
            original.Rows.Select(row => row.Id).Distinct().Count() != original.Rows.Count ||
            original.Projects.Any(link => link.Id == Guid.Empty || link.ProjectId == Guid.Empty || !original.Rows.Any(row => row.Id == link.AccountConnectionId)) ||
            original.Projects.Select(link => link.Id).Distinct().Count() != original.Projects.Count) {
            return Result.Failure(Error.Validation("The connection original does not identify a complete distinct set for this account.",
                CrmHrEditingErrorCodes.ConnectionIdentityInvalid));
        }
        await changes.DemandCommitAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await SerializableMutationScope.BeginAsync(dbContext,
            connectedRecords.SelectMany(row => row.ProjectIds).Concat(original.Projects.Select(row => row.ProjectId))
                .Distinct().Select(ProjectMutationScopeKeys.ForProject)
                .Append(CrmAccountConnectionsContent.ScopeKey(accountPartyId))
                .Append(OpportunityPartyReferences.ScopeKey(accountPartyId)).ToArray(), cancellationToken);
        using var ownerFrame = opportunityTransaction.Enter(dbContext);
        var party = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(item => item.Id == accountPartyId && item.PartyType == PartyType.Organization, cancellationToken);
        if (party is null)
        {
            return Result.Failure(Error.Failure("The selected account no longer exists.", "crmhr.crm.account-missing"));
        }

        var normalizedConnections = connectedRecords
            .Where(item => item.RelatedPartyId != Guid.Empty)
            .Select(item => new CrmAccountConnectionEditorModel
            {
                Id = item.Id,
                RelatedPartyId = item.RelatedPartyId,
                Role = item.Role,
                IsPrimary = item.IsPrimary,
                Notes = item.Notes.Trim(),
                ProjectIds = item.ProjectIds
                    .Where(projectId => projectId != Guid.Empty)
                    .Distinct()
                    .ToList(),
                ProjectAdmissions = new(item.ProjectAdmissions)
            })
            .ToList();
        if (normalizedConnections.Any(item => item.RelatedPartyId == accountPartyId))
        {
            return Result.Failure(Error.Validation(
                "An account cannot be connected to itself.",
                "crmhr.crm.connection-self-reference"));
        }

        var duplicateConnection = normalizedConnections
            .GroupBy(item => new { item.RelatedPartyId, item.Role })
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateConnection is not null)
        {
            return Result.Failure(Error.Validation(
                "The same directory record and relationship role can only be connected once.",
                "crmhr.crm.connection-duplicate"));
        }

        var relatedPartyIds = normalizedConnections
            .Select(item => item.RelatedPartyId)
            .Distinct()
            .ToList();
        var existingRelatedPartyIds = relatedPartyIds.Count == 0
            ? []
            : await dbContext.Set<Party>()
                .AsNoTracking()
                .Where(item => relatedPartyIds.Contains(item.Id))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
        if (existingRelatedPartyIds.Count != relatedPartyIds.Count)
        {
            return Result.Failure(Error.Validation(
                "One or more connected directory records no longer exist.",
                "crmhr.crm.connection-party-missing"));
        }

        var requestedProjectIds = normalizedConnections
            .SelectMany(item => item.ProjectIds)
            .Distinct()
            .ToList();
        var existingProjectIds = requestedProjectIds.Count == 0
            ? []
            : (await projectRecordQueryService.GetManyAsync(requestedProjectIds, cancellationToken))
                .Select(item => item.Id).ToList();
        if (existingProjectIds.Count != requestedProjectIds.Count)
        {
            return Result.Failure(Error.Validation(
                "One or more related projects no longer exist.",
                "crmhr.crm.connection-project-missing"));
        }
        foreach (var projectId in requestedProjectIds) {
            var bindings = normalizedConnections.Where(row => row.ProjectIds.Contains(projectId))
                .Select(row => row.ProjectAdmissions.GetValueOrDefault(projectId)).Distinct().ToArray();
            if (bindings.Length != 1 || bindings[0] is not { } project || project.ProjectId != projectId) {
                return Result.Failure(Error.Validation("Each selected project requires its originally captured lifetime.",
                    CrmHrEditingErrorCodes.ConnectionProjectOriginalRequired));
            }
            await admissions.RequireForMutationAsync(project, cancellationToken);
        }

        var existingConnections = await dbContext.Set<CrmAccountConnection>()
            .Where(item => item.AccountPartyId == accountPartyId)
            .ToListAsync(cancellationToken);
        var existingConnectionIds = existingConnections.Select(item => item.Id).ToList();
        var existingProjectLinks = existingConnectionIds.Count == 0
            ? []
            : await dbContext.Set<CrmAccountConnectionProjectLink>()
                .Where(item => existingConnectionIds.Contains(item.AccountConnectionId))
                .ToListAsync(cancellationToken);

        if (!CrmAccountConnectionsContent.Matches(original,
                CrmAccountConnectionsContent.Capture(accountPartyId, existingConnections, existingProjectLinks))) {
            return Result.Failure(CrmAccountConnectionsContent.Conflict());
        }

        var now = clock.GetUtcNow();
        var normalizedActor = string.IsNullOrWhiteSpace(actor) ? "crm-hr-ui" : actor.Trim();
        var persistedEditorIds = normalizedConnections
            .Where(item => item.Id.HasValue)
            .Select(item => item.Id!.Value)
            .ToList();
        if (persistedEditorIds.Count != persistedEditorIds.Distinct().Count())
        {
            return Result.Failure(Error.Validation(
                "The same saved connection cannot appear more than once.",
                "crmhr.crm.connection-id-duplicate"));
        }

        var existingConnectionById = existingConnections.ToDictionary(item => item.Id);
        var newIds = persistedEditorIds.Where(id => !existingConnectionById.ContainsKey(id)).ToArray();
        if (newIds.Contains(Guid.Empty) || await dbContext.Set<CrmAccountConnection>().AsNoTracking().AnyAsync(row => newIds.Contains(row.Id), cancellationToken))
        {
            return Result.Failure(Error.Validation(
                "A new connection identity already exists or belongs to another account. Review its current state.",
                "crmhr.crm.connection-stale"));
        }

        var requestedIds = persistedEditorIds.ToHashSet();
        var removedConnections = existingConnections
            .Where(item => !requestedIds.Contains(item.Id))
            .ToList();
        if (removedConnections.Count > 0)
        {
            dbContext.Set<CrmAccountConnection>().RemoveRange(removedConnections);
        }

        foreach (var editor in normalizedConnections)
        {
            var connection = editor.Id is Guid connectionId &&
                             existingConnectionById.TryGetValue(connectionId, out var existingConnection)
                ? existingConnection
                : new CrmAccountConnection
                {
                    Id = editor.Id ?? Guid.NewGuid(),
                    AccountPartyId = accountPartyId,
                    CreatedAtUtc = now
                };
            if (connection.AccountPartyId != accountPartyId)
            {
                return Result.Failure(Error.Validation(
                    "A connected record does not belong to the selected account.",
                    "crmhr.crm.connection-account-mismatch"));
            }

            connection.RelatedPartyId = editor.RelatedPartyId;
            connection.Role = editor.Role;
            connection.IsPrimary = editor.IsPrimary;
            connection.Notes = editor.Notes;
            connection.UpdatedAtUtc = CrmHrEditVersions.Next(connection.UpdatedAtUtc, now);
            if (dbContext.Entry(connection).State == EntityState.Detached)
            {
                dbContext.Set<CrmAccountConnection>().Add(connection);
            }

            var currentProjectLinks = existingProjectLinks
                .Where(item => item.AccountConnectionId == connection.Id)
                .ToList();
            var desiredProjectIds = editor.ProjectIds.ToHashSet();
            var removedProjectLinks = currentProjectLinks
                .Where(item => !desiredProjectIds.Contains(item.ProjectId))
                .ToList();
            if (removedProjectLinks.Count > 0)
            {
                dbContext.Set<CrmAccountConnectionProjectLink>().RemoveRange(removedProjectLinks);
            }

            var currentProjectIds = currentProjectLinks
                .Select(item => item.ProjectId)
                .ToHashSet();
            dbContext.Set<CrmAccountConnectionProjectLink>().AddRange(
                desiredProjectIds
                    .Where(projectId => !currentProjectIds.Contains(projectId))
                    .Select(projectId => new CrmAccountConnectionProjectLink
                    {
                        AccountConnectionId = connection.Id,
                        ProjectId = projectId,
                        CreatedAtUtc = now
                    }));
        }

        AddAuditEntry(
            dbContext,
            accountPartyId,
            "AccountConnectionsUpdated",
            $"Updated CRM connected records for '{party.DisplayName}'.",
            normalizedConnections.Select(item => new
            {
                item.RelatedPartyId,
                item.Role,
                item.IsPrimary,
                item.Notes,
                item.ProjectIds
            }),
            normalizedActor,
            party.IsSensitive);

        await changes.DemandCommitAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        var acceptedOriginal = await CrmAccountConnectionsContent.ReadAsync(dbContext, accountPartyId, cancellationToken);
        var accepted = acceptedOriginal.Rows.Select(row => new CrmAccountConnectionEditorModel {
            Id = row.Id, RelatedPartyId = row.RelatedPartyId, Role = row.Role, IsPrimary = row.IsPrimary, Notes = row.Notes,
            ProjectIds = acceptedOriginal.Projects.Where(link => link.AccountConnectionId == row.Id).Select(link => link.ProjectId).ToList(),
            ProjectAdmissions = connectedRecords.SelectMany(source => source.ProjectAdmissions).Where(binding =>
                acceptedOriginal.Projects.Any(link => link.AccountConnectionId == row.Id && link.ProjectId == binding.Key))
                .DistinctBy(binding => binding.Key).ToDictionary()
        }).ToArray();
        await changes.DemandCommitAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        onCommitted?.Invoke(new(accountPartyId, accepted, acceptedOriginal));
        changes.PublishAccountRecords(accountPartyId, EditingChangeKind.EditableContentChanged);
        await UpsertAccountSearchDocumentAsync(accountPartyId, cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "AccountConnectionsUpdated",
                $"Updated connected records for {party.DisplayName}",
                $"{normalizedConnections.Count} account connection(s) saved.",
                ArtifactKind: CrmAccountEntityType,
                ArtifactId: accountPartyId,
                Route: $"/crm-hr/crm?accountId={accountPartyId}",
                Actor: normalizedActor),
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result<Guid>> AddInteractionAsync(
        Guid accountPartyId,
        CrmInteractionEditorModel model,
        string actor,
        CancellationToken cancellationToken = default,
        Action<CrmInteractionAppendReceipt>? onCommitted = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        model = model.Snapshot();

        if (accountPartyId == Guid.Empty)
        {
            return Result<Guid>.Failure(Error.Validation("Choose an account before logging an interaction.", "crmhr.crm.account-required"));
        }

        if (string.IsNullOrWhiteSpace(model.Subject))
        {
            return Result<Guid>.Failure(Error.Validation("Interaction subject is required.", "crmhr.crm.interaction-subject-required"));
        }

        if (!string.IsNullOrWhiteSpace(model.NextActionText) && !model.NextActionOwnerPartyId.HasValue)
        {
            return Result<Guid>.Failure(Error.Validation("Choose a next-action owner when a next action is provided.", "crmhr.crm.next-action-owner-required"));
        }
        if (!Enum.IsDefined(model.InteractionType) || model.Subject.Trim().Length > 200 || model.NextActionText.Trim().Length > 240 || model.NextActionOwnerPartyId == Guid.Empty) {
            return Result<Guid>.Failure(Error.Validation("Choose a valid interaction type and owner, a subject up to 200 characters and next-action text up to 240 characters.",
                CrmHrEditingErrorCodes.InteractionInputInvalid));
        }

        await changes.DemandCommitAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await SerializableMutationScope.BeginAsync(dbContext,
            [OpportunityPartyReferences.ScopeKey(accountPartyId)], cancellationToken);
        var party = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(item => item.Id == accountPartyId && item.PartyType == PartyType.Organization, cancellationToken);
        if (party is null)
        {
            return Result<Guid>.Failure(Error.Failure("The selected account no longer exists.", "crmhr.crm.account-missing"));
        }
        if (model.RelatedOpportunityId is Guid opportunityId && !await dbContext.Set<Opportunity>().AsNoTracking()
                .AnyAsync(row => row.Id == opportunityId && row.AccountPartyId == accountPartyId, cancellationToken)) {
            return Result<Guid>.Failure(Error.Validation("The selected opportunity is unavailable or belongs to another account.",
                CrmHrEditingErrorCodes.InteractionReferenceInvalid));
        }
        var referencedParties = model.ParticipantPartyIds.Where(id => id != Guid.Empty && id != accountPartyId)
            .Concat(model.NextActionOwnerPartyId is Guid ownerId ? [ownerId] : []).Distinct().ToArray();
        if (referencedParties.Length > 0 && await dbContext.Set<Party>().AsNoTracking()
                .CountAsync(row => referencedParties.Contains(row.Id), cancellationToken) != referencedParties.Length) {
            return Result<Guid>.Failure(Error.Validation("A selected participant or next-action owner is unavailable. Choose its current directory record.",
                CrmHrEditingErrorCodes.InteractionReferenceInvalid));
        }

        var now = clock.GetUtcNow();
        var normalizedActor = string.IsNullOrWhiteSpace(actor) ? "crm-hr-ui" : actor.Trim();
        var connectionRoles = await dbContext.Set<CrmAccountConnection>()
            .Where(item => item.AccountPartyId == accountPartyId && model.ParticipantPartyIds.Contains(item.RelatedPartyId))
            .ToListAsync(cancellationToken);
        var connectionRoleByPartyId = connectionRoles
            .GroupBy(item => item.RelatedPartyId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Role).First());

        var interaction = new InteractionRecord
        {
            InteractionType = model.InteractionType,
            Subject = model.Subject.Trim(),
            OccurredAtUtc = ToUtcDate(model.OccurredOn),
            Summary = model.Summary.Trim(),
            Notes = model.Notes.Trim(),
            NextActionText = model.NextActionText.Trim(),
            NextActionOwnerPartyId = model.NextActionOwnerPartyId,
            NextActionDueUtc = model.NextActionDueOn.HasValue ? ToUtcDate(model.NextActionDueOn.Value) : null,
            RelatedOpportunityId = model.RelatedOpportunityId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Set<InteractionRecord>().Add(interaction);
        dbContext.Set<InteractionPartyLink>().Add(new InteractionPartyLink
        {
            InteractionId = interaction.Id,
            PartyId = accountPartyId,
            Role = InteractionPartyRole.Account
        });

        foreach (var participantPartyId in model.ParticipantPartyIds.Distinct().Where(item => item != Guid.Empty && item != accountPartyId))
        {
            dbContext.Set<InteractionPartyLink>().Add(new InteractionPartyLink
            {
                InteractionId = interaction.Id,
                PartyId = participantPartyId,
                Role = ResolveInteractionRole(connectionRoleByPartyId.GetValueOrDefault(participantPartyId))
            });
        }

        AddAuditEntry(
            dbContext,
            accountPartyId,
            "InteractionLogged",
            $"Logged {model.InteractionType} interaction '{interaction.Subject}' for '{party.DisplayName}'.",
            new
            {
                interaction.Subject,
                interaction.InteractionType,
                model.ParticipantPartyIds,
                interaction.NextActionText,
                interaction.NextActionDueUtc,
                interaction.NextActionOwnerPartyId
            },
            normalizedActor,
            party.IsSensitive);

        await changes.DemandCommitAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await changes.DemandCommitAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var accepted = model.Snapshot();
        accepted.Subject = interaction.Subject;
        accepted.Summary = interaction.Summary;
        accepted.Notes = interaction.Notes;
        accepted.NextActionText = interaction.NextActionText;
        accepted.ParticipantPartyIds = model.ParticipantPartyIds.Distinct()
            .Where(id => id != Guid.Empty && id != accountPartyId).ToList();
        onCommitted?.Invoke(new(accountPartyId, interaction.Id, accepted));
        changes.PublishAccountRecords(accountPartyId, EditingChangeKind.Created);
        await UpsertAccountSearchDocumentAsync(accountPartyId, cancellationToken);
        await UpsertInteractionSearchDocumentAsync(interaction.Id, cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "InteractionLogged",
                $"Logged {interaction.InteractionType} for {party.DisplayName}",
                interaction.Subject,
                ArtifactKind: nameof(InteractionRecord),
                ArtifactId: interaction.Id,
                Route: $"/crm-hr/crm?accountId={accountPartyId}",
                Actor: normalizedActor),
            cancellationToken);

        return Result<Guid>.Success(interaction.Id);
    }

    public async Task<Result<CrmOpportunityStageReceipt>> AdvanceOpportunityStageAsync(CrmOpportunityStageAdvance intent, string actor,
        CancellationToken cancellationToken = default, Action<CrmOpportunityStageReceipt>? onCommitted = null) {
        ArgumentNullException.ThrowIfNull(intent);
        if (intent.OpportunityId == Guid.Empty || intent.AccountPartyId == Guid.Empty || intent.ExpectedUpdatedAtUtc == default ||
            intent.DisplayedStage is OpportunityStage.Won or OpportunityStage.Lost) {
            return Result<CrmOpportunityStageReceipt>.Failure(Error.Validation("Choose an open, observed opportunity before advancing.", CrmHrEditingErrorCodes.OpportunityConflict));
        }
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var stageTransaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken) : null;
        var opportunity = await context.Set<Opportunity>().SingleOrDefaultAsync(row => row.Id == intent.OpportunityId, cancellationToken);
        if (opportunity is null || opportunity.AccountPartyId != intent.AccountPartyId || opportunity.Stage != intent.DisplayedStage ||
            opportunity.UpdatedAtUtc != intent.ExpectedUpdatedAtUtc) {
            return Result<CrmOpportunityStageReceipt>.Failure(Error.Failure("The displayed opportunity changed. Refresh the pipeline before advancing.", CrmHrEditingErrorCodes.OpportunityConflict));
        }
        var account = await context.Set<Party>().SingleAsync(row => row.Id == intent.AccountPartyId, cancellationToken);
        var normalizedActor = NormalizeActor(actor);
        var now = CrmHrEditVersions.Normalize(clock.GetUtcNow());
        opportunity.Stage = intent.NextStage;
        opportunity.UpdatedAtUtc = CrmHrEditVersions.Next(opportunity.UpdatedAtUtc, now);
        account.LastChangedBy = normalizedActor;
        account.UpdatedAtUtc = CrmHrEditVersions.Next(account.UpdatedAtUtc, now);
        context.Set<OpportunityStageHistory>().Add(new() {
            OpportunityId = opportunity.Id, Stage = opportunity.Stage, ChangedAtUtc = now, ChangedBy = normalizedActor,
            Notes = $"Advanced from {intent.DisplayedStage} to {opportunity.Stage}.",
            RecognizedAmount = opportunity.Stage == OpportunityStage.Won ? opportunity.Amount : null,
            RecognizedCurrencyCode = opportunity.Stage == OpportunityStage.Won ? opportunity.CurrencyCode : string.Empty
        });
        AddAuditEntry(context, account.Id, "OpportunityStageChanged", $"Moved opportunity '{opportunity.Title}' to {opportunity.Stage}.",
            new { opportunity.Id, opportunity.Stage }, normalizedActor, account.IsSensitive);
        try {
            await changes.DemandCommitAsync(cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await changes.DemandCommitAsync(cancellationToken);
            if (stageTransaction is not null) {
                await stageTransaction.CommitAsync(cancellationToken);
            }
        } catch (DbUpdateConcurrencyException) {
            return Result<CrmOpportunityStageReceipt>.Failure(Error.Failure("The opportunity changed while advancing. Refresh the pipeline.", CrmHrEditingErrorCodes.OpportunityConflict));
        }
        var receipt = new CrmOpportunityStageReceipt(opportunity.Id, opportunity.Stage, opportunity.UpdatedAtUtc);
        onCommitted?.Invoke(receipt);
        changes.PublishOpportunity(opportunity.Id);
        changes.PublishParty(account.Id);
        await UpsertAccountSearchDocumentAsync(account.Id, cancellationToken);
        await UpsertOpportunitySearchDocumentAsync(opportunity.Id, cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest("CRM / HR", "OpportunityStageChanged", $"Updated opportunity {opportunity.Title}",
            $"{account.DisplayName} / {opportunity.Stage}", ArtifactKind: CrmOpportunityEntityType, ArtifactId: opportunity.Id,
            Route: BuildOpportunityRoute(account.Id, opportunity.Id), Actor: normalizedActor), cancellationToken);
        return Result<CrmOpportunityStageReceipt>.Success(receipt);
    }

    public async Task<Result<Guid>> SaveOpportunityAsync(CrmOpportunityEditorModel model, CancellationToken cancellationToken = default) {
        var result = await SaveOpportunityWithReceiptAsync(model, cancellationToken);
        return result.IsSuccess ? Result<Guid>.Success(result.Value!.OpportunityId) : Result<Guid>.Failure(result.Errors);
    }

    public async Task<Result<CrmOpportunitySaveReceipt>> SaveOpportunityWithReceiptAsync(CrmOpportunityEditorModel model,
        CancellationToken cancellationToken = default, Action<CrmOpportunitySaveReceipt>? onCommitted = null) {
        ArgumentNullException.ThrowIfNull(model);
        model = model.Snapshot();

        if (model.AccountPartyId == Guid.Empty)
        {
            return Result<CrmOpportunitySaveReceipt>.Failure(Error.Validation("Choose an account before saving an opportunity.", "crmhr.crm.opportunity-account-required"));
        }

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            return Result<CrmOpportunitySaveReceipt>.Failure(Error.Validation("Opportunity title is required.", "crmhr.crm.opportunity-title-required"));
        }

        if (model.OwnerPartyId == Guid.Empty)
        {
            return Result<CrmOpportunitySaveReceipt>.Failure(Error.Validation("Choose an owner before saving an opportunity.", "crmhr.crm.opportunity-owner-required"));
        }

        if (model.ProbabilityPercent is < 0 or > 100)
        {
            return Result<CrmOpportunitySaveReceipt>.Failure(Error.Validation("Probability must stay between 0 and 100.", "crmhr.crm.opportunity-probability-range"));
        }

        if (model.Stage == OpportunityStage.Lost && string.IsNullOrWhiteSpace(model.LostReason))
        {
            return Result<CrmOpportunitySaveReceipt>.Failure(Error.Validation("Lost opportunities require a loss reason.", "crmhr.crm.opportunity-lost-reason-required"));
        }

        var normalizedCurrencyCode = NormalizeOpportunityCurrencyCode(model.CurrencyCode);
        if (normalizedCurrencyCode is null)
        {
            return Result<CrmOpportunitySaveReceipt>.Failure(Error.Validation(
                "Currency must be a three-letter ASCII code.",
                "crmhr.crm.opportunity-currency-invalid"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var referenceTransaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken) : null;
        using var opportunityEntry = opportunityTransaction.Enter(dbContext);
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(dbContext,
            model.Parties.Select(link => link.PartyId).Append(model.AccountPartyId).Append(model.OwnerPartyId)
                .Concat(model.DeliveryUnitPartyId is Guid deliveryId ? [deliveryId] : []).Distinct()
                .Select(OpportunityPartyReferences.ScopeKey)
                .Concat(model.LinkedProjectId is Guid linkedId ? [ProjectMutationScopeKeys.ForProject(linkedId)] : []).ToArray(), cancellationToken);
        var account = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(
                item => item.Id == model.AccountPartyId && item.PartyType == PartyType.Organization,
                cancellationToken);
        if (account is null)
        {
            return Result<CrmOpportunitySaveReceipt>.Failure(Error.Failure("The selected account no longer exists.", "crmhr.crm.opportunity-account-missing"));
        }

        var entity = model.Id.HasValue
            ? await dbContext.Set<Opportunity>().SingleOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;
        if (model.Id.HasValue && entity is null)
        {
            return Result<CrmOpportunitySaveReceipt>.Failure(Error.Failure(
                "The opportunity no longer exists.",
                "crmhr.crm.opportunity-missing"));
        }

        if (entity is not null && !model.ExpectedUpdatedAtUtc.HasValue)
        {
            return Result<CrmOpportunitySaveReceipt>.Failure(Error.Validation(
                "Reload the opportunity before saving it.",
                "crmhr.crm.opportunity-expected-updated-at-required"));
        }

        if (entity is not null && entity.UpdatedAtUtc != model.ExpectedUpdatedAtUtc)
        {
            return Result<CrmOpportunitySaveReceipt>.Failure(Error.Failure(
                "The opportunity changed after it was loaded. Reload it before saving.",
                "crmhr.crm.opportunity-concurrency-conflict"));
        }

        if (entity is not null && entity.AccountPartyId != model.AccountPartyId)
        {
            return Result<CrmOpportunitySaveReceipt>.Failure(Error.Validation("Move the account selection before editing a different opportunity.", "crmhr.crm.opportunity-account-mismatch"));
        }

        if (entity is not null && entity.Stage == model.Stage && !string.IsNullOrWhiteSpace(model.StageNotes)) {
            return Result<CrmOpportunitySaveReceipt>.Failure(Error.Validation(
                "A stage note requires a stage change. Keep ordinary updates in Notes.", CrmHrEditingErrorCodes.OpportunityStageNoteWithoutTransition));
        }

        var normalizedLinks = model.Parties
            .Where(item => item.PartyId != Guid.Empty && item.PartyId != model.AccountPartyId)
            .GroupBy(item => new { item.PartyId, item.Role })
            .Select(group => group.First())
            .ToList();
        var referencedPartyIds = normalizedLinks
            .Select(item => item.PartyId)
            .Append(model.OwnerPartyId)
            .Concat(model.DeliveryUnitPartyId.HasValue ? [model.DeliveryUnitPartyId.Value] : [])
            .Distinct()
            .ToList();
        var knownParties = referencedPartyIds.Count == 0
            ? []
            : await dbContext.Set<Party>()
                .Where(item => referencedPartyIds.Contains(item.Id))
                .Select(item => new PartyOptionModel(item.Id, item.DisplayName, item.PartyType))
                .ToListAsync(cancellationToken);
        if (knownParties.Count != referencedPartyIds.Count)
        {
            return Result<CrmOpportunitySaveReceipt>.Failure(Error.Failure("One or more selected opportunity parties no longer exist.", "crmhr.crm.opportunity-party-missing"));
        }

        var knownPartiesById = knownParties.ToDictionary(item => item.Id);
        var owner = knownPartiesById[model.OwnerPartyId];
        if (owner.PartyType is not (PartyType.Person or PartyType.OrganizationUnit or PartyType.AiAgent))
        {
            return Result<CrmOpportunitySaveReceipt>.Failure(Error.Validation(
                "Opportunity owners must be a person, organization unit, or AI agent.",
                "crmhr.crm.opportunity-owner-type-invalid"));
        }

        if (model.DeliveryUnitPartyId is Guid deliveryUnitPartyId &&
            knownPartiesById[deliveryUnitPartyId].PartyType != PartyType.OrganizationUnit)
        {
            return Result<CrmOpportunitySaveReceipt>.Failure(Error.Validation(
                "Opportunity delivery units must be organization units.",
                "crmhr.crm.opportunity-delivery-unit-type-invalid"));
        }

        if (model.LinkedProjectId.HasValue)
        {
            if (model.LinkedProjectAdmission is not { } observedProject || observedProject.ProjectId != model.LinkedProjectId) {
                return Result<CrmOpportunitySaveReceipt>.Failure(Error.Validation(
                    "Choose the linked project again to observe its current lifetime.", CrmHrEditingErrorCodes.OpportunityProjectOriginalRequired));
            }
            try {
                await admissions.RequireForMutationAsync(observedProject, cancellationToken);
            } catch (ProjectWriteAdmissionRejectedException) {
                return Result<CrmOpportunitySaveReceipt>.Failure(Error.Failure(
                    "The linked project was retired or replaced. Review this reference before saving.", CrmHrEditingErrorCodes.OpportunityProjectStale));
            }
            var linkedProject = await projectRecordQueryService.GetAsync(
                model.LinkedProjectId.Value,
                cancellationToken);
            if (linkedProject is null)
            {
                return Result<CrmOpportunitySaveReceipt>.Failure(Error.Validation("The linked project was not found.", "crmhr.crm.opportunity-linked-project-missing"));
            }
        }

        var normalizedActor = NormalizeActor(model.LastChangedBy);
        var now = CrmHrEditVersions.Normalize(clock.GetUtcNow());
        var isNew = !model.Id.HasValue;
        var previousStage = entity?.Stage;

        if (entity is null)
        {
            entity = new Opportunity
            {
                AccountPartyId = model.AccountPartyId,
                CreatedAtUtc = now
            };
            dbContext.Set<Opportunity>().Add(entity);
        }

        var accountProfile = await dbContext.Set<CrmAccountProfile>()
            .SingleOrDefaultAsync(item => item.AccountPartyId == model.AccountPartyId, cancellationToken);
        var accountRoles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => item.PartyId == model.AccountPartyId)
            .Select(item => item.RoleKind)
            .ToListAsync(cancellationToken);

        entity.Title = model.Title.Trim();
        entity.Stage = model.Stage;
        entity.RelationshipStage = string.IsNullOrWhiteSpace(model.RelationshipStage)
            ? ResolveRelationshipStage(account.LifecycleStatus, accountRoles, accountProfile).ToString()
            : model.RelationshipStage.Trim();
        entity.OpportunitySource = model.OpportunitySource;
        entity.OwnerPartyId = model.OwnerPartyId;
        entity.DeliveryUnitPartyId = model.DeliveryUnitPartyId;
        entity.LinkedProjectId = model.LinkedProjectId;
        entity.CurrencyCode = normalizedCurrencyCode;
        entity.Amount = model.Amount;
        entity.ProbabilityPercent = model.ProbabilityPercent;
        entity.ExpectedCloseDateUtc = model.ExpectedCloseOn.HasValue ? ToUtcDate(model.ExpectedCloseOn.Value) : null;
        entity.LostReason = model.Stage == OpportunityStage.Lost ? model.LostReason.Trim() : string.Empty;
        entity.Summary = model.Summary.Trim();
        entity.Notes = model.Notes.Trim();
        entity.ExtendedDataJson = JsonSerializer.Serialize(new CrmOpportunityExtendedDataModel
        {
            CompetitorName = model.CompetitorName.Trim(),
            PartnerContributionSummary = model.PartnerContributionSummary.Trim()
        });
        entity.UpdatedAtUtc = isNew ? now : CrmHrEditVersions.Next(entity.UpdatedAtUtc, now);

        var existingLinks = await dbContext.Set<OpportunityPartyLink>()
            .Where(item => item.OpportunityId == entity.Id)
            .ToListAsync(cancellationToken);
        var suppliedIds = normalizedLinks.Where(link => link.Id.HasValue).Select(link => link.Id!.Value).ToArray();
        if (suppliedIds.Contains(Guid.Empty) || suppliedIds.Distinct().Count() != suppliedIds.Length ||
            await dbContext.Set<OpportunityPartyLink>().AnyAsync(link => suppliedIds.Contains(link.Id) && link.OpportunityId != entity.Id, cancellationToken)) {
            return Result<CrmOpportunitySaveReceipt>.Failure(Error.Validation(
                "Linked-party row identities must be distinct and belong to this opportunity.", CrmHrEditingErrorCodes.OpportunityChildIdentityInvalid));
        }
        var existingById = existingLinks.ToDictionary(link => link.Id);
        dbContext.Set<OpportunityPartyLink>().RemoveRange(existingLinks.Where(link => !suppliedIds.Contains(link.Id)));
        var savedLinks = new List<OpportunityPartyLink>(normalizedLinks.Count);
        foreach (var input in normalizedLinks) {
            var link = input.Id is Guid linkId && existingById.TryGetValue(linkId, out var existing) ? existing :
                new OpportunityPartyLink { Id = input.Id ?? Guid.NewGuid(), OpportunityId = entity.Id };
            link.PartyId = input.PartyId;
            link.Role = input.Role;
            if (dbContext.Entry(link).State == EntityState.Detached) {
                dbContext.Set<OpportunityPartyLink>().Add(link);
            }
            savedLinks.Add(link);
        }

        if (isNew || previousStage != model.Stage)
        {
            dbContext.Set<OpportunityStageHistory>().Add(new OpportunityStageHistory
            {
                OpportunityId = entity.Id,
                Stage = model.Stage,
                ChangedAtUtc = now,
                ChangedBy = normalizedActor,
                Notes = model.StageNotes.Trim(),
                RecognizedAmount = model.Stage == OpportunityStage.Won ? model.Amount : null,
                RecognizedCurrencyCode = model.Stage == OpportunityStage.Won
                    ? normalizedCurrencyCode
                    : string.Empty
            });
        }

        account.LastChangedBy = normalizedActor;
        account.UpdatedAtUtc = CrmHrEditVersions.Next(account.UpdatedAtUtc, now);

        var auditAction = isNew
            ? "OpportunityCreated"
            : previousStage != model.Stage
                ? "OpportunityStageChanged"
                : "OpportunityUpdated";
        var auditSummary = isNew
            ? $"Created opportunity '{entity.Title}' for '{account.DisplayName}'."
            : previousStage != model.Stage
                ? $"Moved opportunity '{entity.Title}' to {entity.Stage}."
                : $"Updated opportunity '{entity.Title}'.";

        AddAuditEntry(
            dbContext,
            account.Id,
            auditAction,
            auditSummary,
            new
            {
                entity.Id,
                entity.Title,
                entity.Stage,
                entity.OpportunitySource,
                entity.Amount,
                entity.ProbabilityPercent,
                entity.ExpectedCloseDateUtc,
                entity.LinkedProjectId,
                PartyCount = normalizedLinks.Count
            },
            normalizedActor,
            account.IsSensitive);

        try {
            await changes.DemandCommitAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (referenceTransaction is not null) {
                await changes.DemandCommitAsync(cancellationToken);
                await referenceTransaction.CommitAsync(cancellationToken);
            }
        } catch (DbUpdateConcurrencyException) {
            return Result<CrmOpportunitySaveReceipt>.Failure(Error.Failure(
                "The opportunity changed while it was being saved. Reload it before retrying.",
                "crmhr.crm.opportunity-concurrency-conflict"));
        }

        var receipt = CaptureOpportunityAcceptance(entity, model, savedLinks, normalizedActor);
        onCommitted?.Invoke(receipt);
        changes.PublishOpportunity(entity.Id, isNew ? EditingChangeKind.Created : EditingChangeKind.EditableContentChanged);
        changes.PublishParty(account.Id);
        await UpsertAccountSearchDocumentAsync(account.Id, cancellationToken);
        await UpsertOpportunitySearchDocumentAsync(entity.Id, cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                auditAction,
                isNew ? $"Created opportunity {entity.Title}" : $"Updated opportunity {entity.Title}",
                $"{account.DisplayName} / {entity.Stage} / {entity.OpportunitySource}",
                ArtifactKind: CrmOpportunityEntityType,
                ArtifactId: entity.Id,
                Route: BuildOpportunityRoute(account.Id, entity.Id),
                Actor: normalizedActor),
            cancellationToken);

        return Result<CrmOpportunitySaveReceipt>.Success(receipt);
    }

    private static CrmOpportunitySaveReceipt CaptureOpportunityAcceptance(Opportunity entity, CrmOpportunityEditorModel submitted,
        IReadOnlyList<OpportunityPartyLink> links, string actor) {
        var accepted = submitted.Snapshot();
        accepted.Id = entity.Id;
        accepted.ExpectedUpdatedAtUtc = entity.UpdatedAtUtc;
        accepted.Title = entity.Title;
        accepted.RelationshipStage = entity.RelationshipStage;
        accepted.CurrencyCode = entity.CurrencyCode;
        accepted.LostReason = entity.LostReason;
        accepted.Summary = entity.Summary;
        accepted.Notes = entity.Notes;
        accepted.CompetitorName = submitted.CompetitorName.Trim();
        accepted.PartnerContributionSummary = submitted.PartnerContributionSummary.Trim();
        accepted.StageNotes = string.Empty;
        accepted.LinkedProjectAdmission = entity.LinkedProjectId.HasValue ? submitted.LinkedProjectAdmission : null;
        accepted.LastChangedBy = actor;
        accepted.Parties = links.Select(link => new CrmOpportunityPartyLinkEditorModel {
            Id = link.Id, PartyId = link.PartyId, Role = link.Role
        }).ToList();
        return new(entity.Id, accepted);
    }

    public async Task<Result<CrmOpportunityConversionProjectBasis>> ObserveOpportunityProjectBasisAsync(Guid opportunityId,
        DateTimeOffset expectedOpportunityUpdatedAtUtc, ProjectWriteAdmission project, CancellationToken cancellationToken = default) {
        await admissions.RequireCurrentAsync(project, cancellationToken);
        var projectRecord = await projectRecordQueryService.GetAsync(project.ProjectId, cancellationToken);
        if (projectRecord?.LifetimeId != project.LifetimeId) {
            throw new ProjectWriteAdmissionRejectedException(project);
        }
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var observation = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken) : null;
        var opportunity = (await LoadOpportunityDetailsAsync(context, [opportunityId], cancellationToken, includeHistory: false)).SingleOrDefault();
        if (opportunity is null || opportunity.UpdatedAtUtc != expectedOpportunityUpdatedAtUtc) {
            return Result<CrmOpportunityConversionProjectBasis>.Failure(Error.Failure(
                "The conversion basis changed. Review the opportunity before choosing its project target.", CrmHrEditingErrorCodes.OpportunityConflict));
        }
        var kinds = BuildOpportunityProjectAssignments(opportunity, project.ProjectId)
            .Select(item => ProjectPartyIntegrationService.MapRole(item.Role)).Distinct().ToArray();
        var rows = await context.Set<ProjectPartyAssignment>().AsNoTracking().Where(row => row.ProjectId == project.ProjectId &&
            row.ProjectLifetimeId == project.LifetimeId && row.NodeKey == string.Empty && kinds.Contains(row.AssignmentKind))
            .ToArrayAsync(cancellationToken);
        var partyIds = rows.Select(row => row.PartyId).Distinct().ToArray();
        var partyNames = await context.Set<Party>().AsNoTracking().Where(party => partyIds.Contains(party.Id))
            .ToDictionaryAsync(party => party.Id, party => party.DisplayName, cancellationToken);
        var basis = new CrmOpportunityConversionProjectBasis(opportunity.Id, opportunity.UpdatedAtUtc, project,
            rows.Select(row => CrmOpportunityAssignmentSnapshot.From(row).Original()).ToArray()) {
                ProjectName = projectRecord.Name, PartyNames = partyNames
            };
        if (observation is not null) {
            await observation.CommitAsync(cancellationToken);
        }
        return Result<CrmOpportunityConversionProjectBasis>.Success(basis);
    }

    public async Task<Result<CrmOpportunityConversionResult>> ConvertOpportunityToProjectAsync(
        CrmOpportunityConversionEditorModel model,
        CancellationToken cancellationToken = default,
        Action<CrmOpportunityConversionResult>? onCommitted = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        model = model.Snapshot();

        if (model.OpportunityId == Guid.Empty)
        {
            return Result<CrmOpportunityConversionResult>.Failure(Error.Validation(
                "Choose an opportunity before converting it to a project.",
                "crmhr.crm.opportunity-conversion-opportunity-required"));
        }

        if (model.LinkExistingProject && !model.ExistingProjectId.HasValue)
        {
            return Result<CrmOpportunityConversionResult>.Failure(Error.Validation(
                "Choose an existing project before linking it.",
                "crmhr.crm.opportunity-conversion-project-required"));
        }

        if (!model.LinkExistingProject && string.IsNullOrWhiteSpace(model.ProjectName))
        {
            return Result<CrmOpportunityConversionResult>.Failure(Error.Validation(
                "Project name is required when creating a new project.",
                "crmhr.crm.opportunity-conversion-project-name-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var opportunity = await dbContext.Set<Opportunity>()
            .SingleOrDefaultAsync(item => item.Id == model.OpportunityId, cancellationToken);
        if (opportunity is null)
        {
            return Result<CrmOpportunityConversionResult>.Failure(Error.Failure(
                "The selected opportunity no longer exists.",
                "crmhr.crm.opportunity-conversion-opportunity-missing"));
        }

        if (!model.ExpectedUpdatedAtUtc.HasValue)
        {
            return Result<CrmOpportunityConversionResult>.Failure(Error.Validation(
                "Reload the opportunity before converting it.",
                "crmhr.crm.opportunity-conversion-expected-updated-at-required"));
        }

        if (opportunity.UpdatedAtUtc != model.ExpectedUpdatedAtUtc.Value)
        {
            return Result<CrmOpportunityConversionResult>.Failure(Error.Failure(
                "The opportunity changed after it was loaded. Reload it before converting.",
                CrmHrEditingErrorCodes.OpportunityConversionConflict));
        }

        dbContext.Entry(opportunity)
            .Property(item => item.UpdatedAtUtc)
            .OriginalValue = model.ExpectedUpdatedAtUtc.Value;

        if (opportunity.Stage != OpportunityStage.Won)
        {
            return Result<CrmOpportunityConversionResult>.Failure(Error.Validation(
                "Only won opportunities can be converted into projects.",
                "crmhr.crm.opportunity-conversion-stage-invalid"));
        }

        var account = await dbContext.Set<Party>()
            .SingleAsync(item => item.Id == opportunity.AccountPartyId, cancellationToken);
        var opportunityDetail = (await LoadOpportunityDetailsAsync(dbContext, [opportunity.Id], cancellationToken))
            .FirstOrDefault();
        if (opportunityDetail is null)
        {
            return Result<CrmOpportunityConversionResult>.Failure(Error.Failure(
                "The selected opportunity could not be loaded.",
                "crmhr.crm.opportunity-conversion-load-failed"));
        }

        var normalizedActor = NormalizeActor(model.LastChangedBy);
        var createdNewProject = false;
        ProjectCreationReceipt? creationReceipt = null;
        Guid projectId;
        string projectName;
        ProjectWriteAdmission projectAdmission;

        await changes.DemandCommitAsync(cancellationToken);
        if (model.LinkExistingProject)
        {
            projectAdmission = ProjectAssignmentAdmission.Require(model.ExistingProjectId!.Value, model.ExpectedProjectAdmission);
            var project = await projectRecordQueryService.GetAsync(
                model.ExistingProjectId!.Value,
                cancellationToken);
            if (project is null)
            {
                return Result<CrmOpportunityConversionResult>.Failure(Error.Validation(
                    "The selected project no longer exists.",
                    "crmhr.crm.opportunity-conversion-project-missing"));
            }

            if (projectAdmission.DatabaseProfileId != admissions.DatabaseProfileId || project.LifetimeId != projectAdmission.LifetimeId) {
                throw new ProjectWriteAdmissionRejectedException(projectAdmission);
            }
            if (model.ExpectedProjectBasis is not { } basis || basis.OpportunityId != opportunity.Id ||
                basis.OpportunityUpdatedAtUtc != model.ExpectedUpdatedAtUtc || basis.Project != projectAdmission ||
                basis.Assignments.Any(row => row.ProjectId != projectAdmission.ProjectId || row.ProjectLifetimeId != projectAdmission.LifetimeId ||
                    row.NodeKey != string.Empty || row.Id == Guid.Empty) ||
                basis.Assignments.Select(row => row.Id).Distinct().Count() != basis.Assignments.Count) {
                return Result<CrmOpportunityConversionResult>.Failure(Error.Validation(
                    "Observe the selected project's complete handoff membership before converting.", CrmHrEditingErrorCodes.OpportunityConversionBasisRequired));
            }
            projectId = project.Id;
            projectName = project.Name;
        }
        else
        {
            var projectResult = await projectsService.CreateWithReceiptAsync(Guid.NewGuid(),
                new ProjectEditorModel
                {
                    Name = model.ProjectName.Trim(),
                    Description = string.IsNullOrWhiteSpace(model.ProjectDescription)
                        ? opportunityDetail.Summary
                        : model.ProjectDescription.Trim(),
                    Objective = string.IsNullOrWhiteSpace(model.ProjectObjective)
                        ? opportunityDetail.Title
                        : model.ProjectObjective.Trim(),
                    Status = ProjectStatus.Active,
                    CurrentPhase = string.IsNullOrWhiteSpace(model.CurrentPhase) ? "Sales handoff" : model.CurrentPhase.Trim()
                },
                cancellationToken: cancellationToken);
            if (!projectResult.IsSuccess)
            {
                return Result<CrmOpportunityConversionResult>.Failure(projectResult.Errors.ToArray());
            }

            createdNewProject = true;
            creationReceipt = projectResult.Value
                ?? throw new InvalidOperationException("The Projects owner acknowledged creation without its receipt.");
            projectAdmission = creationReceipt.Project;
            projectId = projectAdmission.ProjectId;
            projectName = model.ProjectName.Trim();
        }

        var opportunityAssignments = BuildOpportunityProjectAssignments(
            opportunityDetail,
            projectId);
        var targetRoles = opportunityAssignments
            .Select(assignment => assignment.Role)
            .Distinct()
            .ToList();
        var originalAssignments = model.LinkExistingProject ? model.ExpectedProjectBasis!.Assignments : [];
        var originalRootAssignments = originalAssignments
            .Select(ToProjectAssignmentRequest)
            .ToList();
        var desiredProjectAssignments = originalRootAssignments
            .Concat(opportunityAssignments)
            .GroupBy(assignment => new
            {
                assignment.PartyId,
                assignment.Role
            })
            .Select(group => group.First())
            .ToList();
        Result<CrmOpportunityAssignmentReceipt> assignmentResult;
        try {
            assignmentResult = await assignmentService.ReplaceOpportunityAssignmentsAsync(
                projectId, desiredProjectAssignments, targetRoles, projectAdmission, cancellationToken,
                changes.DemandCommitAsync, originalAssignments);
        } catch (Exception exception) {
            throw new CrmOpportunityConversionRequiresObservationException(projectAdmission, exception);
        }
        if (!assignmentResult.IsSuccess)
        {
            var compensated = true;
            if (creationReceipt is not null) {
                try {
                    compensated = await projectsService.TryCompensateCreationAsync(creationReceipt, CancellationToken.None);
                } catch (Exception compensationFailure) {
                    throw new CrmOpportunityConversionRequiresObservationException(projectAdmission, compensationFailure, assignmentResult.Errors);
                }
            }
            if (!compensated) {
                return Result<CrmOpportunityConversionResult>.Failure(assignmentResult.Errors.Append(Error.Failure(
                    "Opportunity conversion did not complete. The created project changed and was retained for review.",
                    CrmOpportunityConversionRequiresObservationException.ErrorCode)).ToArray());
            }
            return Result<CrmOpportunityConversionResult>.Failure(assignmentResult.Errors.ToArray());
        }

        var assignmentReceipt = assignmentResult.Value
            ?? throw new InvalidOperationException("The CRM owner acknowledged replacement without its compensation receipt.");
        var now = clock.GetUtcNow();
        opportunity.LinkedProjectId = projectId;
        opportunity.UpdatedAtUtc = CrmHrEditVersions.Next(opportunity.UpdatedAtUtc, now);
        account.LastChangedBy = normalizedActor;
        account.UpdatedAtUtc = CrmHrEditVersions.Next(account.UpdatedAtUtc, now);

        AddAuditEntry(
            dbContext,
            account.Id,
            "OpportunityConvertedToProject",
            $"Converted opportunity '{opportunityDetail.Title}' to project '{projectName}'.",
            new
            {
                opportunity.Id,
                ProjectId = projectId,
                CreatedNewProject = createdNewProject
            },
            normalizedActor,
            account.IsSensitive);

        var linkCommitAttempted = false;
        try
        {
            await using var linkTransaction = dbContext.Database.IsRelational()
                ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken) : null;
            using var linkOwner = opportunityTransaction.Enter(dbContext);
            await SerializableMutationScope.AcquireRelationalScopeLocksAsync(dbContext,
                [ProjectMutationScopeKeys.ForProject(projectId)], cancellationToken);
            await admissions.RequireForMutationAsync(projectAdmission, cancellationToken);
            await changes.DemandCommitAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await changes.DemandCommitAsync(cancellationToken);
            linkCommitAttempted = true;
            if (linkTransaction is not null) {
                await linkTransaction.CommitAsync(cancellationToken);
            }
        }
        catch (Exception exception) {
            var knownRollback = !linkCommitAttempted && (SerializableMutationScope.IsConflict(exception) ||
                exception is UnauthorizedAccessException or ProjectWriteAdmissionRejectedException);
            if (!knownRollback) {
                throw new CrmOpportunityConversionRequiresObservationException(projectAdmission, exception);
            }
            try {
                if (!await assignmentService.TryRestoreOpportunityAssignmentsAsync(assignmentReceipt, CancellationToken.None) ||
                        creationReceipt is not null && !await projectsService.TryCompensateCreationAsync(creationReceipt, CancellationToken.None)) {
                    throw new CrmOpportunityConversionRequiresObservationException(projectAdmission, exception);
                }
            } catch (CrmOpportunityConversionRequiresObservationException) {
                throw;
            } catch (Exception compensationFailure) {
                throw new CrmOpportunityConversionRequiresObservationException(projectAdmission, new AggregateException(exception, compensationFailure));
            }
            if (exception is UnauthorizedAccessException) {
                throw;
            }
            return Result<CrmOpportunityConversionResult>.Failure(Error.Failure(
                "The opportunity changed while it was being converted. Reload it before retrying.",
                CrmHrEditingErrorCodes.OpportunityConversionConflict));
        }

        var conversion = new CrmOpportunityConversionResult(opportunity.Id, projectId, createdNewProject) {
            ProjectAdmission = projectAdmission, ProjectName = projectName, OpportunityUpdatedAtUtc = opportunity.UpdatedAtUtc
        };
        onCommitted?.Invoke(conversion);
        changes.PublishOpportunity(opportunity.Id);
        changes.PublishParty(account.Id);
        await UpsertAccountSearchDocumentAsync(account.Id, cancellationToken);
        await UpsertOpportunitySearchDocumentAsync(opportunity.Id, cancellationToken);
        await activityStream.RecordAsync(
            new ActivityWriteRequest(
                "CRM / HR",
                "OpportunityConvertedToProject",
                $"Converted opportunity {opportunityDetail.Title} to project",
                projectName,
                ArtifactKind: CrmOpportunityEntityType,
                ArtifactId: opportunity.Id,
                Route: BuildOpportunityRoute(account.Id, opportunity.Id),
                Actor: normalizedActor),
            cancellationToken);

        return Result<CrmOpportunityConversionResult>.Success(conversion);
    }

    private async Task<IReadOnlyList<CrmOpportunityDetailModel>> LoadOpportunityDetailsAsync(
        CrmHrDbContext dbContext,
        IReadOnlyCollection<Guid> opportunityIds,
        CancellationToken cancellationToken,
        bool includeHistory = true,
        int? maximumPartyLinks = null)
    {
        if (opportunityIds.Count == 0)
        {
            return [];
        }

        var opportunities = await dbContext.Set<Opportunity>()
            .Where(item => opportunityIds.Contains(item.Id))
            .Select(item => new
            {
                item.Id,
                item.AccountPartyId,
                item.Title,
                item.Stage,
                item.RelationshipStage,
                item.OpportunitySource,
                item.OwnerPartyId,
                item.DeliveryUnitPartyId,
                item.CurrencyCode,
                item.Amount,
                item.ProbabilityPercent,
                item.ExpectedCloseDateUtc,
                item.LostReason,
                item.Summary,
                item.Notes,
                item.ExtendedDataJson,
                item.LinkedProjectId,
                item.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
        var partyLinksQuery = dbContext.Set<OpportunityPartyLink>().AsNoTracking()
            .Where(item => opportunityIds.Contains(item.OpportunityId));
        if (maximumPartyLinks.HasValue) {
            partyLinksQuery = partyLinksQuery.Take(maximumPartyLinks.Value + 1);
        }
        var partyLinks = await partyLinksQuery.ToListAsync(cancellationToken);
        if (maximumPartyLinks.HasValue && partyLinks.Count > maximumPartyLinks.Value) {
            throw new OpportunityReadLimitExceededException(maximumPartyLinks.Value);
        }
        var stageHistory = includeHistory ? await dbContext.Set<OpportunityStageHistory>()
            .Where(item => opportunityIds.Contains(item.OpportunityId))
            .ToListAsync(cancellationToken) : [];
        var partyIds = opportunities
            .Select(item => item.AccountPartyId)
            .Concat(opportunities.Select(item => item.OwnerPartyId))
            .Concat(opportunities.Where(item => item.DeliveryUnitPartyId.HasValue).Select(item => item.DeliveryUnitPartyId!.Value))
            .Concat(partyLinks.Select(item => item.PartyId))
            .Distinct()
            .ToList();
        var partiesById = partyIds.Count == 0
            ? new Dictionary<Guid, PartyOptionModel>()
            : (await dbContext.Set<Party>()
                .Where(item => partyIds.Contains(item.Id))
                .Select(item => new PartyOptionModel(item.Id, item.DisplayName, item.PartyType))
                .ToListAsync(cancellationToken))
                .ToDictionary(item => item.Id);
        var linkedProjectIds = opportunities
            .Where(item => item.LinkedProjectId.HasValue && item.LinkedProjectId.Value != Guid.Empty)
            .Select(item => item.LinkedProjectId!.Value)
            .Distinct()
            .ToList();
        var projectsById = linkedProjectIds.Count == 0
            ? new Dictionary<Guid, ProjectRecordQueryItem>()
            : (await projectRecordQueryService.GetManyAsync(linkedProjectIds, cancellationToken))
                .ToDictionary(item => item.Id);
        var partyLinksByOpportunityId = partyLinks
            .GroupBy(item => item.OpportunityId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var stageHistoryByOpportunityId = stageHistory
            .GroupBy(item => item.OpportunityId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.ChangedAtUtc).ToList());

        return opportunities
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item =>
            {
                var extendedData = DeserializeOpportunityExtendedData(item.ExtendedDataJson);
                var detailLinks = (partyLinksByOpportunityId.GetValueOrDefault(item.Id) ?? [])
                    .Select(link =>
                    {
                        var linkedParty = RequireOpportunityParty(
                            partiesById,
                            link.PartyId,
                            item.Id,
                            $"party link '{link.Id}'");
                        return new CrmOpportunityPartyLinkItemModel(
                            link.Id,
                            link.PartyId,
                            linkedParty.DisplayName,
                            linkedParty.PartyType,
                            link.Role);
                    })
                    .OrderBy(link => link.Role)
                    .ThenBy(link => link.DisplayName)
                    .ToList();
                var detailHistory = (stageHistoryByOpportunityId.GetValueOrDefault(item.Id) ?? [])
                    .Select(history => new OpportunityStageHistoryItemModel(
                        history.Id,
                        history.Stage,
                        history.ChangedAtUtc,
                        history.ChangedBy,
                        history.Notes))
                    .ToList();
                var accountParty = RequireOpportunityParty(
                    partiesById,
                    item.AccountPartyId,
                    item.Id,
                    "account");
                var ownerParty = RequireOpportunityParty(
                    partiesById,
                    item.OwnerPartyId,
                    item.Id,
                    "owner");
                var deliveryUnit = item.DeliveryUnitPartyId.HasValue
                    ? RequireOpportunityParty(
                        partiesById,
                        item.DeliveryUnitPartyId.Value,
                        item.Id,
                        "delivery unit")
                    : null;
                var linkedProjectName = string.Empty;
                if (item.LinkedProjectId is Guid linkedProjectId &&
                    !projectsById.ContainsKey(linkedProjectId))
                {
                    throw new InvalidOperationException(
                        $"Opportunity '{item.Id}' references missing project '{linkedProjectId}'.");
                }
                ProjectWriteAdmission? projectAdmission = null;
                if (item.LinkedProjectId is Guid projectId) {
                    var project = projectsById[projectId];
                    linkedProjectName = project.Name;
                    if (project.LifetimeId is not Guid lifetimeId) {
                        throw new InvalidOperationException($"Opportunity '{item.Id}' received an unobserved project reference '{projectId}'.");
                    }
                    projectAdmission = new(admissions.DatabaseProfileId, projectId, lifetimeId);
                }

                return new CrmOpportunityDetailModel(
                    item.Id,
                    item.AccountPartyId,
                    accountParty.DisplayName,
                    item.Title,
                    item.Stage,
                    item.RelationshipStage,
                    item.OpportunitySource,
                    item.OwnerPartyId,
                    ownerParty.DisplayName,
                    item.DeliveryUnitPartyId,
                    deliveryUnit?.DisplayName ?? string.Empty,
                    item.CurrencyCode.Trim().ToUpperInvariant(),
                    item.Amount,
                    item.ProbabilityPercent,
                    ToDateOnly(item.ExpectedCloseDateUtc),
                    item.LostReason,
                    extendedData.CompetitorName,
                    extendedData.PartnerContributionSummary,
                    item.Summary,
                    item.Notes,
                    item.LinkedProjectId,
                    linkedProjectName,
                    detailLinks,
                    detailHistory,
                    item.UpdatedAtUtc) { LinkedProjectAdmission = projectAdmission };
            })
            .ToList();
    }

    private async Task UpsertOpportunitySearchDocumentAsync(Guid opportunityId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var opportunity = (await LoadOpportunityDetailsAsync(dbContext, [opportunityId], cancellationToken))
            .FirstOrDefault();
        if (opportunity is null)
        {
            await searchIndexService.DeleteAsync(CrmOpportunitySearchSourceType, opportunityId.ToString("N"), cancellationToken);
            return;
        }

        var accountIsSensitive = await dbContext.Set<Party>()
            .Where(item => item.Id == opportunity.AccountPartyId)
            .Select(item => item.IsSensitive)
            .FirstOrDefaultAsync(cancellationToken);
        if (accountIsSensitive)
        {
            await searchIndexService.DeleteAsync(CrmOpportunitySearchSourceType, opportunityId.ToString("N"), cancellationToken);
            return;
        }

        var summaryParts = new List<string>
        {
            opportunity.Stage.ToString(),
            opportunity.OpportunitySource.ToString()
        };

        if (opportunity.Amount.HasValue)
        {
            summaryParts.Add($"{opportunity.CurrencyCode} {opportunity.Amount.Value:0.##}");
        }

        if (opportunity.ProbabilityPercent > 0)
        {
            summaryParts.Add($"{opportunity.ProbabilityPercent}%");
        }

        var bodyParts = new List<string>
        {
            opportunity.AccountDisplayName,
            opportunity.OwnerDisplayName,
            opportunity.DeliveryUnitDisplayName,
            opportunity.Summary,
            opportunity.Notes,
            opportunity.LostReason,
            opportunity.CompetitorName,
            opportunity.PartnerContributionSummary,
            opportunity.LinkedProjectName,
            string.Join(", ", opportunity.Parties.Select(item => $"{item.Role}:{item.DisplayName}")),
            string.Join(Environment.NewLine, opportunity.StageHistory.Select(item => $"{item.Stage} {item.ChangedAtUtc:yyyy-MM-dd} {item.Notes}"))
        };

        await searchIndexService.UpsertAsync(
            new SearchDocumentInput(
                CrmOpportunitySearchSourceType,
                opportunity.Id.ToString("N"),
                "CRM / HR opportunity",
                opportunity.Title,
                string.Join(" / ", summaryParts),
                string.Join(Environment.NewLine, bodyParts.Where(item => !string.IsNullOrWhiteSpace(item))),
                BuildOpportunityRoute(opportunity.AccountPartyId, opportunity.Id)),
            cancellationToken);
    }

    private async Task UpsertAccountSearchDocumentAsync(Guid accountPartyId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>().SingleOrDefaultAsync(item => item.Id == accountPartyId, cancellationToken);
        if (party is null)
        {
            await searchIndexService.DeleteAsync(CrmAccountSearchSourceType, accountPartyId.ToString("N"), cancellationToken);
            return;
        }

        if (party.IsSensitive)
        {
            await searchIndexService.DeleteAsync(CrmAccountSearchSourceType, accountPartyId.ToString("N"), cancellationToken);
            return;
        }

        var roles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => item.PartyId == accountPartyId)
            .OrderBy(item => item.RoleKind)
            .Select(item => item.RoleKind)
            .ToListAsync(cancellationToken);
        var profile = await dbContext.Set<CrmAccountProfile>()
            .SingleOrDefaultAsync(item => item.AccountPartyId == accountPartyId, cancellationToken);
        var connections = await dbContext.Set<CrmAccountConnection>()
            .Where(item => item.AccountPartyId == accountPartyId)
            .ToListAsync(cancellationToken);
        var connectedPartyIds = connections.Select(item => item.RelatedPartyId).Distinct().ToList();
        var connectedPartyNames = connectedPartyIds.Count == 0
            ? []
            : await dbContext.Set<Party>()
                .Where(item => connectedPartyIds.Contains(item.Id))
                .OrderBy(item => item.DisplayName)
                .Select(item => item.DisplayName)
                .ToListAsync(cancellationToken);
        var interactionIds = await dbContext.Set<InteractionPartyLink>()
            .Where(item => item.PartyId == accountPartyId && item.Role == InteractionPartyRole.Account)
            .Select(item => item.InteractionId)
            .ToListAsync(cancellationToken);
        var recentInteractionSubjects = interactionIds.Count == 0
            ? []
            : await dbContext.Set<InteractionRecord>()
                .AsNoTracking()
                .Where(item => interactionIds.Contains(item.Id))
                .OrderByDescending(item => item.OccurredAtUtc)
                .Take(5)
                .Select(item => item.Subject)
                .ToListAsync(cancellationToken);

        var relationshipStage = ResolveRelationshipStage(party.LifecycleStatus, roles, profile);
        var summary = $"{relationshipStage} / {string.Join(", ", roles.Take(3))}".Trim(' ', '/');
        var bodyParts = new List<string>
        {
            party.DisplayName,
            party.Summary,
            profile?.CommercialNotes ?? string.Empty,
            profile?.ConstraintNotes ?? string.Empty,
            profile?.TimingRiskNotes ?? string.Empty,
            string.Join(", ", connectedPartyNames),
            string.Join(Environment.NewLine, recentInteractionSubjects)
        };

        await searchIndexService.UpsertAsync(
            new SearchDocumentInput(
                CrmAccountSearchSourceType,
                accountPartyId.ToString("N"),
                "CRM / HR account",
                party.DisplayName,
                string.IsNullOrWhiteSpace(summary) ? "CRM account" : summary,
                string.Join(Environment.NewLine, bodyParts.Where(item => !string.IsNullOrWhiteSpace(item))),
                $"/crm-hr/crm?accountId={accountPartyId}"),
            cancellationToken);
    }

    private static void AddAuditEntry(
        CrmHrDbContext dbContext,
        Guid entityId,
        string action,
        string summary,
        object detail,
        string actor,
        bool isSensitive)
    {
        dbContext.Set<CrmHrAuditEntry>().Add(new CrmHrAuditEntry
        {
            EntityType = CrmAccountEntityType,
            EntityId = entityId,
            Action = action,
            Summary = summary,
            DetailJson = JsonSerializer.Serialize(detail),
            Actor = actor,
            IsSensitive = isSensitive,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    private static DateTimeOffset ToUtcDate(DateOnly value)
    {
        return new DateTimeOffset(value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }

    private static string? NormalizeOpportunityCurrencyCode(string? currencyCode)
    {
        var normalized = currencyCode?.Trim().ToUpperInvariant() ?? string.Empty;
        return normalized is [var first, var second, var third]
            && char.IsAsciiLetter(first)
            && char.IsAsciiLetter(second)
            && char.IsAsciiLetter(third)
            ? normalized
            : null;
    }

    private static PartyOptionModel RequireOpportunityParty(
        IReadOnlyDictionary<Guid, PartyOptionModel> partiesById,
        Guid partyId,
        Guid opportunityId,
        string referenceKind)
    {
        if (partiesById.TryGetValue(partyId, out var party))
        {
            return party;
        }

        throw new InvalidOperationException(
            $"Opportunity '{opportunityId}' references missing {referenceKind} party '{partyId}'.");
    }

    private static CrmAccountRelationshipStage ResolveRelationshipStage(
        PartyLifecycleStatus lifecycleStatus,
        IReadOnlyList<PartyRoleKind> roles,
        CrmAccountProfile? profile)
    {
        if (profile is not null)
        {
            return profile.RelationshipStage;
        }

        if (lifecycleStatus == PartyLifecycleStatus.Prospect)
        {
            return CrmAccountRelationshipStage.Prospect;
        }

        if (roles.Contains(PartyRoleKind.Customer))
        {
            return CrmAccountRelationshipStage.ActiveCustomer;
        }

        return lifecycleStatus switch
        {
            PartyLifecycleStatus.Inactive or PartyLifecycleStatus.Former => CrmAccountRelationshipStage.DormantCustomer,
            _ => CrmAccountRelationshipStage.Prospect
        };
    }

    private static InteractionPartyRole ResolveInteractionRole(CrmAccountConnectionRole connectionRole)
    {
        return connectionRole switch
        {
            CrmAccountConnectionRole.AccountManager
                or CrmAccountConnectionRole.DeliveryLead
                or CrmAccountConnectionRole.Sponsor
                or CrmAccountConnectionRole.Stakeholder => InteractionPartyRole.Stakeholder,
            _ => InteractionPartyRole.Contact
        };
    }

    private static IReadOnlyList<string> TryDeserializeTags(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static CrmOpportunityExtendedDataModel DeserializeOpportunityExtendedData(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CrmOpportunityExtendedDataModel();
        }

        try
        {
            return JsonSerializer.Deserialize<CrmOpportunityExtendedDataModel>(json) ?? new CrmOpportunityExtendedDataModel();
        }
        catch (JsonException)
        {
            return new CrmOpportunityExtendedDataModel();
        }
    }

    private static string ResolvePrimaryContact(
        IReadOnlyList<CrmPartyContactValue> contactPoints,
        PartyContactType contactType)
    {
        return contactPoints
            .Where(item => item.ContactType == contactType)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => item.Value)
            .FirstOrDefault()
            ?? string.Empty;
    }

    private static string NormalizeActor(string actor)
    {
        return string.IsNullOrWhiteSpace(actor) ? "crm-hr-ui" : actor.Trim();
    }

    private static string BuildOpportunityRoute(Guid accountPartyId, Guid opportunityId)
    {
        return $"/crm-hr/crm?accountId={accountPartyId}&opportunityId={opportunityId}";
    }

    private static bool IsClosedOpportunityStage(OpportunityStage stage)
    {
        return stage is OpportunityStage.Won or OpportunityStage.Lost;
    }

    private static DateOnly? ToDateOnly(DateTimeOffset? value)
    {
        return value.HasValue
            ? DateOnly.FromDateTime(value.Value.UtcDateTime)
            : null;
    }

    private static IReadOnlyList<ProjectPartyAssignmentUpsertRequest> BuildOpportunityProjectAssignments(
        CrmOpportunityDetailModel opportunity,
        Guid projectId)
    {
        var assignments = new List<ProjectPartyAssignmentUpsertRequest>
        {
            new()
            {
                ProjectId = projectId,
                PartyId = opportunity.AccountPartyId,
                Role = ProjectPartyAssignmentRole.Customer,
                IsPrimary = true,
                Source = "crm-opportunity",
                Notes = $"Opportunity: {opportunity.Title}"
            },
            new()
            {
                ProjectId = projectId,
                PartyId = opportunity.OwnerPartyId,
                Role = ProjectPartyAssignmentRole.Manager,
                IsPrimary = true,
                Source = "crm-opportunity",
                Notes = $"Opportunity owner: {opportunity.Title}"
            }
        };

        if (opportunity.DeliveryUnitPartyId.HasValue)
        {
            assignments.Add(new ProjectPartyAssignmentUpsertRequest
            {
                ProjectId = projectId,
                PartyId = opportunity.DeliveryUnitPartyId.Value,
                Role = ProjectPartyAssignmentRole.DeliveryUnit,
                IsPrimary = true,
                Source = "crm-opportunity",
                Notes = $"Opportunity delivery unit: {opportunity.Title}"
            });
        }

        var mappedLinks = opportunity.Parties
            .Select(link => new
            {
                link.PartyId,
                Role = MapOpportunityRole(link.Role, link.PartyId == opportunity.AccountPartyId),
                Notes = $"{link.Role}: {opportunity.Title}"
            })
            .Where(item => item.Role.HasValue)
            .GroupBy(item => new { item.PartyId, Role = item.Role!.Value })
            .Select(group => group.First())
            .ToList();
        var primaryRoles = new HashSet<ProjectPartyAssignmentRole>();
        foreach (var mappedLink in mappedLinks)
        {
            assignments.Add(new ProjectPartyAssignmentUpsertRequest
            {
                ProjectId = projectId,
                PartyId = mappedLink.PartyId,
                Role = mappedLink.Role!.Value,
                IsPrimary = primaryRoles.Add(mappedLink.Role.Value),
                Source = "crm-opportunity",
                Notes = mappedLink.Notes
            });
        }

        return assignments
            .GroupBy(item => new { item.ProjectId, item.PartyId, item.Role, item.NodeKey })
            .Select(group => group.First())
            .ToList();
    }

    private static ProjectPartyAssignmentUpsertRequest ToProjectAssignmentRequest(
        CrmOpportunityAssignmentOriginal assignment)
    {
        return new ProjectPartyAssignmentUpsertRequest
        {
            AssignmentId = assignment.Id,
            ProjectId = assignment.ProjectId,
            PartyId = assignment.PartyId,
            PartyAffiliationId = assignment.PartyOrganizationAffiliationId,
            Role = ProjectPartyIntegrationService.MapRole(assignment.AssignmentKind),
            NodeKey = assignment.NodeKey,
            IsPrimary = assignment.IsPrimary,
            AllocationPercent = assignment.AllocationPercent,
            StartsOn = ToDateOnly(assignment.StartsAtUtc),
            EndsOn = ToDateOnly(assignment.EndsAtUtc),
            Source = assignment.Source,
            Notes = assignment.Notes
        };
    }

    private static ProjectPartyAssignmentRole? MapOpportunityRole(OpportunityPartyRole role, bool isAccountParty)
    {
        return role switch
        {
            OpportunityPartyRole.Customer when !isAccountParty => ProjectPartyAssignmentRole.CustomerContact,
            OpportunityPartyRole.Partner => ProjectPartyAssignmentRole.Partner,
            OpportunityPartyRole.TechnicalContact => ProjectPartyAssignmentRole.TechnicalContact,
            OpportunityPartyRole.BillingContact => ProjectPartyAssignmentRole.BillingContact,
            OpportunityPartyRole.DeliveryLead => ProjectPartyAssignmentRole.TeamMember,
            OpportunityPartyRole.Sponsor or OpportunityPartyRole.Stakeholder => ProjectPartyAssignmentRole.Stakeholder,
            _ => null
        };
    }
}

internal static class CrmActivityHistoryQueryComposer
{
    private const int InteractionSourceOrder = 0;
    private const int AuditSourceOrder = 1;

    public static async Task<CrmActivityHistoryPage> SearchAsync(
        CrmHrDbContext dbContext,
        IQueryable<InteractionRecord> interactions,
        IQueryable<CrmHrAuditEntry> auditEntries,
        CrmActivityHistoryQuery query,
        bool includeParticipantNames,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(query);
        var actionCounts = await interactions
            .GroupBy(_ => 1)
            .Select(group => new CrmActivityActionCounts(
                group.Count(item => item.NextActionText != string.Empty),
                group.Count(item =>
                    item.NextActionText != string.Empty &&
                    item.NextActionDueUtc.HasValue &&
                    item.NextActionDueUtc.Value < nowUtc)))
            .SingleOrDefaultAsync(cancellationToken)
            ?? new CrmActivityActionCounts(0, 0);

        var rows = interactions
            .Select(item => new
            {
                item.Id,
                SourceOrder = InteractionSourceOrder,
                OccurredAtUtc = item.OccurredAtUtc,
                InteractionType = (InteractionType?)item.InteractionType,
                item.Subject,
                item.Summary,
                item.Notes,
                item.NextActionText,
                item.NextActionDueUtc,
                Action = string.Empty,
                Actor = string.Empty,
                IsSensitive = false
            })
            .Concat(auditEntries.Select(item => new
            {
                item.Id,
                SourceOrder = AuditSourceOrder,
                OccurredAtUtc = item.CreatedAtUtc,
                InteractionType = (InteractionType?)null,
                Subject = item.Summary,
                Summary = string.Empty,
                Notes = string.Empty,
                NextActionText = string.Empty,
                NextActionDueUtc = (DateTimeOffset?)null,
                item.Action,
                item.Actor,
                item.IsSensitive
            }));
        var totalCount = await rows.CountAsync(cancellationToken);
        var pageRows = await rows
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenBy(item => item.SourceOrder)
            .ThenBy(item => item.Id)
            .Skip(normalized.PageIndex * normalized.PageSize)
            .Take(normalized.PageSize)
            .ToListAsync(cancellationToken);
        var interactionIds = pageRows
            .Where(item => item.SourceOrder == InteractionSourceOrder)
            .Select(item => item.Id)
            .ToList();
        var participantNames = includeParticipantNames && interactionIds.Count > 0
            ? await LoadParticipantNamesAsync(dbContext, interactionIds, cancellationToken)
            : new Dictionary<Guid, string>();
        var items = pageRows
            .Select(item => MapItem(
                new CrmActivityHistoryQueryRow(
                    item.Id,
                    item.SourceOrder,
                    item.OccurredAtUtc,
                    item.InteractionType,
                    item.Subject,
                    item.Summary,
                    item.Notes,
                    item.NextActionText,
                    item.NextActionDueUtc,
                    item.Action,
                    item.Actor,
                    item.IsSensitive),
                participantNames.GetValueOrDefault(item.Id),
                nowUtc))
            .ToList();

        return new CrmActivityHistoryPage(
            items,
            normalized.PageIndex,
            normalized.PageSize,
            totalCount,
            actionCounts.ActionCount,
            actionCounts.OverdueActionCount);
    }

    private static CrmActivityHistoryQuery Normalize(CrmActivityHistoryQuery query)
    {
        if (query.PartyId == Guid.Empty)
        {
            throw new ArgumentException("A party identifier is required.", nameof(query));
        }

        if (query.PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "Activity history page index cannot be negative.");
        }

        if (query.PageSize is < 1 or > CrmActivityHistoryQueryLimits.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageSize,
                $"Activity history page size must be between 1 and {CrmActivityHistoryQueryLimits.MaximumPageSize}.");
        }

        if (query.PageIndex > int.MaxValue / query.PageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.PageIndex,
                "Activity history page offset is too large.");
        }

        return query;
    }

    private static async Task<Dictionary<Guid, string>> LoadParticipantNamesAsync(
        CrmHrDbContext dbContext,
        IReadOnlyCollection<Guid> interactionIds,
        CancellationToken cancellationToken)
    {
        var rows = await (
                from link in dbContext.Set<InteractionPartyLink>().AsNoTracking()
                join party in dbContext.Set<Party>().AsNoTracking()
                    on link.PartyId equals party.Id
                where interactionIds.Contains(link.InteractionId) &&
                      link.Role != InteractionPartyRole.Account
                select new CrmActivityParticipantName(
                    link.InteractionId,
                    party.DisplayName))
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(item => item.InteractionId)
            .ToDictionary(
                group => group.Key,
                group => string.Join(
                    ", ",
                    group.Select(item => item.DisplayName)
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)));
    }

    private static CrmAccountActivityTimelineItemModel MapItem(
        CrmActivityHistoryQueryRow item,
        string? participantNames,
        DateTimeOffset nowUtc)
    {
        if (item.SourceOrder == AuditSourceOrder)
        {
            return new CrmAccountActivityTimelineItemModel(
                item.Id,
                "Audit",
                item.Subject,
                item.Action,
                item.Actor,
                item.OccurredAtUtc,
                item.IsSensitive ? "warning" : "neutral",
                false);
        }

        var interactionType = item.InteractionType
            ?? throw new InvalidOperationException($"Interaction activity '{item.Id}' has no interaction type.");
        var metaParts = new List<string>
        {
            interactionType.ToString()
        };
        if (!string.IsNullOrWhiteSpace(participantNames))
        {
            metaParts.Add(participantNames);
        }

        if (!string.IsNullOrWhiteSpace(item.NextActionText) &&
            item.NextActionDueUtc is DateTimeOffset dueAtUtc)
        {
            metaParts.Add($"Next action due {dueAtUtc:yyyy-MM-dd}");
        }

        return new CrmAccountActivityTimelineItemModel(
            item.Id,
            "Interaction",
            item.Subject,
            string.IsNullOrWhiteSpace(item.Summary) ? item.Notes : item.Summary,
            string.Join(" / ", metaParts),
            item.OccurredAtUtc,
            ResolveInteractionTone(interactionType),
            !string.IsNullOrWhiteSpace(item.NextActionText) &&
            item.NextActionDueUtc is DateTimeOffset nextActionDueAtUtc &&
            nextActionDueAtUtc < nowUtc);
    }

    private static string ResolveInteractionTone(InteractionType interactionType)
    {
        return interactionType switch
        {
            InteractionType.Meeting => "info",
            InteractionType.Call => "success",
            InteractionType.Email => "neutral",
            InteractionType.Message => "warning",
            _ => "neutral"
        };
    }

    private sealed record CrmActivityActionCounts(
        int ActionCount,
        int OverdueActionCount);

    private sealed record CrmActivityHistoryQueryRow(
        Guid Id,
        int SourceOrder,
        DateTimeOffset OccurredAtUtc,
        InteractionType? InteractionType,
        string Subject,
        string Summary,
        string Notes,
        string NextActionText,
        DateTimeOffset? NextActionDueUtc,
        string Action,
        string Actor,
        bool IsSensitive);

    private sealed record CrmActivityParticipantName(
        Guid InteractionId,
        string DisplayName);
}

public sealed class HrService(
    IDbContextFactory<CrmHrDbContext> dbContextFactory,
    IClock clock,
    ProjectRecordQueryService projectRecordQueryService,
    IProjectWorkAssignmentQueries workAssignments,
    IStaffingRequestEditing staffingRequests,
    IWorkforceProfileEditing workforceProfiles, WorkforceSkillEditingService workforceSkills, WorkforceCapacityEditingService workforceCapacity)
{
    public async Task<IReadOnlyList<WorkforceProfileSummaryModel>> ListWorkforceProfilesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<WorkforceProfile>()
            .OrderBy(item => item.JobTitle)
            .Select(item => new WorkforceProfileSummaryModel(item.Id, item.PartyId, item.WorkforceKind, item.JobTitle, item.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkforceListItemModel>> ListWorkforceDirectoryAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var parties = await dbContext.Set<Party>()
            .OrderBy(item => item.DisplayName)
            .Select(item => new
            {
                item.Id,
                item.DisplayName,
                item.PartyType,
                item.IsSensitive,
                item.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
        if (parties.Count == 0)
        {
            return [];
        }

        var partyIds = parties.Select(item => item.Id).ToList();
        var roles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => partyIds.Contains(item.PartyId))
            .Select(item => new
            {
                item.PartyId,
                item.RoleKind
            })
            .ToListAsync(cancellationToken);
        var profiles = await dbContext.Set<WorkforceProfile>()
            .Where(item => partyIds.Contains(item.PartyId))
            .Select(item => new
            {
                item.Id,
                item.PartyId,
                item.WorkforceKind,
                item.JobTitle,
                item.Discipline,
                item.Status,
                item.Seniority,
                item.Location,
                item.EndDateUtc,
                item.CapacityHoursPerWeek,
                item.HomeUnitPartyId,
                item.ManagerPartyId
            })
            .ToListAsync(cancellationToken);
        var partySkillsByPartyId = await GetPartySkillMapAsync(dbContext, partyIds, cancellationToken);
        var projectAllocationsByPartyId = await GetProjectAllocationMapAsync(dbContext, partyIds, cancellationToken);
        var capacityBlocksByPartyId = await GetCapacityBlockMapAsync(dbContext, partyIds, cancellationToken);

        var rolesByPartyId = roles
            .GroupBy(item => item.PartyId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PartyRoleKind>)group.Select(item => item.RoleKind).Distinct().ToList());
        var namesByPartyId = parties.ToDictionary(item => item.Id, item => item.DisplayName);
        var profilesByPartyId = profiles.ToDictionary(item => item.PartyId, item => item);

        return parties
            .Where(item =>
            {
                if (profilesByPartyId.ContainsKey(item.Id))
                {
                    return true;
                }

                if (item.PartyType == PartyType.Person || item.PartyType == PartyType.OrganizationUnit)
                {
                    return true;
                }

                var partyRoles = rolesByPartyId.GetValueOrDefault(item.Id) ?? [];
                return partyRoles.Contains(PartyRoleKind.DeliveryUnit);
            })
            .Select(item =>
            {
                var profile = profilesByPartyId.GetValueOrDefault(item.Id);
                var capacitySummary = BuildCapacitySummary(
                    profile?.CapacityHoursPerWeek ?? 40m,
                    projectAllocationsByPartyId.GetValueOrDefault(item.Id).Current ?? [],
                    capacityBlocksByPartyId.GetValueOrDefault(item.Id) ?? []);
                var skillSummary = string.Join(
                    ", ",
                    (partySkillsByPartyId.GetValueOrDefault(item.Id) ?? [])
                        .Select(skill => $"{skill.SkillName} ({skill.Proficiency})"));

                return new WorkforceListItemModel(
                    item.Id,
                    item.DisplayName,
                    item.PartyType,
                    item.IsSensitive,
                    profile?.WorkforceKind,
                    profile?.Status ?? "No profile",
                    profile?.JobTitle ?? string.Empty,
                    profile?.Discipline ?? string.Empty,
                    profile?.HomeUnitPartyId is Guid homeUnitPartyId ? namesByPartyId.GetValueOrDefault(homeUnitPartyId) ?? string.Empty : string.Empty,
                    profile?.ManagerPartyId is Guid managerPartyId ? namesByPartyId.GetValueOrDefault(managerPartyId) ?? string.Empty : string.Empty,
                    rolesByPartyId.GetValueOrDefault(item.Id) ?? [],
                    profile is not null,
                    item.UpdatedAtUtc,
                    profile?.Seniority ?? string.Empty,
                    profile?.Location ?? string.Empty,
                    skillSummary,
                    profile is null ? null : capacitySummary.AvailabilityState,
                    profile is null ? 0m : capacitySummary.AvailablePercent,
                    ToDateOnly(profile?.EndDateUtc),
                    profile is null ? null : capacitySummary.NextAvailabilityOn);
            })
            .OrderBy(item => item.DisplayName)
            .ToList();
    }

    public async Task<WorkforceWorkspaceModel?> GetWorkforceWorkspaceAsync(Guid partyId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var profileWorkspace = await GetWorkforceProfileWorkspaceCoreAsync(dbContext, partyId, cancellationToken);
        if (profileWorkspace is null)
        {
            return null;
        }

        var capacityWorkspace = await GetWorkforceCapacityWorkspaceCoreAsync(
            dbContext,
            partyId,
            profileWorkspace.Profile.CapacityHoursPerWeek,
            cancellationToken);
        return CombineWorkforceWorkspaces(profileWorkspace, capacityWorkspace);
    }

    public async Task<WorkforceProfileWorkspaceModel?> GetWorkforceProfileWorkspaceAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await GetWorkforceProfileWorkspaceCoreAsync(dbContext, partyId, cancellationToken);
    }

    public async Task<WorkforceCapacityWorkspaceModel?> GetWorkforceCapacityWorkspaceAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var isSupportedParty = await dbContext.Set<Party>()
            .AnyAsync(item => item.Id == partyId && item.PartyType != PartyType.AiAgent, cancellationToken);
        if (!isSupportedParty)
        {
            return null;
        }

        var capacityHoursPerWeek = await dbContext.Set<WorkforceProfile>()
            .Where(item => item.PartyId == partyId)
            .Select(item => (decimal?)item.CapacityHoursPerWeek)
            .SingleOrDefaultAsync(cancellationToken) ?? 40m;
        return await GetWorkforceCapacityWorkspaceCoreAsync(
            dbContext,
            partyId,
            capacityHoursPerWeek,
            cancellationToken);
    }

    private static Task<WorkforceProfileWorkspaceModel?> GetWorkforceProfileWorkspaceCoreAsync(CrmHrDbContext context, Guid partyId, CancellationToken cancellationToken) =>
        WorkforceProfileQueries.ReadWorkspaceAsync(context, partyId, cancellationToken);

    private async Task<WorkforceCapacityWorkspaceModel> GetWorkforceCapacityWorkspaceCoreAsync(
        CrmHrDbContext dbContext,
        Guid partyId,
        decimal capacityHoursPerWeek,
        CancellationToken cancellationToken)
    {
        var capacityBlocks = (await GetCapacityBlockMapAsync(dbContext, [partyId], cancellationToken)).GetValueOrDefault(partyId) ?? [];
        var projectAllocations = (await GetProjectAllocationMapAsync(dbContext, [partyId], cancellationToken)).GetValueOrDefault(partyId);

        return new WorkforceCapacityWorkspaceModel(
            partyId,
            capacityBlocks,
            projectAllocations.Display ?? [],
            BuildCapacitySummary(capacityHoursPerWeek, projectAllocations.Current ?? [], capacityBlocks));
    }

    private static WorkforceWorkspaceModel CombineWorkforceWorkspaces(
        WorkforceProfileWorkspaceModel profileWorkspace,
        WorkforceCapacityWorkspaceModel capacityWorkspace)
    {
        return new WorkforceWorkspaceModel(
            profileWorkspace.PartyId,
            profileWorkspace.DisplayName,
            profileWorkspace.Summary,
            profileWorkspace.PartyType,
            profileWorkspace.LifecycleStatus,
            profileWorkspace.IsSensitive,
            profileWorkspace.LastChangedBy,
            profileWorkspace.UpdatedAtUtc,
            profileWorkspace.Roles,
            profileWorkspace.PrimaryEmail,
            profileWorkspace.PrimaryPhone,
            profileWorkspace.HomeUnitName,
            profileWorkspace.ManagerName,
            profileWorkspace.Profile,
            profileWorkspace.SkillCatalog,
            profileWorkspace.Skills,
            capacityWorkspace.CapacityBlocks,
            capacityWorkspace.ProjectAllocations,
            capacityWorkspace.CapacitySummary);
    }

    public Task<Result<Guid>> SaveWorkforceProfileAsync(WorkforceProfileEditorModel model, CancellationToken cancellationToken = default,
        Action<WorkforceSaveReceipt<WorkforceProfileEditorModel>>? onCommitted = null) =>
        workforceProfiles.SaveWorkforceProfileAsync(model, cancellationToken, onCommitted);

    public async Task<IReadOnlyList<SkillCatalogItemModel>> ListSkillCatalogAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await WorkforceProfileQueries.ReadCatalogAsync(dbContext, cancellationToken);
    }

    public Task<Result<Guid>> SaveSkillDefinitionAsync(SkillDefinitionEditorModel model, CancellationToken cancellationToken = default,
        Action<WorkforceSaveReceipt<SkillDefinitionEditorModel>>? onCommitted = null) =>
        workforceSkills.SaveSkillDefinitionAsync(model, cancellationToken, onCommitted);

    public Task<Result<Guid>> SavePartySkillAsync(PartySkillEditorModel model, CancellationToken cancellationToken = default,
        Action<WorkforceSaveReceipt<PartySkillEditorModel>>? onCommitted = null) =>
        workforceSkills.SavePartySkillAsync(model, cancellationToken, onCommitted);

    public Task<Result> DeletePartySkillAsync(Guid partySkillId, CancellationToken cancellationToken = default,
        PartySkillObservation? original = null, Action<WorkforceDeleteReceipt>? onCommitted = null) =>
        workforceSkills.DeletePartySkillAsync(partySkillId, cancellationToken, original, onCommitted);

    public Task<Result<Guid>> SaveCapacityBlockAsync(CapacityBlockEditorModel model, CancellationToken cancellationToken = default,
        Action<WorkforceSaveReceipt<CapacityBlockEditorModel>>? onCommitted = null) =>
        workforceCapacity.SaveCapacityBlockAsync(model, cancellationToken, onCommitted);

    public Task<Result> DeleteCapacityBlockAsync(Guid capacityBlockId, CancellationToken cancellationToken = default,
        CapacityBlockObservation? original = null, Action<WorkforceDeleteReceipt>? onCommitted = null) =>
        workforceCapacity.DeleteCapacityBlockAsync(capacityBlockId, cancellationToken, original, onCommitted);

    public async Task<StaffingRequestPage> SearchStaffingRequestsAsync(
        StaffingRequestQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var normalized = NormalizeStaffingRequestQuery(query);
        var project = await projectRecordQueryService.GetNameMatchAsync(normalized.ProjectId, normalized.SearchText, cancellationToken);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<StaffingRequest> candidates = dbContext.Set<StaffingRequest>()
            .AsNoTracking()
            .Where(item => item.ProjectId == normalized.ProjectId && (project == null || item.ProjectLifetimeId == project.LifetimeId));
        if (normalized.Status.HasValue)
        {
            candidates = candidates.Where(item => item.Status == normalized.Status.Value);
        }

        if (!string.IsNullOrEmpty(normalized.SearchText))
        {
            var search = normalized.SearchText.ToUpperInvariant();
            candidates = candidates.Where(item =>
                item.Title.ToUpper().Contains(search) ||
                item.NeededRole.ToUpper().Contains(search) ||
                item.Notes.ToUpper().Contains(search) ||
                (project != null && project.Matches) ||
                dbContext.Set<Party>().Any(party =>
                    (party.Id == item.RequestedByPartyId ||
                     party.Id == item.DeliveryUnitPartyId) &&
                    party.DisplayName.ToUpper().Contains(search)) ||
                dbContext.Set<SkillDefinition>().Any(skill =>
                    (skill.Name.ToUpper().Contains(search) ||
                     skill.Category.ToUpper().Contains(search)) &&
                    item.NeededSkillsJson.Contains(skill.Id.ToString())));
        }

        var totalCount = await candidates.CountAsync(cancellationToken);
        var requests = await candidates
            .OrderBy(item => item.Status)
            .ThenBy(item => item.StartDateUtc ?? DateTimeOffset.MinValue)
            .ThenBy(item => item.Title)
            .ThenBy(item => item.Id)
            .Skip(normalized.PageIndex * normalized.PageSize)
            .Take(normalized.PageSize)
            .ToListAsync(cancellationToken);
        if (requests.Count == 0)
        {
            return new StaffingRequestPage(
                [],
                normalized.PageIndex,
                normalized.PageSize,
                totalCount);
        }

        var projectName = project?.Name ?? string.Empty;
        var partyIds = requests
            .SelectMany(item => new[] { item.RequestedByPartyId, item.DeliveryUnitPartyId })
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToList();
        var partyNames = partyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<Party>()
                .AsNoTracking()
                .Where(item => partyIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);
        var skillIds = requests
            .SelectMany(item => ParseSkillIds(item.NeededSkillsJson))
            .Distinct()
            .ToList();
        var skillsById = skillIds.Count == 0
            ? new Dictionary<Guid, SkillCatalogItemModel>()
            : await dbContext.Set<SkillDefinition>()
                .AsNoTracking()
                .Where(item => skillIds.Contains(item.Id))
                .Select(item => new SkillCatalogItemModel(
                    item.Id,
                    item.Name,
                    item.Category,
                    item.Description,
                    item.IsActive))
                .ToDictionaryAsync(item => item.Id, cancellationToken);

        var items = requests
            .Select(item => new StaffingRequestItemModel(
                item.Id,
                item.ProjectId,
                projectName,
                item.RequestedByPartyId,
                item.RequestedByPartyId.HasValue
                    ? partyNames.GetValueOrDefault(item.RequestedByPartyId.Value) ?? string.Empty
                    : string.Empty,
                item.DeliveryUnitPartyId,
                item.DeliveryUnitPartyId.HasValue
                    ? partyNames.GetValueOrDefault(item.DeliveryUnitPartyId.Value) ?? string.Empty
                    : string.Empty,
                item.Title,
                item.NeededRole,
                ParseSkillIds(item.NeededSkillsJson)
                    .Select(skillId => skillsById.GetValueOrDefault(skillId))
                    .Where(skill => skill is not null)
                    .Cast<SkillCatalogItemModel>()
                    .ToList(),
                ToDateOnly(item.StartDateUtc),
                ToDateOnly(item.EndDateUtc),
                item.AllocationPercent,
                item.Status,
                item.Notes,
                item.ProjectLifetimeId) { Original = StaffingRequestEditValues.Observe(item) })
            .ToList();
        return new StaffingRequestPage(
            items,
            normalized.PageIndex,
            normalized.PageSize,
            totalCount);
    }

    public Task<Result<Guid>> SaveStaffingRequestAsync(StaffingRequestEditorModel model, CancellationToken cancellationToken = default,
        Action<StaffingRequestSaveReceipt>? onCommitted = null) => staffingRequests.SaveAsync(model, cancellationToken, onCommitted);

    public async Task DeleteStaffingRequestAsync(Guid staffingRequestId, CancellationToken cancellationToken = default,
        StaffingRequestObservation? original = null, Action<StaffingRequestDeleteReceipt>? onCommitted = null) {
        var result = await staffingRequests.DeleteAsync(staffingRequestId, original, cancellationToken, onCommitted);
        if (result.IsFailure) {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => $"{error.Code}: {error.Message}")));
        }
    }

    public async Task<StaffingCandidatePage> SearchStaffingCandidatesAsync(
        StaffingCandidateQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var normalized = NormalizeStaffingCandidateQuery(query);
        var todayUtc = new DateTimeOffset(clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
        var tomorrowUtc = todayUtc.AddDays(1);
        var nearAvailabilityUtc = todayUtc.AddDays(31);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var assignmentRows = await ProjectAssignmentReporting.ForWorkforceAsync(dbContext, workAssignments, projectRecordQueryService, cancellationToken);
        var candidateProfiles =
            from profile in ProjectAssignmentReporting.ReadRoot(dbContext, dbContext.Set<WorkforceProfile>().AsNoTracking())
            join party in ProjectAssignmentReporting.ReadRoot(dbContext, dbContext.Set<Party>().AsNoTracking())
                on profile.PartyId equals party.Id
            select new
            {
                Profile = profile,
                Party = party
            };
        if (normalized.SkillId.HasValue)
        {
            candidateProfiles = candidateProfiles.Where(candidate =>
                dbContext.Set<PartySkill>().Any(skill =>
                    skill.PartyId == candidate.Party.Id &&
                    skill.SkillId == normalized.SkillId.Value));
        }

        if (!string.IsNullOrEmpty(normalized.SearchText))
        {
            var search = normalized.SearchText.ToUpperInvariant();
            var hasProficiency = Enum.TryParse<SkillProficiencyLevel>(
                normalized.SearchText,
                ignoreCase: true,
                out var proficiency);
            candidateProfiles = candidateProfiles.Where(candidate =>
                candidate.Party.DisplayName.ToUpper().Contains(search) ||
                candidate.Profile.JobTitle.ToUpper().Contains(search) ||
                candidate.Profile.Discipline.ToUpper().Contains(search) ||
                candidate.Profile.Seniority.ToUpper().Contains(search) ||
                candidate.Profile.Location.ToUpper().Contains(search) ||
                dbContext.Set<PartySkill>().Any(partySkill =>
                    partySkill.PartyId == candidate.Party.Id &&
                    ((hasProficiency && partySkill.Proficiency == proficiency) ||
                     dbContext.Set<SkillDefinition>().Any(skill =>
                         skill.Id == partySkill.SkillId &&
                         (skill.Name.ToUpper().Contains(search) ||
                          skill.Category.ToUpper().Contains(search))))));
        }

        var capacityCandidates = candidateProfiles.Select(candidate => new
        {
            candidate.Party.Id,
            candidate.Party.DisplayName,
            candidate.Party.PartyType,
            candidate.Profile.WorkforceKind,
            candidate.Profile.JobTitle,
            candidate.Profile.Discipline,
            candidate.Profile.Seniority,
            candidate.Profile.Location,
            ActiveAllocationPercent = assignmentRows
                .Where(allocation =>
                    allocation.PartyId == candidate.Party.Id &&
                    allocation.AllocationPercent.HasValue &&
                    (!allocation.StartsAtUtc.HasValue || allocation.StartsAtUtc.Value < tomorrowUtc) &&
                    (!allocation.EndsAtUtc.HasValue || allocation.EndsAtUtc.Value >= todayUtc))
                .Sum(allocation => allocation.AllocationPercent) ?? 0m,
            ActiveBlockedPercent = dbContext.Set<CapacityBlock>()
                .Where(block =>
                    block.PartyId == candidate.Party.Id &&
                    block.StartDateUtc < tomorrowUtc &&
                    block.EndDateUtc >= todayUtc)
                .Sum(block => (decimal?)block.Percentage) ?? 0m,
            NextAllocationAtUtc = assignmentRows
                .Where(allocation =>
                    allocation.PartyId == candidate.Party.Id &&
                    allocation.EndsAtUtc.HasValue &&
                    allocation.EndsAtUtc.Value >= todayUtc)
                .Min(allocation => allocation.EndsAtUtc),
            NextCapacityBlockAtUtc = dbContext.Set<CapacityBlock>()
                .Where(block =>
                    block.PartyId == candidate.Party.Id &&
                    block.EndDateUtc >= todayUtc)
                .Min(block => (DateTimeOffset?)block.EndDateUtc)
        });
        if (normalized.AvailabilityState.HasValue)
        {
            capacityCandidates = normalized.AvailabilityState.Value switch
            {
                WorkforceAvailabilityState.Bench => capacityCandidates.Where(candidate =>
                    candidate.ActiveAllocationPercent <= 10m &&
                    candidate.ActiveBlockedPercent < 25m),
                WorkforceAvailabilityState.Overallocated => capacityCandidates.Where(candidate =>
                    candidate.ActiveAllocationPercent + candidate.ActiveBlockedPercent > 100m),
                WorkforceAvailabilityState.NearAvailable => capacityCandidates.Where(candidate =>
                    candidate.ActiveAllocationPercent + candidate.ActiveBlockedPercent <= 100m &&
                    (candidate.ActiveAllocationPercent > 10m ||
                     candidate.ActiveBlockedPercent >= 25m) &&
                    ((candidate.NextAllocationAtUtc.HasValue &&
                      candidate.NextAllocationAtUtc.Value < nearAvailabilityUtc) ||
                     (candidate.NextCapacityBlockAtUtc.HasValue &&
                      candidate.NextCapacityBlockAtUtc.Value < nearAvailabilityUtc))),
                WorkforceAvailabilityState.Allocated => capacityCandidates.Where(candidate =>
                    candidate.ActiveAllocationPercent + candidate.ActiveBlockedPercent <= 100m &&
                    (candidate.ActiveAllocationPercent > 10m ||
                     candidate.ActiveBlockedPercent >= 25m) &&
                    (!candidate.NextAllocationAtUtc.HasValue ||
                     candidate.NextAllocationAtUtc.Value >= nearAvailabilityUtc) &&
                    (!candidate.NextCapacityBlockAtUtc.HasValue ||
                     candidate.NextCapacityBlockAtUtc.Value >= nearAvailabilityUtc)),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(query),
                    normalized.AvailabilityState,
                    "The staffing availability state is not supported.")
            };
        }

        var totalCount = await capacityCandidates.CountAssignmentReportAsync(cancellationToken);
        var rows = await capacityCandidates
            .OrderByDescending(candidate =>
                candidate.ActiveAllocationPercent <= 10m &&
                candidate.ActiveBlockedPercent < 25m)
            .ThenByDescending(candidate =>
                candidate.ActiveAllocationPercent + candidate.ActiveBlockedPercent >= 100m
                    ? 0m
                    : 100m - candidate.ActiveAllocationPercent - candidate.ActiveBlockedPercent)
            .ThenBy(candidate => candidate.DisplayName)
            .ThenBy(candidate => candidate.Id)
            .Skip(normalized.PageIndex * normalized.PageSize)
            .Take(normalized.PageSize)
            .ToAssignmentReportListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return new StaffingCandidatePage(
                [],
                normalized.PageIndex,
                normalized.PageSize,
                totalCount);
        }

        var partyIds = rows.Select(item => item.Id).ToList();
        var currentAffiliationRows = await (
                from affiliation in dbContext
                    .Set<PartyOrganizationAffiliation>()
                    .AsNoTracking()
                where partyIds.Contains(affiliation.PersonPartyId) &&
                      (!affiliation.ValidFromUtc.HasValue ||
                       affiliation.ValidFromUtc.Value <= todayUtc) &&
                      (!affiliation.ValidToUtc.HasValue ||
                       affiliation.ValidToUtc.Value >= todayUtc)
                join organization in ProjectAssignmentReporting.ReadRoot(dbContext, dbContext.Set<Party>().AsNoTracking())
                    on affiliation.OrganizationPartyId equals organization.Id
                select new
                {
                    affiliation.Id,
                    affiliation.PersonPartyId,
                    affiliation.AffiliationKind,
                    affiliation.IsPrimary,
                    affiliation.JobTitle,
                    affiliation.ValidFromUtc,
                    affiliation.UpdatedAtUtc,
                    OrganizationName = organization.DisplayName
                })
            .ToAssignmentReportListAsync(cancellationToken);
        var currentAffiliationsByPartyId = currentAffiliationRows
            .GroupBy(item => item.PersonPartyId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.IsPrimary)
                    .ThenByDescending(item => item.ValidFromUtc)
                    .ThenByDescending(item => item.UpdatedAtUtc)
                    .ThenBy(item => item.Id)
                    .ToArray());
        var partySkillsByPartyId = await GetPartySkillMapAsync(
            dbContext,
            partyIds,
            cancellationToken);
        var items = rows.Select(item =>
        {
            var nextAvailabilityAtUtc = MinNullable(
                item.NextAllocationAtUtc,
                item.NextCapacityBlockAtUtc);
            var nextAvailabilityOn = ToDateOnly(nextAvailabilityAtUtc);
            var availabilityState = ResolveAvailabilityState(
                item.ActiveAllocationPercent,
                item.ActiveBlockedPercent,
                nextAvailabilityOn);
            var skillSummary = string.Join(
                ", ",
                (partySkillsByPartyId.GetValueOrDefault(item.Id) ?? [])
                    .Select(skill => $"{skill.SkillName} ({skill.Proficiency})"));
            var currentAffiliations =
                currentAffiliationsByPartyId.GetValueOrDefault(item.Id) ?? [];
            var selectedAffiliation = currentAffiliations.FirstOrDefault();
            var classification = WorkforceRecordClassificationPolicy.Resolve(
                selectedAffiliation?.AffiliationKind,
                item.WorkforceKind,
                item.PartyType,
                hasDeliveryUnitRole: false);
            var primaryAffiliationText = selectedAffiliation is null
                ? item.JobTitle
                : FormatOrganizationAffiliation(
                    selectedAffiliation.OrganizationName,
                    selectedAffiliation.JobTitle);
            var otherAffiliationsSummary = string.Join(
                "; ",
                currentAffiliations
                    .Skip(1)
                    .Select(affiliation => FormatOrganizationAffiliation(
                        affiliation.OrganizationName,
                        affiliation.JobTitle))
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            return new StaffingCandidateItemModel(
                item.Id,
                item.DisplayName,
                item.PartyType,
                item.JobTitle,
                item.Discipline,
                item.Seniority,
                item.Location,
                skillSummary,
                availabilityState,
                Math.Max(0m, 100m - item.ActiveAllocationPercent - item.ActiveBlockedPercent),
                nextAvailabilityOn,
                classification,
                primaryAffiliationText,
                otherAffiliationsSummary);
        }).ToList();
        return new StaffingCandidatePage(
            items,
            normalized.PageIndex,
            normalized.PageSize,
            totalCount);
    }

    private static string FormatOrganizationAffiliation(
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
            : $"{organization} / {title}";
    }

    public async Task<StaffingDashboardModel> GetStaffingDashboardAsync(CancellationToken cancellationToken = default)
    {
        var todayUtc = new DateTimeOffset(clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
        var tomorrowUtc = todayUtc.AddDays(1);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var assignmentRows = await ProjectAssignmentReporting.ForWorkforceAsync(dbContext, workAssignments, projectRecordQueryService, cancellationToken);

        var openRequests = dbContext.Set<StaffingRequest>()
            .AsNoTracking()
            .Where(item =>
                item.Status == StaffingRequestStatus.Draft ||
                item.Status == StaffingRequestStatus.Open ||
                item.Status == StaffingRequestStatus.Proposed ||
                item.Status == StaffingRequestStatus.Confirmed);
        var openRequestCount = await openRequests.CountAssignmentReportAsync(cancellationToken);
        var openDemandPercent = await openRequests
            .SumAsync(item => (decimal?)item.AllocationPercent, cancellationToken) ?? 0m;

        var workforceCapacity = ProjectAssignmentReporting.ReadRoot(dbContext, dbContext.Set<WorkforceProfile>().AsNoTracking())
            .Select(profile => new
            {
                ActiveAllocationPercent = assignmentRows
                    .Where(allocation =>
                        allocation.PartyId == profile.PartyId &&
                        allocation.AllocationPercent.HasValue &&
                        (!allocation.StartsAtUtc.HasValue || allocation.StartsAtUtc.Value < tomorrowUtc) &&
                        (!allocation.EndsAtUtc.HasValue || allocation.EndsAtUtc.Value >= todayUtc))
                    .Sum(allocation => allocation.AllocationPercent) ?? 0m,
                ActiveBlockedPercent = dbContext.Set<CapacityBlock>()
                    .Where(block =>
                        block.PartyId == profile.PartyId &&
                        block.StartDateUtc < tomorrowUtc &&
                        block.EndDateUtc >= todayUtc)
                    .Sum(block => (decimal?)block.Percentage) ?? 0m
            });
        var capacityCounts = await workforceCapacity
            .GroupBy(_ => 1)
            .Select(group => new StaffingCapacityCounts(
                group.Count(item =>
                    item.ActiveAllocationPercent <= 10m &&
                    item.ActiveBlockedPercent < 25m),
                group.Count(item =>
                    item.ActiveAllocationPercent + item.ActiveBlockedPercent > 100m)))
            .SingleAssignmentReportOrDefaultAsync(cancellationToken);
        return new StaffingDashboardModel(
            openRequestCount,
            openDemandPercent,
            capacityCounts?.BenchCount ?? 0,
            capacityCounts?.OverallocatedCount ?? 0);
    }

    private static StaffingRequestQuery NormalizeStaffingRequestQuery(StaffingRequestQuery query)
    {
        if (query.ProjectId == Guid.Empty)
        {
            throw new ArgumentException("A project identifier is required.", nameof(query));
        }

        ValidateStaffingPage(query.PageIndex, query.PageSize, nameof(query));
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.Status,
                "The staffing request status is not supported.");
        }

        return query with
        {
            SearchText = NormalizeStaffingSearch(query.SearchText, nameof(query))
        };
    }

    private static StaffingCandidateQuery NormalizeStaffingCandidateQuery(StaffingCandidateQuery query)
    {
        ValidateStaffingPage(query.PageIndex, query.PageSize, nameof(query));
        if (query.SkillId == Guid.Empty)
        {
            throw new ArgumentException(
                "A staffing skill identifier cannot be empty.",
                nameof(query));
        }

        if (query.AvailabilityState.HasValue &&
            !Enum.IsDefined(query.AvailabilityState.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.AvailabilityState,
                "The staffing availability state is not supported.");
        }

        return query with
        {
            SearchText = NormalizeStaffingSearch(query.SearchText, nameof(query))
        };
    }

    private static void ValidateStaffingPage(
        int pageIndex,
        int pageSize,
        string parameterName)
    {
        if (pageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                pageIndex,
                "Staffing page index cannot be negative.");
        }

        if (pageSize is < 1 or > StaffingQueryLimits.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                pageSize,
                $"Staffing page size must be between 1 and {StaffingQueryLimits.MaximumPageSize}.");
        }

        if (pageIndex > int.MaxValue / pageSize)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                pageIndex,
                "Staffing page offset is too large.");
        }
    }

    private static string NormalizeStaffingSearch(
        string? searchText,
        string parameterName)
    {
        var normalized = searchText?.Trim() ?? string.Empty;
        if (normalized.Length > StaffingQueryLimits.MaximumSearchLength)
        {
            throw new ArgumentException(
                $"Staffing search cannot exceed {StaffingQueryLimits.MaximumSearchLength} characters.",
                parameterName);
        }

        return normalized;
    }

    private static DateTimeOffset? MinNullable(
        DateTimeOffset? first,
        DateTimeOffset? second)
    {
        if (!first.HasValue)
        {
            return second;
        }

        if (!second.HasValue)
        {
            return first;
        }

        return first.Value <= second.Value ? first : second;
    }

    private static WorkforceCapacitySummaryModel BuildCapacitySummary(
        decimal capacityHoursPerWeek,
        IReadOnlyList<ProjectAllocationItemModel> allocations,
        IReadOnlyList<CapacityBlockItemModel> capacityBlocks)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeAllocationPercent = allocations
            .Where(item => item.IsActive)
            .Sum(item => item.AllocationPercent);
        var activeBlockedPercent = capacityBlocks
            .Where(item => item.IsActive)
            .Sum(item => item.Percentage);
        var rawAvailablePercent = 100m - activeAllocationPercent - activeBlockedPercent;
        var availablePercent = Math.Max(0m, rawAvailablePercent);
        var nextAvailabilityOn = allocations
            .Where(item => item.EndsOn.HasValue && item.EndsOn.Value >= today)
            .Select(item => item.EndsOn!.Value)
            .Concat(capacityBlocks
                .Where(item => item.EndDate >= today)
                .Select(item => item.EndDate))
            .OrderBy(item => item)
            .FirstOrDefault();
        var availabilityState = ResolveAvailabilityState(activeAllocationPercent, activeBlockedPercent, nextAvailabilityOn);

        return new WorkforceCapacitySummaryModel(
            capacityHoursPerWeek <= 0m ? 40m : capacityHoursPerWeek,
            activeAllocationPercent,
            activeBlockedPercent,
            availablePercent,
            availabilityState,
            BuildAvailabilityMessage(availabilityState, activeAllocationPercent, activeBlockedPercent, nextAvailabilityOn),
            nextAvailabilityOn == default ? null : nextAvailabilityOn,
            availabilityState == WorkforceAvailabilityState.Overallocated,
            availabilityState == WorkforceAvailabilityState.Bench);
    }

    private static WorkforceAvailabilityState ResolveAvailabilityState(
        decimal activeAllocationPercent,
        decimal activeBlockedPercent,
        DateOnly? nextAvailabilityOn)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (activeAllocationPercent + activeBlockedPercent > 100m)
        {
            return WorkforceAvailabilityState.Overallocated;
        }

        if (activeAllocationPercent <= 10m && activeBlockedPercent < 25m)
        {
            return WorkforceAvailabilityState.Bench;
        }

        if (nextAvailabilityOn.HasValue && nextAvailabilityOn.Value <= today.AddDays(30))
        {
            return WorkforceAvailabilityState.NearAvailable;
        }

        return WorkforceAvailabilityState.Allocated;
    }

    private static string BuildAvailabilityMessage(
        WorkforceAvailabilityState availabilityState,
        decimal activeAllocationPercent,
        decimal activeBlockedPercent,
        DateOnly? nextAvailabilityOn)
    {
        return availabilityState switch
        {
            WorkforceAvailabilityState.Overallocated => $"Overallocated at {(activeAllocationPercent + activeBlockedPercent):0.##}% commitment.",
            WorkforceAvailabilityState.Bench => "Bench or lightly committed.",
            WorkforceAvailabilityState.NearAvailable when nextAvailabilityOn.HasValue => $"Near availability on {nextAvailabilityOn.Value:yyyy-MM-dd}.",
            _ => $"Allocated at {activeAllocationPercent:0.##}% with {activeBlockedPercent:0.##}% blocked."
        };
    }

    private static IReadOnlyList<Guid> ParseSkillIds(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Task<IReadOnlyDictionary<Guid, IReadOnlyList<PartySkillItemModel>>> GetPartySkillMapAsync(CrmHrDbContext context,
        IReadOnlyList<Guid> partyIds, CancellationToken cancellationToken) => WorkforceProfileQueries.ReadSkillsAsync(context, partyIds, cancellationToken);

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<CapacityBlockItemModel>>> GetCapacityBlockMapAsync(
        CrmHrDbContext dbContext,
        IReadOnlyList<Guid> partyIds,
        CancellationToken cancellationToken)
    {
        if (partyIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<CapacityBlockItemModel>>();
        }

        var blocks = await dbContext.Set<CapacityBlock>()
            .Where(item => partyIds.Contains(item.PartyId))
            .ToListAsync(cancellationToken);
        if (blocks.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<CapacityBlockItemModel>>();
        }

        blocks = blocks
            .OrderBy(item => item.StartDateUtc)
            .ThenBy(item => item.EndDateUtc)
            .ToList();

        var projectIds = blocks.Where(item => item.RelatedProjectId.HasValue && item.RelatedProjectId.Value != Guid.Empty)
            .Select(item => item.RelatedProjectId!.Value).Distinct().ToList();
        var projectNames = projectIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await projectRecordQueryService.GetManyAsync(projectIds, cancellationToken))
                .ToDictionary(item => item.Id, item => item.Name);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return blocks
            .GroupBy(item => item.PartyId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CapacityBlockItemModel>)group.Select(item =>
                {
                    var startDate = ToDateOnly(item.StartDateUtc) ?? today;
                    var endDate = ToDateOnly(item.EndDateUtc) ?? startDate;
                    return new CapacityBlockItemModel(
                        item.Id,
                        item.BlockKind,
                        startDate,
                        endDate,
                        item.Percentage,
                        item.RelatedProjectId,
                        item.RelatedProjectId.HasValue ? projectNames.GetValueOrDefault(item.RelatedProjectId.Value) ?? string.Empty : string.Empty,
                        item.Notes,
                        startDate <= today && endDate >= today,
                        startDate > today) { Original = WorkforceEditValues.Capacity(item) };
                })
                .OrderBy(item => item.StartDate)
                .ThenBy(item => item.BlockKind)
                .ToList());
    }

    private async Task<IReadOnlyDictionary<Guid, (IReadOnlyList<ProjectAllocationItemModel> Display,
        IReadOnlyList<ProjectAllocationItemModel> Current)>> GetProjectAllocationMapAsync(
        CrmHrDbContext dbContext,
        IReadOnlyList<Guid> partyIds,
        CancellationToken cancellationToken) {
        if (partyIds.Count == 0) {
            return new Dictionary<Guid, (IReadOnlyList<ProjectAllocationItemModel>, IReadOnlyList<ProjectAllocationItemModel>)>();
        }

        var assignmentRows = await ProjectAssignmentReporting.ForPartiesAsync(dbContext, workAssignments, projectRecordQueryService, partyIds, cancellationToken);
        var assignments = await assignmentRows
            .Where(item => partyIds.Contains(item.PartyId) && item.AllocationPercent.HasValue)
            .Select(item => new {
                item.Id,
                item.ProjectId,
                item.ProjectLifetimeId,
                item.CurrentProjectLifetimeId,
                item.PartyId,
                item.AssignmentKind,
                item.AllocationPercent,
                item.StartsAtUtc,
                item.EndsAtUtc,
                item.Notes
            })
            .ToAssignmentReportListAsync(cancellationToken);
        if (assignments.Count == 0) {
            return new Dictionary<Guid, (IReadOnlyList<ProjectAllocationItemModel>, IReadOnlyList<ProjectAllocationItemModel>)>();
        }

        assignments = assignments
            .OrderBy(item => item.StartsAtUtc ?? DateTimeOffset.MinValue)
            .ThenBy(item => item.EndsAtUtc ?? DateTimeOffset.MaxValue)
            .ToList();

        var projectIds = assignments.Select(item => item.ProjectId).Where(projectId => projectId != Guid.Empty).Distinct().ToList();
        var partyNameIds = assignments.Select(item => item.PartyId).Distinct().ToList();
        var projectsById = (await projectRecordQueryService.GetManyAsync(projectIds, cancellationToken))
            .ToDictionary(item => item.Id);
        var partyNames = await dbContext.Set<Party>()
            .Where(item => partyNameIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return assignments
            .GroupBy(item => item.PartyId)
            .ToDictionary(
                group => group.Key,
                group => {
                    var allocations = group.Select(item => {
                        var startsOn = ToDateOnly(item.StartsAtUtc);
                        var endsOn = ToDateOnly(item.EndsAtUtc);
                        var isActive = (!startsOn.HasValue || startsOn.Value <= today) && (!endsOn.HasValue || endsOn.Value >= today);
                        var isFuture = startsOn.HasValue && startsOn.Value > today;
                        var project = projectsById.GetValueOrDefault(item.ProjectId);
                        var projectName = project is not null && (item.ProjectLifetimeId is null || item.ProjectLifetimeId == project.LifetimeId)
                            ? project.Name
                            : string.Empty;
                        var model = new ProjectAllocationItemModel(
                            item.Id,
                            item.ProjectId,
                            projectName,
                            item.PartyId,
                            partyNames.GetValueOrDefault(item.PartyId) ?? string.Empty,
                            MapProjectAssignmentRole(item.AssignmentKind),
                            item.AllocationPercent ?? 0m,
                            startsOn,
                            endsOn,
                            item.Notes,
                            isActive,
                            isFuture);
                        return (Item: model, IsCurrent: item.ProjectLifetimeId.HasValue && item.ProjectLifetimeId == item.CurrentProjectLifetimeId);
                    })
                    .OrderByDescending(item => item.Item.IsActive)
                    .ThenBy(item => item.Item.StartsOn)
                    .ThenBy(item => item.Item.ProjectName)
                    .ToArray();
                    return (Display: (IReadOnlyList<ProjectAllocationItemModel>)allocations.Select(item => item.Item).ToArray(),
                        Current: (IReadOnlyList<ProjectAllocationItemModel>)allocations.Where(item => item.IsCurrent).Select(item => item.Item).ToArray());
                });
    }

    private static DateTimeOffset? ToUtcDate(DateOnly? value)
    {
        return value.HasValue
            ? new DateTimeOffset(value.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : null;
    }

    private static DateOnly? ToDateOnly(DateTimeOffset? value)
    {
        return value.HasValue
            ? DateOnly.FromDateTime(value.Value.UtcDateTime)
            : null;
    }

    private static ProjectPartyAssignmentRole MapProjectAssignmentRole(ProjectPartyAssignmentKind role)
    {
        return role switch
        {
            ProjectPartyAssignmentKind.Customer => ProjectPartyAssignmentRole.Customer,
            ProjectPartyAssignmentKind.CustomerContact => ProjectPartyAssignmentRole.CustomerContact,
            ProjectPartyAssignmentKind.DeliveryUnit => ProjectPartyAssignmentRole.DeliveryUnit,
            ProjectPartyAssignmentKind.TeamMember => ProjectPartyAssignmentRole.TeamMember,
            ProjectPartyAssignmentKind.Manager => ProjectPartyAssignmentRole.Manager,
            ProjectPartyAssignmentKind.Partner => ProjectPartyAssignmentRole.Partner,
            ProjectPartyAssignmentKind.Vendor => ProjectPartyAssignmentRole.Vendor,
            ProjectPartyAssignmentKind.Stakeholder => ProjectPartyAssignmentRole.Stakeholder,
            ProjectPartyAssignmentKind.MeetingParticipant => ProjectPartyAssignmentRole.MeetingParticipant,
            ProjectPartyAssignmentKind.WorkItemAssignee => ProjectPartyAssignmentRole.WorkItemAssignee,
            ProjectPartyAssignmentKind.Reviewer => ProjectPartyAssignmentRole.Reviewer,
            ProjectPartyAssignmentKind.AiAgent => ProjectPartyAssignmentRole.AiAgent,
            ProjectPartyAssignmentKind.BillingContact => ProjectPartyAssignmentRole.BillingContact,
            ProjectPartyAssignmentKind.TechnicalContact => ProjectPartyAssignmentRole.TechnicalContact,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "The persisted project assignment role is not supported.")
        };
    }


}

public sealed class ProjectPartyIntegrationService(
    IDbContextFactory<CrmHrDbContext> dbContextFactory,
    PartyDirectoryService partyDirectoryService,
    ProjectPartyAssignmentNodePolicy projectPartyAssignmentNodePolicy,
    ProjectPartyAffiliationContextService
        projectPartyAffiliationContextService,
    IProjectWorkAssignmentCommands workAssignments,
    IClock clock,
    CoordinatedDatabaseTransaction coordinatedTransaction,
    ProjectRecordQueryService projectRecordQueryService,
    ProjectWriteAdmissionService admissions,
    DbContextOptions<CrmHrDbContext> contextOptions,
    ProjectAssignmentChanges assignmentChanges,
    ILogger<ProjectPartyIntegrationService>? logger = null) :
    IProjectPartyIntegrationBridge,
    IProjectPartyCostRateBridge,
    IProjectPartyDeletionStateQuery
{
    private static readonly ProjectPartyAssignmentKind[] AllocationAssignmentKinds =
    [
        ProjectPartyAssignmentKind.TeamMember,
        ProjectPartyAssignmentKind.DeliveryUnit,
        ProjectPartyAssignmentKind.Manager,
        ProjectPartyAssignmentKind.AiAgent,
        ProjectPartyAssignmentKind.Reviewer,
        ProjectPartyAssignmentKind.WorkItemAssignee
    ];

    public Task<bool> HasProjectDeletionStateForMutationAsync(ProjectWriteAdmission project, CancellationToken cancellationToken = default)
        => HasCreationCleanupStateAsync(project, () => admissions.RequireForMutationAsync(project, cancellationToken), cancellationToken);

    public Task<bool> HasReservationCancellationStateForMutationAsync(ProjectReservationCancellationPreparationContext original,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        return HasCreationCleanupStateAsync(original.Target.Project,
            () => admissions.RequireReservedCancellationForMutationAsync(original, cancellationToken), cancellationToken);
    }

    private async Task<bool> HasCreationCleanupStateAsync(ProjectWriteAdmission project, Func<Task> requireOriginal,
        CancellationToken cancellationToken) {
        var context = await coordinatedTransaction.CreateEnlistedAsync(contextOptions,
            static options => new CrmHrDbContext(options), cancellationToken);
        Exception? primary = null;
        try {
            coordinatedTransaction.RequireOwnerProfile(context);
            await requireOriginal();
            return await context.Set<ProjectPartyAssignment>().AnyAsync(row => row.ProjectId == project.ProjectId &&
                    row.ProjectLifetimeId == project.LifetimeId, cancellationToken)
                || await context.Set<CrmAccountConnectionProjectLink>().AnyAsync(row => row.ProjectId == project.ProjectId, cancellationToken);
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            try {
                await context.DisposeAsync();
            } catch (Exception cleanup) when (primary is not null) {
                throw new AggregateException("Project party cleanup observation and context release failed.", primary, cleanup);
            }
        }
    }

    public async Task<IReadOnlyList<ProjectPartyAssignmentSummaryModel>> ListAssignmentsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return (await ListAssignmentsDetailedAsync(projectId, cancellationToken))
            .Select(item => new ProjectPartyAssignmentSummaryModel(
                item.Id,
                item.ProjectId,
                item.PartyId,
                MapRole(item.Role),
                item.NodeKey,
                item.IsPrimary))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, ProjectPortfolioPartyContext>> GetPortfolioContextsAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        if (projectIds.Count == 0)
        {
            return new Dictionary<Guid, ProjectPortfolioPartyContext>();
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var assignmentRows = await ProjectAssignmentReporting.ForProjectsAsync(dbContext, workAssignments, projectRecordQueryService, projectIds, cancellationToken);
        var assignments = await assignmentRows
            .Where(item => item.ProjectLifetimeId != null && item.ProjectLifetimeId == item.CurrentProjectLifetimeId && projectIds.Contains(item.ProjectId) && string.IsNullOrWhiteSpace(item.NodeKey))
            .Join(
                ProjectAssignmentReporting.ReadRoot(dbContext, dbContext.Set<Party>()),
                assignment => assignment.PartyId,
                party => party.Id,
                (assignment, party) => new
                {
                    assignment.ProjectId,
                    assignment.AssignmentKind,
                    assignment.IsPrimary,
                    party.DisplayName
                })
            .OrderBy(item => item.ProjectId)
            .ThenByDescending(item => item.IsPrimary)
            .ThenBy(item => item.DisplayName)
            .ToAssignmentReportListAsync(cancellationToken);

        return assignments
            .GroupBy(item => item.ProjectId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var items = group
                        .Select(item => new ProjectPortfolioPartyItem(
                            MapPortfolioCategory(item.AssignmentKind),
                            ResolvePortfolioLabel(item.AssignmentKind),
                            item.DisplayName,
                            item.IsPrimary))
                        .Distinct()
                        .ToList();

                    return new ProjectPortfolioPartyContext(
                        ResolvePrimaryDisplayName(items, ProjectPartyPortfolioCategory.Customer),
                        ResolvePrimaryDisplayName(items, ProjectPartyPortfolioCategory.DeliveryUnit),
                        ResolvePrimaryDisplayName(items, ProjectPartyPortfolioCategory.Owner),
                        items,
                        string.Join(
                            '\n',
                            items.Select(item => $"{item.Label}:{item.DisplayName}")
                                .Distinct(StringComparer.OrdinalIgnoreCase)));
                });
    }

    public async Task<IReadOnlyList<ProjectPartyOption>> ListPartyOptionsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var assignmentRows = await ProjectAssignmentReporting.ForProjectsAsync(dbContext, workAssignments, projectRecordQueryService, [projectId], cancellationToken);
        var assignedPartyIds = await assignmentRows
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .Select(item => item.PartyId)
            .Distinct()
            .ToAssignmentReportListAsync(cancellationToken);
        var parties = await dbContext.Set<Party>()
            .AsNoTracking()
            .Select(party => new
            {
                party.Id,
                party.DisplayName,
                party.PartyType,
                party.IsSensitive
            })
            .ToAssignmentReportListAsync(cancellationToken);
        var partyIds = parties.Select(item => item.Id).ToList();
        var contacts = await dbContext.Set<PartyContactPoint>()
            .AsNoTracking()
            .Where(item => partyIds.Contains(item.PartyId) && item.IsPublic)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => new CrmPartyContactValue(item.PartyId, item.ContactType, item.Value, item.IsPrimary))
            .ToAssignmentReportListAsync(cancellationToken);
        var contactsByPartyId = contacts
            .GroupBy(item => item.PartyId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<CrmPartyContactValue>)group.ToList());
        var affiliationContexts =
            await projectPartyAffiliationContextService.LoadPartyContextsAsync(
                dbContext,
                parties.ToDictionary(
                    item => item.Id,
                    item => item.PartyType),
                cancellationToken);
        var assignedSet = assignedPartyIds.ToHashSet();

        return parties
            .OrderByDescending(item => assignedSet.Contains(item.Id))
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(item =>
            {
                var partyContacts = contactsByPartyId.GetValueOrDefault(item.Id) ?? [];
                return new ProjectPartyOption(
                    item.Id,
                    item.DisplayName,
                    ResolvePartyTypeLabel(item.PartyType),
                    MapProjectPartyType(item.PartyType),
                    item.IsSensitive
                        ? string.Empty
                        : ResolvePrimaryContactValue(partyContacts, PartyContactType.Email),
                    item.IsSensitive
                        ? string.Empty
                        : ResolvePrimaryContactValue(partyContacts, PartyContactType.Phone),
                    item.IsSensitive,
                    item.IsSensitive
                        ? null
                        : affiliationContexts.GetValueOrDefault(item.Id));
            })
            .ToList();
    }

    public async Task<ProjectPartyOption?> GetPartyOptionAsync(Guid partyId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>()
            .AsNoTracking()
            .Select(item => new
            {
                item.Id,
                item.DisplayName,
                item.PartyType,
                item.IsSensitive
            })
            .SingleOrDefaultAsync(item => item.Id == partyId, cancellationToken);
        if (party is null)
        {
            return null;
        }

        var contacts = await dbContext.Set<PartyContactPoint>()
            .AsNoTracking()
            .Where(item => item.PartyId == partyId && item.IsPublic)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => new CrmPartyContactValue(item.PartyId, item.ContactType, item.Value, item.IsPrimary))
            .ToListAsync(cancellationToken);
        var affiliationContexts =
            await projectPartyAffiliationContextService.LoadPartyContextsAsync(
                dbContext,
                new Dictionary<Guid, PartyType>
                {
                    [party.Id] = party.PartyType
                },
                cancellationToken);

        return new ProjectPartyOption(
            party.Id,
            party.DisplayName,
            ResolvePartyTypeLabel(party.PartyType),
            MapProjectPartyType(party.PartyType),
            party.IsSensitive
                ? string.Empty
                : ResolvePrimaryContactValue(contacts, PartyContactType.Email),
            party.IsSensitive
                ? string.Empty
                : ResolvePrimaryContactValue(contacts, PartyContactType.Phone),
            party.IsSensitive,
            party.IsSensitive
                ? null
                : affiliationContexts.GetValueOrDefault(party.Id));
    }

    public async Task<ProjectPartyCostRate?> GetInternalCostRateAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        if (partyId == Guid.Empty)
        {
            return null;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rate = await dbContext.Set<WorkforceProfile>()
            .AsNoTracking()
            .Where(profile => profile.PartyId == partyId && profile.InternalCostRate.HasValue)
            .Select(profile => new
            {
                profile.PartyId,
                Rate = profile.InternalCostRate!.Value,
                Unit = profile.RateUnit,
                CurrencyCode = profile.RateCurrencyCode
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (rate is null)
        {
            return null;
        }

        return new ProjectPartyCostRate(
            rate.PartyId,
            rate.Rate,
            rate.Unit,
            rate.CurrencyCode);
    }

    public Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return ListAssignmentsDetailedCoreAsync(
            projectId,
            Enum.GetValues<ProjectPartyAssignmentRole>(),
            orderResults: true,
            cancellationToken);
    }

    public async Task<ProjectPartyAssignmentCounts> GetAssignmentCountsAsync(
        Guid projectId,
        DateTimeOffset scheduleWindowStartUtc,
        DateTimeOffset scheduleWindowEndUtc,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            return ProjectPartyAssignmentCounts.Empty;
        }

        if (scheduleWindowStartUtc > scheduleWindowEndUtc)
        {
            throw new ArgumentException(
                "The assignment schedule window start cannot be after its end.",
                nameof(scheduleWindowStartUtc));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var assignmentRows = await ProjectAssignmentReporting.ForProjectsAsync(dbContext, workAssignments, projectRecordQueryService, [projectId], cancellationToken);
        var summary = await assignmentRows
            .AsNoTracking()
            .Where(assignment => assignment.ProjectId == projectId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalCount = group.Count(),
                AllocationCount = group.Count(assignment =>
                    assignment.AllocationPercent.HasValue ||
                    AllocationAssignmentKinds.Contains(assignment.AssignmentKind)),
                ScheduledCount = group.Count(assignment =>
                    (assignment.AllocationPercent.HasValue ||
                     AllocationAssignmentKinds.Contains(assignment.AssignmentKind)) &&
                    (!assignment.EndsAtUtc.HasValue ||
                     assignment.EndsAtUtc.Value >= scheduleWindowStartUtc) &&
                    (!assignment.StartsAtUtc.HasValue ||
                     assignment.StartsAtUtc.Value <= scheduleWindowEndUtc))
            })
            .SingleAssignmentReportOrDefaultAsync(cancellationToken);

        return summary is null
            ? ProjectPartyAssignmentCounts.Empty
            : new(
                summary.TotalCount,
                summary.AllocationCount,
                summary.ScheduledCount);
    }

    public async Task<ProjectPartyAssignmentPage> SearchAssignmentsDetailedAsync(
        ProjectPartyAssignmentQuery request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = Normalize(request);
        if (query.ProjectId == Guid.Empty || query.Roles.Count == 0)
        {
            return ProjectPartyAssignmentPage.Empty(query.PageSize);
        }

        var assignmentKinds = query.Roles
            .Select(MapRole)
            .Distinct()
            .ToArray();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var assignmentRows = await ProjectAssignmentReporting.ForProjectsAsync(dbContext, workAssignments, projectRecordQueryService, [query.ProjectId], cancellationToken);
        var candidates =
            from assignment in assignmentRows.AsNoTracking()
            join party in ProjectAssignmentReporting.ReadRoot(dbContext, dbContext.Set<Party>().AsNoTracking())
                on assignment.PartyId equals party.Id
            where assignment.ProjectId == query.ProjectId &&
                  assignmentKinds.Contains(assignment.AssignmentKind)
            select new
            {
                assignment.Id,
                assignment.ProjectId,
                assignment.ProjectLifetimeId,
                assignment.PartyId,
                assignment.PartyOrganizationAffiliationId,
                assignment.AssignmentKind,
                assignment.NodeKey,
                assignment.IsPrimary,
                assignment.AllocationPercent,
                assignment.StartsAtUtc,
                assignment.EndsAtUtc,
                assignment.Source,
                assignment.Notes,
                assignment.PhaseName,
                assignment.OpportunityId,
                party.DisplayName,
                party.PartyType,
                party.IsSensitive
            };

        if (query.AllocationOnly)
        {
            candidates = candidates.Where(item =>
                item.AllocationPercent.HasValue ||
                AllocationAssignmentKinds.Contains(item.AssignmentKind));
        }

        if (query.WindowStartUtc.HasValue)
        {
            var windowStartUtc = query.WindowStartUtc.Value;
            candidates = candidates.Where(item =>
                !item.EndsAtUtc.HasValue ||
                item.EndsAtUtc.Value >= windowStartUtc);
        }

        if (query.WindowEndUtc.HasValue)
        {
            var windowEndUtc = query.WindowEndUtc.Value;
            candidates = candidates.Where(item =>
                !item.StartsAtUtc.HasValue ||
                item.StartsAtUtc.Value <= windowEndUtc);
        }

        if (!string.IsNullOrEmpty(query.SearchText))
        {
            var search = query.SearchText.ToUpperInvariant();
            candidates = candidates.Where(item =>
                item.DisplayName.ToUpper().Contains(search) ||
                item.NodeKey.ToUpper().Contains(search) ||
                item.Source.ToUpper().Contains(search) ||
                item.Notes.ToUpper().Contains(search));
        }

        var totalCount = await candidates.CountAssignmentReportAsync(cancellationToken);
        var pageRows = await candidates
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.StartsAtUtc)
            .ThenBy(item => item.DisplayName)
            .ThenBy(item => item.Id)
            .Skip(query.PageIndex * query.PageSize)
            .Take(query.PageSize)
            .ToAssignmentReportListAsync(cancellationToken);
        var affiliationContexts = await projectPartyAffiliationContextService
            .LoadAssignmentContextsAsync(
                dbContext,
                pageRows
                    .Select(item => new ProjectPartyAffiliationReference(
                        item.Id,
                        item.PartyId,
                        item.PartyType,
                        item.PartyOrganizationAffiliationId,
                        item.IsSensitive))
                    .ToArray(),
                cancellationToken);
        var items = pageRows
            .Select(item => new ProjectPartyAssignmentDetail(
                item.Id,
                item.ProjectId,
                item.PartyId,
                MapRole(item.AssignmentKind),
                item.DisplayName,
                ResolvePartyTypeLabel(item.PartyType),
                MapProjectPartyType(item.PartyType),
                item.NodeKey,
                item.IsPrimary,
                item.AllocationPercent,
                item.StartsAtUtc,
                item.EndsAtUtc,
                item.Source,
                item.Notes,
                affiliationContexts.GetValueOrDefault(item.Id),
                item.PartyOrganizationAffiliationId,
                item.ProjectLifetimeId) { Original = new(item.Id, item.ProjectId, item.ProjectLifetimeId, item.PartyId,
                    item.PartyOrganizationAffiliationId, MapRole(item.AssignmentKind), item.NodeKey, item.PhaseName, item.OpportunityId,
                    item.IsPrimary, item.AllocationPercent, item.StartsAtUtc, item.EndsAtUtc, item.Source, item.Notes) })
            .ToList();
        return new(
            items,
            query.PageIndex,
            query.PageSize,
            totalCount);
    }

    private static ProjectPartyAssignmentQuery Normalize(ProjectPartyAssignmentQuery request)
    {
        ArgumentNullException.ThrowIfNull(request.Roles);
        if (request.PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.PageIndex,
                "The assignment page index cannot be negative.");
        }

        if (request.PageSize is < 1 or > ProjectPartyAssignmentQueryLimits.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.PageSize,
                $"The assignment page size must be between 1 and {ProjectPartyAssignmentQueryLimits.MaximumPageSize}.");
        }

        if (request.PageIndex > int.MaxValue / request.PageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.PageIndex,
                "The assignment page offset is too large.");
        }

        if (request.WindowStartUtc > request.WindowEndUtc)
        {
            throw new ArgumentException(
                "The assignment schedule window start cannot be after its end.",
                nameof(request));
        }

        var searchText = request.SearchText?.Trim() ?? string.Empty;
        if (searchText.Length > ProjectPartyAssignmentQueryLimits.MaximumSearchLength)
        {
            throw new ArgumentException(
                $"Assignment search cannot exceed {ProjectPartyAssignmentQueryLimits.MaximumSearchLength} characters.",
                nameof(request));
        }

        var roles = request.Roles
            .Distinct()
            .ToArray();
        if (roles.Any(role => !Enum.IsDefined(role)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.Roles,
                "The assignment query contains an unsupported role.");
        }

        return request with
        {
            Roles = roles,
            SearchText = searchText
        };
    }

    public Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(
        Guid projectId,
        IReadOnlyCollection<ProjectPartyAssignmentRole> roles,
        CancellationToken cancellationToken = default)
    {
        return ListAssignmentsDetailedCoreAsync(
            projectId,
            roles,
            orderResults: false,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectWorkItemAssigneeBinding>> ListWorkItemAssigneeBindingsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var assignmentRows = await ProjectAssignmentReporting.ForProjectsAsync(dbContext, workAssignments, projectRecordQueryService, [projectId], cancellationToken);
        return await assignmentRows
            .Where(item => item.ProjectLifetimeId != null && item.ProjectLifetimeId == item.CurrentProjectLifetimeId &&
                item.ProjectId == projectId &&
                item.AssignmentKind == ProjectPartyAssignmentKind.WorkItemAssignee)
            .Join(
                ProjectAssignmentReporting.ReadRoot(dbContext, dbContext.Set<Party>()),
                assignment => assignment.PartyId,
                party => party.Id,
                (assignment, party) => new ProjectWorkItemAssigneeBinding(
                    assignment.ProjectId,
                    assignment.NodeKey,
                    assignment.PartyId,
                    MapProjectPartyType(party.PartyType)))
            .ToAssignmentReportListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectWorkItemAssigneeBinding>> ListWorkItemAssigneeBindingsAsync(
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        var distinctProjectIds = projectIds.Distinct().ToArray();
        if (distinctProjectIds.Length == 0)
        {
            return [];
        }

        if (distinctProjectIds.Any(static projectId => projectId == Guid.Empty))
        {
            throw new ArgumentException(
                "Project identifiers cannot contain an empty value.",
                nameof(projectIds));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var assignmentRows = await ProjectAssignmentReporting.ForProjectsAsync(dbContext, workAssignments, projectRecordQueryService, distinctProjectIds, cancellationToken);
        return await assignmentRows
            .Where(item => item.ProjectLifetimeId != null && item.ProjectLifetimeId == item.CurrentProjectLifetimeId &&
                distinctProjectIds.Contains(item.ProjectId) &&
                item.AssignmentKind == ProjectPartyAssignmentKind.WorkItemAssignee)
            .Join(
                ProjectAssignmentReporting.ReadRoot(dbContext, dbContext.Set<Party>()),
                assignment => assignment.PartyId,
                party => party.Id,
                (assignment, party) => new ProjectWorkItemAssigneeBinding(
                    assignment.ProjectId,
                    assignment.NodeKey,
                    assignment.PartyId,
                    MapProjectPartyType(party.PartyType)))
            .ToAssignmentReportListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedCoreAsync(
        Guid projectId,
        IReadOnlyCollection<ProjectPartyAssignmentRole> roles,
        bool orderResults,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roles);
        if (roles.Count == 0)
        {
            return [];
        }

        var assignmentKinds = roles.Select(MapRole).Distinct().ToArray();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var assignmentRows = await ProjectAssignmentReporting.ForProjectsAsync(dbContext, workAssignments, projectRecordQueryService, [projectId], cancellationToken);
        var query = assignmentRows
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId && assignmentKinds.Contains(item.AssignmentKind))
            .Join(
                ProjectAssignmentReporting.ReadRoot(dbContext, dbContext.Set<Party>().AsNoTracking()),
                assignment => assignment.PartyId,
                party => party.Id,
                (assignment, party) => new
                {
                    assignment.Id,
                    assignment.ProjectId,
                    assignment.ProjectLifetimeId,
                    assignment.PartyId,
                    assignment.PartyOrganizationAffiliationId,
                    assignment.AssignmentKind,
                    assignment.NodeKey,
                    assignment.IsPrimary,
                    assignment.AllocationPercent,
                    assignment.StartsAtUtc,
                    assignment.EndsAtUtc,
                    assignment.Source,
                    assignment.Notes,
                assignment.PhaseName,
                assignment.OpportunityId,
                    party.DisplayName,
                    party.PartyType,
                    party.IsSensitive
                });
        if (orderResults)
        {
            query = query
                .OrderBy(item => item.NodeKey)
                .ThenBy(item => item.AssignmentKind)
                .ThenByDescending(item => item.IsPrimary)
                .ThenBy(item => item.DisplayName);
        }

        var rows = await query.ToAssignmentReportListAsync(cancellationToken);
        var affiliationContexts = await projectPartyAffiliationContextService
            .LoadAssignmentContextsAsync(
                dbContext,
                rows
                    .Select(item => new ProjectPartyAffiliationReference(
                        item.Id,
                        item.PartyId,
                        item.PartyType,
                        item.PartyOrganizationAffiliationId,
                        item.IsSensitive))
                    .ToArray(),
                cancellationToken);
        return rows
            .Select(item => new ProjectPartyAssignmentDetail(
                item.Id,
                item.ProjectId,
                item.PartyId,
                MapRole(item.AssignmentKind),
                item.DisplayName,
                ResolvePartyTypeLabel(item.PartyType),
                MapProjectPartyType(item.PartyType),
                item.NodeKey,
                item.IsPrimary,
                item.AllocationPercent,
                item.StartsAtUtc,
                item.EndsAtUtc,
                item.Source,
                item.Notes,
                affiliationContexts.GetValueOrDefault(item.Id),
                item.PartyOrganizationAffiliationId,
                item.ProjectLifetimeId) { Original = new(item.Id, item.ProjectId, item.ProjectLifetimeId, item.PartyId,
                    item.PartyOrganizationAffiliationId, MapRole(item.AssignmentKind), item.NodeKey, item.PhaseName, item.OpportunityId,
                    item.IsPrimary, item.AllocationPercent, item.StartsAtUtc, item.EndsAtUtc, item.Source, item.Notes) })
            .ToList();
    }

    public async Task<ProjectAssignmentObservation?> ReadAssignmentForEditingAsync(ProjectWriteAdmission project, Guid id,
        CancellationToken cancellationToken = default) {
        if (project.DatabaseProfileId != admissions.DatabaseProfileId) {
            throw new InvalidOperationException("The assignment observation belongs to another database profile.");
        }
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await ProjectAssignmentReporting.ForProjectsAsync(context, workAssignments, projectRecordQueryService,
            [project.ProjectId], cancellationToken);
        var row = await rows.Where(item => item.Id == id && item.ProjectLifetimeId == project.LifetimeId &&
            item.CurrentProjectLifetimeId == project.LifetimeId).SingleAssignmentReportOrDefaultAsync(cancellationToken);
        return row is null ? null : new(row.Id, row.ProjectId, row.ProjectLifetimeId, row.PartyId,
            row.PartyOrganizationAffiliationId, MapRole(row.AssignmentKind), row.NodeKey, row.PhaseName, row.OpportunityId,
            row.IsPrimary, row.AllocationPercent, row.StartsAtUtc, row.EndsAtUtc, row.Source, row.Notes);
    }

    public async Task<ProjectAssignmentPrimaryObservation> ReadPrimaryObservationAsync(ProjectWriteAdmission project,
        ProjectPartyAssignmentRole role, string nodeKey, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(project);
        if (project.DatabaseProfileId != admissions.DatabaseProfileId) {
            throw new InvalidOperationException("The assignment observation belongs to another database profile.");
        }
        nodeKey = nodeKey.Trim();
        if (role == ProjectPartyAssignmentRole.WorkItemAssignee) {
            return await workAssignments.ReadPrimaryObservationAsync(project, nodeKey, cancellationToken);
        }
        var kind = MapRole(role);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var ids = await context.Set<ProjectPartyAssignment>().AsNoTracking().Where(item =>
            item.ProjectId == project.ProjectId && item.ProjectLifetimeId == project.LifetimeId &&
            item.AssignmentKind == kind && item.NodeKey == nodeKey && item.IsPrimary).Select(item => item.Id).ToArrayAsync(cancellationToken);
        return new(project.ProjectId, project.LifetimeId, role, nodeKey, System.Collections.Immutable.ImmutableArray.CreateRange(ids.Order()));
    }

    public async Task<Result<Guid>> SaveAssignmentAsync(
        ProjectPartyAssignmentUpsertRequest request,
        CancellationToken cancellationToken = default, Action<ProjectAssignmentSaveReceipt>? onCommitted = null) {
        ArgumentNullException.ThrowIfNull(request);
        request = request.Snapshot();
        var expected = ProjectAssignmentAdmission.Require(request.ProjectId, request.ExpectedProjectAdmission);
        if (ProjectAssignmentEditingErrors.ValidateOriginal(request) is { } observationError) {
            return Result<Guid>.Failure(observationError);
        }
        if (request.ProjectId == Guid.Empty) {
            return Result<Guid>.Failure(Error.Validation("Project is required.", "crmhr.project-assignment.project-required"));
        }

        if (request.PartyId == Guid.Empty) {
            return Result<Guid>.Failure(Error.Validation("Party is required.", "crmhr.project-assignment.party-required"));
        }

        var valueError = ProjectPartyAssignmentInvariantPolicy.ValidateValues(request);
        if (valueError is not null) {
            return Result<Guid>.Failure(valueError);
        }

        var committed = false;
        try {
            var newAssignmentId = Guid.NewGuid();
            await assignmentChanges.DemandCommitAsync(cancellationToken);
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var mutationScope = await SerializableMutationScope.BeginAsync(
                dbContext,
                new[] { ProjectMutationScopeKeys.ForProject(request.ProjectId),
                    ProjectAssignmentMutationKeys.ForAssignment(newAssignmentId),
                    ProjectAssignmentMutationKeys.ForAssignment(request.AssignmentId ?? newAssignmentId) },
                cancellationToken);
            using var ownerEntry = coordinatedTransaction.Enter(dbContext);
            await admissions.RequireForMutationAsync(expected, cancellationToken);

            var partyType = await dbContext.Set<Party>()
                .Where(item => item.Id == request.PartyId)
                .Select(item => (PartyType?)item.PartyType)
                .SingleOrDefaultAsync(cancellationToken);
            if (!partyType.HasValue) {
                return Result<Guid>.Failure(Error.Validation("Party was not found.", "crmhr.project-assignment.party-not-found"));
            }

            var partyTypeError = ProjectPartyAssignmentInvariantPolicy.ValidatePartyType(request.Role, partyType.Value);
            if (partyTypeError is not null) {
                return Result<Guid>.Failure(partyTypeError);
            }

            var affiliationError =
                await projectPartyAffiliationContextService.ValidateAsync(
                    dbContext,
                    request.PartyId,
                    request.PartyAffiliationId,
                    ToUtcDate(request.StartsOn),
                    ToUtcDate(request.EndsOn),
                    cancellationToken);
            if (affiliationError is not null) {
                return Result<Guid>.Failure(affiliationError);
            }

            var normalizedNodeKey = request.NodeKey?.Trim() ?? string.Empty;
            var (nodeScope, nodeScopeError) = await projectPartyAssignmentNodePolicy.ResolveScopeAsync(
                request.ProjectId,
                normalizedNodeKey,
                [request.Role],
                allowUnresolvedNamedScope: true,
                cancellationToken);
            if (nodeScopeError is not null) {
                return Result<Guid>.Failure(nodeScopeError);
            }
            var roleError = projectPartyAssignmentNodePolicy.ValidateRole(request.Role, nodeScope);
            if (roleError is not null) {
                return Result<Guid>.Failure(roleError);
            }

            var ownedWorkAssignment = request.AssignmentId.HasValue
                ? await workAssignments.GetForMutationAsync(request.AssignmentId.Value, cancellationToken)
                : null;
            var workNodeTargets = new List<(Guid ProjectId, string NodeKey)>();
            if (ownedWorkAssignment is not null && !string.IsNullOrWhiteSpace(ownedWorkAssignment.NodeKey)) {
                workNodeTargets.Add((ownedWorkAssignment.ProjectId, ownedWorkAssignment.NodeKey));
            }
            if (request.Role == ProjectPartyAssignmentRole.WorkItemAssignee) {
                workNodeTargets.Add((request.ProjectId, normalizedNodeKey));
            }
            var assignmentKind = MapRole(request.Role);
            var entity = request.AssignmentId.HasValue
                ? await dbContext.Set<ProjectPartyAssignment>()
                    .SingleOrDefaultAsync(item => item.Id == request.AssignmentId.Value, cancellationToken)
                : null;
            if (entity is not null && ownedWorkAssignment is not null) {
                throw new InvalidOperationException("An assignment identity is occupied by both owners.");
            }
            var currentOriginal = entity is not null ? ProjectAssignmentEditValues.Observe(entity) : ownedWorkAssignment?.Original;
            if (request.Original is not null && currentOriginal != request.Original) {
                return Result<Guid>.Failure(ProjectAssignmentEditingErrors.Stale());
            }
            if (await dbContext.Set<ProjectPartyAssignment>().AnyAsync(item =>
                item.ProjectId == request.ProjectId && item.ProjectLifetimeId == expected.LifetimeId &&
                item.PartyId == request.PartyId && item.AssignmentKind == assignmentKind &&
                item.NodeKey == normalizedNodeKey && item.Id != (request.AssignmentId ?? Guid.Empty), cancellationToken)) {
                return Result<Guid>.Failure(ProjectAssignmentEditingErrors.DuplicateAssignment());
            }
            if (request.Role != ProjectPartyAssignmentRole.WorkItemAssignee &&
                ProjectAssignmentEditingErrors.RequiresPrimaryObservation(request, normalizedNodeKey)) {
                var primaryIds = await dbContext.Set<ProjectPartyAssignment>().Where(item =>
                    item.ProjectId == request.ProjectId && item.ProjectLifetimeId == expected.LifetimeId &&
                    item.AssignmentKind == assignmentKind && item.NodeKey == normalizedNodeKey && item.IsPrimary)
                    .Select(item => item.Id).ToArrayAsync(cancellationToken);
                if (request.PrimaryOriginal?.Matches(expected, request.Role, normalizedNodeKey, primaryIds) != true) {
                    return Result<Guid>.Failure(ProjectAssignmentEditingErrors.StalePrimary());
                }
            }

            if (entity is not null && (entity.ProjectId != request.ProjectId || entity.ProjectLifetimeId != expected.LifetimeId)) {
                return Result<Guid>.Failure(Error.Validation(
                    "The assignment does not belong to the requested project.",
                    "crmhr.project-assignment.project-mismatch"));
            }

            if (ownedWorkAssignment is not null && (ownedWorkAssignment.ProjectId != request.ProjectId || ownedWorkAssignment.ProjectLifetimeId != expected.LifetimeId)) {
                return Result<Guid>.Failure(Error.Validation(
                    "The assignment does not belong to the requested project.", "crmhr.project-assignment.project-mismatch"));
            }
            if (request.Role == ProjectPartyAssignmentRole.WorkItemAssignee) {
                ProjectWorkAssignmentCarryOver? carryOver = null;
                if (entity is not null) {
                    carryOver = new(entity.Id, entity.PhaseName, entity.OpportunityId, entity.ProjectLifetimeId);

                }
                var saved = await workAssignments.StageSaveAsync(request, newAssignmentId, carryOver, cancellationToken);
                if (saved.IsSuccess) {
                    if (entity is not null) {
                        dbContext.Remove(entity);
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                    var accepted = (await workAssignments.GetForMutationAsync(saved.Value, cancellationToken))!.Original;
                    var nodeChanges = await workAssignments.ReadExistingNodeChangesForMutationAsync(workNodeTargets, cancellationToken);
                    await assignmentChanges.DemandCommitAsync(cancellationToken);
                    await mutationScope.CommitAsync(cancellationToken);
                    committed = true;
                    try {
                        workAssignments.PublishCommittedNodeChanges(nodeChanges);
                        assignmentChanges.Publish(saved.Value, request.ProjectId);
                    } finally {
                        onCommitted?.Invoke(new(saved.Value, accepted));
                    }
                }
                return saved;
            }
            if (ownedWorkAssignment is not null) {
                if (entity is not null) {
                    throw new InvalidOperationException("An assignment identity is occupied by both owners.");
                }
                entity = new ProjectPartyAssignment {
                    Id = ownedWorkAssignment.Id,
                    PhaseName = ownedWorkAssignment.PhaseName,
                    OpportunityId = ownedWorkAssignment.OpportunityId
                };
                dbContext.Add(entity);
                await workAssignments.StageDeleteAsync(ownedWorkAssignment.Id, cancellationToken, ProjectAssignmentReference.From(expected), request.Original);
            }

            if (entity is null) {
                if (await workAssignments.GetForMutationAsync(newAssignmentId, cancellationToken) is not null) {
                    throw new InvalidOperationException("An assignment identity is already occupied by the Work owner.");
                }
                entity = new ProjectPartyAssignment { Id = newAssignmentId };
                dbContext.Set<ProjectPartyAssignment>().Add(entity);
            }

            entity.ProjectId = request.ProjectId;
            entity.ProjectLifetimeId = expected.LifetimeId;
            entity.PartyId = request.PartyId;
            entity.PartyOrganizationAffiliationId =
                request.PartyAffiliationId;
            entity.AssignmentKind = assignmentKind;
            entity.NodeKey = normalizedNodeKey;
            entity.IsPrimary = request.IsPrimary;
            entity.AllocationPercent = request.AllocationPercent;
            entity.StartsAtUtc = ToUtcDate(request.StartsOn);
            entity.EndsAtUtc = ToUtcDate(request.EndsOn);
            entity.Source = string.IsNullOrWhiteSpace(request.Source) ? "crm-hr-ui" : request.Source.Trim();
            entity.Notes = request.Notes?.Trim() ?? string.Empty;

            if (entity.IsPrimary) {
                var primaryAssignments = await dbContext.Set<ProjectPartyAssignment>()
                    .Where(item =>
                        item.ProjectId == request.ProjectId && item.ProjectLifetimeId == expected.LifetimeId &&
                        item.AssignmentKind == assignmentKind &&
                        item.NodeKey == normalizedNodeKey &&
                        item.Id != entity.Id)
                    .ToListAsync(cancellationToken);
                foreach (var primaryAssignment in primaryAssignments) {
                    primaryAssignment.IsPrimary = false;
                }
            }

            await assignmentChanges.DemandCommitAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await assignmentChanges.DemandCommitAsync(cancellationToken);
            var acceptedParticipation = ProjectAssignmentEditValues.Observe(entity);
            var participationNodeChanges = await workAssignments.ReadExistingNodeChangesForMutationAsync(workNodeTargets, cancellationToken);
            await assignmentChanges.DemandCommitAsync(cancellationToken);
            await mutationScope.CommitAsync(cancellationToken);
            committed = true;
            try {
                workAssignments.PublishCommittedNodeChanges(participationNodeChanges);
                assignmentChanges.Publish(entity.Id, request.ProjectId);
            } finally {
                onCommitted?.Invoke(new(entity.Id, acceptedParticipation));
            }
            return Result<Guid>.Success(entity.Id);
        } catch (Exception exception) when (!committed && SerializableMutationScope.IsConflict(exception)) {
            logger?.LogInformation("Assignment save conflicted for {AssignmentId} in project {ProjectId}.", request.AssignmentId, request.ProjectId);
            return Result<Guid>.Failure(ProjectAssignmentEditingErrors.Stale());
        }
    }

    public Task<Result> ReplaceProjectAssignmentsAsync(
        Guid projectId,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
        CancellationToken cancellationToken = default,
        ProjectWriteAdmission? expectedProjectAdmission = null,
        IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot>? expectedAssignments = null)
        => ReplaceAssignmentsCoreAsync(
            projectId,
            string.Empty,
            desiredAssignments,
            targetRoles,
            expectedAssignments: expectedAssignments ?? [],
            expectedDirectAssignmentRevision: null,
            cancellationToken,
            expectedProjectAdmission);

    public Task<Result> ReplaceNodeAssignmentsAsync(
        Guid projectId,
        ProjectNodeReference nodeReference,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
        CancellationToken cancellationToken = default,
        ProjectWriteAdmission? expectedProjectAdmission = null,
        IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot>? expectedAssignments = null)
        => ReplaceAssignmentsCoreAsync(
            projectId,
            nodeReference.NodeKey,
            desiredAssignments,
            targetRoles,
            expectedAssignments: expectedAssignments ?? [],
            expectedDirectAssignmentRevision: null,
            cancellationToken,
            expectedProjectAdmission);

    public Task<Result> ReplaceNodeAssignmentsIfCurrentAsync(
        Guid projectId,
        ProjectNodeReference nodeReference,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
        IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot>
            expectedAssignments,
        ProjectWorkItemDirectAssignmentRevision?
            expectedDirectAssignmentRevision,
        CancellationToken cancellationToken = default,
        ProjectWriteAdmission? expectedProjectAdmission = null)
    {
        ArgumentNullException.ThrowIfNull(expectedAssignments);
        return ReplaceAssignmentsCoreAsync(
            projectId,
            nodeReference.NodeKey,
            desiredAssignments,
            targetRoles,
            expectedAssignments,
            expectedDirectAssignmentRevision,
            cancellationToken,
            expectedProjectAdmission);
    }

    public async Task<Result<ProjectNodeAssignmentCommit>> ReplaceNodeAssignmentsIfCurrentAsync(
        Guid projectId, ProjectNodeReference nodeReference,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
        IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot> expectedAssignments,
        ProjectPartyNodeOccurrence expectedNode, CancellationToken cancellationToken = default,
        ProjectWriteAdmission? expectedProjectAdmission = null,
        Func<CancellationToken, ValueTask>? authorizeCommit = null) {
        ArgumentNullException.ThrowIfNull(expectedNode);
        ArgumentNullException.ThrowIfNull(expectedAssignments);
        ArgumentNullException.ThrowIfNull(targetRoles);
        if (expectedNode.ObjectType is not (ProjectObjectType.Participant or ProjectObjectType.Meeting) ||
            targetRoles.Count == 0 || targetRoles.Contains(ProjectPartyAssignmentRole.WorkItemAssignee)) {
            return Result<ProjectNodeAssignmentCommit>.Failure(Error.Validation(
                "This replacement requires an original participant or meeting.", "crmhr.project-assignment.participation-required"));
        }
        ProjectNodeAssignmentCommit? committed = null;
        try {
            var result = await ReplaceAssignmentsCoreAsync(projectId, nodeReference.NodeKey, desiredAssignments, targetRoles,
                expectedAssignments, null, cancellationToken, expectedProjectAdmission,
                expectedNode: expectedNode, captureAssignments: ids => committed = new(ids), demandCommit: authorizeCommit);
            return result.IsSuccess
                ? Result<ProjectNodeAssignmentCommit>.Success(committed
                    ?? throw new InvalidOperationException("The replacement has no native commit receipt."))
                : Result<ProjectNodeAssignmentCommit>.Failure(result.Errors.ToArray());
        } catch (Exception exception) when (committed is not null) {
            logger?.LogWarning("Participant/meeting assignments for {ProjectId}, {NodeId} committed; cleanup ended with {FailureType}.",
                projectId, nodeReference.NodeKey, exception.GetType().Name);
            return Result<ProjectNodeAssignmentCommit>.Success(committed with { ObservationWarning = "Assignments committed; observation requires a reload." });
        }
    }

    private async Task<Result> ReplaceAssignmentsCoreAsync(
        Guid projectId,
        string normalizedNodeKey,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
        IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot>?
            expectedAssignments,
        ProjectWorkItemDirectAssignmentRevision?
            expectedDirectAssignmentRevision,
        CancellationToken cancellationToken,
        ProjectWriteAdmission? expectedProjectAdmission,
        Action<CrmOpportunityAssignmentReceipt>? captureReceipt = null,
        ProjectPartyNodeOccurrence? expectedNode = null,
        Action<IReadOnlyList<Guid>>? captureAssignments = null,
        Func<CancellationToken, ValueTask>? demandCommit = null,
        IReadOnlyCollection<CrmOpportunityAssignmentOriginal>? expectedOpportunityAssignments = null)
    {
        var expected = ProjectAssignmentAdmission.Require(projectId, expectedProjectAdmission);
        desiredAssignments = ProjectAssignmentAdmission.Snapshot(projectId, desiredAssignments, expected);
        expectedAssignments = expectedAssignments?.ToArray();
        ArgumentNullException.ThrowIfNull(targetRoles);
        targetRoles = targetRoles.ToArray();

        if (projectId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("Project is required.", "crmhr.project-assignment.project-required"));
        }

        var targetRoleSet = targetRoles
            .Distinct()
            .ToHashSet();
        if (targetRoleSet.Count == 0)
        {
            return desiredAssignments.Count == 0
                ? Result.Success()
                : Result.Failure(Error.Validation(
                    "At least one assignment role must be supplied.",
                    "crmhr.project-assignment.target-roles-required"));
        }

        if (desiredAssignments.Any(item => !targetRoleSet.Contains(item.Role)))
        {
            return Result.Failure(Error.Validation(
                "Desired assignments included a role outside the replacement scope.",
                "crmhr.project-assignment.target-role-mismatch"));
        }

        if (expectedDirectAssignmentRevision.HasValue &&
            !targetRoleSet.Contains(
                ProjectPartyAssignmentRole.WorkItemAssignee))
        {
            return Result.Failure(Error.Validation(
                "A direct-assignment revision can only guard work-item assignee replacement.",
                "crmhr.project-assignment.revision-role-mismatch"));
        }

        foreach (var desiredAssignment in desiredAssignments)
        {
            var valueError = ProjectPartyAssignmentInvariantPolicy.ValidateValues(desiredAssignment);
            if (valueError is not null)
            {
                return Result.Failure(valueError);
            }
        }

        var duplicateAssignment = desiredAssignments
            .GroupBy(item => (item.PartyId, item.Role))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateAssignment is not null)
        {
            return Result.Failure(Error.Validation(
                "Desired assignments contain the same party and role more than once.",
                "crmhr.project-assignment.duplicate"));
        }

        var (nodeScope, nodeScopeError) = await projectPartyAssignmentNodePolicy.ResolveScopeAsync(
            projectId,
            normalizedNodeKey,
            targetRoleSet.ToList(),
            allowUnresolvedNamedScope: false,
            cancellationToken);
        if (nodeScopeError is not null)
        {
            return Result.Failure(nodeScopeError);
        }

        var replacementIds = desiredAssignments.Select(_ => Guid.NewGuid()).ToArray();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await SerializableMutationScope.BeginAsync(
                dbContext,
                replacementIds.Concat(desiredAssignments.Where(item => item.AssignmentId.HasValue)
                        .Select(item => item.AssignmentId!.Value))
                    .Select(ProjectAssignmentMutationKeys.ForAssignment)
                    .Append(ProjectMutationScopeKeys.ForProject(projectId)).ToArray(),
                cancellationToken);
        using var ownerEntry = coordinatedTransaction.Enter(dbContext);
        await admissions.RequireForMutationAsync(expected, cancellationToken);

        if (expectedNode is not null) {
            var (currentScope, currentError) = await projectPartyAssignmentNodePolicy.ResolveScopeAsync(
                projectId, normalizedNodeKey, targetRoleSet.ToArray(), false, cancellationToken);
            if (currentError is not null) {
                return Result.Failure(currentError);
            }
            if (currentScope?.Occurrence != expectedNode) {
                return Result.Failure(Error.Failure("The original participant or meeting changed. Reload before saving.",
                    "crmhr.project-assignment.node-occurrence-changed"));
            }
            nodeScope = currentScope;
        }

        var desiredPartyIds = desiredAssignments
            .Select(item => item.PartyId)
            .Distinct()
            .ToList();
        if (desiredPartyIds.Any(id => id == Guid.Empty))
        {
            return Result.Failure(Error.Validation("Party is required.", "crmhr.project-assignment.party-required"));
        }

        if (desiredPartyIds.Count > 0)
        {
            var existingParties = await dbContext.Set<Party>()
                .Where(item => desiredPartyIds.Contains(item.Id))
                .Select(item => new { item.Id, item.PartyType })
                .ToListAsync(cancellationToken);
            var existingPartyIdSet = existingParties.Select(item => item.Id).ToHashSet();
            if (desiredPartyIds.Any(id => !existingPartyIdSet.Contains(id)))
            {
                return Result.Failure(Error.Validation("Party was not found.", "crmhr.project-assignment.party-not-found"));
            }

            var partyTypesById = existingParties.ToDictionary(item => item.Id, item => item.PartyType);
            foreach (var desiredAssignment in desiredAssignments)
            {
                var partyTypeError = ProjectPartyAssignmentInvariantPolicy.ValidatePartyType(
                    desiredAssignment.Role,
                    partyTypesById[desiredAssignment.PartyId]);
                if (partyTypeError is not null)
                {
                    return Result.Failure(partyTypeError);
                }
            }
        }

        var affiliationError =
            await projectPartyAffiliationContextService.ValidateAsync(
                dbContext,
                desiredAssignments
                    .Select(item =>
                        new ProjectPartyAffiliationValidation(
                            item.PartyId,
                            item.PartyAffiliationId,
                            ToUtcDate(item.StartsOn),
                            ToUtcDate(item.EndsOn)))
                    .ToArray(),
                cancellationToken);
        if (affiliationError is not null)
        {
            return Result.Failure(affiliationError);
        }

        foreach (var desiredAssignment in desiredAssignments)
        {
            if (desiredAssignment.ProjectId != Guid.Empty && desiredAssignment.ProjectId != projectId)
            {
                return Result.Failure(Error.Validation(
                    "Desired assignments must target the same project.",
                    "crmhr.project-assignment.project-mismatch"));
            }

            var assignmentNodeKey = desiredAssignment.NodeKey?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(assignmentNodeKey) &&
                !string.Equals(assignmentNodeKey, normalizedNodeKey, StringComparison.Ordinal))
            {
                return Result.Failure(Error.Validation(
                    "Desired assignments must target the same node.",
                    "crmhr.project-assignment.node-mismatch"));
            }

            var roleError = projectPartyAssignmentNodePolicy.ValidateRole(desiredAssignment.Role, nodeScope);
            if (roleError is not null)
            {
                return Result.Failure(roleError);
            }
        }

        var targetAssignmentKinds = targetRoleSet
            .Select(MapRole)
            .Distinct()
            .ToList();
        var existingAssignments = await dbContext.Set<ProjectPartyAssignment>()
            .Where(item =>
                item.ProjectId == projectId && item.ProjectLifetimeId == expected.LifetimeId &&
                item.NodeKey == normalizedNodeKey &&
                targetAssignmentKinds.Contains(item.AssignmentKind))
            .ToListAsync(cancellationToken);
        var workRows = targetRoleSet.Contains(ProjectPartyAssignmentRole.WorkItemAssignee)
            ? (await workAssignments.ListForProjectsForMutationAsync([projectId], cancellationToken))
                .Where(item => item.ProjectLifetimeId == expected.LifetimeId && item.NodeKey == normalizedNodeKey).ToArray()
            : [];
        var existingIds = existingAssignments.Select(item => item.Id).Concat(workRows.Select(item => item.Id)).ToHashSet();
        var retained = expectedNode is null && expectedOpportunityAssignments is null ? [] : existingAssignments.Where(row => desiredAssignments.Any(request =>
            request.AssignmentId == row.Id && request.PartyId == row.PartyId && MapRole(request.Role) == row.AssignmentKind)).ToArray();
        var retainedIds = retained.Select(row => row.Id).ToHashSet();
        var resolvedReplacementIds = desiredAssignments.Select((item, index) =>
            item.AssignmentId.HasValue && (!existingIds.Contains(item.AssignmentId.Value) || retainedIds.Contains(item.AssignmentId.Value))
                ? item.AssignmentId.Value : replacementIds[index]).ToArray();
        if (resolvedReplacementIds.Distinct().Count() != resolvedReplacementIds.Length) {
            return Result.Failure(Error.Validation("An assignment identity is already in use.", "crmhr.project-assignment.identity-in-use"));
        }
        if (expectedAssignments is not null) {
            var currentPartyIds = existingAssignments.Select(item => item.PartyId).Concat(workRows.Select(item => item.PartyId)).Distinct().ToArray();
            var currentPartyTypes = await dbContext.Set<Party>().Where(item => currentPartyIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.PartyType, cancellationToken);
            var currentSnapshots = existingAssignments.Where(item => currentPartyTypes.ContainsKey(item.PartyId))
                .Select(item => new ProjectPartyAssignmentConcurrencySnapshot(item.Id, item.PartyId,
                    MapProjectPartyType(currentPartyTypes[item.PartyId]), item.IsPrimary, item.PartyOrganizationAffiliationId, ProjectAssignmentEditValues.Observe(item)))
                .Concat(workRows.Where(item => currentPartyTypes.ContainsKey(item.PartyId))
                    .Select(item => new ProjectPartyAssignmentConcurrencySnapshot(item.Id, item.PartyId,
                        MapProjectPartyType(currentPartyTypes[item.PartyId]), item.IsPrimary, item.PartyOrganizationAffiliationId, item.Original)))
                .ToHashSet();
            if (currentSnapshots.Count != existingAssignments.Count + workRows.Length || !currentSnapshots.SetEquals(expectedAssignments)) {
                return Result.Failure(Error.Failure(
                    "Project assignments changed before the requested replacement could be applied.", ProjectPartyIntegrationErrorCodes.StaleAssignmentSnapshot));
            }
        }

        if (expectedOpportunityAssignments is not null && !existingAssignments
                .Select(row => CrmOpportunityAssignmentSnapshot.From(row).Original()).ToHashSet().SetEquals(expectedOpportunityAssignments)) {
            return Result.Failure(Error.Failure("The selected project's handoff membership changed. Review its current state before converting.",
                ProjectPartyIntegrationErrorCodes.StaleAssignmentSnapshot));
        }
        var before = captureReceipt is null ? null : existingAssignments.Select(CrmOpportunityAssignmentSnapshot.From).ToArray();
        if (existingAssignments.Count > 0)
        {
            dbContext.RemoveRange(existingAssignments.Where(row => !retainedIds.Contains(row.Id)));
        }

        var desiredAssignmentItems = desiredAssignments
            .Select((request, index) => new
            {
                Request = request,
                Index = index,
                AssignmentKind = MapRole(request.Role)
            })
            .Where(item => item.AssignmentKind != ProjectPartyAssignmentKind.WorkItemAssignee)
            .ToList();
        var explicitPrimaryKinds = desiredAssignmentItems
            .Where(item => item.Request.IsPrimary)
            .Select(item => item.AssignmentKind)
            .ToHashSet();
        var emittedPrimaryKinds = new HashSet<ProjectPartyAssignmentKind>();

        foreach (var desiredAssignment in desiredAssignmentItems.OrderBy(item => item.Index))
        {
            if (retainedIds.Contains(resolvedReplacementIds[desiredAssignment.Index])) {
                var retainedRow = retained.Single(row => row.Id == resolvedReplacementIds[desiredAssignment.Index]);
                if (retainedRow.PartyOrganizationAffiliationId != desiredAssignment.Request.PartyAffiliationId ||
                    retainedRow.IsPrimary != desiredAssignment.Request.IsPrimary) {
                    return Result.Failure(Error.Validation("A retained assignment's affiliation or primary flag cannot be changed by party selection.",
                        "crmhr.project-assignment.retained-context-changed"));
                }
                if (retainedRow.IsPrimary) {
                    emittedPrimaryKinds.Add(retainedRow.AssignmentKind);
                }
                continue;
            }
            var isPrimary = desiredAssignment.Request.IsPrimary;
            if (emittedPrimaryKinds.Contains(desiredAssignment.AssignmentKind))
            {
                isPrimary = false;
            }
            else if (!explicitPrimaryKinds.Contains(desiredAssignment.AssignmentKind))
            {
                isPrimary = true;
            }

            if (isPrimary)
            {
                emittedPrimaryKinds.Add(desiredAssignment.AssignmentKind);
            }

            var assignmentId = resolvedReplacementIds[desiredAssignment.Index];
            if (await workAssignments.GetForMutationAsync(assignmentId, cancellationToken) is not null) {
                return Result.Failure(Error.Validation("An assignment identity is already in use.", "crmhr.project-assignment.identity-in-use"));
            }
            dbContext.Set<ProjectPartyAssignment>().Add(new ProjectPartyAssignment
            {
                Id = assignmentId,
                ProjectId = projectId,
                ProjectLifetimeId = expected.LifetimeId,
                PartyId = desiredAssignment.Request.PartyId,
                PartyOrganizationAffiliationId =
                    desiredAssignment.Request.PartyAffiliationId,
                AssignmentKind = desiredAssignment.AssignmentKind,
                NodeKey = normalizedNodeKey,
                IsPrimary = isPrimary,
                AllocationPercent = desiredAssignment.Request.AllocationPercent,
                StartsAtUtc = ToUtcDate(desiredAssignment.Request.StartsOn),
                EndsAtUtc = ToUtcDate(desiredAssignment.Request.EndsOn),
                Source = string.IsNullOrWhiteSpace(desiredAssignment.Request.Source) ? "crm-hr-ui" : desiredAssignment.Request.Source.Trim(),
                Notes = desiredAssignment.Request.Notes?.Trim() ?? string.Empty
            });
        }

        if (targetRoleSet.Contains(ProjectPartyAssignmentRole.WorkItemAssignee)) {
            var workRequests = desiredAssignments.Select((request, index) => (request, index))
                .Where(item => item.request.Role == ProjectPartyAssignmentRole.WorkItemAssignee).ToArray();
            var staged = await workAssignments.StageReplaceAsync(projectId, new ProjectNodeReference(normalizedNodeKey),
                workRequests.Select(item => WithAssignmentId(item.request, resolvedReplacementIds[item.index])).ToArray(),
                workRequests.Select(item => replacementIds[item.index]).ToArray(),
                expectedAssignments: expectedAssignments?.Where(item => item.Original?.Role == ProjectPartyAssignmentRole.WorkItemAssignee).ToArray() ?? [],
                expectedRevision: expectedDirectAssignmentRevision, cancellationToken: cancellationToken, expectedProjectAdmission: expected);
            if (staged.IsFailure) {
                return staged;
            }
        }

        if (demandCommit is not null) {
            await demandCommit(cancellationToken);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        CrmOpportunityAssignmentReceipt? receipt = null;
        if (captureReceipt is not null) {
            var after = await dbContext.Set<ProjectPartyAssignment>().AsNoTracking().Where(row =>
                row.ProjectId == projectId && row.ProjectLifetimeId == expected.LifetimeId && row.NodeKey == normalizedNodeKey &&
                targetAssignmentKinds.Contains(row.AssignmentKind)).ToArrayAsync(cancellationToken);
            receipt = new(expected, targetAssignmentKinds, before!, after.Select(CrmOpportunityAssignmentSnapshot.From));
        }
        if (demandCommit is not null) {
            await demandCommit(cancellationToken);
        }
        IReadOnlyList<ProjectWorkAssignmentNodeChange> nodeChanges = targetRoleSet.Contains(ProjectPartyAssignmentRole.WorkItemAssignee)
            ? await workAssignments.ReadExistingNodeChangesForMutationAsync([(projectId, normalizedNodeKey)], cancellationToken)
            : [];
        if (demandCommit is not null) {
            await demandCommit(cancellationToken);
        }
        await mutationScope.CommitAsync(cancellationToken);
        workAssignments.PublishCommittedNodeChanges(nodeChanges);
        captureAssignments?.Invoke(resolvedReplacementIds);
        if (receipt is not null) {
            captureReceipt!(receipt);
        }
        return Result.Success();
    }

    internal async Task<Result<CrmOpportunityAssignmentReceipt>> ReplaceOpportunityAssignmentsAsync(Guid projectId,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desired, IReadOnlyList<ProjectPartyAssignmentRole> roles,
        ProjectWriteAdmission expected, CancellationToken cancellationToken,
        Func<CancellationToken, ValueTask> demandCommit,
        IReadOnlyCollection<CrmOpportunityAssignmentOriginal> expectedAssignments) {
        if (roles.Count == 0 || roles.Contains(ProjectPartyAssignmentRole.WorkItemAssignee)) {
            throw new InvalidOperationException("Opportunity conversion requires explicit project participation roles.");
        }
        CrmOpportunityAssignmentReceipt? receipt = null;
        var result = await ReplaceAssignmentsCoreAsync(projectId, string.Empty, desired, roles, null, null,
            cancellationToken, expected, captured => receipt = captured, demandCommit: demandCommit,
            expectedOpportunityAssignments: expectedAssignments);
        return result.IsSuccess
            ? Result<CrmOpportunityAssignmentReceipt>.Success(receipt
                ?? throw new InvalidOperationException("The committed opportunity assignments have no original owner receipt."))
            : Result<CrmOpportunityAssignmentReceipt>.Failure(result.Errors.ToArray());
    }

    internal async Task<bool> TryRestoreOpportunityAssignmentsAsync(CrmOpportunityAssignmentReceipt receipt,
        CancellationToken cancellationToken) {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var originalIds = receipt.Before.Select(row => row.Id).ToArray();
        var writtenIds = receipt.After.Select(row => row.Id).ToArray();
        await using var scope = await SerializableMutationScope.BeginAsync(context,
            originalIds.Concat(writtenIds).Distinct().Select(ProjectAssignmentMutationKeys.ForAssignment)
                .Append(ProjectMutationScopeKeys.ForProject(receipt.Project.ProjectId)).ToArray(), cancellationToken);
        using var entry = coordinatedTransaction.Enter(context);
        await admissions.RequireForMutationAsync(receipt.Project, cancellationToken);
        var kinds = receipt.Kinds.ToArray();
        var current = await context.Set<ProjectPartyAssignment>().Where(row => row.ProjectId == receipt.Project.ProjectId &&
            row.ProjectLifetimeId == receipt.Project.LifetimeId && row.NodeKey == string.Empty && kinds.Contains(row.AssignmentKind))
            .ToArrayAsync(cancellationToken);
        if (!current.Select(CrmOpportunityAssignmentSnapshot.From).ToHashSet().SetEquals(receipt.After)) {
            return false;
        }
        if (await context.Set<ProjectPartyAssignment>().AnyAsync(row => originalIds.Contains(row.Id) && !writtenIds.Contains(row.Id), cancellationToken)) {
            return false;
        }
        foreach (var id in originalIds) {
            if (await workAssignments.GetForMutationAsync(id, cancellationToken) is not null) {
                return false;
            }
        }
        context.RemoveRange(current);
        await context.SaveChangesAsync(cancellationToken);
        context.AddRange(receipt.Before.Select(row => row.ToRecord()));
        await context.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync(cancellationToken);
        return true;
    }

    public async Task DeleteAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default,
        ProjectAssignmentReference? expectedReference = null, ProjectAssignmentObservation? original = null,
        Action<ProjectAssignmentDeleteReceipt>? onCommitted = null) {
        if (original is null || original.Id != assignmentId) {
            throw new InvalidOperationException(ProjectAssignmentEditingErrors.OriginalRequired);
        }
        ArgumentNullException.ThrowIfNull(expectedReference);
        expectedReference.RequireProfile(admissions.DatabaseProfileId, original.ProjectId);
        RequireAssignmentReference(original.ProjectId, original.ProjectLifetimeId, expectedReference);
        var committed = false;
        try {
            await assignmentChanges.DemandCommitAsync(cancellationToken);
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var mutationScope = await SerializableMutationScope.BeginAsync(dbContext,
                [ProjectMutationScopeKeys.ForProject(original.ProjectId), ProjectAssignmentMutationKeys.ForAssignment(assignmentId)], cancellationToken);
            using var ownerEntry = coordinatedTransaction.Enter(dbContext);
            var work = await workAssignments.GetForMutationAsync(assignmentId, cancellationToken);
            var entity = await dbContext.Set<ProjectPartyAssignment>().SingleOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);
            if (work is not null && entity is not null) {
                throw new InvalidOperationException("An assignment identity is occupied by both owners.");
            }
            var current = entity is not null ? ProjectAssignmentEditValues.Observe(entity) : work?.Original;
            if (current != original) {
                throw new ProjectAssignmentConflictException();
            }
            if (work is not null) {
                await workAssignments.StageDeleteAsync(assignmentId, cancellationToken, expectedReference, original);
            } else {
                dbContext.Remove(entity!);
                await assignmentChanges.DemandCommitAsync(cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await assignmentChanges.DemandCommitAsync(cancellationToken);
            }
            await assignmentChanges.DemandCommitAsync(cancellationToken);
            IReadOnlyList<ProjectWorkAssignmentNodeChange> nodeChanges = work is not null && !string.IsNullOrWhiteSpace(work.NodeKey)
                ? await workAssignments.ReadExistingNodeChangesForMutationAsync([(work.ProjectId, work.NodeKey)], cancellationToken)
                : [];
            await assignmentChanges.DemandCommitAsync(cancellationToken);
            await mutationScope.CommitAsync(cancellationToken);
            committed = true;
            try {
                workAssignments.PublishCommittedNodeChanges(nodeChanges);
                assignmentChanges.Publish(assignmentId, original.ProjectId, EditingChangeKind.Deleted);
            } finally {
                onCommitted?.Invoke(new(assignmentId, original));
            }
        } catch (Exception exception) when (!committed && SerializableMutationScope.IsConflict(exception)) {
            logger?.LogInformation("Assignment deletion conflicted for {AssignmentId}.", assignmentId);
            throw new ProjectAssignmentConflictException(exception);
        }
    }

    public async Task DeleteAssignmentsForNodesAsync(
        Guid projectId,
        IReadOnlyCollection<ProjectNodeReference> nodeReferences,
        CancellationToken cancellationToken = default,
        ProjectAssignmentReference? expectedReference = null)
    {
        var reference = RequireBoundAssignmentReference(projectId, expectedReference);
        var normalizedNodeKeys = NormalizeNodeKeys(nodeReferences);
        if (projectId == Guid.Empty || normalizedNodeKeys.Count == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await SerializableMutationScope.BeginAsync(
            dbContext,
            $"project:{projectId:D}",
            cancellationToken);
        using var ownerEntry = coordinatedTransaction.Enter(dbContext);
        await workAssignments.StageDeleteForNodesAsync(projectId, nodeReferences, cancellationToken, expectedReference);
        var assignments = await dbContext.Set<ProjectPartyAssignment>()
            .Where(item => item.ProjectId == projectId && item.ProjectLifetimeId == reference.LifetimeId && normalizedNodeKeys.Contains(item.NodeKey))
            .ToListAsync(cancellationToken);
        dbContext.RemoveRange(assignments);

        await dbContext.SaveChangesAsync(cancellationToken);
        await mutationScope.CommitAsync(cancellationToken);
    }

    public async Task DeleteAssignmentsForProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default,
        ProjectAssignmentReference? expectedReference = null)
    {
        var reference = RequireBoundAssignmentReference(projectId, expectedReference);
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project identifier is required.", nameof(projectId));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await SerializableMutationScope.BeginAsync(
            dbContext,
            ProjectMutationScopeKeys.ForProject(projectId),
            cancellationToken);
        using var ownerEntry = coordinatedTransaction.Enter(dbContext);
        await workAssignments.StageDeleteForProjectAsync(projectId, cancellationToken, expectedReference);
        var assignments = await dbContext.Set<ProjectPartyAssignment>()
            .Where(item => item.ProjectId == projectId && item.ProjectLifetimeId == reference.LifetimeId)
            .ToListAsync(cancellationToken);
        if (assignments.Count > 0)
        {
            dbContext.RemoveRange(assignments);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await mutationScope.CommitAsync(cancellationToken);
    }

    public async Task MoveAssignmentsToProjectAsync(
        ProjectPartyAssignmentMoveOperationId operationId,
        Guid sourceProjectId,
        IReadOnlyCollection<ProjectNodeReference> nodeReferences,
        Guid targetProjectId,
        CancellationToken cancellationToken = default,
        ProjectAssignmentReference? sourceReference = null,
        ProjectWriteAdmission? expectedTargetAdmission = null)
    {
        var source = RequireBoundAssignmentReference(sourceProjectId, sourceReference);
        var target = ProjectAssignmentAdmission.Require(targetProjectId, expectedTargetAdmission);
        if (target.DatabaseProfileId != admissions.DatabaseProfileId) {
            throw new ProjectWriteAdmissionRejectedException(target);
        }
        if (operationId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "A project-party assignment move operation identifier is required.",
                nameof(operationId));
        }

        var normalizedNodeKeys = NormalizeNodeKeys(nodeReferences);
        var nodeSetFingerprint = BuildNodeSetFingerprint(normalizedNodeKeys);
        if (sourceProjectId == Guid.Empty ||
            targetProjectId == Guid.Empty ||
            sourceProjectId == targetProjectId ||
            normalizedNodeKeys.Count == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await SerializableMutationScope.BeginAsync(
            dbContext,
            new[]
            {
                $"crmhr:project-assignment-move:{operationId.Value:D}",
                $"project:{sourceProjectId:D}",
                $"project:{targetProjectId:D}"
            },
            cancellationToken);
        using var ownerEntry = coordinatedTransaction.Enter(dbContext);
        var existingReceipt = await dbContext
            .Set<ProjectPartyAssignmentMoveReceipt>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.OperationId == operationId.Value,
                cancellationToken);
        if (existingReceipt is not null)
        {
            if (existingReceipt.DatabaseProfileId != source.DatabaseProfileId ||
                existingReceipt.SourceProjectLifetimeId != source.LifetimeId ||
                existingReceipt.TargetProjectLifetimeId != target.LifetimeId ||
                existingReceipt.SourceProjectId != sourceProjectId ||
                existingReceipt.TargetProjectId != targetProjectId ||
                !string.Equals(
                    existingReceipt.NodeSetFingerprint,
                    nodeSetFingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The project-party assignment move operation identifier was already used for a different request.");
            }

            await mutationScope.CommitAsync(cancellationToken);
            return;
        }

        await admissions.RequireForMutationAsync(target, cancellationToken);

        var staleTargetAssignments = await dbContext.Set<ProjectPartyAssignment>()
            .Where(item => item.ProjectId == targetProjectId && item.ProjectLifetimeId == target.LifetimeId && normalizedNodeKeys.Contains(item.NodeKey))
            .ToListAsync(cancellationToken);
        if (staleTargetAssignments.Count > 0)
        {
            dbContext.RemoveRange(staleTargetAssignments);
        }

        var assignmentsToMove = await dbContext.Set<ProjectPartyAssignment>()
            .Where(item => item.ProjectId == sourceProjectId && item.ProjectLifetimeId == source.LifetimeId && normalizedNodeKeys.Contains(item.NodeKey))
            .ToListAsync(cancellationToken);
        foreach (var assignment in assignmentsToMove)
        {
            assignment.ProjectId = targetProjectId;
            assignment.ProjectLifetimeId = target.LifetimeId;
        }

        await workAssignments.StageMoveAsync(sourceProjectId, targetProjectId, nodeReferences, cancellationToken, sourceReference, target);

        dbContext.Set<ProjectPartyAssignmentMoveReceipt>().Add(
            new ProjectPartyAssignmentMoveReceipt
            {
                OperationId = operationId.Value,
                DatabaseProfileId = source.DatabaseProfileId,
                SourceProjectLifetimeId = source.LifetimeId,
                TargetProjectLifetimeId = target.LifetimeId,
                SourceProjectId = sourceProjectId,
                TargetProjectId = targetProjectId,
                NodeSetFingerprint = nodeSetFingerprint,
                CompletedAtUtc = clock.GetUtcNow()
            });

        await dbContext.SaveChangesAsync(cancellationToken);
        await mutationScope.CommitAsync(cancellationToken);
    }

    private static string BuildNodeSetFingerprint(
        IReadOnlyCollection<string> normalizedNodeKeys)
    {
        var canonicalPayload = JsonSerializer.Serialize(
            normalizedNodeKeys.Order(StringComparer.Ordinal));
        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload)));
    }

    private static ProjectPartyAssignmentUpsertRequest WithAssignmentId(ProjectPartyAssignmentUpsertRequest request, Guid? id) => new() {
        AssignmentId = id,
        ProjectId = request.ProjectId,
        ExpectedProjectAdmission = request.ExpectedProjectAdmission,
        PartyId = request.PartyId,
        PartyAffiliationId = request.PartyAffiliationId,
        Role = request.Role,
        NodeKey = request.NodeKey,
        IsPrimary = request.IsPrimary,
        AllocationPercent = request.AllocationPercent,
        StartsOn = request.StartsOn,
        EndsOn = request.EndsOn,
        Source = request.Source,
        Notes = request.Notes
    };

    public async Task<Result<ProjectPartyQuickCreateResult>> CreatePartyAsync(
        ProjectPartyQuickCreateRequest request,
        CancellationToken cancellationToken = default,
        Func<CancellationToken, ValueTask>? authorizeCommit = null,
        Func<CancellationToken, ValueTask>? authorizeRead = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (authorizeRead is not null) {
            await authorizeRead(cancellationToken);
        }
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Result<ProjectPartyQuickCreateResult>.Failure(Error.Validation(
                "Display name is required.",
                "crmhr.project-party.display-name-required"));
        }

        var partyType = request.PartyKind switch
        {
            ProjectPartyQuickCreateKind.Organization => PartyType.Organization,
            ProjectPartyQuickCreateKind.OrganizationUnit => PartyType.OrganizationUnit,
            ProjectPartyQuickCreateKind.AiAgent => PartyType.AiAgent,
            _ => PartyType.Person
        };

        var editor = new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = request.DisplayName.Trim(),
            Summary = request.Summary?.Trim() ?? string.Empty,
            LastChangedBy = "project-structure",
            ContactPoints = BuildQuickCreateContacts(request)
        };
        var expected = request.ExpectedProjectAdmission;
        if (expected is not null) {
            if (expected.ProjectId != request.ProjectId) {
                throw new ArgumentException("The quick-create admission must name its original project.", nameof(request));
            }
            await admissions.RequireCurrentAsync(expected, cancellationToken);
        }

        Guid? committed = null;
        try {
            var saveResult = await partyDirectoryService.SavePartyObservedAsync(editor, id => committed = id, cancellationToken, authorizeCommit);
            if (!saveResult.IsSuccess) {
                return Result<ProjectPartyQuickCreateResult>.Failure(saveResult.Errors.ToArray());
            }
            if (authorizeRead is not null) {
                await authorizeRead(cancellationToken);
            }
            var option = await GetPartyOptionAsync(saveResult.Value, cancellationToken);
            if (authorizeRead is not null) {
                await authorizeRead(cancellationToken);
            }
            return Result<ProjectPartyQuickCreateResult>.Success(new(saveResult.Value,
                option?.DisplayName ?? editor.DisplayName, option?.PartyTypeLabel ?? ResolvePartyTypeLabel(partyType)) {
                ObservationWarning = option is null ? "The party was created, but its directory option could not be loaded." : null
            });
        } catch (Exception exception) when (committed.HasValue) {
            logger?.LogWarning("Directory party {PartyId} was committed; its follow-up ended with {FailureType}.",
                committed.Value, exception.GetType().Name);
            return Result<ProjectPartyQuickCreateResult>.Success(new(committed.Value, editor.DisplayName, ResolvePartyTypeLabel(partyType)) {
                ObservationWarning = "The party was created, but directory follow-up did not finish. Reload its metadata; do not create it again."
            });
        }
    }

    private static List<PartyContactPointEditorModel> BuildQuickCreateContacts(ProjectPartyQuickCreateRequest request)
    {
        var contacts = new List<PartyContactPointEditorModel>();
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            contacts.Add(new PartyContactPointEditorModel
            {
                ContactType = PartyContactType.Email,
                Label = "Primary email",
                Value = request.Email.Trim(),
                IsPrimary = true,
                IsPublic = true
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            contacts.Add(new PartyContactPointEditorModel
            {
                ContactType = PartyContactType.Phone,
                Label = "Primary phone",
                Value = request.Phone.Trim(),
                IsPrimary = contacts.Count == 0,
                IsPublic = true
            });
        }

        return contacts;
    }

    private static string ResolvePrimaryDisplayName(
        IReadOnlyList<ProjectPortfolioPartyItem> items,
        ProjectPartyPortfolioCategory category)
    {
        return items
            .Where(item => item.Category == category)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => item.DisplayName)
            .FirstOrDefault() ?? string.Empty;
    }

    internal static ProjectPartyAssignmentRole MapRole(ProjectPartyAssignmentKind role) => ProjectAssignmentRoleMapping.MapRole(role);
    internal static ProjectPartyAssignmentKind MapRole(ProjectPartyAssignmentRole role) => ProjectAssignmentRoleMapping.MapRole(role);

    private static ProjectPartyPortfolioCategory MapPortfolioCategory(ProjectPartyAssignmentKind assignmentKind)
    {
        return assignmentKind switch
        {
            ProjectPartyAssignmentKind.Customer => ProjectPartyPortfolioCategory.Customer,
            ProjectPartyAssignmentKind.DeliveryUnit => ProjectPartyPortfolioCategory.DeliveryUnit,
            ProjectPartyAssignmentKind.Manager or ProjectPartyAssignmentKind.TeamMember or ProjectPartyAssignmentKind.Reviewer => ProjectPartyPortfolioCategory.Owner,
            ProjectPartyAssignmentKind.Partner => ProjectPartyPortfolioCategory.Partner,
            ProjectPartyAssignmentKind.AiAgent => ProjectPartyPortfolioCategory.AiAgent,
            _ => ProjectPartyPortfolioCategory.Stakeholder
        };
    }

    private static string ResolvePortfolioLabel(ProjectPartyAssignmentKind assignmentKind)
    {
        return assignmentKind switch
        {
            ProjectPartyAssignmentKind.CustomerContact => "Customer contact",
            ProjectPartyAssignmentKind.DeliveryUnit => "Delivery unit",
            ProjectPartyAssignmentKind.TeamMember => "Team member",
            ProjectPartyAssignmentKind.AiAgent => "AI agent",
            ProjectPartyAssignmentKind.BillingContact => "Billing contact",
            ProjectPartyAssignmentKind.TechnicalContact => "Technical contact",
            _ => assignmentKind.ToString()
        };
    }

    private static string ResolvePartyTypeLabel(PartyType partyType)
    {
        return partyType switch
        {
            PartyType.OrganizationUnit => "Organization unit",
            PartyType.AiAgent => "AI agent",
            _ => partyType.ToString()
        };
    }

    private static ProjectPartyType MapProjectPartyType(PartyType partyType)
    {
        return partyType switch
        {
            PartyType.Person => ProjectPartyType.Person,
            PartyType.Organization => ProjectPartyType.Organization,
            PartyType.OrganizationUnit => ProjectPartyType.OrganizationUnit,
            PartyType.AiAgent => ProjectPartyType.AiAgent,
            _ => throw new ArgumentOutOfRangeException(nameof(partyType), partyType, "The party type is not supported by project integration.")
        };
    }

    private static string ResolvePrimaryContactValue(
        IReadOnlyList<CrmPartyContactValue> contacts,
        PartyContactType contactType)
    {
        return contacts
            .Where(item => item.ContactType == contactType)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => item.Value)
            .FirstOrDefault()
            ?? string.Empty;
    }

    private static DateTimeOffset? ToUtcDate(DateOnly? value)
    {
        return value.HasValue
            ? new DateTimeOffset(value.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            : null;
    }

    private static List<string> NormalizeNodeKeys(IReadOnlyCollection<ProjectNodeReference> nodeReferences)
    {
        return nodeReferences
            .Select(nodeReference => nodeReference.NodeKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
    private void RequireAssignmentReference(Guid projectId, Guid? lifetimeId, ProjectAssignmentReference? reference) {
        ArgumentNullException.ThrowIfNull(reference);
        reference.RequireProfile(admissions.DatabaseProfileId, projectId);
        if (reference.LifetimeId != lifetimeId) {
            throw new InvalidOperationException("The assignment belongs to a different captured project lifetime.");
        }
    }

    private ProjectWriteAdmission RequireBoundAssignmentReference(Guid projectId, ProjectAssignmentReference? reference) {
        ArgumentNullException.ThrowIfNull(reference);
        reference.RequireProfile(admissions.DatabaseProfileId, projectId);
        return reference.RequireBoundAdmission();
    }

}
