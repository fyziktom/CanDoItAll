using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

/// <summary>
/// Governed process-step outcome recovery, moved verbatim from the deleted MAF <c>ProcessArtifactRecoveryService</c>
/// (SB13): current run/step identity, exact primary managed artifact path, successful current-execution overwrite
/// verification, canonical Status parsing, concrete blocker evidence, and outcome synthesis. MAF now only offers
/// typed, runtime-neutral evidence through <see cref="IAgentExecutionOutcomeRecoveryPolicy"/>; this policy is the
/// sole owner of what counts as a governed process step and how its recovered outcome is built. It never mutates
/// the run store directly &#8212; the caller validates and persists the recovered machine output through the same
/// required-finalizer response assembly used for ordinary completion.
/// </summary>
public sealed class ProcessAgentExecutionOutcomeRecoveryPolicy : IAgentExecutionOutcomeRecoveryPolicy
{
    private const int MaxRecoveredProcessArtifactSummaryCharacters = 1_200;

    public AgentExecutionOutcomeRecoveryDecision Evaluate(AgentExecutionOutcomeRecoveryEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        if (evidence.OutputType != typeof(ProcessStepOutcomeResult))
        {
            return AgentExecutionOutcomeRecoveryDecision.NotApplicable(
                "The finalizer output type is not the governed process-step outcome contract.");
        }

        var contextIntent = evidence.ContextIntent;
        if (!contextIntent.IsGovernedProcessStep ||
            !string.Equals(contextIntent.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(contextIntent.ProcessRunId) ||
            string.IsNullOrWhiteSpace(contextIntent.SourceId))
        {
            return AgentExecutionOutcomeRecoveryDecision.NotApplicable(
                "The runtime context is not a governed process step.");
        }

        if (!TryBuildCurrentStepPrimaryManagedArtifactPath(contextIntent, out var primaryArtifactRef, out var pathFailureMessage))
        {
            return AgentExecutionOutcomeRecoveryDecision.Rejected(pathFailureMessage);
        }

        if (!evidence.ArtifactReader.TryReadCompleteTextFile(primaryArtifactRef, out var artifactMarkdown))
        {
            return AgentExecutionOutcomeRecoveryDecision.Rejected(
                $"The primary managed artifact '{primaryArtifactRef}' could not be read completely.");
        }

        if (!TryCreateProcessStepOutcomeFromPrimaryArtifact(
                contextIntent,
                primaryArtifactRef,
                artifactMarkdown,
                evidence.Cause,
                evidence.CurrentExecutionToolTraces,
                out var outcome,
                out var outcomeFailureMessage))
        {
            return AgentExecutionOutcomeRecoveryDecision.Rejected(outcomeFailureMessage);
        }

        return AgentExecutionOutcomeRecoveryDecision.Recovered(
            JsonSerializer.Serialize(outcome, AgentOutputJson.SerializerOptions),
            DescribeRecoveryCause(evidence.Cause),
            outcome.Status.ToString(),
            primaryArtifactRef,
            $"Recovered governed process step outcome '{outcome.Status}' from primary managed artifact '{primaryArtifactRef}'.");
    }

    private static bool TryCreateProcessStepOutcomeFromPrimaryArtifact(
        AgentRuntimeContextIntent contextIntent,
        string primaryArtifactRef,
        string artifactMarkdown,
        AgentExecutionOutcomeFailureCause recoveryCause,
        IReadOnlyList<AgentToolInvocationTrace> currentExecutionToolTraces,
        out ProcessStepOutcomeResult outcome,
        out string failureMessage)
    {
        ArgumentNullException.ThrowIfNull(currentExecutionToolTraces);

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

        if (!HasSuccessfulCurrentExecutionPrimaryArtifactOverwrite(
                currentExecutionToolTraces,
                primaryArtifactRef))
        {
            failureMessage =
                "The current execution did not record a successful overwrite of the exact primary process artifact.";
            return false;
        }

        var parsedArtifactOutcome = ManagedProcessArtifactOutcomeReader.Read(artifactMarkdown);
        if (!parsedArtifactOutcome.IsValid)
        {
            failureMessage = parsedArtifactOutcome.FailureMessage!;
            return false;
        }

        if (!parsedArtifactOutcome.HasStatus ||
            parsedArtifactOutcome.Status is not { } status)
        {
            failureMessage =
                "The primary process artifact does not declare the canonical Status required for recovery.";
            return false;
        }

        if (status == ProcessStepOutcomeStatus.Blocked &&
            IsStatusOnlyRecoveredBlockedArtifact(artifactMarkdown))
        {
            failureMessage = "The primary process artifact declares Blocked without concrete blocker evidence.";
            return false;
        }

        var recoveryClause = ResolveRecoveryClause(recoveryCause);
        var reason =
            $"Recovered governed process step outcome from primary managed artifact '{primaryArtifactRef}' {recoveryClause}. The artifact declares status '{status}'.";
        outcome = new ProcessStepOutcomeResult
        {
            Status = status,
            Reason = reason,
            BranchOutcomeKey = string.Empty,
            EvidenceRefs = [primaryArtifactRef],
            NextActions = CreateRecoveredProcessArtifactNextActions(status, primaryArtifactRef),
            HumanReadableSummaryMarkdown = BuildRecoveredProcessArtifactSummary(
                primaryArtifactRef,
                artifactMarkdown,
                recoveryCause)
        };
        return true;
    }

    private static string DescribeRecoveryCause(AgentExecutionOutcomeFailureCause recoveryCause)
        => recoveryCause switch
        {
            AgentExecutionOutcomeFailureCause.ProviderStreamingTimeout =>
                "Provider streaming timed out after the current process step primary artifact was written.",
            AgentExecutionOutcomeFailureCause.MissingRequiredFinalizer =>
                "The provider completed without the required finalizer after the current process step primary artifact was written.",
            _ => throw new ArgumentOutOfRangeException(nameof(recoveryCause), recoveryCause, "Unsupported process artifact recovery cause.")
        };

    private static bool TryBuildCurrentStepPrimaryManagedArtifactPath(
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

    private static bool HasSuccessfulCurrentExecutionPrimaryArtifactOverwrite(
        IReadOnlyList<AgentToolInvocationTrace> currentExecutionToolTraces,
        string primaryArtifactRef)
    {
        return currentExecutionToolTraces.Any(trace =>
            trace.Succeeded &&
            trace.CompletedAtUtc is not null &&
            trace.ToolName == ToolContractCatalog.WorkspaceWriteFile &&
            string.Equals(
                NormalizeManagedPath(trace.TargetPath),
                NormalizeManagedPath(primaryArtifactRef),
                StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeManagedPath(string value)
    {
        var normalized = value
            .Replace('\\', '/')
            .Trim()
            .Trim('`', '"', '\'')
            .TrimEnd('.', ',', ';', ':', ')', ']', '}');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized.TrimStart('/');
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
        AgentExecutionOutcomeFailureCause recoveryCause)
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

    private static string ResolveRecoveryClause(AgentExecutionOutcomeFailureCause recoveryCause)
        => recoveryCause switch
        {
            AgentExecutionOutcomeFailureCause.ProviderStreamingTimeout => "after provider streaming timed out",
            AgentExecutionOutcomeFailureCause.MissingRequiredFinalizer => "after provider completion omitted the required finalizer",
            _ => throw new ArgumentOutOfRangeException(nameof(recoveryCause), recoveryCause, "Unsupported process artifact recovery cause.")
        };

    private static string ResolveRecoveryLabel(AgentExecutionOutcomeFailureCause recoveryCause)
        => recoveryCause switch
        {
            AgentExecutionOutcomeFailureCause.ProviderStreamingTimeout => "provider-streaming-timeout recovery",
            AgentExecutionOutcomeFailureCause.MissingRequiredFinalizer => "missing-required-finalizer recovery",
            _ => throw new ArgumentOutOfRangeException(nameof(recoveryCause), recoveryCause, "Unsupported process artifact recovery cause.")
        };
}
