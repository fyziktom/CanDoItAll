using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private async Task<ProcessLaunchCandidateSet> BuildLaunchCandidateSetAsync(
        AppDbContext dbContext,
        ProcessLaunchPlan plan,
        PublishedProcessLaunchContext publishedContext,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var projectAssignments = projectId.HasValue
            ? await projectPartyIntegrationBridge.ListAssignmentsDetailedAsync(projectId.Value, cancellationToken)
            : [];
        var projectAssignmentsByRole = projectAssignments
            .GroupBy(item => item.Role)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProjectPartyAssignmentDetail>)group
                    .OrderByDescending(item => item.IsPrimary)
                    .ThenBy(item => item.PartyDisplayName)
                    .ToList());
        var aiDirectory = await aiAgentService.ListAgentDirectorySnapshotAsync(dbContext, cancellationToken);
        var aiDirectoryByPartyId = aiDirectory.ToDictionary(item => item.PartyId);
        var aiStaffingFactsByPartyId = (await aiAgentService.ListAgentStaffingFactsSnapshotAsync(
                aiDirectory.Select(item => item.PartyId).Distinct().ToList(),
                cancellationToken))
            .ToDictionary(item => item.PartyId);
        var roleSkillIds = publishedContext.RoleSkillsByRoleId.Values
            .SelectMany(item => item)
            .Select(item => item.SkillId)
            .Distinct()
            .ToList();
        var skillNamesById = roleSkillIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<SkillDefinition>()
                .Where(item => roleSkillIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var roleRecommendations = new List<ProcessLaunchRoleRecommendation>(publishedContext.Roles.Count);

        foreach (var role in publishedContext.Roles)
        {
            var requiredSkillIds = publishedContext.RoleSkillsByRoleId.GetValueOrDefault(role.Id) ?? [];
            var requiredSkillIdValues = requiredSkillIds
                .Select(item => item.SkillId)
                .Distinct()
                .ToList();
            var candidateList = await BuildCandidatesForRoleAsync(
                dbContext,
                plan,
                role,
                requiredSkillIds,
                projectAssignmentsByRole,
                aiDirectory,
                aiDirectoryByPartyId,
                cancellationToken);
            ApplyLaunchRoleRecommendation(
                role,
                requiredSkillIdValues,
                skillNamesById,
                aiStaffingFactsByPartyId,
                candidateList);
            var selectedCandidate = candidateList
                .OrderByDescending(item => item.IsRecommended)
                .ThenByDescending(item => item.Score)
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (selectedCandidate is null)
            {
                selectedCandidate = CreateGapCandidate(role);
                candidateList.Add(selectedCandidate);
            }

            var roleEntity = new ProcessLaunchPlanRole
            {
                LaunchPlanId = plan.Id,
                RoleRequirementId = role.Id,
                RoleKey = role.Key,
                DisplayName = role.DisplayName,
                PreferredExecutorKind = role.PreferredExecutorKind,
                RequiredSkillIdsJson = JsonSerializer.Serialize(
                    requiredSkillIdValues,
                    LaunchJsonOptions),
                RecommendationSummary = BuildLaunchRecommendationSummary(candidateList),
                SelectionSummary = ResolveLaunchSelectionSummary(selectedCandidate),
                ReadinessSummary = ResolveLaunchReadinessSummary(selectedCandidate, "Recommended"),
                SelectedCandidateId = selectedCandidate.Id,
                IsRequired = role.IsRequired,
                RequiresExplicitApproval = role.RequiresExplicitApproval,
                RequiresProvisioning = selectedCandidate.RequiresProvisioning,
                IsResolved = selectedCandidate.CandidateKind != ProcessLaunchCandidateKind.Gap,
                DisplayOrder = role.DisplayOrder
            };

            foreach (var candidate in candidateList)
            {
                candidate.LaunchPlanRoleId = roleEntity.Id;
            }

            roleRecommendations.Add(new ProcessLaunchRoleRecommendation(roleEntity, candidateList));
        }

        return new ProcessLaunchCandidateSet(roleRecommendations);
    }

    private async Task<List<ProcessLaunchCandidate>> BuildCandidatesForRoleAsync(
        AppDbContext dbContext,
        ProcessLaunchPlan plan,
        ProcessRoleRequirement role,
        IReadOnlyList<ProcessRoleSkillRequirement> requiredSkills,
        IReadOnlyDictionary<ProjectPartyAssignmentRole, IReadOnlyList<ProjectPartyAssignmentDetail>> projectAssignmentsByRole,
        IReadOnlyList<AiAgentListItemModel> aiDirectory,
        IReadOnlyDictionary<Guid, AiAgentListItemModel> aiDirectoryByPartyId,
        CancellationToken cancellationToken)
    {
        var candidates = new List<ProcessLaunchCandidate>();
        var requiredSkillIds = requiredSkills
            .Where(item => item.IsRequired)
            .Select(item => item.SkillId)
            .Distinct()
            .ToList();
        var seenPartyIds = new HashSet<Guid>();
        var requiresTechnicalAgentBinding = RequiresTechnicalAgentBinding(plan);
        var includeAiDirectoryCandidates = IsAiRole(role) || requiresTechnicalAgentBinding;

        var preferredProjectRole = role.PreferredProjectAssignmentRole
            ?? (IsAiRole(role) ? ProjectPartyAssignmentRole.AiAgent : ProjectPartyAssignmentRole.TeamMember);
        projectAssignmentsByRole.TryGetValue(preferredProjectRole, out var matchingAssignments);
        var assignmentMatchedSkillsByPartyId = await LoadMatchedSkillsByPartyIdAsync(
            dbContext,
            matchingAssignments?.Select(item => item.PartyId).Distinct().ToList() ?? [],
            requiredSkillIds,
            cancellationToken);

        if (matchingAssignments is not null)
        {
            foreach (var assignment in matchingAssignments)
            {
                assignmentMatchedSkillsByPartyId.TryGetValue(assignment.PartyId, out var matchedSkillSet);
                var matchedSkillCount = matchedSkillSet?.Count ?? 0;
                aiDirectoryByPartyId.TryGetValue(assignment.PartyId, out var linkedAiResource);
                var requiresProvisioning = requiresTechnicalAgentBinding && !HasBoundTechnicalAgent(linkedAiResource);
                var metadata = BuildLaunchProvisioningMetadata(
                    role,
                    requiredSkillIds,
                    assignment.PartyDisplayName,
                    assignment.PartyId);
                candidates.Add(new ProcessLaunchCandidate
                {
                    CandidateKind = ProcessLaunchCandidateKind.ProjectAssignment,
                    PartyId = assignment.PartyId,
                    TechnicalAgentId = linkedAiResource?.TechnicalAgentId,
                    DisplayName = assignment.PartyDisplayName,
                    ExecutorKind = assignment.PartyTypeLabel,
                    Score = (assignment.IsPrimary ? 100m : 92m) + matchedSkillCount * 2m - (requiresProvisioning ? 36m : 0m),
                    IsRecommended = assignment.IsPrimary && !requiresProvisioning,
                    AllowsDirectMessaging = true,
                    RequiresProvisioning = requiresProvisioning,
                    RecommendationSummary = requiredSkillIds.Count == 0
                        ? $"Matched project assignment role {assignment.Role}."
                        : matchedSkillCount > 0
                            ? $"Matched project assignment role {assignment.Role} and {matchedSkillCount} required skill(s)."
                            : $"Matched project assignment role {assignment.Role}, but none of the explicit required skills are currently recorded on the assigned party.",
                    AvailabilitySummary = requiresProvisioning
                        ? "Project assignment exists, but a runnable internal AI resource must be provisioned before execution."
                        : "Project assignment is already attached to the project.",
                    SourceRegistryKey = $"project-assignment:{assignment.Id:D}",
                    MetadataJson = metadata,
                    CreatedAtUtc = clock.GetUtcNow()
                });
                seenPartyIds.Add(assignment.PartyId);
            }
        }

        if (includeAiDirectoryCandidates)
        {
            var aiMatchedSkillsByPartyId = await LoadMatchedSkillsByPartyIdAsync(
                dbContext,
                aiDirectory.Select(item => item.PartyId).Distinct().ToList(),
                requiredSkillIds,
                cancellationToken);
            var hasRecommendedCandidate = candidates.Any(item => item.IsRecommended);
            var hasReadySkillMatchedAiCandidate = false;

            foreach (var aiResource in aiDirectory
                         .Where(item => !seenPartyIds.Contains(item.PartyId))
                         .OrderByDescending(item => item.BindingStatus == AiResourceBindingStatus.Bound)
                         .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                aiMatchedSkillsByPartyId.TryGetValue(aiResource.PartyId, out var matchedSkillSet);
                var matchedSkillCount = matchedSkillSet?.Count ?? 0;
                var hasAllRequiredSkills = requiredSkillIds.Count == 0 || matchedSkillCount == requiredSkillIds.Count;
                var isReadyBoundCandidate =
                    aiResource.BindingStatus == AiResourceBindingStatus.Bound &&
                    aiResource.TechnicalAgentId.HasValue &&
                    hasAllRequiredSkills;
                var isRecommended = !hasRecommendedCandidate && isReadyBoundCandidate;

                if (isReadyBoundCandidate)
                {
                    hasReadySkillMatchedAiCandidate = true;
                }

                candidates.Add(new ProcessLaunchCandidate
                {
                    CandidateKind = ProcessLaunchCandidateKind.AiResource,
                    PartyId = aiResource.PartyId,
                    TechnicalAgentId = aiResource.TechnicalAgentId,
                    DisplayName = aiResource.DisplayName,
                    ExecutorKind = "AI agent",
                    Score = ResolveAiResourceScore(aiResource, matchedSkillCount, requiredSkillIds.Count),
                    IsRecommended = isRecommended,
                    AllowsDirectMessaging = true,
                    RequiresProvisioning = !aiResource.TechnicalAgentId.HasValue || aiResource.BindingStatus != AiResourceBindingStatus.Bound,
                    RecommendationSummary = BuildAiResourceRecommendationSummary(aiResource, matchedSkillCount, requiredSkillIds.Count),
                    AvailabilitySummary = string.IsNullOrWhiteSpace(aiResource.ProviderName)
                        ? "AI resource is available in the directory."
                        : $"{aiResource.ProviderName} / {aiResource.DefaultModel}",
                    SourceRegistryKey = $"crmhr-ai-agent:{aiResource.PartyId:D}",
                    MetadataJson = BuildLaunchProvisioningMetadata(
                        role,
                        requiredSkillIds,
                        aiResource.DisplayName,
                        aiResource.PartyId),
                    CreatedAtUtc = clock.GetUtcNow()
                });
                hasRecommendedCandidate = hasRecommendedCandidate || isRecommended;
                seenPartyIds.Add(aiResource.PartyId);
            }

            var recommendNewAiProposal = !hasRecommendedCandidate &&
                                         (!hasReadySkillMatchedAiCandidate || requiresTechnicalAgentBinding);
            candidates.Add(new ProcessLaunchCandidate
            {
                CandidateKind = ProcessLaunchCandidateKind.NewAiAgentProposal,
                DisplayName = $"{role.DisplayName} AI agent",
                ExecutorKind = "AI agent",
                Score = recommendNewAiProposal
                    ? 86m
                    : candidates.Count == 0
                        ? 72m
                        : 48m,
                IsRecommended = recommendNewAiProposal || (candidates.Count == 0 && requiredSkillIds.Count == 0),
                AllowsDirectMessaging = true,
                RequiresProvisioning = true,
                RecommendationSummary = "Provision a new technical AI resource for this process role.",
                AvailabilitySummary = "A new CRM-HR AI resource and AgentFramework binding will be created during provisioning.",
                SourceRegistryKey = $"launch-proposal:{plan.Id:D}:{role.Id:D}",
                MetadataJson = BuildLaunchProvisioningMetadata(
                    role,
                    requiredSkillIds,
                    $"{role.DisplayName} AI agent"),
                CreatedAtUtc = clock.GetUtcNow()
            });
        }

        var staffingSearchText = string.IsNullOrWhiteSpace(role.DisplayName)
            ? role.Key
            : role.DisplayName;
        var staffingCandidates = await hrService.SearchStaffingCandidatesAsync(
            requiredSkillIds.FirstOrDefault(),
            staffingSearchText,
            cancellationToken: cancellationToken);
        var staffingPartyIds = staffingCandidates
            .Where(item => !seenPartyIds.Contains(item.PartyId))
            .Select(item => item.PartyId)
            .Distinct()
            .ToList();
        var matchedSkillsByPartyId = await LoadMatchedSkillsByPartyIdAsync(
            dbContext,
            staffingPartyIds,
            requiredSkillIds,
            cancellationToken);

        foreach (var staffingCandidate in staffingCandidates.Where(item => !seenPartyIds.Contains(item.PartyId)))
        {
            matchedSkillsByPartyId.TryGetValue(staffingCandidate.PartyId, out var matchedSkillSet);
            var matchedSkillCount = matchedSkillSet?.Count ?? 0;
            aiDirectoryByPartyId.TryGetValue(staffingCandidate.PartyId, out var staffingAiResource);
            var requiresProvisioning = requiresTechnicalAgentBinding && !HasBoundTechnicalAgent(staffingAiResource);
            candidates.Add(new ProcessLaunchCandidate
            {
                CandidateKind = ProcessLaunchCandidateKind.Workforce,
                PartyId = staffingCandidate.PartyId,
                TechnicalAgentId = staffingAiResource?.TechnicalAgentId,
                DisplayName = staffingCandidate.DisplayName,
                ExecutorKind = string.IsNullOrWhiteSpace(staffingCandidate.JobTitle)
                    ? staffingCandidate.PartyType.ToString()
                    : staffingCandidate.JobTitle,
                Score = 60m + staffingCandidate.AvailablePercent / 5m + matchedSkillCount * 7m,
                IsRecommended = !requiresProvisioning && candidates.Count == 0,
                AllowsDirectMessaging = true,
                RequiresProvisioning = requiresProvisioning,
                RecommendationSummary = matchedSkillCount > 0
                    ? $"Matched {matchedSkillCount} required skill(s) for this process role."
                    : "Matched the workforce directory using role title and availability.",
                AvailabilitySummary = requiresProvisioning
                    ? $"{staffingCandidate.AvailabilityState} / {staffingCandidate.AvailablePercent:0.#}% available. A runnable internal AI resource will be provisioned before execution."
                    : $"{staffingCandidate.AvailabilityState} / {staffingCandidate.AvailablePercent:0.#}% available",
                SourceRegistryKey = $"crmhr-workforce:{staffingCandidate.PartyId:D}",
                MetadataJson = BuildLaunchProvisioningMetadata(
                    role,
                    requiredSkillIds,
                    staffingCandidate.DisplayName,
                    staffingCandidate.PartyId),
                CreatedAtUtc = clock.GetUtcNow()
            });
            seenPartyIds.Add(staffingCandidate.PartyId);
        }

        if (candidates.Count == 0)
        {
            var broaderStaffingCandidates = await hrService.SearchStaffingCandidatesAsync(
                null,
                staffingSearchText,
                cancellationToken: cancellationToken);
            var broaderPartyIds = broaderStaffingCandidates
                .Where(item => !seenPartyIds.Contains(item.PartyId))
                .Select(item => item.PartyId)
                .Distinct()
                .ToList();
            var broaderMatchedSkillsByPartyId = await LoadMatchedSkillsByPartyIdAsync(
                dbContext,
                broaderPartyIds,
                requiredSkillIds,
                cancellationToken);

            foreach (var staffingCandidate in broaderStaffingCandidates.Where(item => !seenPartyIds.Contains(item.PartyId)))
            {
                broaderMatchedSkillsByPartyId.TryGetValue(staffingCandidate.PartyId, out var matchedSkillSet);
                var matchedSkillCount = matchedSkillSet?.Count ?? 0;
                aiDirectoryByPartyId.TryGetValue(staffingCandidate.PartyId, out var broaderAiResource);
                var requiresProvisioning = requiresTechnicalAgentBinding && !HasBoundTechnicalAgent(broaderAiResource);
                candidates.Add(new ProcessLaunchCandidate
                {
                    CandidateKind = ProcessLaunchCandidateKind.Workforce,
                    PartyId = staffingCandidate.PartyId,
                    TechnicalAgentId = broaderAiResource?.TechnicalAgentId,
                    DisplayName = staffingCandidate.DisplayName,
                    ExecutorKind = string.IsNullOrWhiteSpace(staffingCandidate.JobTitle)
                        ? staffingCandidate.PartyType.ToString()
                        : staffingCandidate.JobTitle,
                    Score = 48m + staffingCandidate.AvailablePercent / 5m + matchedSkillCount * 7m,
                    IsRecommended = !requiresProvisioning && candidates.Count == 0,
                    AllowsDirectMessaging = true,
                    RequiresProvisioning = requiresProvisioning,
                    RecommendationSummary = matchedSkillCount > 0
                        ? $"Broad workforce fallback matched {matchedSkillCount} required skill(s) for this role."
                        : "Broad workforce fallback matched the CRM-HR directory by role wording and current availability.",
                    AvailabilitySummary = requiresProvisioning
                        ? $"{staffingCandidate.AvailabilityState} / {staffingCandidate.AvailablePercent:0.#}% available. A runnable internal AI resource will be provisioned before execution."
                        : $"{staffingCandidate.AvailabilityState} / {staffingCandidate.AvailablePercent:0.#}% available",
                    SourceRegistryKey = $"crmhr-workforce-broad:{staffingCandidate.PartyId:D}",
                    MetadataJson = BuildLaunchProvisioningMetadata(
                        role,
                        requiredSkillIds,
                        staffingCandidate.DisplayName,
                        staffingCandidate.PartyId),
                    CreatedAtUtc = clock.GetUtcNow()
                });
                seenPartyIds.Add(staffingCandidate.PartyId);
            }
        }

        if (candidates.Count == 0)
        {
            candidates.Add(CreateGapCandidate(role));
        }

        return candidates
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ApplyLaunchRoleRecommendation(
        ProcessRoleRequirement role,
        IReadOnlyList<Guid> requiredSkillIds,
        IReadOnlyDictionary<Guid, string> skillNames,
        IReadOnlyDictionary<Guid, AiAgentStaffingFactListItemModel> aiFactsByPartyId,
        List<ProcessLaunchCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return;
        }

        foreach (var candidate in candidates)
        {
            candidate.Score = ScoreCandidateForHrManager(
                role,
                candidate,
                requiredSkillIds,
                skillNames,
                aiFactsByPartyId);
            candidate.IsRecommended = false;
        }

        var selectedCandidate = candidates
            .OrderByDescending(item => item.CandidateKind != ProcessLaunchCandidateKind.Gap)
            .ThenByDescending(item => item.Score)
            .ThenBy(item => item.RequiresProvisioning)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .First();

        selectedCandidate.IsRecommended = true;
    }

    private static async Task<Dictionary<Guid, HashSet<Guid>>> LoadMatchedSkillsByPartyIdAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> partyIds,
        IReadOnlyCollection<Guid> requiredSkillIds,
        CancellationToken cancellationToken)
    {
        if (partyIds.Count == 0 || requiredSkillIds.Count == 0)
        {
            return [];
        }

        return await dbContext.Set<PartySkill>()
            .Where(item => partyIds.Contains(item.PartyId) && requiredSkillIds.Contains(item.SkillId))
            .GroupBy(item => item.PartyId)
            .ToDictionaryAsync(
                group => group.Key,
                group => group.Select(item => item.SkillId).ToHashSet(),
                cancellationToken);
    }

    private static decimal ResolveAiResourceScore(
        AiAgentListItemModel aiResource,
        int matchedSkillCount,
        int requiredSkillCount)
    {
        var score = aiResource.BindingStatus == AiResourceBindingStatus.Bound ? 72m : 54m;
        score += matchedSkillCount * 10m;

        if (requiredSkillCount > 0)
        {
            if (matchedSkillCount == requiredSkillCount)
            {
                score += 8m;
            }
            else if (matchedSkillCount == 0)
            {
                score -= aiResource.BindingStatus == AiResourceBindingStatus.Bound ? 12m : 18m;
            }
        }

        return score;
    }

    private static string BuildAiResourceRecommendationSummary(
        AiAgentListItemModel aiResource,
        int matchedSkillCount,
        int requiredSkillCount)
    {
        var parts = new List<string>();
        parts.Add(string.IsNullOrWhiteSpace(aiResource.BindingSummary)
            ? "Discovered from the CRM-HR AI resource directory."
            : aiResource.BindingSummary.Trim());

        if (requiredSkillCount > 0)
        {
            parts.Add(matchedSkillCount > 0
                ? $"Matches {matchedSkillCount} of {requiredSkillCount} required skill(s)."
                : "Does not currently match the explicit required skills recorded for this role.");
        }

        return string.Join(" ", parts.Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    private static ProcessLaunchCandidate CreateGapCandidate(ProcessRoleRequirement role)
    {
        return new ProcessLaunchCandidate
        {
            CandidateKind = ProcessLaunchCandidateKind.Gap,
            DisplayName = $"Unresolved / {role.DisplayName}",
            ExecutorKind = string.IsNullOrWhiteSpace(role.PreferredExecutorKind) ? "Unresolved" : role.PreferredExecutorKind,
            Score = 0m,
            IsRecommended = true,
            AllowsDirectMessaging = false,
            RequiresProvisioning = false,
            RecommendationSummary = "No matching project assignment, workforce record, or AI resource is currently available.",
            AvailabilitySummary = "Manual correction is required before this role can proceed to approval.",
            SourceRegistryKey = $"launch-gap:{role.Id:D}",
            MetadataJson = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
