using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private async Task<DispatchExecutionOutcome> ExecuteUntilSettledAsync(
        DispatchCandidate candidate,
        string trigger,
        Func<CancellationToken, Task>? renewLeaseAsync,
        CancellationToken cancellationToken)
    {
        DispatchExecutionOutcome? finalOutcome = null;
        string? recoveryDirective = string.IsNullOrWhiteSpace(candidate.ManualRecoveryDirective)
            ? null
            : candidate.ManualRecoveryDirective.Trim();
        var recoverableExecutionRunId = candidate.RecoveryExecutionRunId;
        Guid? automationChatSessionId = null;
        var prefetchedProjectStructureGrounding = await TryBuildProjectStructureGroundingAsync(candidate, cancellationToken);
        var prefetchedArtifactInspectionGrounding = await TryBuildArtifactInspectionGroundingAsync(candidate, cancellationToken);
        var successfulToolNamesAcrossAttempts = new HashSet<string>(
            prefetchedProjectStructureGrounding.SatisfiedToolNames,
            StringComparer.Ordinal);
        successfulToolNamesAcrossAttempts.UnionWith(prefetchedArtifactInspectionGrounding.SatisfiedToolNames);
        var carriedImplementationProof = await ResolveHistoricalCarriedImplementationProofAsync(candidate, cancellationToken);
        var maxExecutionAttempts = ResolveMaxExecutionAttempts(candidate);
        EnsureProviderNativeBrowserOutputDirectories(candidate);

        for (var attemptNumber = 1; attemptNumber <= maxExecutionAttempts; attemptNumber++)
        {
            if (renewLeaseAsync is not null)
            {
                await renewLeaseAsync(cancellationToken);
            }

            ProcessAutomationExecutionRunDetail detail;
            Guid executionRunId;
            string responseText;

            if (attemptNumber == 1 && recoverableExecutionRunId.HasValue)
            {
                executionRunId = recoverableExecutionRunId.Value;
                detail = await executionClient.GetExecutionRunDetailAsync(executionRunId, cancellationToken);
                responseText = ResolveRecoveredExecutionResponseText(detail);
                automationChatSessionId ??= detail.Run.ChatSessionId;
                recoverableExecutionRunId = null;

                logger.LogInformation(
                    "Recovering existing AgentFramework execution run {ExecutionRunId} for stranded process step {StepRunId} on run {RunId}.",
                    executionRunId,
                    candidate.StepRun.Id,
                    candidate.Run.Id);
            }
            else
            {
                var concurrentExecution = await TryAdoptConcurrentAutomationExecutionAsync(candidate, cancellationToken);
                if (concurrentExecution is not null)
                {
                    executionRunId = concurrentExecution.ExecutionRunId;
                    detail = concurrentExecution.Detail;
                    responseText = concurrentExecution.ResponseText;
                    automationChatSessionId ??= detail.Run.ChatSessionId;

                    logger.LogInformation(
                        "Adopting concurrently-started AgentFramework execution run {ExecutionRunId} for process step {StepRunId} on run {RunId}.",
                        executionRunId,
                        candidate.StepRun.Id,
                        candidate.Run.Id);
                }
                else
                {
                    ProcessAutomationExecutionRunResult? executionResult = null;
                    ConcurrentAutomationExecution? adoptedConcurrentExecution = null;
                    ProcessAutomationExecutionRunDetail? failedExecutionDetail = null;
                    Guid? failedExecutionRunId = null;
                    string? failedResponseText = null;
                    var processInvocationPolicy = new ExecutionInvocationPolicy(
                        FinalizerMode: AgentFinalizerMode.Required,
                        MaxStructuredOutputRepairAttempts: ExecutionInvocationMetadata.DefaultGovernedRepairAttempts,
                        RequireStructuredOutputValidation: true);
                    var processInvocationMetadataJson = BuildProcessInvocationMetadataJson(
                        candidate,
                        processInvocationPolicy,
                        prefetchedProjectStructureGrounding.HasPromptSummary
                            ? prefetchedProjectStructureGrounding.PromptSummary
                            : null,
                        prefetchedArtifactInspectionGrounding.HasPromptSummary
                            ? prefetchedArtifactInspectionGrounding.PromptSummary
                            : null);

                    try
                    {
                        executionResult = await executionClient.ExecuteRunAsync(
                            new ProcessAutomationExecutionRequest(
                                candidate.TechnicalAgentId,
                                BuildExecutionPromptCore(
                                    candidate,
                                    recoveryDirective,
                                    prefetchedProjectStructureGrounding.HasPromptSummary
                                        ? prefetchedProjectStructureGrounding.PromptSummary
                                        : null,
                                    prefetchedArtifactInspectionGrounding.HasPromptSummary
                                        ? prefetchedArtifactInspectionGrounding.PromptSummary
                                        : null),
                                new ProcessAutomationInvocationSource(
                                    SourceKind: "process-step",
                                    SourceId: candidate.StepRun.Id.ToString("D"),
                                    CorrelationId: BuildCorrelationId(candidate.StepRun.Id),
                                    CausationId: string.IsNullOrWhiteSpace(trigger)
                                        ? string.Empty
                                        : trigger.Trim(),
                                    RequestedBy: AutomationActor,
                                    RequestedByKind: "system",
                                    MetadataJson: processInvocationMetadataJson,
                                    ProcessRunId: candidate.Run.Id.ToString("D"),
                                    ProcessStepId: candidate.StepRun.Id.ToString("D")),
                                new ProcessAutomationInvocationPolicy(
                                    ProcessAutomationFinalizerMode.Required,
                                    processInvocationPolicy.MaxStructuredOutputRepairAttempts,
                                    processInvocationPolicy.RequireStructuredOutputValidation),
                                AutoApprovePendingToolCalls: true,
                                StructuredOutputKind: ProcessAutomationStructuredOutputKind.ProcessStepOutcomeResult),
                            cancellationToken);
                    }
                    catch (ProcessAutomationExecutionFailedException exception)
                    {
                        failedExecutionRunId = exception.ExecutionRunId;
                        automationChatSessionId ??= exception.ChatSessionId;
                        failedExecutionDetail = await executionClient.GetExecutionRunDetailAsync(
                            exception.ExecutionRunId,
                            cancellationToken);
                        failedResponseText = ResolvePreferredExecutionResponseText(
                            candidate,
                            exception.Message,
                            failedExecutionDetail);

                        logger.LogWarning(
                            exception,
                            "Continuing recovery inspection for failed AgentFramework execution run {ExecutionRunId} on process step {StepRunId} and run {RunId}.",
                            exception.ExecutionRunId,
                            candidate.StepRun.Id,
                            candidate.Run.Id);
                    }
                    catch (InvalidOperationException exception)
                    {
                        if (!IsConcurrentAutomationSessionBusyException(exception))
                        {
                            throw;
                        }

                        adoptedConcurrentExecution = await TryAdoptConcurrentAutomationExecutionAsync(candidate, cancellationToken);
                        if (adoptedConcurrentExecution is null)
                        {
                            throw;
                        }

                        logger.LogInformation(
                            "Adopting concurrently-started AgentFramework execution run {ExecutionRunId} for process step {StepRunId} on run {RunId} after chat-session start collision. Message: {Message}",
                            adoptedConcurrentExecution.ExecutionRunId,
                            candidate.StepRun.Id,
                            candidate.Run.Id,
                            exception.Message);
                    }

                    if (adoptedConcurrentExecution is not null)
                    {
                        executionRunId = adoptedConcurrentExecution.ExecutionRunId;
                        detail = adoptedConcurrentExecution.Detail;
                        responseText = adoptedConcurrentExecution.ResponseText;
                        automationChatSessionId ??= detail.Run.ChatSessionId;
                    }
                    else if (failedExecutionDetail is not null && failedExecutionRunId.HasValue)
                    {
                        executionRunId = failedExecutionRunId.Value;
                        detail = failedExecutionDetail;
                        responseText = failedResponseText ?? ResolveRecoveredExecutionResponseText(detail);
                    }
                    else
                    {
                        if (executionResult is null)
                        {
                            throw new InvalidOperationException(
                                $"AgentFramework execution start did not return a result for process step '{candidate.StepRun.Id:D}'.");
                        }

                        executionRunId = executionResult.ExecutionRunId;
                        automationChatSessionId ??= executionResult.ChatSessionId;
                        detail = await executionClient.GetExecutionRunDetailAsync(executionRunId, cancellationToken);
                        responseText = ResolvePreferredExecutionResponseText(candidate, executionResult.ResponseText, detail);
                    }
                }
            }

            if (!IsTerminalAutomationExecutionRun(detail.Run))
            {
                logger.LogInformation(
                    "Observed active AgentFramework execution run {ExecutionRunId} for process run {RunId}, step {StepRunId} in state {ProcessAutomationExecutionState}; leaving the process step InProgress without finalization.",
                    executionRunId,
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    detail.Run.State);

                return CreateObservedActiveAutomationExecutionOutcome(
                    detail,
                    responseText,
                    attemptNumber);
            }

            if (RequiresGovernedStepOutcome(candidate.StepRun) &&
                !TryReadProcessStepOutcome(responseText, out _, out var outputValidation))
            {
                logger.LogWarning(
                    "AgentFramework run {ExecutionRunId} returned invalid structured process outcome for process run {RunId}, step {StepRunId}. Raw output hash: {RawOutputHash}. Validation errors: {ValidationErrors}",
                    executionRunId,
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    AgentOutputJson.ComputeRawOutputHash(responseText),
                    string.Join(
                        "; ",
                        outputValidation.Errors.Select(error => $"{error.Code}: {error.Message}")));
            }

            successfulToolNamesAcrossAttempts.UnionWith(ResolveSuccessfulToolNames(detail));
            carriedImplementationProof = ResolveCarriedImplementationProof(candidate, detail, carriedImplementationProof);
            if (renewLeaseAsync is not null)
            {
                await renewLeaseAsync(cancellationToken);
            }

            var missingRequiredTools = ResolveMissingRequiredToolExecutionsWithCarriedImplementationProof(
                candidate,
                detail,
                successfulToolNamesAcrossAttempts,
                carriedImplementationProof);
            var unresolvedCriticalToolFailures = ResolveUnresolvedCriticalToolFailures(candidate, detail);
            var completionStatus = ResolveCompletionStatusWithCarryForward(
                candidate,
                detail,
                successfulToolNamesAcrossAttempts,
                responseText,
                carriedImplementationProof);
            var completionReason = BuildCompletionReasonWithCarryForward(
                candidate,
                detail,
                candidate.StepRun.Title,
                successfulToolNamesAcrossAttempts,
                responseText,
                carriedImplementationProof);
            var selectedBranchOutcomeId = ResolveSelectedBranchOutcomeId(
                candidate,
                completionStatus,
                responseText);

            if (attemptNumber > 1)
            {
                completionReason = completionStatus == ProcessStepRunStatus.Completed
                    ? $"{completionReason} Recovered on attempt {attemptNumber} of {maxExecutionAttempts}."
                    : $"{completionReason} Recovery attempt {attemptNumber} of {maxExecutionAttempts}.";
            }

            finalOutcome = new DispatchExecutionOutcome(
                detail,
                responseText,
                completionStatus,
                completionReason,
                missingRequiredTools,
                attemptNumber,
                selectedBranchOutcomeId);
            CleanupKeptAliveDotnetRunProcesses(candidate, detail);

            if (completionStatus == ProcessStepRunStatus.Completed)
            {
                return finalOutcome;
            }

            var providerRepair = await TryRepairAssignedAgentProvidersAsync(
                candidate,
                detail,
                responseText,
                attemptNumber,
                maxExecutionAttempts,
                cancellationToken);
            if (providerRepair is not null)
            {
                logger.LogWarning(
                    "Recovered provider failure for process run {RunId}, step {StepRunId} by switching {AffectedAgentCount} assigned internal agent(s) from '{FailedProviderName}' to '{FallbackProviderName}' ({FallbackModel}). Failure summary: {FailureSummary}",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    providerRepair.AffectedAgentCount,
                    providerRepair.FailedProviderName,
                    providerRepair.FallbackProviderName,
                    providerRepair.FallbackModel,
                    providerRepair.FailureSummary);

                var providerRecoveryDecision = AgentRecoveryDecisionFactory.Create(
                    AgentFailureCategory.ProviderFailure,
                    providerRepair.FailureSummary,
                    attemptNumber,
                    executionRunId.ToString("D"),
                    nextAttemptAtUtc: clock.GetUtcNow().AddSeconds(Math.Min(60, attemptNumber * 5)));
                await PersistRecoveryJournalAsync(
                    candidate,
                    providerRecoveryDecision,
                    packet: null,
                    providerFallbackCount: 1,
                    cancellationToken);

                automationChatSessionId = null;
                var providerRecoveryDirective = BuildProviderRepairRecoveryDirective(
                    BuildRecoveryDirective(
                        candidate,
                        detail,
                        responseText,
                        missingRequiredTools,
                        unresolvedCriticalToolFailures,
                        attemptNumber),
                    providerRepair);
                recoveryDirective = BuildTypedRecoveryDirective(
                    providerRecoveryDecision,
                    packet: null,
                    providerRecoveryDirective);
                continue;
            }

            var shouldRetry =
                ShouldRetryIncompleteSuccessfulRun(
                    candidate,
                    detail,
                    responseText,
                    missingRequiredTools,
                    carriedImplementationProof,
                    attemptNumber,
                    maxExecutionAttempts) ||
                ShouldRetryRecoverableFailedRun(
                    candidate,
                    detail,
                    responseText,
                    missingRequiredTools,
                    unresolvedCriticalToolFailures,
                    attemptNumber,
                    maxExecutionAttempts);

            var retryReasons = ResolveIncompleteSuccessfulRunRetryReasons(
                candidate,
                detail,
                responseText,
                missingRequiredTools,
                carriedImplementationProof);
            var noProgressSignal = TryCreateNoProgressRetrySignal(
                candidate,
                detail,
                responseText,
                missingRequiredTools,
                retryReasons);

            if (shouldRetry &&
                noProgressSignal is not null &&
                await HasPriorNoProgressRetrySignalAsync(candidate, noProgressSignal, cancellationToken))
            {
                shouldRetry = false;
            }

            if (!shouldRetry)
            {
                if (noProgressSignal is not null)
                {
                    await PersistNoProgressRetryCompressedDiagnosticAsync(
                        candidate,
                        detail,
                        missingRequiredTools,
                        retryReasons,
                        noProgressSignal,
                        cancellationToken);
                }

                return finalOutcome;
            }

            logger.LogWarning(
                "AgentFramework run {ExecutionRunId} ended with unresolved execution work for process run {RunId}, step {StepRunId}. Retry reasons: {RetryReasons}. Missing tools: {MissingTools}. Critical failures: {CriticalFailures}. Retrying attempt {NextAttempt}/{MaxAttempts}.",
                executionRunId,
                candidate.Run.Id,
                candidate.StepRun.Id,
                retryReasons.Count == 0
                    ? "unspecified recoverable failure"
                    : string.Join(" | ", retryReasons),
                missingRequiredTools.Count == 0
                    ? "none"
                    : string.Join(", ", missingRequiredTools),
                unresolvedCriticalToolFailures.Count == 0
                    ? "none"
                    : string.Join(
                        "; ",
                        unresolvedCriticalToolFailures
                            .Take(2)
                            .Select(item => $"{item.ToolName}: {item.ExitSummary}")),
                attemptNumber + 1,
                maxExecutionAttempts);

            var recoveryDecision = CreateRecoveryDecisionForRetry(
                candidate,
                detail,
                responseText,
                missingRequiredTools,
                unresolvedCriticalToolFailures,
                attemptNumber,
                nextAttemptAtUtc: clock.GetUtcNow().AddSeconds(Math.Min(60, attemptNumber * 5)));
            var reworkPacket = CreateReworkPacketForDecision(
                candidate,
                detail,
                recoveryDecision,
                missingRequiredTools,
                unresolvedCriticalToolFailures,
                clock.GetUtcNow());
            if (reworkPacket is not null)
            {
                recoveryDecision = recoveryDecision with
                {
                    Mode = AgentRecoveryMode.ReworkContinuation,
                    ReworkPacketId = reworkPacket.Id
                };
            }

            if (noProgressSignal is not null)
            {
                await PersistNoProgressRetryObservedAsync(
                    candidate,
                    detail,
                    missingRequiredTools,
                    retryReasons,
                    noProgressSignal,
                    cancellationToken);
            }

            await PersistRecoveryJournalAsync(
                candidate,
                recoveryDecision,
                reworkPacket,
                providerFallbackCount: 0,
                cancellationToken);

            // Start recovery attempts on a fresh run so stale provider-side state
            // from the previous attempt does not poison the next governed retry.
            automationChatSessionId = null;

            var legacyRecoveryDirective = BuildRecoveryDirective(
                candidate,
                detail,
                responseText,
                missingRequiredTools,
                unresolvedCriticalToolFailures,
                attemptNumber);
            recoveryDirective = BuildTypedRecoveryDirective(
                recoveryDecision,
                reworkPacket,
                legacyRecoveryDirective);
        }

        return finalOutcome
               ?? throw new InvalidOperationException($"No AgentFramework execution outcome was captured for process step '{candidate.StepRun.Id:D}'.");
    }

    private static DispatchExecutionOutcome CreateObservedActiveAutomationExecutionOutcome(
        ProcessAutomationExecutionRunDetail detail,
        string responseText,
        int attemptNumber)
    {
        return new DispatchExecutionOutcome(
            detail,
            responseText,
            ProcessStepRunStatus.InProgress,
            $"AgentFramework run '{detail.Run.Title}' is still {detail.Run.State}; automation will observe it again after it becomes terminal or stale.",
            [],
            attemptNumber,
            null);
    }

    private async Task<CarriedImplementationProof> ResolveHistoricalCarriedImplementationProofAsync(
        DispatchCandidate candidate,
        CancellationToken cancellationToken)
    {
        if (!RequiresCurrentAttemptProductMutation(candidate))
        {
            return CarriedImplementationProof.None;
        }

        var executionRuns = await executionClient.ListExecutionRunsAsync(
            new ProcessAutomationExecutionRunQuery(
                ProcessRunId: candidate.Run.Id.ToString("D"),
                ProcessStepId: candidate.StepRun.Id.ToString("D"),
                Take: 20),
            cancellationToken);
        var historicalDetails = new List<ProcessAutomationExecutionRunDetail>();
        foreach (var executionRun in executionRuns
                     .Where(IsHistoricalCarryForwardExecutionRun)
                     .OrderByDescending(executionRun => executionRun.CompletedAtUtc ?? executionRun.UpdatedAtUtc)
                     .ThenByDescending(executionRun => executionRun.UpdatedAtUtc)
                     .ThenByDescending(executionRun => executionRun.CreatedAtUtc))
        {
            historicalDetails.Add(await executionClient.GetExecutionRunDetailAsync(executionRun.Id, cancellationToken));
        }

        return ResolveHistoricalCarriedImplementationProof(candidate, historicalDetails);
    }

    private static int ResolveMaxExecutionAttempts(DispatchCandidate candidate)
    {
        return RequiresConcreteImplementationProof(candidate)
            ? ConcreteImplementationMaxExecutionAttempts
            : DefaultMaxExecutionAttempts;
    }

    private async Task<ProviderRepairOutcome?> TryRepairAssignedAgentProvidersAsync(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string responseText,
        int attemptNumber,
        int maxExecutionAttempts,
        CancellationToken cancellationToken)
    {
        if (attemptNumber >= maxExecutionAttempts ||
            !TryResolveRecoverableProviderFailure(detail, responseText, out var failureSummary))
        {
            return null;
        }

        var agents = await executionClient.ListAgentsAsync(includeTemplates: false, cancellationToken);
        var agentsById = agents.ToDictionary(item => item.Id);
        if (!agentsById.TryGetValue(candidate.TechnicalAgentId, out var currentAgent) ||
            !currentAgent.ProviderProfileId.HasValue)
        {
            return null;
        }

        var failedProviderId = currentAgent.ProviderProfileId.Value;
        var providers = await executionClient.ListProvidersAsync(cancellationToken);
        var failedProviderName = providers.FirstOrDefault(item => item.Id == failedProviderId)?.Name;
        var fallbackResolution = await ResolveHealthyFallbackProviderAsync(
            providers,
            failedProviderId,
            cancellationToken);
        if (fallbackResolution is null)
        {
            logger.LogWarning(
                "Process run {RunId}, step {StepRunId} detected a recoverable provider failure, but no healthy fallback provider was available for technical agent {TechnicalAgentId}. Failure summary: {FailureSummary}",
                candidate.Run.Id,
                candidate.StepRun.Id,
                candidate.TechnicalAgentId,
                failureSummary);
            return null;
        }

        var assignedPartyIds = await LoadAssignedPartyIdsAsync(
            candidate.Run.Id,
            candidate.StepRun.CurrentExecutorPartyId,
            cancellationToken);
        var assignedSummaries = assignedPartyIds.Count == 0
            ? new Dictionary<Guid, AiTechnicalAgentDirectorySummary>()
            : await technicalAgentBridge.GetDirectorySummariesAsync(assignedPartyIds, cancellationToken);
        var technicalAgentIdsToRepair = assignedSummaries.Values
            .Where(summary => summary.TechnicalAgentId.HasValue)
            .Select(summary => summary.TechnicalAgentId!.Value)
            .Distinct()
            .Where(agentId =>
                agentsById.TryGetValue(agentId, out var assignedAgent) &&
                assignedAgent.ProviderProfileId == failedProviderId)
            .ToHashSet();
        technicalAgentIdsToRepair.Add(candidate.TechnicalAgentId);

        var affectedAgentCount = 0;
        foreach (var technicalAgentId in technicalAgentIdsToRepair)
        {
            try
            {
                var editor = await executionClient.GetAgentEditorAsync(technicalAgentId, cancellationToken);
                var resolvedEditorModel = NormalizeFallbackEditorModel(fallbackResolution);
                if (editor.ProviderProfileId == fallbackResolution.Provider.Id &&
                    string.Equals(editor.Model, resolvedEditorModel, StringComparison.Ordinal))
                {
                    affectedAgentCount++;
                    continue;
                }

                editor.ProviderProfileId = fallbackResolution.Provider.Id;
                editor.Model = resolvedEditorModel;
                await executionClient.SaveAgentAsync(editor, cancellationToken);
                affectedAgentCount++;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Failed to switch technical agent {TechnicalAgentId} to fallback provider '{ProviderName}' while recovering process run {RunId}, step {StepRunId}.",
                    technicalAgentId,
                    fallbackResolution.Provider.Name,
                    candidate.Run.Id,
                    candidate.StepRun.Id);

                if (technicalAgentId == candidate.TechnicalAgentId)
                {
                    return null;
                }
            }
        }

        if (affectedAgentCount == 0)
        {
            return null;
        }

        return new ProviderRepairOutcome(
            failedProviderName ?? detail.Run.ProviderName,
            fallbackResolution.Provider.Name,
            fallbackResolution.Model,
            affectedAgentCount,
            failureSummary);
    }

    private async Task<ProviderFallbackResolution?> ResolveHealthyFallbackProviderAsync(
        IReadOnlyList<ProviderProfile> providers,
        Guid failedProviderId,
        CancellationToken cancellationToken)
    {
        foreach (var provider in OrderFallbackProviders(providers, failedProviderId))
        {
            ProviderHealthResult healthResult;
            try
            {
                using var probeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                probeCancellation.CancelAfter(ProviderFallbackHealthProbeTimeout);
                healthResult = await executionClient.TestProviderAsync(provider.Id, probeCancellation.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation(
                    "Skipping fallback provider '{ProviderName}' because its health probe exceeded {TimeoutSeconds} seconds.",
                    provider.Name,
                    ProviderFallbackHealthProbeTimeout.TotalSeconds);
                continue;
            }
            catch (Exception exception)
            {
                logger.LogInformation(
                    exception,
                    "Fallback provider probe for '{ProviderName}' failed while evaluating process execution recovery.",
                    provider.Name);
                continue;
            }

            if (!healthResult.Success)
            {
                logger.LogInformation(
                    "Skipping fallback provider '{ProviderName}' because its health probe failed: {Summary}",
                    provider.Name,
                    healthResult.Summary);
                continue;
            }

            return new ProviderFallbackResolution(
                provider,
                ResolveFallbackProviderModel(provider, healthResult),
                healthResult.Summary);
        }

        return null;
    }

    private async Task<IReadOnlyList<Guid>> LoadAssignedPartyIdsAsync(
        Guid processRunId,
        Guid? currentExecutorPartyId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var partyIds = await dbContext.Set<ProcessRunAssignment>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == processRunId && item.PartyId.HasValue && !item.IsCapabilityGap)
            .Select(item => item.PartyId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (currentExecutorPartyId.HasValue && !partyIds.Contains(currentExecutorPartyId.Value))
        {
            partyIds.Add(currentExecutorPartyId.Value);
        }

        return partyIds;
    }

    private static string NormalizeFallbackEditorModel(ProviderFallbackResolution fallbackResolution)
    {
        if (!string.IsNullOrWhiteSpace(fallbackResolution.Provider.DefaultModel) &&
            string.Equals(
                fallbackResolution.Model,
                fallbackResolution.Provider.DefaultModel,
                StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return fallbackResolution.Model;
    }

}
