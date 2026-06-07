using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private const int MaxProcessArtifactValidationContentBytes = 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8Encoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

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
        ProcessStepBlockCause? blockCause = null;
        var selectedBranchOutcomeId = context.SelectedBranchOutcomeId;

        if (context.ExecutionDetail is not null && context.ProjectExecutionArtifacts)
        {
            ResetRecordedArtifactExpectationsForExecutionProjection(candidate);
            await ProjectExecutionArtifactsAsync(
                candidate,
                context.ExecutionDetail,
                context.ResponseText,
                completionStatus,
                dispatchClaim,
                cancellationToken);
        }

        var validationContext = context;
        var validationResults = await ValidateRequiredCompletionArtifactsAsync(validationContext, cancellationToken);
        if (completionStatus == ProcessStepRunStatus.Completed)
        {
            await PersistArtifactValidationDiagnosticsAsync(validationContext, validationResults, cancellationToken);
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

                validationContext = context with
                    {
                        CompletionStatus = completionStatus,
                        CompletionReason = completionReason,
                        SelectedBranchOutcomeId = selectedBranchOutcomeId,
                        ProjectExecutionArtifacts = false,
                        AllowManagerArtifactRecovery = false,
                        RecoveryExecutionRunId = recoveryOutcome.Detail.Run.Id,
                        RecoveredForExecutionRunId = context.ExecutionDetail.Run.Id
                    };
                validationResults = await ValidateRequiredCompletionArtifactsAsync(validationContext, cancellationToken);
                await PersistArtifactValidationDiagnosticsAsync(validationContext, validationResults, cancellationToken);
                unsatisfiedResults = validationResults.Where(result => !result.IsSatisfied).ToList();
            }

            if (completionStatus == ProcessStepRunStatus.Completed && unsatisfiedResults.Count > 0)
            {
                var routedDisposition = ResolveArtifactContractDispositionBranchOutcome(candidate, unsatisfiedResults);
                if (routedDisposition is not null)
                {
                    completionReason = BuildArtifactContractDispositionReason(routedDisposition, unsatisfiedResults);
                    selectedBranchOutcomeId = routedDisposition.Id;
                }
                else
                {
                    completionStatus = ProcessStepRunStatus.Blocked;
                    completionReason = BuildArtifactContractBlockedReason(unsatisfiedResults);
                    blockCause = ResolveArtifactContractBlockCause(unsatisfiedResults);
                    selectedBranchOutcomeId = null;
                }
            }
        }
        else
        {
            RefreshCandidateArtifactSatisfaction(candidate, validationResults);
        }

        if (completionStatus == ProcessStepRunStatus.Completed &&
            selectedBranchOutcomeId is null &&
            TryResolveExplicitDispositionBranchOutcome(candidate, context.ResponseText, out var explicitDisposition))
        {
            selectedBranchOutcomeId = explicitDisposition.Id;
            completionReason = BuildExplicitDispositionCompletionReason(
                explicitDisposition,
                completionReason,
                "selected from explicit current-run disposition text");
        }

        if (selectedBranchOutcomeId is null &&
            TryResolveSatisfiedArtifactDispositionCompletion(
                candidate,
                completionStatus,
                validationResults,
                context.ResponseText,
                out var recoveredDisposition,
                out var recoveredDispositionReason))
        {
            completionStatus = ProcessStepRunStatus.Completed;
            completionReason = recoveredDispositionReason;
            selectedBranchOutcomeId = recoveredDisposition.Id;
            blockCause = null;
        }

        var invariantViolations = await PersistRuntimeInvariantAuditAsync(
            context,
            completionStatus,
            validationResults,
            cancellationToken);
        var severeInvariant = invariantViolations.FirstOrDefault(violation =>
            violation.Severity is ProcessConformanceSeverity.High or ProcessConformanceSeverity.Critical);
        if (completionStatus == ProcessStepRunStatus.Completed && severeInvariant is not null)
        {
            completionStatus = ProcessStepRunStatus.Blocked;
            completionReason = $"Runtime invariant violation: {severeInvariant.Observation}";
            blockCause = ProcessStepBlockCause.RuntimeEvidence;
            selectedBranchOutcomeId = null;
        }

        if (completionStatus is ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Failed &&
            !blockCause.HasValue)
        {
            blockCause = ProcessBlockStateClassifier.InferBlockCause(completionReason);
        }

        return new ProcessStepCompletionFinalizerResult(
            completionStatus,
            completionReason,
            blockCause,
            selectedBranchOutcomeId,
            stepRunSnapshot.ConcurrencyToken,
            validationResults,
            BuildStepTransitionArtifactValidationContext(validationContext));
    }

    internal static void ResetRecordedArtifactExpectationsForExecutionProjection(DispatchCandidate candidate)
    {
        candidate.RecordedArtifactExpectationIds.Clear();
    }

    private async Task ApplyFinalizedStepTransitionAsync(
        DispatchCandidate candidate,
        ProcessStepCompletionFinalizerResult finalizerResult,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var completionResult = await TransitionStepWithClaimAsync(
            BuildFinalizedStepTransitionRequest(candidate, finalizerResult),
            dispatchClaim,
            cancellationToken);

        if (completionResult.IsSuccess)
        {
            await TrySyncProcessRunActualCostAsync(candidate.Run.Id, cancellationToken);
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

    internal static ProcessArtifactExpectationValidationResult ValidateArtifactExpectationForRecordedArtifacts(
        Guid processRunId,
        Guid stepRunId,
        DispatchArtifactExpectation expectation,
        IReadOnlyList<ProcessArtifactRecord> artifacts,
        ProcessStepCompletionExecutorKind executorKind,
        Guid? executionRunId = null,
        Guid? workflowRunId = null,
        Guid? subprocessRunId = null,
        Guid? recoveryExecutionRunId = null,
        Guid? recoveredForExecutionRunId = null,
        IProcessArtifactContentReader? managedArtifactContentReader = null)
        => ProcessCompletionArtifactValidator.ValidateArtifactExpectationForRecordedArtifacts(
            processRunId,
            stepRunId,
            expectation,
            artifacts,
            executorKind,
            executionRunId,
            workflowRunId,
            subprocessRunId,
            recoveryExecutionRunId,
            recoveredForExecutionRunId,
            managedArtifactContentReader);

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
                failure.FailureOwnership,
                failure.ArtifactRecordId,
                failure.AttemptedPath,
                failure.Diagnostic,
                failure.SuggestedAction,
                failure.Fingerprint,
                context.ExecutorKind,
                context.ExecutionDetail?.Run.Id,
                context.WorkflowRunId,
                context.SubprocessRunId,
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

    internal static ProcessStepBlockCause ResolveArtifactContractBlockCause(
        IReadOnlyList<ProcessArtifactExpectationValidationResult> unsatisfiedResults)
    {
        ArgumentNullException.ThrowIfNull(unsatisfiedResults);

        if (unsatisfiedResults.Any(result => result.FailureOwnership == ProcessArtifactFailureOwnership.UpstreamInput))
        {
            return ProcessStepBlockCause.UpstreamInput;
        }

        if (unsatisfiedResults.Any(result => result.FailureOwnership == ProcessArtifactFailureOwnership.RuntimeEvidence))
        {
            return ProcessStepBlockCause.RuntimeEvidence;
        }

        return ProcessStepBlockCause.OwnOutput;
    }

    private static DispatchBranchOutcome? ResolveArtifactContractDispositionBranchOutcome(
        DispatchCandidate candidate,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> unsatisfiedResults)
    {
        if (unsatisfiedResults.Count == 0 ||
            candidate.BranchOutcomes.Count == 0 ||
            ProcessMissingUpstreamArtifactMaterializationFactsResolver.ResolveMissingInputs(candidate).Count > 0 ||
            !IsDispositionRoutingStep(candidate) ||
            !CanRouteArtifactContractDispositionFailures(candidate, unsatisfiedResults))
        {
            return null;
        }

        return ResolveNegativeDispositionBranchOutcome(candidate, unsatisfiedResults);
    }

    private static bool IsDispositionRoutingStep(DispatchCandidate candidate)
    {
        if (candidate.StepRun.StepKind is ProcessStepKind.Decision or ProcessStepKind.Approval or ProcessStepKind.Review)
        {
            return true;
        }

        var text = CollapsePromptWhitespace(string.Join(
                ' ',
                candidate.StepRun.Title,
                candidate.StepDefinition.Title,
                candidate.StepDefinition.DecisionRightsSummary,
                candidate.StepDefinition.OutputContractSummary,
                candidate.WorkBrief?.WorkBriefText,
                candidate.WorkBrief?.ExpectedOutcome))
            .ToLowerInvariant();
        return ContainsAnyToken(
            text,
            "qa",
            "quality",
            "review",
            "approval",
            "approve",
            "decision",
            "decide",
            "escalation",
            "escalate",
            "inspection",
            "inspect");
    }

    private static bool CanRouteArtifactContractDispositionFailures(
        DispatchCandidate candidate,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> unsatisfiedResults)
    {
        if (!HasSatisfiedRequiredDecisionArtifact(candidate))
        {
            return false;
        }

        return unsatisfiedResults.All(result =>
            ResolveDispositionRoutingFailureOwnership(result) == ProcessArtifactFailureOwnership.ReviewDisposition);
    }

    private static bool HasSatisfiedRequiredDecisionArtifact(DispatchCandidate candidate)
    {
        return candidate.ExpectedArtifacts.Any(expectation =>
            expectation.IsRequired &&
            expectation.ArtifactKind is ProcessArtifactKind.Decision or ProcessArtifactKind.DecisionRecord &&
            candidate.RecordedArtifactExpectationIds.Contains(expectation.Id));
    }

    private static ProcessArtifactFailureOwnership ResolveDispositionRoutingFailureOwnership(
        ProcessArtifactExpectationValidationResult result)
    {
        if (result.FailureOwnership == ProcessArtifactFailureOwnership.UpstreamInput ||
            result.Diagnostic.Contains("upstream", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactFailureOwnership.UpstreamInput;
        }

        if (IsOwnOutputArtifactProductionFailure(result))
        {
            return ProcessArtifactFailureOwnership.OwnOutput;
        }

        return result.FailureOwnership;
    }

    private static bool IsOwnOutputArtifactProductionFailure(ProcessArtifactExpectationValidationResult result)
    {
        return result.Status is ProcessArtifactValidationStatus.Missing or
            ProcessArtifactValidationStatus.InvalidFormat or
            ProcessArtifactValidationStatus.PlaceholderOnly or
            ProcessArtifactValidationStatus.StaleOrWrongRun or
            ProcessArtifactValidationStatus.ContentUnavailable or
            ProcessArtifactValidationStatus.ContentHashMismatch;
    }

    private static DispatchBranchOutcome? ResolveNegativeDispositionBranchOutcome(
        DispatchCandidate candidate,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> unsatisfiedResults)
    {
        if (TryResolveRepairBranchOutcome(candidate, out var repairBranchOutcome))
        {
            return IsRepairDispositionCompatible(unsatisfiedResults)
                ? repairBranchOutcome
                : null;
        }

        return candidate.BranchOutcomes.FirstOrDefault(IsNegativeDispositionBranchOutcomeCandidate);
    }

    private static bool IsRepairDispositionCompatible(
        IReadOnlyList<ProcessArtifactExpectationValidationResult> unsatisfiedResults)
    {
        return unsatisfiedResults.Any(result =>
            result.Status is ProcessArtifactValidationStatus.InvalidFormat or
                ProcessArtifactValidationStatus.InsufficientEvidence or
                ProcessArtifactValidationStatus.WrongProducerMode or
                ProcessArtifactValidationStatus.PlaceholderOnly or
                ProcessArtifactValidationStatus.ContentUnavailable or
                ProcessArtifactValidationStatus.ContentHashMismatch);
    }

    private static bool TryResolveSatisfiedArtifactDispositionCompletion(
        DispatchCandidate candidate,
        ProcessStepRunStatus completionStatus,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> validationResults,
        string? responseText,
        out DispatchBranchOutcome branchOutcome,
        out string completionReason)
    {
        branchOutcome = null!;
        completionReason = string.Empty;
        if (completionStatus is not (ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Failed) ||
            !candidate.RequiresExplicitBranchOutcomeSelection ||
            candidate.BranchOutcomes.Count == 0 ||
            ProcessMissingUpstreamArtifactMaterializationFactsResolver.ResolveMissingInputs(candidate).Count > 0 ||
            !IsDispositionRoutingStep(candidate) ||
            !HasSatisfiedRequiredCompletionArtifacts(candidate, validationResults) ||
            !TryResolveExplicitDispositionBranchOutcome(candidate, responseText, out branchOutcome))
        {
            return false;
        }

        completionReason = BuildExplicitDispositionCompletionReason(
            branchOutcome,
            ResolveDeclaredOutcomeReason(responseText),
            $"recovered from {completionStatus} status because required current-run artifacts are satisfied");
        return true;
    }

    private static bool HasSatisfiedRequiredCompletionArtifacts(
        DispatchCandidate candidate,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> validationResults)
    {
        var requiredExpectationIds = candidate.ExpectedArtifacts
            .Where(expectation => expectation.IsRequired)
            .Select(expectation => expectation.Id)
            .ToHashSet();
        if (requiredExpectationIds.Count == 0)
        {
            return false;
        }

        var resultsByExpectationId = validationResults.ToDictionary(result => result.ExpectationId);
        return requiredExpectationIds.All(expectationId =>
            resultsByExpectationId.TryGetValue(expectationId, out var result) && result.IsSatisfied);
    }

    private static string BuildExplicitDispositionCompletionReason(
        DispatchBranchOutcome branchOutcome,
        string originalReason,
        string recoveryContext)
    {
        var reason = CollapsePromptWhitespace(originalReason);
        if (string.IsNullOrWhiteSpace(reason))
        {
            return $"Governed disposition '{branchOutcome.Title}' {recoveryContext}.";
        }

        return $"Governed disposition '{branchOutcome.Title}' {recoveryContext}. Original reason: {reason}";
    }

    private static string ResolveDeclaredOutcomeReason(string? responseText)
    {
        return TryResolveDeclaredStepOutcome(responseText, out var declaredOutcome)
            ? declaredOutcome.Reason
            : responseText ?? string.Empty;
    }

    private static bool IsNegativeDispositionBranchOutcomeCandidate(DispatchBranchOutcome outcome)
    {
        var token = NormalizeBranchOutcomeToken($"{outcome.Key} {outcome.Title} {outcome.Description}");
        if (string.IsNullOrWhiteSpace(token) || IsAcceptingBranchOutcomeToken(token))
        {
            return false;
        }

        return token.Contains("nogo", StringComparison.Ordinal) ||
               token.Contains("escalat", StringComparison.Ordinal) ||
               token.Contains("reject", StringComparison.Ordinal) ||
               token.Contains("decline", StringComparison.Ordinal) ||
               token.Contains("fail", StringComparison.Ordinal) ||
               token.Contains("blocked", StringComparison.Ordinal) ||
               token.Contains("risk", StringComparison.Ordinal);
    }

    private static string BuildArtifactContractDispositionReason(
        DispatchBranchOutcome routedDisposition,
        IReadOnlyList<ProcessArtifactExpectationValidationResult> unsatisfiedResults)
    {
        var summary = string.Join(
            "; ",
            unsatisfiedResults
                .Take(5)
                .Select(result => $"{result.ExpectationTitle}: {result.Status} ({result.Diagnostic})"));
        return $"Required artifact contract validation produced governed disposition '{routedDisposition.Title}' instead of hard blocking: {summary}.";
    }

    private static ProcessArtifactExpectationMode ResolveArtifactExpectationMode(DispatchArtifactExpectation expectation)
    {
        return ProcessArtifactValidationDescriptorAdapter.ResolveArtifactExpectationMode(expectation);
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
        var lineage = ProcessArtifactProjectionLineageJson.Deserialize(artifact.ProjectionLineageJson);
        if (lineage is not null)
        {
            if (IsManagerRecoveryLineage(lineage))
            {
                return ProcessArtifactProducerKind.ManagerRecovery;
            }

            var typedProducerKind = ResolveArtifactProducerKind(lineage.SourceKind);
            if (typedProducerKind != ProcessArtifactProducerKind.Unknown)
            {
                return typedProducerKind;
            }
        }

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

        if (key.StartsWith("subprocess-run:", StringComparison.OrdinalIgnoreCase) &&
            key.Contains(":artifact:", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.SubprocessArtifact;
        }

        if (key.StartsWith("manager-recovery-artifact|", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactProducerKind.ManagerRecovery;
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
        => ProcessArtifactEvidenceValidationRules.IsProducerAllowedForMode(mode, producerKind);

    private static bool RequiresManagedEvidencePath(
        ProcessArtifactExpectationMode mode,
        ProcessArtifactProducerKind producerKind)
        => ProcessArtifactEvidenceValidationRules.RequiresManagedEvidencePath(mode, producerKind);

    private static bool RequiresStoredArtifactContent(
        DispatchArtifactExpectation expectation,
        ProcessArtifactRecord artifact,
        ProcessArtifactExpectationMode mode,
        ProcessArtifactProducerKind producerKind)
        => ProcessArtifactEvidenceValidationRules.RequiresStoredArtifactContent(
            expectation.IsRequired,
            mode,
            producerKind,
            artifact.ManagedStoragePath);

    private static ProcessArtifactProducerKind ResolveArtifactProducerKind(ProcessArtifactProjectionSourceKind sourceKind)
    {
        return ProcessArtifactValidationDescriptorAdapter.ResolveArtifactProducerKind(sourceKind);
    }

    private static bool IsManagerRecoveryLineage(ProcessArtifactProjectionLineage lineage)
    {
        return lineage.RecoveryExecutionRunId.HasValue && lineage.RecoveredForExecutionRunId.HasValue;
    }

    private static bool MatchesDeclaredFormat(
        DispatchArtifactExpectation expectation,
        ProcessArtifactRecord artifact,
        ProcessArtifactExpectationMode mode,
        ProcessArtifactProducerKind producerKind,
        bool requiresStoredContent,
        IProcessArtifactContentReader? managedArtifactContentReader,
        out string diagnostic)
    {
        diagnostic = string.Empty;
        var contractText = CollapsePromptWhitespace(string.Join(' ', expectation.Title, expectation.ValidationRequirementSummary)).ToLowerInvariant();
        var extension = ResolveManagedArtifactExtension(artifact.ManagedStoragePath);
        ProcessArtifactContentReadResult? content = null;
        var contentRead = false;

        bool TryReadStoredContent(
            out ProcessArtifactContentReadResult? readContent,
            out string readDiagnostic)
        {
            readDiagnostic = string.Empty;
            if (contentRead)
            {
                readContent = content;
                return true;
            }

            contentRead = true;
            if (!TryReadManagedArtifactContent(artifact, managedArtifactContentReader, out content, out readDiagnostic))
            {
                readContent = null;
                return false;
            }

            readContent = content;
            return true;
        }

        var requiresJson = contractText.Contains("json", StringComparison.Ordinal);
        if (requiresJson && !string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
        {
            diagnostic = "The artifact contract declares JSON, but the managed artifact path is not a .json file.";
            return false;
        }

        if (requiresJson && !HasValidJsonArtifactContent(artifact, managedArtifactContentReader, out diagnostic))
        {
            return false;
        }

        var requiresYaml =
            contractText.Contains("yaml", StringComparison.Ordinal) ||
            contractText.Contains(".yml", StringComparison.Ordinal) ||
            contractText.Contains(".yaml", StringComparison.Ordinal);
        if (requiresYaml && extension is not ".yml" and not ".yaml")
        {
            diagnostic = "The artifact contract declares YAML, but the managed artifact path is not a .yml or .yaml file.";
            return false;
        }

        if (requiresYaml &&
            requiresStoredContent &&
            (!TryReadStoredContent(out var yamlContent, out diagnostic) ||
             !HasReadableTextArtifactContent(yamlContent, "YAML", out diagnostic)))
        {
            return false;
        }

        var requiresMarkdown = contractText.Contains("markdown", StringComparison.Ordinal) || contractText.Contains(".md", StringComparison.Ordinal);
        if (requiresMarkdown &&
            !string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase))
        {
            diagnostic = "The artifact contract declares Markdown, but the managed artifact path is not a .md file.";
            return false;
        }

        if (requiresMarkdown &&
            requiresStoredContent &&
            (!TryReadStoredContent(out var markdownContent, out diagnostic) ||
             !HasReadableTextArtifactContent(markdownContent, "Markdown", out diagnostic)))
        {
            return false;
        }

        var requiresImage = RequiresImageArtifactFile(expectation, artifact, contractText, extension);
        if (requiresImage &&
            extension is not ".png" and not ".jpg" and not ".jpeg" and not ".webp" and not ".svg")
        {
            diagnostic = "The artifact contract declares image or screenshot evidence, but the managed artifact path is not an image file.";
            return false;
        }

        if (requiresImage &&
            requiresStoredContent &&
            (!TryReadStoredContent(out var imageContent, out diagnostic) ||
             !HasValidImageArtifactContent(imageContent, out diagnostic)))
        {
            return false;
        }

        if (requiresStoredContent &&
            mode is ProcessArtifactExpectationMode.Evidence or ProcessArtifactExpectationMode.RuntimeProof &&
            !TryReadStoredContent(out _, out diagnostic))
        {
            return false;
        }

        return true;
    }

    private static bool RequiresImageArtifactFile(
        DispatchArtifactExpectation expectation,
        ProcessArtifactRecord artifact,
        string contractText,
        string extension)
    {
        var titleText = CollapsePromptWhitespace(expectation.Title).ToLowerInvariant();
        if (IsImageArtifactExtension(extension))
        {
            return true;
        }

        if (TryExtractExpectedArtifactRelativePath(expectation.ValidationRequirementSummary, out var declaredRelativePath) &&
            IsImageArtifactExtension(Path.GetExtension(declaredRelativePath).ToLowerInvariant()))
        {
            return true;
        }

        if (ContainsExplicitImageFileSignal(contractText) &&
            !IsNarrativeArtifactContainerTitle(titleText))
        {
            return true;
        }

        if (ContainsImageEvidenceToken(titleText) &&
            !IsNarrativeArtifactContainerTitle(titleText))
        {
            return true;
        }

        return ContainsExplicitImageFileSignal(artifact.ManagedStoragePath.ToLowerInvariant());
    }

    private static bool IsImageArtifactExtension(string extension)
    {
        return extension is ".png" or ".jpg" or ".jpeg" or ".webp" or ".svg";
    }

    private static bool ContainsExplicitImageFileSignal(string text)
    {
        return text.Contains(".png", StringComparison.Ordinal) ||
               text.Contains(".jpg", StringComparison.Ordinal) ||
               text.Contains(".jpeg", StringComparison.Ordinal) ||
               text.Contains(".webp", StringComparison.Ordinal) ||
               text.Contains(".svg", StringComparison.Ordinal) ||
               text.Contains("image file", StringComparison.Ordinal) ||
               text.Contains("screenshot file", StringComparison.Ordinal) ||
               text.Contains("image artifact", StringComparison.Ordinal) ||
               text.Contains("screenshot artifact", StringComparison.Ordinal) ||
               text.Contains("as an image", StringComparison.Ordinal) ||
               text.Contains("as a screenshot", StringComparison.Ordinal);
    }

    private static bool ContainsImageEvidenceToken(string text)
    {
        return text.Contains("screenshot", StringComparison.Ordinal) ||
               text.Contains("image", StringComparison.Ordinal);
    }

    private static bool IsNarrativeArtifactContainerTitle(string titleText)
    {
        return titleText.Contains("pack", StringComparison.Ordinal) ||
               titleText.Contains("summary", StringComparison.Ordinal) ||
               titleText.Contains("report", StringComparison.Ordinal) ||
               titleText.Contains("index", StringComparison.Ordinal) ||
               titleText.Contains("log", StringComparison.Ordinal) ||
               titleText.Contains("manifest", StringComparison.Ordinal) ||
               titleText.Contains("list", StringComparison.Ordinal);
    }

    private static string ResolveManagedArtifactExtension(string managedStoragePath)
    {
        if (StorageJson.TryParseReference(managedStoragePath, out var reference) && reference is not null)
        {
            return Path.GetExtension(reference.Locator).ToLowerInvariant();
        }

        return Path.GetExtension(managedStoragePath).ToLowerInvariant();
    }

    private static bool ContainsPlaceholderArtifactSignal(
        ProcessArtifactRecord artifact,
        ProcessArtifactExpectationMode mode)
    {
        var text = CollapsePromptWhitespace(string.Join(
            ' ',
            artifact.Title,
            artifact.ReviewSummary,
            artifact.ProvenanceSummary,
            artifact.ManagedStoragePath,
            artifact.ExternalReferenceKey)).ToLowerInvariant();
        if (IsLegitimateUnavailableOrPlanningArtifact(text, mode))
        {
            return false;
        }

        return text.Contains("placeholder", StringComparison.Ordinal) ||
               text.Contains("gap marker", StringComparison.Ordinal) ||
               text.Contains("missing artifact diagnostic", StringComparison.Ordinal) ||
               text.Contains("artifact is not available", StringComparison.Ordinal) ||
               text.Contains("no artifact available", StringComparison.Ordinal);
    }

    private static bool IsLegitimateUnavailableOrPlanningArtifact(
        string text,
        ProcessArtifactExpectationMode mode)
    {
        if (mode is not (ProcessArtifactExpectationMode.Narrative or ProcessArtifactExpectationMode.Decision or ProcessArtifactExpectationMode.Deliverable))
        {
            return false;
        }

        return text.Contains("todo register", StringComparison.Ordinal) ||
               text.Contains("todo list", StringComparison.Ordinal) ||
               text.Contains("unavailable findings", StringComparison.Ordinal) ||
               text.Contains("not available finding", StringComparison.Ordinal) ||
               text.Contains("missing artifact analysis", StringComparison.Ordinal) ||
               text.Contains("missing-artifact analysis", StringComparison.Ordinal);
    }

    private static bool HasValidJsonArtifactContent(
        ProcessArtifactRecord artifact,
        IProcessArtifactContentReader? managedArtifactContentReader,
        out string diagnostic)
    {
        diagnostic = string.Empty;
        if (managedArtifactContentReader is not null)
        {
            if (!TryReadManagedArtifactContent(artifact, managedArtifactContentReader, out var content, out diagnostic))
            {
                return false;
            }

            if (content?.TextContent is null)
            {
                diagnostic = $"The artifact contract declares JSON, but the managed artifact content type '{content?.ContentType ?? "unknown"}' is not readable text.";
                return false;
            }

            try
            {
                using var _ = JsonDocument.Parse(content.TextContent);
                return true;
            }
            catch (JsonException exception)
            {
                diagnostic = $"The artifact contract declares JSON, but the managed artifact content is malformed JSON: {exception.Message}";
                return false;
            }
        }

        if (Path.IsPathRooted(artifact.ManagedStoragePath) && File.Exists(artifact.ManagedStoragePath))
        {
            try
            {
                using var stream = File.OpenRead(artifact.ManagedStoragePath);
                using var _ = JsonDocument.Parse(stream);
                return true;
            }
            catch (JsonException exception)
            {
                diagnostic = $"The artifact contract declares JSON, but the managed artifact content is malformed JSON: {exception.Message}";
                return false;
            }
            catch (IOException exception)
            {
                diagnostic = $"The artifact contract declares JSON, but the managed artifact content could not be read: {exception.Message}";
                return false;
            }
        }

        if (TryResolveInlineJsonArtifactContent(artifact, out var jsonContent))
        {
            try
            {
                using var _ = JsonDocument.Parse(jsonContent);
                return true;
            }
            catch (JsonException exception)
            {
                diagnostic = $"The artifact contract declares JSON, but the recorded JSON content is malformed: {exception.Message}";
                return false;
            }
        }

        return true;
    }

    private static bool TryReadManagedArtifactContent(
        ProcessArtifactRecord artifact,
        IProcessArtifactContentReader? managedArtifactContentReader,
        out ProcessArtifactContentReadResult? content,
        out string diagnostic)
    {
        content = null;
        diagnostic = string.Empty;
        if (managedArtifactContentReader is null)
        {
            return true;
        }

        content = managedArtifactContentReader.Read(artifact.ManagedStoragePath);
        if (!content.Succeeded)
        {
            diagnostic = $"The managed artifact content could not be loaded from '{artifact.ManagedStoragePath}': {content.Diagnostic}";
            return false;
        }

        if (content.ByteLength == 0)
        {
            diagnostic = $"The managed artifact content at '{artifact.ManagedStoragePath}' is empty.";
            return false;
        }

        return true;
    }

    private static bool TryValidateManagedArtifactContent(
        ProcessArtifactRecord artifact,
        IProcessArtifactContentReader managedArtifactContentReader,
        out string diagnostic,
        out ProcessArtifactValidationStatus status)
    {
        status = ProcessArtifactValidationStatus.Satisfied;
        if (!TryReadManagedArtifactContent(artifact, managedArtifactContentReader, out var content, out diagnostic))
        {
            status = ProcessArtifactValidationStatus.ContentUnavailable;
            return false;
        }

        var lineage = ProcessArtifactProjectionLineageJson.Deserialize(artifact.ProjectionLineageJson);
        var expectedContentHash = lineage?.ContentHash?.Trim();
        if (string.IsNullOrWhiteSpace(expectedContentHash))
        {
            return true;
        }

        var actualContentHash = ProcessArtifactIdentityService.ComputeContentHash(content?.ContentBytes ?? []);
        if (string.Equals(expectedContentHash, actualContentHash, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        diagnostic = "The managed artifact content hash does not match the recorded projection lineage content hash.";
        status = ProcessArtifactValidationStatus.ContentHashMismatch;
        return false;
    }

    private static bool HasReadableTextArtifactContent(
        ProcessArtifactContentReadResult? content,
        string declaredFormat,
        out string diagnostic)
    {
        if (content is null)
        {
            diagnostic = string.Empty;
            return true;
        }

        if (string.IsNullOrWhiteSpace(content.TextContent))
        {
            diagnostic = $"The artifact contract declares {declaredFormat}, but the managed artifact content type '{content.ContentType}' is not readable non-empty text.";
            return false;
        }

        diagnostic = string.Empty;
        return true;
    }

    private static bool HasValidImageArtifactContent(
        ProcessArtifactContentReadResult? content,
        out string diagnostic)
    {
        if (content is null)
        {
            diagnostic = string.Empty;
            return true;
        }

        if (!content.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            diagnostic = $"The artifact contract declares image or screenshot evidence, but the managed artifact content type is '{content.ContentType}'.";
            return false;
        }

        var extension = Path.GetExtension(content.ResolvedPath).ToLowerInvariant();
        var bytes = content.ContentBytes;
        var isValidImage = extension switch
        {
            ".png" => bytes.Length >= 8 &&
                bytes[0] == 0x89 &&
                bytes[1] == 0x50 &&
                bytes[2] == 0x4E &&
                bytes[3] == 0x47 &&
                bytes[4] == 0x0D &&
                bytes[5] == 0x0A &&
                bytes[6] == 0x1A &&
                bytes[7] == 0x0A,
            ".jpg" or ".jpeg" => bytes.Length >= 2 &&
                bytes[0] == 0xFF &&
                bytes[1] == 0xD8,
            ".webp" => bytes.Length >= 12 &&
                bytes[0] == (byte)'R' &&
                bytes[1] == (byte)'I' &&
                bytes[2] == (byte)'F' &&
                bytes[3] == (byte)'F' &&
                bytes[8] == (byte)'W' &&
                bytes[9] == (byte)'E' &&
                bytes[10] == (byte)'B' &&
                bytes[11] == (byte)'P',
            ".svg" => content.TextContent?.Contains("<svg", StringComparison.OrdinalIgnoreCase) == true,
            _ => false
        };

        if (isValidImage)
        {
            diagnostic = string.Empty;
            return true;
        }

        diagnostic = "The artifact contract declares image or screenshot evidence, but the stored bytes do not match the declared image format.";
        return false;
    }

    private static string? TryDecodeManagedArtifactTextContent(
        string contentType,
        string fullPath,
        byte[] contentBytes)
    {
        if (!IsTextReadableArtifactContent(contentType, fullPath))
        {
            return null;
        }

        if (contentBytes.Contains((byte)0))
        {
            return null;
        }

        try
        {
            return StrictUtf8Encoding.GetString(contentBytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static bool IsTextReadableArtifactContent(string contentType, string fullPath)
    {
        return contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("xml", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("yaml", StringComparison.OrdinalIgnoreCase) ||
               IsTextReadableManagedArtifactPath(fullPath);
    }

    private static bool TryResolveInlineJsonArtifactContent(
        ProcessArtifactRecord artifact,
        out string jsonContent)
    {
        jsonContent = string.Empty;
        foreach (var text in new[] { artifact.ReviewSummary, artifact.ProvenanceSummary })
        {
            var trimmed = text.Trim();
            if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            {
                jsonContent = trimmed;
                return true;
            }

            const string jsonContentPrefix = "json content:";
            var prefixIndex = trimmed.IndexOf(jsonContentPrefix, StringComparison.OrdinalIgnoreCase);
            if (prefixIndex < 0)
            {
                continue;
            }

            jsonContent = trimmed[(prefixIndex + jsonContentPrefix.Length)..].Trim();
            return !string.IsNullOrWhiteSpace(jsonContent);
        }

        return false;
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
        Guid? workflowRunId,
        Guid? subprocessRunId,
        Guid? recoveryExecutionRunId = null,
        Guid? recoveredForExecutionRunId = null)
    {
        var failureOwnership = ResolveArtifactFailureOwnership(mode, status, diagnostic);
        var fingerprint = CreateArtifactFailureFingerprint(
            processRunId,
            stepRunId,
            expectation.Id,
            status,
            attemptedPath,
            mode,
            failureOwnership,
            expectation.ValidationRequirementSummary,
            executorKind,
            executionRunId,
            workflowRunId,
            subprocessRunId,
            recoveryExecutionRunId,
            recoveredForExecutionRunId);
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
            fingerprint,
            failureOwnership);
    }

    private static ProcessArtifactFailureOwnership ResolveArtifactFailureOwnership(
        ProcessArtifactExpectationMode mode,
        ProcessArtifactValidationStatus status,
        string diagnostic)
    {
        if (diagnostic.Contains("upstream", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactFailureOwnership.UpstreamInput;
        }

        if (status is ProcessArtifactValidationStatus.Missing or
            ProcessArtifactValidationStatus.InvalidFormat or
            ProcessArtifactValidationStatus.PlaceholderOnly or
            ProcessArtifactValidationStatus.StaleOrWrongRun or
            ProcessArtifactValidationStatus.ContentUnavailable or
            ProcessArtifactValidationStatus.ContentHashMismatch)
        {
            return ProcessArtifactFailureOwnership.OwnOutput;
        }

        return mode switch
        {
            ProcessArtifactExpectationMode.Decision => ProcessArtifactFailureOwnership.ReviewDisposition,
            ProcessArtifactExpectationMode.Evidence or ProcessArtifactExpectationMode.RuntimeProof => ProcessArtifactFailureOwnership.RuntimeEvidence,
            _ => ProcessArtifactFailureOwnership.OwnOutput
        };
    }

    private static string CreateArtifactFailureFingerprint(
        Guid processRunId,
        Guid stepRunId,
        Guid expectationId,
        ProcessArtifactValidationStatus status,
        string attemptedPath,
        ProcessArtifactExpectationMode mode,
        ProcessArtifactFailureOwnership failureOwnership,
        string validationRequirementSummary,
        ProcessStepCompletionExecutorKind executorKind,
        Guid? executionRunId,
        Guid? workflowRunId,
        Guid? subprocessRunId,
        Guid? recoveryExecutionRunId = null,
        Guid? recoveredForExecutionRunId = null)
    {
        var normalized = string.Join(
            "|",
            processRunId.ToString("D"),
            stepRunId.ToString("D"),
            expectationId.ToString("D"),
            status,
            NormalizeManagedRelativePathForComparison(attemptedPath),
            mode,
            failureOwnership,
            CollapsePromptWhitespace(validationRequirementSummary).ToLowerInvariant(),
            executorKind,
            executionRunId?.ToString("D") ?? string.Empty,
            workflowRunId?.ToString("D") ?? string.Empty,
            subprocessRunId?.ToString("D") ?? string.Empty,
            recoveryExecutionRunId?.ToString("D") ?? string.Empty,
            recoveredForExecutionRunId?.ToString("D") ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }
}
