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
        AppDbContext dbContext,
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

public sealed record PartyProjectAssignmentItemModel(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    ProjectPartyAssignmentKind AssignmentKind,
    string NodeKey,
    decimal? AllocationPercent,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    bool IsPrimary,
    string Notes);

public sealed partial class PartyDirectoryService
{
    public async Task<IReadOnlyList<CrmAccountActivityTimelineItemModel>> ListPartyActivityTimelineAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var interactionIds = await dbContext.Set<InteractionPartyLink>()
            .Where(item => item.PartyId == partyId)
            .Select(item => item.InteractionId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var interactions = interactionIds.Count == 0
            ? []
            : await dbContext.Set<InteractionRecord>()
                .Where(item => interactionIds.Contains(item.Id))
                .ToListAsync(cancellationToken);

        var recruitmentApplicationIds = await dbContext.Set<RecruitmentApplication>()
            .Where(item => item.PartyId == partyId)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var auditEntries = await dbContext.Set<CrmHrAuditEntry>()
            .Where(item =>
                (item.EntityType == nameof(Party) && item.EntityId == partyId) ||
                (item.EntityType == nameof(WorkforceProfile) && item.EntityId == partyId) ||
                (item.EntityType == nameof(AiAgentProfile) && item.EntityId == partyId) ||
                (item.EntityType == nameof(RecruitmentApplication) && recruitmentApplicationIds.Contains(item.EntityId)) ||
                item.EntityId == partyId)
            .ToListAsync(cancellationToken);

        var interactionItems = interactions.Select(item => new CrmAccountActivityTimelineItemModel(
            item.Id,
            item.InteractionType.ToString(),
            item.Subject,
            string.IsNullOrWhiteSpace(item.Summary) ? item.Notes : item.Summary,
            string.IsNullOrWhiteSpace(item.NextActionText)
                ? item.OccurredAtUtc.LocalDateTime.ToString("g")
                : $"Next action: {item.NextActionText}",
            item.OccurredAtUtc,
            item.NextActionDueUtc.HasValue && item.NextActionDueUtc.Value < clock.GetUtcNow() ? "danger" : "info",
            item.NextActionDueUtc.HasValue && item.NextActionDueUtc.Value < clock.GetUtcNow()));

        var auditItems = auditEntries.Select(item => new CrmAccountActivityTimelineItemModel(
            item.Id,
            "Change",
            item.Summary,
            item.Action,
            item.Actor,
            item.CreatedAtUtc,
            item.IsSensitive ? "warning" : "neutral",
            false));

        return interactionItems
            .Concat(auditItems)
            .OrderByDescending(item => item.OccurredAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<PartyProjectAssignmentItemModel>> ListPartyProjectAssignmentsAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var assignments = await dbContext.Set<ProjectPartyAssignment>()
            .Where(item => item.PartyId == partyId)
            .ToListAsync(cancellationToken);
        if (assignments.Count == 0)
        {
            return [];
        }

        var projectNames = await dbContext.Set<Projects.Project>()
            .Where(item => assignments.Select(assignment => assignment.ProjectId).Distinct().Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        return assignments
            .Select(item => new PartyProjectAssignmentItemModel(
                item.Id,
                item.ProjectId,
                projectNames.GetValueOrDefault(item.ProjectId, "Unknown project"),
                item.AssignmentKind,
                item.NodeKey,
                item.AllocationPercent,
                item.StartsAtUtc is DateTimeOffset startsAtUtc ? DateOnly.FromDateTime(startsAtUtc.UtcDateTime) : null,
                item.EndsAtUtc is DateTimeOffset endsAtUtc ? DateOnly.FromDateTime(endsAtUtc.UtcDateTime) : null,
                item.IsPrimary,
                item.Notes))
            .OrderBy(item => item.ProjectName)
            .ThenBy(item => item.AssignmentKind)
            .ToList();
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
            .Where(item => item.PartyId == partyId)
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
        AppDbContext dbContext,
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

public sealed partial class HrService
{
    private async Task UpsertWorkforceSearchDocumentAsync(Guid partyId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(item => item.Id == partyId, cancellationToken);
        var profile = await dbContext.Set<WorkforceProfile>()
            .SingleOrDefaultAsync(item => item.PartyId == partyId, cancellationToken);
        if (party is null || profile is null || party.IsSensitive)
        {
            await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.Workforce, partyId.ToString("N"), cancellationToken);
            return;
        }

        var skillSummary = string.Join(
            ", ",
            ((await GetPartySkillMapAsync(dbContext, [partyId], cancellationToken)).GetValueOrDefault(partyId) ?? [])
                .Select(item => $"{item.SkillName} ({item.Proficiency})"));
        var managerName = profile.ManagerPartyId.HasValue
            ? await dbContext.Set<Party>()
                .Where(item => item.Id == profile.ManagerPartyId.Value)
                .Select(item => item.DisplayName)
                .FirstOrDefaultAsync(cancellationToken)
            : string.Empty;
        var homeUnitName = profile.HomeUnitPartyId.HasValue
            ? await dbContext.Set<Party>()
                .Where(item => item.Id == profile.HomeUnitPartyId.Value)
                .Select(item => item.DisplayName)
                .FirstOrDefaultAsync(cancellationToken)
            : string.Empty;

        await searchIndexService.UpsertAsync(
            new SearchDocumentInput(
                CrmHrSearchSourceTypes.Workforce,
                partyId.ToString("N"),
                "CRM / HR workforce",
                party.DisplayName,
                $"{profile.JobTitle} / {profile.Discipline} / {profile.Status}".Trim(' ', '/'),
                string.Join(
                    Environment.NewLine,
                    new[]
                    {
                        party.DisplayName,
                        profile.JobTitle,
                        profile.Discipline,
                        profile.Seniority,
                        profile.Location,
                        profile.TimeZone,
                        homeUnitName,
                        managerName,
                        skillSummary,
                        profile.Notes
                    }.Where(item => !string.IsNullOrWhiteSpace(item))),
                $"/crm-hr/workforce?partyId={partyId}"),
            cancellationToken);
    }
}

public sealed partial class AiAgentService
{
    private async Task UpsertAiAgentSearchDocumentAsync(Guid partyId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var party = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(item => item.Id == partyId, cancellationToken);
        var profile = await dbContext.Set<AiAgentProfile>()
            .SingleOrDefaultAsync(item => item.PartyId == partyId, cancellationToken);
        if (party is null || profile is null || party.IsSensitive)
        {
            await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.AiAgent, partyId.ToString("N"), cancellationToken);
            return;
        }

        var technicalWorkspace = await technicalAgentBridge.GetWorkspaceAsync(partyId, cancellationToken);
        var ownerName = profile.OwnerPartyId.HasValue
            ? await dbContext.Set<Party>()
                .Where(item => item.Id == profile.OwnerPartyId.Value)
                .Select(item => item.DisplayName)
                .FirstOrDefaultAsync(cancellationToken)
            : string.Empty;

        await searchIndexService.UpsertAsync(
            new SearchDocumentInput(
                CrmHrSearchSourceTypes.AiAgent,
                partyId.ToString("N"),
                "CRM / HR AI agent",
                party.DisplayName,
                $"{technicalWorkspace.ExecutionMode} / {profile.ValidationStatus}",
                string.Join(
                    Environment.NewLine,
                    new[]
                    {
                        party.DisplayName,
                        party.Summary,
                        technicalWorkspace.ProviderName,
                        technicalWorkspace.DefaultModel,
                        ownerName,
                        technicalWorkspace.BindingSummary,
                        profile.Notes
                    }.Where(item => !string.IsNullOrWhiteSpace(item))),
                $"/crm-hr/agents?partyId={partyId}"),
            cancellationToken);
    }
}

public sealed partial class RecruitingService
{
    private async Task UpsertRecruitmentApplicationSearchDocumentAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var application = await dbContext.Set<RecruitmentApplication>()
            .SingleOrDefaultAsync(item => item.Id == applicationId, cancellationToken);
        if (application is null)
        {
            await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.RecruitmentApplication, applicationId.ToString("N"), cancellationToken);
            return;
        }

        var party = await dbContext.Set<Party>()
            .SingleOrDefaultAsync(item => item.Id == application.PartyId, cancellationToken);
        if (party is null || party.IsSensitive)
        {
            await searchIndexService.DeleteAsync(CrmHrSearchSourceTypes.RecruitmentApplication, applicationId.ToString("N"), cancellationToken);
            return;
        }

        var peopleNames = await dbContext.Set<Party>()
            .Where(item =>
                item.Id == application.RecruiterPartyId ||
                item.Id == application.HiringManagerPartyId ||
                item.Id == application.TargetUnitPartyId)
            .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);
        var contacts = await dbContext.Set<PartyContactPoint>()
            .Where(item => item.PartyId == application.PartyId)
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

        await searchIndexService.UpsertAsync(
            new SearchDocumentInput(
                CrmHrSearchSourceTypes.RecruitmentApplication,
                applicationId.ToString("N"),
                "CRM / HR recruiting",
                party.DisplayName,
                $"{application.Stage} / {application.Decision} / {application.DesiredRole}".Trim(' ', '/'),
                string.Join(
                    Environment.NewLine,
                    new[]
                    {
                        party.DisplayName,
                        primaryEmail,
                        primaryPhone,
                        application.DesiredRole,
                        application.Source,
                        application.Notes,
                        application.RecruiterPartyId.HasValue ? peopleNames.GetValueOrDefault(application.RecruiterPartyId.Value) : string.Empty,
                        application.HiringManagerPartyId.HasValue ? peopleNames.GetValueOrDefault(application.HiringManagerPartyId.Value) : string.Empty,
                        application.TargetUnitPartyId.HasValue ? peopleNames.GetValueOrDefault(application.TargetUnitPartyId.Value) : string.Empty
                    }.Where(item => !string.IsNullOrWhiteSpace(item))),
                $"/crm-hr/recruiting?applicationId={applicationId}"),
            cancellationToken);
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
