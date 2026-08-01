using CanDoItAll.Memory.SourceGateway;
using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MafMemorySourceKind = CanDoItAll.Memory.SourceGateway.MemorySourceKind;

namespace CanDoItAll.Modules.CrmHr;

public sealed partial class CrmHrSourceSnapshotProvider(
    IDbContextFactory<AppDbContext> dbContextFactory) : ICrmHrSourceSnapshotProvider
{
    public async Task<MemorySourceSnapshot> ReadSnapshotAsync(
        CrmHrSourceSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PartyId == Guid.Empty)
        {
            throw new ArgumentException("CRM/HR source requests must use null for directory scope or a non-empty party id.", nameof(request));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var scopeId = request.PartyId ?? Guid.Empty;
        var sources = new[]
        {
            CreateSimpleSource(
                MemorySourceEntityKind.CrmAccountProfile,
                FilterAccountProfiles(dbContext.Set<CrmAccountProfile>().AsNoTracking(), request.PartyId),
                profile => profile.Id,
                "account-profile",
                scopeId,
                MapAccountProfile),
            CreateInteractionSource(dbContext, request.PartyId, scopeId),
            CreateSimpleSource(
                MemorySourceEntityKind.CrmOpportunity,
                FilterOpportunities(dbContext, request.PartyId),
                opportunity => opportunity.Id,
                "opportunity",
                scopeId,
                MapOpportunity),
            CreatePartySource(dbContext, request.PartyId, scopeId),
            CreateSimpleSource(
                MemorySourceEntityKind.HrWorkforceProfile,
                FilterWorkforceProfiles(dbContext.Set<WorkforceProfile>().AsNoTracking(), request.PartyId),
                profile => profile.Id,
                "workforce",
                scopeId,
                MapWorkforce)
        };
        var page = await ReadPageAsync(
            sources,
            request.Cursor,
            request.Take,
            scopeId,
            cancellationToken);

        return new MemorySourceSnapshot(
            new MemorySourceSnapshotManifest(
                MemorySourceSnapshotId.Create(MafMemorySourceKind.CrmHr, scopeId, page.SnapshotHash),
                MafMemorySourceKind.CrmHr,
                scopeId,
                DateTimeOffset.UtcNow,
                page.TotalItemCount,
                page.NextCursor,
                page.HasMore,
                page.HasMore ? MemorySourceSnapshotPageStatus.PageReturned : MemorySourceSnapshotPageStatus.EndOfSource,
                MemorySourceSnapshotHashScope.PageScoped,
                MemorySourceSnapshotProviderVersions.CrmHr),
            page.Items);
    }

    private static async Task<MemorySourcePageSlice> ReadPageAsync(
        IReadOnlyList<CrmHrSourcePage> sources,
        MemorySourceSnapshotCursor? cursor,
        int? take,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        var descriptor = MemorySourceSnapshotCursor.ReadDescriptorOrThrow(
            cursor,
            MafMemorySourceKind.CrmHr,
            scopeId,
            MemorySourceSnapshotProviderVersions.CrmHr);
        var sourceCounts = new List<CrmHrSourcePageCount>(sources.Count);
        foreach (var source in sources)
        {
            sourceCounts.Add(new CrmHrSourcePageCount(
                source,
                await source.CountAsync(cancellationToken)));
        }

        var totalItemCount = sourceCounts.Sum(item => item.Count);
        var startPosition = descriptor?.Position ?? 0;
        if (descriptor is not null)
        {
            var anchor = await ReadItemIdAtPositionAsync(
                sourceCounts,
                descriptor.Position - 1,
                cancellationToken);
            if (anchor is null || anchor.Value != descriptor.LastItemId)
            {
                MemorySourceSnapshotCursor.ThrowStaleAnchor(
                    cursor!.Value,
                    MafMemorySourceKind.CrmHr,
                    scopeId,
                    MemorySourceSnapshotProviderVersions.CrmHr,
                    "CRM/HR source cursor anchor is stale or no longer matches the ordered source item at the recorded position.");
            }
        }

        var pageSize = MemorySourceSnapshotPage.NormalizeTake(take);
        var pageItems = new List<MemorySourceItem>(pageSize);
        var remainingSkip = startPosition;
        foreach (var sourceCount in sourceCounts)
        {
            if (pageItems.Count == pageSize)
            {
                break;
            }

            if (remainingSkip >= sourceCount.Count)
            {
                remainingSkip -= sourceCount.Count;
                continue;
            }

            var sourceSkip = remainingSkip;
            remainingSkip = 0;
            var sourceTake = Math.Min(
                pageSize - pageItems.Count,
                sourceCount.Count - sourceSkip);
            if (sourceTake <= 0)
            {
                continue;
            }

            pageItems.AddRange(await sourceCount.Source.ReadPageAsync(
                sourceSkip,
                sourceTake,
                cancellationToken));
        }

        var hasMore = startPosition + pageItems.Count < totalItemCount;
        MemorySourceSnapshotCursor? nextCursor = hasMore && pageItems.Count > 0
            ? MemorySourceSnapshotCursor.Create(
                MafMemorySourceKind.CrmHr,
                scopeId,
                MemorySourceSnapshotProviderVersions.CrmHr,
                startPosition + pageItems.Count,
                pageItems[^1].Id)
            : null;
        var snapshotHash = MemorySourceSnapshotHasher.Compute(
            MemorySourceSnapshotProviderVersions.CrmHr,
            scopeId.ToString("D"),
            startPosition.ToString(CultureInfo.InvariantCulture),
            string.Join("|", pageItems.Select(item => item.ContentHash)));
        return new MemorySourcePageSlice(
            pageItems,
            totalItemCount,
            nextCursor,
            hasMore,
            snapshotHash);
    }

    private static async Task<MemorySourceItemId?> ReadItemIdAtPositionAsync(
        IReadOnlyList<CrmHrSourcePageCount> sourceCounts,
        int position,
        CancellationToken cancellationToken)
    {
        if (position < 0)
        {
            return null;
        }

        var remaining = position;
        foreach (var sourceCount in sourceCounts)
        {
            if (remaining >= sourceCount.Count)
            {
                remaining -= sourceCount.Count;
                continue;
            }

            return await sourceCount.Source.ReadItemIdAsync(remaining, cancellationToken);
        }

        return null;
    }

    private static CrmHrSourcePage CreatePartySource(
        AppDbContext dbContext,
        Guid? partyId,
        Guid scopeId)
    {
        var query = FilterParties(dbContext.Set<Party>().AsNoTracking(), partyId);
        return new CrmHrSourcePage(
            MemorySourceEntityKind.CrmParty,
            cancellationToken => query.CountAsync(cancellationToken),
            (skip, take, cancellationToken) => ReadPartyPageAsync(
                dbContext,
                query,
                skip,
                take,
                scopeId,
                cancellationToken),
            (index, cancellationToken) => ReadSourceItemIdAsync(
                query,
                party => party.Id,
                index,
                MemorySourceEntityKind.CrmParty,
                "party",
                scopeId,
                cancellationToken));
    }

    private static async Task<IReadOnlyList<MemorySourceItem>> ReadPartyPageAsync(
        AppDbContext dbContext,
        IQueryable<Party> query,
        int skip,
        int take,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        var parties = await query
            .OrderBy(party => party.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        if (parties.Count == 0)
        {
            return [];
        }

        var partyIds = parties.Select(party => party.Id).ToArray();
        var roles = await dbContext.Set<PartyRoleAssignment>()
            .AsNoTracking()
            .Where(role => partyIds.Contains(role.PartyId))
            .OrderBy(role => role.RoleKind)
            .ThenBy(role => role.Title)
            .ThenBy(role => role.Id)
            .ToListAsync(cancellationToken);
        var contacts = await dbContext.Set<PartyContactPoint>()
            .AsNoTracking()
            .Where(contact => partyIds.Contains(contact.PartyId))
            .OrderByDescending(contact => contact.IsPrimary)
            .ThenBy(contact => contact.ContactType)
            .ThenBy(contact => contact.Label)
            .ThenBy(contact => contact.Id)
            .ToListAsync(cancellationToken);
        var confidentialNotes = await dbContext.Set<PartyConfidentialNote>()
            .AsNoTracking()
            .Where(note => partyIds.Contains(note.PartyId))
            .OrderBy(note => note.Category)
            .ThenBy(note => note.Id)
            .ToListAsync(cancellationToken);
        var rolesByPartyId = roles
            .GroupBy(role => role.PartyId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PartyRoleAssignment>)group.ToArray());
        var contactsByPartyId = contacts
            .GroupBy(contact => contact.PartyId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PartyContactPoint>)group.ToArray());
        var notesByPartyId = confidentialNotes
            .GroupBy(note => note.PartyId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PartyConfidentialNote>)group.ToArray());

        return parties
            .Select(party => MapParty(
                party,
                rolesByPartyId.GetValueOrDefault(party.Id) ?? [],
                contactsByPartyId.GetValueOrDefault(party.Id) ?? [],
                notesByPartyId.GetValueOrDefault(party.Id) ?? [],
                scopeId))
            .ToArray();
    }

    private static CrmHrSourcePage CreateInteractionSource(
        AppDbContext dbContext,
        Guid? partyId,
        Guid scopeId)
    {
        var query = FilterInteractions(dbContext, partyId);
        return new CrmHrSourcePage(
            MemorySourceEntityKind.CrmInteraction,
            cancellationToken => query.CountAsync(cancellationToken),
            (skip, take, cancellationToken) => ReadInteractionPageAsync(
                dbContext,
                query,
                skip,
                take,
                scopeId,
                cancellationToken),
            (index, cancellationToken) => ReadSourceItemIdAsync(
                query,
                interaction => interaction.Id,
                index,
                MemorySourceEntityKind.CrmInteraction,
                "interaction",
                scopeId,
                cancellationToken));
    }

    private static async Task<IReadOnlyList<MemorySourceItem>> ReadInteractionPageAsync(
        AppDbContext dbContext,
        IQueryable<InteractionRecord> query,
        int skip,
        int take,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        var interactions = await query
            .OrderBy(interaction => interaction.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        var interactionIds = interactions.Select(interaction => interaction.Id).ToList();
        var accountIdsByInteractionId = interactionIds.Count == 0
            ? new Dictionary<Guid, Guid>()
            : (await dbContext.Set<InteractionPartyLink>()
                .AsNoTracking()
                .Where(link =>
                    interactionIds.Contains(link.InteractionId) &&
                    link.Role == InteractionPartyRole.Account)
                .OrderBy(link => link.PartyId)
                .Select(link => new { link.InteractionId, link.PartyId })
                .ToListAsync(cancellationToken))
                .GroupBy(link => link.InteractionId)
                .Select(group => new
                {
                    InteractionId = group.Key,
                    AccountPartyIds = group
                        .Select(link => link.PartyId)
                        .Distinct()
                        .Take(2)
                        .ToArray()
                })
                .Where(group => group.AccountPartyIds.Length == 1)
                .ToDictionary(group => group.InteractionId, group => group.AccountPartyIds[0]);
        return interactions
            .Select(interaction => MapInteraction(
                interaction,
                accountIdsByInteractionId.GetValueOrDefault(interaction.Id),
                scopeId))
            .ToArray();
    }

    private static CrmHrSourcePage CreateSimpleSource<TEntity>(
        MemorySourceEntityKind entityKind,
        IQueryable<TEntity> query,
        Expression<Func<TEntity, Guid>> orderKey,
        string sourceEntityPrefix,
        Guid scopeId,
        Func<TEntity, Guid, MemorySourceItem> map)
        where TEntity : class
        => new(
            entityKind,
            cancellationToken => query.CountAsync(cancellationToken),
            async (skip, take, cancellationToken) => (await query
                    .OrderBy(orderKey)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync(cancellationToken))
                .Select(item => map(item, scopeId))
                .ToArray(),
            (index, cancellationToken) => ReadSourceItemIdAsync(
                query,
                orderKey,
                index,
                entityKind,
                sourceEntityPrefix,
                scopeId,
                cancellationToken));

    private static async Task<MemorySourceItemId?> ReadSourceItemIdAsync<TEntity>(
        IQueryable<TEntity> query,
        Expression<Func<TEntity, Guid>> orderKey,
        int index,
        MemorySourceEntityKind entityKind,
        string sourceEntityPrefix,
        Guid scopeId,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var id = await query
            .OrderBy(orderKey)
            .Skip(index)
            .Select(orderKey)
            .Select(value => (Guid?)value)
            .FirstOrDefaultAsync(cancellationToken);
        return id.HasValue
            ? MemorySourceItemId.Create(
                MafMemorySourceKind.CrmHr,
                scopeId,
                entityKind,
                $"{sourceEntityPrefix}:{id.Value:D}")
            : null;
    }

    private static IQueryable<CrmAccountProfile> FilterAccountProfiles(
        IQueryable<CrmAccountProfile> query,
        Guid? partyId)
        => partyId.HasValue
            ? query.Where(profile => profile.AccountPartyId == partyId.Value)
            : query;

    private static IQueryable<Opportunity> FilterOpportunities(
        AppDbContext dbContext,
        Guid? partyId)
    {
        var query = dbContext.Set<Opportunity>().AsNoTracking();
        return partyId.HasValue
            ? query.Where(opportunity =>
                opportunity.AccountPartyId == partyId.Value ||
                opportunity.OwnerPartyId == partyId.Value ||
                opportunity.DeliveryUnitPartyId == partyId.Value ||
                dbContext.Set<OpportunityPartyLink>().Any(link =>
                    link.PartyId == partyId.Value &&
                    link.OpportunityId == opportunity.Id))
            : query;
    }

    private static IQueryable<InteractionRecord> FilterInteractions(
        AppDbContext dbContext,
        Guid? partyId)
    {
        var query = dbContext.Set<InteractionRecord>().AsNoTracking();
        return partyId.HasValue
            ? query.Where(interaction =>
                dbContext.Set<InteractionPartyLink>().Any(link =>
                    link.PartyId == partyId.Value &&
                    link.InteractionId == interaction.Id))
            : query;
    }

    private static IQueryable<Party> FilterParties(IQueryable<Party> query, Guid? partyId)
        => partyId.HasValue ? query.Where(party => party.Id == partyId.Value) : query;

    private static IQueryable<WorkforceProfile> FilterWorkforceProfiles(
        IQueryable<WorkforceProfile> query,
        Guid? partyId)
        => partyId.HasValue
            ? query.Where(profile => profile.PartyId == partyId.Value)
            : query;

    private static MemorySourceItem MapParty(
        Party party,
        IReadOnlyList<PartyRoleAssignment> roles,
        IReadOnlyList<PartyContactPoint> contacts,
        IReadOnlyList<PartyConfidentialNote> confidentialNotes,
        Guid scopeId)
    {
        var hasSensitivePayload = party.IsSensitive ||
            confidentialNotes.Count > 0 ||
            contacts.Any(contact => !contact.IsPublic);
        var normalizedPartyTags = NormalizeTags(
            party.TagsJson,
            $"Party '{party.Id:D}'");
        var contactProjections = contacts
            .Select(contact => ProjectContact(contact, party.IsSensitive))
            .ToArray();
        var content = BuildContent(
            ("Display name", party.DisplayName),
            ("Party type", party.PartyType.ToString()),
            ("Lifecycle status", party.LifecycleStatus.ToString()),
            ("External code", RedactWhenSensitive(party.ExternalCode, party.IsSensitive)),
            ("Summary", RedactWhenSensitive(party.Summary, party.IsSensitive)),
            ("Notes", RedactWhenSensitive(party.Notes, hasSensitivePayload)),
            ("Tags", FormatTags(normalizedPartyTags, party.IsSensitive)),
            ("Region", party.Region),
            ("Country", party.CountryCode),
            ("Roles", string.Join(", ", roles.Select(role => $"{role.RoleKind}:{role.Title}"))),
            ("Contacts", string.Join(", ", contactProjections.Select(contact => contact.Content))),
            ("Confidential note count", confidentialNotes.Count.ToString(CultureInfo.InvariantCulture)));
        return CreateItem(
            scopeId,
            MemorySourceEntityKind.CrmParty,
            $"party:{party.Id:D}",
            party.DisplayName,
            content,
            MemorySourceSnapshotHasher.Compute(
                party.Id.ToString("D"),
                party.DisplayName,
                party.LegalName,
                party.PreferredName,
                party.ExternalCode,
                party.Summary,
                party.Notes,
                string.Join(",", normalizedPartyTags),
                string.Join(
                    "|",
                    roles.Select(role =>
                        $"{role.Id:D}:{role.RoleKind}:{role.Title}:{role.IsPrimary}:{role.Notes}")),
                string.Join("|", contactProjections.Select(contact => contact.IntegrityValue)),
                string.Join(
                    "|",
                    confidentialNotes.Select(note =>
                        $"{note.Id:D}:{note.Category}:{note.NoteText}"))),
            party.CreatedAtUtc,
            party.UpdatedAtUtc,
            hasSensitivePayload,
            $"/crm-hr/directory?partyId={party.Id:D}",
            [
                new("party", party.Id.ToString("D"), 0)
            ],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["partyType"] = party.PartyType.ToString(),
                ["lifecycleStatus"] = party.LifecycleStatus.ToString(),
                ["isSensitive"] = party.IsSensitive.ToString(CultureInfo.InvariantCulture)
            });
    }

    private static MemorySourceItem MapAccountProfile(CrmAccountProfile profile, Guid scopeId)
    {
        var content = BuildContent(
            ("Relationship stage", profile.RelationshipStage.ToString()),
            ("Commercial notes", RedactText(profile.CommercialNotes)),
            ("Constraint notes", RedactText(profile.ConstraintNotes)),
            ("Timing risk notes", RedactText(profile.TimingRiskNotes)));
        return CreateItem(
            scopeId,
            MemorySourceEntityKind.CrmAccountProfile,
            $"account-profile:{profile.Id:D}",
            $"Account profile {profile.RelationshipStage}",
            content,
            MemorySourceSnapshotHasher.Compute(
                profile.Id.ToString("D"),
                profile.AccountPartyId.ToString("D"),
                profile.CommercialNotes,
                profile.ConstraintNotes,
                profile.TimingRiskNotes),
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc,
            hasSensitivePayload: true,
            $"/crm-hr/crm?accountId={profile.AccountPartyId:D}",
            [new("party", profile.AccountPartyId.ToString("D"), 0)],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["relationshipStage"] = profile.RelationshipStage.ToString()
            });
    }

    private static MemorySourceItem MapOpportunity(Opportunity opportunity, Guid scopeId)
    {
        var content = BuildContent(
            ("Title", opportunity.Title),
            ("Stage", opportunity.Stage.ToString()),
            ("Source", opportunity.OpportunitySource.ToString()),
            ("Amount", opportunity.Amount?.ToString(CultureInfo.InvariantCulture)),
            ("Probability", opportunity.ProbabilityPercent.ToString(CultureInfo.InvariantCulture)),
            ("Expected close", opportunity.ExpectedCloseDateUtc?.ToString("O")),
            ("Summary", RedactText(opportunity.Summary)),
            ("Notes", RedactText(opportunity.Notes)),
            ("Lost reason", RedactText(opportunity.LostReason)));
        return CreateItem(
            scopeId,
            MemorySourceEntityKind.CrmOpportunity,
            $"opportunity:{opportunity.Id:D}",
            opportunity.Title,
            content,
            MemorySourceSnapshotHasher.Compute(
                opportunity.Id.ToString("D"),
                opportunity.Title,
                opportunity.Stage.ToString(),
                opportunity.Summary,
                opportunity.Notes,
                opportunity.LostReason,
                opportunity.ExtendedDataJson),
            opportunity.CreatedAtUtc,
            opportunity.UpdatedAtUtc,
            hasSensitivePayload: true,
            $"/crm-hr/crm?accountId={opportunity.AccountPartyId:D}&opportunityId={opportunity.Id:D}",
            [
                new("opportunity", opportunity.Id.ToString("D"), 0),
                new("account-party", opportunity.AccountPartyId.ToString("D"), 1)
            ],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["stage"] = opportunity.Stage.ToString(),
                ["opportunitySource"] = opportunity.OpportunitySource.ToString()
            });
    }

    private static MemorySourceItem MapInteraction(
        InteractionRecord interaction,
        Guid accountPartyId,
        Guid scopeId)
    {
        var content = BuildContent(
            ("Subject", interaction.Subject),
            ("Interaction type", interaction.InteractionType.ToString()),
            ("Occurred", interaction.OccurredAtUtc.ToString("O")),
            ("Summary", RedactText(interaction.Summary)),
            ("Notes", RedactText(interaction.Notes)),
            ("Next action", RedactText(interaction.NextActionText)));
        var route = accountPartyId == Guid.Empty
            ? $"/crm-hr/crm?interactionId={interaction.Id:D}"
            : $"/crm-hr/crm?accountId={accountPartyId:D}&interactionId={interaction.Id:D}";
        IReadOnlyList<MemorySourceReference> references = accountPartyId == Guid.Empty
            ? [new("interaction", interaction.Id.ToString("D"), 0)]
            :
            [
                new("interaction", interaction.Id.ToString("D"), 0),
                new("account-party", accountPartyId.ToString("D"), 1)
            ];
        return CreateItem(
            scopeId,
            MemorySourceEntityKind.CrmInteraction,
            $"interaction:{interaction.Id:D}",
            interaction.Subject,
            content,
            MemorySourceSnapshotHasher.Compute(
                interaction.Id.ToString("D"),
                interaction.Subject,
                interaction.Summary,
                interaction.Notes,
                interaction.NextActionText),
            interaction.CreatedAtUtc,
            interaction.UpdatedAtUtc,
            hasSensitivePayload: true,
            route,
            references,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["interactionType"] = interaction.InteractionType.ToString()
            });
    }

    private static MemorySourceItem MapWorkforce(WorkforceProfile profile, Guid scopeId)
    {
        var content = BuildContent(
            ("Workforce kind", profile.WorkforceKind.ToString()),
            ("Job title", profile.JobTitle),
            ("Discipline", profile.Discipline),
            ("Seniority", profile.Seniority),
            ("Status", profile.Status),
            ("Location", profile.Location),
            ("Capacity hours", profile.CapacityHoursPerWeek.ToString(CultureInfo.InvariantCulture)),
            ("Notes", RedactText(profile.Notes)));
        return CreateItem(
            scopeId,
            MemorySourceEntityKind.HrWorkforceProfile,
            $"workforce:{profile.Id:D}",
            $"Workforce profile {profile.JobTitle}",
            content,
            MemorySourceSnapshotHasher.Compute(
                profile.Id.ToString("D"),
                profile.PartyId.ToString("D"),
                profile.EmployeeCode,
                profile.JobTitle,
                profile.Discipline,
                profile.InternalCostRate?.ToString(CultureInfo.InvariantCulture),
                profile.ExternalBillingRate?.ToString(CultureInfo.InvariantCulture),
                profile.Notes),
            profile.StartDateUtc,
            profile.EndDateUtc,
            hasSensitivePayload: true,
            $"/crm-hr/workforce?partyId={profile.PartyId:D}",
            [new("party", profile.PartyId.ToString("D"), 0)],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["workforceKind"] = profile.WorkforceKind.ToString(),
                ["status"] = profile.Status
            });
    }

    private static MemorySourceItem CreateItem(
        Guid scopeId,
        MemorySourceEntityKind entityKind,
        string sourceEntityId,
        string title,
        string content,
        string contentHash,
        DateTimeOffset? createdAtUtc,
        DateTimeOffset? updatedAtUtc,
        bool hasSensitivePayload,
        string sourceRoute,
        IReadOnlyList<MemorySourceReference> references,
        IReadOnlyDictionary<string, string> metadata)
    {
        var itemId = MemorySourceItemId.Create(
            MafMemorySourceKind.CrmHr,
            scopeId,
            entityKind,
            sourceEntityId);
        return new MemorySourceItem(
            itemId,
            MafMemorySourceKind.CrmHr,
            entityKind,
            title,
            content,
            contentHash,
            createdAtUtc,
            updatedAtUtc,
            new MemorySourceProvenance(
                MafMemorySourceKind.CrmHr,
                scopeId,
                entityKind,
                sourceEntityId,
                sourceRoute),
            MemorySourceSnapshotSecurity.CreatePermission(
                hasSensitivePayload,
                "CRM/HR source snapshots redact sensitive contacts, notes, commercial details, and HR payloads before provider delivery.",
                "Source-grounded CRM/HR evidence for selected memory provider ingestion."),
            Layout: null,
            Links: [],
            References: references,
            StorageReference: null,
            Metadata: metadata)
        {
            HashPolicy = MemorySourceSnapshotSecurity.CreateIntegrityHashPolicy(
                hasSensitivePayload,
                "CRM/HR snapshot hashes may include raw sensitive source fields and are for non-exportable integrity checks only.")
        };
    }

    private static ContactSourceProjection ProjectContact(
        PartyContactPoint contact,
        bool partySensitive)
    {
        var isSensitive = partySensitive || !contact.IsPublic;
        var tags = NormalizeTags(
            contact.TagsJson,
            $"Party contact '{contact.Id:D}'");
        var value = isSensitive
            ? MemorySourceSnapshotSecurity.RedactedValue
            : RedactText(contact.Value);
        var content = $"{contact.ContactType}:{contact.Label}:{value}";
        var formattedTags = FormatTags(tags, isSensitive);
        if (!string.IsNullOrEmpty(formattedTags))
        {
            content = $"{content} [tags: {formattedTags}]";
        }

        return new ContactSourceProjection(
            content,
            string.Join(
                ":",
                contact.Id.ToString("D"),
                contact.ContactType,
                contact.Label,
                contact.Value,
                contact.NormalizedValue,
                contact.IsPrimary.ToString(CultureInfo.InvariantCulture),
                contact.IsPublic.ToString(CultureInfo.InvariantCulture),
                string.Join(",", tags),
                contact.Notes));
    }

    private static IReadOnlyList<string> NormalizeTags(
        string tagsJson,
        string owner)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
        {
            return [];
        }

        try
        {
            return (JsonSerializer.Deserialize<List<string>>(tagsJson) ?? [])
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray();
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"{owner} contains invalid tags JSON.",
                exception);
        }
    }

    private static string FormatTags(
        IReadOnlyList<string> tags,
        bool sensitive)
    {
        if (tags.Count == 0)
        {
            return string.Empty;
        }

        return sensitive
            ? MemorySourceSnapshotSecurity.RedactedValue
            : string.Join(", ", tags);
    }

    private static string RedactWhenSensitive(string value, bool sensitive)
        => MemorySourceSnapshotSecurity.RedactWhenSensitive(value, sensitive);

    private static string RedactText(string value)
        => MemorySourceSnapshotSecurity.RedactSensitiveInlineValues(value);

    private static string BuildContent(params (string Label, string? Value)[] fields)
        => string.Join(
            Environment.NewLine,
            fields
                .Where(field => !string.IsNullOrWhiteSpace(field.Value))
                .Select(field => $"{field.Label}: {field.Value}"));

    private sealed record MemorySourcePageSlice(
        IReadOnlyList<MemorySourceItem> Items,
        int TotalItemCount,
        MemorySourceSnapshotCursor? NextCursor,
        bool HasMore,
        string SnapshotHash);

    private sealed record CrmHrSourcePage(
        MemorySourceEntityKind EntityKind,
        Func<CancellationToken, Task<int>> CountAsync,
        Func<int, int, CancellationToken, Task<IReadOnlyList<MemorySourceItem>>> ReadPageAsync,
        Func<int, CancellationToken, Task<MemorySourceItemId?>> ReadItemIdAsync);

    private sealed record CrmHrSourcePageCount(
        CrmHrSourcePage Source,
        int Count);

    private readonly record struct ContactSourceProjection(
        string Content,
        string IntegrityValue);
}
