using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private const int HistoricalEstimateRunLimit = 20;
    private const decimal MinimumDifficultyScore = 1m;
    private const decimal MinimumIncompleteRunCompletionRatio = 0.15m;
    private const decimal MaximumIncompleteRunScale = 4m;
    private const decimal MinimumDifficultyRatio = 0.35m;
    private const decimal MaximumDifficultyRatio = 3m;

    public async Task<Result<ProcessRunEstimateResult>> EstimateRunAsync(
        ProcessRunEstimateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ProcessDefinitionId == Guid.Empty && !request.LaunchPlanId.HasValue)
        {
            return Result<ProcessRunEstimateResult>.Failure(Error.Validation(
                "Process definition or launch plan is required.",
                "processes.run-estimate.definition-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var estimateContextResult = await LoadRunEstimateContextAsync(dbContext, request, cancellationToken);
        if (estimateContextResult.IsFailure)
        {
            return Result<ProcessRunEstimateResult>.Failure(estimateContextResult.Errors);
        }

        return Result<ProcessRunEstimateResult>.Success(
            await EstimateRunAsync(dbContext, estimateContextResult.Value!, cancellationToken));
    }

    private Task<ProcessRunEstimateResult> EstimateRunAsync(
        AppDbContext dbContext,
        RunStartContext context,
        CancellationToken cancellationToken)
    {
        return EstimateRunAsync(
            dbContext,
            new ProcessRunEstimateContext(
                context.Definition,
                context.PublishedVersion,
                context.ProjectId,
                context.OperatingMode,
                context.Steps,
                context.Roles,
                context.ArtifactExpectations),
            cancellationToken);
    }

    private async Task<ProcessRunEstimateResult> EstimateRunAsync(
        AppDbContext dbContext,
        ProcessRunEstimateContext context,
        CancellationToken cancellationToken)
    {
        var currentDifficulty = BuildProcessEstimateDifficulty(
            context.Steps,
            context.Roles,
            context.ArtifactExpectations);
        var fallback = BuildDefinitionFallbackEstimate(currentDifficulty);
        var samples = await LoadHistoricalEstimateSamplesAsync(
            dbContext,
            context,
            currentDifficulty,
            fallback.EstimatedCostUsd,
            cancellationToken);
        if (samples.Count == 0)
        {
            return fallback;
        }

        var totalWeight = samples.Sum(sample => sample.Weight);
        if (totalWeight <= 0m)
        {
            return fallback with
            {
                HistoricalRunCount = samples.Count,
                Summary = "Historical process runs exist, but none contain usable cost evidence. Used definition complexity fallback."
            };
        }

        var estimatedCost = decimal.Round(
            samples.Sum(sample => sample.EstimatedCostUsd * sample.Weight) / totalWeight,
            6,
            MidpointRounding.AwayFromZero);
        var estimatedElapsedMinutes = RoundWeightedMinutes(samples, sample => sample.EstimatedElapsedMinutes, totalWeight);
        var estimatedTouchMinutes = RoundWeightedMinutes(samples, sample => sample.EstimatedTouchMinutes, totalWeight);
        var completedActualCount = samples.Count(sample => sample.SourceKind == ProcessRunEstimateSourceKind.CompletedHistoricalActualCost);
        var incompleteActualCount = samples.Count(sample => sample.SourceKind == ProcessRunEstimateSourceKind.IncompleteHistoricalActualCost);
        var estimatedOnlyCount = samples.Count(sample => sample.SourceKind == ProcessRunEstimateSourceKind.HistoricalEstimatedCost);
        var sourceKind = ResolveAggregateSourceKind(samples);
        var confidenceLabel = ResolveEstimateConfidenceLabel(completedActualCount, incompleteActualCount, estimatedOnlyCount);

        return new ProcessRunEstimateResult(
            Math.Max(estimatedCost, fallback.EstimatedCostUsd),
            Math.Max(estimatedElapsedMinutes, fallback.EstimatedElapsedMinutes),
            Math.Max(estimatedTouchMinutes, fallback.EstimatedTouchMinutes),
            sourceKind,
            confidenceLabel,
            BuildHistoricalEstimateSummary(completedActualCount, incompleteActualCount, estimatedOnlyCount, confidenceLabel),
            samples.Count,
            completedActualCount,
            incompleteActualCount,
            decimal.Round(samples.Sum(sample => sample.DifficultyRatio * sample.Weight) / totalWeight, 3, MidpointRounding.AwayFromZero));
    }

    private async Task<Result<ProcessRunEstimateContext>> LoadRunEstimateContextAsync(
        AppDbContext dbContext,
        ProcessRunEstimateRequest request,
        CancellationToken cancellationToken)
    {
        ProcessLaunchPlan? launchPlan = null;
        ProcessDefinition? definition;
        ProcessDefinitionVersion? publishedVersion;
        Guid? projectId;
        ProcessOperatingMode operatingMode;

        if (request.LaunchPlanId.HasValue)
        {
            launchPlan = await dbContext.Set<ProcessLaunchPlan>()
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.LaunchPlanId.Value, cancellationToken);
            if (launchPlan is null)
            {
                return Result<ProcessRunEstimateContext>.Failure(Error.Validation(
                    "Launch plan was not found.",
                    "processes.run-estimate.launch-plan-not-found"));
            }

            definition = await dbContext.Set<ProcessDefinition>()
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == launchPlan.ProcessDefinitionId, cancellationToken);
            publishedVersion = await dbContext.Set<ProcessDefinitionVersion>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.ProcessDefinitionId == launchPlan.ProcessDefinitionId &&
                        item.Id == launchPlan.ProcessDefinitionVersionId,
                    cancellationToken);
            projectId = launchPlan.ProjectId ?? request.ProjectId ?? definition?.ProjectId;
            operatingMode = launchPlan.OperatingMode;
        }
        else
        {
            definition = await dbContext.Set<ProcessDefinition>()
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.ProcessDefinitionId, cancellationToken);
            if (definition is null)
            {
                return Result<ProcessRunEstimateContext>.Failure(Error.Validation(
                    "Process definition was not found.",
                    "processes.run-estimate.definition-not-found"));
            }

            var versionId = request.ProcessDefinitionVersionId ?? definition.ActivePublishedVersionId;
            if (!versionId.HasValue)
            {
                return Result<ProcessRunEstimateContext>.Failure(Error.Validation(
                    "Publish a process definition before estimating a run.",
                    "processes.run-estimate.published-version-required"));
            }

            publishedVersion = await dbContext.Set<ProcessDefinitionVersion>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.ProcessDefinitionId == definition.Id &&
                        item.Id == versionId.Value &&
                        item.Status == ProcessVersionStatus.Published,
                    cancellationToken);
            projectId = request.ProjectId ?? definition.ProjectId;
            operatingMode = request.OperatingMode;
        }

        if (definition is null || publishedVersion is null)
        {
            return Result<ProcessRunEstimateContext>.Failure(Error.Validation(
                "The estimate target no longer points to a published process version.",
                "processes.run-estimate.version-missing"));
        }

        var roles = await dbContext.Set<ProcessRoleRequirement>()
            .AsNoTracking()
            .Where(item => item.ProcessDefinitionVersionId == publishedVersion.Id)
            .OrderBy(item => item.DisplayOrder)
            .ToListAsync(cancellationToken);
        var steps = await dbContext.Set<ProcessStepDefinition>()
            .AsNoTracking()
            .Where(item => item.ProcessDefinitionVersionId == publishedVersion.Id)
            .OrderBy(item => item.OrderIndex)
            .ToListAsync(cancellationToken);
        var stepIds = steps.Select(item => item.Id).ToList();
        var artifactExpectations = stepIds.Count == 0
            ? []
            : await dbContext.Set<ProcessArtifactExpectation>()
                .AsNoTracking()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ToListAsync(cancellationToken);

        return Result<ProcessRunEstimateContext>.Success(
            new ProcessRunEstimateContext(
                definition,
                publishedVersion,
                projectId,
                operatingMode,
                steps,
                roles,
                artifactExpectations));
    }

    private async Task<IReadOnlyList<HistoricalProcessRunEstimateSample>> LoadHistoricalEstimateSamplesAsync(
        AppDbContext dbContext,
        ProcessRunEstimateContext context,
        ProcessEstimateDifficulty currentDifficulty,
        decimal definitionFallbackCost,
        CancellationToken cancellationToken)
    {
        var historicalRuns = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .Where(item => item.ProcessDefinitionId == context.Definition.Id)
            .OrderByDescending(item => item.CompletedAtUtc ?? item.StartedAtUtc ?? item.UpdatedAtUtc)
            .Take(HistoricalEstimateRunLimit)
            .ToListAsync(cancellationToken);
        if (historicalRuns.Count == 0)
        {
            return [];
        }

        var runIds = historicalRuns.Select(item => item.Id).ToList();
        var stepRunsByRunId = (await dbContext.Set<ProcessStepRun>()
                .AsNoTracking()
                .Where(item => runIds.Contains(item.ProcessRunId))
                .ToListAsync(cancellationToken))
            .GroupBy(item => item.ProcessRunId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ProcessStepRun>)group.ToList());
        var historicalDifficultiesByVersionId = await LoadHistoricalDifficultiesAsync(
            dbContext,
            historicalRuns.Select(item => item.ProcessDefinitionVersionId).Distinct().ToList(),
            cancellationToken);

        var samples = new List<HistoricalProcessRunEstimateSample>(historicalRuns.Count);
        foreach (var run in historicalRuns)
        {
            stepRunsByRunId.TryGetValue(run.Id, out var stepRuns);
            stepRuns ??= [];
            var historicalDifficulty = historicalDifficultiesByVersionId.TryGetValue(run.ProcessDefinitionVersionId, out var resolvedDifficulty)
                ? resolvedDifficulty
                : currentDifficulty;
            var difficultyRatio = Clamp(
                currentDifficulty.Score / Math.Max(historicalDifficulty.Score, MinimumDifficultyScore),
                MinimumDifficultyRatio,
                MaximumDifficultyRatio);
            var completionRatio = ResolveHistoricalCompletionRatio(stepRuns, historicalDifficulty.StepCount);
            var elapsedMinutes = ResolveHistoricalElapsedMinutes(run, stepRuns);
            var touchMinutes = ResolveHistoricalTouchMinutes(stepRuns, elapsedMinutes);

            var sample = TryCreateHistoricalEstimateSample(
                run,
                difficultyRatio,
                completionRatio,
                elapsedMinutes,
                touchMinutes,
                context.PublishedVersion.Id,
                definitionFallbackCost);
            if (sample is not null)
            {
                samples.Add(sample);
            }
        }

        return samples;
    }

    private async Task<IReadOnlyDictionary<Guid, ProcessEstimateDifficulty>> LoadHistoricalDifficultiesAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> versionIds,
        CancellationToken cancellationToken)
    {
        if (versionIds.Count == 0)
        {
            return new Dictionary<Guid, ProcessEstimateDifficulty>();
        }

        var steps = await dbContext.Set<ProcessStepDefinition>()
            .AsNoTracking()
            .Where(item => versionIds.Contains(item.ProcessDefinitionVersionId))
            .ToListAsync(cancellationToken);
        var roles = await dbContext.Set<ProcessRoleRequirement>()
            .AsNoTracking()
            .Where(item => versionIds.Contains(item.ProcessDefinitionVersionId))
            .ToListAsync(cancellationToken);
        var stepIds = steps.Select(item => item.Id).ToList();
        var artifactExpectations = stepIds.Count == 0
            ? []
            : await dbContext.Set<ProcessArtifactExpectation>()
                .AsNoTracking()
                .Where(item => stepIds.Contains(item.StepDefinitionId))
                .ToListAsync(cancellationToken);
        var stepsByVersionId = steps
            .GroupBy(item => item.ProcessDefinitionVersionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ProcessStepDefinition>)group.ToList());
        var rolesByVersionId = roles
            .GroupBy(item => item.ProcessDefinitionVersionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ProcessRoleRequirement>)group.ToList());
        var artifactExpectationsByVersionId = artifactExpectations
            .Join(
                steps,
                artifact => artifact.StepDefinitionId,
                step => step.Id,
                (artifact, step) => new { step.ProcessDefinitionVersionId, Artifact = artifact })
            .GroupBy(item => item.ProcessDefinitionVersionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ProcessArtifactExpectation>)group.Select(item => item.Artifact).ToList());

        return versionIds.ToDictionary(
            versionId => versionId,
            versionId => BuildProcessEstimateDifficulty(
                stepsByVersionId.GetValueOrDefault(versionId) ?? [],
                rolesByVersionId.GetValueOrDefault(versionId) ?? [],
                artifactExpectationsByVersionId.GetValueOrDefault(versionId) ?? []));
    }

    private static HistoricalProcessRunEstimateSample? TryCreateHistoricalEstimateSample(
        ProcessRun run,
        decimal difficultyRatio,
        decimal completionRatio,
        int elapsedMinutes,
        int touchMinutes,
        Guid currentVersionId,
        decimal definitionFallbackCost)
    {
        var isCompletedRun = run.Status == ProcessRunStatus.Completed && run.CompletedAtUtc.HasValue;
        if (run.ActualCost > 0m)
        {
            var sourceKind = isCompletedRun
                ? ProcessRunEstimateSourceKind.CompletedHistoricalActualCost
                : ProcessRunEstimateSourceKind.IncompleteHistoricalActualCost;
            var incompleteScale = isCompletedRun
                ? 1m
                : Math.Min(1m / Math.Max(completionRatio, MinimumIncompleteRunCompletionRatio), MaximumIncompleteRunScale);
            var weight = ResolveHistoricalSampleWeight(sourceKind, run.ProcessDefinitionVersionId, currentVersionId, isCompletedRun);

            return new HistoricalProcessRunEstimateSample(
                decimal.Round(run.ActualCost * incompleteScale * difficultyRatio, 6, MidpointRounding.AwayFromZero),
                Math.Max((int)Math.Ceiling(elapsedMinutes * incompleteScale * difficultyRatio), 1),
                Math.Max((int)Math.Ceiling(touchMinutes * incompleteScale * difficultyRatio), 1),
                sourceKind,
                difficultyRatio,
                weight);
        }

        if (run.EstimatedCost <= 0m)
        {
            return null;
        }

        var cappedHistoricalEstimate = Math.Min(run.EstimatedCost, Math.Max(definitionFallbackCost * 8m, 1m));
        return new HistoricalProcessRunEstimateSample(
            decimal.Round(cappedHistoricalEstimate * difficultyRatio, 6, MidpointRounding.AwayFromZero),
            Math.Max((int)Math.Ceiling(elapsedMinutes * difficultyRatio), 1),
            Math.Max((int)Math.Ceiling(touchMinutes * difficultyRatio), 1),
            ProcessRunEstimateSourceKind.HistoricalEstimatedCost,
            difficultyRatio,
            0.15m);
    }

    private static decimal ResolveHistoricalSampleWeight(
        ProcessRunEstimateSourceKind sourceKind,
        Guid runVersionId,
        Guid currentVersionId,
        bool isCompletedRun)
    {
        var versionWeight = runVersionId == currentVersionId ? 1.25m : 1m;
        return sourceKind switch
        {
            ProcessRunEstimateSourceKind.CompletedHistoricalActualCost => isCompletedRun ? 1.4m * versionWeight : 1m * versionWeight,
            ProcessRunEstimateSourceKind.IncompleteHistoricalActualCost => 0.55m * versionWeight,
            ProcessRunEstimateSourceKind.HistoricalEstimatedCost => 0.15m * versionWeight,
            _ => 0.1m * versionWeight
        };
    }

    private static ProcessEstimateDifficulty BuildProcessEstimateDifficulty(
        IReadOnlyList<ProcessStepDefinition> steps,
        IReadOnlyList<ProcessRoleRequirement> roles,
        IReadOnlyList<ProcessArtifactExpectation> artifactExpectations)
    {
        var aiRoleCount = roles.Count(IsAiEstimateRole);
        var workflowRoleCount = roles.Count(role => ProcessExecutorKindNames.IsWorkflow(role.PreferredExecutorKind));
        var requiredArtifactCount = artifactExpectations.Count(item => item.IsRequired);
        var targetLeadHours = Math.Max(steps.Sum(item => item.TargetLeadHours), 0);
        var approvalStepCount = steps.Count(item => item.RequiresApproval);
        var decisionStepCount = steps.Count(item => item.RequiresDecisionRecord);
        var subprocessStepCount = steps.Count(item => item.SubprocessDefinitionId.HasValue);
        var score =
            MinimumDifficultyScore +
            steps.Count * 1.0m +
            targetLeadHours * 0.30m +
            roles.Count * 0.40m +
            aiRoleCount * 0.80m +
            workflowRoleCount * 0.60m +
            artifactExpectations.Count * 0.30m +
            requiredArtifactCount * 0.20m +
            approvalStepCount * 0.40m +
            decisionStepCount * 0.30m +
            subprocessStepCount * 1.20m;

        return new ProcessEstimateDifficulty(
            steps.Count,
            targetLeadHours,
            roles.Count,
            aiRoleCount,
            workflowRoleCount,
            artifactExpectations.Count,
            requiredArtifactCount,
            approvalStepCount,
            decisionStepCount,
            subprocessStepCount,
            Math.Max(score, MinimumDifficultyScore));
    }

    private static bool IsAiEstimateRole(ProcessRoleRequirement role)
    {
        return role.PreferredProjectAssignmentRole == ProjectPartyAssignmentRole.AiAgent ||
            ProcessExecutorKindNames.IsAiAgent(role.PreferredExecutorKind) ||
            role.Key.Contains("ai", StringComparison.OrdinalIgnoreCase);
    }

    private static ProcessRunEstimateResult BuildDefinitionFallbackEstimate(ProcessEstimateDifficulty difficulty)
    {
        var cost =
            0.05m +
            difficulty.StepCount * 0.02m +
            difficulty.AiRoleCount * 0.15m +
            difficulty.WorkflowRoleCount * 0.05m +
            difficulty.ArtifactExpectationCount * 0.01m +
            difficulty.TargetLeadHours * 0.005m;
        var elapsedMinutes = Math.Max(difficulty.TargetLeadHours * 60, Math.Max(difficulty.StepCount, 1) * 20);
        var touchMinutes = Math.Max(difficulty.StepCount * 20, Math.Max(elapsedMinutes / 4, 15));

        return new ProcessRunEstimateResult(
            decimal.Round(Math.Max(cost, 0.01m), 6, MidpointRounding.AwayFromZero),
            elapsedMinutes,
            touchMinutes,
            ProcessRunEstimateSourceKind.DefinitionFallback,
            "low",
            "No usable historical process cost was available. Estimated from process definition complexity.",
            0,
            0,
            0,
            1m);
    }

    private static decimal ResolveHistoricalCompletionRatio(
        IReadOnlyList<ProcessStepRun> stepRuns,
        int fallbackStepCount)
    {
        var totalSteps = Math.Max(stepRuns.Count, Math.Max(fallbackStepCount, 1));
        var completedUnits = stepRuns.Count(item => item.Status is ProcessStepRunStatus.Completed or ProcessStepRunStatus.Skipped);
        var partialUnits = stepRuns.Count(item =>
            item.Status is ProcessStepRunStatus.InProgress or
                ProcessStepRunStatus.Blocked or
                ProcessStepRunStatus.Failed or
                ProcessStepRunStatus.Refused);
        var progressUnits = completedUnits + partialUnits * 0.5m;

        return Clamp(progressUnits / totalSteps, MinimumIncompleteRunCompletionRatio, 1m);
    }

    private static int ResolveHistoricalElapsedMinutes(ProcessRun run, IReadOnlyList<ProcessStepRun> stepRuns)
    {
        if (run.StartedAtUtc.HasValue && run.CompletedAtUtc.HasValue)
        {
            return Math.Max((int)Math.Ceiling((run.CompletedAtUtc.Value - run.StartedAtUtc.Value).TotalMinutes), 1);
        }

        var stepMinutes = stepRuns.Sum(item => item.WaitMinutes + item.TouchMinutes + item.BlockedMinutes);
        if (stepMinutes > 0)
        {
            return stepMinutes;
        }

        return Math.Max(stepRuns.Count, 1) * 20;
    }

    private static int ResolveHistoricalTouchMinutes(IReadOnlyList<ProcessStepRun> stepRuns, int elapsedMinutes)
    {
        var touchMinutes = stepRuns.Sum(item => item.TouchMinutes);
        if (touchMinutes > 0)
        {
            return touchMinutes;
        }

        return Math.Max(elapsedMinutes / 3, 15);
    }

    private static int RoundWeightedMinutes(
        IReadOnlyList<HistoricalProcessRunEstimateSample> samples,
        Func<HistoricalProcessRunEstimateSample, int> selector,
        decimal totalWeight)
    {
        return Math.Max(
            (int)Math.Ceiling(samples.Sum(sample => selector(sample) * sample.Weight) / totalWeight),
            1);
    }

    private static ProcessRunEstimateSourceKind ResolveAggregateSourceKind(
        IReadOnlyList<HistoricalProcessRunEstimateSample> samples)
    {
        var sourceKinds = samples.Select(item => item.SourceKind).Distinct().ToList();
        return sourceKinds.Count == 1
            ? sourceKinds[0]
            : ProcessRunEstimateSourceKind.MixedHistoricalEvidence;
    }

    private static string ResolveEstimateConfidenceLabel(
        int completedActualCount,
        int incompleteActualCount,
        int estimatedOnlyCount)
    {
        if (completedActualCount >= 2)
        {
            return "high";
        }

        if (completedActualCount == 1 && incompleteActualCount > 0)
        {
            return "medium";
        }

        if (completedActualCount == 1)
        {
            return "medium";
        }

        if (incompleteActualCount > 0)
        {
            return "medium-low";
        }

        return estimatedOnlyCount > 0 ? "low" : "low";
    }

    private static string BuildHistoricalEstimateSummary(
        int completedActualCount,
        int incompleteActualCount,
        int estimatedOnlyCount,
        string confidenceLabel)
    {
        return $"Estimated from prior process runs: {completedActualCount} completed actual, {incompleteActualCount} incomplete actual, {estimatedOnlyCount} estimated-only sample(s). Confidence: {confidenceLabel}.";
    }

    private static decimal Clamp(decimal value, decimal minimum, decimal maximum)
    {
        if (value < minimum)
        {
            return minimum;
        }

        return value > maximum ? maximum : value;
    }

    private sealed record ProcessRunEstimateContext(
        ProcessDefinition Definition,
        ProcessDefinitionVersion PublishedVersion,
        Guid? ProjectId,
        ProcessOperatingMode OperatingMode,
        IReadOnlyList<ProcessStepDefinition> Steps,
        IReadOnlyList<ProcessRoleRequirement> Roles,
        IReadOnlyList<ProcessArtifactExpectation> ArtifactExpectations);

    private sealed record ProcessEstimateDifficulty(
        int StepCount,
        int TargetLeadHours,
        int RoleCount,
        int AiRoleCount,
        int WorkflowRoleCount,
        int ArtifactExpectationCount,
        int RequiredArtifactExpectationCount,
        int ApprovalStepCount,
        int DecisionStepCount,
        int SubprocessStepCount,
        decimal Score);

    private sealed record HistoricalProcessRunEstimateSample(
        decimal EstimatedCostUsd,
        int EstimatedElapsedMinutes,
        int EstimatedTouchMinutes,
        ProcessRunEstimateSourceKind SourceKind,
        decimal DifficultyRatio,
        decimal Weight);
}
