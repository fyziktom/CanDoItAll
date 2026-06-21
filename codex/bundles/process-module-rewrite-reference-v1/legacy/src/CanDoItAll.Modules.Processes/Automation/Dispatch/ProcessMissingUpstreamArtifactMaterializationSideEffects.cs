using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessMissingUpstreamArtifactMaterializationJournalCoordinator(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock)
{
    public async Task<bool> RecordAsync(
        ProcessDispatchPreExecutionRouteFacts routeFacts,
        ProcessMissingUpstreamArtifactMaterializationFacts facts,
        string blockReason,
        CancellationToken cancellationToken)
    {
        var fingerprint = ProcessMissingUpstreamArtifactMaterializationFingerprint.Create(routeFacts, facts);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingFingerprint = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.ProcessRunId == routeFacts.Run.Id &&
                    item.StepRunId == routeFacts.StepRun.Id &&
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
                ProcessRunId = routeFacts.Run.Id,
                StepRunId = routeFacts.StepRun.Id,
                EventType = ProcessRuntimeEventTypes.MissingUpstreamArtifactMaterializationRequested,
                Title = "Missing upstream artifact materialization requested",
                Description = blockReason,
                CorrelationId = fingerprint,
                OperatingMode = routeFacts.Run.OperatingMode,
                PolicyVersion = $"definition-version:{routeFacts.Run.ProcessDefinitionVersionId:D}",
                EnvironmentMode = routeFacts.Run.OperatingMode.ToString(),
                ReplayContextJson = JsonSerializer.Serialize(new
                {
                    routeFacts.Run.Id,
                    StepRunId = routeFacts.StepRun.Id,
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
        ProcessDispatchPreExecutionRouteFacts routeFacts,
        ProcessMissingUpstreamArtifactMaterializationFacts facts,
        string blockReason,
        CancellationToken cancellationToken)
    {
        if (facts.MaterializationTarget is null)
        {
            await journalCoordinator.RecordAsync(
                routeFacts,
                facts,
                blockReason,
                cancellationToken);
            logger.LogWarning(
                "Process run {RunId}, step {StepRunId} is missing required upstream artifacts, but no completed, blocked, or failed agent-owned source step is available for automatic materialization. Missing inputs: {MissingInputs}",
                routeFacts.Run.Id,
                routeFacts.StepRun.Id,
                string.Join(" | ", facts.MissingInputs.Select(input => $"{input.SourceStepTitle}: {input.ExpectedArtifactTitle}")));
            return true;
        }

        var shouldRequestMaterialization = await journalCoordinator.RecordAsync(
            routeFacts,
            facts,
            blockReason,
            cancellationToken);
        if (!shouldRequestMaterialization)
        {
            logger.LogInformation(
                "Skipping duplicate upstream artifact materialization request for run {RunId}, blocked downstream step {StepRunId}, source step {SourceStepRunId}; the same missing-artifact fingerprint is already recorded.",
                routeFacts.Run.Id,
                routeFacts.StepRun.Id,
                facts.MaterializationTarget.SourceStepRunId);
            return true;
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var rerunResult = await processesService.RerunAgentStepAsync(
            ProcessMissingUpstreamArtifactRerunRequestBuilder.BuildRequest(routeFacts, facts),
            cancellationToken);
        if (rerunResult.IsFailure)
        {
            logger.LogWarning(
                "Could not request upstream artifact materialization from step {SourceStepRunId} for run {RunId}, blocked downstream step {StepRunId}. Errors: {Errors}",
                facts.MaterializationTarget.SourceStepRunId,
                routeFacts.Run.Id,
                routeFacts.StepRun.Id,
                string.Join(" | ", rerunResult.Errors.Select(error => error.Message)));
            return true;
        }

        logger.LogInformation(
            "Requested upstream artifact materialization from step {SourceStepRunId} for blocked downstream step {StepRunId} on process run {RunId}. Missing artifact: {ExpectedArtifactTitle}",
            facts.MaterializationTarget.SourceStepRunId,
            routeFacts.StepRun.Id,
            routeFacts.Run.Id,
            facts.MaterializationTarget.ExpectedArtifactTitle);
        return true;
    }
}
