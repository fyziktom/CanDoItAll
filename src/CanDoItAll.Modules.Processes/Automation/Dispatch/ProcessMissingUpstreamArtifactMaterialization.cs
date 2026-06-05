using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessMissingUpstreamArtifactMaterializationFacts(
    IReadOnlyList<ProcessRunAutomationDispatchService.DispatchArtifactInput> MissingInputs,
    ProcessRunAutomationDispatchService.DispatchArtifactInput? MaterializationTarget)
{
    public bool HasMissingInputs => MissingInputs.Count > 0;
}

internal static class ProcessMissingUpstreamArtifactMaterializationFactsResolver
{
    public static ProcessMissingUpstreamArtifactMaterializationFacts Create(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate)
    {
        var missingInputs = ResolveMissingInputs(candidate);

        return new ProcessMissingUpstreamArtifactMaterializationFacts(
            missingInputs,
            missingInputs.FirstOrDefault(IsRunnableTarget));
    }

    public static IReadOnlyList<ProcessRunAutomationDispatchService.DispatchArtifactInput> ResolveMissingInputs(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate)
    {
        return candidate.ArtifactInputs
            .Where(input => input.Artifacts.Count == 0)
            .ToList();
    }

    public static bool IsRunnableTarget(ProcessRunAutomationDispatchService.DispatchArtifactInput input)
    {
        return input.SourceStepRunId.HasValue &&
               input.SourceStepRunConcurrencyToken.HasValue &&
               input.SourceStepHasAgentExecutor &&
               input.SourceStepRunStatus is ProcessStepRunStatus.Completed or ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Failed;
    }
}

internal static class ProcessMissingUpstreamArtifactMaterializationBlocker
{
    public static ProcessStepTransitionRequest BuildBlockTransitionRequest(
        Guid stepRunId,
        Guid concurrencyToken,
        string blockReason,
        string automationActor)
    {
        return new ProcessStepTransitionRequest
        {
            StepRunId = stepRunId,
            StepRunConcurrencyToken = concurrencyToken,
            TargetStatus = ProcessStepRunStatus.Blocked,
            Reason = blockReason,
            BlockCause = ProcessStepBlockCause.UpstreamInput,
            DecidedBy = automationActor,
            SuppressAutomationDispatch = true
        };
    }

    public static string BuildBlockReason(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessMissingUpstreamArtifactMaterializationFacts facts)
    {
        var missingSummary = string.Join(
            "; ",
            facts.MissingInputs
                .Take(3)
                .Select(input => $"upstream step '{input.SourceStepTitle}' must provide required artifact '{input.ExpectedArtifactTitle}'"));
        var targetSummary = facts.MaterializationTarget is null
            ? "No eligible agent-owned upstream step is available for automatic materialization."
            : $"Automation requested upstream artifact materialization from '{facts.MaterializationTarget.SourceStepTitle}' before retrying this step.";
        return $"Cannot dispatch '{candidate.StepRun.Title}' because required upstream artifacts are missing: {missingSummary}. {targetSummary}";
    }
}

internal static class ProcessMissingUpstreamArtifactMaterializationFingerprint
{
    public static string Create(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessMissingUpstreamArtifactMaterializationFacts facts)
    {
        var normalizedInputs = facts.MissingInputs
            .OrderBy(input => input.SourceStepDefinitionId)
            .ThenBy(input => input.ArtifactExpectationId)
            .Select(input => string.Join(
                ":",
                input.SourceStepDefinitionId.ToString("D"),
                input.ArtifactExpectationId.ToString("D"),
                input.SourceStepRunId?.ToString("D") ?? string.Empty,
                input.SourceStepRunStatus?.ToString() ?? string.Empty));
        var normalized = string.Join(
            "|",
            "missing-upstream-artifact-materialization",
            candidate.Run.Id.ToString("D"),
            candidate.StepRun.Id.ToString("D"),
            facts.MaterializationTarget?.SourceStepRunId?.ToString("D") ?? string.Empty,
            string.Join(",", normalizedInputs));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }
}

internal static class ProcessMissingUpstreamArtifactRerunRequestBuilder
{
    public static ProcessAgentStepRerunRequest BuildRequest(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessMissingUpstreamArtifactMaterializationFacts facts)
    {
        var materializationTarget = facts.MaterializationTarget
            ?? throw new ArgumentException("A materialization target is required to build a rerun request.", nameof(facts));

        return new ProcessAgentStepRerunRequest
        {
            StepRunId = materializationTarget.SourceStepRunId!.Value,
            StepRunConcurrencyToken = materializationTarget.SourceStepRunConcurrencyToken,
            OperatorReason = BuildDirective(candidate, facts)
        };
    }

    public static string BuildDirective(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessMissingUpstreamArtifactMaterializationFacts facts)
    {
        var materializationTarget = facts.MaterializationTarget
            ?? throw new ArgumentException("A materialization target is required to build a rerun directive.", nameof(facts));
        var targetMissingInputs = facts.MissingInputs
            .Where(input => input.SourceStepRunId == materializationTarget.SourceStepRunId)
            .ToList();
        var artifactTitles = targetMissingInputs.Count == 0
            ? materializationTarget.ExpectedArtifactTitle
            : string.Join(", ", targetMissingInputs.Select(input => input.ExpectedArtifactTitle).Distinct(StringComparer.OrdinalIgnoreCase));
        return $"Automatic upstream artifact materialization requested. Downstream step '{candidate.StepRun.Title}' cannot proceed because required upstream artifact(s) are missing: {artifactTitles}. Use this step's existing records, artifacts, decisions, and prior execution context to create or repair only the missing required artifact(s). Do not redo unrelated work. When the artifact(s) are recorded, the downstream step will retry from its configured artifact inputs.";
    }
}

