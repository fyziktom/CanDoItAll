using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

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

    public async Task<Result> MatchLaunchPlanWithHrManagerAsync(
        Guid launchPlanId,
        string requestedBy = "process-workspace",
        CancellationToken cancellationToken = default)
    {
        if (launchPlanId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("Launch plan is required.", "processes.launch.plan-required"));
        }

        // Keep projection repair outside the launch-plan transaction to avoid self-blocking SQLite writes.
        await aiAgentService.SynchronizeDirectoryProjectionAsync(cancellationToken);

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
            var aiFactsByPartyId = (await aiAgentService.ListAgentStaffingFactsSnapshotAsync(
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
                    .OrderByDescending(item => ScoreCandidateForHrManager(role, roleRequirement, item, requiredSkillIds, skillNames, aiFactsByPartyId, launchContext))
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

        var requiresTechnicalAgentBinding = RequiresTechnicalAgentBinding(plan);
        var seenPartyIds = existingCandidates
            .Where(item => item.PartyId.HasValue)
            .Select(item => item.PartyId!.Value)
            .ToHashSet();
        var aiDirectoryByPartyId = (await aiAgentService.ListAgentDirectorySnapshotAsync(dbContext, cancellationToken))
            .ToDictionary(item => item.PartyId);
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
            var requiresProvisioning = requiresTechnicalAgentBinding && !HasBoundTechnicalAgent(staffingAiResource);
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
                var requiresProvisioning = requiresTechnicalAgentBinding && !HasBoundTechnicalAgent(linkedAiResource);
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
        IReadOnlyList<string?> launchContext)
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
            aiFactsByPartyId);
    }

    private static decimal ScoreCandidateForHrManager(
        ProcessRoleRequirement role,
        ProcessLaunchCandidate candidate,
        IReadOnlyList<Guid> requiredSkillIds,
        IReadOnlyDictionary<Guid, string> skillNames,
        IReadOnlyDictionary<Guid, AiAgentStaffingFactListItemModel> aiFactsByPartyId,
        IReadOnlyList<string?> launchContext)
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
            aiFactsByPartyId);
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
        var workMentionsBlazor = RoleMentions(workText, "blazor") ||
                                 RoleMentions(workText, "razor");
        var workMentionsDotNet = RoleMentions(workText, ".net") ||
                                 RoleMentions(workText, "dotnet") ||
                                 RoleMentions(workText, "c#") ||
                                 RoleMentions(workText, "csharp");
        var workMentionsJavaScript = RoleMentions(workText, "javascript") ||
                                     RoleMentions(workText, "typescript");
        var agentMentionsBlazor = RoleMentions(aiFact.DisplayName, "blazor") ||
                                  RoleMentions(aiFact.RoleTitle, "blazor") ||
                                  RoleMentions(aiFact.TemplateKey, "blazor") ||
                                  RoleMentions(agentText, "blazor");
        var agentMentionsDotNet = RoleMentions(aiFact.DisplayName, ".net") ||
                                  RoleMentions(aiFact.DisplayName, "dotnet") ||
                                  RoleMentions(aiFact.RoleTitle, ".net") ||
                                  RoleMentions(aiFact.RoleTitle, "dotnet") ||
                                  RoleMentions(aiFact.TemplateKey, "dotnet") ||
                                  RoleMentions(agentText, ".net") ||
                                  RoleMentions(agentText, "dotnet") ||
                                  RoleMentions(agentText, "c#") ||
                                  RoleMentions(agentText, "csharp");
        var agentMentionsJavaScript = RoleMentions(aiFact.DisplayName, "javascript") ||
                                      RoleMentions(aiFact.RoleTitle, "javascript") ||
                                      RoleMentions(aiFact.TemplateKey, "javascript") ||
                                      RoleMentions(agentText, "javascript") ||
                                      RoleMentions(agentText, "typescript");

        if (TextEqualsNormalized(aiFact.DisplayName, displayName) ||
            TextEqualsNormalized(aiFact.RoleTitle, displayName))
        {
            score += 35m;
        }

        if (isImplementationRole)
        {
            if (HasCapability(capabilityNames, WorkspaceDotnetBuildCapability))
            {
                score += 72m;
            }

            if (HasCapability(capabilityNames, WorkspaceDotnetTestCapability))
            {
                score += 16m;
            }

            if (HasCapability(capabilityNames, WorkspaceDotnetRunCapability))
            {
                score += 12m;
            }

            if (HasCapability(capabilityNames, WorkspaceDotnetNewCapability))
            {
                score += 14m;
            }

            if (RoleMentions(agentText, "programming") ||
                RoleMentions(agentText, "implements"))
            {
                score += 30m;
            }

            if (workMentionsBlazor)
            {
                if (RoleMentions(aiFact.DisplayName, "blazor") ||
                    RoleMentions(aiFact.RoleTitle, "blazor") ||
                    RoleMentions(aiFact.TemplateKey, "blazor"))
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
                if (RoleMentions(aiFact.DisplayName, ".net") ||
                    RoleMentions(aiFact.DisplayName, "dotnet") ||
                    RoleMentions(aiFact.RoleTitle, ".net") ||
                    RoleMentions(aiFact.TemplateKey, "dotnet"))
                {
                    score += 45m;
                }
                else if (agentMentionsDotNet)
                {
                    score += 24m;
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

            if (RoleMentions(agentText, "programming"))
            {
                score -= 16m;
            }
        }

        if (RoleMentions(primaryRoleText, "security"))
        {
            score += RoleMentions(agentText, "security") ? 40m : -12m;
        }

        if (RoleMentions(primaryRoleText, "release"))
        {
            if (RoleMentions(agentText, "release") ||
                RoleMentions(agentText, "readiness") ||
                RoleMentions(agentText, "rollout"))
            {
                score += 50m;
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
                score += 70m;
            }

            if (MentionsTechnicalImplementationIdentity(agentText))
            {
                score -= 100m;
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
                score += 70m;
            }

            if (MentionsTechnicalImplementationIdentity(agentText))
            {
                score -= 90m;
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
            }

            if (workMentionsJavaScript &&
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
               RoleMentions(agentText, "blazor") ||
               RoleMentions(agentText, "dotnet") ||
               RoleMentions(agentText, ".net") ||
               RoleMentions(agentText, "javascript");
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
            ProcessProjectStructureContextFormatter.RemoveSerializedContext(plan.TriggerReason),
            project?.Name,
            project?.Description,
            project?.Objective,
            project?.CurrentPhase
        };

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
