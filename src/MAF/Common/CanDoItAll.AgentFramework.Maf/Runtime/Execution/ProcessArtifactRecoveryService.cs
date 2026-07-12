using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal enum ProcessArtifactRecoveryCause
{
    ProviderStreamingTimeout,
    MissingRequiredFinalizer
}

internal static class ProcessArtifactRecoveryService
{
    private const int MaxRecoveredProcessArtifactSummaryCharacters = 1_200;
    internal static bool TryCreateProcessStepOutcomeFromPrimaryArtifact(
        AgentRuntimeContextIntent contextIntent,
        string primaryArtifactRef,
        string artifactMarkdown,
        ProcessArtifactRecoveryCause recoveryCause,
        out ProcessStepOutcomeResult outcome,
        out string failureMessage)
    {
        outcome = default!;
        failureMessage = string.Empty;

        if (!contextIntent.IsGovernedProcessStep ||
            !string.Equals(contextIntent.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(contextIntent.ProcessRunId) ||
            string.IsNullOrWhiteSpace(contextIntent.SourceId))
        {
            failureMessage = "The runtime context is not a governed process step.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(primaryArtifactRef))
        {
            failureMessage = "The primary artifact reference is required.";
            return false;
        }

        var parsedArtifactOutcome = ManagedProcessArtifactOutcomeReader.Read(artifactMarkdown);
        if (!parsedArtifactOutcome.IsValid)
        {
            failureMessage = parsedArtifactOutcome.FailureMessage!;
            return false;
        }

        var statusWasDeclared = parsedArtifactOutcome.HasStatus;
        var status = parsedArtifactOutcome.Status ?? default;
        if (!statusWasDeclared &&
            !TryInferProcessArtifactStatus(artifactMarkdown, out status))
        {
            failureMessage = "The primary process artifact is empty or does not contain recoverable process outcome evidence.";
            return false;
        }

        if (statusWasDeclared &&
            status == ProcessStepOutcomeStatus.Blocked &&
            IsStatusOnlyRecoveredBlockedArtifact(artifactMarkdown))
        {
            failureMessage = "The primary process artifact declares Blocked without concrete blocker evidence.";
            return false;
        }

        var recoveryClause = ResolveRecoveryClause(recoveryCause);
        var reason =
            statusWasDeclared
                ? $"Recovered governed process step outcome from primary managed artifact '{primaryArtifactRef}' {recoveryClause}. The artifact declares status '{status}'."
                : $"Recovered governed process step outcome from primary managed artifact '{primaryArtifactRef}' {recoveryClause}. The artifact did not declare a Status line, so the runtime inferred status '{status}' from the artifact text.";
        outcome = new ProcessStepOutcomeResult
        {
            Status = status,
            Reason = reason,
            BranchOutcomeKey = parsedArtifactOutcome.BranchOutcomeKey,
            EvidenceRefs = [primaryArtifactRef],
            NextActions = CreateRecoveredProcessArtifactNextActions(status, primaryArtifactRef),
            HumanReadableSummaryMarkdown = BuildRecoveredProcessArtifactSummary(
                primaryArtifactRef,
                artifactMarkdown,
                recoveryCause)
        };
        return true;
    }

    internal static string DescribeRecoveryCause(ProcessArtifactRecoveryCause recoveryCause)
        => recoveryCause switch
        {
            ProcessArtifactRecoveryCause.ProviderStreamingTimeout =>
                "Provider streaming timed out after the current process step primary artifact was written.",
            ProcessArtifactRecoveryCause.MissingRequiredFinalizer =>
                "The provider completed without the required finalizer after the current process step primary artifact was written.",
            _ => throw new ArgumentOutOfRangeException(nameof(recoveryCause), recoveryCause, "Unsupported process artifact recovery cause.")
        };

    internal static bool TryBuildCurrentStepPrimaryManagedArtifactPath(
        AgentRuntimeContextIntent contextIntent,
        out string primaryArtifactRef,
        out string failureMessage)
    {
        primaryArtifactRef = string.Empty;
        failureMessage = string.Empty;

        if (!Guid.TryParse(contextIntent.ProcessRunId, out var processRunId))
        {
            failureMessage = "The process run id is not a GUID.";
            return false;
        }

        var sourceId = contextIntent.SourceId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sourceId) ||
            sourceId.Contains('/') ||
            sourceId.Contains('\\') ||
            sourceId.Contains("..", StringComparison.Ordinal))
        {
            failureMessage = "The process step source id is not a safe artifact file name.";
            return false;
        }

        primaryArtifactRef = WorkspaceScopeDescriptor.NormalizeRelativePath(
            $"artifacts/process-runs/{processRunId:D}/steps/{sourceId}.md");
        return true;
    }

