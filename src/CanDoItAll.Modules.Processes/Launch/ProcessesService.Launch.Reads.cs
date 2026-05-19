using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    public async Task<IReadOnlyList<ProcessLaunchPlanListItem>> ListLaunchPlansAsync(
        Guid? definitionId = null,
        Guid? projectId = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.Set<ProcessLaunchPlan>()
            .AsNoTracking()
            .AsQueryable();
        if (definitionId.HasValue)
        {
            query = query.Where(item => item.ProcessDefinitionId == definitionId.Value);
        }

        if (projectId.HasValue)
        {
            query = query.Where(item => item.ProjectId == projectId.Value);
        }

        var plans = (await query
                .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();
        if (plans.Count == 0)
        {
            return [];
        }

        var planIds = plans.Select(item => item.Id).ToList();
        var generatedRunStatusesByPlanId = await ResolveGeneratedRunStatusesByPlanIdAsync(dbContext, plans, cancellationToken);
        var roleSummaries = await dbContext.Set<ProcessLaunchPlanRole>()
            .AsNoTracking()
            .Where(item => planIds.Contains(item.LaunchPlanId))
            .GroupBy(item => item.LaunchPlanId)
            .Select(group => new
            {
                LaunchPlanId = group.Key,
                TotalRoleCount = group.Count(),
                ResolvedRoleCount = group.Count(item => item.IsResolved),
                PendingProvisioningCount = group.Count(item => item.RequiresProvisioning)
            })
            .ToDictionaryAsync(item => item.LaunchPlanId, cancellationToken);

        return plans
            .Select(item =>
            {
                roleSummaries.TryGetValue(item.Id, out var summary);
                var displayProjection = ProcessLaunchPlanDisplayProjector.Resolve(
                    item.Status,
                    generatedRunStatusesByPlanId.GetValueOrDefault(item.Id));
                return new ProcessLaunchPlanListItem(
                    item.Id,
                    item.ProcessDefinitionId,
                    item.ProcessDefinitionVersionId,
                    item.ProjectId,
                    item.Name,
                    item.OperatingMode,
                    item.Status,
                    summary?.ResolvedRoleCount ?? 0,
                    summary?.TotalRoleCount ?? 0,
                    summary?.PendingProvisioningCount ?? 0,
                    item.UpdatedAtUtc)
                {
                    GeneratedRunId = item.GeneratedRunId,
                    StatusBadgeText = displayProjection.StatusBadgeText,
                    StatusTone = displayProjection.StatusTone,
                    PlanningStatusBadgeText = displayProjection.PlanningStatusBadgeText,
                    StatusDetail = displayProjection.StatusDetail
                };
            })
            .ToList();
    }

    public async Task<ProcessLaunchPlanDetails?> GetLaunchPlanAsync(
        Guid launchPlanId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await LoadLaunchPlanDetailsAsync(dbContext, launchPlanId, cancellationToken);
    }

    private async Task<ProcessLaunchPlanDetails?> LoadLaunchPlanDetailsAsync(
        AppDbContext dbContext,
        Guid launchPlanId,
        CancellationToken cancellationToken)
    {
        var plan = await dbContext.Set<ProcessLaunchPlan>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == launchPlanId, cancellationToken);
        if (plan is null)
        {
            return null;
        }

        ProcessRunStatus? generatedRunStatus = null;
        if (plan.GeneratedRunId.HasValue)
        {
            generatedRunStatus = await dbContext.Set<ProcessRun>()
                .AsNoTracking()
                .Where(item => item.Id == plan.GeneratedRunId.Value)
                .Select(item => (ProcessRunStatus?)item.Status)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var roles = await dbContext.Set<ProcessLaunchPlanRole>()
            .AsNoTracking()
            .Where(item => item.LaunchPlanId == plan.Id)
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);
        var roleIds = roles.Select(item => item.Id).ToList();
        var candidates = roleIds.Count == 0
            ? []
            : await dbContext.Set<ProcessLaunchCandidate>()
                .AsNoTracking()
                .Where(item => roleIds.Contains(item.LaunchPlanRoleId))
                .ToListAsync(cancellationToken);
        var approvals = (await dbContext.Set<ProcessLaunchApprovalRecord>()
                .AsNoTracking()
                .Where(item => item.LaunchPlanId == plan.Id)
                .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
        var provisioningRequests = (await dbContext.Set<ProcessLaunchProvisioningRequest>()
                .AsNoTracking()
                .Where(item => item.LaunchPlanId == plan.Id)
                .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
        var candidatesByRoleId = candidates
            .GroupBy(item => item.LaunchPlanRoleId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProcessLaunchCandidateViewModel>)group
                    .OrderByDescending(item => item.Score)
                    .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(item =>
                    {
                        var teamMatch = ParseLaunchAgentTeamMatchMetadata(item.MetadataJson);
                        return new ProcessLaunchCandidateViewModel(
                            item.Id,
                            item.CandidateKind,
                            item.PartyId,
                            item.TechnicalAgentId,
                            item.WorkflowDefinitionId,
                            item.WorkflowVersionId,
                            item.DisplayName,
                            item.ExecutorKind,
                            item.Score,
                            item.IsRecommended,
                            item.AllowsDirectMessaging,
                            item.RequiresProvisioning,
                            item.RecommendationSummary,
                            item.AvailabilitySummary,
                            item.SourceRegistryKey)
                        {
                            AgentTeamId = teamMatch.AgentTeamId,
                            AgentTeamName = teamMatch.AgentTeamName,
                            IsOutsideSelectedTeam = teamMatch.IsOutsideSelectedTeam
                        };
                    })
                    .ToList());
        var displayProjection = ProcessLaunchPlanDisplayProjector.Resolve(plan.Status, generatedRunStatus);

        return new ProcessLaunchPlanDetails(
            plan.Id,
            plan.ProcessDefinitionId,
            plan.ProcessDefinitionVersionId,
            plan.ProjectId,
            plan.Name,
            plan.OperatingMode,
            plan.TriggerReason,
            plan.Status,
            plan.RecommendationStrategy,
            plan.FallbackStrategy,
            plan.Summary,
            plan.ApprovalThreadId,
            plan.GeneratedRunId,
            plan.RequestedBy,
            plan.CreatedAtUtc,
            plan.UpdatedAtUtc,
            plan.SubmittedAtUtc,
            plan.ApprovedAtUtc,
            plan.ExecutedAtUtc,
            roles
                .Select(item => new ProcessLaunchRoleViewModel(
                    item.Id,
                    item.RoleRequirementId,
                    item.RoleKey,
                    item.DisplayName,
                    item.PreferredExecutorKind,
                    item.IsRequired,
                    item.RequiresExplicitApproval,
                    item.RequiresProvisioning,
                    item.IsResolved,
                    item.SelectedCandidateId,
                    item.RecommendationSummary,
                    item.SelectionSummary,
                    item.ReadinessSummary,
                    DeserializeGuidList(item.RequiredSkillIdsJson),
                    candidatesByRoleId.GetValueOrDefault(item.Id) ?? []))
                .ToList(),
            approvals
                .Select(item => new ProcessLaunchApprovalViewModel(
                    item.Id,
                    item.Status,
                    item.ApproverPartyId,
                    item.ApproverDisplayName,
                    item.ApproverKind,
                    item.HumanSubstitutePartyId,
                    item.HumanSubstituteName,
                    item.CollaborationThreadId,
                    item.RequestMessage,
                    item.ResolutionSummary,
                    item.DecidedBy,
                    item.CreatedAtUtc,
                    item.DecidedAtUtc))
                .ToList(),
            provisioningRequests
                .Select(item => new ProcessLaunchProvisioningViewModel(
                    item.Id,
                    item.LaunchPlanRoleId,
                    item.SelectedCandidateId,
                    item.Status,
                    item.RequestKind,
                    item.Title,
                    item.ResultPartyId,
                    item.ResultTechnicalAgentId,
                    item.ResultSummary,
                    item.CreatedAtUtc,
                    item.CompletedAtUtc))
                .ToList())
        {
            StatusBadgeText = displayProjection.StatusBadgeText,
            StatusTone = displayProjection.StatusTone,
            PlanningStatusBadgeText = displayProjection.PlanningStatusBadgeText,
            StatusDetail = displayProjection.StatusDetail
        };
    }

    private static async Task<Dictionary<Guid, ProcessRunStatus?>> ResolveGeneratedRunStatusesByPlanIdAsync(
        AppDbContext dbContext,
        IReadOnlyList<ProcessLaunchPlan> plans,
        CancellationToken cancellationToken)
    {
        var generatedRunIdsByPlanId = plans
            .Where(item => item.GeneratedRunId.HasValue)
            .ToDictionary(item => item.Id, item => item.GeneratedRunId!.Value);
        if (generatedRunIdsByPlanId.Count == 0)
        {
            return [];
        }

        var generatedRunIds = generatedRunIdsByPlanId.Values
            .Distinct()
            .ToList();
        var runStatusesById = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .Where(item => generatedRunIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Status, cancellationToken);

        return generatedRunIdsByPlanId.ToDictionary(
            pair => pair.Key,
            pair => runStatusesById.TryGetValue(pair.Value, out var runStatus)
                ? (ProcessRunStatus?)runStatus
                : null);
    }

    private async Task<PublishedProcessLaunchContext?> LoadPublishedContextAsync(
        AppDbContext dbContext,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        var definition = await dbContext.Set<ProcessDefinition>()
            .SingleOrDefaultAsync(item => item.Id == definitionId, cancellationToken);
        if (definition is null || !definition.ActivePublishedVersionId.HasValue)
        {
            return null;
        }

        var publishedVersion = await dbContext.Set<ProcessDefinitionVersion>()
            .SingleOrDefaultAsync(
                item => item.ProcessDefinitionId == definition.Id &&
                    item.Id == definition.ActivePublishedVersionId.Value &&
                    item.Status == ProcessVersionStatus.Published,
                cancellationToken);
        if (publishedVersion is null)
        {
            return null;
        }

        var roles = await dbContext.Set<ProcessRoleRequirement>()
            .Where(item => item.ProcessDefinitionVersionId == publishedVersion.Id)
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);
        var roleIds = roles.Select(item => item.Id).ToList();
        var roleSkills = roleIds.Count == 0
            ? []
            : await dbContext.Set<ProcessRoleSkillRequirement>()
                .Where(item => roleIds.Contains(item.RoleRequirementId))
                .OrderBy(item => item.MinimumYearsExperience)
                .ToListAsync(cancellationToken);

        return new PublishedProcessLaunchContext(
            definition,
            publishedVersion,
            roles,
            roleSkills
                .GroupBy(item => item.RoleRequirementId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<ProcessRoleSkillRequirement>)group.ToList()));
    }
}