internal sealed class ProcessMissingUpstreamArtifactMaterializationJournalCoordinator(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock)
{
    public async Task<bool> RecordAsync(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessMissingUpstreamArtifactMaterializationFacts facts,
        string blockReason,
        CancellationToken cancellationToken)
    {
        var fingerprint = ProcessMissingUpstreamArtifactMaterializationFingerprint.Create(candidate, facts);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingFingerprint = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.ProcessRunId == candidate.Run.Id &&
                    item.StepRunId == candidate.StepRun.Id &&
                    item.EventType == ProcessRuntimeEventTypes.MissingUpstreamArtifactMaterializationRequested &&
                    item.CorrelationId == fingerprint,
                cancellationToken);
        if (existingFingerprint)
        {
            return false;
        }

        var now = clock.GetUtcNow();
        await dbContext.Set<ProcessJournalEntry>().AddAsync(
            new ProcessJournalEntry
            {
                ProcessRunId = candidate.Run.Id,
                StepRunId = candidate.StepRun.Id,
                EventType = ProcessRuntimeEventTypes.MissingUpstreamArtifactMaterializationRequested,
                Title = "Missing upstream artifact materialization requested",
                Description = blockReason,
                CorrelationId = fingerprint,
                OperatingMode = candidate.Run.OperatingMode,
                PolicyVersion = $"definition-version:{candidate.Run.ProcessDefinitionVersionId:D}",
                EnvironmentMode = candidate.Run.OperatingMode.ToString(),
                ReplayContextJson = JsonSerializer.Serialize(new
                {
                    candidate.Run.Id,
                    StepRunId = candidate.StepRun.Id,
                    MaterializationSourceStepRunId = facts.MaterializationTarget?.SourceStepRunId,
                    MissingInputs = facts.MissingInputs.Select(input => new
                    {
                        input.SourceStepTitle,
                        input.ExpectedArtifactTitle,
                        input.ArtifactExpectationId,
                        input.SourceStepDefinitionId,
                        input.SourceStepRunId,
                        input.SourceStepRunStatus
                    }).ToArray()
                }),
                OccurredAtUtc = now
            },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

internal sealed class ProcessMissingUpstreamArtifactMaterializationCoordinator(
    ProcessMissingUpstreamArtifactMaterializationJournalCoordinator journalCoordinator,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ProcessRunAutomationDispatchService> logger)
{
    public async Task<bool> RecordAndRequestAsync(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessMissingUpstreamArtifactMaterializationFacts facts,
        string blockReason,
        CancellationToken cancellationToken)
    {
        if (facts.MaterializationTarget is null)
        {
            await journalCoordinator.RecordAsync(
                candidate,
                facts,
                blockReason,
                cancellationToken);
            logger.LogWarning(
                "Process run {RunId}, step {StepRunId} is missing required upstream artifacts, but no completed, blocked, or failed agent-owned source step is available for automatic materialization. Missing inputs: {MissingInputs}",
                candidate.Run.Id,
                candidate.StepRun.Id,
                string.Join(" | ", facts.MissingInputs.Select(input => $"{input.SourceStepTitle}: {input.ExpectedArtifactTitle}")));
            return true;
        }

        var shouldRequestMaterialization = await journalCoordinator.RecordAsync(
            candidate,
            facts,
            blockReason,
            cancellationToken);
        if (!shouldRequestMaterialization)
        {
            logger.LogInformation(
                "Skipping duplicate upstream artifact materialization request for run {RunId}, blocked downstream step {StepRunId}, source step {SourceStepRunId}; the same missing-artifact fingerprint is already recorded.",
                candidate.Run.Id,
                candidate.StepRun.Id,
                facts.MaterializationTarget.SourceStepRunId);
            return true;
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var rerunResult = await processesService.RerunAgentStepAsync(
            ProcessMissingUpstreamArtifactRerunRequestBuilder.BuildRequest(candidate, facts),
            cancellationToken);
        if (rerunResult.IsFailure)
        {
            logger.LogWarning(
                "Could not request upstream artifact materialization from step {SourceStepRunId} for run {RunId}, blocked downstream step {StepRunId}. Errors: {Errors}",
                facts.MaterializationTarget.SourceStepRunId,
                candidate.Run.Id,
                candidate.StepRun.Id,
                string.Join(" | ", rerunResult.Errors.Select(error => error.Message)));
            return true;
        }

        logger.LogInformation(
            "Requested upstream artifact materialization from step {SourceStepRunId} for blocked downstream step {StepRunId} on process run {RunId}. Missing artifact: {ExpectedArtifactTitle}",
            facts.MaterializationTarget.SourceStepRunId,
            candidate.StepRun.Id,
            candidate.Run.Id,
            facts.MaterializationTarget.ExpectedArtifactTitle);
        return true;
    }
}
