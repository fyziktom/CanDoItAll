using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

internal static class CrmHrSearchSourceTypes
{
    public const string Party = "crmhr-party";
    public const string Account = "crm-account";
    public const string Opportunity = "crm-opportunity";
    public const string Interaction = "crmhr-interaction";
    public const string Workforce = "crmhr-workforce";
    public const string AiAgent = "crmhr-ai-agent";
    public const string RecruitmentApplication = "crmhr-recruitment-application";
}

internal static class CrmHrAuditWriter
{
    public static void AddEntry(
        CrmHrDbContext dbContext,
        string entityType,
        Guid entityId,
        string action,
        string summary,
        object detail,
        string actor,
        bool isSensitive,
        DateTimeOffset createdAtUtc)
    {
        dbContext.Set<CrmHrAuditEntry>().Add(new CrmHrAuditEntry
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Summary = summary,
            DetailJson = JsonSerializer.Serialize(detail),
            Actor = actor,
            IsSensitive = isSensitive,
            CreatedAtUtc = createdAtUtc
        });
    }
}

public sealed partial class PartyDirectoryService
{
    public async Task<CrmActivityHistoryPage> SearchPartyActivityAsync(
        CrmActivityHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var interactions = dbContext.Set<InteractionRecord>()
            .AsNoTracking()
            .Where(interaction => dbContext.Set<InteractionPartyLink>().Any(link =>
                link.PartyId == query.PartyId &&
                link.InteractionId == interaction.Id));
        var auditEntries = dbContext.Set<CrmHrAuditEntry>()
            .AsNoTracking()
            .Where(item =>
                item.EntityId == query.PartyId ||
                (item.EntityType == nameof(RecruitmentApplication) &&
                 dbContext.Set<RecruitmentApplication>().Any(application =>
                     application.PartyId == query.PartyId &&
                     application.Id == item.EntityId)));

        return await CrmActivityHistoryQueryComposer.SearchAsync(
            dbContext,
            interactions,
            auditEntries,
            query,
            includeParticipantNames: false,
            clock.GetUtcNow(),
            cancellationToken);
    }

    public async Task<PartyProjectAssignmentPage> SearchPartyProjectAssignmentsAsync(
        PartyProjectAssignmentQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.PartyId == Guid.Empty)
        {
            throw new ArgumentException("A persisted party identifier is required.", nameof(query));
        }

        if (query.PageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Page index cannot be negative.");
        }

        if (query.PageSize is <= 0 or > PartyProjectAssignmentQueryLimits.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                $"Page size must be between 1 and {PartyProjectAssignmentQueryLimits.MaximumPageSize}.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var totalCount = await ProjectAssignmentReporting.Relational(dbContext)
            .CountAsync(assignment => assignment.PartyId == query.PartyId, cancellationToken);
        const string unknownProject = "Unknown project";
        var items = await dbContext.Database.SqlQueryRaw<PartyProjectAssignmentItemModel>("""
                SELECT assignment."Id", assignment."ProjectId",
                    COALESCE(project."Name", {0}) AS "ProjectName",
                    assignment."AssignmentKind", assignment."NodeKey", assignment."AllocationPercent",
                    (assignment."StartsAtUtc" AT TIME ZONE 'UTC')::date AS "StartsOn",
                    (assignment."EndsAtUtc" AT TIME ZONE 'UTC')::date AS "EndsOn",
                    assignment."IsPrimary", assignment."Notes"
                FROM (
                """ + ProjectAssignmentReporting.AllAssignmentsSql + """
                ) AS assignment
                LEFT JOIN "Projects_Projects" AS project ON assignment."ProjectId" = project."Id"
                    AND assignment."ProjectLifetimeId" = project."LifetimeId"
                WHERE assignment."PartyId" = {1}
                """, unknownProject, query.PartyId)
            .OrderBy(item => item.ProjectName)
            .ThenBy(item => item.AssignmentKind)
            .ThenBy(item => item.Id)
            .Skip(query.PageIndex * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);

        return new PartyProjectAssignmentPage(
            items,
            query.PageIndex,
            query.PageSize,
            totalCount);
    }

    private async Task UpsertPartySearchDocumentAsync(Guid partyId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(item => item.Id == partyId, cancellationToken);
        if (party is null)
        {
            await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.Party, partyId.ToString("N"), cancellationToken);
            return;
        }

        if (party.IsSensitive)
        {
            await DeleteRelatedSearchDocumentsAsync(dbContext, party.Id, cancellationToken);
            return;
        }

