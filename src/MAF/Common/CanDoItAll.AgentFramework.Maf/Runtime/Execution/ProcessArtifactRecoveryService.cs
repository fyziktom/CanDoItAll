using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal static class ProcessArtifactRecoveryService
{
    private const int MaxRecoveredProcessArtifactSummaryCharacters = 1_200;
    private const string ProcessArtifactBranchOutcomeKeyLineKey = "Branch outcome key";

    internal static bool TryCreateProcessStepOutcomeFromPrimaryArtifact(
        AgentRuntimeContextIntent contextIntent,
        string primaryArtifactRef,
        string artifactMarkdown,
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

        var statusWasDeclared = TryReadProcessArtifactStatus(artifactMarkdown, out var status, out var hasStatusLine);
        if (!statusWasDeclared &&
            hasStatusLine)
        {
            failureMessage = "The primary process artifact does not contain a recoverable Status line.";
            return false;
        }

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

        if (!TryReadProcessArtifactBranchOutcomeKey(
            artifactMarkdown,
            out var branchOutcomeKey,
            out var branchOutcomeFailure))
        {
            failureMessage = branchOutcomeFailure;
            return false;
        }

        var reason =
            statusWasDeclared
                ? $"Recovered governed process step outcome from primary managed artifact '{primaryArtifactRef}' after provider timeout. The artifact declares status '{status}'."
                : $"Recovered governed process step outcome from primary managed artifact '{primaryArtifactRef}' after provider timeout. The artifact did not declare a Status line, so the runtime inferred status '{status}' from the artifact text.";
        outcome = new ProcessStepOutcomeResult
        {
            Status = status,
            Reason = reason,
            BranchOutcomeKey = branchOutcomeKey,
            EvidenceRefs = [primaryArtifactRef],
            NextActions = CreateRecoveredProcessArtifactNextActions(status, primaryArtifactRef),
            HumanReadableSummaryMarkdown = BuildRecoveredProcessArtifactSummary(primaryArtifactRef, artifactMarkdown)
        };
        return true;
    }

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

    private static bool TryReadProcessArtifactStatus(
        string artifactMarkdown,
        out ProcessStepOutcomeStatus status,
        out bool hasStatusLine)
    {
        status = default;
        hasStatusLine = false;
        foreach (var rawLine in artifactMarkdown.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim().TrimStart('#', '-', '*', ' ');
            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim(' ', '*', '`');
            if (!string.Equals(key, "Status", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            hasStatusLine = true;
            return TryMapProcessArtifactStatus(line[(separatorIndex + 1)..], out status);
        }

        return false;
    }

    private static bool TryReadProcessArtifactBranchOutcomeKey(
        string artifactMarkdown,
        out string branchOutcomeKey,
        out string failureMessage)
    {
        branchOutcomeKey = string.Empty;
        failureMessage = string.Empty;
        var declaredKeys = new HashSet<string>(StringComparer.Ordinal);
        var lines = artifactMarkdown.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index < lines.Length; index++)
        {
            var line = NormalizeProcessArtifactMetadataLine(lines[index]);
            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                if (!string.Equals(line, ProcessArtifactBranchOutcomeKeyLineKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (index + 1 >= lines.Length ||
                    !TryAddProcessArtifactBranchOutcomeKey(
                        NormalizeProcessArtifactBranchOutcomeKeyValue(NormalizeProcessArtifactMetadataLine(lines[index + 1])),
                        declaredKeys,
                        out failureMessage))
                {
                    failureMessage = string.IsNullOrWhiteSpace(failureMessage)
                        ? "The primary process artifact contains an invalid Branch outcome key section."
                        : failureMessage;
                    return false;
                }

                continue;
            }

            var key = line[..separatorIndex].Trim(' ', '*', '`');
            if (!string.Equals(key, ProcessArtifactBranchOutcomeKeyLineKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = NormalizeProcessArtifactBranchOutcomeKeyValue(line[(separatorIndex + 1)..]);
            if (!TryAddProcessArtifactBranchOutcomeKey(value, declaredKeys, out failureMessage))
            {
                return false;
            }
        }

        branchOutcomeKey = declaredKeys.SingleOrDefault() ?? string.Empty;
        return true;
    }

    private static string NormalizeProcessArtifactMetadataLine(string value)
        => value.Trim().TrimStart('#', '-', '*', ' ');

    private static bool TryAddProcessArtifactBranchOutcomeKey(
        string value,
        ISet<string> declaredKeys,
        out string failureMessage)
    {
        failureMessage = string.Empty;
        if (!IsSafeProcessArtifactBranchOutcomeKey(value))
        {
            failureMessage = "The primary process artifact contains an invalid Branch outcome key line.";
            return false;
        }

        declaredKeys.Add(value);
        if (declaredKeys.Count <= 1)
        {
            return true;
        }

        failureMessage = "The primary process artifact contains multiple different Branch outcome key lines.";
        return false;
    }

    private static string NormalizeProcessArtifactBranchOutcomeKeyValue(string value)
    {
        var trimmed = value.Trim().Trim('*', '`', '.', ';');
        var commentIndex = trimmed.IndexOf('#', StringComparison.Ordinal);
        if (commentIndex >= 0)
        {
            trimmed = trimmed[..commentIndex].Trim();
        }

        return trimmed.Trim('*', '`', '.', ';');
    }

    private static bool IsSafeProcessArtifactBranchOutcomeKey(string value)
        => !string.IsNullOrWhiteSpace(value) &&
           char.IsLetterOrDigit(value[0]) &&
           value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');

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

    private static bool TryMapProcessArtifactStatus(
        string value,
        out ProcessStepOutcomeStatus status)
    {
        status = default;
        var normalized = NormalizeProcessArtifactStatusValue(value);
        status = normalized switch
        {
            "completed" or "complete" or "succeeded" or "success" => ProcessStepOutcomeStatus.Completed,
            "blocked" or "waiting" or "waitingonchild" or "waitingforchild" => ProcessStepOutcomeStatus.Blocked,
            "failed" or "failure" => ProcessStepOutcomeStatus.Failed,
            "waitingapproval" or "pendingapproval" => ProcessStepOutcomeStatus.WaitingApproval,
            "refused" or "rejected" => ProcessStepOutcomeStatus.Refused,
            _ => default
        };
        return normalized is "completed" or "complete" or "succeeded" or "success" or
            "blocked" or "waiting" or "waitingonchild" or "waitingforchild" or
            "failed" or "failure" or
            "waitingapproval" or "pendingapproval" or
            "refused" or "rejected";
    }

    private static string NormalizeProcessArtifactStatusValue(string value)
    {
        var trimmed = value.Trim().Trim('*', '`', '.', ';');
        var commentIndex = trimmed.IndexOf('#', StringComparison.Ordinal);
        if (commentIndex >= 0)
        {
            trimmed = trimmed[..commentIndex].Trim();
        }

        return new string(
            trimmed
                .Where(character => char.IsLetterOrDigit(character))
                .Select(char.ToLowerInvariant)
                .ToArray());
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
        string artifactMarkdown)
    {
        var trimmed = string.IsNullOrWhiteSpace(artifactMarkdown)
            ? string.Empty
            : artifactMarkdown.Trim();
        if (trimmed.Length > MaxRecoveredProcessArtifactSummaryCharacters)
        {
            trimmed = trimmed[..MaxRecoveredProcessArtifactSummaryCharacters] + Environment.NewLine + "[... artifact summary truncated during provider-timeout recovery ...]";
        }

        return string.IsNullOrWhiteSpace(trimmed)
            ? $"Recovered outcome from primary process artifact `{primaryArtifactRef}` after provider timeout."
            : $"Recovered outcome from primary process artifact `{primaryArtifactRef}` after provider timeout.{Environment.NewLine}{Environment.NewLine}{trimmed}";
    }
}