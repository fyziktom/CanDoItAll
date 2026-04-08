using System.Text;
using System.Text.Json;
using System.Linq.Expressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public sealed class PartyDirectoryManagementService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    PartyDirectoryService partyDirectoryService)
{
    private static readonly string[] ExportColumns =
    [
        "DisplayName",
        "PartyType",
        "LifecycleStatus",
        "ExternalCode",
        "LegalName",
        "PreferredName",
        "Summary",
        "Tags",
        "Region",
        "CountryCode",
        "TimeZone",
        "IsSensitive",
        "Roles",
        "ContactPoints",
        "Addresses"
    ];

    public async Task<IReadOnlyList<PartyRelationshipListItemModel>> ListRelationshipsAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var relationships = await dbContext.Set<PartyRelationship>()
            .Where(item => item.SourcePartyId == partyId || item.TargetPartyId == partyId)
            .ToListAsync(cancellationToken);

        var relatedPartyIds = relationships
            .Select(item => item.SourcePartyId == partyId ? item.TargetPartyId : item.SourcePartyId)
            .Distinct()
            .ToList();

        var relatedParties = await dbContext.Set<Party>()
            .Where(item => relatedPartyIds.Contains(item.Id))
            .Select(item => new { item.Id, item.DisplayName, item.PartyType })
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        return relationships
            .Select(item =>
            {
                var isOutgoing = item.SourcePartyId == partyId;
                var relatedPartyId = isOutgoing ? item.TargetPartyId : item.SourcePartyId;
                if (!relatedParties.TryGetValue(relatedPartyId, out var relatedParty))
                {
                    return null;
                }

                return new PartyRelationshipListItemModel(
                    item.Id,
                    relatedPartyId,
                    relatedParty.DisplayName,
                    relatedParty.PartyType,
                    item.RelationshipKind,
                    isOutgoing,
                    item.IsPrimary,
                    item.StartDateUtc,
                    item.EndDateUtc,
                    item.Notes);
            })
            .Where(item => item is not null)
            .Cast<PartyRelationshipListItemModel>()
            .OrderBy(item => item.RelatedPartyDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.RelationshipKind)
            .ToList();
    }

    public async Task<Result> SaveRelationshipsAsync(
        Guid partyId,
        IReadOnlyList<PartyRelationshipEditorModel> relationships,
        string actor,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var partyExists = await dbContext.Set<Party>().AnyAsync(item => item.Id == partyId, cancellationToken);
        if (!partyExists)
        {
            return Result.Failure(Error.Failure("The selected party was not found.", "crmhr.party.not-found"));
        }

        var invalidRelationships = relationships
            .Where(item => item.RelatedPartyId == Guid.Empty || item.RelatedPartyId == partyId)
            .ToList();
        if (invalidRelationships.Count > 0)
        {
            return Result.Failure(Error.Validation(
                "Relationships must target another saved party.",
                "crmhr.party.relationship-invalid"));
        }

        var existingRelationships = await dbContext.Set<PartyRelationship>()
            .Where(item => item.SourcePartyId == partyId || item.TargetPartyId == partyId)
            .ToListAsync(cancellationToken);
        dbContext.Set<PartyRelationship>().RemoveRange(existingRelationships);

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var relationship in relationships)
        {
            var sourcePartyId = relationship.IsOutgoing ? partyId : relationship.RelatedPartyId;
            var targetPartyId = relationship.IsOutgoing ? relationship.RelatedPartyId : partyId;
            var dedupeKey = BuildRelationshipKey(sourcePartyId, targetPartyId, relationship.RelationshipKind);
            if (!seenKeys.Add(dedupeKey))
            {
                continue;
            }

            dbContext.Set<PartyRelationship>().Add(new PartyRelationship
            {
                Id = relationship.Id ?? Guid.NewGuid(),
                SourcePartyId = sourcePartyId,
                TargetPartyId = targetPartyId,
                RelationshipKind = relationship.RelationshipKind,
                IsPrimary = relationship.IsPrimary,
                StartDateUtc = relationship.StartDateUtc,
                EndDateUtc = relationship.EndDateUtc,
                Notes = relationship.Notes.Trim()
            });
        }

        dbContext.Set<CrmHrAuditEntry>().Add(new CrmHrAuditEntry
        {
            EntityType = nameof(Party),
            EntityId = partyId,
            Action = "RelationshipsSaved",
            Summary = $"Saved {seenKeys.Count} relationship(s).",
            DetailJson = JsonSerializer.Serialize(new { RelationshipCount = seenKeys.Count }),
            Actor = ResolveActor(actor),
            CreatedAtUtc = clock.GetUtcNow()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<IReadOnlyList<PartyDuplicateCandidateModel>> FindPotentialDuplicatesAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>()
            .Where(item => item.Id == partyId)
            .Select(item => new
            {
                item.Id,
                item.DisplayName,
                item.LegalName,
                item.PreferredName,
                item.ExternalCode
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (party is null)
        {
            return [];
        }

        var contactValues = await dbContext.Set<PartyContactPoint>()
            .Where(item => item.PartyId == partyId)
            .Select(item => item.NormalizedValue)
            .ToListAsync(cancellationToken);

        return await FindPotentialDuplicatesCoreAsync(
            dbContext,
            party.DisplayName,
            party.LegalName,
            party.PreferredName,
            party.ExternalCode,
            contactValues,
            party.Id,
            cancellationToken);
    }

    public async Task<Result<PartyMergeSummaryModel>> MergePartyAsync(
        Guid retainedPartyId,
        Guid mergedPartyId,
        string actor,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (retainedPartyId == mergedPartyId)
        {
            return Result<PartyMergeSummaryModel>.Failure(Error.Validation(
                "A party cannot be merged into itself.",
                "crmhr.party.merge-same-party"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var parties = await dbContext.Set<Party>()
            .Where(item => item.Id == retainedPartyId || item.Id == mergedPartyId)
            .ToListAsync(cancellationToken);
        var retainedParty = parties.SingleOrDefault(item => item.Id == retainedPartyId);
        var mergedParty = parties.SingleOrDefault(item => item.Id == mergedPartyId);
        if (retainedParty is null || mergedParty is null)
        {
            return Result<PartyMergeSummaryModel>.Failure(Error.Failure(
                "One or both selected parties were not found.",
                "crmhr.party.merge-not-found"));
        }

        retainedParty.DisplayName = PreferExisting(retainedParty.DisplayName, mergedParty.DisplayName);
        retainedParty.LegalName = PreferExisting(retainedParty.LegalName, mergedParty.LegalName);
        retainedParty.PreferredName = PreferExisting(retainedParty.PreferredName, mergedParty.PreferredName);
        retainedParty.ExternalCode = PreferExisting(retainedParty.ExternalCode, mergedParty.ExternalCode);
        retainedParty.Summary = PreferExisting(retainedParty.Summary, mergedParty.Summary);
        retainedParty.Region = PreferExisting(retainedParty.Region, mergedParty.Region);
        retainedParty.CountryCode = PreferExisting(retainedParty.CountryCode, mergedParty.CountryCode);
        retainedParty.TimeZone = PreferExisting(retainedParty.TimeZone, mergedParty.TimeZone);
        retainedParty.IsSensitive = retainedParty.IsSensitive || mergedParty.IsSensitive;
        retainedParty.Notes = CombineText(
            retainedParty.Notes,
            $"Merged party '{mergedParty.DisplayName}' on {clock.GetUtcNow():u}.{Environment.NewLine}{reason}".Trim());
        retainedParty.TagsJson = JsonSerializer.Serialize(
            DeserializeTags(retainedParty.TagsJson)
                .Concat(DeserializeTags(mergedParty.TagsJson))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList());
        retainedParty.UpdatedAtUtc = clock.GetUtcNow();
        retainedParty.LastChangedBy = ResolveActor(actor);

        await MergeRoleAssignmentsAsync(dbContext, retainedPartyId, mergedPartyId, cancellationToken);
        await MergeContactPointsAsync(dbContext, retainedPartyId, mergedPartyId, cancellationToken);
        await MergeAddressesAsync(dbContext, retainedPartyId, mergedPartyId, cancellationToken);
        await MergeRelationshipsAsync(dbContext, retainedPartyId, mergedPartyId, cancellationToken);
        await MergePartySkillsAsync(dbContext, retainedPartyId, mergedPartyId, cancellationToken);
        await MergeAiAgentProfilesAsync(dbContext, retainedPartyId, mergedPartyId, cancellationToken);
        await ReassignDirectPartyReferencesAsync(dbContext, retainedPartyId, mergedPartyId, cancellationToken);
        await ReassignOptionalPartyReferencesAsync(dbContext, retainedPartyId, mergedPartyId, cancellationToken);

        dbContext.Set<Party>().Remove(mergedParty);
        dbContext.Set<CrmHrAuditEntry>().Add(new CrmHrAuditEntry
        {
            EntityType = nameof(Party),
            EntityId = retainedPartyId,
            Action = "MergedDuplicate",
            Summary = $"Merged duplicate party '{mergedParty.DisplayName}' into '{retainedParty.DisplayName}'.",
            DetailJson = JsonSerializer.Serialize(new
            {
                RetainedPartyId = retainedPartyId,
                MergedPartyId = mergedPartyId,
                Reason = reason
            }),
            Actor = ResolveActor(actor),
            IsSensitive = retainedParty.IsSensitive,
            CreatedAtUtc = clock.GetUtcNow()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<PartyMergeSummaryModel>.Success(new PartyMergeSummaryModel(
            retainedPartyId,
            mergedPartyId,
            $"Merged '{mergedParty.DisplayName}' into '{retainedParty.DisplayName}'."));
    }

    public async Task<string> ExportPartiesCsvAsync(
        IReadOnlyCollection<Guid> partyIds,
        CancellationToken cancellationToken = default)
    {
        if (partyIds.Count == 0)
        {
            return string.Join(",", ExportColumns) + Environment.NewLine;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var parties = await dbContext.Set<Party>()
            .Where(item => partyIds.Contains(item.Id))
            .OrderBy(item => item.DisplayName)
            .ToListAsync(cancellationToken);
        var roles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => partyIds.Contains(item.PartyId))
            .ToListAsync(cancellationToken);
        var contactPoints = await dbContext.Set<PartyContactPoint>()
            .Where(item => partyIds.Contains(item.PartyId))
            .ToListAsync(cancellationToken);
        var addresses = await dbContext.Set<PartyAddress>()
            .Where(item => partyIds.Contains(item.PartyId))
            .ToListAsync(cancellationToken);

        var rolesByPartyId = roles.GroupBy(item => item.PartyId).ToDictionary(group => group.Key, group => group.ToList());
        var contactsByPartyId = contactPoints.GroupBy(item => item.PartyId).ToDictionary(group => group.Key, group => group.ToList());
        var addressesByPartyId = addresses.GroupBy(item => item.PartyId).ToDictionary(group => group.Key, group => group.ToList());

        var builder = new StringBuilder();
        AppendCsvRow(builder, ExportColumns);
        foreach (var party in parties)
        {
            AppendCsvRow(
                builder,
                party.DisplayName,
                party.PartyType.ToString(),
                party.LifecycleStatus.ToString(),
                party.ExternalCode,
                party.LegalName,
                party.PreferredName,
                party.Summary,
                string.Join(", ", DeserializeTags(party.TagsJson)),
                party.Region,
                party.CountryCode,
                party.TimeZone,
                party.IsSensitive.ToString(),
                SerializeRoleAssignments(rolesByPartyId.GetValueOrDefault(party.Id) ?? []),
                SerializeContactPoints(contactsByPartyId.GetValueOrDefault(party.Id) ?? []),
                SerializeAddresses(addressesByPartyId.GetValueOrDefault(party.Id) ?? []));
        }

        return builder.ToString();
    }

    public async Task<Result<PartyCsvImportPreviewModel>> PreviewImportAsync(
        string csvContent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return Result<PartyCsvImportPreviewModel>.Failure(Error.Validation(
                "Paste CSV content before previewing the import.",
                "crmhr.party.import-empty"));
        }

        var rows = ParseCsv(csvContent);
        if (rows.Count < 2)
        {
            return Result<PartyCsvImportPreviewModel>.Failure(Error.Validation(
                "The CSV content must include a header row and at least one data row.",
                "crmhr.party.import-no-data"));
        }

        var headerMap = BuildHeaderMap(rows[0]);
        var previewRows = new List<PartyCsvImportPreviewRowModel>();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            var cells = rows[rowIndex];
            if (cells.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var messages = new List<string>();
            var party = BuildImportParty(cells, headerMap, messages);
            if (party is null)
            {
                previewRows.Add(new PartyCsvImportPreviewRowModel
                {
                    RowNumber = rowIndex + 1,
                    Messages = messages,
                    CanImport = false
                });
                continue;
            }

            var duplicateCandidates = await FindPotentialDuplicatesCoreAsync(
                dbContext,
                party.DisplayName,
                party.LegalName,
                party.PreferredName,
                party.ExternalCode,
                party.ContactPoints.Select(item => item.NormalizedValue),
                excludePartyId: null,
                cancellationToken);

            if (duplicateCandidates.Count > 0)
            {
                messages.Add($"Potential duplicates detected: {string.Join(", ", duplicateCandidates.Select(item => item.DisplayName))}.");
            }

            previewRows.Add(new PartyCsvImportPreviewRowModel
            {
                RowNumber = rowIndex + 1,
                Party = party,
                Messages = messages,
                DuplicateCandidates = duplicateCandidates,
                CanImport = messages.Count == 0
            });
        }

        return Result<PartyCsvImportPreviewModel>.Success(new PartyCsvImportPreviewModel
        {
            Rows = previewRows
        });
    }

    public async Task<Result<int>> ApplyImportAsync(
        IReadOnlyList<PartyCsvImportPreviewRowModel> rows,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var importableRows = rows.Where(item => item.CanImport).ToList();
        if (importableRows.Count == 0)
        {
            return Result<int>.Failure(Error.Validation(
                "There are no importable rows in the current preview.",
                "crmhr.party.import-no-ready-rows"));
        }

        var importedCount = 0;
        foreach (var row in importableRows)
        {
            row.Party.LastChangedBy = ResolveActor(actor);
            var result = await partyDirectoryService.SavePartyAsync(row.Party, cancellationToken);
            if (result.IsFailure)
            {
                return Result<int>.Failure(result.Errors);
            }

            importedCount++;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Set<CrmHrAuditEntry>().Add(new CrmHrAuditEntry
        {
            EntityType = nameof(Party),
            EntityId = Guid.Empty,
            Action = "CsvImport",
            Summary = $"Imported {importedCount} party row(s) from CSV.",
            DetailJson = JsonSerializer.Serialize(new { ImportedCount = importedCount }),
            Actor = ResolveActor(actor),
            CreatedAtUtc = clock.GetUtcNow()
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(importedCount);
    }

    private static async Task MergeRoleAssignmentsAsync(
        AppDbContext dbContext,
        Guid retainedPartyId,
        Guid mergedPartyId,
        CancellationToken cancellationToken)
    {
        var retainedRoles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => item.PartyId == retainedPartyId)
            .ToListAsync(cancellationToken);
        var mergedRoles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => item.PartyId == mergedPartyId)
            .ToListAsync(cancellationToken);

        var keys = retainedRoles
            .Select(BuildRoleAssignmentKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var mergedRole in mergedRoles)
        {
            var key = BuildRoleAssignmentKey(mergedRole);
            if (!keys.Add(key))
            {
                var retainedRole = retainedRoles.First(item => string.Equals(BuildRoleAssignmentKey(item), key, StringComparison.OrdinalIgnoreCase));
                retainedRole.IsPrimary = retainedRole.IsPrimary || mergedRole.IsPrimary;
                retainedRole.ValidFromUtc = MinDate(retainedRole.ValidFromUtc, mergedRole.ValidFromUtc);
                retainedRole.ValidToUtc = MaxDate(retainedRole.ValidToUtc, mergedRole.ValidToUtc);
                retainedRole.Notes = CombineText(retainedRole.Notes, mergedRole.Notes);
                dbContext.Set<PartyRoleAssignment>().Remove(mergedRole);
                continue;
            }

            mergedRole.PartyId = retainedPartyId;
            retainedRoles.Add(mergedRole);
        }
    }

    private static async Task MergeContactPointsAsync(
        AppDbContext dbContext,
        Guid retainedPartyId,
        Guid mergedPartyId,
        CancellationToken cancellationToken)
    {
        var retainedContactPoints = await dbContext.Set<PartyContactPoint>()
            .Where(item => item.PartyId == retainedPartyId)
            .ToListAsync(cancellationToken);
        var mergedContactPoints = await dbContext.Set<PartyContactPoint>()
            .Where(item => item.PartyId == mergedPartyId)
            .ToListAsync(cancellationToken);

        var keys = retainedContactPoints
            .Select(BuildContactPointKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var mergedContactPoint in mergedContactPoints)
        {
            mergedContactPoint.NormalizedValue = NormalizeContactValue(mergedContactPoint.ContactType, mergedContactPoint.Value);
            var key = BuildContactPointKey(mergedContactPoint);
            if (!keys.Add(key))
            {
                var retainedContactPoint = retainedContactPoints.First(item => string.Equals(BuildContactPointKey(item), key, StringComparison.OrdinalIgnoreCase));
                retainedContactPoint.IsPrimary = retainedContactPoint.IsPrimary || mergedContactPoint.IsPrimary;
                retainedContactPoint.IsPublic = retainedContactPoint.IsPublic || mergedContactPoint.IsPublic;
                retainedContactPoint.Notes = CombineText(retainedContactPoint.Notes, mergedContactPoint.Notes);
                dbContext.Set<PartyContactPoint>().Remove(mergedContactPoint);
                continue;
            }

            mergedContactPoint.PartyId = retainedPartyId;
            retainedContactPoints.Add(mergedContactPoint);
        }
    }

    private static async Task MergeAddressesAsync(
        AppDbContext dbContext,
        Guid retainedPartyId,
        Guid mergedPartyId,
        CancellationToken cancellationToken)
    {
        var retainedAddresses = await dbContext.Set<PartyAddress>()
            .Where(item => item.PartyId == retainedPartyId)
            .ToListAsync(cancellationToken);
        var mergedAddresses = await dbContext.Set<PartyAddress>()
            .Where(item => item.PartyId == mergedPartyId)
            .ToListAsync(cancellationToken);

        var keys = retainedAddresses
            .Select(BuildAddressKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var mergedAddress in mergedAddresses)
        {
            var key = BuildAddressKey(mergedAddress);
            if (!keys.Add(key))
            {
                var retainedAddress = retainedAddresses.First(item => string.Equals(BuildAddressKey(item), key, StringComparison.OrdinalIgnoreCase));
                retainedAddress.IsPrimary = retainedAddress.IsPrimary || mergedAddress.IsPrimary;
                retainedAddress.Notes = CombineText(retainedAddress.Notes, mergedAddress.Notes);
                dbContext.Set<PartyAddress>().Remove(mergedAddress);
                continue;
            }

            mergedAddress.PartyId = retainedPartyId;
            retainedAddresses.Add(mergedAddress);
        }
    }

    private static async Task MergeRelationshipsAsync(
        AppDbContext dbContext,
        Guid retainedPartyId,
        Guid mergedPartyId,
        CancellationToken cancellationToken)
    {
        var relationships = await dbContext.Set<PartyRelationship>()
            .Where(item =>
                item.SourcePartyId == retainedPartyId
                || item.TargetPartyId == retainedPartyId
                || item.SourcePartyId == mergedPartyId
                || item.TargetPartyId == mergedPartyId)
            .ToListAsync(cancellationToken);

        var dedupeMap = new Dictionary<string, PartyRelationship>(StringComparer.Ordinal);
        foreach (var relationship in relationships)
        {
            if (relationship.SourcePartyId == mergedPartyId)
            {
                relationship.SourcePartyId = retainedPartyId;
            }

            if (relationship.TargetPartyId == mergedPartyId)
            {
                relationship.TargetPartyId = retainedPartyId;
            }

            if (relationship.SourcePartyId == relationship.TargetPartyId)
            {
                dbContext.Set<PartyRelationship>().Remove(relationship);
                continue;
            }

            var key = BuildRelationshipKey(relationship.SourcePartyId, relationship.TargetPartyId, relationship.RelationshipKind);
            if (!dedupeMap.TryAdd(key, relationship))
            {
                var retainedRelationship = dedupeMap[key];
                retainedRelationship.IsPrimary = retainedRelationship.IsPrimary || relationship.IsPrimary;
                retainedRelationship.StartDateUtc = MinDate(retainedRelationship.StartDateUtc, relationship.StartDateUtc);
                retainedRelationship.EndDateUtc = MaxDate(retainedRelationship.EndDateUtc, relationship.EndDateUtc);
                retainedRelationship.Notes = CombineText(retainedRelationship.Notes, relationship.Notes);
                dbContext.Set<PartyRelationship>().Remove(relationship);
            }
        }
    }

    private static async Task MergePartySkillsAsync(
        AppDbContext dbContext,
        Guid retainedPartyId,
        Guid mergedPartyId,
        CancellationToken cancellationToken)
    {
        var retainedSkills = await dbContext.Set<PartySkill>()
            .Where(item => item.PartyId == retainedPartyId)
            .ToListAsync(cancellationToken);
        var mergedSkills = await dbContext.Set<PartySkill>()
            .Where(item => item.PartyId == mergedPartyId)
            .ToListAsync(cancellationToken);

        var retainedBySkillId = retainedSkills.ToDictionary(item => item.SkillId);
        foreach (var mergedSkill in mergedSkills)
        {
            if (retainedBySkillId.TryGetValue(mergedSkill.SkillId, out var retainedSkill))
            {
                if ((int)mergedSkill.Proficiency > (int)retainedSkill.Proficiency)
                {
                    retainedSkill.Proficiency = mergedSkill.Proficiency;
                }

                retainedSkill.YearsExperience = Math.Max(retainedSkill.YearsExperience, mergedSkill.YearsExperience);
                retainedSkill.CertificationStatus = PreferExisting(retainedSkill.CertificationStatus, mergedSkill.CertificationStatus);
                retainedSkill.LastValidatedAtUtc = MaxDate(retainedSkill.LastValidatedAtUtc, mergedSkill.LastValidatedAtUtc);
                retainedSkill.Notes = CombineText(retainedSkill.Notes, mergedSkill.Notes);
                dbContext.Set<PartySkill>().Remove(mergedSkill);
                continue;
            }

            mergedSkill.PartyId = retainedPartyId;
            retainedBySkillId[mergedSkill.SkillId] = mergedSkill;
        }
    }

    private static async Task MergeAiAgentProfilesAsync(
        AppDbContext dbContext,
        Guid retainedPartyId,
        Guid mergedPartyId,
        CancellationToken cancellationToken)
    {
        var profiles = await dbContext.Set<AiAgentProfile>()
            .Where(item => item.PartyId == retainedPartyId || item.PartyId == mergedPartyId)
            .ToListAsync(cancellationToken);

        var retainedProfile = profiles.SingleOrDefault(item => item.PartyId == retainedPartyId);
        var mergedProfile = profiles.SingleOrDefault(item => item.PartyId == mergedPartyId);
        if (mergedProfile is null)
        {
            return;
        }

        if (retainedProfile is null)
        {
            mergedProfile.PartyId = retainedPartyId;
            return;
        }

        retainedProfile.ProviderProfileId ??= mergedProfile.ProviderProfileId;
        retainedProfile.DefaultModel = PreferExisting(retainedProfile.DefaultModel, mergedProfile.DefaultModel);
        retainedProfile.OwnerPartyId ??= mergedProfile.OwnerPartyId;
        retainedProfile.ExecutionMode = retainedProfile.ExecutionMode == AiExecutionMode.Remote
            ? retainedProfile.ExecutionMode
            : mergedProfile.ExecutionMode;
        retainedProfile.ValidationStatus = retainedProfile.ValidationStatus == AiValidationStatus.Draft
            ? mergedProfile.ValidationStatus
            : retainedProfile.ValidationStatus;
        retainedProfile.LastReviewedAtUtc = MaxDate(retainedProfile.LastReviewedAtUtc, mergedProfile.LastReviewedAtUtc);
        retainedProfile.CapabilityJson = JsonSerializer.Serialize(
            DeserializeStringArray(retainedProfile.CapabilityJson)
                .Concat(DeserializeStringArray(mergedProfile.CapabilityJson))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList());
        retainedProfile.Notes = CombineText(retainedProfile.Notes, mergedProfile.Notes);
        dbContext.Set<AiAgentProfile>().Remove(mergedProfile);
    }

    private static async Task ReassignDirectPartyReferencesAsync(
        AppDbContext dbContext,
        Guid retainedPartyId,
        Guid mergedPartyId,
        CancellationToken cancellationToken)
    {
        await ReassignPartyIdAsync(dbContext.Set<PartyConfidentialNote>(), item => item.PartyId == mergedPartyId, item => item.PartyId = retainedPartyId, cancellationToken);
        await ReassignPartyIdAsync(dbContext.Set<WorkforceProfile>(), item => item.PartyId == mergedPartyId, item => item.PartyId = retainedPartyId, cancellationToken);
        await ReassignPartyIdAsync(dbContext.Set<CapacityBlock>(), item => item.PartyId == mergedPartyId, item => item.PartyId = retainedPartyId, cancellationToken);
        await ReassignPartyIdAsync(dbContext.Set<RecruitmentApplication>(), item => item.PartyId == mergedPartyId, item => item.PartyId = retainedPartyId, cancellationToken);
        await ReassignPartyIdAsync(dbContext.Set<OnboardingTask>(), item => item.PartyId == mergedPartyId, item => item.PartyId = retainedPartyId, cancellationToken);
        await ReassignPartyIdAsync(dbContext.Set<ProjectPartyAssignment>(), item => item.PartyId == mergedPartyId, item => item.PartyId = retainedPartyId, cancellationToken);
        await ReassignPartyIdAsync(dbContext.Set<InteractionPartyLink>(), item => item.PartyId == mergedPartyId, item => item.PartyId = retainedPartyId, cancellationToken);
        await ReassignPartyIdAsync(dbContext.Set<OpportunityPartyLink>(), item => item.PartyId == mergedPartyId, item => item.PartyId = retainedPartyId, cancellationToken);
    }

    private static async Task ReassignOptionalPartyReferencesAsync(
        AppDbContext dbContext,
        Guid retainedPartyId,
        Guid mergedPartyId,
        CancellationToken cancellationToken)
    {
        var interactionRecords = await dbContext.Set<InteractionRecord>()
            .Where(item => item.NextActionOwnerPartyId == mergedPartyId)
            .ToListAsync(cancellationToken);
        foreach (var interactionRecord in interactionRecords)
        {
            interactionRecord.NextActionOwnerPartyId = retainedPartyId;
        }

        var opportunities = await dbContext.Set<Opportunity>()
            .Where(item =>
                item.AccountPartyId == mergedPartyId
                || item.OwnerPartyId == mergedPartyId
                || item.DeliveryUnitPartyId == mergedPartyId)
            .ToListAsync(cancellationToken);
        foreach (var opportunity in opportunities)
        {
            if (opportunity.AccountPartyId == mergedPartyId)
            {
                opportunity.AccountPartyId = retainedPartyId;
            }

            if (opportunity.OwnerPartyId == mergedPartyId)
            {
                opportunity.OwnerPartyId = retainedPartyId;
            }

            if (opportunity.DeliveryUnitPartyId == mergedPartyId)
            {
                opportunity.DeliveryUnitPartyId = retainedPartyId;
            }
        }

        var workforceProfiles = await dbContext.Set<WorkforceProfile>()
            .Where(item => item.HomeUnitPartyId == mergedPartyId || item.ManagerPartyId == mergedPartyId)
            .ToListAsync(cancellationToken);
        foreach (var workforceProfile in workforceProfiles)
        {
            if (workforceProfile.HomeUnitPartyId == mergedPartyId)
            {
                workforceProfile.HomeUnitPartyId = retainedPartyId;
            }

            if (workforceProfile.ManagerPartyId == mergedPartyId)
            {
                workforceProfile.ManagerPartyId = retainedPartyId;
            }
        }

        var staffingRequests = await dbContext.Set<StaffingRequest>()
            .Where(item => item.RequestedByPartyId == mergedPartyId || item.DeliveryUnitPartyId == mergedPartyId)
            .ToListAsync(cancellationToken);
        foreach (var staffingRequest in staffingRequests)
        {
            if (staffingRequest.RequestedByPartyId == mergedPartyId)
            {
                staffingRequest.RequestedByPartyId = retainedPartyId;
            }

            if (staffingRequest.DeliveryUnitPartyId == mergedPartyId)
            {
                staffingRequest.DeliveryUnitPartyId = retainedPartyId;
            }
        }

        var recruitmentApplications = await dbContext.Set<RecruitmentApplication>()
            .Where(item =>
                item.TargetUnitPartyId == mergedPartyId
                || item.RecruiterPartyId == mergedPartyId
                || item.HiringManagerPartyId == mergedPartyId)
            .ToListAsync(cancellationToken);
        foreach (var recruitmentApplication in recruitmentApplications)
        {
            if (recruitmentApplication.TargetUnitPartyId == mergedPartyId)
            {
                recruitmentApplication.TargetUnitPartyId = retainedPartyId;
            }

            if (recruitmentApplication.RecruiterPartyId == mergedPartyId)
            {
                recruitmentApplication.RecruiterPartyId = retainedPartyId;
            }

            if (recruitmentApplication.HiringManagerPartyId == mergedPartyId)
            {
                recruitmentApplication.HiringManagerPartyId = retainedPartyId;
            }
        }

        var recruitmentInterviews = await dbContext.Set<RecruitmentInterview>()
            .Where(item => item.InterviewerPartyId == mergedPartyId)
            .ToListAsync(cancellationToken);
        foreach (var recruitmentInterview in recruitmentInterviews)
        {
            recruitmentInterview.InterviewerPartyId = retainedPartyId;
        }

        var onboardingTasks = await dbContext.Set<OnboardingTask>()
            .Where(item => item.OwnerPartyId == mergedPartyId)
            .ToListAsync(cancellationToken);
        foreach (var onboardingTask in onboardingTasks)
        {
            onboardingTask.OwnerPartyId = retainedPartyId;
        }

        var aiAgentProfiles = await dbContext.Set<AiAgentProfile>()
            .Where(item => item.OwnerPartyId == mergedPartyId)
            .ToListAsync(cancellationToken);
        foreach (var aiAgentProfile in aiAgentProfiles)
        {
            aiAgentProfile.OwnerPartyId = retainedPartyId;
        }
    }

    private static async Task ReassignPartyIdAsync<TEntity>(
        DbSet<TEntity> set,
        Expression<Func<TEntity, bool>> predicate,
        Action<TEntity> assign,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var entities = await set.Where(predicate).ToListAsync(cancellationToken);
        foreach (var entity in entities)
        {
            assign(entity);
        }
    }

    private async Task<IReadOnlyList<PartyDuplicateCandidateModel>> FindPotentialDuplicatesCoreAsync(
        AppDbContext dbContext,
        string displayName,
        string legalName,
        string preferredName,
        string externalCode,
        IEnumerable<string> normalizedContactValues,
        Guid? excludePartyId,
        CancellationToken cancellationToken)
    {
        var normalizedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddIfNotBlank(normalizedNames, NormalizeName(displayName));
        AddIfNotBlank(normalizedNames, NormalizeName(legalName));
        AddIfNotBlank(normalizedNames, NormalizeName(preferredName));

        var normalizedContacts = normalizedContactValues
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var parties = await dbContext.Set<Party>()
            .Where(item => !excludePartyId.HasValue || item.Id != excludePartyId.Value)
            .Select(item => new
            {
                item.Id,
                item.DisplayName,
                item.LegalName,
                item.PreferredName,
                item.ExternalCode,
                item.PartyType,
                item.LifecycleStatus,
                item.Summary
            })
            .ToListAsync(cancellationToken);

        var partyIds = parties.Select(item => item.Id).ToList();
        var contactPoints = await dbContext.Set<PartyContactPoint>()
            .Where(item => partyIds.Contains(item.PartyId))
            .Select(item => new { item.PartyId, item.NormalizedValue })
            .ToListAsync(cancellationToken);
        var contactsByPartyId = contactPoints
            .GroupBy(item => item.PartyId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.NormalizedValue).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var candidates = new List<(PartyDuplicateCandidateModel Candidate, int Score)>();
        foreach (var party in parties)
        {
            var reasons = new List<string>();
            var score = 0;

            if (!string.IsNullOrWhiteSpace(externalCode)
                && string.Equals(party.ExternalCode, externalCode, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("matching external code");
                score += 4;
            }

            if (normalizedNames.Contains(NormalizeName(party.DisplayName))
                || normalizedNames.Contains(NormalizeName(party.LegalName))
                || normalizedNames.Contains(NormalizeName(party.PreferredName)))
            {
                reasons.Add("matching normalized name");
                score += 2;
            }

            var partyContacts = contactsByPartyId.GetValueOrDefault(party.Id) ?? [];
            if (partyContacts.Any(normalizedContacts.Contains))
            {
                reasons.Add("matching contact value");
                score += 3;
            }

            if (reasons.Count == 0)
            {
                continue;
            }

            candidates.Add((
                new PartyDuplicateCandidateModel(
                    party.Id,
                    party.DisplayName,
                    party.PartyType,
                    party.LifecycleStatus,
                    party.Summary,
                    reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToList()),
                score));
        }

        return candidates
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Candidate)
            .ToList();
    }

    private static PartyEditorModel? BuildImportParty(
        IReadOnlyList<string> cells,
        IReadOnlyDictionary<string, int> headerMap,
        List<string> messages)
    {
        var displayName = GetCell(cells, headerMap, "DisplayName").Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            messages.Add("DisplayName is required.");
        }

        if (!Enum.TryParse<PartyType>(GetCell(cells, headerMap, "PartyType"), ignoreCase: true, out var partyType))
        {
            messages.Add("PartyType is invalid.");
        }

        if (!Enum.TryParse<PartyLifecycleStatus>(GetCell(cells, headerMap, "LifecycleStatus"), ignoreCase: true, out var lifecycleStatus))
        {
            messages.Add("LifecycleStatus is invalid.");
        }

        var roles = ParseRoles(GetCell(cells, headerMap, "Roles"), messages);
        var contactPoints = ParseContactPoints(GetCell(cells, headerMap, "ContactPoints"), messages);
        var addresses = ParseAddresses(GetCell(cells, headerMap, "Addresses"), messages);
        if (messages.Count > 0)
        {
            return null;
        }

        return new PartyEditorModel
        {
            DisplayName = displayName,
            PartyType = partyType,
            LifecycleStatus = lifecycleStatus,
            ExternalCode = GetCell(cells, headerMap, "ExternalCode").Trim(),
            LegalName = GetCell(cells, headerMap, "LegalName").Trim(),
            PreferredName = GetCell(cells, headerMap, "PreferredName").Trim(),
            Summary = GetCell(cells, headerMap, "Summary").Trim(),
            Tags = GetCell(cells, headerMap, "Tags")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList(),
            Region = GetCell(cells, headerMap, "Region").Trim(),
            CountryCode = GetCell(cells, headerMap, "CountryCode").Trim(),
            TimeZone = GetCell(cells, headerMap, "TimeZone").Trim(),
            IsSensitive = bool.TryParse(GetCell(cells, headerMap, "IsSensitive"), out var isSensitive) && isSensitive,
            Roles = roles,
            ContactPoints = contactPoints,
            Addresses = addresses,
            ExtendedDataJson = "{}",
            LastChangedBy = "crm-hr-import"
        };
    }

    private static List<PartyRoleAssignmentEditorModel> ParseRoles(string rawValue, List<string> messages)
    {
        var roles = new List<PartyRoleAssignmentEditorModel>();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return roles;
        }

        foreach (var segment in rawValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = segment.Split('|');
            if (parts.Length < 1 || !Enum.TryParse<PartyRoleKind>(parts[0], ignoreCase: true, out var roleKind))
            {
                messages.Add($"Role segment '{segment}' is invalid.");
                continue;
            }

            roles.Add(new PartyRoleAssignmentEditorModel
            {
                RoleKind = roleKind,
                Title = parts.Length > 1 ? parts[1].Trim() : roleKind.ToString(),
                IsPrimary = parts.Length > 2 && bool.TryParse(parts[2], out var isPrimary) && isPrimary
            });
        }

        return roles;
    }

    private static List<PartyContactPointEditorModel> ParseContactPoints(string rawValue, List<string> messages)
    {
        var contactPoints = new List<PartyContactPointEditorModel>();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return contactPoints;
        }

        foreach (var segment in rawValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = segment.Split('|');
            if (parts.Length < 3 || !Enum.TryParse<PartyContactType>(parts[0], ignoreCase: true, out var contactType))
            {
                messages.Add($"Contact point segment '{segment}' is invalid.");
                continue;
            }

            var value = parts[2].Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                messages.Add($"Contact point segment '{segment}' is missing a value.");
                continue;
            }

            contactPoints.Add(new PartyContactPointEditorModel
            {
                ContactType = contactType,
                Label = parts[1].Trim(),
                Value = value,
                NormalizedValue = NormalizeContactValue(contactType, value),
                IsPrimary = parts.Length > 3 && bool.TryParse(parts[3], out var isPrimary) && isPrimary,
                IsPublic = parts.Length <= 4 || !bool.TryParse(parts[4], out var isPublic) || isPublic
            });
        }

        return contactPoints;
    }

    private static List<PartyAddressEditorModel> ParseAddresses(string rawValue, List<string> messages)
    {
        var addresses = new List<PartyAddressEditorModel>();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return addresses;
        }

        foreach (var segment in rawValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = segment.Split('|');
            if (parts.Length < 6)
            {
                messages.Add($"Address segment '{segment}' is invalid.");
                continue;
            }

            var line1 = parts[1].Trim();
            if (string.IsNullOrWhiteSpace(line1))
            {
                messages.Add($"Address segment '{segment}' is missing line 1.");
                continue;
            }

            addresses.Add(new PartyAddressEditorModel
            {
                AddressType = parts[0].Trim(),
                Line1 = line1,
                Line2 = parts[2].Trim(),
                City = parts[3].Trim(),
                Region = parts[4].Trim(),
                PostalCode = parts[5].Trim(),
                CountryCode = parts.Length > 6 ? parts[6].Trim() : string.Empty,
                IsPrimary = parts.Length > 7 && bool.TryParse(parts[7], out var isPrimary) && isPrimary
            });
        }

        return addresses;
    }

    private static string SerializeRoleAssignments(IReadOnlyList<PartyRoleAssignment> roles)
    {
        return string.Join(';', roles.Select(item => $"{item.RoleKind}|{item.Title}|{item.IsPrimary}"));
    }

    private static string SerializeContactPoints(IReadOnlyList<PartyContactPoint> contactPoints)
    {
        return string.Join(';', contactPoints.Select(item => $"{item.ContactType}|{item.Label}|{item.Value}|{item.IsPrimary}|{item.IsPublic}"));
    }

    private static string SerializeAddresses(IReadOnlyList<PartyAddress> addresses)
    {
        return string.Join(';', addresses.Select(item => $"{item.AddressType}|{item.Line1}|{item.Line2}|{item.City}|{item.Region}|{item.PostalCode}|{item.CountryCode}|{item.IsPrimary}"));
    }

    private static List<IReadOnlyList<string>> ParseCsv(string csvContent)
    {
        var rows = new List<IReadOnlyList<string>>();
        using var reader = new StringReader(csvContent.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'));
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            rows.Add(ParseCsvLine(line));
        }

        return rows;
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var cells = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    builder.Append('"');
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (character == ',' && !inQuotes)
            {
                cells.Add(builder.ToString());
                builder.Clear();
                continue;
            }

            builder.Append(character);
        }

        cells.Add(builder.ToString());
        return cells;
    }

    private static IReadOnlyDictionary<string, int> BuildHeaderMap(IReadOnlyList<string> headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headerRow.Count; index++)
        {
            var header = headerRow[index].Trim();
            if (!string.IsNullOrWhiteSpace(header))
            {
                map[header] = index;
            }
        }

        return map;
    }

    private static string GetCell(IReadOnlyList<string> cells, IReadOnlyDictionary<string, int> headerMap, string columnName)
    {
        return headerMap.TryGetValue(columnName, out var columnIndex) && columnIndex < cells.Count
            ? cells[columnIndex]
            : string.Empty;
    }

    private static void AppendCsvRow(StringBuilder builder, params string?[] values)
    {
        builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
    }

    private static string EscapeCsv(string? value)
    {
        var resolvedValue = value ?? string.Empty;
        if (!resolvedValue.Contains(',') && !resolvedValue.Contains('"') && !resolvedValue.Contains('\n'))
        {
            return resolvedValue;
        }

        return $"\"{resolvedValue.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string NormalizeName(string value)
    {
        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static string NormalizeContactValue(PartyContactType contactType, string value)
    {
        var trimmedValue = value.Trim();
        return contactType switch
        {
            PartyContactType.Email or PartyContactType.Website or PartyContactType.Messaging or PartyContactType.Social => trimmedValue.ToLowerInvariant(),
            PartyContactType.Phone => new string(trimmedValue.Where(character => char.IsDigit(character) || character == '+').ToArray()),
            _ => trimmedValue.ToLowerInvariant()
        };
    }

    private static string ResolveActor(string actor)
    {
        return string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim();
    }

    private static string PreferExisting(string currentValue, string incomingValue)
    {
        return string.IsNullOrWhiteSpace(currentValue) ? incomingValue.Trim() : currentValue.Trim();
    }

    private static string CombineText(string currentValue, string incomingValue)
    {
        if (string.IsNullOrWhiteSpace(incomingValue))
        {
            return currentValue.Trim();
        }

        if (string.IsNullOrWhiteSpace(currentValue))
        {
            return incomingValue.Trim();
        }

        if (currentValue.Contains(incomingValue, StringComparison.Ordinal))
        {
            return currentValue.Trim();
        }

        return $"{currentValue.Trim()}{Environment.NewLine}{Environment.NewLine}{incomingValue.Trim()}";
    }

    private static string BuildRelationshipKey(Guid sourcePartyId, Guid targetPartyId, PartyRelationshipKind relationshipKind)
    {
        return $"{sourcePartyId:N}|{targetPartyId:N}|{relationshipKind}";
    }

    private static string BuildRoleAssignmentKey(PartyRoleAssignment roleAssignment)
    {
        return $"{roleAssignment.RoleKind}|{NormalizeName(roleAssignment.Title)}";
    }

    private static string BuildContactPointKey(PartyContactPoint contactPoint)
    {
        return $"{contactPoint.ContactType}|{contactPoint.NormalizedValue}";
    }

    private static string BuildAddressKey(PartyAddress address)
    {
        return $"{NormalizeName(address.AddressType)}|{NormalizeName(address.Line1)}|{NormalizeName(address.City)}|{NormalizeName(address.Region)}|{NormalizeName(address.PostalCode)}|{NormalizeName(address.CountryCode)}";
    }

    private static DateTimeOffset? MinDate(DateTimeOffset? left, DateTimeOffset? right)
    {
        return left switch
        {
            null => right,
            _ when right is null => left,
            _ => left <= right ? left : right
        };
    }

    private static DateTimeOffset? MaxDate(DateTimeOffset? left, DateTimeOffset? right)
    {
        return left switch
        {
            null => right,
            _ when right is null => left,
            _ => left >= right ? left : right
        };
    }

    private static void AddIfNotBlank(ICollection<string> target, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target.Add(value);
        }
    }

    private static List<string> DeserializeTags(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<string> DeserializeStringArray(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
