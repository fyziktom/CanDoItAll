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
                var recoveredExecution = await ProcessRecoveredExecutionAdoptionCoordinator.AdoptAsync(
                    executionClient,
                    recoverableExecutionRunId.Value,
                    ResolveRecoveredExecutionResponseText,
                    cancellationToken);
                executionRunId = recoveredExecution.ExecutionRunId;
                detail = recoveredExecution.Detail;
                responseText = recoveredExecution.ResponseText;
                automationChatSessionId ??= recoveredExecution.ChatSessionId;
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
                    ProcessExecutionAttemptResult? failedExecution = null;
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
                            ProcessExecutionInvocationRequestBuilder.Build(
                                candidate,
                                BuildExecutionPromptCore(
                                    candidate,
                                    recoveryDirective,
                                    prefetchedProjectStructureGrounding.HasPromptSummary
                                        ? prefetchedProjectStructureGrounding.PromptSummary
                                        : null,
                                    prefetchedArtifactInspectionGrounding.HasPromptSummary
                                        ? prefetchedArtifactInspectionGrounding.PromptSummary
                                        : null),
                                trigger,
                                BuildCorrelationId(candidate.StepRun.Id),
                                processInvocationMetadataJson,
                                processInvocationPolicy),
                            cancellationToken);
                    }
                    catch (ProcessAutomationExecutionFailedException exception)
                    {
                        failedExecution = await ProcessFailedExecutionInspectionCoordinator.InspectAsync(
                            executionClient,
                            candidate,
                            exception,
                            ResolvePreferredExecutionResponseText,
                            cancellationToken);
                        automationChatSessionId ??= failedExecution.ChatSessionId;

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
                        // Normalization below handles the adopted execution snapshot.
                    }

                    var attemptResult = await ProcessExecutionAttemptResultNormalizer.NormalizeAsync(
                        executionClient,
                        candidate,
                        executionResult,
                        adoptedConcurrentExecution,
                        failedExecution,
                        ResolvePreferredExecutionResponseText,
                        cancellationToken);
                    executionRunId = attemptResult.ExecutionRunId;
                    detail = attemptResult.Detail;
                    responseText = attemptResult.ResponseText;
                    automationChatSessionId ??= attemptResult.ChatSessionId;
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

            var postAttemptFacts = ProcessExecutionPostAttemptFactsBuilder.Create(
                candidate,
                detail,
                successfulToolNamesAcrossAttempts,
                responseText,
                carriedImplementationProof,
                attemptNumber,
                maxExecutionAttempts);
            carriedImplementationProof = postAttemptFacts.CarriedImplementationProof;

            finalOutcome = new DispatchExecutionOutcome(
                detail,
                responseText,
                postAttemptFacts.CompletionStatus,
                postAttemptFacts.CompletionReason,
                postAttemptFacts.MissingRequiredTools,
                attemptNumber,
                postAttemptFacts.SelectedBranchOutcomeId);
            CleanupKeptAliveDotnetRunProcesses(candidate, detail);

            if (postAttemptFacts.CompletionStatus == ProcessStepRunStatus.Completed)
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

                var providerRecoveryDecision = ProcessProviderRecoveryDirectiveBuilder.CreateRecoveryDecision(
                    providerRepair,
                    attemptNumber,
                    executionRunId,
                    clock.GetUtcNow().AddSeconds(Math.Min(60, attemptNumber * 5)));
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
                        postAttemptFacts.MissingRequiredTools,
                        postAttemptFacts.UnresolvedCriticalToolFailures,
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
                    postAttemptFacts.MissingRequiredTools,
                    carriedImplementationProof,
                    attemptNumber,
                    maxExecutionAttempts) ||
                ShouldRetryRecoverableFailedRun(
                    candidate,
                    detail,
                    responseText,
                    postAttemptFacts.MissingRequiredTools,
                    postAttemptFacts.UnresolvedCriticalToolFailures,
                    attemptNumber,
                    maxExecutionAttempts);

            var retryReasons = ResolveIncompleteSuccessfulRunRetryReasons(
                candidate,
                detail,
                responseText,
                postAttemptFacts.MissingRequiredTools,
                carriedImplementationProof);
            var noProgressSignal = TryCreateNoProgressRetrySignal(
                candidate,
                detail,
                responseText,
                postAttemptFacts.MissingRequiredTools,
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
                        postAttemptFacts.MissingRequiredTools,
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
                postAttemptFacts.MissingRequiredTools.Count == 0
                    ? "none"
                    : string.Join(", ", postAttemptFacts.MissingRequiredTools),
                postAttemptFacts.UnresolvedCriticalToolFailures.Count == 0
                    ? "none"
                    : string.Join(
                        "; ",
                        postAttemptFacts.UnresolvedCriticalToolFailures
                            .Take(2)
                            .Select(item => $"{item.ToolName}: {item.ExitSummary}")),
                attemptNumber + 1,
                maxExecutionAttempts);

            var recoveryDecision = CreateRecoveryDecisionForRetry(
                candidate,
                detail,
                responseText,
                postAttemptFacts.MissingRequiredTools,
                postAttemptFacts.UnresolvedCriticalToolFailures,
                attemptNumber,
                nextAttemptAtUtc: clock.GetUtcNow().AddSeconds(Math.Min(60, attemptNumber * 5)));
            var reworkPacket = CreateReworkPacketForDecision(
                candidate,
                detail,
                recoveryDecision,
                postAttemptFacts.MissingRequiredTools,
                postAttemptFacts.UnresolvedCriticalToolFailures,
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
                    postAttemptFacts.MissingRequiredTools,
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
                postAttemptFacts.MissingRequiredTools,
                postAttemptFacts.UnresolvedCriticalToolFailures,
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
        return ProcessObservedExecutionOutcomeBuilder.Create(
            detail,
            responseText,
            attemptNumber);
    }

    private async Task<CarriedImplementationProof> ResolveHistoricalCarriedImplementationProofAsync(
        DispatchCandidate candidate,
        CancellationToken cancellationToken)
    {
        if (!RequiresCurrentAttemptProductMutation(candidate))
        {
            return CarriedImplementationProof.None;
        }

        var historicalDetails = await CreateExecutionAttemptLoopFacade().HistoricalCarriedProof
            .LoadHistoricalDetailsAsync(
                candidate,
                IsHistoricalCarryForwardExecutionRun,
                cancellationToken);

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

        return await CreateExecutionAttemptLoopFacade().ProviderRepair
            .TryRepairAsync(
                candidate,
                detail,
                failureSummary,
                cancellationToken);
    }

    private ProcessExecutionAttemptLoopFacade CreateExecutionAttemptLoopFacade()
    {
        return new ProcessExecutionAttemptLoopFacade(
            executionClient,
            dbContextFactory,
            technicalAgentBridge,
            logger,
            ProviderFallbackHealthProbeTimeout);
    }

}
