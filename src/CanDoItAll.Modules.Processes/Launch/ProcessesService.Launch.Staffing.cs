using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private const string HrStaffingManagerDisplayName = "HR Staffing Manager";
    private const string WorkspaceDotnetBuildCapability = "workspace-dotnet-build";
    private const string WorkspaceDotnetNewCapability = "workspace-dotnet-new";
    private const string WorkspaceDotnetTestCapability = "workspace-dotnet-test";
    private const string WorkspaceDotnetRunCapability = "workspace-dotnet-run";
    private const string RunTestsCapability = "run-tests";
    private const string PlaywrightLocalMcpCapability = "playwright-local-mcp";
    private const string ArchitectureSourceRagCapability = "architecture-source-rag";
    private const int RoleSpecificContextItemCount = 4;
    private static readonly HashSet<string> RoleKeywordStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "and",
        "the",
        "for",
        "with",
        "from",
        "this",
        "that",
        "who",
        "able",
        "into",
        "before",
        "after",
        "each",
        "current",
        "must",
        "keep",
        "actual",
        "process",
        "workflow",
        "launch",
        "proof",
        "validation",
        "integration",
        "deterministic",
        "sample",
        "role",
        "agent",
        "resource",
        "delivery",
        "request",
        "requested",
        "implementation"
    };

    public Task<Result> MatchLaunchPlanWithHrManagerAsync(
        Guid launchPlanId,
        string requestedBy,
        CancellationToken cancellationToken = default)
        => MatchLaunchPlanWithHrManagerAsync(
            launchPlanId,
            agentTeamId: null,
            requestedBy,
            cancellationToken);

    public async Task<Result> MatchLaunchPlanWithHrManagerAsync(
        Guid launchPlanId,
        Guid? agentTeamId = null,
        string requestedBy = "process-workspace",
        CancellationToken cancellationToken = default)
    {
        if (launchPlanId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("Launch plan is required.", "processes.launch.plan-required"));
        }

        logger.LogInformation(
            "Starting HR staffing match for launch plan {LaunchPlanId}. AgentTeamId={AgentTeamId}. RequestedBy={RequestedBy}.",
            launchPlanId,
            agentTeamId,
            requestedBy);
        await SynchronizeAiDirectoryProjectionForProcessAsync("launch-plan HR matching", cancellationToken);
        var teamScopeResult = await LoadLaunchAgentTeamScopeAsync(agentTeamId, cancellationToken);
        if (teamScopeResult.IsFailure)
        {
            return Result.Failure(teamScopeResult.Errors);
        }

        var teamScope = teamScopeResult.Value;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await BeginCoordinatedTransactionAsync(dbContext, cancellationToken);

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
            var roleRequirementIds = roles
                .Select(item => item.RoleRequirementId)
                .ToList();
            var roleRequirementsById = await dbContext.Set<ProcessRoleRequirement>()
                .AsNoTracking()
                .Where(item => item.ProcessDefinitionVersionId == plan.ProcessDefinitionVersionId &&
                               roleRequirementIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
            var project = plan.ProjectId.HasValue
                ? await dbContext.Set<Project>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == plan.ProjectId.Value, cancellationToken)
                : null;
            ProcessProjectStructureContextFormatter.TryParse(plan.TriggerReason, out var projectStructureContext);
            var launchContext = await BuildLaunchRoleContextAsync(
                dbContext,
                plan,
                project,
                projectStructureContext,
                cancellationToken);
            var aiDirectorySnapshot = await LoadLaunchAiDirectorySnapshotAsync(dbContext, cancellationToken);
            var aiDirectoryByPartyId = aiDirectorySnapshot.Directory.ToDictionary(item => item.PartyId);
            var aiFactsByPartyId = aiDirectorySnapshot.StaffingFactsByPartyId;
            logger.LogInformation(
                "HR staffing match for launch plan {LaunchPlanId} loaded {RoleCount} roles, {CandidateCount} candidates, and {AiResourceCount} projected AI resources.",
                launchPlanId,
                roles.Count,
                candidates.Count,
                aiDirectorySnapshot.Directory.Count);

            foreach (var role in roles)
            {
                var requiredSkillIds = DeserializeGuidList(role.RequiredSkillIdsJson);
                roleRequirementsById.TryGetValue(role.RoleRequirementId, out var roleRequirement);
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
                    aiDirectoryByPartyId,
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

                foreach (var candidate in roleCandidates)
                {
                    candidate.MetadataJson = ApplyLaunchAgentTeamMatchMetadata(
                        candidate.MetadataJson,
                        teamScope,
                        candidate);
                }

                var selectedCandidate = roleCandidates
                    .Where(item => item.CandidateKind != ProcessLaunchCandidateKind.Gap)
                    .OrderByDescending(item => ScoreCandidateForHrManager(
                        role,
                        roleRequirement,
                        item,
                        requiredSkillIds,
                        skillNames,
                        aiFactsByPartyId,
                        launchContext,
                        teamScope))
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
            logger.LogInformation(
                "Completed HR staffing match for launch plan {LaunchPlanId}. AgentTeamId={AgentTeamId}. ChangedRoleCount={ChangedRoleCount} ResolvedRoleCount={ResolvedRoleCount} RequestedBy={RequestedBy}.",
                launchPlanId,
                teamScope?.Id,
                changedRoleIds.Count,
                roles.Count(item => item.IsResolved),
                requestedBy);
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

    private async Task<Result<LaunchAgentTeamScope?>> LoadLaunchAgentTeamScopeAsync(
        Guid? agentTeamId,
        CancellationToken cancellationToken)
    {
        if (!agentTeamId.HasValue)
        {
            return Result<LaunchAgentTeamScope?>.Success(null);
        }

        var teams = await agentWorkspaceService.ListAgentTeamsAsync(cancellationToken);
        var team = teams.SingleOrDefault(item => item.Id == agentTeamId.Value);
        if (team is null)
        {
            return Result<LaunchAgentTeamScope?>.Failure(Error.Validation(
                "Agent team was not found.",
                "processes.launch.agent-team-not-found"));
        }

        return Result<LaunchAgentTeamScope?>.Success(new LaunchAgentTeamScope(
            team.Id,
            team.Name,
            team.AgentIds
                .Where(item => item != Guid.Empty)
                .ToHashSet()));
    }

    private async Task<List<ProcessLaunchCandidate>> BuildHrManagerSupplementalCandidatesAsync(
        AppDbContext dbContext,
        ProcessLaunchPlan plan,
        ProcessLaunchPlanRole role,
        IReadOnlyList<Guid> requiredSkillIds,
        IReadOnlyList<ProcessLaunchCandidate> existingCandidates,
        IReadOnlyDictionary<Guid, AiAgentListItemModel> aiDirectoryByPartyId,
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

        var requiresTechnicalAgentBinding = RequiresTechnicalAgentBinding(plan);
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
            aiDirectoryByPartyId.TryGetValue(staffingCandidate.PartyId, out var staffingAiResource);
            var requiresProvisioning = requiresTechnicalAgentBinding && !HasRunnableTechnicalAgent(staffingAiResource);
            supplementalCandidates.Add(new ProcessLaunchCandidate
            {
                LaunchPlanRoleId = role.Id,
                CandidateKind = ProcessLaunchCandidateKind.Workforce,
                PartyId = staffingCandidate.PartyId,
                TechnicalAgentId = staffingAiResource?.TechnicalAgentId,
                DisplayName = staffingCandidate.DisplayName,
                ExecutorKind = string.IsNullOrWhiteSpace(staffingCandidate.JobTitle)
                    ? staffingCandidate.PartyType.ToString()
                    : staffingCandidate.JobTitle,
                Score = 44m + staffingCandidate.AvailablePercent / 5m + matchedSkillCount * 6m - (requiresProvisioning ? 8m : 0m),
                IsRecommended = !requiresProvisioning && supplementalCandidates.Count == 0 && matchedSkillCount > 0,
                AllowsDirectMessaging = true,
                RequiresProvisioning = requiresProvisioning,
                RecommendationSummary = matchedSkillCount > 0
                    ? $"{HrStaffingManagerDisplayName} matched {matchedSkillCount} recorded skill(s) from the broader workforce directory."
                    : $"{HrStaffingManagerDisplayName} matched this resource from the broader workforce directory using the role wording and availability.",
                AvailabilitySummary = requiresProvisioning
                    ? $"{staffingCandidate.AvailabilityState} / {staffingCandidate.AvailablePercent:0.#}% available. A runnable internal AI resource will be provisioned before execution."
                    : $"{staffingCandidate.AvailabilityState} / {staffingCandidate.AvailablePercent:0.#}% available",
                SourceRegistryKey = $"crmhr-workforce-hr-manager:{staffingCandidate.PartyId:D}",
                MetadataJson = BuildLaunchProvisioningMetadata(
                    new ProcessRoleRequirement
                    {
                        Id = role.RoleRequirementId,
                        DisplayName = role.DisplayName,
                        Key = role.RoleKey,
                        PreferredExecutorKind = role.PreferredExecutorKind
                    },
                    requiredSkillIds,
                    staffingCandidate.DisplayName,
                    staffingCandidate.PartyId),
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
                aiDirectoryByPartyId.TryGetValue(assignment.PartyId, out var linkedAiResource);
                var requiresProvisioning = requiresTechnicalAgentBinding && !HasRunnableTechnicalAgent(linkedAiResource);
                supplementalCandidates.Add(new ProcessLaunchCandidate
                {
                    LaunchPlanRoleId = role.Id,
                    CandidateKind = ProcessLaunchCandidateKind.ProjectAssignment,
                    PartyId = assignment.PartyId,
                    DisplayName = assignment.PartyDisplayName,
                    ExecutorKind = assignment.PartyTypeLabel,
                    TechnicalAgentId = linkedAiResource?.TechnicalAgentId,
                    Score = (assignment.IsPrimary ? 70m : 58m) - (requiresProvisioning ? 8m : 0m),
                    IsRecommended = assignment.IsPrimary && !requiresProvisioning,
                    AllowsDirectMessaging = true,
                    RequiresProvisioning = requiresProvisioning,
                    RecommendationSummary = $"{HrStaffingManagerDisplayName} reused the existing project assignment for role {assignment.Role}.",
                    AvailabilitySummary = requiresProvisioning
                        ? "Project assignment is attached to the target project, and a runnable internal AI resource will be provisioned before execution."
                        : "Project assignment is already attached to the target project.",
                    SourceRegistryKey = $"project-assignment-hr-manager:{assignment.Id:D}",
                    MetadataJson = BuildLaunchProvisioningMetadata(
                        new ProcessRoleRequirement
                        {
                            Id = role.RoleRequirementId,
                            DisplayName = role.DisplayName,
                            Key = role.RoleKey,
                            PreferredExecutorKind = role.PreferredExecutorKind
                        },
                        requiredSkillIds,
                        assignment.PartyDisplayName,
                        assignment.PartyId),
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
        ProcessRoleRequirement? roleRequirement,
        ProcessLaunchCandidate candidate,
        IReadOnlyList<Guid> requiredSkillIds,
        IReadOnlyDictionary<Guid, string> skillNames,
        IReadOnlyDictionary<Guid, AiAgentStaffingFactListItemModel> aiFactsByPartyId,
        IReadOnlyList<string?> launchContext,
        LaunchAgentTeamScope? teamScope = null)
    {
        var roleContext = roleRequirement is null
            ? launchContext
            : [
                roleRequirement.Purpose,
                roleRequirement.StaffingIntent,
                roleRequirement.SnapshotSummary,
                roleRequirement.RoleTemplateSourceKey,
                .. launchContext
            ];

        return ScoreCandidateForRole(
            role.DisplayName,
            role.RoleKey,
            role.PreferredExecutorKind,
            roleContext,
            IsAiRoleFromLaunchRole(role),
            candidate,
            requiredSkillIds,
            skillNames,
            aiFactsByPartyId) + ScoreLaunchAgentTeamFit(candidate, teamScope);
    }

    private static decimal ScoreCandidateForHrManager(
        ProcessRoleRequirement role,
        ProcessLaunchCandidate candidate,
        IReadOnlyList<Guid> requiredSkillIds,
        IReadOnlyDictionary<Guid, string> skillNames,
        IReadOnlyDictionary<Guid, AiAgentStaffingFactListItemModel> aiFactsByPartyId,
        IReadOnlyList<string?> launchContext,
        LaunchAgentTeamScope? teamScope = null)
    {
        return ScoreCandidateForRole(
            role.DisplayName,
            role.Key,
            role.PreferredExecutorKind,
            [role.Purpose, role.StaffingIntent, role.SnapshotSummary, role.RoleTemplateSourceKey, .. launchContext],
            IsAiRole(role),
            candidate,
            requiredSkillIds,
            skillNames,
            aiFactsByPartyId) + ScoreLaunchAgentTeamFit(candidate, teamScope);
    }

    private static decimal ScoreLaunchAgentTeamFit(
        ProcessLaunchCandidate candidate,
        LaunchAgentTeamScope? teamScope)
    {
        if (teamScope is null || !IsLaunchAgentCandidate(candidate))
        {
            return 0m;
        }

        if (candidate.TechnicalAgentId.HasValue &&
            teamScope.AgentIds.Contains(candidate.TechnicalAgentId.Value))
        {
            return 16m;
        }

        return candidate.CandidateKind == ProcessLaunchCandidateKind.NewAiAgentProposal
            ? -12m
            : -4m;
    }

    private static decimal ScoreCandidateForRole(
        string displayName,
        string roleKey,
        string preferredExecutorKind,
        IReadOnlyList<string?> additionalRoleContext,
        bool prefersAiProposal,
        ProcessLaunchCandidate candidate,
        IReadOnlyList<Guid> requiredSkillIds,
        IReadOnlyDictionary<Guid, string> skillNames,
        IReadOnlyDictionary<Guid, AiAgentStaffingFactListItemModel> aiFactsByPartyId)
    {
        var score = candidate.Score;
        var keywords = BuildRoleMatchingKeywords(
            displayName,
            roleKey,
            preferredExecutorKind,
            additionalRoleContext);
        var identityKeywords = BuildRoleIdentityKeywords(displayName, roleKey);

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
        var roleIdentityText = BuildRoleFitText(displayName, roleKey, []);
        var candidateLooksLikeQa = RoleMentions(candidateText, "qa") ||
                                   RoleMentions(candidateText, "quality") ||
                                   RoleMentions(candidateText, "review");
        if (!RoleMentions(roleIdentityText, "qa") &&
            !RoleMentions(roleIdentityText, "quality") &&
            (RoleMentions(roleIdentityText, "lead-engineer") ||
             RoleMentions(roleIdentityText, "engineer") ||
             RoleMentions(roleIdentityText, "implementation")) &&
            candidateLooksLikeQa)
        {
            score -= 140m;
        }

        var selectedWorkIsNonBlazorDotNet = MentionsNonBlazorDotNetWork(BuildRoleFitText(displayName, roleKey, additionalRoleContext));
        if (selectedWorkIsNonBlazorDotNet)
        {
            var candidateMentionsBlazor = MentionsBlazorStack(candidateText);
            if (candidateMentionsBlazor)
            {
                score -= 90m;
            }

            if (!candidateMentionsBlazor &&
                !candidateLooksLikeQa &&
                MentionsDotNetStack(candidateText))
            {
                score += 160m;
            }
            else if (MentionsJavaScriptStack(candidateText) &&
                     !MentionsDotNetStack(candidateText))
            {
                score -= 180m;
            }
            else if (RoleMentions(candidateText, "analyst"))
            {
                score -= 80m;
            }
        }

        if (candidate.CandidateKind is not ProcessLaunchCandidateKind.NewAiAgentProposal and not ProcessLaunchCandidateKind.Gap)
        {
            score += CountRoleKeywordMatches(candidate.DisplayName, identityKeywords) * 28m;
        }

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
            score += ScoreExplicitAgentTagAlias(additionalRoleContext, aiFact.Tags);
            score += ScoreAiRoleCapabilityFit(displayName, roleKey, additionalRoleContext, aiFact);
        }

        score += ScorePreferredExecutorFit(preferredExecutorKind, candidate);

        if (candidate.RequiresProvisioning)
        {
            score -= 4m;
        }

        if (candidate.CandidateKind == ProcessLaunchCandidateKind.NewAiAgentProposal)
        {
            score += prefersAiProposal ? 8m : -30m;
        }

        if (candidate.CandidateKind == ProcessLaunchCandidateKind.Gap)
        {
            score -= 120m;
        }

        return score;
    }

    private static decimal ScoreExplicitAgentTagAlias(
        IReadOnlyList<string?> additionalRoleContext,
        IReadOnlyList<string> candidateTags)
    {
        if (additionalRoleContext.Count == 0 || candidateTags.Count == 0)
        {
            return 0m;
        }

        var hasExactTagAlias = additionalRoleContext
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Any(alias => candidateTags.Any(tag => string.Equals(tag, alias, StringComparison.OrdinalIgnoreCase)));

        return hasExactTagAlias ? 240m : 0m;
    }

    private static decimal ScoreAiRoleCapabilityFit(
        string displayName,
        string roleKey,
        IReadOnlyList<string?> additionalRoleContext,
        AiAgentStaffingFactListItemModel aiFact)
    {
        var primaryRoleText = BuildRoleFitText(displayName, roleKey, []);
        var roleSpecificText = BuildRoleFitText(
            displayName,
            roleKey,
            TakeRoleSpecificContext(additionalRoleContext));
        var workText = BuildRoleFitText(displayName, roleKey, additionalRoleContext);
        var agentText = BuildAgentFitText(aiFact);
        var capabilityNames = aiFact.Capabilities
            .Select(item => item.Name)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();

        var score = 0m;
        var isArchitectureRole = RoleMentions(primaryRoleText, "solution-architect") ||
                                 RoleMentions(primaryRoleText, "architect");
        var isQaRole = RoleMentions(primaryRoleText, "qa") ||
                       RoleMentions(primaryRoleText, "quality");
        var isProductOwnerRole = RoleMentions(primaryRoleText, "product-owner") ||
                                 (RoleMentions(primaryRoleText, "product") &&
                                  RoleMentions(primaryRoleText, "owner"));
        var isDeliveryManagerRole = RoleMentions(primaryRoleText, "delivery-manager") ||
                                    (RoleMentions(primaryRoleText, "delivery") &&
                                     RoleMentions(primaryRoleText, "manager"));
        var isImplementationRole = !isArchitectureRole &&
                                   !isQaRole &&
                                   !isProductOwnerRole &&
                                   !isDeliveryManagerRole &&
                                   (RoleMentions(primaryRoleText, "lead-engineer") ||
                                    RoleMentions(primaryRoleText, "engineer") ||
                                    RoleMentions(roleSpecificText, "software-engineer") ||
                                    RoleMentions(roleSpecificText, "implementation") ||
                                    RoleMentions(roleSpecificText, "build-capable"));
        var workMentionsBlazor = MentionsBlazorStack(workText);
        var workMentionsDotNet = MentionsDotNetStack(workText);
        var workMentionsJavaScript = MentionsJavaScriptStack(workText);
        var selectedWorkIsNonBlazorDotNet = MentionsNonBlazorDotNetWork(workText);
        var selectedWorkIsStaticClientWeb = MentionsStaticClientWebWork(workText);
        var selectedWorkIsJavaScript = ContextHasExclusiveStackSignal(
                                           additionalRoleContext,
                                           MentionsJavaScriptStack,
                                           MentionsBlazorStack,
                                           MentionsDotNetStack) ||
                                       selectedWorkIsStaticClientWeb ||
                                       (MentionsJavaScriptStack(roleSpecificText) &&
                                        !MentionsBlazorStack(roleSpecificText) &&
                                        !MentionsDotNetStack(roleSpecificText));
        var agentHasDirectBlazorIdentity = MentionsBlazorStack(aiFact.DisplayName) ||
                                           MentionsBlazorStack(aiFact.RoleTitle) ||
                                           MentionsBlazorStack(aiFact.TemplateKey);
        var agentHasDirectDotNetIdentity = MentionsDotNetStack(aiFact.DisplayName) ||
                                           MentionsDotNetStack(aiFact.RoleTitle) ||
                                           MentionsDotNetStack(aiFact.TemplateKey);
        var agentHasDirectJavaScriptIdentity = MentionsJavaScriptStack(aiFact.DisplayName) ||
                                               MentionsJavaScriptStack(aiFact.RoleTitle) ||
                                               MentionsJavaScriptStack(aiFact.TemplateKey);
        var agentMentionsBlazor = MentionsBlazorStack(aiFact.DisplayName) ||
                                  MentionsBlazorStack(aiFact.RoleTitle) ||
                                  MentionsBlazorStack(aiFact.TemplateKey) ||
                                  MentionsBlazorStack(agentText);
        var agentMentionsDotNet = MentionsDotNetStack(aiFact.DisplayName) ||
                                  MentionsDotNetStack(aiFact.RoleTitle) ||
                                  MentionsDotNetStack(aiFact.TemplateKey) ||
                                  MentionsDotNetStack(agentText);
        var agentMentionsJavaScript = MentionsJavaScriptStack(aiFact.DisplayName) ||
                                      MentionsJavaScriptStack(aiFact.RoleTitle) ||
                                      MentionsJavaScriptStack(aiFact.TemplateKey) ||
                                      MentionsJavaScriptStack(agentText);

        if (TextEqualsNormalized(aiFact.DisplayName, displayName) ||
            TextEqualsNormalized(aiFact.RoleTitle, displayName))
        {
            score += 35m;
        }

        var isScreenshotCaptureRole = RoleMentions(primaryRoleText, "screenshot") &&
                                      (RoleMentions(primaryRoleText, "capture") ||
                                       RoleMentions(workText, "playwright") ||
                                       RoleMentions(roleKey, "app-screenshot-capture-agent"));
        var isScreenshotStorageReviewRole = RoleMentions(primaryRoleText, "screenshot") &&
                                            (RoleMentions(primaryRoleText, "review") ||
                                             RoleMentions(primaryRoleText, "storage") ||
                                             RoleMentions(primaryRoleText, "asset") ||
                                             RoleMentions(roleKey, "screenshot-review-storage-agent"));
        var isLayoutImageGenerationRole = (RoleMentions(primaryRoleText, "layout") ||
                                           RoleMentions(roleSpecificText, "layout")) &&
                                          (RoleMentions(primaryRoleText, "image-generation") ||
                                           RoleMentions(primaryRoleText, "image") ||
                                           RoleMentions(roleSpecificText, "image-generation") ||
                                           RoleMentions(roleKey, "layout-image-generation-agent"));
        if (isScreenshotCaptureRole)
        {
            if (HasCapability(capabilityNames, PlaywrightLocalMcpCapability))
            {
                score += 260m;
            }
            else
            {
                score -= 180m;
            }

            if (HasCapability(capabilityNames, WorkspaceDotnetRunCapability))
            {
                score += 80m;
            }

            if (RoleMentions(agentText, "screenshot") ||
                RoleMentions(aiFact.TemplateKey, "app-screenshot-capture-agent"))
            {
                score += 220m;
            }

            if (RoleMentions(agentText, "review") ||
                RoleMentions(agentText, "asset-storage"))
            {
                score -= 90m;
            }
        }

        if (isScreenshotStorageReviewRole)
        {
            var hasScreenshotStorageIdentity =
                RoleMentions(aiFact.DisplayName, "screenshot") &&
                (RoleMentions(aiFact.DisplayName, "review") ||
                 RoleMentions(aiFact.DisplayName, "asset") ||
                 RoleMentions(aiFact.DisplayName, "storage")) ||
                RoleMentions(aiFact.RoleTitle, "screenshot") &&
                (RoleMentions(aiFact.RoleTitle, "review") ||
                 RoleMentions(aiFact.RoleTitle, "asset") ||
                 RoleMentions(aiFact.RoleTitle, "storage")) ||
                RoleMentions(aiFact.TemplateKey, "screenshot-review-storage-agent") ||
                RoleMentions(aiFact.TemplateKey, "runtime-screenshot-review-storage-agent");

            if (hasScreenshotStorageIdentity)
            {
                score += 850m;
            }
            else if (RoleMentions(agentText, "screenshot") &&
                     (RoleMentions(agentText, "review") ||
                      RoleMentions(agentText, "asset") ||
                      RoleMentions(agentText, "storage")))
            {
                score += 260m;
            }
            else
            {
                score -= 320m;
            }

            if (RoleMentions(agentText, "capture") &&
                HasCapability(capabilityNames, PlaywrightLocalMcpCapability))
            {
                score -= 100m;
            }

            if (!hasScreenshotStorageIdentity &&
                (RoleMentions(agentText, "programming") ||
                 RoleMentions(agentText, "implements") ||
                 RoleMentions(agentText, "developer")))
            {
                score -= 160m;
            }
        }

        if (isLayoutImageGenerationRole)
        {
            var hasLayoutImageIdentity =
                RoleMentions(aiFact.DisplayName, "layout") &&
                (RoleMentions(aiFact.DisplayName, "image") ||
                 RoleMentions(aiFact.DisplayName, "generation")) ||
                RoleMentions(aiFact.RoleTitle, "layout") &&
                (RoleMentions(aiFact.RoleTitle, "image") ||
                 RoleMentions(aiFact.RoleTitle, "generation")) ||
                RoleMentions(aiFact.TemplateKey, "layout-image-generation-agent") ||
                RoleMentions(aiFact.TemplateKey, "runtime-layout-image-generation-agent");

            if (hasLayoutImageIdentity)
            {
                score += 900m;
            }
            else if (RoleMentions(agentText, "image-generation") ||
                     RoleMentions(agentText, "layout-recommendation"))
            {
                score += 280m;
            }
            else
            {
                score -= 280m;
            }

            if (RoleMentions(agentText, "screenshot") &&
                RoleMentions(agentText, "capture") &&
                HasCapability(capabilityNames, PlaywrightLocalMcpCapability))
            {
                score -= 120m;
            }

            if (RoleMentions(agentText, "project-structure") ||
                RoleMentions(agentText, "asset-storage"))
            {
                score += 120m;
            }
        }

        if (isImplementationRole)
        {
            if (!selectedWorkIsJavaScript &&
                HasCapability(capabilityNames, WorkspaceDotnetBuildCapability))
            {
                score += 72m;
            }

            if (!selectedWorkIsJavaScript &&
                HasCapability(capabilityNames, WorkspaceDotnetTestCapability))
            {
                score += 16m;
            }

            if (!selectedWorkIsJavaScript &&
                HasCapability(capabilityNames, WorkspaceDotnetRunCapability))
            {
                score += 12m;
            }

            if (!selectedWorkIsJavaScript &&
                HasCapability(capabilityNames, WorkspaceDotnetNewCapability))
            {
                score += 14m;
            }

            if (RoleMentions(agentText, "programming") ||
                RoleMentions(agentText, "implements"))
            {
                score += 30m;
            }

            var agentLooksLikeImplementationOwner =
                RoleMentions(aiFact.DisplayName, "developer") ||
                RoleMentions(aiFact.RoleTitle, "developer") ||
                RoleMentions(aiFact.TemplateKey, "developer") ||
                RoleMentions(agentText, "application-developer") ||
                RoleMentions(agentText, "implements");
            var agentLooksLikeArchitectureOwner =
                RoleMentions(aiFact.DisplayName, "architect") ||
                RoleMentions(aiFact.RoleTitle, "architect") ||
                RoleMentions(aiFact.TemplateKey, "architect") ||
                RoleMentions(agentText, "architecture");
            if (agentLooksLikeImplementationOwner)
            {
                score += 180m;
            }

            if (agentLooksLikeArchitectureOwner && !agentLooksLikeImplementationOwner)
            {
                score -= 220m;
            }

            if (workMentionsBlazor)
            {
                if (agentHasDirectBlazorIdentity)
                {
                    score += 220m;
                }
                else if (agentMentionsBlazor)
                {
                    score += 110m;
                }
                else if (HasCapability(capabilityNames, WorkspaceDotnetBuildCapability))
                {
                    score += 8m;
                }
            }

            if (workMentionsDotNet)
            {
                if (agentHasDirectDotNetIdentity)
                {
                    score += 120m;
                }
                else if (agentMentionsDotNet)
                {
                    score += 48m;
                }

                if (!workMentionsBlazor &&
                    agentHasDirectBlazorIdentity)
                {
                    score -= 220m;
                }

                if (selectedWorkIsNonBlazorDotNet &&
                    agentHasDirectJavaScriptIdentity &&
                    !agentHasDirectDotNetIdentity)
                {
                    score -= 260m;
                }
            }

            if (workMentionsJavaScript || selectedWorkIsJavaScript)
            {
                if (agentHasDirectJavaScriptIdentity)
                {
                    score += selectedWorkIsJavaScript ? 560m : 70m;
                }
                else if (agentMentionsJavaScript &&
                         !agentHasDirectBlazorIdentity &&
                         !agentHasDirectDotNetIdentity)
                {
                    score += selectedWorkIsJavaScript ? 220m : 45m;
                }

                if (selectedWorkIsJavaScript &&
                    !agentHasDirectJavaScriptIdentity &&
                    (agentHasDirectDotNetIdentity ||
                     agentHasDirectBlazorIdentity ||
                     HasCapability(capabilityNames, WorkspaceDotnetBuildCapability)))
                {
                    score -= 260m;
                }
            }

            if (RoleMentions(agentText, "qa") ||
                RoleMentions(agentText, "quality") ||
                RoleMentions(agentText, "review"))
            {
                score -= 90m;
            }
        }

        if (isQaRole)
        {
            if (RoleMentions(aiFact.DisplayName, "qa") ||
                RoleMentions(aiFact.RoleTitle, "qa") ||
                RoleMentions(aiFact.TemplateKey, "qa"))
            {
                score += 110m;
            }
            else if (RoleMentions(agentText, "qa"))
            {
                score += 40m;
            }
            else if (RoleMentions(agentText, "quality"))
            {
                score += 24m;
            }

            if (HasCapability(capabilityNames, RunTestsCapability))
            {
                score += 14m;
            }

            if (HasCapability(capabilityNames, PlaywrightLocalMcpCapability))
            {
                score += 12m;
            }

            if (workMentionsJavaScript || selectedWorkIsJavaScript)
            {
                if (agentHasDirectJavaScriptIdentity)
                {
                    score += selectedWorkIsJavaScript ? 280m : 80m;
                }
                else if (agentMentionsJavaScript &&
                         !agentHasDirectBlazorIdentity &&
                         !agentHasDirectDotNetIdentity)
                {
                    score += selectedWorkIsJavaScript ? 140m : 50m;
                }

                if (selectedWorkIsJavaScript &&
                    !agentHasDirectJavaScriptIdentity &&
                    (agentHasDirectDotNetIdentity ||
                     agentHasDirectBlazorIdentity ||
                     HasCapability(capabilityNames, WorkspaceDotnetBuildCapability)))
                {
                    score -= 160m;
                }
            }

            if (workMentionsBlazor)
            {
                if (agentHasDirectBlazorIdentity)
                {
                    score += 160m;
                }
                else if (agentHasDirectDotNetIdentity ||
                         agentMentionsBlazor ||
                         agentMentionsDotNet)
                {
                    score += 80m;
                }

                if (agentHasDirectJavaScriptIdentity &&
                    !agentHasDirectDotNetIdentity &&
                    !agentHasDirectBlazorIdentity)
                {
                    score -= 90m;
                }
            }
            else if (workMentionsDotNet)
            {
                if (agentHasDirectDotNetIdentity)
                {
                    score += 120m;
                }
                else if (agentMentionsDotNet)
                {
                    score += 60m;
                }

                if (agentHasDirectJavaScriptIdentity &&
                    !agentHasDirectDotNetIdentity)
                {
                    score -= 70m;
                }

                if (selectedWorkIsNonBlazorDotNet &&
                    agentHasDirectJavaScriptIdentity &&
                    !agentHasDirectDotNetIdentity)
                {
                    score -= 130m;
                }
            }

            if (RoleMentions(agentText, "programming"))
            {
                score -= 16m;
            }
        }

        if (RoleMentions(primaryRoleText, "security"))
        {
            score += RoleMentions(agentText, "security") ? 120m : -24m;
            if (!RoleMentions(agentText, "security") &&
                MentionsTechnicalImplementationIdentity(agentText))
            {
                score -= 90m;
            }
        }

        if (RoleMentions(primaryRoleText, "release"))
        {
            if (RoleMentions(agentText, "release") ||
                RoleMentions(agentText, "readiness") ||
                RoleMentions(agentText, "rollout"))
            {
                score += 120m;
            }
            else if (MentionsTechnicalImplementationIdentity(agentText))
            {
                score -= 90m;
            }
        }

        if (isProductOwnerRole)
        {
            if (RoleMentions(agentText, "product") ||
                RoleMentions(agentText, "business") ||
                RoleMentions(agentText, "requirements") ||
                RoleMentions(agentText, "strategy") ||
                RoleMentions(agentText, "stakeholder") ||
                RoleMentions(agentText, "scope"))
            {
                score += 150m;
            }

            if (MentionsTechnicalImplementationIdentity(agentText))
            {
                score -= 520m;
            }
        }

        if (isDeliveryManagerRole)
        {
            if (RoleMentions(agentText, "portfolio") ||
                RoleMentions(agentText, "governance") ||
                RoleMentions(agentText, "delivery") ||
                RoleMentions(agentText, "readiness") ||
                RoleMentions(agentText, "release") ||
                RoleMentions(agentText, "coordination") ||
                RoleMentions(agentText, "manager"))
            {
                score += 140m;
            }

            if (MentionsTechnicalImplementationIdentity(agentText))
            {
                score -= 420m;
            }
        }

        if (isArchitectureRole)
        {
            if (RoleMentions(aiFact.DisplayName, "architect") ||
                RoleMentions(aiFact.RoleTitle, "architect") ||
                RoleMentions(aiFact.TemplateKey, "architect"))
            {
                score += 90m;
            }
            else if (RoleMentions(agentText, "architect") ||
                     HasCapability(capabilityNames, ArchitectureSourceRagCapability))
            {
                score += 24m;
            }

            if (RoleMentions(agentText, "programming"))
            {
                score -= 40m;
            }

            if (workMentionsBlazor)
            {
                if (agentMentionsBlazor)
                {
                    score += 180m;
                }

                if (agentMentionsDotNet ||
                    HasCapability(capabilityNames, WorkspaceDotnetBuildCapability))
                {
                    score += 90m;
                }

                if (agentMentionsJavaScript &&
                    !agentMentionsBlazor &&
                    !agentMentionsDotNet)
                {
                    score -= 100m;
                }
            }

            if (workMentionsDotNet)
            {
                if (agentMentionsDotNet ||
                    HasCapability(capabilityNames, WorkspaceDotnetBuildCapability))
                {
                    score += 80m;
                }

                if (agentMentionsJavaScript &&
                    !agentMentionsBlazor &&
                    !agentMentionsDotNet)
                {
                    score -= 70m;
                }

                if (selectedWorkIsNonBlazorDotNet &&
                    agentHasDirectJavaScriptIdentity &&
                    !agentHasDirectDotNetIdentity)
                {
                    score -= 150m;
                }
            }

            if (selectedWorkIsJavaScript)
            {
                if (agentHasDirectJavaScriptIdentity)
                {
                    score += 560m;
                }
                else if (agentMentionsJavaScript &&
                         !agentHasDirectBlazorIdentity &&
                         !agentHasDirectDotNetIdentity)
                {
                    score += 220m;
                }

                if (!agentHasDirectJavaScriptIdentity &&
                    (agentHasDirectDotNetIdentity ||
                     agentHasDirectBlazorIdentity ||
                     HasCapability(capabilityNames, WorkspaceDotnetBuildCapability)))
                {
                    score -= 260m;
                }
            }
            else if (workMentionsJavaScript &&
                     !workMentionsBlazor &&
                     !workMentionsDotNet &&
                     agentMentionsJavaScript)
            {
                score += 65m;
            }
        }

        return score;
    }

    private static IReadOnlyList<string?> TakeRoleSpecificContext(IReadOnlyList<string?> additionalRoleContext)
    {
        if (additionalRoleContext.Count <= RoleSpecificContextItemCount)
        {
            return additionalRoleContext;
        }

        return additionalRoleContext
            .Take(RoleSpecificContextItemCount)
            .ToList();
    }

    private static bool MentionsTechnicalImplementationIdentity(string agentText)
    {
        return RoleMentions(agentText, "programming") ||
               RoleMentions(agentText, "developer") ||
               RoleMentions(agentText, "engineer") ||
               RoleMentions(agentText, "implements") ||
               MentionsBlazorStack(agentText) ||
               MentionsDotNetStack(agentText) ||
               MentionsJavaScriptStack(agentText);
    }

    private async Task<IReadOnlyList<string?>> BuildLaunchRoleContextAsync(
        AppDbContext dbContext,
        ProcessLaunchPlan plan,
        Project? project,
        ProcessProjectStructureContext? projectStructureContext,
        CancellationToken cancellationToken)
    {
        var context = new List<string?>
        {
            plan.Name,
            ProcessProjectStructureContextFormatter.RemoveSerializedContext(plan.TriggerReason)
        };

        if (projectStructureContext is null)
        {
            context.Add(project?.Name);
            context.Add(project?.Description);
            context.Add(project?.Objective);
            context.Add(project?.CurrentPhase);
        }

        if (projectStructureContext is not null)
        {
            context.Add(projectStructureContext.NodeTitle);
            context.Add(projectStructureContext.ParentNodeTitle);
            context.Add(projectStructureContext.ResolveTargetNodeTitle());
        }

        if (plan.ProjectId.HasValue)
        {
            context.AddRange(await projectStructureBridge.ListLaunchContextAsync(
                dbContext,
                plan.ProjectId.Value,
                projectStructureContext,
                cancellationToken));
        }

        return context;
    }

    private static decimal ScorePreferredExecutorFit(string preferredExecutorKind, ProcessLaunchCandidate candidate)
    {
        if (string.IsNullOrWhiteSpace(preferredExecutorKind))
        {
            return 0m;
        }

        var prefersPerson = preferredExecutorKind.Contains("person", StringComparison.OrdinalIgnoreCase);
        var prefersAgent = preferredExecutorKind.Contains("agent", StringComparison.OrdinalIgnoreCase) ||
                           preferredExecutorKind.Contains("ai", StringComparison.OrdinalIgnoreCase);
        var candidateIsAi = candidate.CandidateKind is ProcessLaunchCandidateKind.AiResource or ProcessLaunchCandidateKind.NewAiAgentProposal ||
                            candidate.ExecutorKind.Contains("agent", StringComparison.OrdinalIgnoreCase) ||
                            candidate.ExecutorKind.Contains("ai", StringComparison.OrdinalIgnoreCase);

        if (prefersPerson && !prefersAgent)
        {
            return candidateIsAi ? -10m : 10m;
        }

        if (prefersAgent && !prefersPerson)
        {
            return candidateIsAi ? 10m : -6m;
        }

        return candidateIsAi ? 2m : 1m;
    }

    private static IReadOnlyList<string> BuildRoleMatchingKeywords(
        string displayName,
        string roleKey,
        string preferredExecutorKind,
        IReadOnlyList<string?> additionalRoleContext)
    {
        return new[] { displayName, roleKey, preferredExecutorKind }
            .Concat(additionalRoleContext)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .SelectMany(item => item!.Split([' ', '-', '/', '_', ',', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(IsMeaningfulRoleKeyword)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> BuildRoleIdentityKeywords(string displayName, string roleKey)
    {
        return new[] { displayName, roleKey }
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .SelectMany(item => item!.Split([' ', '-', '/', '_', ',', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(IsMeaningfulRoleKeyword)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsMeaningfulRoleKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword) || RoleKeywordStopWords.Contains(keyword))
        {
            return false;
        }

        return keyword.Length >= 3 ||
               keyword.Equals("qa", StringComparison.OrdinalIgnoreCase) ||
               keyword.Equals("ui", StringComparison.OrdinalIgnoreCase) ||
               keyword.Equals("hr", StringComparison.OrdinalIgnoreCase) ||
               keyword.Equals("ai", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountRoleKeywordMatches(string text, IReadOnlyList<string> keywords)
    {
        if (string.IsNullOrWhiteSpace(text) || keywords.Count == 0)
        {
            return 0;
        }

        return keywords.Count(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildRoleFitText(
        string displayName,
        string roleKey,
        IReadOnlyList<string?> additionalRoleContext)
    {
        return string.Join(
            ' ',
            new[] { displayName, roleKey }
                .Concat(additionalRoleContext)
                .Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    private static string BuildAgentFitText(AiAgentStaffingFactListItemModel aiFact)
    {
        return string.Join(
            ' ',
            new[]
            {
                aiFact.DisplayName,
                aiFact.RoleTitle,
                aiFact.Summary,
                aiFact.Instructions,
                aiFact.TemplateKey,
                string.Join(' ', aiFact.Tags),
                string.Join(' ', aiFact.Capabilities.Select(item => item.Name))
            }.Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    private static bool RoleMentions(string text, string value)
    {
        return !string.IsNullOrWhiteSpace(text) &&
               text.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MentionsBlazorStack(string text)
    {
        return ContainsAffirmativeStackToken(text, "blazor") ||
               ContainsAffirmativeStackToken(text, "razor");
    }

    private static bool MentionsDotNetStack(string text)
    {
        return ContainsAffirmativeStackToken(text, ".net") ||
               ContainsAffirmativeStackToken(text, "dotnet") ||
               ContainsAffirmativeStackToken(text, "c#") ||
               ContainsAffirmativeStackToken(text, "csharp");
    }

    private static bool MentionsJavaScriptStack(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsAffirmativeStackToken(text, "javascript") ||
               ContainsAffirmativeStackToken(text, "typescript") ||
               ContainsAffirmativeStackPattern(
                   text,
                   @"(?:^|[^a-z0-9])(?:js|mjs|cjs|node\.?js|npm|vite|react|vue|svelte)(?:[^a-z0-9]|$)");
    }

    private static bool MentionsNonBlazorDotNetWork(string text)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            !MentionsDotNetStack(text) ||
            MentionsBlazorStack(text))
        {
            return false;
        }

        return ContainsNegatedStackToken(text, "blazor") ||
               ContainsNegatedStackToken(text, "razor") ||
               ContainsAffirmativeStackPattern(
                   text,
                   @"(?:^|[^a-z0-9])(?:console\s+app|cli\s+app|command[-\s]+line\s+app|minimal\s+api|web\s+api|rest\s+api|worker\s+service|background\s+service|class\s+library)(?:[^a-z0-9]|$)") ||
               Regex.IsMatch(
                   text,
                   @"\b(?:not|no|without)\s+(?:a\s+)?browser\s+ui\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool MentionsStaticClientWebWork(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var hasWebSurface =
            ContainsAffirmativeStackPattern(
                text,
                @"(?:^|[^a-z0-9])(?:web\s+page|website|web\s+site|browser\s+(?:app|application|game|ui)|frontend|front[-\s]+end|single[-\s]+page\s+(?:app|application)|spa|webhosting)(?:[^a-z0-9]|$)");
        var hasStaticOrClientOnlyConstraint =
            ContainsAffirmativeStackPattern(
                text,
                @"(?:^|[^a-z0-9])(?:static\s+(?:site|website|web\s+site|web\s+page|web\s+hosting|hosting|webhosting)|client[-\s]+side|local\s+storage|localstorage|no\s+backend|without\s+(?:a\s+)?backend|backend[-\s]+free)(?:[^a-z0-9]|$)");

        return hasWebSurface && hasStaticOrClientOnlyConstraint;
    }

    private static bool ContainsNegatedStackToken(string text, string token)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var pattern = $@"(?<![A-Za-z0-9_]){Regex.Escape(token)}(?![A-Za-z0-9_])";
        return Regex
            .Matches(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Any(match => IsNegatedStackMention(text, match.Index));
    }

    private static bool ContainsAffirmativeStackToken(string text, string token)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var pattern = $@"(?<![A-Za-z0-9_]){Regex.Escape(token)}(?![A-Za-z0-9_])";
        return ContainsAffirmativeStackPattern(text, pattern);
    }

    private static bool ContainsAffirmativeStackPattern(string text, string pattern)
    {
        foreach (Match match in Regex.Matches(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            if (!IsNegatedStackMention(text, match.Index))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNegatedStackMention(string text, int matchIndex)
    {
        var prefixStart = Math.Max(0, matchIndex - 64);
        var prefix = text[prefixStart..matchIndex];
        return Regex.IsMatch(
            prefix,
            @"(?:\bnot\s+(?:a\s+)?$|\bno\s+$|\bnon[-\s]+$|\bnegated\s+$|\bwithout\s+$|\bnever\s+$|\bdo\s+not\s+(?:use\s+|call\s+|default\s+to\s+)?[^.;:\r\n]{0,48}$|\bdon't\s+(?:use\s+|call\s+|default\s+to\s+)?[^.;:\r\n]{0,48}$|\bmust\s+not\s+(?:use\s+|call\s+|default\s+to\s+)?[^.;:\r\n]{0,48}$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ContextHasExclusiveStackSignal(
        IReadOnlyList<string?> additionalRoleContext,
        Func<string, bool> includedStack,
        params Func<string, bool>[] excludedStacks)
    {
        foreach (var contextItem in additionalRoleContext)
        {
            if (string.IsNullOrWhiteSpace(contextItem) ||
                !includedStack(contextItem))
            {
                continue;
            }

            if (excludedStacks.Any(excludedStack => excludedStack(contextItem)))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool HasCapability(IReadOnlyCollection<string> capabilityNames, string capabilityKey)
    {
        var normalizedCapabilityKey = NormalizeRoleFitToken(capabilityKey);
        return capabilityNames.Any(item =>
            string.Equals(NormalizeRoleFitToken(item), normalizedCapabilityKey, StringComparison.Ordinal));
    }

    private static bool TextEqualsNormalized(string left, string right)
    {
        return string.Equals(NormalizeRoleFitToken(left), NormalizeRoleFitToken(right), StringComparison.Ordinal);
    }

    private static string NormalizeRoleFitToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
    }
}
