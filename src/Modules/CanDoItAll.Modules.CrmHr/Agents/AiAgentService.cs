using System.Text.Json;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public sealed class AiAgentService(
    IDbContextFactory<CrmHrDbContext> dbContextFactory,
    PartyDirectoryService partyDirectoryService,
    IAiTechnicalAgentBridge technicalAgentBridge,
    AiStewardProfileEditingService stewardship) {
    private const int LegacyDirectorySnapshotLimit = AiAgentDirectoryQueryLimits.MaximumPageSize;

    public Task SynchronizeDirectoryProjectionAsync(CancellationToken cancellationToken = default) {
        return technicalAgentBridge.SynchronizeDirectoryProjectionAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiAgentListItemModel>> ListAgentDirectoryAsync(CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await ListAgentDirectoryFromProjectionAsync(dbContext, cancellationToken);
    }

    public Task<IReadOnlyList<AiAgentListItemModel>> ListAgentDirectorySnapshotAsync(
        CrmHrDbContext dbContext,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(dbContext);

        return ListAgentDirectoryFromProjectionAsync(dbContext, cancellationToken);
    }

    public async Task<IReadOnlyList<AiAgentStaffingFactListItemModel>> ListAgentStaffingFactsAsync(
        IReadOnlyList<Guid>? partyIds = null,
        CancellationToken cancellationToken = default) {
        return await ListAgentStaffingFactsSnapshotAsync(partyIds, cancellationToken);
    }

    public async Task<IReadOnlyList<AiAgentStaffingFactListItemModel>> ListAgentStaffingFactsSnapshotAsync(
        IReadOnlyList<Guid>? partyIds = null,
        CancellationToken cancellationToken = default) {
        var resolvedPartyIds = partyIds?
            .Where(item => item != Guid.Empty)
            .Distinct()
            .ToList()
            ?? [];
        if (resolvedPartyIds.Count == 0) {
            throw new ArgumentException(
                "AI agent staffing facts require at least one party identifier.",
                nameof(partyIds));
        }

        var facts = await technicalAgentBridge.GetStaffingFactsAsync(resolvedPartyIds, cancellationToken);

        return facts.Values
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(item => new AiAgentStaffingFactListItemModel(
                item.PartyId,
                item.TechnicalAgentId,
                item.DisplayName,
                item.RoleTitle,
                item.Summary,
                item.Instructions,
                item.BindingStatus,
                item.BindingSummary,
                item.ExecutionMode,
                item.ProviderName,
                item.DefaultModel,
                item.TemplateKey,
                item.Tags,
                item.Capabilities,
                item.AgentsRoute))
            .ToList();
    }

    public async Task<IReadOnlyList<AiAgentStaffingFactListItemModel>> ListAgentStaffingFactsProjectionAsync(
        CrmHrDbContext dbContext,
        IReadOnlyList<Guid>? partyIds = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(dbContext);

        var resolvedPartyIds = partyIds?
            .Where(item => item != Guid.Empty)
            .Distinct()
            .ToList()
            ?? [];
        if (resolvedPartyIds.Count == 0) {
            throw new ArgumentException(
                "AI agent staffing facts require at least one party identifier.",
                nameof(partyIds));
        }

        var partiesQuery = dbContext.Set<Party>()
            .AsNoTracking()
            .Where(item => item.PartyType == PartyType.AiAgent)
            .Where(item => resolvedPartyIds.Contains(item.Id));

        var parties = await partiesQuery
            .OrderBy(item => item.DisplayName)
            .Select(item => new {
                item.Id,
                item.DisplayName,
                item.Summary,
                item.TagsJson
            })
            .ToListAsync(cancellationToken);
        if (parties.Count == 0) {
            return [];
        }

        var partyIdsToLoad = parties.Select(item => item.Id).ToList();
        var technicalFacts = await technicalAgentBridge.GetStaffingFactsAsync(partyIdsToLoad, cancellationToken);

        return parties
            .Select(party => {
                technicalFacts.TryGetValue(party.Id, out var technicalFact);
                if (technicalFact is null) {
                    throw new InvalidOperationException(
                        $"AI agent party '{party.Id:D}' did not receive a technical projection fact.");
                }

                return new AiAgentStaffingFactListItemModel(
                    party.Id,
                    technicalFact.TechnicalAgentId,
                    party.DisplayName,
                    technicalFact.RoleTitle,
                    party.Summary,
                    technicalFact.Instructions,
                    technicalFact.BindingStatus,
                    technicalFact.BindingSummary,
                    technicalFact.ExecutionMode,
                    technicalFact.ProviderName,
                    technicalFact.DefaultModel,
                    technicalFact.TemplateKey,
                    ResolveAiProjectionTags(party.TagsJson, party.Id, technicalFact.Tags),
                    technicalFact.Capabilities,
                    technicalFact.AgentsRoute);
            })
            .ToList();
    }

    private async Task<IReadOnlyList<AiAgentListItemModel>> ListAgentDirectoryFromProjectionAsync(
        CrmHrDbContext dbContext,
        CancellationToken cancellationToken) {
        var parties = await dbContext.Set<Party>()
            .Where(item => item.PartyType == PartyType.AiAgent)
            .OrderBy(item => item.DisplayName)
            .Take(LegacyDirectorySnapshotLimit + 1)
            .Select(item => new {
                item.Id,
                item.DisplayName,
                item.Summary,
                item.LifecycleStatus,
                item.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
        if (parties.Count > LegacyDirectorySnapshotLimit) {
            throw new InvalidOperationException(
                $"The legacy AI agent directory snapshot is limited to {LegacyDirectorySnapshotLimit} records. Use {nameof(IAiAgentDirectoryQueryService)} for paged access.");
        }
        if (parties.Count == 0) {
            return [];
        }

        var partyIds = parties.Select(item => item.Id).ToList();
        var profiles = await dbContext.Set<AiAgentProfile>()
            .Where(item => partyIds.Contains(item.PartyId))
            .ToListAsync(cancellationToken);
        var technicalSummaries = await technicalAgentBridge.GetDirectorySummariesAsync(partyIds, cancellationToken);
        var ownerIds = profiles
            .Where(item => item.OwnerPartyId.HasValue)
            .Select(item => item.OwnerPartyId!.Value)
            .Distinct()
            .ToList();

        var ownerNames = ownerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<Party>()
                .Where(item => ownerIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);
        var profileByPartyId = profiles.ToDictionary(item => item.PartyId);

        return parties
            .Where(item => technicalSummaries.GetValueOrDefault(item.Id)?.HasTechnicalProfile == true)
            .Select(item => {
                profileByPartyId.TryGetValue(item.Id, out var profile);
                technicalSummaries.TryGetValue(item.Id, out var technicalSummary);
                var hasTechnicalProfile = technicalSummary?.HasTechnicalProfile == true;
                var hasProfile = hasTechnicalProfile || profile is not null;
                var validationStatus = profile?.ValidationStatus ?? (hasTechnicalProfile ? AiValidationStatus.Draft : null);
                var executionMode = technicalSummary?.ExecutionMode;
                return new AiAgentListItemModel(
                    item.Id,
                    item.DisplayName,
                    item.Summary,
                    item.LifecycleStatus,
                    technicalSummary?.TechnicalAgentId,
                    technicalSummary?.BindingStatus ?? AiResourceBindingStatus.Unbound,
                    technicalSummary?.BindingSummary ?? "No technical binding.",
                    executionMode,
                    validationStatus,
                    technicalSummary?.ProviderName ?? string.Empty,
                    technicalSummary?.DefaultModel ?? string.Empty,
                    profile?.OwnerPartyId is Guid ownerPartyId ? ownerNames.GetValueOrDefault(ownerPartyId) ?? string.Empty : string.Empty,
                    technicalSummary?.CapabilityCount ?? 0,
                    hasProfile,
                    technicalSummary?.AgentsRoute ?? "/agents?tab=agents",
                    item.UpdatedAtUtc);
            })
            .ToList();
    }

    public async Task<Result<Guid>> CreateAgentAsync(
        string displayName,
        string externalCode = "",
        string summary = "",
        string lastChangedBy = "crm-hr-ui",
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(displayName)) {
            return Result<Guid>.Failure(Error.Validation("AI agent name is required.", "crmhr.ai-agent.display-name-required"));
        }

        PartySaveReceipt? accepted = null;
        try {
            var created = await partyDirectoryService.SavePartyWithReceiptAsync(new() {
                PartyType = PartyType.AiAgent,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = displayName.Trim(),
                ExternalCode = externalCode.Trim(),
                Summary = summary.Trim(),
                LastChangedBy = string.IsNullOrWhiteSpace(lastChangedBy) ? "crm-hr-ui" : lastChangedBy.Trim()
            }, receipt => accepted = receipt, cancellationToken);
            if (created.IsFailure) {
                return Result<Guid>.Failure(created.Errors);
            }
            var partyId = created.Value!.PartyId;
            var profile = await SaveAgentProfileAsync(new() {
                PartyId = partyId,
                Notes = summary.Trim(),
                LastChangedBy = string.IsNullOrWhiteSpace(lastChangedBy) ? "crm-hr-ui" : lastChangedBy.Trim()
            }, cancellationToken);
            if (profile.IsFailure) {
                throw new AiAgentCreationIncompleteException(partyId, profile.Errors);
            }
            return Result<Guid>.Success(partyId);
        } catch (AiTechnicalAgentCommittedSaveException) {
            throw;
        } catch (AiAgentCreationIncompleteException) {
            throw;
        } catch (Exception exception) when (accepted is not null) {
            throw new AiAgentCreationIncompleteException(accepted.PartyId, [], exception);
        }
    }

    public async Task<AiAgentWorkspaceModel?> GetAgentWorkspaceAsync(Guid partyId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>()
            .Select(item => new {
                item.Id,
                item.DisplayName,
                item.Summary,
                item.LifecycleStatus,
                item.PartyType
            })
            .SingleOrDefaultAsync(item => item.Id == partyId, cancellationToken);
        if (party is null || party.PartyType != PartyType.AiAgent) {
            return null;
        }

        var contactPoints = await dbContext.Set<PartyContactPoint>()
            .Where(item => item.PartyId == partyId)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => new CrmPartyContactValue(item.PartyId, item.ContactType, item.Value, item.IsPrimary))
            .ToListAsync(cancellationToken);
        var profile = await dbContext.Set<AiAgentProfile>()
            .SingleOrDefaultAsync(item => item.PartyId == partyId, cancellationToken);
        var technicalSummaries = await technicalAgentBridge.GetDirectorySummariesAsync(
            [partyId],
            cancellationToken);
        if (!technicalSummaries.TryGetValue(partyId, out var technicalSummary) ||
            !technicalSummary.HasTechnicalProfile) {
            return null;
        }

        var ownerName = profile?.OwnerPartyId is Guid ownerPartyId
            ? await dbContext.Set<Party>()
                .AsNoTracking()
                .Where(item => item.Id == ownerPartyId)
                .Select(item => item.DisplayName)
                .SingleOrDefaultAsync(cancellationToken)
                ?? string.Empty
            : string.Empty;

        var editor = new AiAgentProfileEditorModel {
                Id = profile?.Id,
                PartyId = party.Id,
                DefaultModel = technicalSummary.DefaultModel,
                ExecutionMode = technicalSummary.ExecutionMode ?? AiExecutionMode.Remote,
                OwnerPartyId = profile?.OwnerPartyId,
                ValidationStatus = profile?.ValidationStatus ?? AiValidationStatus.Draft,
                LastReviewedOn = profile?.LastReviewedAtUtc is DateTimeOffset reviewedAtUtc ? DateOnly.FromDateTime(reviewedAtUtc.UtcDateTime) : null,
                Notes = profile?.Notes ?? string.Empty,
                ExtendedDataJson = profile?.ExtendedDataJson ?? "{}",
                LastChangedBy = "crm-hr-ui"
            };
        editor.Original = new(AiStewardEditValues.Observe(party.Id, party.PartyType, profile), AiProfileTechnicalInput.From(editor));

        return new AiAgentWorkspaceModel(
            party.Id,
            party.DisplayName,
            party.Summary,
            party.LifecycleStatus,
            ResolvePrimaryContactValue(contactPoints, PartyContactType.Email),
            ResolvePrimaryContactValue(contactPoints, PartyContactType.Phone),
            technicalSummary.TechnicalAgentId,
            technicalSummary.BindingStatus,
            technicalSummary.BindingSummary,
            technicalSummary.AgentsRoute,
            technicalSummary.ProviderName,
            ownerName,
            technicalSummary.CapabilityCount,
            editor);
    }

    public async Task<Result<Guid>> SaveAgentProfileAsync(AiAgentProfileEditorModel model, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(model);
        var draft = model.Snapshot();
        if (draft.Original is { } observed) {
            if (AiProfileTechnicalInput.From(draft) != observed.Technical) {
                return Result<Guid>.Failure(Error.Validation("Technical configuration is read-only in the business profile. Edit it through AgentFramework.", AiStewardEditingErrors.TechnicalReadOnly));
            }
            return await stewardship.SaveAsync(BusinessDraft(draft, observed.Business), cancellationToken);
        }
        if (draft.PartyId == Guid.Empty || draft.Id.HasValue) {
            return Result<Guid>.Failure(Error.Validation("Read the business profile before replacing it.", AiStewardEditingErrors.OriginalRequired));
        }
        if (!Enum.IsDefined(draft.ValidationStatus) || !Enum.IsDefined(draft.ExecutionMode)) {
            return Result<Guid>.Failure(Error.Validation("Choose supported profile statuses."));
        }
        if (NormalizeJson(draft.ExtendedDataJson, "{}") is null) {
            return Result<Guid>.Failure(Error.Validation("Extended data must be valid JSON.", AiStewardEditingErrors.InvalidTechnicalJson));
        }
        await using (var context = await dbContextFactory.CreateDbContextAsync(cancellationToken)) {
            if (draft.OwnerPartyId is { } ownerId && (ownerId == draft.PartyId ||
                !await context.Set<Party>().AnyAsync(row => row.Id == ownerId && row.PartyType == PartyType.Person, cancellationToken))) {
                return Result<Guid>.Failure(Error.Validation("The steward must reference a current person.", AiStewardEditingErrors.OwnerInvalid));
            }
        }
        var technical = await technicalAgentBridge.SaveAsync(draft, cancellationToken);
        if (technical.IsFailure) {
            return Result<Guid>.Failure(technical.Errors);
        }
        var accepted = technical.Value!;
        try {
            var original = accepted.CreatedBusinessProfile ?? throw new InvalidOperationException("Technical creation did not return its accepted business-profile observation.");
            var business = BusinessDraft(draft, original);
            business.Id = original.ProfileId;
            var result = await stewardship.SaveAsync(business, cancellationToken);
            if (result.IsFailure) {
                throw new InvalidOperationException($"Technical creation was accepted, but business continuation was rejected: {string.Join(", ", result.Errors.Select(error => error.Code))}.");
            }
            return result;
        } catch (AiTechnicalAgentCommittedSaveException) {
            throw;
        } catch (Exception exception) when (accepted.TechnicalAgentId is { } technicalAgentId) {
            throw new AiTechnicalAgentCommittedSaveException(technicalAgentId, draft.PartyId, exception);
        }
    }

    private static AiStewardProfileEditorModel BusinessDraft(AiAgentProfileEditorModel draft, AiStewardProfileObservation original) => new() {
        Id = draft.Id,
        PartyId = draft.PartyId,
        OwnerPartyId = draft.OwnerPartyId,
        ValidationStatus = draft.ValidationStatus,
        LastReviewedOn = draft.LastReviewedOn,
        Notes = draft.Notes,
        LastChangedBy = draft.LastChangedBy,
        Original = original
    };

    public async Task<IReadOnlyList<AiAgentProfileSummaryModel>> ListAgentProfilesAsync(CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<AiAgentProfile>()
            .OrderBy(item => item.ValidationStatus)
            .ThenBy(item => item.DefaultModel)
            .Select(item => new AiAgentProfileSummaryModel(item.Id, item.PartyId, item.ProviderProfileId, item.ExecutionMode, item.ValidationStatus))
            .ToListAsync(cancellationToken);
    }

    private static string ResolveAiProjectionBindingSummary(AiResourceBinding? binding) {
        if (binding is null) {
            return "No technical binding.";
        }

        if (!string.IsNullOrWhiteSpace(binding.LastError)) {
            return $"{binding.BindingStatus}: {binding.LastError.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(binding.BindingReason)) {
            return binding.BindingReason.Trim();
        }

        return binding.BindingStatus.ToString();
    }

    private static IReadOnlyList<string> DeserializeAiProjectionTags(string json, Guid partyId) {
        if (string.IsNullOrWhiteSpace(json)) {
            return [];
        }

        try {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        } catch (JsonException) {
            throw new InvalidOperationException($"AI agent party '{partyId}' contains invalid tags JSON.");
        }
    }

    private static IReadOnlyList<string> ResolveAiProjectionTags(
        string json,
        Guid partyId,
        IReadOnlyList<string> technicalTags) {
        return DeserializeAiProjectionTags(json, partyId)
            .Concat(technicalTags)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildAgentsRoute(Guid? technicalAgentId) {
        return technicalAgentId.HasValue
            ? $"/agents?tab=agents&agentId={technicalAgentId.Value:D}"
            : "/agents?tab=agents";
    }

    private static string ResolvePrimaryContactValue(IEnumerable<CrmPartyContactValue> contacts, PartyContactType contactType) {
        return contacts
            .Where(item => item.ContactType == contactType)
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => item.Value)
            .FirstOrDefault()
            ?? string.Empty;
    }

    private static string? NormalizeJson(string json, string fallback) {
        if (string.IsNullOrWhiteSpace(json)) {
            return fallback;
        }

        try {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetRawText();
        } catch (JsonException) {
            return null;
        }
    }
    public async Task<IReadOnlyList<AiAgentDiagnosticParty>> ListDiagnosticPartiesAsync(CancellationToken cancellationToken = default) {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<Party>().AsNoTracking()
            .Where(party => party.PartyType == PartyType.AiAgent)
            .OrderBy(party => party.DisplayName)
            .Select(party => new AiAgentDiagnosticParty(party.Id, party.DisplayName))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiAgentDiagnosticBinding>> ListDiagnosticBindingsAsync(
        IReadOnlyCollection<Guid> partyIds, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(partyIds);
        var ids = partyIds.ToArray();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<AiResourceBinding>().AsNoTracking()
            .Where(binding => ids.Contains(binding.PartyId))
            .Select(binding => new AiAgentDiagnosticBinding(
                binding.PartyId, binding.TechnicalAgentId, binding.BindingStatus, binding.BindingReason))
            .ToListAsync(cancellationToken);
    }
}
