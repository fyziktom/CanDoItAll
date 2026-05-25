using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    internal enum ProcessStepCompletionExecutorKind
    {
        DirectAgent,
        WorkflowBackedRole,
        ManagerArtifactRecovery
    }

    internal enum ProcessArtifactExpectationMode
    {
        Narrative,
        Decision,
        Evidence,
        Deliverable,
        RuntimeProof,
        RecoveryDiagnostic
    }

    internal enum ProcessArtifactValidationStatus
    {
        Satisfied,
        Missing,
        InvalidFormat,
        InsufficientEvidence,
        StaleOrWrongRun,
        WrongProducerMode,
        PlaceholderOnly
    }

    internal enum ProcessArtifactProducerKind
    {
        Unknown,
        AgentExecutionArtifact,
        WorkspaceWrite,
        ExistingManagedFile,
        AssistantResponse,
        CompletedDecision,
        ProcessMock,
        ProviderNativeBrowser,
        WorkflowRun,
        WorkflowArtifact,
        ManagerRecovery,
        Manual
    }

    internal sealed record ProcessArtifactExpectationValidationResult(
        Guid ExpectationId,
        string ExpectationTitle,
        ProcessArtifactExpectationMode Mode,
        ProcessArtifactValidationStatus Status,
        ProcessArtifactProducerKind ProducerKind,
        Guid? ArtifactRecordId,
        string AttemptedPath,
        string Diagnostic,
        string SuggestedAction,
        string Fingerprint)
    {
        public bool IsSatisfied => Status == ProcessArtifactValidationStatus.Satisfied;
    }

    private sealed record ProcessStepCompletionFinalizerContext(
        ProcessStepCompletionExecutorKind ExecutorKind,
        DispatchCandidate Candidate,
        ProcessStepRunStatus CompletionStatus,
        string CompletionReason,
        Guid? SelectedBranchOutcomeId,
        ExecutionRunDetail? ExecutionDetail,
        Guid? WorkflowRunId,
        string ResponseText,
        bool ProjectExecutionArtifacts,
        bool AllowManagerArtifactRecovery,
        string Trigger,
        Func<CancellationToken, Task>? RenewLeaseAsync);

    private sealed record ProcessStepCompletionFinalizerResult(
        ProcessStepRunStatus CompletionStatus,
        string CompletionReason,
        Guid? SelectedBranchOutcomeId,
        Guid StepRunConcurrencyToken,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> ArtifactValidationResults);

    private sealed record ProcessArtifactValidationDiagnosticPayload(
        Guid ProcessRunId,
        Guid StepRunId,
        Guid ExpectationId,
        string ExpectationTitle,
        ProcessArtifactExpectationMode Mode,
        ProcessArtifactValidationStatus Status,
        ProcessArtifactProducerKind ProducerKind,
        Guid? ArtifactRecordId,
        string AttemptedPath,
        string Diagnostic,
        string SuggestedAction,
        string Fingerprint,
        ProcessStepCompletionExecutorKind ExecutorKind,
        Guid? ExecutionRunId,
        Guid? WorkflowRunId,
        DateTimeOffset CreatedAtUtc);

    private async Task<ProcessStepCompletionFinalizerResult?> FinalizeStepCompletionAsync(
        ProcessStepCompletionFinalizerContext context,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var candidate = context.Candidate;
        var stepRunSnapshot = await LoadStepRunTransitionSnapshotAsync(candidate.StepRun.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Process step run {candidate.StepRun.Id} could not be reloaded before process-owned completion finalization.");

        if (context.CompletionStatus == ProcessStepRunStatus.InProgress ||
            stepRunSnapshot.Status == context.CompletionStatus)
        {
            logger.LogInformation(
                "Process-owned finalizer observed {ExecutorKind} completion for run {RunId}, step {StepRunId} as {Status}; no transition is required.",
                context.ExecutorKind,
                candidate.Run.Id,
                candidate.StepRun.Id,
                context.CompletionStatus);
            return null;
        }

        if (ShouldSkipAutomationCompletionTransition(stepRunSnapshot.Status, context.CompletionStatus))
        {
            logger.LogInformation(
                "Skipping stale process-owned finalizer transition for run {RunId}, step {StepRunId}. Current status is {CurrentStatus}, requested status is {RequestedStatus}, executor kind is {ExecutorKind}.",
                candidate.Run.Id,
                candidate.StepRun.Id,
                stepRunSnapshot.Status,
                context.CompletionStatus,
                context.ExecutorKind);
            return null;
        }

        var completionStatus = context.CompletionStatus;
        var completionReason = context.CompletionReason;
        var selectedBranchOutcomeId = context.SelectedBranchOutcomeId;

        if (context.ExecutionDetail is not null && context.ProjectExecutionArtifacts)
        {
            await ProjectExecutionArtifactsAsync(
                candidate,
                context.ExecutionDetail,
                context.ResponseText,
                completionStatus,
                dispatchClaim,
                cancellationToken);
        }

        var validationResults = await ValidateRequiredCompletionArtifactsAsync(context, cancellationToken);
        if (completionStatus == ProcessStepRunStatus.Completed)
        {
            await PersistArtifactValidationDiagnosticsAsync(context, validationResults, cancellationToken);
            var unsatisfiedResults = validationResults.Where(result => !result.IsSatisfied).ToList();
            if (unsatisfiedResults.Count > 0 &&
                context.ExecutionDetail is not null &&
                context.AllowManagerArtifactRecovery)
            {
                var recoveryOutcome = await RecoverMissingCompletionArtifactsWithManagerAsync(
                    candidate,
                    new DispatchExecutionOutcome(
                        context.ExecutionDetail,
                        context.ResponseText,
                        completionStatus,
                        completionReason,
                        [],
                        AttemptNumber: 1,
                        selectedBranchOutcomeId),
                    ResolveUnsatisfiedArtifactExpectations(candidate, unsatisfiedResults),
                    context.Trigger,
                    dispatchClaim,
                    context.RenewLeaseAsync,
                    cancellationToken);

                completionStatus = recoveryOutcome.CompletionStatus;
                completionReason = recoveryOutcome.CompletionReason;
                selectedBranchOutcomeId = recoveryOutcome.SelectedBranchOutcomeId;

                validationResults = await ValidateRequiredCompletionArtifactsAsync(
                    context with
                    {
                        CompletionStatus = completionStatus,
                        CompletionReason = completionReason,
                        SelectedBranchOutcomeId = selectedBranchOutcomeId,
                        ProjectExecutionArtifacts = false,
                        AllowManagerArtifactRecovery = false
                    },
                    cancellationToken);
                await PersistArtifactValidationDiagnosticsAsync(context, validationResults, cancellationToken);
                unsatisfiedResults = validationResults.Where(result => !result.IsSatisfied).ToList();
            }

            if (completionStatus == ProcessStepRunStatus.Completed && unsatisfiedResults.Count > 0)
            {
                completionStatus = ProcessStepRunStatus.Blocked;
                completionReason = BuildArtifactContractBlockedReason(unsatisfiedResults);
                selectedBranchOutcomeId = null;
            }
        }
        else
        {
            RefreshCandidateArtifactSatisfaction(candidate, validationResults);
        }

        return new ProcessStepCompletionFinalizerResult(
            completionStatus,
            completionReason,
            selectedBranchOutcomeId,
            stepRunSnapshot.ConcurrencyToken,
            validationResults);
    }

    private async Task ApplyFinalizedStepTransitionAsync(
        DispatchCandidate candidate,
        ProcessStepCompletionFinalizerResult finalizerResult,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var completionResult = await TransitionStepWithClaimAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = candidate.StepRun.Id,
                StepRunConcurrencyToken = finalizerResult.StepRunConcurrencyToken,
                TargetStatus = finalizerResult.CompletionStatus,
                Reason = finalizerResult.CompletionReason,
                SelectedBranchOutcomeId = finalizerResult.SelectedBranchOutcomeId,
                DecidedBy = AutomationActor,
                SuppressAutomationDispatch = finalizerResult.CompletionStatus != ProcessStepRunStatus.Completed
            },
            dispatchClaim,
            cancellationToken);

        if (completionResult.IsSuccess)
        {
            return;
        }

        var refreshedSnapshot = await LoadStepRunTransitionSnapshotAsync(candidate.StepRun.Id, cancellationToken);
        if (refreshedSnapshot is not null &&
            ShouldSkipAutomationCompletionTransition(refreshedSnapshot.Status, finalizerResult.CompletionStatus))
        {
            logger.LogInformation(
                "Skipping stale process-owned finalizer transition after a failed attempt for run {RunId}, step {StepRunId}. Current status is {CurrentStatus}, requested status is {RequestedStatus}.",
                candidate.Run.Id,
                candidate.StepRun.Id,
                refreshedSnapshot.Status,
                finalizerResult.CompletionStatus);
            return;
        }

        throw new InvalidOperationException(string.Join(" | ", completionResult.Errors.Select(error => error.Message)));
    }

    private async Task<IReadOnlyList<ProcessArtifactExpectationValidationResult>> ValidateRequiredCompletionArtifactsAsync(
        ProcessStepCompletionFinalizerContext context,
        CancellationToken cancellationToken)
    {
        var candidate = context.Candidate;
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var artifacts = await dbContext.Set<ProcessArtifactRecord>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == candidate.Run.Id && item.StepRunId == candidate.StepRun.Id)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var results = candidate.ExpectedArtifacts
            .Where(expectation => expectation.IsRequired)
            .Select(expectation => ValidateArtifactExpectationForRecordedArtifacts(
                candidate.Run.Id,
                candidate.StepRun.Id,
                expectation,
                artifacts,
                context.ExecutorKind,
                context.ExecutionDetail?.Run.Id,
                context.ExecutorKind == ProcessStepCompletionExecutorKind.WorkflowBackedRole
                    ? context.WorkflowRunId ?? ResolveWorkflowRunIdForStep(artifacts)
                    : null))
            .ToList();

        candidate.ExternalReferenceKeys.Clear();
        foreach (var externalReferenceKey in artifacts
                     .Select(item => item.ExternalReferenceKey)
                     .Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            candidate.ExternalReferenceKeys.Add(externalReferenceKey);
        }

        RefreshCandidateArtifactSatisfaction(candidate, results);
        return results;
    }

    internal static ProcessArtifactExpectationValidationResult ValidateArtifactExpectationForRecordedArtifacts(
        Guid processRunId,
        Guid stepRunId,
        DispatchArtifactExpectation expectation,
        IReadOnlyList<ProcessArtifactRecord> artifacts,
        ProcessStepCompletionExecutorKind executorKind,
        Guid? executionRunId = null,
        Guid? workflowRunId = null)
    {
        ArgumentNullException.ThrowIfNull(expectation);
        ArgumentNullException.ThrowIfNull(artifacts);

        var mode = ResolveArtifactExpectationMode(expectation);
        var candidateArtifacts = artifacts
            .Where(artifact => IsArtifactCandidateForExpectation(expectation, artifact))
            .OrderBy(artifact => ResolveArtifactCandidatePriority(expectation, artifact))
            .ThenByDescending(artifact => artifact.CreatedAtUtc)
            .ToList();

        if (candidateArtifacts.Count == 0)
        {
            return CreateArtifactValidationResult(
                processRunId,
                stepRunId,
                expectation,
                mode,
                ProcessArtifactValidationStatus.Missing,
                ProcessArtifactProducerKind.Unknown,
                null,
                string.Empty,
                "No current step artifact record matches the required expectation.",
                "Recover or block with the exact missing artifact.",
                executorKind,
                executionRunId,
                workflowRunId);
        }

        ProcessArtifactExpectationValidationResult? firstFailure = null;
        foreach (var artifact in candidateArtifacts)
        {
            var result = ValidateArtifactCandidate(
                processRunId,
                stepRunId,
                expectation,
                mode,
                artifact,
                executorKind,
                executionRunId,
                workflowRunId);
            if (result.IsSatisfied)
            {
                return result;
            }

            firstFailure ??= result;
        }

        return firstFailure!;
    }

    private static ProcessArtifactExpectationValidationResult ValidateArtifactCandidate(
        Guid processRunId,
        Guid stepRunId,
        DispatchArtifactExpectation expectation,
        ProcessArtifactExpectationMode mode,
        ProcessArtifactRecord artifact,
        ProcessStepCompletionExecutorKind executorKind,
        Guid? executionRunId,
        Guid? workflowRunId)
    {
        var producerKind = ResolveArtifactProducerKind(artifact);
        if (ContainsPlaceholderArtifactSignal(artifact))
        {
            return CreateArtifactValidationResult(
                processRunId,
                stepRunId,
                expectation,
                mode,
                ProcessArtifactValidationStatus.PlaceholderOnly,
                producerKind,
                artifact,
                artifact.ManagedStoragePath,
                "The candidate artifact is a placeholder, gap marker, or missing-artifact diagnostic.",
                "Produce a real current-run artifact or block with the evidence gap.",
                executorKind,
                executionRunId,
                workflowRunId);
        }

        if (!IsProducerAllowedForMode(mode, producerKind, expectation))
        {
            return CreateArtifactValidationResult(
                processRunId,
                stepRunId,
                expectation,
                mode,
                ProcessArtifactValidationStatus.WrongProducerMode,
                producerKind,
                artifact,
                artifact.ManagedStoragePath,
                $"Producer {producerKind} is not allowed to satisfy {mode} artifact expectations.",
                "Recover from an allowed producer or block with an exact producer-mode diagnostic.",
                executorKind,
                executionRunId,
                workflowRunId);
        }

        if (!IsCurrentRunArtifact(artifact, processRunId, stepRunId, executionRunId, workflowRunId))
        {
            return CreateArtifactValidationResult(
                processRunId,
                stepRunId,
                expectation,
                mode,
                ProcessArtifactValidationStatus.StaleOrWrongRun,
                producerKind,
                artifact,
                artifact.ManagedStoragePath,
                "The candidate artifact is not bound to the current process run, step, execution run, or workflow run.",
                "Recover using current-run evidence or block instead of carrying stale artifacts forward.",
                executorKind,
                executionRunId,
                workflowRunId);
        }

        if (RequiresManagedEvidencePath(mode, producerKind) && string.IsNullOrWhiteSpace(artifact.ManagedStoragePath))
        {
            return CreateArtifactValidationResult(
                processRunId,
                stepRunId,
                expectation,
                mode,
                ProcessArtifactValidationStatus.InsufficientEvidence,
                producerKind,
                artifact,
                artifact.ManagedStoragePath,
                "The candidate artifact has no managed storage path for a file-backed expectation.",
                "Write or recover a durable managed artifact with current-run provenance.",
                executorKind,
                executionRunId,
                workflowRunId);
        }

        if (!MatchesDeclaredFormat(expectation, artifact, out var formatDiagnostic))
        {
            return CreateArtifactValidationResult(
                processRunId,
                stepRunId,
                expectation,
                mode,
                ProcessArtifactValidationStatus.InvalidFormat,
                producerKind,
                artifact,
                artifact.ManagedStoragePath,
                formatDiagnostic,
                "Regenerate the artifact in the declared format or block with the format mismatch.",
                executorKind,
                executionRunId,
                workflowRunId);
        }

        return CreateArtifactValidationResult(
            processRunId,
            stepRunId,
            expectation,
            mode,
            ProcessArtifactValidationStatus.Satisfied,
            producerKind,
            artifact,
            artifact.ManagedStoragePath,
            "Required artifact expectation is satisfied by a current-run, mode-compatible artifact.",
            "Complete",
            executorKind,
            executionRunId,
            workflowRunId);
    }

    private async Task PersistArtifactValidationDiagnosticsAsync(
        ProcessStepCompletionFinalizerContext context,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> validationResults,
        CancellationToken cancellationToken)
    {
        var failures = validationResults.Where(result => !result.IsSatisfied).ToList();
        if (failures.Count == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingFingerprints = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .Where(item =>
                item.ProcessRunId == context.Candidate.Run.Id &&
                item.StepRunId == context.Candidate.StepRun.Id &&
                item.EventType == ProcessRuntimeEventTypes.ArtifactValidationDiagnostic)
            .Select(item => item.CorrelationId)
            .ToListAsync(cancellationToken);
        var existingFingerprintSet = existingFingerprints.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = clock.GetUtcNow();

        foreach (var failure in failures)
        {
            if (existingFingerprintSet.Contains(failure.Fingerprint))
            {
                continue;
            }

            var payload = new ProcessArtifactValidationDiagnosticPayload(
                context.Candidate.Run.Id,
                context.Candidate.StepRun.Id,
                failure.ExpectationId,
                failure.ExpectationTitle,
                failure.Mode,
                failure.Status,
                failure.ProducerKind,
                failure.ArtifactRecordId,
                failure.AttemptedPath,
                failure.Diagnostic,
                failure.SuggestedAction,
                failure.Fingerprint,
                context.ExecutorKind,
                context.ExecutionDetail?.Run.Id,
                context.WorkflowRunId,
                now);

            await dbContext.Set<ProcessJournalEntry>().AddAsync(
                new ProcessJournalEntry
                {
                    ProcessRunId = context.Candidate.Run.Id,
                    StepRunId = context.Candidate.StepRun.Id,
                    EventType = ProcessRuntimeEventTypes.ArtifactValidationDiagnostic,
                    Title = $"Artifact validation failed: {failure.ExpectationTitle}",
                    Description = $"{failure.Status}: {failure.Diagnostic}",
                    CorrelationId = failure.Fingerprint,
                    OperatingMode = context.Candidate.Run.OperatingMode,
                    PolicyVersion = $"definition-version:{context.Candidate.Run.ProcessDefinitionVersionId:D}",
                    EnvironmentMode = context.Candidate.Run.OperatingMode.ToString(),
                    ReplayContextJson = JsonSerializer.Serialize(payload, AgentOutputJson.SerializerOptions),
                    OccurredAtUtc = now
                },
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void RefreshCandidateArtifactSatisfaction(
        DispatchCandidate candidate,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> validationResults)
    {
        candidate.RecordedArtifactExpectationIds.Clear();
        foreach (var result in validationResults.Where(result => result.IsSatisfied))
        {
            candidate.RecordedArtifactExpectationIds.Add(result.ExpectationId);
        }
    }

    private static IReadOnlyList<DispatchArtifactExpectation> ResolveUnsatisfiedArtifactExpectations(
        DispatchCandidate candidate,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> unsatisfiedResults)
    {
        var unsatisfiedIds = unsatisfiedResults.Select(result => result.ExpectationId).ToHashSet();
        return candidate.ExpectedArtifacts
            .Where(expectation => unsatisfiedIds.Contains(expectation.Id))
            .ToList();
    }

    private static string BuildArtifactContractBlockedReason(
        IReadOnlyList<ProcessArtifactExpectationValidationResult> unsatisfiedResults)
    {
        var summary = string.Join(
            "; ",
            unsatisfiedResults
                .Take(5)
                .Select(result => $"{result.ExpectationTitle}: {result.Status} ({result.Diagnostic})"));
        return $"Required artifact contract validation failed: {summary}. The process step is blocked instead of completing with missing, malformed, stale, placeholder, or weakly produced artifacts.";
    }

    private static ProcessArtifactExpectationMode ResolveArtifactExpectationMode(DispatchArtifactExpectation expectation)
    {
        var contractText = CollapsePromptWhitespace(string.Join(
            ' ',
            expectation.Title,
            expectation.ValidationRequirementSummary,
            expectation.AllowedFutureUsageSummary)).ToLowerInvariant();

        if (contractText.Contains("runtime proof", StringComparison.Ordinal) ||
            contractText.Contains("browser proof", StringComparison.Ordinal) ||
            contractText.Contains("test output", StringComparison.Ordinal) ||
            contractText.Contains("build output", StringComparison.Ordinal) ||
            contractText.Contains("command output", StringComparison.Ordinal) ||
            contractText.Contains("screenshot", StringComparison.Ordinal) ||
            contractText.Contains("log", StringComparison.Ordinal))
        {
            return ProcessArtifactExpectationMode.RuntimeProof;
        }

        return expectation.ArtifactKind switch
        {
            ProcessArtifactKind.Decision => ProcessArtifactExpectationMode.Decision,
            ProcessArtifactKind.Deliverable => ProcessArtifactExpectationMode.Deliverable,
            ProcessArtifactKind.Evidence or ProcessArtifactKind.Transcript or ProcessArtifactKind.Dataset => ProcessArtifactExpectationMode.Evidence,
            _ => ProcessArtifactExpectationMode.Narrative
        };
    }

    private static bool IsArtifactCandidateForExpectation(
        DispatchArtifactExpectation expectation,
        ProcessArtifactRecord artifact)
    {
        if (artifact.ArtifactExpectationId == expectation.Id)
        {
            return true;
        }

        if (artifact.ArtifactKind != expectation.ArtifactKind)
        {
            return false;
        }

        if (string.Equals(artifact.Title, expectation.Title, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var expectedSlug = FileSafeSlugBuilder.Build(expectation.Title);
        if (string.IsNullOrWhiteSpace(expectedSlug))
        {
            return false;
        }

        return string.Equals(FileSafeSlugBuilder.Build(artifact.Title), expectedSlug, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(FileSafeSlugBuilder.Build(Path.GetFileNameWithoutExtension(artifact.ManagedStoragePath)), expectedSlug, StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveArtifactCandidatePriority(
        DispatchArtifactExpectation expectation,
        ProcessArtifactRecord artifact)
    {
        if (artifact.ArtifactExpectationId == expectation.Id)
        {
            return 0;
        }

        if (string.Equals(artifact.Title, expectation.Title, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static ProcessArtifactProducerKind ResolveArtifactProducerKind(ProcessArtifactRecord artifact)
    {
        var key = artifact.ExternalReferenceKey;
        if (key.StartsWith("agentframework-artifact:", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.AgentExecutionArtifact;
        }

        if (key.StartsWith("workspace-written-artifact|", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.WorkspaceWrite;
        }

        if (key.StartsWith("existing-managed-artifact|", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.ExistingManagedFile;
        }

        if (key.StartsWith("assistant-response|", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.AssistantResponse;
        }

        if (key.StartsWith("process-step-decision:", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.CompletedDecision;
        }

        if (key.StartsWith("process-mock-artifact:", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.ProcessMock;
        }

        if (key.StartsWith("agentframework-browser-artifact:", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.ProviderNativeBrowser;
        }

        if (key.StartsWith("workflow-run:", StringComparison.OrdinalIgnoreCase) &&
            key.Contains(":artifact:", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.WorkflowArtifact;
        }

        if (key.StartsWith("workflow-run:", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.WorkflowRun;
        }

        if (artifact.ProvenanceSummary.Contains("manager", StringComparison.OrdinalIgnoreCase) ||
            artifact.ProvenanceSummary.Contains("recovery", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.ManagerRecovery;
        }

        return string.IsNullOrWhiteSpace(key)
            ? ProcessArtifactProducerKind.Manual
            : ProcessArtifactProducerKind.Unknown;
    }

    private static bool IsProducerAllowedForMode(
        ProcessArtifactExpectationMode mode,
        ProcessArtifactProducerKind producerKind,
        DispatchArtifactExpectation expectation)
    {
        return mode switch
        {
            ProcessArtifactExpectationMode.Narrative => producerKind != ProcessArtifactProducerKind.WorkflowRun,
            ProcessArtifactExpectationMode.Decision => producerKind is not ProcessArtifactProducerKind.WorkflowRun and not ProcessArtifactProducerKind.ProviderNativeBrowser,
            ProcessArtifactExpectationMode.Evidence => producerKind is not ProcessArtifactProducerKind.AssistantResponse and not ProcessArtifactProducerKind.CompletedDecision,
            ProcessArtifactExpectationMode.Deliverable => producerKind is
                ProcessArtifactProducerKind.AgentExecutionArtifact or
                ProcessArtifactProducerKind.WorkspaceWrite or
                ProcessArtifactProducerKind.ExistingManagedFile or
                ProcessArtifactProducerKind.WorkflowArtifact or
                ProcessArtifactProducerKind.ManagerRecovery or
                ProcessArtifactProducerKind.Manual,
            ProcessArtifactExpectationMode.RuntimeProof => producerKind is
                ProcessArtifactProducerKind.AgentExecutionArtifact or
                ProcessArtifactProducerKind.WorkspaceWrite or
                ProcessArtifactProducerKind.ProviderNativeBrowser or
                ProcessArtifactProducerKind.WorkflowArtifact or
                ProcessArtifactProducerKind.ManagerRecovery or
                ProcessArtifactProducerKind.Manual,
            ProcessArtifactExpectationMode.RecoveryDiagnostic => false,
            _ => false
        };
    }

    private static bool RequiresManagedEvidencePath(
        ProcessArtifactExpectationMode mode,
        ProcessArtifactProducerKind producerKind)
    {
        if (producerKind == ProcessArtifactProducerKind.WorkflowArtifact)
        {
            return false;
        }

        return mode is ProcessArtifactExpectationMode.Evidence or
            ProcessArtifactExpectationMode.Deliverable or
            ProcessArtifactExpectationMode.RuntimeProof;
    }

    private static bool IsCurrentRunArtifact(
        ProcessArtifactRecord artifact,
        Guid processRunId,
        Guid stepRunId,
        Guid? executionRunId,
        Guid? workflowRunId)
    {
        if (artifact.ProcessRunId != processRunId || artifact.StepRunId != stepRunId)
        {
            return false;
        }

        var key = artifact.ExternalReferenceKey;
        if (executionRunId.HasValue &&
            (key.Contains(executionRunId.Value.ToString("D"), StringComparison.OrdinalIgnoreCase) ||
             artifact.ProvenanceSummary.Contains(executionRunId.Value.ToString("D"), StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (workflowRunId.HasValue &&
            key.Contains(workflowRunId.Value.ToString("D"), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !key.StartsWith("existing-managed-artifact|", StringComparison.OrdinalIgnoreCase) ||
               !executionRunId.HasValue ||
               key.Contains(executionRunId.Value.ToString("D"), StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesDeclaredFormat(
        DispatchArtifactExpectation expectation,
        ProcessArtifactRecord artifact,
        out string diagnostic)
    {
        diagnostic = string.Empty;
        var contractText = CollapsePromptWhitespace(string.Join(' ', expectation.Title, expectation.ValidationRequirementSummary)).ToLowerInvariant();
        var extension = Path.GetExtension(artifact.ManagedStoragePath);
        if (contractText.Contains("json", StringComparison.Ordinal) && !string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
        {
            diagnostic = "The artifact contract declares JSON, but the managed artifact path is not a .json file.";
            return false;
        }

        if ((contractText.Contains("markdown", StringComparison.Ordinal) || contractText.Contains(".md", StringComparison.Ordinal)) &&
            !string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase))
        {
            diagnostic = "The artifact contract declares Markdown, but the managed artifact path is not a .md file.";
            return false;
        }

        if ((contractText.Contains("screenshot", StringComparison.Ordinal) || contractText.Contains("image", StringComparison.Ordinal)) &&
            extension is not ".png" and not ".jpg" and not ".jpeg" and not ".webp")
        {
            diagnostic = "The artifact contract declares image or screenshot evidence, but the managed artifact path is not an image file.";
            return false;
        }

        return true;
    }

    private static bool ContainsPlaceholderArtifactSignal(ProcessArtifactRecord artifact)
    {
        var text = CollapsePromptWhitespace(string.Join(
            ' ',
            artifact.Title,
            artifact.ReviewSummary,
            artifact.ProvenanceSummary,
            artifact.ManagedStoragePath,
            artifact.ExternalReferenceKey)).ToLowerInvariant();
        return text.Contains("placeholder", StringComparison.Ordinal) ||
               text.Contains("gap marker", StringComparison.Ordinal) ||
               text.Contains("missing artifact", StringComparison.Ordinal) ||
               text.Contains("not available", StringComparison.Ordinal) ||
               text.Contains("todo", StringComparison.Ordinal);
    }

    private static Guid? ResolveWorkflowRunIdForStep(IReadOnlyList<ProcessArtifactRecord> artifacts)
    {
        foreach (var artifact in artifacts)
        {
            var key = artifact.ExternalReferenceKey;
            if (!key.StartsWith("workflow-run:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var start = "workflow-run:".Length;
            var length = Math.Min(36, key.Length - start);
            if (length <= 0)
            {
                continue;
            }

            if (Guid.TryParse(key.Substring(start, length), out var workflowRunId))
            {
                return workflowRunId;
            }
        }

        return null;
    }

    private static ProcessArtifactExpectationValidationResult CreateArtifactValidationResult(
        Guid processRunId,
        Guid stepRunId,
        DispatchArtifactExpectation expectation,
        ProcessArtifactExpectationMode mode,
        ProcessArtifactValidationStatus status,
        ProcessArtifactProducerKind producerKind,
        ProcessArtifactRecord? artifact,
        string attemptedPath,
        string diagnostic,
        string suggestedAction,
        ProcessStepCompletionExecutorKind executorKind,
        Guid? executionRunId,
        Guid? workflowRunId)
    {
        var fingerprint = CreateArtifactFailureFingerprint(
            processRunId,
            stepRunId,
            expectation.Id,
            status,
            attemptedPath,
            mode,
            expectation.ValidationRequirementSummary,
            executorKind,
            executionRunId,
            workflowRunId);
        return new ProcessArtifactExpectationValidationResult(
            expectation.Id,
            expectation.Title,
            mode,
            status,
            producerKind,
            artifact?.Id,
            attemptedPath,
            diagnostic,
            suggestedAction,
            fingerprint);
    }

    private static string CreateArtifactFailureFingerprint(
        Guid processRunId,
        Guid stepRunId,
        Guid expectationId,
        ProcessArtifactValidationStatus status,
        string attemptedPath,
        ProcessArtifactExpectationMode mode,
        string validationRequirementSummary,
        ProcessStepCompletionExecutorKind executorKind,
        Guid? executionRunId,
        Guid? workflowRunId)
    {
        var normalized = string.Join(
            "|",
            processRunId.ToString("D"),
            stepRunId.ToString("D"),
            expectationId.ToString("D"),
            status,
            NormalizeManagedRelativePathForComparison(attemptedPath),
            mode,
            CollapsePromptWhitespace(validationRequirementSummary).ToLowerInvariant(),
            executorKind,
            executionRunId?.ToString("D") ?? string.Empty,
            workflowRunId?.ToString("D") ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }
}
