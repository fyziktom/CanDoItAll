using System.Globalization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MafMemorySourceKind = CanDoItAll.AgentFramework.Core.MemorySourceKind;

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
        var items = new List<MemorySourceItem>();
        items.AddRange(await ReadPartyItemsAsync(dbContext, request.PartyId, scopeId, cancellationToken));
        items.AddRange(await ReadAccountProfileItemsAsync(dbContext, request.PartyId, scopeId, cancellationToken));
        items.AddRange(await ReadOpportunityItemsAsync(dbContext, request.PartyId, scopeId, cancellationToken));
        items.AddRange(await ReadInteractionItemsAsync(dbContext, request.PartyId, scopeId, cancellationToken));
        items.AddRange(await ReadWorkforceItemsAsync(dbContext, request.PartyId, scopeId, cancellationToken));

        var page = MemorySourceSnapshotPage.Apply(
            items,
            request.Cursor,
            request.Take,
            MafMemorySourceKind.CrmHr,
            scopeId,
            MemorySourceSnapshotProviderVersions.CrmHr,
            out var nextCursor,
            out var hasMore);
        var snapshotHash = MemorySourceSnapshotHasher.Compute(page.Select(item => item.ContentHash).ToArray());
        return new MemorySourceSnapshot(
            new MemorySourceSnapshotManifest(
                MemorySourceSnapshotId.Create(MafMemorySourceKind.CrmHr, scopeId, snapshotHash),
                MafMemorySourceKind.CrmHr,
                scopeId,
                DateTimeOffset.UtcNow,
                items.Count,
                nextCursor,
                hasMore,
                hasMore ? MemorySourceSnapshotPageStatus.PageReturned : MemorySourceSnapshotPageStatus.EndOfSource,
                MemorySourceSnapshotHashScope.FullSnapshot,
                MemorySourceSnapshotProviderVersions.CrmHr),
            page);
    }

    private static async Task<IReadOnlyList<MemorySourceItem>> ReadPartyItemsAsync(
        AppDbContext dbContext,
        Guid? partyId,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        var parties = await FilterParties(dbContext.Set<Party>().AsNoTracking(), partyId)
            .OrderBy(party => party.DisplayName)
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
            .ToListAsync(cancellationToken);
        var contacts = await dbContext.Set<PartyContactPoint>()
            .AsNoTracking()
            .Where(contact => partyIds.Contains(contact.PartyId))
            .OrderByDescending(contact => contact.IsPrimary)
            .ThenBy(contact => contact.ContactType)
            .ToListAsync(cancellationToken);
        var confidentialNotes = await dbContext.Set<PartyConfidentialNote>()
            .AsNoTracking()
            .Where(note => partyIds.Contains(note.PartyId))
            .ToListAsync(cancellationToken);

        return parties
            .Select(party => MapParty(
                party,
                roles.Where(role => role.PartyId == party.Id).ToArray(),
                contacts.Where(contact => contact.PartyId == party.Id).ToArray(),
                confidentialNotes.Where(note => note.PartyId == party.Id).ToArray(),
                scopeId))
            .ToArray();
    }

    private static async Task<IReadOnlyList<MemorySourceItem>> ReadAccountProfileItemsAsync(
        AppDbContext dbContext,
        Guid? partyId,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<CrmAccountProfile>().AsNoTracking();
        if (partyId.HasValue)
        {
            query = query.Where(profile => profile.AccountPartyId == partyId.Value);
        }

        var profiles = await query.OrderBy(profile => profile.AccountPartyId).ToListAsync(cancellationToken);
        return profiles.Select(profile => MapAccountProfile(profile, scopeId)).ToArray();
    }

    private static async Task<IReadOnlyList<MemorySourceItem>> ReadOpportunityItemsAsync(
        AppDbContext dbContext,
        Guid? partyId,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        var linkedOpportunityIds = partyId.HasValue
            ? await dbContext.Set<OpportunityPartyLink>()
                .AsNoTracking()
                .Where(link => link.PartyId == partyId.Value)
                .Select(link => link.OpportunityId)
                .ToListAsync(cancellationToken)
            : [];
        var query = dbContext.Set<Opportunity>().AsNoTracking();
        if (partyId.HasValue)
        {
            query = query.Where(opportunity =>
                opportunity.AccountPartyId == partyId.Value ||
                opportunity.OwnerPartyId == partyId.Value ||
                opportunity.DeliveryUnitPartyId == partyId.Value ||
                linkedOpportunityIds.Contains(opportunity.Id));
        }

        var opportunities = await query.OrderBy(opportunity => opportunity.Title).ToListAsync(cancellationToken);
        return opportunities.Select(opportunity => MapOpportunity(opportunity, scopeId)).ToArray();
    }

    private static async Task<IReadOnlyList<MemorySourceItem>> ReadInteractionItemsAsync(
        AppDbContext dbContext,
        Guid? partyId,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        var linkedInteractionIds = partyId.HasValue
            ? await dbContext.Set<InteractionPartyLink>()
                .AsNoTracking()
                .Where(link => link.PartyId == partyId.Value)
                .Select(link => link.InteractionId)
                .ToListAsync(cancellationToken)
            : [];
        var query = dbContext.Set<InteractionRecord>().AsNoTracking();
        if (partyId.HasValue)
        {
            query = query.Where(interaction => linkedInteractionIds.Contains(interaction.Id));
        }

        var interactions = await query.OrderByDescending(interaction => interaction.OccurredAtUtc).ToListAsync(cancellationToken);
        return interactions.Select(interaction => MapInteraction(interaction, scopeId)).ToArray();
    }

    private static async Task<IReadOnlyList<MemorySourceItem>> ReadWorkforceItemsAsync(
        AppDbContext dbContext,
        Guid? partyId,
        Guid scopeId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<WorkforceProfile>().AsNoTracking();
        if (partyId.HasValue)
        {
            query = query.Where(profile => profile.PartyId == partyId.Value);
        }

        var profiles = await query.OrderBy(profile => profile.PartyId).ToListAsync(cancellationToken);
        return profiles.Select(profile => MapWorkforce(profile, scopeId)).ToArray();
    }

    private static IQueryable<Party> FilterParties(IQueryable<Party> query, Guid? partyId)
        => partyId.HasValue ? query.Where(party => party.Id == partyId.Value) : query;

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
        var content = BuildContent(
            ("Display name", party.DisplayName),
            ("Party type", party.PartyType.ToString()),
            ("Lifecycle status", party.LifecycleStatus.ToString()),
            ("External code", RedactWhenSensitive(party.ExternalCode, party.IsSensitive)),
            ("Summary", RedactWhenSensitive(party.Summary, party.IsSensitive)),
            ("Notes", RedactWhenSensitive(party.Notes, hasSensitivePayload)),
            ("Tags", party.TagsJson),
            ("Region", party.Region),
            ("Country", party.CountryCode),
            ("Roles", string.Join(", ", roles.Select(role => $"{role.RoleKind}:{role.Title}"))),
            ("Contacts", string.Join(", ", contacts.Select(contact => FormatContact(contact, hasSensitivePayload)))),
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
                string.Join("|", contacts.Select(contact => contact.Value)),
                string.Join("|", confidentialNotes.Select(note => note.NoteText))),
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
            $"/crm-hr/crm?opportunityId={opportunity.Id:D}",
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

    private static MemorySourceItem MapInteraction(InteractionRecord interaction, Guid scopeId)
    {
        var content = BuildContent(
            ("Subject", interaction.Subject),
            ("Interaction type", interaction.InteractionType.ToString()),
            ("Occurred", interaction.OccurredAtUtc.ToString("O")),
            ("Summary", RedactText(interaction.Summary)),
            ("Notes", RedactText(interaction.Notes)),
            ("Next action", RedactText(interaction.NextActionText)));
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
            $"/crm-hr/crm?interactionId={interaction.Id:D}",
            [new("interaction", interaction.Id.ToString("D"), 0)],
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

    private static string FormatContact(PartyContactPoint contact, bool partySensitive)
    {
        var value = partySensitive || !contact.IsPublic
            ? MemorySourceSnapshotSecurity.RedactedValue
            : RedactText(contact.Value);
        return $"{contact.ContactType}:{contact.Label}:{value}";
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
}
