using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    internal static bool HasBlockingAutomationExecutionRun(IReadOnlyList<ProcessAutomationExecutionRunRecord> executionRuns)
        => HasBlockingAutomationExecutionRun(executionRuns, DateTimeOffset.UtcNow);

    internal static bool HasBlockingAutomationExecutionRun(
        IReadOnlyList<ProcessAutomationExecutionRunRecord> executionRuns,
        DateTimeOffset now)
    {
        return ProcessAutomationExecutionRunSelection.HasBlockingAutomationExecutionRun(
            executionRuns,
            now,
            AutomationActor,
            StaleAutomationExecutionRunTimeout);
    }

    internal static Guid? ResolveBlockingAutomationExecutionRunId(
        IReadOnlyList<ProcessAutomationExecutionRunRecord> executionRuns)
        => ResolveBlockingAutomationExecutionRunId(executionRuns, DateTimeOffset.UtcNow);

    internal static Guid? ResolveBlockingAutomationExecutionRunId(
        IReadOnlyList<ProcessAutomationExecutionRunRecord> executionRuns,
        DateTimeOffset now)
    {
        return ProcessAutomationExecutionRunSelection.ResolveBlockingAutomationExecutionRunId(
            executionRuns,
            now,
            AutomationActor,
            StaleAutomationExecutionRunTimeout);
    }

    internal static Guid? ResolveBlockingAutomationExecutionRunId(
        ProcessStepRun stepRun,
        IReadOnlyList<ProcessAutomationExecutionRunRecord> executionRuns,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        return ProcessAutomationExecutionRunSelection.ResolveBlockingAutomationExecutionRunId(
            stepRun.StartedAtUtc,
            executionRuns,
            now,
            AutomationActor,
            StaleAutomationExecutionRunTimeout);
    }

    internal static Guid? ResolveRecoverableAutomationExecutionRunId(
        ProcessStepRun stepRun,
        IReadOnlyList<ProcessAutomationExecutionRunRecord> executionRuns)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        return ProcessAutomationExecutionRunSelection.ResolveRecoverableAutomationExecutionRunId(
            stepRun.Status,
            stepRun.StartedAtUtc,
            executionRuns,
            AutomationActor);
    }

    internal static Guid? ResolveReusableAutomationChatSessionId(
        IReadOnlyList<ProcessAutomationExecutionRunRecord> executionRuns)
    {
        ArgumentNullException.ThrowIfNull(executionRuns);

        return null;
    }

    private async Task<ConcurrentAutomationExecution?> TryAdoptConcurrentAutomationExecutionAsync(
        DispatchCandidate candidate,
        CancellationToken cancellationToken)
    {
        return await ProcessConcurrentExecutionAdoptionCoordinator.TryAdoptAsync(
            executionClient,
            candidate,
            clock.GetUtcNow(),
            AutomationActor,
            StaleAutomationExecutionRunTimeout,
            ResolveRecoveredExecutionResponseText,
            cancellationToken);
    }

    internal async Task<ProcessAutomationExecutionRunRecord?> ResolveCompetingActiveAutomationExecutionAsync(
        DispatchCandidate candidate,
        DispatchExecutionOutcome executionOutcome,
        CancellationToken cancellationToken)
    {
        var executionRuns = await executionClient.ListExecutionRunsAsync(
            ProcessExecutionRunQueryBuilder.ForCandidate(candidate),
            cancellationToken);

        return ProcessAutomationExecutionRunSelection.ResolveCompetingActiveAutomationExecutionRun(
            executionRuns,
            executionOutcome.Detail.Run.Id,
            candidate.StepRun.StartedAtUtc,
            clock.GetUtcNow(),
            AutomationActor,
            StaleAutomationExecutionRunTimeout);
    }

    internal static bool ShouldSkipAutomationCompletionTransition(
        ProcessStepRunStatus currentStatus,
        ProcessStepRunStatus requestedStatus)
    {
        return ProcessAutomationExecutionRunSelection.ShouldSkipAutomationCompletionTransition(
            currentStatus,
            requestedStatus);
    }

    internal static bool IsConcurrentAutomationSessionBusyException(Exception exception)
    {
        return ProcessAutomationExecutionRunSelection.IsConcurrentAutomationSessionBusyException(
            exception,
            ConcurrentAutomationSessionBusyMessages);
    }

    internal static bool ShouldSkipFreshAutomationDispatch(
        ProcessStepRunStatus currentStatus,
        Guid? recoverableExecutionRunId,
        DateTimeOffset? currentAttemptStartedAtUtc,
        DateTimeOffset now,
        string trigger)
    {
        return ProcessAutomationExecutionRunSelection.ShouldSkipFreshAutomationDispatch(
            currentStatus,
            recoverableExecutionRunId,
            currentAttemptStartedAtUtc,
            now,
            trigger,
            FreshInProgressRecoveryGracePeriod);
    }

    internal static bool ShouldSkipFreshAutomationDispatch(
        ProcessDispatchRouteSnapshot routeSnapshot,
        DateTimeOffset now)
    {
        return ProcessDispatchStartTransitionPlanner.ShouldSkipFreshAutomationDispatch(
            routeSnapshot,
            now,
            FreshInProgressRecoveryGracePeriod);
    }

    private static bool IsStaleAutomationExecutionRun(
        ProcessAutomationExecutionRunRecord executionRun,
        DateTimeOffset now)
    {
        return ProcessAutomationExecutionRunSelection.IsStaleAutomationExecutionRun(
            executionRun,
            now,
            StaleAutomationExecutionRunTimeout);
    }

    private static string ResolveRecoveredExecutionResponseText(ProcessAutomationExecutionRunDetail detail)
    {
        return ProcessExecutionResponseTextResolver.ResolveRecovered(
            detail,
            ResolveLatestAssistantResponseText);
    }

    private static string ResolvePreferredExecutionResponseText(
        DispatchCandidate candidate,
        string? responseText,
        ProcessAutomationExecutionRunDetail detail)
    {
        return ProcessExecutionResponseTextResolver.ResolvePreferred(
            RequiresGovernedStepOutcome(candidate.StepRun),
            responseText,
            detail,
            static value => TryResolveDeclaredStepOutcome(value, out _),
            ResolveRecoveredExecutionResponseText);
    }

    private static bool TryResolveRecoverableProviderFailure(
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        out string failureSummary)
    {
        return ProcessRecoverableProviderFailureRules.TryResolve(detail, responseText, out failureSummary);
    }

    private static ProcessStepRunStatus ResolveCompletionStatus(DispatchCandidate candidate, ProcessAutomationExecutionRunDetail detail)
    {
        var dispatchDecision = DispatchDecisionEngine.Evaluate(new DispatchDecisionInput(
            candidate,
            detail,
            [],
            detail.Run.ResultSummary,
            CarriedImplementationProof.None,
            ResolveOutputInspectionText(detail.Run.ResultSummary)));
        return dispatchDecision.CompletionStatus;
    }

    private static bool ShouldRetryIncompleteSuccessfulRun(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        CarriedImplementationProof carriedImplementationProof,
        int attemptNumber,
        int maxExecutionAttempts)
    {
        return ProcessIncompleteSuccessfulRunRetryRules.ShouldRetry(
            candidate,
            detail,
            responseText,
            missingRequiredTools,
            carriedImplementationProof,
            attemptNumber,
            maxExecutionAttempts);
    }

    private static bool ShouldCompressNoProgressRetry(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<string> retryReasons,
        int attemptNumber)
    {
        return ProcessNoProgressRetrySignalBuilder.ShouldCompress(
            candidate,
            detail,
            responseText,
            missingRequiredTools,
            retryReasons,
            attemptNumber);
    }

    internal static bool HasPriorNoProgressRetrySignal(
        IEnumerable<ProcessJournalEntry> journalEntries,
        NoProgressRetrySignal signal)
    {
        return ProcessNoProgressRetryLedgerRules.HasPriorSignal(journalEntries, signal);
    }

    private async Task<bool> HasPriorNoProgressRetrySignalAsync(
        DispatchCandidate candidate,
        NoProgressRetrySignal signal,
        CancellationToken cancellationToken)
    {
        return await new ProcessNoProgressRetryJournalQueryCoordinator(dbContextFactory)
            .HasPriorSignalAsync(candidate, signal, cancellationToken);
    }

    private static bool HasNewSatisfiedCurrentAttemptEvidence(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        return ProcessNoProgressEvidenceDeltaRules.HasNewSatisfiedCurrentAttemptEvidence(candidate, detail);
    }

    private static string? TryCreateNoProgressRetryFingerprint(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<string> retryReasons,
        int attemptNumber)
    {
        return ProcessNoProgressRetrySignalBuilder.TryCreateFingerprint(
            candidate,
            detail,
            responseText,
            missingRequiredTools,
            retryReasons,
            attemptNumber);
    }

    private static NoProgressRetrySignal? TryCreateNoProgressRetrySignal(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<string> retryReasons)
    {
        return ProcessNoProgressRetrySignalBuilder.TryCreateSignal(
            candidate,
            detail,
            responseText,
            missingRequiredTools,
            retryReasons);
    }

    private static string ResolveNoProgressMutationDelta(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        return ProcessNoProgressRetrySignalBuilder.ResolveMutationDelta(candidate, detail);
    }

    private static string ResolveNoProgressProofDelta(ProcessAutomationExecutionRunDetail detail)
    {
        return ProcessNoProgressRetrySignalBuilder.ResolveProofDelta(detail);
    }

    private static bool IsNoProgressRetryLedgerEvent(string eventType)
    {
        return ProcessNoProgressRetryLedgerRules.IsLedgerEvent(eventType);
    }

    private static bool TryResolveNoProgressRetryLedgerExecutionRunId(
        string? replayContextJson,
        out Guid executionRunId)
    {
        return ProcessNoProgressRetryLedgerRules.TryResolveExecutionRunId(replayContextJson, out executionRunId);
    }

    private static bool IsTerminalAutomationExecutionRun(ProcessAutomationExecutionRunRecord run)
    {
        return run.State is ProcessAutomationExecutionState.Completed or ProcessAutomationExecutionState.Failed;
    }

    private static bool IsNoProgressRetryReason(string retryReason)
    {
        return ProcessExecutionRetryReasonAggregator.IsNoProgressRetryReason(retryReason);
    }

    private static bool ShouldRetryRepairableImplementationBlockedOutcome(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        int attemptNumber,
        int maxExecutionAttempts)
    {
        if (attemptNumber >= maxExecutionAttempts ||
            detail.Run.State != ProcessAutomationExecutionState.Completed ||
            detail.Run.PendingApprovals.Count > 0 ||
            detail.Run.Outcome != ProcessAutomationRunOutcome.Succeeded ||
            !RequiresConcreteImplementationProof(candidate) ||
            !TryResolveDeclaredStepOutcome(candidate, responseText, out var declaredOutcome) ||
            declaredOutcome.Status != ProcessStepRunStatus.Blocked)
        {
            return false;
        }

        if (IsRecoverableImplementationPunt(candidate, responseText))
        {
            return true;
        }

        if (!HasSuccessfulConcreteProductMutation(candidate, detail) &&
            !HasRepairableImplementationValidationFailure(candidate, detail))
        {
            return false;
        }

        return missingRequiredTools.Count > 0 ||
            ResolveUnresolvedCriticalToolFailures(candidate, detail).Count > 0 ||
               !string.IsNullOrWhiteSpace(ResolveMissingConcreteImplementationProofSummary(candidate, detail)) ||
               !string.IsNullOrWhiteSpace(ResolveMissingRunnableApplicationProofSummary(candidate, detail));
    }

    private static bool ShouldRetryRecoverableBrowserProofBlockedOutcome(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        int attemptNumber,
        int maxExecutionAttempts)
    {
        if (attemptNumber >= maxExecutionAttempts ||
            detail.Run.State != ProcessAutomationExecutionState.Completed ||
            detail.Run.PendingApprovals.Count > 0 ||
            detail.Run.Outcome != ProcessAutomationRunOutcome.Succeeded ||
            !RequiresConcreteBrowserProof(candidate) ||
            missingRequiredTools.Count == 0 ||
            !TryResolveDeclaredStepOutcome(candidate, responseText, out var declaredOutcome) ||
            declaredOutcome.Status != ProcessStepRunStatus.Blocked)
        {
            return false;
        }

        if (!missingRequiredTools.Any(IsBrowserLaunchOrProofToolName))
        {
            return false;
        }

        return IsRecoverableBrowserProofPunt(responseText);
    }

    private static bool IsBrowserLaunchOrProofToolName(string toolName)
    {
        var normalizedToolName = NormalizeToolToken(toolName);
        return string.Equals(normalizedToolName, "workspace_pwsh_run_script", StringComparison.Ordinal) ||
               string.Equals(normalizedToolName, "workspace_dotnet_run", StringComparison.Ordinal) ||
               RequiredBrowserEvidenceToolNames.Contains(normalizedToolName);
    }

    private static bool IsRecoverableBrowserProofPunt(string? responseText)
    {
        var normalizedResponse = CollapsePromptWhitespace(responseText).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedResponse))
        {
            return false;
        }

        if (normalizedResponse.Contains("tool unavailable", StringComparison.Ordinal) ||
            normalizedResponse.Contains("tools unavailable", StringComparison.Ordinal) ||
            normalizedResponse.Contains("approval denied", StringComparison.Ordinal) ||
            normalizedResponse.Contains("permission denied", StringComparison.Ordinal))
        {
            return false;
        }

        return normalizedResponse.Contains("no reachable localhost url", StringComparison.Ordinal) ||
               normalizedResponse.Contains("no reachable url", StringComparison.Ordinal) ||
               normalizedResponse.Contains("no localhost url", StringComparison.Ordinal) ||
               normalizedResponse.Contains("no url exists", StringComparison.Ordinal) ||
               normalizedResponse.Contains("no browser receipts", StringComparison.Ordinal) ||
               normalizedResponse.Contains("browser receipts were captured", StringComparison.Ordinal) ||
               normalizedResponse.Contains("browser receipts are still missing", StringComparison.Ordinal) ||
               normalizedResponse.Contains("browser proof could not be completed", StringComparison.Ordinal) ||
               normalizedResponse.Contains("browser proof remains incomplete", StringComparison.Ordinal);
    }

    private static bool HasRepairableImplementationValidationFailure(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        var unresolvedCriticalToolFailures = ResolveUnresolvedCriticalToolFailures(candidate, detail);
        if (!unresolvedCriticalToolFailures.Any(IsImplementationValidationFailure))
        {
            return false;
        }

        var successfulReceipts = detail.ToolReceipts
            .Where(receipt => !IsFailedToolReceipt(receipt))
            .ToList();
        return ResolveLatestImplementationProofReadReceipt(candidate, successfulReceipts) is not null;
    }

    private static bool IsImplementationValidationFailure(ProcessAutomationToolExecutionReceipt receipt)
    {
        var toolName = NormalizeToolToken(receipt.ToolName);
        return IsBuildValidationToolName(toolName) ||
               IsRunValidationToolName(toolName);
    }

    private static IReadOnlyList<string> ResolveIncompleteSuccessfulRunRetryReasons(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        CarriedImplementationProof carriedImplementationProof)
    {
        return ProcessExecutionRetryReasonAggregator.ResolveIncompleteSuccessfulRunRetryReasons(
            candidate,
            detail,
            responseText,
            missingRequiredTools,
            carriedImplementationProof);
    }

    private static bool ShouldRetryRecoverableFailedRun(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> unresolvedCriticalToolFailures,
        int attemptNumber,
        int maxExecutionAttempts)
    {
        return ProcessRecoverableFailedRunRetryRules.ShouldRetry(
            candidate,
            detail,
            responseText,
            missingRequiredTools,
            unresolvedCriticalToolFailures,
            attemptNumber,
            maxExecutionAttempts);
    }

    private static bool TryResolveRecoverableFinalizerValidationFailure(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        out string failureSummary)
    {
        failureSummary = string.Empty;
        if (!RequiresGovernedStepOutcome(candidate.StepRun))
        {
            return false;
        }

        var candidateTexts = new[]
        {
            responseText,
            detail.ChatSession?.Messages.LastOrDefault(item => item.Role == ProcessAutomationChatMessageRole.Assistant)?.Content,
            ResolveLatestAssistantErrorSummary(detail.Run.SerializedSessionStateJson),
            ResolveLatestAssistantResponseText(detail.Run.SerializedSessionStateJson),
            detail.Run.ResultSummary
        };

        foreach (var candidateText in candidateTexts)
        {
            if (MentionsProcessStepOutcomeFinalizerMissing(candidateText))
            {
                failureSummary = $"Required finalizer tool '{AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName}' was not called.";
                return true;
            }

            if (MentionsProcessStepOutcomeFinalizerInvalid(candidateText))
            {
                failureSummary = $"Required finalizer tool '{AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName}' failed validation.";
                return true;
            }
        }

        return false;
    }

    private static bool MentionsProcessStepOutcomeFinalizerMissing(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains(ProcessStepOutcomeFinalizerMissingErrorCode, StringComparison.OrdinalIgnoreCase) ||
               (text.Contains(AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName, StringComparison.OrdinalIgnoreCase) &&
                text.Contains("was not called", StringComparison.OrdinalIgnoreCase));
    }

    private static bool MentionsProcessStepOutcomeFinalizerInvalid(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains(ProcessStepOutcomeFinalizerMultipleCallsErrorCode, StringComparison.OrdinalIgnoreCase) ||
               (text.Contains(AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName, StringComparison.OrdinalIgnoreCase) &&
                text.Contains("finalizer", StringComparison.OrdinalIgnoreCase) &&
                text.Contains("failed validation", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryResolveRecoverableExecutionInterruption(
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        out string interruptionSummary)
    {
        interruptionSummary = string.Empty;
        var run = detail.Run;
        if (run.State != ProcessAutomationExecutionState.Failed ||
            run.Outcome is not (ProcessAutomationRunOutcome.Cancelled or ProcessAutomationRunOutcome.Failed))
        {
            return false;
        }

        var candidateTexts = new[]
        {
            responseText,
            detail.ChatSession?.Messages.LastOrDefault(item => item.Role == ProcessAutomationChatMessageRole.Assistant)?.Content,
            ResolveLatestAssistantErrorSummary(run.SerializedSessionStateJson),
            ResolveLatestAssistantResponseText(run.SerializedSessionStateJson),
            run.ResultSummary
        };

        if (!candidateTexts.Any(MentionsHostRestartInterruption))
        {
            return false;
        }

        interruptionSummary = "The AgentFramework execution was interrupted by host restart before the agent completed the step.";
        return true;
    }

    private static bool MentionsHostRestartInterruption(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("host restarted", StringComparison.OrdinalIgnoreCase) ||
               (text.Contains("execution interrupted", StringComparison.OrdinalIgnoreCase) &&
                text.Contains("before the run completed", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildCompletionReason(DispatchCandidate candidate, ProcessAutomationExecutionRunDetail detail, string stepTitle)
    {
        return BuildCompletionReasonCore(
            candidate,
            detail,
            stepTitle,
            ResolveMissingRequiredToolExecutions(candidate, detail),
            detail.Run.ResultSummary);
    }

    private static string BuildCompletionReasonCore(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string stepTitle,
        IReadOnlyList<string> missingRequiredTools,
        string? responseText)
    {
        return BuildCompletionReasonCoreWithCarryForward(
            candidate,
            detail,
            stepTitle,
            missingRequiredTools,
            responseText,
            CarriedImplementationProof.None);
    }

    private static string BuildCompletionReasonCoreWithCarryForward(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string stepTitle,
        IReadOnlyList<string> missingRequiredTools,
        string? responseText,
        CarriedImplementationProof carriedImplementationProof)
    {
        var run = detail.Run;
        if (run.State == ProcessAutomationExecutionState.WaitingOnTool || run.PendingApprovals.Count > 0)
        {
            return $"AgentFramework run '{run.Title}' is waiting on approval before '{stepTitle}' can continue.";
        }

        if (run.Outcome != ProcessAutomationRunOutcome.Succeeded)
        {
            return string.IsNullOrWhiteSpace(run.ResultSummary)
                ? $"AgentFramework run '{run.Title}' failed."
                : $"AgentFramework run '{run.Title}' failed: {run.ResultSummary}";
        }

        var inspectionText = ResolveOutputInspectionText(responseText);
        var hasDeclaredOutcome = TryResolveDeclaredStepOutcome(candidate, responseText, out var declaredOutcome, out var processOutcome);
        if (hasDeclaredOutcome)
        {
            var contextValidation = ValidateProcessStepOutcomeContextWithCarryForward(
                candidate,
                detail,
                processOutcome,
                declaredOutcome,
                inspectionText,
                carriedImplementationProof);
            if (!contextValidation.IsValid)
            {
                if (TryRecoverExplicitDispositionBranchSelection(
                        candidate,
                        declaredOutcome,
                        contextValidation,
                        responseText,
                        out var explicitDisposition))
                {
                    return BuildExplicitDispositionCompletionReason(
                        explicitDisposition,
                        declaredOutcome.Reason,
                        "selected from explicit current-run disposition text");
                }

                var branchOutcomeSelectionFailure = ResolveBranchOutcomeSelectionFailure(candidate, declaredOutcome);
                if (!string.IsNullOrWhiteSpace(branchOutcomeSelectionFailure))
                {
                    return branchOutcomeSelectionFailure;
                }

                if (declaredOutcome.Status != ProcessStepRunStatus.Completed)
                {
                    return $"AgentFramework run '{run.Title}' returned an invalid governed {declaredOutcome.Status} outcome for '{stepTitle}': {string.Join("; ", contextValidation.Errors.Select(error => error.Message))}";
                }
            }
            else if (declaredOutcome.Status != ProcessStepRunStatus.Completed)
            {
                if (DeclaredBlockedOutcomeClaimsRequiredToolFailureWithoutReceipt(
                    declaredOutcome,
                    responseText,
                    missingRequiredTools,
                    detail))
                {
                    return $"AgentFramework run '{run.Title}' claimed '{stepTitle}' is blocked by a required tool failure, but no failed receipt for the required tool was recorded. Required tools: {string.Join(", ", missingRequiredTools)}.";
                }

                if (TryResolveRepairBranchCompletionFromBlockedOutcome(
                    candidate,
                    detail,
                    declaredOutcome,
                    responseText,
                    missingRequiredTools,
                    carriedImplementationProof,
                    out var repairBranchOutcome))
                {
                    return string.IsNullOrWhiteSpace(declaredOutcome.Reason)
                        ? $"AgentFramework run '{run.Title}' completed '{stepTitle}' with repair disposition '{repairBranchOutcome.Title}'."
                        : $"AgentFramework run '{run.Title}' completed '{stepTitle}' with repair disposition '{repairBranchOutcome.Title}': {declaredOutcome.Reason}";
                }

                if (TryResolveTerminalEscalationCompletionFromBlockedOutcome(
                    candidate,
                    detail,
                    declaredOutcome,
                    responseText,
                    missingRequiredTools,
                    out var escalationDispositionTitle))
                {
                    return string.IsNullOrWhiteSpace(declaredOutcome.Reason)
                        ? $"AgentFramework run '{run.Title}' completed '{stepTitle}' with escalation disposition '{escalationDispositionTitle}'."
                        : $"AgentFramework run '{run.Title}' completed '{stepTitle}' with escalation disposition '{escalationDispositionTitle}': {declaredOutcome.Reason}";
                }

                return BuildDeclaredStepOutcomeReason(run.Title, stepTitle, declaredOutcome);
            }
        }

        var unresolvedFailures = ResolveUnresolvedCriticalToolFailures(candidate, detail);
        if (unresolvedFailures.Count > 0)
        {
            var summary = string.Join(
                "; ",
                unresolvedFailures
                    .Take(2)
                    .Select(item => $"{item.ToolName}: {item.ExitSummary}"));
            return $"AgentFramework run '{run.Title}' failed because critical tool executions did not recover: {summary}";
        }

        if (TryResolveRecoverableProviderFailure(detail, responseText, out var providerFailureSummary))
        {
            return $"AgentFramework run '{run.Title}' failed because the assigned provider could not produce a usable response: {providerFailureSummary}";
        }

        var missingUpstreamArtifactInputSummary = ResolveMissingUpstreamArtifactInputSummary(candidate);
        var missingConcreteProofSummary = ResolveMissingConcreteProofSummary(candidate, inspectionText);
        var incompleteImplementationSummary = ResolveIncompleteImplementationSummary(candidate, inspectionText);
        var missingConcreteImplementationProofSummary = ResolveMissingConcreteImplementationProofSummaryWithCarryForward(
            candidate,
            detail,
            carriedImplementationProof);
        var missingRunnableApplicationProofSummary = ResolveMissingRunnableApplicationProofSummaryWithCarryForward(
            candidate,
            detail,
            carriedImplementationProof);
        var invalidBrowserProofSummary = ResolveInvalidBrowserProofSummary(candidate, detail);
        var invalidQualityValidationProofSummary = ResolveInvalidQualityValidationProofSummary(candidate, detail, inspectionText);
        var missingRequiredArtifactSummary = ResolveMissingRequiredArtifactSummary(candidate, detail, inspectionText);
        var downgradedProjectStructureRequirementSummary = ResolveDowngradedProjectStructureRequirementSummary(candidate, detail, inspectionText);
        var missingUpstreamArtifactInspectionSummary = ResolveMissingUpstreamArtifactInspectionSummary(candidate, detail);
        var outOfScopeExternalTargetReferenceSummary = ResolveOutOfScopeExternalTargetReferenceSummary(detail, inspectionText);
        var shallowSharedManagedArtifactReferenceSummary = ResolveShallowSharedManagedArtifactReferenceSummary(detail, inspectionText);
        if (hasDeclaredOutcome)
        {
            var branchOutcomeSelectionFailure = ResolveBranchOutcomeSelectionFailure(candidate, declaredOutcome);
            if (!string.IsNullOrWhiteSpace(branchOutcomeSelectionFailure))
            {
                if (TryResolveExplicitDispositionBranchOutcome(candidate, responseText, out var explicitDisposition))
                {
                    return BuildExplicitDispositionCompletionReason(
                        explicitDisposition,
                        declaredOutcome.Reason,
                        "selected from explicit current-run disposition text");
                }

                return branchOutcomeSelectionFailure;
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                HasUnrecoverableMissingRequiredTool(missingRequiredTools))
            {
                return BuildMissingRequiredToolsReason(candidate, detail, missingRequiredTools);
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(missingUpstreamArtifactInputSummary))
            {
                return $"AgentFramework run '{run.Title}' claimed '{stepTitle}' completed, but required upstream artifacts are missing: {missingUpstreamArtifactInputSummary}";
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(missingConcreteProofSummary))
            {
                return $"AgentFramework run '{run.Title}' claimed '{stepTitle}' completed, but the response still reported missing required browser proof: {missingConcreteProofSummary}";
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(incompleteImplementationSummary))
            {
                return $"AgentFramework run '{run.Title}' claimed '{stepTitle}' completed, but the response still deferred required implementation work: {incompleteImplementationSummary}";
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(missingConcreteImplementationProofSummary))
            {
                return $"AgentFramework run '{run.Title}' claimed '{stepTitle}' completed, but current-attempt implementation proof is invalid: {missingConcreteImplementationProofSummary}";
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(missingRunnableApplicationProofSummary))
            {
                return $"AgentFramework run '{run.Title}' claimed '{stepTitle}' completed, but runnable application proof is missing: {missingRunnableApplicationProofSummary}";
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(invalidBrowserProofSummary))
            {
                return $"AgentFramework run '{run.Title}' claimed '{stepTitle}' completed, but browser proof is invalid: {invalidBrowserProofSummary}";
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(invalidQualityValidationProofSummary))
            {
                return $"AgentFramework run '{run.Title}' claimed '{stepTitle}' completed, but validation proof is invalid: {invalidQualityValidationProofSummary}";
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(missingRequiredArtifactSummary))
            {
                return $"AgentFramework run '{run.Title}' claimed '{stepTitle}' completed, but required artifacts still could not be recorded automatically: {missingRequiredArtifactSummary}";
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(downgradedProjectStructureRequirementSummary))
            {
                return $"AgentFramework run '{run.Title}' claimed '{stepTitle}' completed, but generated evidence weakened project-structure scope: {downgradedProjectStructureRequirementSummary}";
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(missingUpstreamArtifactInspectionSummary))
            {
                return $"AgentFramework run '{run.Title}' claimed '{stepTitle}' completed, but inherited implementation artifacts were not directly inspected: {missingUpstreamArtifactInspectionSummary}";
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(outOfScopeExternalTargetReferenceSummary))
            {
                return $"AgentFramework run '{run.Title}' claimed '{stepTitle}' completed, but generated evidence used stale or ungrounded product paths: {outOfScopeExternalTargetReferenceSummary}";
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(shallowSharedManagedArtifactReferenceSummary))
            {
                return $"AgentFramework run '{run.Title}' claimed '{stepTitle}' completed, but generated evidence used shared managed artifact paths that can be overwritten by concurrent runs: {shallowSharedManagedArtifactReferenceSummary}";
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                missingRequiredTools.Count > 0)
            {
                return BuildMissingRequiredToolsReason(candidate, detail, missingRequiredTools);
            }

            return BuildDeclaredStepOutcomeReason(run.Title, stepTitle, declaredOutcome);
        }

        if (!string.IsNullOrWhiteSpace(missingUpstreamArtifactInputSummary))
        {
            return $"AgentFramework run '{run.Title}' could not complete '{stepTitle}' because required upstream artifacts are missing: {missingUpstreamArtifactInputSummary}";
        }

        if (!string.IsNullOrWhiteSpace(missingConcreteProofSummary))
        {
            return $"AgentFramework run '{run.Title}' could not complete '{stepTitle}' because required browser proof is still missing: {missingConcreteProofSummary}";
        }

        if (!string.IsNullOrWhiteSpace(incompleteImplementationSummary))
        {
            return $"AgentFramework run '{run.Title}' could not complete '{stepTitle}' because the response still deferred required implementation work: {incompleteImplementationSummary}";
        }

        if (!string.IsNullOrWhiteSpace(missingConcreteImplementationProofSummary))
        {
            return $"AgentFramework run '{run.Title}' could not complete '{stepTitle}' because current-attempt implementation proof is invalid: {missingConcreteImplementationProofSummary}";
        }

        if (!string.IsNullOrWhiteSpace(missingRunnableApplicationProofSummary))
        {
            return $"AgentFramework run '{run.Title}' could not complete '{stepTitle}' because runnable application proof is missing: {missingRunnableApplicationProofSummary}";
        }

        if (!string.IsNullOrWhiteSpace(invalidBrowserProofSummary))
        {
            return $"AgentFramework run '{run.Title}' could not complete '{stepTitle}' because browser proof is invalid: {invalidBrowserProofSummary}";
        }

        if (!string.IsNullOrWhiteSpace(invalidQualityValidationProofSummary))
        {
            return $"AgentFramework run '{run.Title}' could not complete '{stepTitle}' because validation proof is invalid: {invalidQualityValidationProofSummary}";
        }

        if (!string.IsNullOrWhiteSpace(missingRequiredArtifactSummary))
        {
            return $"AgentFramework run '{run.Title}' could not complete '{stepTitle}' because required artifacts still could not be recorded automatically: {missingRequiredArtifactSummary}";
        }

        if (!string.IsNullOrWhiteSpace(downgradedProjectStructureRequirementSummary))
        {
            return $"AgentFramework run '{run.Title}' could not complete '{stepTitle}' because generated evidence weakened project-structure scope: {downgradedProjectStructureRequirementSummary}";
        }

        if (!string.IsNullOrWhiteSpace(missingUpstreamArtifactInspectionSummary))
        {
            return $"AgentFramework run '{run.Title}' could not complete '{stepTitle}' because inherited implementation artifacts were not directly inspected: {missingUpstreamArtifactInspectionSummary}";
        }

        if (!string.IsNullOrWhiteSpace(outOfScopeExternalTargetReferenceSummary))
        {
            return $"AgentFramework run '{run.Title}' could not complete '{stepTitle}' because generated evidence used stale or ungrounded product paths: {outOfScopeExternalTargetReferenceSummary}";
        }

        if (!string.IsNullOrWhiteSpace(shallowSharedManagedArtifactReferenceSummary))
        {
            return $"AgentFramework run '{run.Title}' could not complete '{stepTitle}' because generated evidence used shared managed artifact paths that can be overwritten by concurrent runs: {shallowSharedManagedArtifactReferenceSummary}";
        }

        if (CanImplicitlyCompleteGovernedStep(candidate, detail, missingRequiredTools, inspectionText))
        {
            return $"AgentFramework run '{run.Title}' completed step '{stepTitle}' from successful governed evidence, and the dispatcher inferred the governed completed outcome because a structured ProcessStepOutcomeResult was omitted.";
        }

        if (RequiresGovernedStepOutcome(candidate.StepRun))
        {
            return $"AgentFramework run '{run.Title}' did not return a valid structured ProcessStepOutcomeResult for governed step '{stepTitle}'.";
        }

        if (missingRequiredTools.Count > 0)
        {
            return BuildMissingRequiredToolsReason(candidate, detail, missingRequiredTools);
        }

        return $"AgentFramework run '{run.Title}' completed successfully.";
    }

    private static string BuildMissingRequiredToolsReason(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        IReadOnlyList<string> missingRequiredTools)
    {
        var missingImplementationProofForRequiredTools = ResolveMissingConcreteImplementationProofSummary(candidate, detail);
        if (!string.IsNullOrWhiteSpace(missingImplementationProofForRequiredTools))
        {
            return $"AgentFramework run '{detail.Run.Title}' did not execute the required step tools successfully: {string.Join(", ", missingRequiredTools)}. Current-attempt implementation proof is also invalid: {missingImplementationProofForRequiredTools}";
        }

        return $"AgentFramework run '{detail.Run.Title}' did not execute the required step tools successfully: {string.Join(", ", missingRequiredTools)}";
    }

    private static bool HasValidNonCompletedDeclaredOutcome(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        string inspectionText)
    {
        if (!TryResolveDeclaredStepOutcome(candidate, responseText, out var declaredOutcome, out var processOutcome) ||
            declaredOutcome.Status == ProcessStepRunStatus.Completed)
        {
            return false;
        }

        return ValidateProcessStepOutcomeContext(
            candidate,
            detail,
            processOutcome,
            declaredOutcome,
            inspectionText).IsValid;
    }

}