    private static bool TryInferProcessArtifactStatus(
        string artifactMarkdown,
        out ProcessStepOutcomeStatus status)
    {
        status = default;
        var text = artifactMarkdown.ReplaceLineEndings(" ").Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (ContainsAny(
            text,
            "waiting approval",
            "approval required",
            "pending approval",
            "human approval"))
        {
            status = ProcessStepOutcomeStatus.WaitingApproval;
            return true;
        }

        if (ContainsAny(
            text,
            "blocked",
            "cannot proceed",
            "unable to proceed",
            "missing required",
            "requires manager",
            "manager action required",
            "policydenied",
            "permission denied",
            "access denied",
            "not authorized"))
        {
            status = ProcessStepOutcomeStatus.Blocked;
            return true;
        }

        if (ContainsAny(
            text,
            "unrecoverable failure",
            "unrecoverable error",
            "execution failed",
            "validation failed",
            "failed to complete"))
        {
            status = ProcessStepOutcomeStatus.Failed;
            return true;
        }

        status = ProcessStepOutcomeStatus.Completed;
        return true;
    }

    private static bool ContainsAny(string text, params string[] values)
        => values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static bool IsStatusOnlyRecoveredBlockedArtifact(string artifactMarkdown)
    {
        var normalized = artifactMarkdown.Trim();
        if (normalized.Length > 700)
        {
            return false;
        }

        return !ContainsAny(
            normalized,
            "PolicyDenied",
            "denied",
            "failed",
            "failure",
            "exception",
            "error",
            "cannot proceed",
            "unable to proceed",
            "missing",
            "required tool",
            "unavailable",
            "approval",
            "dependency",
            "environment",
            "boundary",
            "evidence",
            "receipt");
    }

    private static IReadOnlyList<string> CreateRecoveredProcessArtifactNextActions(
        ProcessStepOutcomeStatus status,
        string primaryArtifactRef)
    {
        if (status == ProcessStepOutcomeStatus.Completed)
        {
            return [];
        }

        return
        [
            $"Review '{primaryArtifactRef}' and re-dispatch or rework the governed process step with the recorded evidence."
        ];
    }

    private static string BuildRecoveredProcessArtifactSummary(
        string primaryArtifactRef,
        string artifactMarkdown,
        ProcessArtifactRecoveryCause recoveryCause)
    {
        var recoveryClause = ResolveRecoveryClause(recoveryCause);
        var recoveryLabel = ResolveRecoveryLabel(recoveryCause);
        var trimmed = string.IsNullOrWhiteSpace(artifactMarkdown)
            ? string.Empty
            : artifactMarkdown.Trim();
        if (trimmed.Length > MaxRecoveredProcessArtifactSummaryCharacters)
        {
            trimmed = trimmed[..MaxRecoveredProcessArtifactSummaryCharacters] + Environment.NewLine + $"[... artifact summary truncated during {recoveryLabel} ...]";
        }

        return string.IsNullOrWhiteSpace(trimmed)
            ? $"Recovered outcome from primary process artifact `{primaryArtifactRef}` {recoveryClause}."
            : $"Recovered outcome from primary process artifact `{primaryArtifactRef}` {recoveryClause}.{Environment.NewLine}{Environment.NewLine}{trimmed}";
    }

    private static string ResolveRecoveryClause(ProcessArtifactRecoveryCause recoveryCause)
        => recoveryCause switch
        {
            ProcessArtifactRecoveryCause.ProviderStreamingTimeout => "after provider streaming timed out",
            ProcessArtifactRecoveryCause.MissingRequiredFinalizer => "after provider completion omitted the required finalizer",
            _ => throw new ArgumentOutOfRangeException(nameof(recoveryCause), recoveryCause, "Unsupported process artifact recovery cause.")
        };

    private static string ResolveRecoveryLabel(ProcessArtifactRecoveryCause recoveryCause)
        => recoveryCause switch
        {
            ProcessArtifactRecoveryCause.ProviderStreamingTimeout => "provider-streaming-timeout recovery",
            ProcessArtifactRecoveryCause.MissingRequiredFinalizer => "missing-required-finalizer recovery",
            _ => throw new ArgumentOutOfRangeException(nameof(recoveryCause), recoveryCause, "Unsupported process artifact recovery cause.")
        };
}
