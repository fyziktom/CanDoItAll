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
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    internal sealed record NoProgressRetrySignal(
        string Fingerprint,
        Guid ExecutionRunId,
        string ToolSignature,
        string ArtifactValidationFingerprint,
        string MutationDelta,
        string ProofDelta);

    internal static bool HasBlockingAutomationExecutionRun(IReadOnlyList<ProcessAutomationExecutionRunRecord> executionRuns)
        => HasBlockingAutomationExecutionRun(executionRuns, DateTimeOffset.UtcNow);

    internal static bool HasBlockingAutomationExecutionRun(
        IReadOnlyList<ProcessAutomationExecutionRunRecord> executionRuns,
        DateTimeOffset now)
    {
        return ResolveBlockingAutomationExecutionRunId(executionRuns, now).HasValue;
    }

    internal static Guid? ResolveBlockingAutomationExecutionRunId(
        IReadOnlyList<ProcessAutomationExecutionRunRecord> executionRuns)
        => ResolveBlockingAutomationExecutionRunId(executionRuns, DateTimeOffset.UtcNow);

    internal static Guid? ResolveBlockingAutomationExecutionRunId(
        IReadOnlyList<ProcessAutomationExecutionRunRecord> executionRuns,
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

    internal static Guid? ResolveBlockingAutomationExecutionRunId(
        ProcessStepRun stepRun,
        IReadOnlyList<ProcessAutomationExecutionRunRecord> executionRuns,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(stepRun);
        ArgumentNullException.ThrowIfNull(executionRuns);

        return executionRuns
            .Where(executionRun =>
                IsBlockingAutomationExecutionRun(executionRun, now) &&
                IsRecoverableExecutionRunForCurrentAttempt(executionRun, stepRun.StartedAtUtc))
            .OrderByDescending(executionRun => executionRun.UpdatedAtUtc == default
                ? executionRun.CreatedAtUtc
                : executionRun.UpdatedAtUtc)
            .ThenByDescending(executionRun => executionRun.CreatedAtUtc)
            .Select(executionRun => (Guid?)executionRun.Id)
            .FirstOrDefault();
    }

    internal static Guid? ResolveRecoverableAutomationExecutionRunId(
        ProcessStepRun stepRun,
        IReadOnlyList<ProcessAutomationExecutionRunRecord> executionRuns)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        if (stepRun.Status != ProcessStepRunStatus.InProgress)
        {
            return null;
        }

        return executionRuns
            .Where(executionRun =>
                string.Equals(executionRun.RequestedBy, AutomationActor, StringComparison.OrdinalIgnoreCase) &&
                executionRun.State is ProcessAutomationExecutionState.Completed or ProcessAutomationExecutionState.Failed &&
                IsRecoverableExecutionRunForCurrentAttempt(executionRun, stepRun.StartedAtUtc))
            .OrderByDescending(executionRun => executionRun.CompletedAtUtc ?? executionRun.UpdatedAtUtc)
            .ThenByDescending(executionRun => executionRun.UpdatedAtUtc)
            .ThenByDescending(executionRun => executionRun.CreatedAtUtc)
            .Select(executionRun => (Guid?)executionRun.Id)
            .FirstOrDefault();
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
        var executionRuns = await executionClient.ListExecutionRunsAsync(
            new ProcessAutomationExecutionRunQuery(
                ProcessRunId: candidate.Run.Id.ToString("D"),
                ProcessStepId: candidate.StepRun.Id.ToString("D"),
                Take: 20),
            cancellationToken);
        var blockingExecutionRunId = ResolveBlockingAutomationExecutionRunId(candidate.StepRun, executionRuns, clock.GetUtcNow());
        if (!blockingExecutionRunId.HasValue)
        {
            return null;
        }

        var detail = await executionClient.GetExecutionRunDetailAsync(blockingExecutionRunId.Value, cancellationToken);
        for (var pollIndex = 0; pollIndex < 2 && !IsTerminalAutomationExecutionRun(detail.Run); pollIndex++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            detail = await executionClient.GetExecutionRunDetailAsync(blockingExecutionRunId.Value, cancellationToken);
        }

        return new ConcurrentAutomationExecution(
            blockingExecutionRunId.Value,
            detail,
            ResolveRecoveredExecutionResponseText(detail));
    }

    private async Task<ProcessAutomationExecutionRunRecord?> ResolveCompetingActiveAutomationExecutionAsync(
        DispatchCandidate candidate,
        DispatchExecutionOutcome executionOutcome,
        CancellationToken cancellationToken)
    {
        var executionRuns = await executionClient.ListExecutionRunsAsync(
            new ProcessAutomationExecutionRunQuery(
                ProcessRunId: candidate.Run.Id.ToString("D"),
                ProcessStepId: candidate.StepRun.Id.ToString("D"),
                Take: 20),
            cancellationToken);
        var now = clock.GetUtcNow();
        return executionRuns
            .Where(executionRun => executionRun.Id != executionOutcome.Detail.Run.Id)
            .Where(executionRun =>
                IsBlockingAutomationExecutionRun(executionRun, now) &&
                IsRecoverableExecutionRunForCurrentAttempt(executionRun, candidate.StepRun.StartedAtUtc))
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
        ProcessAutomationExecutionRunRecord executionRun,
        DateTimeOffset now)
    {
        return string.Equals(executionRun.RequestedBy, AutomationActor, StringComparison.OrdinalIgnoreCase)
               && executionRun.State is not ProcessAutomationExecutionState.Completed
               and not ProcessAutomationExecutionState.Failed
               && !IsStaleAutomationExecutionRun(executionRun, now);
    }

    private static bool IsStaleAutomationExecutionRun(
        ProcessAutomationExecutionRunRecord executionRun,
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
        ProcessAutomationExecutionRunRecord executionRun,
        DateTimeOffset? currentAttemptStartedAtUtc)
    {
        if (!currentAttemptStartedAtUtc.HasValue)
        {
            return true;
        }

        var executionAttemptStartedAtUtc = executionRun.StartedAtUtc ?? executionRun.CreatedAtUtc;
        return executionAttemptStartedAtUtc >= currentAttemptStartedAtUtc.Value;
    }

    private static string ResolveRecoveredExecutionResponseText(ProcessAutomationExecutionRunDetail detail)
    {
        var assistantMessage = detail.ChatSession?.Messages.LastOrDefault(item => item.Role == ProcessAutomationChatMessageRole.Assistant);
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
        ProcessAutomationExecutionRunDetail detail)
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
        var resultSummary = string.IsNullOrWhiteSpace(detail.Run.ResultSummary)
            ? string.Empty
            : detail.Run.ResultSummary.Trim();
        var resultSummaryHasDeclaredOutcome = TryResolveDeclaredStepOutcome(resultSummary, out _);
        var recoveredHasDeclaredOutcome = TryResolveDeclaredStepOutcome(recoveredResponse, out _);
        if (resultSummaryHasDeclaredOutcome)
        {
            return resultSummary;
        }

        return !primaryHasDeclaredOutcome && recoveredHasDeclaredOutcome
            ? recoveredResponse
            : primaryResponse;
    }

    private static bool TryResolveRecoverableProviderFailure(
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        out string failureSummary)
    {
        failureSummary = string.Empty;
        if (detail.Run.State == ProcessAutomationExecutionState.Completed &&
            detail.Run.Outcome == ProcessAutomationRunOutcome.Succeeded &&
            TryReadProcessStepOutcome(responseText, out _, out _))
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
            if (TryMapRecoverableProviderFailureSummary(candidateText, out failureSummary))
            {
                return true;
            }
        }

        return false;
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
        var run = detail.Run;
        var inspectionText = ResolveOutputInspectionText(responseText);
        if (HasValidNonCompletedDeclaredOutcome(candidate, detail, responseText, inspectionText))
        {
            return ShouldRetryRepairableImplementationBlockedOutcome(
                       candidate,
                       detail,
                       responseText,
                       missingRequiredTools,
                       attemptNumber,
                       maxExecutionAttempts) ||
                   ShouldRetryRecoverableBrowserProofBlockedOutcome(
                       candidate,
                       detail,
                       responseText,
                       missingRequiredTools,
                       attemptNumber,
                       maxExecutionAttempts);
        }

        if (!string.IsNullOrWhiteSpace(ResolveMissingUpstreamArtifactInputSummary(candidate)) &&
            TryResolveDeclaredStepOutcome(candidate, responseText, out var declaredOutcome) &&
            declaredOutcome.Status == ProcessStepRunStatus.Blocked)
        {
            return false;
        }

        if (TryResolveDeclaredStepOutcome(candidate, responseText, out declaredOutcome, out var processOutcome) &&
            CanCompleteExplicitDispositionOutcomeWithCriticalToolFailures(
                candidate,
                detail,
                declaredOutcome,
                processOutcome,
                responseText,
                missingRequiredTools,
                carriedImplementationProof))
        {
            return false;
        }

        var retryReasons = ResolveIncompleteSuccessfulRunRetryReasons(
            candidate,
            detail,
            responseText,
            missingRequiredTools,
            carriedImplementationProof);

        return attemptNumber < maxExecutionAttempts
               && run.State == ProcessAutomationExecutionState.Completed
                && run.PendingApprovals.Count == 0
                && run.Outcome == ProcessAutomationRunOutcome.Succeeded
                && retryReasons.Count > 0
                && !ShouldCompressNoProgressRetry(
                    candidate,
                    detail,
                    responseText,
                    missingRequiredTools,
                    retryReasons,
                    attemptNumber);
    }

    private static bool ShouldCompressNoProgressRetry(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<string> retryReasons,
        int attemptNumber)
    {
        if (attemptNumber <= 1)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(candidate.ManualRecoveryDirective) ||
            TryResolveRecoverableProviderFailure(detail, responseText, out _) ||
            HasNewSatisfiedCurrentAttemptEvidence(candidate, detail))
        {
            return false;
        }

        var fingerprint = TryCreateNoProgressRetryFingerprint(candidate, detail, responseText, missingRequiredTools, retryReasons, attemptNumber);
        if (!string.IsNullOrWhiteSpace(fingerprint))
        {
            return true;
        }

        return missingRequiredTools.Count > 0 ||
               retryReasons.Any(IsNoProgressRetryReason);
    }

    internal static bool HasPriorNoProgressRetrySignal(
        IEnumerable<ProcessJournalEntry> journalEntries,
        NoProgressRetrySignal signal)
    {
        ArgumentNullException.ThrowIfNull(journalEntries);
        ArgumentNullException.ThrowIfNull(signal);

        return journalEntries.Any(entry =>
            IsNoProgressRetryLedgerEvent(entry.EventType) &&
            string.Equals(entry.CorrelationId, signal.Fingerprint, StringComparison.Ordinal) &&
            TryResolveNoProgressRetryLedgerExecutionRunId(entry.ReplayContextJson, out var priorExecutionRunId) &&
            priorExecutionRunId != signal.ExecutionRunId);
    }

    private async Task<bool> HasPriorNoProgressRetrySignalAsync(
        DispatchCandidate candidate,
        NoProgressRetrySignal signal,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entries = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .Where(entry =>
                entry.ProcessRunId == candidate.Run.Id &&
                entry.StepRunId == candidate.StepRun.Id &&
                entry.CorrelationId == signal.Fingerprint &&
                (entry.EventType == ProcessRuntimeEventTypes.NoProgressRetryObserved ||
                 entry.EventType == ProcessRuntimeEventTypes.NoProgressRetryCompressed))
            .ToListAsync(cancellationToken);

        return HasPriorNoProgressRetrySignal(entries, signal);
    }

    private static bool HasNewSatisfiedCurrentAttemptEvidence(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        return detail.Artifacts.Any(artifact =>
                   candidate.ExpectedArtifacts.Any(expectation =>
                       !candidate.RecordedArtifactExpectationIds.Contains(expectation.Id) &&
                       ResolveArtifactExpectation(candidate, artifact)?.Id == expectation.Id)) ||
               detail.ToolReceipts.Any(receipt =>
                   !IsFailedToolReceipt(receipt) &&
                   ImplementationProofToolNames.Contains(NormalizeToolToken(receipt.ToolName), StringComparer.Ordinal));
    }

    private static string? TryCreateNoProgressRetryFingerprint(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<string> retryReasons,
        int attemptNumber)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(missingRequiredTools);
        ArgumentNullException.ThrowIfNull(retryReasons);

        if (attemptNumber <= 1 ||
            !string.IsNullOrWhiteSpace(candidate.ManualRecoveryDirective) ||
            TryResolveRecoverableProviderFailure(detail, responseText, out _) ||
            HasSuccessfulConcreteProductMutation(candidate, detail) ||
            HasNewSatisfiedCurrentAttemptEvidence(candidate, detail))
        {
            return null;
        }

        if (missingRequiredTools.Count == 0 &&
            !retryReasons.Any(IsNoProgressRetryReason))
        {
            return null;
        }

        return TryCreateNoProgressRetrySignal(candidate, detail, responseText, missingRequiredTools, retryReasons)?.Fingerprint;
    }

    private static NoProgressRetrySignal? TryCreateNoProgressRetrySignal(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<string> retryReasons)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(missingRequiredTools);
        ArgumentNullException.ThrowIfNull(retryReasons);

        if (!string.IsNullOrWhiteSpace(candidate.ManualRecoveryDirective) ||
            TryResolveRecoverableProviderFailure(detail, responseText, out _) ||
            HasSuccessfulConcreteProductMutation(candidate, detail) ||
            HasNewSatisfiedCurrentAttemptEvidence(candidate, detail))
        {
            return null;
        }

        if (missingRequiredTools.Count == 0 &&
            !retryReasons.Any(IsNoProgressRetryReason))
        {
            return null;
        }

        var failedToolNames = detail.ToolReceipts
            .Where(IsFailedToolReceipt)
            .Select(receipt => NormalizeToolToken(receipt.ToolName))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var artifactSignals = detail.Artifacts
            .Select(artifact => string.Join(
                ":",
                NormalizeManagedRelativePathForComparison(artifact.RelativePath),
                CollapsePromptWhitespace(artifact.DisplayName).ToLowerInvariant(),
                CollapsePromptWhitespace(artifact.ContentType).ToLowerInvariant(),
                CreateBoundedTextHash(artifact.Summary)))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var receiptSignals = detail.ToolReceipts
            .Select(receipt => string.Join(
                ":",
                NormalizeToolToken(receipt.ToolName),
                NormalizeManagedRelativePathForComparison(receipt.WorkingDirectory),
                CreateBoundedTextHash(receipt.RequestSummary),
                CreateBoundedTextHash(receipt.ExitSummary),
                IsFailedToolReceipt(receipt) ? "failed" : "succeeded"))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var unsatisfiedExpectationIds = candidate.ExpectedArtifacts
            .Where(expectation => expectation.IsRequired && !candidate.RecordedArtifactExpectationIds.Contains(expectation.Id))
            .Select(expectation => expectation.Id.ToString("D"))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var toolSignature = CreateBoundedTextHash(string.Join(
            "|",
            string.Join(",", missingRequiredTools.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
            string.Join(",", failedToolNames),
            string.Join(",", receiptSignals)));
        var artifactValidationFingerprint = CreateBoundedTextHash(string.Join(
            "|",
            string.Join(",", unsatisfiedExpectationIds),
            string.Join(",", artifactSignals)));
        var mutationDelta = ResolveNoProgressMutationDelta(candidate, detail);
        var proofDelta = ResolveNoProgressProofDelta(detail);
        var normalized = string.Join(
            "|",
            "no-progress-retry",
            candidate.Run.Id.ToString("D"),
            candidate.StepRun.Id.ToString("D"),
            toolSignature,
            artifactValidationFingerprint,
            mutationDelta,
            proofDelta,
            string.Join(",", retryReasons.Select(reason => CollapsePromptWhitespace(reason).ToLowerInvariant()).OrderBy(item => item, StringComparer.Ordinal)));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
        return new NoProgressRetrySignal(
            fingerprint,
            detail.Run.Id,
            toolSignature,
            artifactValidationFingerprint,
            mutationDelta,
            proofDelta);
    }

    private static string ResolveNoProgressMutationDelta(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        var mutationSignals = detail.ToolReceipts
            .Where(receipt => !IsFailedToolReceipt(receipt))
            .Where(receipt => IsConcreteProductMutationToolName(NormalizeToolToken(receipt.ToolName)))
            .Select(receipt => string.Join(
                ":",
                NormalizeToolToken(receipt.ToolName),
                NormalizeManagedRelativePathForComparison(receipt.WorkingDirectory),
                IsConcreteProductMutationReceipt(candidate, detail, receipt)
                    ? "concrete"
                    : "non-concrete",
                CreateBoundedTextHash(receipt.RequestSummary),
                CreateBoundedTextHash(receipt.ExitSummary)))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        return mutationSignals.Length == 0
            ? "mutation-delta:none"
            : $"mutation-delta:{CreateBoundedTextHash(string.Join("|", mutationSignals))}";
    }

    private static string ResolveNoProgressProofDelta(ProcessAutomationExecutionRunDetail detail)
    {
        var proofSignals = detail.ToolReceipts
            .Where(receipt => !IsFailedToolReceipt(receipt))
            .Where(receipt => ImplementationProofToolNames.Contains(NormalizeToolToken(receipt.ToolName), StringComparer.Ordinal))
            .Select(receipt => string.Join(
                ":",
                NormalizeToolToken(receipt.ToolName),
                NormalizeManagedRelativePathForComparison(receipt.WorkingDirectory),
                CreateBoundedTextHash(receipt.RequestSummary),
                CreateBoundedTextHash(receipt.ExitSummary)))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        return proofSignals.Length == 0
            ? "proof-delta:none"
            : $"proof-delta:{CreateBoundedTextHash(string.Join("|", proofSignals))}";
    }

    private static bool IsNoProgressRetryLedgerEvent(string eventType)
    {
        return string.Equals(eventType, ProcessRuntimeEventTypes.NoProgressRetryObserved, StringComparison.Ordinal) ||
               string.Equals(eventType, ProcessRuntimeEventTypes.NoProgressRetryCompressed, StringComparison.Ordinal);
    }

    private static bool TryResolveNoProgressRetryLedgerExecutionRunId(
        string? replayContextJson,
        out Guid executionRunId)
    {
        executionRunId = default;
        if (string.IsNullOrWhiteSpace(replayContextJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(replayContextJson);
            if (!document.RootElement.TryGetProperty(nameof(NoProgressRetrySignal.ExecutionRunId), out var executionRunIdElement))
            {
                return false;
            }

            if (executionRunIdElement.ValueKind == JsonValueKind.String &&
                Guid.TryParse(executionRunIdElement.GetString(), out executionRunId))
            {
                return true;
            }

            return executionRunIdElement.TryGetGuid(out executionRunId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsTerminalAutomationExecutionRun(ProcessAutomationExecutionRunRecord run)
    {
        return run.State is ProcessAutomationExecutionState.Completed or ProcessAutomationExecutionState.Failed;
    }

    private static string CreateBoundedTextHash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CollapsePromptWhitespace(value)))).ToLowerInvariant();
    }

    private static bool IsNoProgressRetryReason(string retryReason)
    {
        return retryReason.StartsWith("missing required tools:", StringComparison.OrdinalIgnoreCase) ||
               retryReason.StartsWith("unresolved critical tool failures:", StringComparison.OrdinalIgnoreCase) ||
               retryReason.StartsWith("missing concrete proof:", StringComparison.OrdinalIgnoreCase) ||
               retryReason.StartsWith("missing concrete implementation proof:", StringComparison.OrdinalIgnoreCase) ||
               retryReason.StartsWith("missing runnable application proof:", StringComparison.OrdinalIgnoreCase) ||
               retryReason.StartsWith("invalid browser proof:", StringComparison.OrdinalIgnoreCase) ||
               retryReason.StartsWith("invalid quality validation proof:", StringComparison.OrdinalIgnoreCase) ||
               retryReason.StartsWith("missing required artifact:", StringComparison.OrdinalIgnoreCase) ||
               retryReason.StartsWith("missing upstream artifact inspection:", StringComparison.OrdinalIgnoreCase) ||
               retryReason.StartsWith("stale or ungrounded product path reference:", StringComparison.OrdinalIgnoreCase) ||
               retryReason.StartsWith("shared managed artifact collision risk:", StringComparison.OrdinalIgnoreCase) ||
               retryReason.StartsWith("recoverable finalizer validation failure:", StringComparison.OrdinalIgnoreCase);
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
        var reasons = new List<string>();
        if (missingRequiredTools.Count > 0)
        {
            reasons.Add($"missing required tools: {string.Join(", ", missingRequiredTools)}");
        }

        var unresolvedCriticalToolFailures = ResolveUnresolvedCriticalToolFailures(candidate, detail);
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
        AddRetryReason(
            reasons,
            "missing concrete implementation proof",
            ResolveMissingConcreteImplementationProofSummaryWithCarryForward(candidate, detail, carriedImplementationProof));
        AddRetryReason(reasons, "missing runnable application proof", ResolveMissingRunnableApplicationProofSummary(candidate, detail));
        AddRetryReason(reasons, "invalid browser proof", ResolveInvalidBrowserProofSummary(candidate, detail));
        AddRetryReason(reasons, "invalid quality validation proof", ResolveInvalidQualityValidationProofSummary(candidate, detail, inspectionText));
        AddRetryReason(reasons, "missing required artifact", ResolveMissingRequiredArtifactSummary(candidate, detail, inspectionText));
        AddRetryReason(reasons, "downgraded project-structure requirement", ResolveDowngradedProjectStructureRequirementSummary(candidate, detail, inspectionText));
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

        if (TryResolveRecoverableFinalizerValidationFailure(candidate, detail, responseText, out var finalizerFailureSummary))
        {
            reasons.Add($"recoverable finalizer validation failure: {finalizerFailureSummary}");
        }

        if (TryResolveRecoverableExecutionInterruption(detail, responseText, out var interruptionSummary))
        {
            reasons.Add($"recoverable execution interruption: {interruptionSummary}");
        }

        if (detail.Run.State == ProcessAutomationExecutionState.Failed &&
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
        ProcessAutomationExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> unresolvedCriticalToolFailures,
        int attemptNumber,
        int maxExecutionAttempts)
    {
        var run = detail.Run;
        var recoverableGovernedOutcomeGap = IsRecoverableGovernedOutcomeGap(candidate, responseText);
        var recoverableProviderFailure = TryResolveRecoverableProviderFailure(detail, responseText, out _);
        var recoverableFinalizerValidationFailure = TryResolveRecoverableFinalizerValidationFailure(candidate, detail, responseText, out _);
        var recoverableExecutionInterruption = TryResolveRecoverableExecutionInterruption(detail, responseText, out _);
        var recoverableRepeatedToolInvocation =
            MentionsRepeatedToolInvocation(responseText) ||
            MentionsRepeatedToolInvocation(run.ResultSummary);
        if (attemptNumber >= maxExecutionAttempts ||
            run.State != ProcessAutomationExecutionState.Failed ||
            run.PendingApprovals.Count > 0)
        {
            return false;
        }

        if (!RequiresConcreteImplementationProof(candidate) &&
            !RequiresConcreteBrowserProof(candidate) &&
            !recoverableProviderFailure &&
            !recoverableFinalizerValidationFailure &&
            !recoverableExecutionInterruption &&
            !recoverableRepeatedToolInvocation)
        {
            return false;
        }

        if (!recoverableFinalizerValidationFailure &&
            HasValidNonCompletedDeclaredOutcome(
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
               recoverableFinalizerValidationFailure ||
               recoverableExecutionInterruption ||
               recoverableRepeatedToolInvocation;
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
