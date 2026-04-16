using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

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
    IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
    ILogger<ProcessRunAutomationDispatchService> logger) : IProcessRunAutomationDispatchService
{
    private const string AutomationActor = "process-automation-dispatch";
    private static readonly Regex RequiredToolNameRegex = new(
        @"\b(?:workspace|browser)_[a-z0-9_]+\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly string[] NegatedRequiredToolPhrases =
    [
        "do not",
        "don't",
        "must not",
        "should not",
        "shall not",
        "cannot",
        "can't",
        "never",
        "without"
    ];

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
                            ProcessStepId: candidate.StepRun.Id.ToString("D")),
                        AutoApprovePendingToolCalls: true),
                    cancellationToken);
                var detail = await workspaceService.GetExecutionRunDetailAsync(executionResult.ExecutionRunId, cancellationToken);

                await ProjectExecutionArtifactsAsync(candidate, detail, cancellationToken);

                var completionStatus = ResolveCompletionStatus(candidate, detail);
                var completionReason = BuildCompletionReason(candidate, detail, candidate.StepRun.Title);
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
        var dispatchableSteps = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == processRunId &&
                (item.Status == ProcessStepRunStatus.Ready || item.Status == ProcessStepRunStatus.WaitingApproval))
            .OrderBy(item => item.Sequence)
            .ToListAsync(cancellationToken);
        if (dispatchableSteps.Count == 0)
        {
            return null;
        }

        var stepRunIds = dispatchableSteps.Select(item => item.Id).ToList();
        var workBriefsByStepRunId = (await dbContext.Set<ProcessWorkBrief>()
                .AsNoTracking()
                .Where(item => item.ProcessRunId == processRunId && item.StepRunId.HasValue && stepRunIds.Contains(item.StepRunId.Value))
                .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.CreatedAtUtc)
            .GroupBy(item => item.StepRunId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var allStepRuns = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == processRunId)
            .ToListAsync(cancellationToken);
        var stepRunsByDefinitionId = allStepRuns
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProcessStepRun>)group
                    .OrderByDescending(item => item.Sequence)
                    .ToList());
        var existingArtifacts = (await dbContext.Set<ProcessArtifactRecord>()
                .AsNoTracking()
                .Where(item => item.ProcessRunId == processRunId)
                .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
        var externalReferenceKeys = existingArtifacts
            .Select(item => item.ExternalReferenceKey)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var readyStepDefinitionIds = dispatchableSteps
            .Select(item => item.StepDefinitionId)
            .Distinct()
            .ToList();
        var artifactInputs = readyStepDefinitionIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepArtifactInputDefinition>()
                .AsNoTracking()
                .Where(item => readyStepDefinitionIds.Contains(item.StepDefinitionId))
                .OrderBy(item => item.DisplayOrder)
                .ToListAsync(cancellationToken);
        var artifactInputsByStepDefinitionId = artifactInputs
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ProcessStepArtifactInputDefinition>)group.ToList());
        var artifactExpectationIds = artifactInputs
            .Select(item => item.ArtifactExpectationId)
            .Distinct()
            .ToList();
        var artifactExpectationsById = artifactExpectationIds.Count == 0
            ? new Dictionary<Guid, ProcessArtifactExpectation>()
            : await dbContext.Set<ProcessArtifactExpectation>()
                .AsNoTracking()
                .Where(item => artifactExpectationIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        var sourceStepDefinitionIds = artifactExpectationsById.Values
            .Select(item => item.StepDefinitionId)
            .Distinct()
            .ToList();
        var sourceStepsById = sourceStepDefinitionIds.Count == 0
            ? new Dictionary<Guid, ProcessStepDefinition>()
            : await dbContext.Set<ProcessStepDefinition>()
                .AsNoTracking()
                .Where(item => sourceStepDefinitionIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);

        foreach (var stepRun in dispatchableSteps)
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

            artifactInputsByStepDefinitionId.TryGetValue(stepRun.StepDefinitionId, out var configuredArtifactInputs);
            return new DispatchCandidate(
                run,
                definition,
                stepRun,
                workBriefsByStepRunId.GetValueOrDefault(stepRun.Id),
                technicalAgentSummary.TechnicalAgentId.Value,
                await LoadExpectedArtifactsAsync(dbContext, stepRun.StepDefinitionId, cancellationToken),
                BuildResolvedArtifactInputs(
                    configuredArtifactInputs ?? [],
                    artifactExpectationsById,
                    sourceStepsById,
                    stepRunsByDefinitionId,
                    existingArtifacts),
                externalReferenceKeys);
        }

        return null;
    }

    private async Task ProjectExecutionArtifactsAsync(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        CancellationToken cancellationToken)
    {
        var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        foreach (var artifact in detail.Artifacts)
        {
            if (IsTransientExecutionArtifact(artifact))
            {
                logger.LogDebug(
                    "Skipping transient execution artifact projection for run {RunId}, step {StepRunId}, artifact {ArtifactId}, path {RelativePath}.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    artifact.Id,
                    artifact.RelativePath);
                continue;
            }

            var matchedExpectation = ResolveArtifactExpectation(candidate, artifact);
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
                    ArtifactExpectationId = matchedExpectation?.Id,
                    ArtifactKind = matchedExpectation?.ArtifactKind ?? ResolveProcessArtifactKind(candidate, artifact),
                    Title = matchedExpectation?.Title ?? BuildArtifactTitle(artifact),
                    TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                    SensitivityLevel = matchedExpectation?.SensitivityLevel ?? ProcessSensitivityLevel.Internal,
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

        await ProjectProviderNativeBrowserArtifactsAsync(candidate, detail, workspaceRoot, cancellationToken);
    }

    private async Task ProjectProviderNativeBrowserArtifactsAsync(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        if (candidate.ExpectedArtifacts.Count == 0 || string.IsNullOrWhiteSpace(detail.Run.SerializedSessionStateJson))
        {
            return;
        }

        var browserOutputsByToolName = ResolveSuccessfulSessionToolOutputFiles(detail.Run.SerializedSessionStateJson);
        if (browserOutputsByToolName.Count == 0)
        {
            return;
        }

        var browserWorkingDirectory = ResolveProviderNativeBrowserWorkingDirectory(detail);
        if (string.IsNullOrWhiteSpace(browserWorkingDirectory))
        {
            return;
        }

        var workspaceScope = WorkspaceScopeDescriptor.Organization(
            databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N"));

        foreach (var expectedArtifact in candidate.ExpectedArtifacts)
        {
            if (!TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var expectedRelativePath))
            {
                continue;
            }

            if (detail.Artifacts.Any(artifact => ResolveArtifactExpectationId(candidate, artifact) == expectedArtifact.Id))
            {
                continue;
            }

            var requiredToolName = ResolveProviderNativeBrowserToolName(expectedRelativePath);
            if (string.IsNullOrWhiteSpace(requiredToolName) ||
                !browserOutputsByToolName.TryGetValue(requiredToolName, out var outputFileNames))
            {
                continue;
            }

            var matchedOutputFileName = outputFileNames.FirstOrDefault(outputFileName =>
                MatchesExpectedBrowserOutputFile(expectedRelativePath, outputFileName));
            if (string.IsNullOrWhiteSpace(matchedOutputFileName))
            {
                continue;
            }

            var sourceFullPath = Path.GetFullPath(Path.Combine(
                browserWorkingDirectory,
                matchedOutputFileName.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinWorkspace(workspaceRoot, sourceFullPath) || !File.Exists(sourceFullPath))
            {
                logger.LogDebug(
                    "Skipping provider-native browser artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because source file {SourcePath} is unavailable.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title,
                    sourceFullPath);
                continue;
            }

            var projectedRelativePath = ResolveScopedManagedRelativePath(workspaceScope, expectedRelativePath);
            var targetFullPath = Path.GetFullPath(Path.Combine(
                workspaceRoot,
                projectedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinWorkspace(workspaceRoot, targetFullPath))
            {
                logger.LogWarning(
                    "Skipping provider-native browser artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because target path '{ExpectedPath}' resolves outside the workspace root.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title,
                    projectedRelativePath);
                continue;
            }

            try
            {
                var targetDirectory = Path.GetDirectoryName(targetFullPath);
                if (!string.IsNullOrWhiteSpace(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                if (!string.Equals(sourceFullPath, targetFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(sourceFullPath, targetFullPath, overwrite: true);
                }

                var content = await File.ReadAllBytesAsync(targetFullPath, cancellationToken);
                var syntheticArtifact = new ExecutionArtifactRecord(
                    Guid.NewGuid(),
                    detail.Run.Id,
                    "generated-output",
                    expectedArtifact.Title,
                    projectedRelativePath,
                    GuessContentTypeFromPath(targetFullPath),
                    requiredToolName,
                    $"Projected provider-native browser output '{matchedOutputFileName}' into the required managed artifact path.",
                    DateTimeOffset.UtcNow);
                var externalReferenceKey = BuildProviderNativeBrowserArtifactExternalReferenceKey(
                    detail.Run.Id,
                    projectedRelativePath);
                if (candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
                {
                    continue;
                }

                var placement = await storagePlacementService.PlaceAsync(
                    new StoragePlacementRequest(
                        Path.GetFileName(targetFullPath),
                        syntheticArtifact.ContentType,
                        content,
                        StorageUsagePurpose.Evidence,
                        ResolveStorageContentKind(syntheticArtifact.ContentType, targetFullPath),
                        ProjectId: candidate.Run.ProjectId,
                        RelativePathHint: BuildStorageRelativePath(candidate, syntheticArtifact)),
                    cancellationToken);

                var recordResult = await RecordArtifactAsync(
                    new ProcessArtifactRecordRequest
                    {
                        ProcessRunId = candidate.Run.Id,
                        StepRunId = candidate.StepRun.Id,
                        ArtifactExpectationId = expectedArtifact.Id,
                        ArtifactKind = expectedArtifact.ArtifactKind,
                        Title = expectedArtifact.Title,
                        TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                        SensitivityLevel = expectedArtifact.SensitivityLevel,
                        ProvenanceSummary = $"Projected from provider-native browser output '{matchedOutputFileName}' for AgentFramework execution run {detail.Run.Id:D}.",
                        AllowedFutureUsageSummary = "Process evidence and audit review.",
                        ReviewSummary = syntheticArtifact.Summary,
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
                        "Provider-native browser artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                        candidate.Run.Id,
                        candidate.StepRun.Id,
                        expectedArtifact.Title,
                        string.Join(" | ", recordResult.Errors.Select(error => error.Message)));
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Provider-native browser artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title);
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
        var builder = new StringBuilder();
        builder.AppendLine("You are executing a CanDoItAll process step.");
        builder.AppendLine();
        builder.AppendLine($"Process: {candidate.Definition.Name}");
        builder.AppendLine($"Run: {candidate.Run.Name}");
        builder.AppendLine($"Step: {candidate.StepRun.Title}");
        builder.AppendLine($"Executor: {candidate.StepRun.CurrentExecutorName}");
        builder.AppendLine();
        builder.AppendLine("Run objective:");
        builder.AppendLine(string.IsNullOrWhiteSpace(candidate.Run.TriggerReason)
            ? string.IsNullOrWhiteSpace(candidate.Definition.Summary)
                ? candidate.Definition.ValueStatement
                : candidate.Definition.Summary
            : candidate.Run.TriggerReason);
        builder.AppendLine();
        builder.AppendLine("Work brief:");
        builder.AppendLine(workBrief?.WorkBriefText ?? "No work brief was captured for this step.");
        builder.AppendLine();
        builder.AppendLine("Handoff summary:");
        builder.AppendLine(workBrief?.HandoffSummary ?? "None");
        builder.AppendLine();
        builder.AppendLine("Expected outcome:");
        builder.AppendLine(workBrief?.ExpectedOutcome ?? "Complete the step and produce durable evidence artifacts.");
        builder.AppendLine();
        builder.AppendLine("Evidence expectation:");
        builder.AppendLine(workBrief?.EvidenceExpectationSummary ?? "Save any relevant evidence artifacts inside the workspace.");
        builder.AppendLine();
        builder.AppendLine("Required output artifacts:");
        builder.AppendLine(BuildExpectedArtifactSummary(candidate.ExpectedArtifacts));
        builder.AppendLine();
        builder.AppendLine("Upstream artifacts:");
        builder.AppendLine(BuildArtifactInputSummary(candidate.ArtifactInputs));
        builder.AppendLine();
        builder.AppendLine("Execution rules:");
        builder.AppendLine("- Complete the actual work described in the work brief and expected outcome before writing summary artifacts.");
        builder.AppendLine("- Required output artifacts are evidence of completed work. They do not replace code changes, runnable outputs, tests, screenshots, or other concrete deliverables.");
        builder.AppendLine("- Do not execute helper scripts, app launches, browser proof, release rollout, or other side actions unless the current step contract or required artifacts explicitly call for them.");
        builder.AppendLine("- If the step names a workspace path, app, build, test, launch, or browser proof, that concrete output must exist before you conclude.");
        builder.Append("Before concluding, create one durable workspace artifact for every required output listed above. If required upstream artifacts are missing or the concrete deliverable does not exist, stop and say so explicitly. Keep the response concise and mention what you completed.");
        return builder.ToString();
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

    private static ProcessStepRunStatus ResolveCompletionStatus(DispatchCandidate candidate, ExecutionRunDetail detail)
    {
        var run = detail.Run;
        if (run.State == ExecutionState.WaitingOnTool || run.PendingApprovals.Count > 0)
        {
            return ProcessStepRunStatus.WaitingApproval;
        }

        if (run.Outcome != RunOutcome.Succeeded)
        {
            return ProcessStepRunStatus.Failed;
        }

        if (ResolveMissingRequiredToolExecutions(candidate, detail).Count > 0)
        {
            return ProcessStepRunStatus.Failed;
        }

        return ResolveUnresolvedCriticalToolFailures(detail).Count == 0
            ? ProcessStepRunStatus.Completed
            : ProcessStepRunStatus.Failed;
    }

    private static string BuildCompletionReason(DispatchCandidate candidate, ExecutionRunDetail detail, string stepTitle)
    {
        var run = detail.Run;
        if (run.State == ExecutionState.WaitingOnTool || run.PendingApprovals.Count > 0)
        {
            return $"AgentFramework run '{run.Title}' is waiting on approval before '{stepTitle}' can continue.";
        }

        var unresolvedFailures = ResolveUnresolvedCriticalToolFailures(detail);
        if (unresolvedFailures.Count > 0)
        {
            var summary = string.Join(
                "; ",
                unresolvedFailures
                    .Take(2)
                    .Select(item => $"{item.ToolName}: {item.ExitSummary}"));
            return $"AgentFramework run '{run.Title}' failed because critical tool executions did not recover: {summary}";
        }

        var missingRequiredTools = ResolveMissingRequiredToolExecutions(candidate, detail);
        if (missingRequiredTools.Count > 0)
        {
            return $"AgentFramework run '{run.Title}' did not execute the required step tools successfully: {string.Join(", ", missingRequiredTools)}";
        }

        return run.Outcome == RunOutcome.Succeeded
            ? $"AgentFramework run '{run.Title}' completed successfully."
            : string.IsNullOrWhiteSpace(run.ResultSummary)
                ? $"AgentFramework run '{run.Title}' failed."
                : $"AgentFramework run '{run.Title}' failed: {run.ResultSummary}";
    }

    private static IReadOnlyList<ToolExecutionReceiptRecord> ResolveUnresolvedCriticalToolFailures(ExecutionRunDetail detail)
    {
        return detail.ToolReceipts
            .Where(IsCriticalToolReceipt)
            .GroupBy(
                item => string.Join(
                    "|",
                    NormalizeToolToken(item.ToolName),
                    item.RequestSummary.Trim(),
                    item.WorkingDirectory.Trim()),
                StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(item => item.CompletedAtUtc)
                .ThenByDescending(item => item.StartedAtUtc)
                .First())
            .Where(IsFailedToolReceipt)
            .ToList();
    }

    private static IReadOnlyList<string> ResolveMissingRequiredToolExecutions(
        DispatchCandidate candidate,
        ExecutionRunDetail detail)
    {
        var requiredToolNames = ResolveRequiredToolNames(candidate);
        if (requiredToolNames.Count == 0)
        {
            return [];
        }

        var successfulToolNames = ResolveSuccessfulToolNames(detail);
        var missing = new List<string>();

        foreach (var requiredToolName in requiredToolNames)
        {
            if (!successfulToolNames.Contains(requiredToolName))
            {
                missing.Add(requiredToolName);
            }
        }

        return missing;
    }

    private static ISet<string> ResolveSuccessfulToolNames(ExecutionRunDetail detail)
    {
        var successfulToolNames = detail.ToolReceipts
            .Where(receipt => !IsFailedToolReceipt(receipt))
            .Select(receipt => NormalizeToolToken(receipt.ToolName))
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var toolName in ResolveSuccessfulSessionToolNames(detail.Run.SerializedSessionStateJson))
        {
            successfulToolNames.Add(toolName);
        }

        return successfulToolNames;
    }

    private static IReadOnlyList<string> ResolveSuccessfulSessionToolNames(string? serializedSessionStateJson)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionStateJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(serializedSessionStateJson);
            if (!document.RootElement.TryGetProperty("stateBag", out var stateBag) ||
                !stateBag.TryGetProperty("InMemoryChatHistoryProvider", out var historyProvider) ||
                !historyProvider.TryGetProperty("messages", out var messages) ||
                messages.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var toolNamesByCallId = new Dictionary<string, string>(StringComparer.Ordinal);
            var successfulToolNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var message in messages.EnumerateArray())
            {
                if (!message.TryGetProperty("contents", out var contents) ||
                    contents.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var content in contents.EnumerateArray())
                {
                    if (!content.TryGetProperty("$type", out var typeElement))
                    {
                        continue;
                    }

                    var contentType = typeElement.GetString();
                    if (string.Equals(contentType, "functionCall", StringComparison.Ordinal))
                    {
                        var callId = content.TryGetProperty("callId", out var callIdElement)
                            ? callIdElement.GetString()
                            : null;
                        var toolName = content.TryGetProperty("name", out var nameElement)
                            ? NormalizeToolToken(nameElement.GetString() ?? string.Empty)
                            : string.Empty;
                        if (!string.IsNullOrWhiteSpace(callId) && !string.IsNullOrWhiteSpace(toolName))
                        {
                            toolNamesByCallId[callId] = toolName;
                        }

                        continue;
                    }

                    if (!string.Equals(contentType, "functionResult", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var resultCallId = content.TryGetProperty("callId", out var resultCallIdElement)
                        ? resultCallIdElement.GetString()
                        : null;
                    if (string.IsNullOrWhiteSpace(resultCallId) ||
                        !toolNamesByCallId.TryGetValue(resultCallId, out var recordedToolName) ||
                        !content.TryGetProperty("result", out var resultElement) ||
                        !IsSuccessfulSessionFunctionResult(resultElement))
                    {
                        continue;
                    }

                    successfulToolNames.Add(recordedToolName);
                }
            }

            return successfulToolNames.ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveSuccessfulSessionToolOutputFiles(string serializedSessionStateJson)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionStateJson))
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        }

        try
        {
            using var document = JsonDocument.Parse(serializedSessionStateJson);
            if (!document.RootElement.TryGetProperty("stateBag", out var stateBag) ||
                !stateBag.TryGetProperty("InMemoryChatHistoryProvider", out var historyProvider) ||
                !historyProvider.TryGetProperty("messages", out var messages) ||
                messages.ValueKind != JsonValueKind.Array)
            {
                return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            }

            var callsById = new Dictionary<string, SessionToolCall>(StringComparer.Ordinal);
            var outputFilesByToolName = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (var message in messages.EnumerateArray())
            {
                if (!message.TryGetProperty("contents", out var contents) ||
                    contents.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var content in contents.EnumerateArray())
                {
                    if (!content.TryGetProperty("$type", out var typeElement))
                    {
                        continue;
                    }

                    var contentType = typeElement.GetString();
                    if (string.Equals(contentType, "functionCall", StringComparison.Ordinal))
                    {
                        var callId = content.TryGetProperty("callId", out var callIdElement)
                            ? callIdElement.GetString()
                            : null;
                        var toolName = content.TryGetProperty("name", out var nameElement)
                            ? NormalizeToolToken(nameElement.GetString() ?? string.Empty)
                            : string.Empty;
                        var outputFileName = TryResolveSessionToolOutputFileName(content);
                        if (!string.IsNullOrWhiteSpace(callId) &&
                            !string.IsNullOrWhiteSpace(toolName) &&
                            !string.IsNullOrWhiteSpace(outputFileName))
                        {
                            callsById[callId] = new SessionToolCall(toolName, outputFileName);
                        }

                        continue;
                    }

                    if (!string.Equals(contentType, "functionResult", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var resultCallId = content.TryGetProperty("callId", out var resultCallIdElement)
                        ? resultCallIdElement.GetString()
                        : null;
                    if (string.IsNullOrWhiteSpace(resultCallId) ||
                        !callsById.TryGetValue(resultCallId, out var call) ||
                        !content.TryGetProperty("result", out var resultElement) ||
                        !IsSuccessfulSessionFunctionResult(resultElement))
                    {
                        continue;
                    }

                    if (!outputFilesByToolName.TryGetValue(call.ToolName, out var outputFiles))
                    {
                        outputFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        outputFilesByToolName[call.ToolName] = outputFiles;
                    }

                    outputFiles.Add(WorkspaceScopeDescriptor.NormalizeRelativePath(call.OutputFileName));
                }
            }

            return outputFilesByToolName.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        }
    }

    private static bool IsSuccessfulSessionFunctionResult(JsonElement result)
    {
        switch (result.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            {
                return false;
            }
            case JsonValueKind.False:
            {
                return false;
            }
            case JsonValueKind.True:
            case JsonValueKind.Number:
            {
                return true;
            }
            case JsonValueKind.String:
            {
                var text = result.GetString();
                return !string.IsNullOrWhiteSpace(text) &&
                       !text.TrimStart().StartsWith("Error", StringComparison.OrdinalIgnoreCase);
            }
            case JsonValueKind.Array:
            {
                return result.GetArrayLength() > 0;
            }
            case JsonValueKind.Object:
            {
                if (result.TryGetProperty("succeeded", out var succeededElement))
                {
                    return succeededElement.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.String when bool.TryParse(succeededElement.GetString(), out var succeeded) => succeeded,
                        _ => false
                    };
                }

                if (result.TryGetProperty("receipt", out var receiptElement) &&
                    receiptElement.ValueKind == JsonValueKind.Object &&
                    receiptElement.TryGetProperty("outcome", out var outcomeElement))
                {
                    var outcome = outcomeElement.GetString();
                    return !string.IsNullOrWhiteSpace(outcome) &&
                           !outcome.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) &&
                           !outcome.StartsWith("Denied", StringComparison.OrdinalIgnoreCase) &&
                           !outcome.StartsWith("TimedOut", StringComparison.OrdinalIgnoreCase);
                }

                if (result.TryGetProperty("$type", out _))
                {
                    return true;
                }

                return result.EnumerateObject().Any();
            }
            default:
            {
                return false;
            }
        }
    }

    private static string? TryResolveSessionToolOutputFileName(JsonElement functionCallContent)
    {
        if (!functionCallContent.TryGetProperty("arguments", out var argumentsElement) ||
            argumentsElement.ValueKind != JsonValueKind.Object ||
            !argumentsElement.TryGetProperty("filename", out var fileNameElement) ||
            fileNameElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var fileName = fileNameElement.GetString();
        return string.IsNullOrWhiteSpace(fileName)
            ? null
            : fileName.Trim();
    }

    private static IReadOnlyList<string> ResolveRequiredToolNames(DispatchCandidate candidate)
    {
        var workBriefText = candidate.WorkBrief?.WorkBriefText;
        if (string.IsNullOrWhiteSpace(workBriefText))
        {
            return [];
        }

        return RequiredToolNameRegex.Matches(workBriefText)
            .Where(match => !IsNegatedRequiredToolReference(workBriefText, match))
            .Select(match => NormalizeToolToken(match.Value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsNegatedRequiredToolReference(string workBriefText, Match match)
    {
        if (!match.Success)
        {
            return false;
        }

        var segmentStart = FindInstructionSegmentStart(workBriefText, match.Index);
        var contextLength = match.Index - segmentStart;
        if (contextLength <= 0)
        {
            return false;
        }

        var context = workBriefText.Substring(segmentStart, contextLength);
        if (string.IsNullOrWhiteSpace(context))
        {
            return false;
        }

        var normalizedContext = Regex.Replace(context, @"\s+", " ").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedContext))
        {
            return false;
        }

        foreach (var phrase in NegatedRequiredToolPhrases)
        {
            if (normalizedContext.Contains(phrase, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int FindInstructionSegmentStart(string workBriefText, int matchIndex)
    {
        if (matchIndex <= 0)
        {
            return 0;
        }

        var segmentStart = 0;
        for (var index = matchIndex - 1; index >= 0; index--)
        {
            var current = workBriefText[index];
            if (current is '\r' or '\n' or '.' or '!' or '?' or ';')
            {
                segmentStart = index + 1;
                break;
            }
        }

        return segmentStart;
    }

    private static bool IsCriticalToolReceipt(ToolExecutionReceiptRecord receipt)
    {
        return string.Equals(receipt.ToolFamily, "workspace-process", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFailedToolReceipt(ToolExecutionReceiptRecord receipt)
    {
        if (string.IsNullOrWhiteSpace(receipt.ExitSummary))
        {
            return false;
        }

        return receipt.ExitSummary.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ||
               receipt.ExitSummary.StartsWith("Denied", StringComparison.OrdinalIgnoreCase) ||
               receipt.ExitSummary.StartsWith("TimedOut", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeToolToken(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('-', '_').Trim().ToLowerInvariant();
    }

    private static string? ResolveProviderNativeBrowserWorkingDirectory(ExecutionRunDetail detail)
    {
        return detail.ToolReceipts
            .Where(receipt =>
                string.Equals(NormalizeToolToken(receipt.ToolName), "local_mcp_launch", StringComparison.Ordinal) &&
                receipt.RequestSummary.Contains("@playwright/mcp", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(receipt.WorkingDirectory) &&
                !IsFailedToolReceipt(receipt))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .Select(receipt => receipt.WorkingDirectory.Trim())
            .FirstOrDefault();
    }

    private static string ResolveProviderNativeBrowserToolName(string expectedRelativePath)
    {
        return Path.GetExtension(expectedRelativePath).ToLowerInvariant() switch
        {
            ".png" => "browser_take_screenshot",
            ".yml" or ".yaml" => "browser_snapshot",
            ".log" or ".txt" => "browser_console_messages",
            _ => string.Empty
        };
    }

    private static bool MatchesExpectedBrowserOutputFile(string expectedRelativePath, string outputFileName)
    {
        var normalizedExpectedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(expectedRelativePath);
        var normalizedOutputPath = WorkspaceScopeDescriptor.NormalizeRelativePath(outputFileName);
        if (string.Equals(normalizedExpectedPath, normalizedOutputPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var expectedFileName = Path.GetFileName(normalizedExpectedPath);
        var outputFileNameOnly = Path.GetFileName(normalizedOutputPath);
        if (!string.Equals(expectedFileName, outputFileNameOnly, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expectedDirectoryName = Path.GetFileName(Path.GetDirectoryName(normalizedExpectedPath) ?? string.Empty);
        var outputDirectoryName = Path.GetFileName(Path.GetDirectoryName(normalizedOutputPath) ?? string.Empty);
        return string.Equals(expectedDirectoryName, outputDirectoryName, StringComparison.OrdinalIgnoreCase);
    }

    private static string GuessContentTypeFromPath(string fullPath)
    {
        return Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".svg" => "image/svg+xml",
            ".yml" or ".yaml" => "text/yaml",
            ".log" or ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
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

    private static string BuildProviderNativeBrowserArtifactExternalReferenceKey(Guid executionRunId, string relativePath)
    {
        return $"agentframework-browser-artifact:{executionRunId:D}:{WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath)}";
    }

    private static string BuildStorageRelativePath(
        DispatchCandidate candidate,
        ExecutionArtifactRecord artifact)
    {
        return $"process-runs/{candidate.Run.Id:D}/{candidate.StepRun.Id:D}/{Path.GetFileName(artifact.RelativePath)}";
    }

    private static ProcessArtifactKind ResolveProcessArtifactKind(
        DispatchCandidate candidate,
        ExecutionArtifactRecord artifact)
    {
        var matchedExpectation = ResolveArtifactExpectation(candidate, artifact);
        if (matchedExpectation is not null)
        {
            return matchedExpectation.ArtifactKind;
        }

        if (artifact.RelativePath.EndsWith("/response.md", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactKind.Transcript;
        }

        var relativePath = artifact.RelativePath.Replace('\\', '/');
        var fileName = Path.GetFileName(relativePath);
        var extension = Path.GetExtension(fileName);

        if (artifact.ContentType.Contains("image", StringComparison.OrdinalIgnoreCase) ||
            IsImageExtension(extension))
        {
            return ProcessArtifactKind.Evidence;
        }

        if (ContainsArtifactHint(fileName, "checklist"))
        {
            return ProcessArtifactKind.Checklist;
        }

        if (ContainsArtifactHint(fileName, "decision"))
        {
            return ProcessArtifactKind.Decision;
        }

        if (ContainsArtifactHint(fileName, "brief"))
        {
            return ProcessArtifactKind.Brief;
        }

        if (ContainsArtifactHint(fileName, "prompt"))
        {
            return ProcessArtifactKind.Prompt;
        }

        if (ContainsArtifactHint(fileName, "dataset"))
        {
            return ProcessArtifactKind.Dataset;
        }

        if (ContainsArtifactHint(fileName, "log") ||
            ContainsArtifactHint(fileName, "transcript") ||
            ContainsArtifactHint(fileName, "stdout") ||
            ContainsArtifactHint(fileName, "stderr") ||
            extension.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactKind.Transcript;
        }

        return string.Equals(artifact.ArtifactKind, "generated-output", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               IsCodeOrProjectExtension(extension)
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

    private static IReadOnlyList<DispatchArtifactInput> BuildResolvedArtifactInputs(
        IReadOnlyList<ProcessStepArtifactInputDefinition> configuredInputs,
        IReadOnlyDictionary<Guid, ProcessArtifactExpectation> artifactExpectationsById,
        IReadOnlyDictionary<Guid, ProcessStepDefinition> sourceStepsById,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProcessStepRun>> stepRunsByDefinitionId,
        IReadOnlyList<ProcessArtifactRecord> existingArtifacts)
    {
        if (configuredInputs.Count == 0)
        {
            return [];
        }

        var resolvedInputs = new List<DispatchArtifactInput>(configuredInputs.Count);
        foreach (var configuredInput in configuredInputs)
        {
            if (!artifactExpectationsById.TryGetValue(configuredInput.ArtifactExpectationId, out var artifactExpectation))
            {
                continue;
            }

            sourceStepsById.TryGetValue(artifactExpectation.StepDefinitionId, out var sourceStepDefinition);
            stepRunsByDefinitionId.TryGetValue(artifactExpectation.StepDefinitionId, out var sourceStepRuns);
            var sourceStepRunIds = sourceStepRuns?
                .Select(item => item.Id)
                .ToHashSet()
                ?? [];
            var matchingArtifacts = existingArtifacts
                .Where(item =>
                    item.StepRunId.HasValue &&
                    sourceStepRunIds.Contains(item.StepRunId.Value) &&
                    SatisfiesExpectedArtifactInput(item, artifactExpectation))
                .OrderByDescending(item => item.CreatedAtUtc)
                .Take(3)
                .Select(item => new DispatchArtifactReference(
                    item.Title,
                    item.ArtifactKind.ToString(),
                    item.ManagedStoragePath,
                    item.ReviewSummary,
                    item.ProvenanceSummary))
                .ToList();

            resolvedInputs.Add(new DispatchArtifactInput(
                sourceStepDefinition?.Title ?? "Unknown upstream step",
                artifactExpectation.Title,
                matchingArtifacts));
        }

        return resolvedInputs;
    }

    private static string BuildArtifactInputSummary(IReadOnlyList<DispatchArtifactInput> artifactInputs)
    {
        if (artifactInputs.Count == 0)
        {
            return "No configured upstream artifact inputs for this step.";
        }

        var builder = new StringBuilder();
        foreach (var artifactInput in artifactInputs)
        {
            builder.Append("- Source step: ");
            builder.Append(artifactInput.SourceStepTitle);
            builder.Append(" | Expected artifact: ");
            builder.AppendLine(artifactInput.ExpectedArtifactTitle);

            if (artifactInput.Artifacts.Count == 0)
            {
                builder.AppendLine("  No recorded upstream artifacts are attached yet. If the contract cannot be fulfilled without them, stop and say so explicitly.");
                continue;
            }

            foreach (var artifact in artifactInput.Artifacts)
            {
                builder.Append("  - ");
                builder.Append(artifact.Title);
                builder.Append(" [");
                builder.Append(artifact.ArtifactKind);
                builder.Append(']');
                if (!string.IsNullOrWhiteSpace(artifact.ManagedStoragePath))
                {
                    builder.Append(" @ ");
                    builder.Append(artifact.ManagedStoragePath);
                }

                builder.AppendLine();
                if (!string.IsNullOrWhiteSpace(artifact.ReviewSummary))
                {
                    builder.Append("    Review: ");
                    builder.AppendLine(TrimForPrompt(artifact.ReviewSummary, 240));
                }

                if (!string.IsNullOrWhiteSpace(artifact.ProvenanceSummary))
                {
                    builder.Append("    Provenance: ");
                    builder.AppendLine(TrimForPrompt(artifact.ProvenanceSummary, 240));
                }
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string TrimForPrompt(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.ReplaceLineEndings(" ").Trim();
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..maxLength].TrimEnd() + "...";
    }

    private static bool SatisfiesExpectedArtifactInput(
        ProcessArtifactRecord artifact,
        ProcessArtifactExpectation expectation)
    {
        if (artifact.ArtifactKind != expectation.ArtifactKind)
        {
            return false;
        }

        if (artifact.ArtifactExpectationId.HasValue)
        {
            return artifact.ArtifactExpectationId.Value == expectation.Id;
        }

        return string.Equals(artifact.Title, expectation.Title, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildExpectedArtifactSummary(IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts)
    {
        if (expectedArtifacts.Count == 0)
        {
            return "No explicit artifact outputs are configured for this step.";
        }

        var builder = new StringBuilder();
        foreach (var expectedArtifact in expectedArtifacts)
        {
            builder.Append("- ");
            builder.Append(expectedArtifact.Title);
            builder.Append(" [");
            builder.Append(expectedArtifact.ArtifactKind);
            builder.Append(']');
            if (expectedArtifact.IsRequired)
            {
                builder.Append(" required");
            }

            builder.AppendLine();
            if (!string.IsNullOrWhiteSpace(expectedArtifact.ValidationRequirementSummary))
            {
                builder.Append("  Validation: ");
                builder.AppendLine(TrimForPrompt(expectedArtifact.ValidationRequirementSummary, 240));
            }

            builder.Append("  Trust: ");
            builder.Append(expectedArtifact.TrustRequirement);
            builder.Append(" | Sensitivity: ");
            builder.AppendLine(expectedArtifact.SensitivityLevel.ToString());
        }

        return builder.ToString().TrimEnd();
    }

    private static Guid? ResolveArtifactExpectationId(
        DispatchCandidate candidate,
        ExecutionArtifactRecord artifact)
    {
        return ResolveArtifactExpectation(candidate, artifact)?.Id;
    }

    private static DispatchArtifactExpectation? ResolveArtifactExpectation(
        DispatchCandidate candidate,
        ExecutionArtifactRecord artifact)
    {
        var matchedExpectationId = MatchExpectedArtifactId(candidate.ExpectedArtifacts, artifact);
        if (!matchedExpectationId.HasValue)
        {
            return null;
        }

        return candidate.ExpectedArtifacts.FirstOrDefault(item => item.Id == matchedExpectationId.Value);
    }

    internal static Guid? MatchExpectedArtifactId(
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        ExecutionArtifactRecord artifact)
    {
        if (expectedArtifacts.Count == 0)
        {
            return null;
        }

        if (IsTransientExecutionArtifact(artifact))
        {
            return null;
        }

        var relativePath = artifact.RelativePath.Replace('\\', '/');
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(relativePath);
        var displayName = BuildArtifactTitle(artifact);
        var displaySlug = FileSafeSlugBuilder.Build(displayName);
        var fileSlug = FileSafeSlugBuilder.Build(fileNameWithoutExtension);
        var expectedKind = ResolveExpectedArtifactKind(artifact);
        var strongMatches = expectedArtifacts
            .Where(item => MatchesExpectedArtifact(item, relativePath, displayName, displaySlug, fileSlug))
            .ToList();
        if (strongMatches.Count == 1)
        {
            return strongMatches[0].Id;
        }

        if (strongMatches.Count > 1)
        {
            var kindMatches = strongMatches
                .Where(item => item.ArtifactKind == expectedKind)
                .ToList();
            if (kindMatches.Count == 1)
            {
                return kindMatches[0].Id;
            }
        }

        return null;
    }

    private static bool MatchesExpectedArtifact(
        DispatchArtifactExpectation expectedArtifact,
        string relativePath,
        string displayName,
        string displaySlug,
        string fileSlug)
    {
        if (TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var expectedRelativePath))
        {
            return string.Equals(
                NormalizeManagedRelativePathForComparison(expectedRelativePath),
                NormalizeManagedRelativePathForComparison(relativePath),
                StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(expectedArtifact.Title, displayName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var expectedSlug = FileSafeSlugBuilder.Build(expectedArtifact.Title);
        return string.Equals(expectedSlug, displaySlug, StringComparison.Ordinal) ||
               string.Equals(expectedSlug, fileSlug, StringComparison.Ordinal) ||
               relativePath.Contains(expectedSlug, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryExtractExpectedArtifactRelativePath(string validationRequirementSummary, out string relativePath)
    {
        foreach (var marker in new[]
                 {
                     "Create this artifact at ",
                     "must exist at ",
                     "must be written at "
                 })
        {
            var markerIndex = validationRequirementSummary.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                continue;
            }

            var startIndex = markerIndex + marker.Length;
            var remainder = validationRequirementSummary[startIndex..].TrimStart();
            if (string.IsNullOrWhiteSpace(remainder))
            {
                continue;
            }

            var endIndex = remainder.IndexOfAny([' ', '\r', '\n', '\t']);
            var token = endIndex >= 0
                ? remainder[..endIndex]
                : remainder;
            token = token.Trim().TrimEnd('.', ',', ';', ':').Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            relativePath = token;
            return true;
        }

        relativePath = string.Empty;
        return false;
    }

    private static string NormalizeManagedRelativePathForComparison(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var segments = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length >= 5 &&
            IsManagedRootSegment(segments[0]) &&
            string.Equals(segments[1], "scopes", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join('/', [segments[0], .. segments.Skip(4)]);
        }

        return normalized;
    }

    private static string ResolveScopedManagedRelativePath(WorkspaceScopeDescriptor workspaceScope, string relativePath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
        if (workspaceScope.IsDefaultSandbox || string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return TryResolveScopedManagedRelativePath(normalized, "artifacts", workspaceScope.ArtifactRootRelativePath)
            ?? TryResolveScopedManagedRelativePath(normalized, "output", workspaceScope.OutputRootRelativePath)
            ?? TryResolveScopedManagedRelativePath(normalized, "integration-map", workspaceScope.IntegrationMapRootRelativePath)
            ?? TryResolveScopedManagedRelativePath(normalized, "data", workspaceScope.DataRootRelativePath)
            ?? normalized;
    }

    private static string? TryResolveScopedManagedRelativePath(string relativePath, string rootName, string scopedRootRelativePath)
    {
        if (!IsManagedRootMatch(relativePath, rootName))
        {
            return null;
        }

        if (IsManagedRootMatch(relativePath, scopedRootRelativePath))
        {
            return relativePath;
        }

        var foreignScopedPrefix = $"{rootName}/scopes/";
        if (relativePath.StartsWith(foreignScopedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return relativePath;
        }

        var suffix = RemoveManagedRoot(relativePath, rootName);
        return string.IsNullOrWhiteSpace(suffix)
            ? scopedRootRelativePath
            : WorkspaceScopeDescriptor.NormalizeRelativePath(Path.Combine(scopedRootRelativePath, suffix));
    }

    private static bool IsManagedRootMatch(string relativePath, string rootRelativePath)
    {
        return string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase) ||
               relativePath.StartsWith(rootRelativePath + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveManagedRoot(string relativePath, string rootRelativePath)
    {
        if (string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return relativePath[(rootRelativePath.Length + 1)..];
    }

    private static bool IsManagedRootSegment(string segment)
    {
        return string.Equals(segment, "artifacts", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "output", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "integration-map", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "data", StringComparison.OrdinalIgnoreCase);
    }

    private static ProcessArtifactKind ResolveExpectedArtifactKind(ExecutionArtifactRecord artifact)
    {
        if (artifact.RelativePath.EndsWith("/response.md", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactKind.Transcript;
        }

        var fileName = Path.GetFileName(artifact.RelativePath.Replace('\\', '/'));
        var extension = Path.GetExtension(fileName);
        if (artifact.ContentType.Contains("image", StringComparison.OrdinalIgnoreCase) || IsImageExtension(extension))
        {
            return ProcessArtifactKind.Evidence;
        }

        if (ContainsArtifactHint(fileName, "checklist"))
        {
            return ProcessArtifactKind.Checklist;
        }

        if (ContainsArtifactHint(fileName, "log") ||
            ContainsArtifactHint(fileName, "transcript") ||
            ContainsArtifactHint(fileName, "stdout") ||
            ContainsArtifactHint(fileName, "stderr"))
        {
            return ProcessArtifactKind.Transcript;
        }

        return string.Equals(artifact.ArtifactKind, "generated-output", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               IsCodeOrProjectExtension(extension)
            ? ProcessArtifactKind.Deliverable
            : ProcessArtifactKind.Evidence;
    }

    private static bool ContainsArtifactHint(string fileName, string hint)
    {
        return fileName.Contains(hint, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCodeOrProjectExtension(string extension)
    {
        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".razor", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".css", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImageExtension(string extension)
    {
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".svg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTransientExecutionArtifact(ExecutionArtifactRecord artifact)
    {
        var relativePath = artifact.RelativePath.Replace('\\', '/');
        return relativePath.StartsWith(".playwright-mcp/", StringComparison.OrdinalIgnoreCase) ||
               relativePath.Contains("/.playwright-mcp/", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<IReadOnlyList<DispatchArtifactExpectation>> LoadExpectedArtifactsAsync(
        AppDbContext dbContext,
        Guid stepDefinitionId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<ProcessArtifactExpectation>()
            .AsNoTracking()
            .Where(item => item.StepDefinitionId == stepDefinitionId)
            .OrderBy(item => item.Title)
            .Select(item => new DispatchArtifactExpectation(
                item.Id,
                item.ArtifactKind,
                item.Title,
                item.IsRequired,
                item.TrustRequirement,
                item.SensitivityLevel,
                item.ValidationRequirementSummary))
            .ToListAsync(cancellationToken);
    }

    private sealed record DispatchCandidate(
        ProcessRun Run,
        ProcessDefinition Definition,
        ProcessStepRun StepRun,
        ProcessWorkBrief? WorkBrief,
        Guid TechnicalAgentId,
        IReadOnlyList<DispatchArtifactExpectation> ExpectedArtifacts,
        IReadOnlyList<DispatchArtifactInput> ArtifactInputs,
        HashSet<string> ExternalReferenceKeys);

    internal sealed record DispatchArtifactExpectation(
        Guid Id,
        ProcessArtifactKind ArtifactKind,
        string Title,
        bool IsRequired,
        ProcessArtifactTrustRequirement TrustRequirement,
        ProcessSensitivityLevel SensitivityLevel,
        string ValidationRequirementSummary);

    private sealed record DispatchArtifactInput(
        string SourceStepTitle,
        string ExpectedArtifactTitle,
        IReadOnlyList<DispatchArtifactReference> Artifacts);

    private sealed record DispatchArtifactReference(
        string Title,
        string ArtifactKind,
        string ManagedStoragePath,
        string ReviewSummary,
        string ProvenanceSummary);

    private sealed record SessionToolCall(string ToolName, string OutputFileName);
}