        var roles = await dbContext.Set<PartyRoleAssignment>()
            .Where(item => item.PartyId == partyId)
            .OrderBy(item => item.RoleKind)
            .Select(item => item.RoleKind)
            .ToListAsync(cancellationToken);
        var contacts = await dbContext.Set<PartyContactPoint>()
            .Where(item => item.PartyId == partyId && item.IsPublic)
            .OrderByDescending(item => item.IsPrimary)
            .ToListAsync(cancellationToken);

        var primaryEmail = contacts
            .Where(item => item.ContactType == PartyContactType.Email)
            .Select(item => item.Value)
            .FirstOrDefault();
        var primaryPhone = contacts
            .Where(item => item.ContactType == PartyContactType.Phone)
            .Select(item => item.Value)
            .FirstOrDefault();
        var tags = string.IsNullOrWhiteSpace(party.TagsJson)
            ? []
            : JsonSerializer.Deserialize<List<string>>(party.TagsJson) ?? [];

        await searchIndexService.UpsertAsync(
            new SearchDocumentInput(
                CrmHrSearchSourceTypes.Party,
                partyId.ToString("N"),
                "CRM / HR party",
                party.DisplayName,
                $"{party.PartyType} / {party.LifecycleStatus}",
                string.Join(
                    Environment.NewLine,
                    new[]
                    {
                        party.DisplayName,
                        party.ExternalCode,
                        party.Summary,
                        primaryEmail,
                        primaryPhone,
                        party.Region,
                        party.CountryCode,
                        party.TimeZone,
                        string.Join(", ", roles),
                        string.Join(", ", tags)
                    }.Where(item => !string.IsNullOrWhiteSpace(item))),
                $"/crm-hr/directory?partyId={partyId}"),
            cancellationToken);
    }

    private async Task DeleteRelatedSearchDocumentsAsync(
        CrmHrDbContext dbContext,
        Guid partyId,
        CancellationToken cancellationToken)
    {
        await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.Party, partyId.ToString("N"), cancellationToken);
        await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.Account, partyId.ToString("N"), cancellationToken);
        await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.Workforce, partyId.ToString("N"), cancellationToken);
        await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.AiAgent, partyId.ToString("N"), cancellationToken);

        var recruitmentIds = await dbContext.Set<RecruitmentApplication>()
            .Where(item => item.PartyId == partyId)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        foreach (var recruitmentId in recruitmentIds)
        {
            await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.RecruitmentApplication, recruitmentId.ToString("N"), cancellationToken);
        }

        var interactionIds = await dbContext.Set<InteractionPartyLink>()
            .Where(item => item.PartyId == partyId)
            .Select(item => item.InteractionId)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var interactionId in interactionIds)
        {
            await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.Interaction, interactionId.ToString("N"), cancellationToken);
        }

        var opportunityIds = await dbContext.Set<Opportunity>()
            .Where(item => item.AccountPartyId == partyId)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        foreach (var opportunityId in opportunityIds)
        {
            await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.Opportunity, opportunityId.ToString("N"), cancellationToken);
        }
    }
}

public sealed partial class CrmService
{
    private async Task UpsertInteractionSearchDocumentAsync(Guid interactionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var interaction = await dbContext.Set<InteractionRecord>()
            .SingleOrDefaultAsync(item => item.Id == interactionId, cancellationToken);
        if (interaction is null)
        {
            await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.Interaction, interactionId.ToString("N"), cancellationToken);
            return;
        }

        var accountLink = await dbContext.Set<InteractionPartyLink>()
            .Where(item => item.InteractionId == interactionId && item.Role == InteractionPartyRole.Account)
            .Select(item => item.PartyId)
            .FirstOrDefaultAsync(cancellationToken);
        if (accountLink == Guid.Empty)
        {
            await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.Interaction, interactionId.ToString("N"), cancellationToken);
            return;
        }

        var account = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(item => item.Id == accountLink, cancellationToken);
        if (account is null || account.IsSensitive)
        {
            await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.Interaction, interactionId.ToString("N"), cancellationToken);
            return;
        }

        await searchIndexService.UpsertAsync(
            new SearchDocumentInput(
                CrmHrSearchSourceTypes.Interaction,
                interactionId.ToString("N"),
                "CRM / HR interaction",
                interaction.Subject,
                $"{interaction.InteractionType} / {account.DisplayName}",
                string.Join(
                    Environment.NewLine,
                    new[]
                    {
                        account.DisplayName,
                        interaction.Subject,
                        interaction.Summary,
                        interaction.Notes,
                        interaction.NextActionText,
                        interaction.RelatedOpportunityId?.ToString()
                    }.Where(item => !string.IsNullOrWhiteSpace(item))),
                $"/crm-hr/crm?accountId={account.Id}"),
            cancellationToken);
    }
}
