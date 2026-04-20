using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private const string HrStaffingManagerDisplayName = "HR Staffing Manager";

    public async Task<Result> MatchLaunchPlanWithHrManagerAsync(
        Guid launchPlanId,
        string requestedBy = "process-workspace",
        CancellationToken cancellationToken = default)
    {
        if (launchPlanId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("Launch plan is required.", "processes.launch.plan-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var plan = await dbContext.Set<ProcessLaunchPlan>()
                .SingleOrDefaultAsync(item => item.Id == launchPlanId, cancellationToken);
            if (plan is null)
            {
                return Result.Failure(Error.Validation("Launch plan was not found.", "processes.launch.not-found"));
            }

            if (plan.Status is not ProcessLaunchPlanStatus.Draft and not ProcessLaunchPlanStatus.ChangesRequested)
            {
                return Result.Failure(Error.Validation(
                    "HR matching is available only while the launch plan is still in draft or changes-requested state.",
                    "processes.launch.staffing-locked"));
            }

            var roles = await dbContext.Set<ProcessLaunchPlanRole>()
                .Where(item => item.LaunchPlanId == plan.Id)
                .OrderBy(item => item.DisplayOrder)
                .ToListAsync(cancellationToken);
            if (roles.Count == 0)
            {
                return Result.Success();
            }

            var candidates = await dbContext.Set<ProcessLaunchCandidate>()
                .Where(item => roles.Select(role => role.Id).Contains(item.LaunchPlanRoleId))
                .ToListAsync(cancellationToken);
            var candidatesByRoleId = candidates
                .GroupBy(item => item.LaunchPlanRoleId)
                .ToDictionary(group => group.Key, group => group.ToList());
            var changedRoleIds = new HashSet<Guid>();
            var skillNames = await LoadSkillNamesAsync(dbContext, roles, cancellationToken);
            var aiFactsByPartyId = (await aiAgentService.ListAgentStaffingFactsAsync(
                    candidates
                        .Where(item => item.PartyId.HasValue)
                        .Select(item => item.PartyId!.Value)
                        .Distinct()
                        .ToList(),
                    cancellationToken))
                .ToDictionary(item => item.PartyId);

            foreach (var role in roles)
            {
                var requiredSkillIds = DeserializeGuidList(role.RequiredSkillIdsJson);
                if (!candidatesByRoleId.TryGetValue(role.Id, out var roleCandidates))
                {
                    roleCandidates = [];
                    candidatesByRoleId[role.Id] = roleCandidates;
                }

                var supplementalCandidates = await BuildHrManagerSupplementalCandidatesAsync(
                    dbContext,
                    plan,
                    role,
                    requiredSkillIds,
                    roleCandidates,
                    cancellationToken);
                if (supplementalCandidates.Count > 0)
                {
                    foreach (var supplementalCandidate in supplementalCandidates)
                    {
                        await dbContext.Set<ProcessLaunchCandidate>().AddAsync(supplementalCandidate, cancellationToken);
                        roleCandidates.Add(supplementalCandidate);
                    }

                    candidates.AddRange(supplementalCandidates);
                    role.RecommendationSummary = BuildLaunchRecommendationSummary(roleCandidates);
                }

                var selectedCandidate = roleCandidates
                    .Where(item => item.CandidateKind != ProcessLaunchCandidateKind.Gap)
                    .OrderByDescending(item => ScoreCandidateForHrManager(role, item, requiredSkillIds, skillNames, aiFactsByPartyId))
                    .ThenByDescending(item => item.IsRecommended)
                    .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (selectedCandidate is null)
                {
                    if (!string.Equals(role.SelectionSummary, "No confirmed match yet.", StringComparison.Ordinal) ||
                        !string.Equals(role.ReadinessSummary, "HR Manager: no suitable resource matched the recorded role facts yet.", StringComparison.Ordinal))
                    {
                        role.SelectionSummary = "No confirmed match yet.";
                        role.ReadinessSummary = "HR Manager: no suitable resource matched the recorded role facts yet.";
                        role.IsResolved = false;
                        role.RequiresProvisioning = false;
                        changedRoleIds.Add(role.Id);
                    }

                    continue;
                }

                if (role.SelectedCandidateId != selectedCandidate.Id ||
                    role.RequiresProvisioning != selectedCandidate.RequiresProvisioning ||
                    !role.IsResolved ||
                    !string.Equals(role.SelectionSummary, $"{selectedCandidate.DisplayName} / matched by {HrStaffingManagerDisplayName}", StringComparison.Ordinal) ||
                    !string.Equals(role.ReadinessSummary, ResolveLaunchReadinessSummary(selectedCandidate, "HR Manager"), StringComparison.Ordinal))
                {
                    role.SelectedCandidateId = selectedCandidate.Id;
                    role.RequiresProvisioning = selectedCandidate.RequiresProvisioning;
                    role.IsResolved = true;
                    role.SelectionSummary = $"{selectedCandidate.DisplayName} / matched by {HrStaffingManagerDisplayName}";
                    role.ReadinessSummary = ResolveLaunchReadinessSummary(selectedCandidate, "HR Manager");
                    changedRoleIds.Add(role.Id);
                }
            }

            if (changedRoleIds.Count > 0)
            {
                var staleProvisioning = await dbContext.Set<ProcessLaunchProvisioningRequest>()
                    .Where(item => item.LaunchPlanId == plan.Id && changedRoleIds.Contains(item.LaunchPlanRoleId))
                    .ToListAsync(cancellationToken);
                if (staleProvisioning.Count > 0)
                {
                    dbContext.RemoveRange(staleProvisioning);
                }
            }

            plan.UpdatedAtUtc = clock.GetUtcNow();
            plan.Status = ProcessLaunchPlanStatus.Draft;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Validation(
                "HR staffing matching conflicted with another update. Reload the launch plan and try again.",
                "processes.launch.staffing-conflict"));
        }
    }

    private async Task<List<ProcessLaunchCandidate>> BuildHrManagerSupplementalCandidatesAsync(
        AppDbContext dbContext,
        ProcessLaunchPlan plan,
        ProcessLaunchPlanRole role,
        IReadOnlyList<Guid> requiredSkillIds,
        IReadOnlyList<ProcessLaunchCandidate> existingCandidates,
        CancellationToken cancellationToken)
    {
        if (IsAiRoleFromLaunchRole(role))
        {
            return [];
        }

        var searchText = string.IsNullOrWhiteSpace(role.DisplayName)
            ? role.RoleKey
            : role.DisplayName;
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return [];
        }

        var seenPartyIds = existingCandidates
            .Where(item => item.PartyId.HasValue)
            .Select(item => item.PartyId!.Value)
            .ToHashSet();
        var broaderStaffingCandidates = await hrService.SearchStaffingCandidatesAsync(
            null,
            searchText,
            cancellationToken: cancellationToken);
        var broaderPartyIds = broaderStaffingCandidates
            .Where(item => !seenPartyIds.Contains(item.PartyId))
            .Select(item => item.PartyId)
            .Distinct()
            .ToList();
        var matchedSkillsByPartyId = await LoadMatchedSkillsByPartyIdAsync(
            dbContext,
            broaderPartyIds,
            requiredSkillIds,
            cancellationToken);

        var supplementalCandidates = new List<ProcessLaunchCandidate>();
        foreach (var staffingCandidate in broaderStaffingCandidates.Where(item => !seenPartyIds.Contains(item.PartyId)))
        {
            matchedSkillsByPartyId.TryGetValue(staffingCandidate.PartyId, out var matchedSkillSet);
            var matchedSkillCount = matchedSkillSet?.Count ?? 0;
            supplementalCandidates.Add(new ProcessLaunchCandidate
            {
                LaunchPlanRoleId = role.Id,
                CandidateKind = ProcessLaunchCandidateKind.Workforce,
                PartyId = staffingCandidate.PartyId,
                DisplayName = staffingCandidate.DisplayName,
                ExecutorKind = string.IsNullOrWhiteSpace(staffingCandidate.JobTitle)
                    ? staffingCandidate.PartyType.ToString()
                    : staffingCandidate.JobTitle,
                Score = 44m + staffingCandidate.AvailablePercent / 5m + matchedSkillCount * 6m,
                IsRecommended = supplementalCandidates.Count == 0 && matchedSkillCount > 0,
                AllowsDirectMessaging = true,
                RequiresProvisioning = false,
                RecommendationSummary = matchedSkillCount > 0
                    ? $"{HrStaffingManagerDisplayName} matched {matchedSkillCount} recorded skill(s) from the broader workforce directory."
                    : $"{HrStaffingManagerDisplayName} matched this resource from the broader workforce directory using the role wording and availability.",
                AvailabilitySummary = $"{staffingCandidate.AvailabilityState} / {staffingCandidate.AvailablePercent:0.#}% available",
                SourceRegistryKey = $"crmhr-workforce-hr-manager:{staffingCandidate.PartyId:D}",
                MetadataJson = "{}",
                CreatedAtUtc = clock.GetUtcNow()
            });
        }

        if (supplementalCandidates.Count == 0 &&
            plan.ProjectId.HasValue)
        {
            var projectAssignments = await projectPartyIntegrationBridge.ListAssignmentsDetailedAsync(plan.ProjectId.Value, cancellationToken);
            foreach (var assignment in projectAssignments
                         .Where(item => !seenPartyIds.Contains(item.PartyId))
                         .Where(item =>
                             item.PartyDisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                             item.PartyTypeLabel.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                             item.Notes.Contains(searchText, StringComparison.OrdinalIgnoreCase)))
            {
                supplementalCandidates.Add(new ProcessLaunchCandidate
                {
                    LaunchPlanRoleId = role.Id,
                    CandidateKind = ProcessLaunchCandidateKind.ProjectAssignment,
                    PartyId = assignment.PartyId,
                    DisplayName = assignment.PartyDisplayName,
                    ExecutorKind = assignment.PartyTypeLabel,
                    Score = assignment.IsPrimary ? 70m : 58m,
                    IsRecommended = assignment.IsPrimary,
                    AllowsDirectMessaging = true,
                    RequiresProvisioning = false,
                    RecommendationSummary = $"{HrStaffingManagerDisplayName} reused the existing project assignment for role {assignment.Role}.",
                    AvailabilitySummary = "Project assignment is already attached to the target project.",
                    SourceRegistryKey = $"project-assignment-hr-manager:{assignment.Id:D}",
                    MetadataJson = "{}",
                    CreatedAtUtc = clock.GetUtcNow()
                });
            }
        }

        return supplementalCandidates
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
    }

    private static decimal ScoreCandidateForHrManager(
        ProcessLaunchPlanRole role,
        ProcessLaunchCandidate candidate,
        IReadOnlyList<Guid> requiredSkillIds,
        IReadOnlyDictionary<Guid, string> skillNames,
        IReadOnlyDictionary<Guid, AiAgentStaffingFactListItemModel> aiFactsByPartyId)
    {
        var score = candidate.Score;
        var keywords = BuildRoleMatchingKeywords(role);

        var candidateText = string.Join(
            ' ',
            new[]
            {
                candidate.DisplayName,
                candidate.ExecutorKind,
                candidate.RecommendationSummary,
                candidate.AvailabilitySummary,
                candidate.SourceRegistryKey
            }.Where(item => !string.IsNullOrWhiteSpace(item)));
        score += CountRoleKeywordMatches(candidateText, keywords) * 2m;

        if (candidate.PartyId.HasValue &&
            aiFactsByPartyId.TryGetValue(candidate.PartyId.Value, out var aiFact))
        {
            var factText = string.Join(
                ' ',
                new[]
                {
                    aiFact.DisplayName,
                    aiFact.RoleTitle,
                    aiFact.Summary,
                    aiFact.Instructions,
                    aiFact.ProviderName,
                    aiFact.DefaultModel,
                    aiFact.TemplateKey,
                    string.Join(' ', aiFact.Tags),
                    string.Join(' ', aiFact.Capabilities.Select(item => $"{item.Name} {item.Scope} {item.ToolAccess} {item.Notes}"))
                }.Where(item => !string.IsNullOrWhiteSpace(item)));
            score += CountRoleKeywordMatches(factText, keywords) * 3m;

            foreach (var requiredSkillName in requiredSkillIds
                         .Select(skillId => skillNames.GetValueOrDefault(skillId))
                         .Where(item => !string.IsNullOrWhiteSpace(item)))
            {
                if (factText.Contains(requiredSkillName!, StringComparison.OrdinalIgnoreCase))
                {
                    score += 7m;
                }
            }

            score += aiFact.BindingStatus == AiResourceBindingStatus.Bound && aiFact.TechnicalAgentId.HasValue
                ? 6m
                : -8m;
        }

        if (candidate.RequiresProvisioning)
        {
            score -= 4m;
        }

        if (candidate.CandidateKind == ProcessLaunchCandidateKind.NewAiAgentProposal)
        {
            score += IsAiRoleFromLaunchRole(role) ? 8m : -30m;
        }

        return score;
    }

    private static IReadOnlyList<string> BuildRoleMatchingKeywords(ProcessLaunchPlanRole role)
    {
        return string.Join(
                ' ',
                new[]
                {
                    role.DisplayName,
                    role.RoleKey,
                    role.PreferredExecutorKind
                }.Where(item => !string.IsNullOrWhiteSpace(item)))
            .Split([' ', '-', '/', '_', ',', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int CountRoleKeywordMatches(string text, IReadOnlyList<string> keywords)
    {
        if (string.IsNullOrWhiteSpace(text) || keywords.Count == 0)
        {
            return 0;
        }

        return keywords.Count(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
