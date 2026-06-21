using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    public async Task<Result<Guid>> CreateLaunchPlanAsync(
        ProcessLaunchCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ProcessDefinitionId == Guid.Empty)
        {
            return Result<Guid>.Failure(Error.Validation("Process definition is required.", "processes.launch.definition-required"));
        }

        await SynchronizeAiDirectoryProjectionForProcessAsync("launch-plan creation", cancellationToken);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await BeginCoordinatedTransactionAsync(dbContext, cancellationToken);

        try
        {
            var publishedContext = await LoadPublishedContextAsync(dbContext, request.ProcessDefinitionId, cancellationToken);
            if (publishedContext is null)
            {
                return Result<Guid>.Failure(Error.Validation(
                    "Publish a process definition before creating a launch plan.",
                    "processes.launch.published-version-required"));
            }

            var projectId = request.ProjectId ?? publishedContext.Definition.ProjectId;
            var projectStructureContext = request.ProjectStructureContext;
            if (projectStructureContext is null && projectId.HasValue)
            {
                projectStructureContext = await projectStructureBridge.TryResolveLaunchContextAsync(
                    dbContext,
                    projectId.Value,
                    publishedContext.Definition.Id,
                    cancellationToken);
            }

            var now = clock.GetUtcNow();
            var planName = ResolveLaunchPlanName(request.LaunchName, publishedContext.Definition.Name, now);
            var triggerReason = ProcessProjectStructureContextFormatter.AppendToTriggerReason(
                request.TriggerReason,
                projectStructureContext);
            var requestedBy = ResolveLaunchPlanRequestedBy(request.RequestedBy);
            var reusableLaunchPlanId = await TryFindReusableOpenLaunchPlanAsync(
                dbContext,
                publishedContext.Definition.Id,
                publishedContext.PublishedVersion.Id,
                projectId,
                planName,
                request.OperatingMode,
                triggerReason,
                requestedBy,
                cancellationToken);
            if (reusableLaunchPlanId.HasValue)
            {
                return Result<Guid>.Success(reusableLaunchPlanId.Value);
            }

            var plan = new ProcessLaunchPlan
            {
                ProcessDefinitionId = publishedContext.Definition.Id,
                ProcessDefinitionVersionId = publishedContext.PublishedVersion.Id,
                ProjectId = projectId,
                Name = planName,
                OperatingMode = request.OperatingMode,
                TriggerReason = triggerReason,
                Status = ProcessLaunchPlanStatus.Draft,
                RecommendationStrategy = "Project assignments first, then CRM-HR staffing and AI resource directories, then deterministic AI proposal fallback.",
                FallbackStrategy = "Human substitute approval and explicit provisioning remain mandatory when no ready executor is already bound.",
                Summary = publishedContext.Definition.ValueStatement,
                RequestedBy = requestedBy,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await dbContext.Set<ProcessLaunchPlan>().AddAsync(plan, cancellationToken);

            var candidateSet = await BuildLaunchCandidateSetAsync(
                dbContext,
                plan,
                publishedContext,
                projectId,
                cancellationToken);
            foreach (var roleRecommendation in candidateSet.Roles)
            {
                await dbContext.Set<ProcessLaunchPlanRole>().AddAsync(roleRecommendation.Role, cancellationToken);
                foreach (var candidate in roleRecommendation.Candidates)
                {
                    await dbContext.Set<ProcessLaunchCandidate>().AddAsync(candidate, cancellationToken);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<Guid>.Success(plan.Id);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Guid>.Failure(Error.Validation(
                "Launch plan creation conflicted with another update. Reload and try again.",
                "processes.launch.conflict"));
        }
    }

    private static string ResolveLaunchPlanName(
        string requestedName,
        string definitionName,
        DateTimeOffset now)
    {
        return string.IsNullOrWhiteSpace(requestedName)
            ? $"{definitionName} launch / {now:yyyy-MM-dd HH:mm}"
            : requestedName.Trim();
    }

    private static string ResolveLaunchPlanRequestedBy(string requestedBy)
    {
        return string.IsNullOrWhiteSpace(requestedBy)
            ? "process-workspace"
            : requestedBy.Trim();
    }

    private static async Task<Guid?> TryFindReusableOpenLaunchPlanAsync(
        AppDbContext dbContext,
        Guid processDefinitionId,
        Guid processDefinitionVersionId,
        Guid? projectId,
        string planName,
        ProcessOperatingMode operatingMode,
        string triggerReason,
        string requestedBy,
        CancellationToken cancellationToken)
    {
        var matchingPlans = await dbContext.Set<ProcessLaunchPlan>()
            .AsNoTracking()
            .Where(item =>
                item.ProcessDefinitionId == processDefinitionId &&
                item.ProcessDefinitionVersionId == processDefinitionVersionId &&
                item.ProjectId == projectId &&
                item.Name == planName &&
                item.OperatingMode == operatingMode &&
                item.TriggerReason == triggerReason &&
                item.RequestedBy == requestedBy &&
                item.GeneratedRunId == null &&
                (item.Status == ProcessLaunchPlanStatus.Draft ||
                 item.Status == ProcessLaunchPlanStatus.ChangesRequested))
            .Select(item => new
            {
                item.Id,
                item.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return matchingPlans
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefault();
    }

    public async Task<Result> SelectLaunchCandidateAsync(
        ProcessLaunchCandidateSelectionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.LaunchPlanId == Guid.Empty || request.LaunchPlanRoleId == Guid.Empty || request.CandidateId == Guid.Empty)
        {
            return Result.Failure(Error.Validation(
                "Launch plan, role, and candidate are required.",
                "processes.launch.selection-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var plan = await dbContext.Set<ProcessLaunchPlan>()
            .SingleOrDefaultAsync(item => item.Id == request.LaunchPlanId, cancellationToken);
        if (plan is null)
        {
            return Result.Failure(Error.Validation("Launch plan was not found.", "processes.launch.not-found"));
        }

        if (plan.Status is not ProcessLaunchPlanStatus.Draft and not ProcessLaunchPlanStatus.ChangesRequested)
        {
            return Result.Failure(Error.Validation(
                "Only draft or changes-requested launch plans can change candidate selection.",
                "processes.launch.selection-locked"));
        }

        var role = await dbContext.Set<ProcessLaunchPlanRole>()
            .SingleOrDefaultAsync(item => item.Id == request.LaunchPlanRoleId && item.LaunchPlanId == request.LaunchPlanId, cancellationToken);
        if (role is null)
        {
            return Result.Failure(Error.Validation("Launch role was not found.", "processes.launch.role-not-found"));
        }

        var candidate = await dbContext.Set<ProcessLaunchCandidate>()
            .SingleOrDefaultAsync(item => item.Id == request.CandidateId && item.LaunchPlanRoleId == request.LaunchPlanRoleId, cancellationToken);
        if (candidate is null)
        {
            return Result.Failure(Error.Validation("Launch candidate was not found.", "processes.launch.candidate-not-found"));
        }

        role.SelectedCandidateId = candidate.Id;
        role.RequiresProvisioning = candidate.RequiresProvisioning;
        role.IsResolved = candidate.CandidateKind != ProcessLaunchCandidateKind.Gap;
        role.SelectionSummary = ResolveLaunchSelectionSummary(candidate);
        role.ReadinessSummary = ResolveLaunchReadinessSummary(candidate, "Selected");
        plan.UpdatedAtUtc = clock.GetUtcNow();
        plan.Status = ProcessLaunchPlanStatus.Draft;

        var staleProvisioning = await dbContext.Set<ProcessLaunchProvisioningRequest>()
            .Where(item => item.LaunchPlanId == plan.Id && item.LaunchPlanRoleId == role.Id)
            .ToListAsync(cancellationToken);
        if (staleProvisioning.Count > 0)
        {
            dbContext.RemoveRange(staleProvisioning);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SelectLaunchTechnicalAgentAsync(
        ProcessLaunchTechnicalAgentSelectionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.LaunchPlanId == Guid.Empty ||
            request.LaunchPlanRoleId == Guid.Empty ||
            request.TechnicalAgentId == Guid.Empty)
        {
            return Result.Failure(Error.Validation(
                "Launch plan, role, and technical agent are required.",
                "processes.launch.technical-agent-selection-required"));
        }

        await SynchronizeAiDirectoryProjectionForProcessAsync("launch-plan manual technical agent selection", cancellationToken);
        var aiDirectory = await aiAgentService.ListAgentDirectoryAsync(cancellationToken);
        var aiResource = aiDirectory.FirstOrDefault(item => item.TechnicalAgentId == request.TechnicalAgentId);
        if (aiResource is null)
        {
            return Result.Failure(Error.Validation(
                "The selected AI agent is not available in the CRM-HR AI directory projection yet. Refresh the agent catalog and try again.",
                "processes.launch.technical-agent-not-projected"));
        }

        if (aiResource.BindingStatus != AiResourceBindingStatus.Bound ||
            !aiResource.TechnicalAgentId.HasValue)
        {
            return Result.Failure(Error.Validation(
                $"The selected AI agent '{aiResource.DisplayName}' is not bound to a runnable technical agent.",
                "processes.launch.technical-agent-not-bound"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var plan = await dbContext.Set<ProcessLaunchPlan>()
            .SingleOrDefaultAsync(item => item.Id == request.LaunchPlanId, cancellationToken);
        if (plan is null)
        {
            return Result.Failure(Error.Validation("Launch plan was not found.", "processes.launch.not-found"));
        }

        if (plan.Status is not ProcessLaunchPlanStatus.Draft and not ProcessLaunchPlanStatus.ChangesRequested)
        {
            return Result.Failure(Error.Validation(
                "Only draft or changes-requested launch plans can change candidate selection.",
                "processes.launch.selection-locked"));
        }

        var role = await dbContext.Set<ProcessLaunchPlanRole>()
            .SingleOrDefaultAsync(item => item.Id == request.LaunchPlanRoleId && item.LaunchPlanId == request.LaunchPlanId, cancellationToken);
        if (role is null)
        {
            return Result.Failure(Error.Validation("Launch role was not found.", "processes.launch.role-not-found"));
        }

        var candidate = await dbContext.Set<ProcessLaunchCandidate>()
            .SingleOrDefaultAsync(
                item =>
                    item.LaunchPlanRoleId == request.LaunchPlanRoleId &&
                    item.TechnicalAgentId == request.TechnicalAgentId,
                cancellationToken);
        if (candidate is null)
        {
            candidate = BuildManualTechnicalAgentCandidate(role, aiResource);
            await dbContext.Set<ProcessLaunchCandidate>().AddAsync(candidate, cancellationToken);
        }
        else
        {
            UpdateManualTechnicalAgentCandidate(candidate, role, aiResource);
        }

        role.SelectedCandidateId = candidate.Id;
        role.RequiresProvisioning = candidate.RequiresProvisioning;
        role.IsResolved = candidate.CandidateKind != ProcessLaunchCandidateKind.Gap;
        role.SelectionSummary = ResolveLaunchSelectionSummary(candidate);
        role.ReadinessSummary = ResolveLaunchReadinessSummary(candidate, "Selected");
        plan.UpdatedAtUtc = clock.GetUtcNow();
        plan.Status = ProcessLaunchPlanStatus.Draft;

        var staleProvisioning = await dbContext.Set<ProcessLaunchProvisioningRequest>()
            .Where(item => item.LaunchPlanId == plan.Id && item.LaunchPlanRoleId == role.Id)
            .ToListAsync(cancellationToken);
        if (staleProvisioning.Count > 0)
        {
            dbContext.RemoveRange(staleProvisioning);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private ProcessLaunchCandidate BuildManualTechnicalAgentCandidate(
        ProcessLaunchPlanRole role,
        AiAgentListItemModel aiResource)
    {
        var candidate = new ProcessLaunchCandidate
        {
            LaunchPlanRoleId = role.Id,
            CandidateKind = ProcessLaunchCandidateKind.AiResource,
            CreatedAtUtc = clock.GetUtcNow()
        };

        UpdateManualTechnicalAgentCandidate(candidate, role, aiResource);
        return candidate;
    }

    private static void UpdateManualTechnicalAgentCandidate(
        ProcessLaunchCandidate candidate,
        ProcessLaunchPlanRole role,
        AiAgentListItemModel aiResource)
    {
        var requiredSkillIds = DeserializeGuidList(role.RequiredSkillIdsJson);
        candidate.CandidateKind = ProcessLaunchCandidateKind.AiResource;
        candidate.PartyId = aiResource.PartyId;
        candidate.TechnicalAgentId = aiResource.TechnicalAgentId;
        candidate.DisplayName = aiResource.DisplayName;
        candidate.ExecutorKind = string.IsNullOrWhiteSpace(aiResource.OwnerName)
            ? "AI agent"
            : $"AI agent / {aiResource.OwnerName}";
        candidate.Score = 520m + Math.Min(aiResource.CapabilityCount, 40);
        candidate.IsRecommended = false;
        candidate.AllowsDirectMessaging = true;
        candidate.RequiresProvisioning = false;
        candidate.RecommendationSummary = $"Manually selected from the shared AI agent directory for '{role.DisplayName}'.";
        candidate.AvailabilitySummary = BuildManualTechnicalAgentAvailabilitySummary(aiResource);
        candidate.SourceRegistryKey = $"agent-framework-directory:{aiResource.PartyId:D}:{aiResource.TechnicalAgentId!.Value:D}";
        candidate.MetadataJson = BuildLaunchProvisioningMetadata(
            new ProcessRoleRequirement
            {
                Id = role.RoleRequirementId,
                DisplayName = role.DisplayName,
                Key = role.RoleKey,
                PreferredExecutorKind = role.PreferredExecutorKind
            },
            requiredSkillIds,
            aiResource.DisplayName,
            aiResource.PartyId);
    }

    private static string BuildManualTechnicalAgentAvailabilitySummary(AiAgentListItemModel aiResource)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(aiResource.ProviderName))
        {
            parts.Add(aiResource.ProviderName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(aiResource.DefaultModel))
        {
            parts.Add(aiResource.DefaultModel.Trim());
        }

        parts.Add(aiResource.CapabilityCount == 1
            ? "1 capability"
            : $"{aiResource.CapabilityCount} capabilities");

        return string.Join(" / ", parts);
    }
}
