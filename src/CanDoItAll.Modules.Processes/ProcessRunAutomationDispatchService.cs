using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

public interface IProcessRunAutomationDispatchService
{
    Task DispatchAsync(
        Guid processRunId,
        Guid? triggerStepRunId,
        string trigger,
        CancellationToken cancellationToken = default);
}

internal sealed class ProcessRunAutomationDispatchService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IServiceScopeFactory serviceScopeFactory,
    IAiTechnicalAgentBridge technicalAgentBridge,
    IAgentFrameworkWorkspaceService workspaceService,
    IStoragePlacementService storagePlacementService,
    IWorkspacePathResolver workspacePathResolver,
    ILogger<ProcessRunAutomationDispatchService> logger) : IProcessRunAutomationDispatchService
{
    private const string AutomationActor = "process-automation-dispatch";

    public async Task DispatchAsync(
        Guid processRunId,
        Guid? triggerStepRunId,
        string trigger,
        CancellationToken cancellationToken = default)
    {
        if (processRunId == Guid.Empty)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var candidate = await LoadDispatchCandidateAsync(processRunId, cancellationToken);
            if (candidate is null)
            {
                return;
            }

            var startResult = await TransitionStepAsync(
                new ProcessStepTransitionRequest
                {
                    StepRunId = candidate.StepRun.Id,
                    StepRunConcurrencyToken = candidate.StepRun.ConcurrencyToken,
                    TargetStatus = ProcessStepRunStatus.InProgress,
                    Reason = $"Started by the durable process automation dispatcher ({NormalizeTrigger(trigger, triggerStepRunId)}).",
                    DecidedBy = AutomationActor,
                    SuppressAutomationDispatch = true
                },
                cancellationToken);
            if (startResult.IsFailure)
            {
                logger.LogInformation(
                    "Process step {StepRunId} could not be claimed for automation dispatch on run {RunId}. Errors: {Errors}",
                    candidate.StepRun.Id,
                    processRunId,
                    string.Join(" | ", startResult.Errors.Select(error => error.Message)));
                continue;
            }

            try
            {
                var executionResult = await workspaceService.ExecuteRunAsync(
                    new ExecutionRunRequest(
                        candidate.TechnicalAgentId,
                        BuildExecutionPrompt(candidate),
                        Context: new ExecutionInvocationContext(
                            SourceKind: "process-step",
                            SourceId: candidate.StepRun.Id.ToString("D"),
                            CorrelationId: BuildCorrelationId(candidate.StepRun.Id),
                            CausationId: string.IsNullOrWhiteSpace(trigger)
                                ? string.Empty
                                : trigger.Trim(),
                            RequestedBy: AutomationActor,
                            RequestedByKind: "system",
                            MetadataJson: BuildExecutionMetadataJson(candidate, trigger),
                            ProcessRunId: candidate.Run.Id.ToString("D"),
                            ProcessStepId: candidate.StepRun.Id.ToString("D"))),
                    cancellationToken);
                var detail = await workspaceService.GetExecutionRunDetailAsync(executionResult.ExecutionRunId, cancellationToken);

                await ProjectExecutionArtifactsAsync(candidate, detail, cancellationToken);

                var completionStatus = ResolveCompletionStatus(detail.Run);
                var completionReason = BuildCompletionReason(detail.Run, candidate.StepRun.Title);
                var completionResult = await TransitionStepAsync(
                    new ProcessStepTransitionRequest
                    {
                        StepRunId = candidate.StepRun.Id,
                        TargetStatus = completionStatus,
                        Reason = completionReason,
                        DecidedBy = AutomationActor,
                        SuppressAutomationDispatch = true
                    },
                    cancellationToken);
                if (completionResult.IsFailure)
                {
                    throw new InvalidOperationException(string.Join(" | ", completionResult.Errors.Select(error => error.Message)));
                }
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Process automation dispatch failed for run {RunId}, step {StepRunId}.",
                    candidate.Run.Id,
                    candidate.StepRun.Id);

                var failResult = await TransitionStepAsync(
                    new ProcessStepTransitionRequest
                    {
                        StepRunId = candidate.StepRun.Id,
                        TargetStatus = ProcessStepRunStatus.Failed,
                        Reason = $"AgentFramework execution failed: {exception.Message}",
                        DecidedBy = AutomationActor,
                        SuppressAutomationDispatch = true
                    },
                    cancellationToken);
                if (failResult.IsFailure)
                {
                    logger.LogWarning(
                        "Process step {StepRunId} could not be moved to Failed after an execution exception. Errors: {Errors}",
                        candidate.StepRun.Id,
                        string.Join(" | ", failResult.Errors.Select(error => error.Message)));
                }
            }
        }
    }

    private async Task<DispatchCandidate?> LoadDispatchCandidateAsync(
        Guid processRunId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == processRunId, cancellationToken);
        if (run is null || run.Status is ProcessRunStatus.Completed or ProcessRunStatus.Cancelled or ProcessRunStatus.Failed)
        {
            return null;
        }

        var definition = await dbContext.Set<ProcessDefinition>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == run.ProcessDefinitionId, cancellationToken);
        var readySteps = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == processRunId && item.Status == ProcessStepRunStatus.Ready)
            .OrderBy(item => item.Sequence)
            .ToListAsync(cancellationToken);
        if (readySteps.Count == 0)
        {
            return null;
        }

        var stepRunIds = readySteps.Select(item => item.Id).ToList();
        var workBriefsByStepRunId = (await dbContext.Set<ProcessWorkBrief>()
                .AsNoTracking()
                .Where(item => item.ProcessRunId == processRunId && item.StepRunId.HasValue && stepRunIds.Contains(item.StepRunId.Value))
                .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.CreatedAtUtc)
            .GroupBy(item => item.StepRunId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var existingArtifacts = await dbContext.Set<ProcessArtifactRecord>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == processRunId)
            .Select(item => item.ExternalReferenceKey)
            .ToListAsync(cancellationToken);
        var externalReferenceKeys = existingArtifacts
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var stepRun in readySteps)
        {
            if (!stepRun.CurrentExecutorPartyId.HasValue)
            {
                continue;
            }

            var executionRuns = await workspaceService.ListExecutionRunsAsync(
                new ExecutionRunQuery(
                    ProcessRunId: processRunId.ToString("D"),
                    ProcessStepId: stepRun.Id.ToString("D"),
                    Take: 1),
                cancellationToken);
            if (executionRuns.Count > 0)
            {
                continue;
            }

            var summaries = await technicalAgentBridge.GetDirectorySummariesAsync([stepRun.CurrentExecutorPartyId.Value], cancellationToken);
            if (!summaries.TryGetValue(stepRun.CurrentExecutorPartyId.Value, out var technicalAgentSummary) ||
                !technicalAgentSummary.TechnicalAgentId.HasValue ||
                technicalAgentSummary.BindingStatus != AiResourceBindingStatus.Bound)
            {
                continue;
            }

            return new DispatchCandidate(
                run,
                definition,
                stepRun,
                workBriefsByStepRunId.GetValueOrDefault(stepRun.Id),
                technicalAgentSummary.TechnicalAgentId.Value,
                externalReferenceKeys);
        }

        return null;
    }

    private async Task ProjectExecutionArtifactsAsync(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        CancellationToken cancellationToken)
    {
        if (detail.Artifacts.Count == 0)
        {
            return;
        }

        var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        foreach (var artifact in detail.Artifacts)
        {
            var externalReferenceKey = BuildExternalReferenceKey(artifact);
            if (candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinWorkspace(workspaceRoot, fullPath) || !File.Exists(fullPath))
            {
                logger.LogDebug(
                    "Skipping execution artifact projection for run {RunId}, step {StepRunId}, artifact {ArtifactId} because the file path is unavailable.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    artifact.Id);
                continue;
            }

            byte[] content;
            try
            {
                content = await File.ReadAllBytesAsync(fullPath, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Execution artifact {ArtifactId} could not be read for process run {RunId}.",
                    artifact.Id,
                    candidate.Run.Id);
                continue;
            }

            var placement = await storagePlacementService.PlaceAsync(
                new StoragePlacementRequest(
                    Path.GetFileName(fullPath),
                    string.IsNullOrWhiteSpace(artifact.ContentType)
                        ? "application/octet-stream"
                        : artifact.ContentType,
                    content,
                    StorageUsagePurpose.Evidence,
                    ResolveStorageContentKind(artifact.ContentType, fullPath),
                    ProjectId: candidate.Run.ProjectId,
                    RelativePathHint: BuildStorageRelativePath(candidate, artifact)),
                cancellationToken);

            var recordResult = await RecordArtifactAsync(
                new ProcessArtifactRecordRequest
                {
                    ProcessRunId = candidate.Run.Id,
                    StepRunId = candidate.StepRun.Id,
                    ArtifactKind = ResolveProcessArtifactKind(artifact),
                    Title = BuildArtifactTitle(artifact),
                    TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                    SensitivityLevel = ProcessSensitivityLevel.Internal,
                    ProvenanceSummary = $"Projected from AgentFramework execution run {detail.Run.Id:D} artifact '{artifact.RelativePath}'.",
                    AllowedFutureUsageSummary = "Process evidence and audit review.",
                    ReviewSummary = string.IsNullOrWhiteSpace(artifact.Summary)
                        ? detail.Run.ResultSummary
                        : artifact.Summary,
                    ManagedStoragePath = placement.RelativePath,
                    ExternalReferenceKey = externalReferenceKey
                },
                cancellationToken);
            if (recordResult.IsSuccess)
            {
                candidate.ExternalReferenceKeys.Add(externalReferenceKey);
            }
            else
            {
                logger.LogWarning(
                    "Process artifact projection failed for run {RunId}, step {StepRunId}, artifact {ArtifactId}. Errors: {Errors}",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    artifact.Id,
                    string.Join(" | ", recordResult.Errors.Select(error => error.Message)));
            }
        }
    }

    private async Task<Result> TransitionStepAsync(
        ProcessStepTransitionRequest request,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        return await processesService.TransitionStepAsync(request, cancellationToken);
    }

    private async Task<Result<Guid>> RecordArtifactAsync(
        ProcessArtifactRecordRequest request,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        return await processesService.RecordArtifactAsync(request, cancellationToken);
    }

    private static string BuildExecutionPrompt(DispatchCandidate candidate)
    {
        var workBrief = candidate.WorkBrief;
        return $"""
                You are executing a CanDoItAll process step.

                Process: {candidate.Definition.Name}
                Run: {candidate.Run.Name}
                Step: {candidate.StepRun.Title}
                Executor: {candidate.StepRun.CurrentExecutorName}

                Work brief:
                {workBrief?.WorkBriefText ?? "No work brief was captured for this step."}

                Handoff summary:
                {workBrief?.HandoffSummary ?? "None"}

                Expected outcome:
                {workBrief?.ExpectedOutcome ?? "Complete the step and produce durable evidence artifacts."}

                Evidence expectation:
                {workBrief?.EvidenceExpectationSummary ?? "Save any relevant evidence artifacts inside the workspace."}

                Save generated files and execution evidence inside the workspace. Keep the response concise and mention what you completed.
                """;
    }

    private static string BuildCorrelationId(Guid stepRunId)
    {
        return $"process-step:{stepRunId:D}";
    }

    private static string BuildExecutionMetadataJson(DispatchCandidate candidate, string trigger)
    {
        return System.Text.Json.JsonSerializer.Serialize(
            new
            {
                processDefinitionId = candidate.Definition.Id,
                processRunId = candidate.Run.Id,
                processStepRunId = candidate.StepRun.Id,
                processStepTitle = candidate.StepRun.Title,
                trigger = string.IsNullOrWhiteSpace(trigger) ? "process-runtime" : trigger.Trim()
            });
    }

    private static ProcessStepRunStatus ResolveCompletionStatus(ExecutionRunRecord run)
    {
        if (run.State == ExecutionState.WaitingOnTool || run.PendingApprovals.Count > 0)
        {
            return ProcessStepRunStatus.WaitingApproval;
        }

        return run.Outcome == RunOutcome.Succeeded
            ? ProcessStepRunStatus.Completed
            : ProcessStepRunStatus.Failed;
    }

    private static string BuildCompletionReason(ExecutionRunRecord run, string stepTitle)
    {
        if (run.State == ExecutionState.WaitingOnTool || run.PendingApprovals.Count > 0)
        {
            return $"AgentFramework run '{run.Title}' is waiting on approval before '{stepTitle}' can continue.";
        }

        return run.Outcome == RunOutcome.Succeeded
            ? $"AgentFramework run '{run.Title}' completed successfully."
            : string.IsNullOrWhiteSpace(run.ResultSummary)
                ? $"AgentFramework run '{run.Title}' failed."
                : $"AgentFramework run '{run.Title}' failed: {run.ResultSummary}";
    }

    private static string BuildArtifactTitle(ExecutionArtifactRecord artifact)
    {
        return string.IsNullOrWhiteSpace(artifact.DisplayName)
            ? Path.GetFileName(artifact.RelativePath)
            : artifact.DisplayName.Trim();
    }

    private static string BuildExternalReferenceKey(ExecutionArtifactRecord artifact)
    {
        return $"agentframework-artifact:{artifact.Id:D}";
    }

    private static string BuildStorageRelativePath(
        DispatchCandidate candidate,
        ExecutionArtifactRecord artifact)
    {
        return $"process-runs/{candidate.Run.Id:D}/{candidate.StepRun.Id:D}/{Path.GetFileName(artifact.RelativePath)}";
    }

    private static ProcessArtifactKind ResolveProcessArtifactKind(ExecutionArtifactRecord artifact)
    {
        if (artifact.RelativePath.EndsWith("/response.md", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactKind.Transcript;
        }

        return string.Equals(artifact.ArtifactKind, "generated-output", StringComparison.OrdinalIgnoreCase)
            ? ProcessArtifactKind.Deliverable
            : ProcessArtifactKind.Evidence;
    }

    private static StorageContentKind ResolveStorageContentKind(string contentType, string fullPath)
    {
        if (contentType.Contains("markdown", StringComparison.OrdinalIgnoreCase))
        {
            return StorageContentKind.Markdown;
        }

        if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return StorageContentKind.Json;
        }

        if (contentType.Contains("image", StringComparison.OrdinalIgnoreCase))
        {
            return StorageContentKind.Image;
        }

        if (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return StorageContentKind.Pdf;
        }

        return Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".md" => StorageContentKind.Markdown,
            ".json" => StorageContentKind.Json,
            ".svg" => StorageContentKind.Image,
            ".png" => StorageContentKind.Image,
            ".jpg" or ".jpeg" => StorageContentKind.Image,
            ".pdf" => StorageContentKind.Pdf,
            ".txt" or ".log" => StorageContentKind.Log,
            _ => StorageContentKind.Unknown
        };
    }

    private static string NormalizeTrigger(string trigger, Guid? stepRunId)
    {
        if (!string.IsNullOrWhiteSpace(trigger))
        {
            return trigger.Trim();
        }

        return stepRunId.HasValue
            ? $"step:{stepRunId.Value:D}"
            : "process-runtime";
    }

    private static bool IsWithinWorkspace(string workspaceRoot, string fullPath)
    {
        return fullPath.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record DispatchCandidate(
        ProcessRun Run,
        ProcessDefinition Definition,
        ProcessStepRun StepRun,
        ProcessWorkBrief? WorkBrief,
        Guid TechnicalAgentId,
        HashSet<string> ExternalReferenceKeys);
}
