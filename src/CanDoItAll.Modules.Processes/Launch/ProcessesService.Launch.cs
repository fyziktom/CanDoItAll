using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private static readonly JsonSerializerOptions LaunchJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ProcessAiDirectoryProjectionSyncTimeout = TimeSpan.FromSeconds(3);

    public Task<Result<Guid>> ExecuteLaunchPlanAsync(
        ProcessLaunchExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.LaunchPlanId == Guid.Empty)
        {
            return Task.FromResult(Result<Guid>.Failure(Error.Validation(
                "Launch plan is required.",
                "processes.launch.plan-required")));
        }

        return StartRunAsync(
            new ProcessRunStartRequest
            {
                LaunchPlanId = request.LaunchPlanId
            },
            cancellationToken);
    }

    private async Task<Dictionary<Guid, string>> LoadSkillNamesAsync(
        AppDbContext dbContext,
        IReadOnlyList<ProcessLaunchPlanRole> roles,
        CancellationToken cancellationToken)
    {
        var skillIds = roles
            .SelectMany(item => DeserializeGuidList(item.RequiredSkillIdsJson))
            .Distinct()
            .ToList();
        if (skillIds.Count == 0)
        {
            return [];
        }

        return await dbContext.Set<SkillDefinition>()
            .Where(item => skillIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
    }

    private static bool IsAiRole(ProcessRoleRequirement role)
    {
        if (IsWorkflowRole(role))
        {
            return false;
        }

        return role.PreferredProjectAssignmentRole == ProjectPartyAssignmentRole.AiAgent ||
               ProcessExecutorKindNames.IsAiAgent(role.PreferredExecutorKind) ||
               role.Key.Contains("ai", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAiRoleFromLaunchRole(ProcessLaunchPlanRole role)
    {
        return ProcessExecutorKindNames.IsAiAgent(role.PreferredExecutorKind);
    }

    private static bool IsWorkflowRole(ProcessRoleRequirement role)
    {
        return role.PreferredWorkflowDefinitionId.HasValue ||
               ProcessExecutorKindNames.IsWorkflow(role.PreferredExecutorKind);
    }

    private static bool RequiresTechnicalAgentBinding(ProcessLaunchPlan plan)
        => RequiresTechnicalAgentBinding(plan.OperatingMode);

    private static bool RequiresTechnicalAgentBinding(ProcessOperatingMode operatingMode)
        => operatingMode is ProcessOperatingMode.AssistedExecution or ProcessOperatingMode.GovernedLive;

    private static bool HasBoundTechnicalAgent(AiAgentListItemModel? aiResource)
    {
        return aiResource is not null &&
               aiResource.TechnicalAgentId.HasValue &&
               aiResource.BindingStatus == AiResourceBindingStatus.Bound;
    }

    private async Task SynchronizeAiDirectoryProjectionForProcessAsync(
        string operationName,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource? timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var syncTask = aiAgentService.SynchronizeDirectoryProjectionAsync(timeout.Token);

        try
        {
            await syncTask.WaitAsync(ProcessAiDirectoryProjectionSyncTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            timeout.Cancel();
            logger.LogWarning(
                "AI resource projection refresh for {OperationName} exceeded {TimeoutSeconds:0.#} seconds. Continuing with the current CRM-HR AI projection.",
                operationName,
                ProcessAiDirectoryProjectionSyncTimeout.TotalSeconds);

            ObserveTimedOutAiDirectoryProjectionSync(syncTask, timeout, operationName);
            timeout = null;
        }
        finally
        {
            timeout?.Dispose();
        }
    }

    private void ObserveTimedOutAiDirectoryProjectionSync(
        Task syncTask,
        CancellationTokenSource timeout,
        string operationName)
    {
        _ = syncTask.ContinueWith(
            completedTask =>
            {
                timeout.Dispose();
                if (completedTask.IsFaulted && completedTask.Exception is not null)
                {
                    logger.LogWarning(
                        completedTask.Exception.GetBaseException(),
                        "Timed-out AI resource projection refresh for {OperationName} failed after process launch continued.",
                        operationName);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task<LaunchAiDirectorySnapshot> LoadLaunchAiDirectorySnapshotAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var staffingFacts = await aiAgentService.ListAgentStaffingFactsProjectionAsync(dbContext, cancellationToken: cancellationToken);
        var factsByPartyId = staffingFacts
            .Where(IsUsableLaunchAiFact)
            .GroupBy(item => item.PartyId)
            .ToDictionary(
                group => group.Key,
                group => group.First());

        var now = clock.GetUtcNow();
        var directory = factsByPartyId.Values
            .Select(fact => BuildAiDirectoryItem(fact, now))
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new LaunchAiDirectorySnapshot(directory, factsByPartyId);
    }

    private static bool IsUsableLaunchAiFact(AiAgentStaffingFactListItemModel fact)
    {
        if (fact.PartyId == Guid.Empty)
        {
            return false;
        }

        if (fact.BindingStatus == AiResourceBindingStatus.Bound && fact.TechnicalAgentId.HasValue)
        {
            return true;
        }

        return fact.Capabilities.Count > 0 ||
               !string.IsNullOrWhiteSpace(fact.ProviderName) ||
               !string.IsNullOrWhiteSpace(fact.DefaultModel) ||
               !string.IsNullOrWhiteSpace(fact.TemplateKey) ||
               !string.IsNullOrWhiteSpace(fact.Instructions);
    }

    private static AiAgentListItemModel BuildAiDirectoryItem(
        AiAgentStaffingFactListItemModel fact,
        DateTimeOffset now)
    {
        return new AiAgentListItemModel(
            fact.PartyId,
            string.IsNullOrWhiteSpace(fact.DisplayName) ? "AI agent" : fact.DisplayName,
            fact.Summary,
            PartyLifecycleStatus.Active,
            fact.TechnicalAgentId,
            fact.BindingStatus,
            fact.BindingSummary,
            fact.ExecutionMode,
            AiValidationStatus.Draft,
            fact.ProviderName,
            fact.DefaultModel,
            string.Empty,
            fact.Capabilities.Count,
            true,
            fact.AgentsRoute,
            now);
    }

    private static string BuildLaunchRecommendationSummary(IReadOnlyList<ProcessLaunchCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return "No recommendation was produced.";
        }

        return string.Join(
            "; ",
            candidates
                .OrderByDescending(item => item.Score)
                .Take(3)
                .Select(item => $"{item.DisplayName} ({item.CandidateKind})"));
    }

    private static string ResolveLaunchSelectionSummary(ProcessLaunchCandidate candidate)
    {
        return $"{candidate.DisplayName} / {candidate.CandidateKind}";
    }

    private static string ResolveLaunchReadinessSummary(ProcessLaunchCandidate candidate, string prefix)
    {
        if (candidate.CandidateKind == ProcessLaunchCandidateKind.Gap)
        {
            return $"{prefix}: manual correction is required before this role can proceed.";
        }

        if (candidate.RequiresProvisioning)
        {
            return $"{prefix}: provisioning must complete before this role can execute.";
        }

        return $"{prefix}: candidate is ready for approval and execution.";
    }

    private static string ResolveProvisioningRequestKind(ProcessLaunchPlanRole role, ProcessLaunchCandidate candidate)
    {
        return candidate.CandidateKind switch
        {
            ProcessLaunchCandidateKind.NewAiAgentProposal => "CreateAiResource",
            ProcessLaunchCandidateKind.AiResource => "BindAiResource",
            ProcessLaunchCandidateKind.ProjectAssignment when ProcessExecutorKindNames.IsAiAgent(role.PreferredExecutorKind) => "BindAiResource",
            _ => "ProvisionRole"
        };
    }

    private static string BuildLaunchRoute(Guid definitionId, Guid? projectId, Guid launchPlanId)
    {
        return projectId.HasValue
            ? $"/projects/{projectId.Value:D}/processes?processId={definitionId:D}&launchPlanId={launchPlanId:D}"
            : $"/processes?processId={definitionId:D}&launchPlanId={launchPlanId:D}";
    }

    private static string BuildLaunchProvisioningMetadata(
        ProcessRoleRequirement role,
        IReadOnlyList<Guid> requiredSkillIds,
        string displayName,
        Guid? existingPartyId = null)
    {
        return JsonSerializer.Serialize(
            new LaunchProvisioningMetadata(
                displayName,
                role.DisplayName,
                requiredSkillIds,
                existingPartyId),
            LaunchJsonOptions);
    }

    private static LaunchProvisioningMetadata ParseLaunchProvisioningMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return LaunchProvisioningMetadata.Empty;
        }

        try
        {
            return JsonSerializer.Deserialize<LaunchProvisioningMetadata>(json, LaunchJsonOptions)
                ?? LaunchProvisioningMetadata.Empty;
        }
        catch (JsonException)
        {
            return LaunchProvisioningMetadata.Empty;
        }
    }

    private static IReadOnlyList<Guid> DeserializeGuidList(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json, LaunchJsonOptions)
                ?.Where(item => item != Guid.Empty)
                .Distinct()
                .ToList()
                ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string BuildProvisionedAgentNotes(
        string bindingSummary,
        string recommendationSummary,
        string requestedBy)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(bindingSummary))
        {
            parts.Add(bindingSummary.Trim());
        }

        if (!string.IsNullOrWhiteSpace(recommendationSummary))
        {
            parts.Add(recommendationSummary.Trim());
        }

        parts.Add($"Provisioned by {requestedBy.Trim()}.");
        return string.Join(" ", parts.Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    private sealed record PublishedProcessLaunchContext(
        ProcessDefinition Definition,
        ProcessDefinitionVersion PublishedVersion,
        IReadOnlyList<ProcessRoleRequirement> Roles,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProcessRoleSkillRequirement>> RoleSkillsByRoleId);

    private sealed record ProcessLaunchRoleRecommendation(
        ProcessLaunchPlanRole Role,
        List<ProcessLaunchCandidate> Candidates);

    private sealed record ProcessLaunchCandidateSet(
        IReadOnlyList<ProcessLaunchRoleRecommendation> Roles);

    private sealed record LaunchAiDirectorySnapshot(
        IReadOnlyList<AiAgentListItemModel> Directory,
        IReadOnlyDictionary<Guid, AiAgentStaffingFactListItemModel> StaffingFactsByPartyId);

    private sealed record LaunchApprovalAuthority(
        Guid? ApproverPartyId,
        string ApproverDisplayName,
        string ApproverKind,
        Guid? HumanSubstitutePartyId,
        string HumanSubstituteName);

    private sealed record LaunchProvisioningMetadata(
        string DisplayName,
        string RoleDisplayName,
        IReadOnlyList<Guid> RequiredSkillIds,
        Guid? ExistingPartyId)
    {
        public static LaunchProvisioningMetadata Empty { get; } = new(string.Empty, string.Empty, [], null);
    }

    private sealed record LaunchProvisioningOutcome(
        Guid? PartyId,
        Guid? TechnicalAgentId,
        string Summary);
}
