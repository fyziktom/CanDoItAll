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
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    internal static bool HasBlockingAutomationExecutionRun(IReadOnlyList<ExecutionRunRecord> executionRuns)
        => HasBlockingAutomationExecutionRun(executionRuns, DateTimeOffset.UtcNow);

    internal static bool HasBlockingAutomationExecutionRun(
        IReadOnlyList<ExecutionRunRecord> executionRuns,
        DateTimeOffset now)
    {
        return ResolveBlockingAutomationExecutionRunId(executionRuns, now).HasValue;
    }

    internal static Guid? ResolveBlockingAutomationExecutionRunId(
        IReadOnlyList<ExecutionRunRecord> executionRuns)
        => ResolveBlockingAutomationExecutionRunId(executionRuns, DateTimeOffset.UtcNow);

    internal static Guid? ResolveBlockingAutomationExecutionRunId(
        IReadOnlyList<ExecutionRunRecord> executionRuns,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(executionRuns);

        return executionRuns
            .Where(executionRun => IsBlockingAutomationExecutionRun(executionRun, now))
            .OrderByDescending(executionRun => executionRun.UpdatedAtUtc == default
                ? executionRun.CreatedAtUtc
                : executionRun.UpdatedAtUtc)
            .ThenByDescending(executionRun => executionRun.CreatedAtUtc)
            .Select(executionRun => (Guid?)executionRun.Id)
            .FirstOrDefault();
    }

    internal static Guid? ResolveRecoverableAutomationExecutionRunId(
        ProcessStepRun stepRun,
        IReadOnlyList<ExecutionRunRecord> executionRuns)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        if (stepRun.Status != ProcessStepRunStatus.InProgress)
        {
            return null;
        }

        return executionRuns
            .Where(executionRun =>
                string.Equals(executionRun.RequestedBy, AutomationActor, StringComparison.OrdinalIgnoreCase) &&
                executionRun.State is ExecutionState.Completed or ExecutionState.Failed &&
                IsRecoverableExecutionRunForCurrentAttempt(executionRun, stepRun.StartedAtUtc))
            .OrderByDescending(executionRun => executionRun.CompletedAtUtc ?? executionRun.UpdatedAtUtc)
            .ThenByDescending(executionRun => executionRun.UpdatedAtUtc)
            .ThenByDescending(executionRun => executionRun.CreatedAtUtc)
            .Select(executionRun => (Guid?)executionRun.Id)
            .FirstOrDefault();
    }

    internal static Guid? ResolveReusableAutomationChatSessionId(
        IReadOnlyList<ExecutionRunRecord> executionRuns)
    {
        ArgumentNullException.ThrowIfNull(executionRuns);

        return executionRuns
            .Where(executionRun =>
                string.Equals(executionRun.RequestedBy, AutomationActor, StringComparison.OrdinalIgnoreCase) &&
                executionRun.ChatSessionId.HasValue &&
                executionRun.State is ExecutionState.Completed or ExecutionState.Failed)
            .OrderByDescending(executionRun => executionRun.UpdatedAtUtc)
            .ThenByDescending(executionRun => executionRun.CreatedAtUtc)
            .Select(executionRun => executionRun.ChatSessionId)
            .FirstOrDefault();
    }

    private async Task<ConcurrentAutomationExecution?> TryAdoptConcurrentAutomationExecutionAsync(
        DispatchCandidate candidate,
        CancellationToken cancellationToken)
    {
        var executionRuns = await workspaceService.ListExecutionRunsAsync(
            new ExecutionRunQuery(
                ProcessRunId: candidate.Run.Id.ToString("D"),
                ProcessStepId: candidate.StepRun.Id.ToString("D"),
                Take: 20),
            cancellationToken);
        var blockingExecutionRunId = ResolveBlockingAutomationExecutionRunId(executionRuns, clock.GetUtcNow());
        if (!blockingExecutionRunId.HasValue)
        {
            return null;
        }

        var detail = await workspaceService.GetExecutionRunDetailAsync(blockingExecutionRunId.Value, cancellationToken);
        return new ConcurrentAutomationExecution(
            blockingExecutionRunId.Value,
            detail,
            ResolveRecoveredExecutionResponseText(detail));
    }

    private async Task<ExecutionRunRecord?> ResolveCompetingActiveAutomationExecutionAsync(
        DispatchCandidate candidate,
        DispatchExecutionOutcome executionOutcome,
        CancellationToken cancellationToken)
    {
        var executionRuns = await workspaceService.ListExecutionRunsAsync(
            new ExecutionRunQuery(
                ProcessRunId: candidate.Run.Id.ToString("D"),
                ProcessStepId: candidate.StepRun.Id.ToString("D"),
                Take: 20),
            cancellationToken);
        var now = clock.GetUtcNow();
        return executionRuns
            .Where(executionRun => executionRun.Id != executionOutcome.Detail.Run.Id)
            .Where(executionRun => IsBlockingAutomationExecutionRun(executionRun, now))
            .OrderByDescending(executionRun => executionRun.UpdatedAtUtc == default
                ? executionRun.CreatedAtUtc
                : executionRun.UpdatedAtUtc)
            .ThenByDescending(executionRun => executionRun.CreatedAtUtc)
            .FirstOrDefault();
    }

    internal static bool ShouldSkipAutomationCompletionTransition(
        ProcessStepRunStatus currentStatus,
        ProcessStepRunStatus requestedStatus)
    {
        if (currentStatus == requestedStatus)
        {
            return true;
        }

        return currentStatus is not ProcessStepRunStatus.InProgress and not ProcessStepRunStatus.WaitingApproval;
    }

    internal static bool IsConcurrentAutomationSessionBusyException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception is InvalidOperationException &&
               ConcurrentAutomationSessionBusyMessages.Contains(exception.Message.Trim());
    }

    internal static bool ShouldSkipFreshAutomationDispatch(
        ProcessStepRunStatus currentStatus,
        Guid? recoverableExecutionRunId,
        DateTimeOffset? currentAttemptStartedAtUtc,
        DateTimeOffset now,
        string trigger)
    {
        if (currentStatus != ProcessStepRunStatus.InProgress)
        {
            return false;
        }

        if (!IsRecoveryTrigger(trigger))
        {
            return false;
        }

        if (recoverableExecutionRunId.HasValue)
        {
            return false;
        }

        if (!currentAttemptStartedAtUtc.HasValue)
        {
            return false;
        }

        return now - currentAttemptStartedAtUtc.Value < FreshInProgressRecoveryGracePeriod;
    }

    private static bool IsBlockingAutomationExecutionRun(
        ExecutionRunRecord executionRun,
        DateTimeOffset now)
    {
        return string.Equals(executionRun.RequestedBy, AutomationActor, StringComparison.OrdinalIgnoreCase)
               && executionRun.State is not ExecutionState.Completed
               and not ExecutionState.Failed
               && !IsStaleAutomationExecutionRun(executionRun, now);
    }

    private static bool IsStaleAutomationExecutionRun(
        ExecutionRunRecord executionRun,
        DateTimeOffset now)
    {
        if (executionRun.PendingApprovals.Count > 0)
        {
            return false;
        }

        var lastProgressAtUtc = executionRun.UpdatedAtUtc == default
            ? executionRun.CreatedAtUtc
            : executionRun.UpdatedAtUtc;
        return now - lastProgressAtUtc >= StaleAutomationExecutionRunTimeout;
    }

    private static bool IsRecoveryTrigger(string trigger)
    {
        return string.Equals(
            trigger?.Trim(),
            "runtime-recovery-scan",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecoverableExecutionRunForCurrentAttempt(
        ExecutionRunRecord executionRun,
        DateTimeOffset? currentAttemptStartedAtUtc)
    {
        if (!currentAttemptStartedAtUtc.HasValue)
        {
            return true;
        }

        var executionAttemptStartedAtUtc = executionRun.StartedAtUtc ?? executionRun.CreatedAtUtc;
        return executionAttemptStartedAtUtc >= currentAttemptStartedAtUtc.Value;
    }

    private static string ResolveRecoveredExecutionResponseText(ExecutionRunDetail detail)
    {
        var assistantMessage = detail.ChatSession?.Messages.LastOrDefault(item => item.Role == ChatMessageRole.Assistant);
        if (!string.IsNullOrWhiteSpace(assistantMessage?.Content))
        {
            return assistantMessage.Content;
        }

        var serializedResponseText = ResolveLatestAssistantResponseText(detail.Run.SerializedSessionStateJson);
        return string.IsNullOrWhiteSpace(serializedResponseText)
            ? detail.Run.ResultSummary
            : serializedResponseText;
    }

    private static string ResolvePreferredExecutionResponseText(
        DispatchCandidate candidate,
        string? responseText,
        ExecutionRunDetail detail)
    {
        var primaryResponse = string.IsNullOrWhiteSpace(responseText)
            ? string.Empty
            : responseText.Trim();
        var recoveredResponse = ResolveRecoveredExecutionResponseText(detail).Trim();
        if (string.IsNullOrWhiteSpace(primaryResponse))
        {
            return recoveredResponse;
        }

        if (!RequiresGovernedStepOutcome(candidate.StepRun))
        {
            return primaryResponse;
        }

        var primaryHasDeclaredOutcome = TryResolveDeclaredStepOutcome(primaryResponse, out _);
        var recoveredHasDeclaredOutcome = TryResolveDeclaredStepOutcome(recoveredResponse, out _);
        return !primaryHasDeclaredOutcome && recoveredHasDeclaredOutcome
            ? recoveredResponse
            : primaryResponse;
    }

    private static bool TryResolveRecoverableProviderFailure(
        ExecutionRunDetail detail,
        string? responseText,
        out string failureSummary)
    {
        failureSummary = string.Empty;
        if (detail.Run.State == ExecutionState.Completed &&
            detail.Run.Outcome == RunOutcome.Succeeded &&
            TryReadProcessStepOutcome(responseText, out _, out _))
        {
            return false;
        }

        var candidateTexts = new[]
        {
            responseText,
            detail.ChatSession?.Messages.LastOrDefault(item => item.Role == ChatMessageRole.Assistant)?.Content,
            ResolveLatestAssistantErrorSummary(detail.Run.SerializedSessionStateJson),
            ResolveLatestAssistantResponseText(detail.Run.SerializedSessionStateJson),
            detail.Run.ResultSummary
        };

        foreach (var candidateText in candidateTexts)
        {
            if (TryMapRecoverableProviderFailureSummary(candidateText, out failureSummary))
            {
                return true;
            }
        }

        return false;
    }

    private static ProcessStepRunStatus ResolveCompletionStatus(DispatchCandidate candidate, ExecutionRunDetail detail)
    {
        return ResolveCompletionStatusWithCarryForward(candidate, detail, [], detail.Run.ResultSummary);
    }

    private static bool ShouldRetryIncompleteSuccessfulRun(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        int attemptNumber,
        int maxExecutionAttempts)
    {
        var run = detail.Run;
        var inspectionText = ResolveOutputInspectionText(responseText);
        if (HasValidNonCompletedDeclaredOutcome(candidate, detail, responseText, inspectionText))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(ResolveMissingUpstreamArtifactInputSummary(candidate)) &&
            TryResolveDeclaredStepOutcome(candidate, responseText, out var declaredOutcome) &&
            declaredOutcome.Status == ProcessStepRunStatus.Blocked)
        {
            return false;
        }

        return attemptNumber < maxExecutionAttempts
               && run.State == ExecutionState.Completed
               && run.PendingApprovals.Count == 0
               && run.Outcome == RunOutcome.Succeeded
                && ResolveIncompleteSuccessfulRunRetryReasons(candidate, detail, responseText, missingRequiredTools).Count > 0;
    }

    private static IReadOnlyList<string> ResolveIncompleteSuccessfulRunRetryReasons(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools)
    {
        var reasons = new List<string>();
        if (missingRequiredTools.Count > 0)
        {
            reasons.Add($"missing required tools: {string.Join(", ", missingRequiredTools)}");
        }

        var unresolvedCriticalToolFailures = ResolveUnresolvedCriticalToolFailures(detail);
        if (unresolvedCriticalToolFailures.Count > 0)
        {
            reasons.Add(
                "unresolved critical tool failures: " +
                string.Join(
                    "; ",
                    unresolvedCriticalToolFailures
                        .Take(2)
                        .Select(item => $"{item.ToolName}: {item.ExitSummary}")));
        }

        if (IsRecoverableImplementationPunt(candidate, responseText))
        {
            reasons.Add("recoverable implementation punt");
        }

        var inspectionText = ResolveOutputInspectionText(responseText);
        AddRetryReason(reasons, "incomplete implementation", ResolveIncompleteImplementationSummary(candidate, inspectionText));
        AddRetryReason(reasons, "missing concrete proof", ResolveMissingConcreteProofSummary(candidate, inspectionText));
        AddRetryReason(reasons, "missing concrete implementation proof", ResolveMissingConcreteImplementationProofSummary(candidate, detail));
        AddRetryReason(reasons, "missing runnable application proof", ResolveMissingRunnableApplicationProofSummary(candidate, detail));
        AddRetryReason(reasons, "invalid browser proof", ResolveInvalidBrowserProofSummary(candidate, detail));
        AddRetryReason(reasons, "missing required artifact", ResolveMissingRequiredArtifactSummary(candidate, detail, inspectionText));
        AddRetryReason(reasons, "missing upstream artifact inspection", ResolveMissingUpstreamArtifactInspectionSummary(candidate, detail));
        AddRetryReason(reasons, "stale or ungrounded product path reference", ResolveOutOfScopeExternalTargetReferenceSummary(detail, inspectionText));
        AddRetryReason(reasons, "shared managed artifact collision risk", ResolveShallowSharedManagedArtifactReferenceSummary(detail, inspectionText));

        if (IsRecoverableGovernedOutcomeGap(candidate, responseText) &&
            !CanImplicitlyCompleteGovernedStep(candidate, detail, missingRequiredTools, inspectionText))
        {
            reasons.Add("recoverable governed outcome gap");
        }

        if (TryResolveRecoverableProviderFailure(detail, responseText, out var providerFailureSummary))
        {
            reasons.Add($"recoverable provider failure: {providerFailureSummary}");
        }

        if (TryResolveRecoverableExecutionInterruption(detail, responseText, out var interruptionSummary))
        {
            reasons.Add($"recoverable execution interruption: {interruptionSummary}");
        }

        if (detail.Run.State == ExecutionState.Failed &&
            (MentionsRepeatedToolInvocation(responseText) || MentionsRepeatedToolInvocation(detail.Run.ResultSummary)))
        {
            reasons.Add("recoverable repeated tool invocation");
        }

        return reasons;
    }

    private static void AddRetryReason(List<string> reasons, string label, string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return;
        }

        reasons.Add($"{label}: {summary}");
    }

    private static bool ShouldRetryRecoverableFailedRun(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures,
        int attemptNumber,
        int maxExecutionAttempts)
    {
        var run = detail.Run;
        var recoverableGovernedOutcomeGap = IsRecoverableGovernedOutcomeGap(candidate, responseText);
        var recoverableProviderFailure = TryResolveRecoverableProviderFailure(detail, responseText, out _);
        var recoverableExecutionInterruption = TryResolveRecoverableExecutionInterruption(detail, responseText, out _);
        var recoverableRepeatedToolInvocation =
            MentionsRepeatedToolInvocation(responseText) ||
            MentionsRepeatedToolInvocation(run.ResultSummary);
        if (attemptNumber >= maxExecutionAttempts ||
            run.State != ExecutionState.Failed ||
            run.PendingApprovals.Count > 0)
        {
            return false;
        }

        if (!RequiresConcreteImplementationProof(candidate) &&
            !RequiresConcreteBrowserProof(candidate) &&
            !recoverableProviderFailure &&
            !recoverableExecutionInterruption &&
            !recoverableRepeatedToolInvocation)
        {
            return false;
        }

        if (HasValidNonCompletedDeclaredOutcome(
                candidate,
                detail,
                responseText,
                ResolveOutputInspectionText(responseText)))
        {
            return false;
        }

        return missingRequiredTools.Count > 0 ||
               unresolvedCriticalToolFailures.Count > 0 ||
               recoverableGovernedOutcomeGap ||
               recoverableProviderFailure ||
               recoverableExecutionInterruption ||
               recoverableRepeatedToolInvocation;
    }

    private static bool TryResolveRecoverableExecutionInterruption(
        ExecutionRunDetail detail,
        string? responseText,
        out string interruptionSummary)
    {
        interruptionSummary = string.Empty;
        var run = detail.Run;
        if (run.State != ExecutionState.Failed || run.Outcome != RunOutcome.Cancelled)
        {
            return false;
        }

        var candidateTexts = new[]
        {
            responseText,
            detail.ChatSession?.Messages.LastOrDefault(item => item.Role == ChatMessageRole.Assistant)?.Content,
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

    private static string BuildCompletionReason(DispatchCandidate candidate, ExecutionRunDetail detail, string stepTitle)
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
        ExecutionRunDetail detail,
        string stepTitle,
        IReadOnlyList<string> missingRequiredTools,
        string? responseText)
    {
        var run = detail.Run;
        if (run.State == ExecutionState.WaitingOnTool || run.PendingApprovals.Count > 0)
        {
            return $"AgentFramework run '{run.Title}' is waiting on approval before '{stepTitle}' can continue.";
        }

        if (run.Outcome != RunOutcome.Succeeded)
        {
            return string.IsNullOrWhiteSpace(run.ResultSummary)
                ? $"AgentFramework run '{run.Title}' failed."
                : $"AgentFramework run '{run.Title}' failed: {run.ResultSummary}";
        }

        var inspectionText = ResolveOutputInspectionText(responseText);
        var hasDeclaredOutcome = TryResolveDeclaredStepOutcome(candidate, responseText, out var declaredOutcome, out var processOutcome);
        if (hasDeclaredOutcome)
        {
            var contextValidation = ValidateProcessStepOutcomeContext(
                candidate,
                detail,
                processOutcome,
                declaredOutcome,
                inspectionText);
            if (!contextValidation.IsValid)
            {
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
                return BuildDeclaredStepOutcomeReason(run.Title, stepTitle, declaredOutcome);
            }
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

        if (TryResolveRecoverableProviderFailure(detail, responseText, out var providerFailureSummary))
        {
            return $"AgentFramework run '{run.Title}' failed because the assigned provider could not produce a usable response: {providerFailureSummary}";
        }

        if (missingRequiredTools.Count > 0)
        {
            var missingImplementationProofForRequiredTools = ResolveMissingConcreteImplementationProofSummary(candidate, detail);
            if (!string.IsNullOrWhiteSpace(missingImplementationProofForRequiredTools))
            {
                return $"AgentFramework run '{run.Title}' did not execute the required step tools successfully: {string.Join(", ", missingRequiredTools)}. Current-attempt implementation proof is also invalid: {missingImplementationProofForRequiredTools}";
            }

            return $"AgentFramework run '{run.Title}' did not execute the required step tools successfully: {string.Join(", ", missingRequiredTools)}";
        }

        var missingUpstreamArtifactInputSummary = ResolveMissingUpstreamArtifactInputSummary(candidate);
        var missingConcreteProofSummary = ResolveMissingConcreteProofSummary(candidate, inspectionText);
        var incompleteImplementationSummary = ResolveIncompleteImplementationSummary(candidate, inspectionText);
        var missingConcreteImplementationProofSummary = ResolveMissingConcreteImplementationProofSummary(candidate, detail);
        var missingRunnableApplicationProofSummary = ResolveMissingRunnableApplicationProofSummary(candidate, detail);
        var invalidBrowserProofSummary = ResolveInvalidBrowserProofSummary(candidate, detail);
        var missingRequiredArtifactSummary = ResolveMissingRequiredArtifactSummary(candidate, detail, inspectionText);
        var missingUpstreamArtifactInspectionSummary = ResolveMissingUpstreamArtifactInspectionSummary(candidate, detail);
        var outOfScopeExternalTargetReferenceSummary = ResolveOutOfScopeExternalTargetReferenceSummary(detail, inspectionText);
        var shallowSharedManagedArtifactReferenceSummary = ResolveShallowSharedManagedArtifactReferenceSummary(detail, inspectionText);
        if (hasDeclaredOutcome)
        {
            var branchOutcomeSelectionFailure = ResolveBranchOutcomeSelectionFailure(candidate, declaredOutcome);
            if (!string.IsNullOrWhiteSpace(branchOutcomeSelectionFailure))
            {
                return branchOutcomeSelectionFailure;
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
                !string.IsNullOrWhiteSpace(missingRequiredArtifactSummary))
            {
                return $"AgentFramework run '{run.Title}' claimed '{stepTitle}' completed, but required artifacts still could not be recorded automatically: {missingRequiredArtifactSummary}";
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

        if (!string.IsNullOrWhiteSpace(missingRequiredArtifactSummary))
        {
            return $"AgentFramework run '{run.Title}' could not complete '{stepTitle}' because required artifacts still could not be recorded automatically: {missingRequiredArtifactSummary}";
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

        return $"AgentFramework run '{run.Title}' completed successfully.";
    }

    private static bool HasValidNonCompletedDeclaredOutcome(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
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
