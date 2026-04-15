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
        var aiDirectory = await aiAgentService.ListAgentDirectoryAsync(cancellationToken);
        var aiDirectoryByPartyId = aiDirectory.ToDictionary(item => item.PartyId);
        var roleRecommendations = new List<ProcessLaunchRoleRecommendation>(publishedContext.Roles.Count);

        foreach (var role in publishedContext.Roles)
        {
            var requiredSkillIds = publishedContext.RoleSkillsByRoleId.GetValueOrDefault(role.Id) ?? [];
            var candidateList = await BuildCandidatesForRoleAsync(
                dbContext,
                plan,
                role,
                requiredSkillIds,
                projectAssignmentsByRole,
                aiDirectory,
                aiDirectoryByPartyId,
                cancellationToken);
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
                    requiredSkillIds.Select(item => item.SkillId).Distinct().ToList(),
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

        var preferredProjectRole = role.PreferredProjectAssignmentRole
            ?? (IsAiRole(role) ? ProjectPartyAssignmentRole.AiAgent : ProjectPartyAssignmentRole.TeamMember);
        if (projectAssignmentsByRole.TryGetValue(preferredProjectRole, out var matchingAssignments))
        {
            foreach (var assignment in matchingAssignments)
            {
                var requiresProvisioning = IsAiRole(role) &&
                    (!aiDirectoryByPartyId.TryGetValue(assignment.PartyId, out var linkedAiResource) ||
                        !linkedAiResource.TechnicalAgentId.HasValue ||
                        linkedAiResource.BindingStatus != AiResourceBindingStatus.Bound);
                var metadata = BuildLaunchProvisioningMetadata(
                    role,
                    requiredSkillIds,
                    assignment.PartyDisplayName,
                    assignment.PartyId);
                candidates.Add(new ProcessLaunchCandidate
                {
                    CandidateKind = ProcessLaunchCandidateKind.ProjectAssignment,
                    PartyId = assignment.PartyId,
                    TechnicalAgentId = aiDirectoryByPartyId.TryGetValue(assignment.PartyId, out var aiAssignment)
                        ? aiAssignment.TechnicalAgentId
                        : null,
                    DisplayName = assignment.PartyDisplayName,
                    ExecutorKind = assignment.PartyTypeLabel,
                    Score = assignment.IsPrimary ? 100m : 92m,
                    IsRecommended = assignment.IsPrimary,
                    AllowsDirectMessaging = true,
                    RequiresProvisioning = requiresProvisioning,
                    RecommendationSummary = $"Matched project assignment role {assignment.Role}.",
                    AvailabilitySummary = requiresProvisioning
                        ? "Project assignment exists, but the technical AI binding must be provisioned before execution."
                        : "Project assignment is already attached to the project.",
                    SourceRegistryKey = $"project-assignment:{assignment.Id:D}",
                    MetadataJson = metadata,
                    CreatedAtUtc = clock.GetUtcNow()
                });
                seenPartyIds.Add(assignment.PartyId);
            }
        }

        if (IsAiRole(role))
        {
            foreach (var aiResource in aiDirectory
                         .Where(item => !seenPartyIds.Contains(item.PartyId))
                         .OrderByDescending(item => item.BindingStatus == AiResourceBindingStatus.Bound)
                         .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(new ProcessLaunchCandidate
                {
                    CandidateKind = ProcessLaunchCandidateKind.AiResource,
                    PartyId = aiResource.PartyId,
                    TechnicalAgentId = aiResource.TechnicalAgentId,
                    DisplayName = aiResource.DisplayName,
                    ExecutorKind = "AI agent",
                    Score = aiResource.BindingStatus == AiResourceBindingStatus.Bound ? 88m : 70m,
                    IsRecommended = aiResource.BindingStatus == AiResourceBindingStatus.Bound && candidates.Count == 0,
                    AllowsDirectMessaging = true,
                    RequiresProvisioning = !aiResource.TechnicalAgentId.HasValue || aiResource.BindingStatus != AiResourceBindingStatus.Bound,
                    RecommendationSummary = string.IsNullOrWhiteSpace(aiResource.BindingSummary)
                        ? "Discovered from the CRM-HR AI resource directory."
                        : aiResource.BindingSummary,
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
                seenPartyIds.Add(aiResource.PartyId);
            }

            candidates.Add(new ProcessLaunchCandidate
            {
                CandidateKind = ProcessLaunchCandidateKind.NewAiAgentProposal,
                DisplayName = $"{role.DisplayName} AI agent",
                ExecutorKind = "AI agent",
                Score = candidates.Count == 0 ? 72m : 48m,
                IsRecommended = candidates.Count == 0,
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

            return candidates
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
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
        var matchedSkillsByPartyId = staffingPartyIds.Count == 0 || requiredSkillIds.Count == 0
            ? new Dictionary<Guid, HashSet<Guid>>()
            : await dbContext.Set<PartySkill>()
                .Where(item => staffingPartyIds.Contains(item.PartyId) && requiredSkillIds.Contains(item.SkillId))
                .GroupBy(item => item.PartyId)
                .ToDictionaryAsync(
                    group => group.Key,
                    group => group.Select(item => item.SkillId).ToHashSet(),
                    cancellationToken);

        foreach (var staffingCandidate in staffingCandidates.Where(item => !seenPartyIds.Contains(item.PartyId)))
        {
            matchedSkillsByPartyId.TryGetValue(staffingCandidate.PartyId, out var matchedSkillSet);
            var matchedSkillCount = matchedSkillSet?.Count ?? 0;
            candidates.Add(new ProcessLaunchCandidate
            {
                CandidateKind = ProcessLaunchCandidateKind.Workforce,
                PartyId = staffingCandidate.PartyId,
                DisplayName = staffingCandidate.DisplayName,
                ExecutorKind = string.IsNullOrWhiteSpace(staffingCandidate.JobTitle)
                    ? staffingCandidate.PartyType.ToString()
                    : staffingCandidate.JobTitle,
                Score = 60m + staffingCandidate.AvailablePercent / 5m + matchedSkillCount * 7m,
                IsRecommended = candidates.Count == 0,
                AllowsDirectMessaging = true,
                RequiresProvisioning = false,
                RecommendationSummary = matchedSkillCount > 0
                    ? $"Matched {matchedSkillCount} required skill(s) for this process role."
                    : "Matched the workforce directory using role title and availability.",
                AvailabilitySummary = $"{staffingCandidate.AvailabilityState} / {staffingCandidate.AvailablePercent:0.#}% available",
                SourceRegistryKey = $"crmhr-workforce:{staffingCandidate.PartyId:D}",
                MetadataJson = "{}",
                CreatedAtUtc = clock.GetUtcNow()
            });
            seenPartyIds.Add(staffingCandidate.PartyId);
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
