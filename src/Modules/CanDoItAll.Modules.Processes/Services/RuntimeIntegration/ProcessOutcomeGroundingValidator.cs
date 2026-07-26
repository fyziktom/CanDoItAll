using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static CanDoItAll.Modules.Processes.ProcessAgentRightsDiagnosticPolicy;
using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactEvidence;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactService;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactFormatter;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactOutcomeParser;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessOutcomeGroundingValidator(IWorkspaceFileService workspaceFiles)
{
    internal static ProcessCompletionIssue? ValidateGroundedOutcomeReferences(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        ParentSubprocessBridgedOutcome? verifiedSubprocessOutcome = null)
    {
        var verifiedForwardedContextEnvelope = ResolveVerifiedForwardedContextEnvelope(
            verifiedSubprocessOutcome,
            toolReceipts);
        var ungroundedRefs = FindUngroundedPathReferences(
            assignment,
            EnumerateOutcomePathReferences(output, verifiedForwardedContextEnvelope),
            toolReceipts);
        if (ungroundedRefs.Length == 0)
        {
            return null;
        }

        var refSummary = DescribeUngroundedReferenceSet(ungroundedRefs);
        return new ProcessCompletionIssue(
            "process.adapter.ungrounded_outcome_reference",
            $"Step '{assignment.StepKey}' claimed completion but cited {refSummary}. Those refs are not grounded in the current step brief, launch variables, required upstream refs, or current-run tool receipts. Retry the same step, remove the rejected path-like refs from the reason, summary, next actions, and evidence refs, and overwrite the managed artifact if needed. Do not quote or restate rejected literal path strings from diagnostics or earlier attempts. Keep a path-like ref only if this same retry first reads or writes current-run evidence that grounds the exact ref.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:ungrounded-outcome-reference:{ComputeHash(string.Join("|", ungroundedRefs))}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    internal ProcessCompletionIssue? ValidateManagedArtifactBodyReferences(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        ParentSubprocessBridgedOutcome? verifiedSubprocessOutcome = null)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed ||
            assignment.ProducedArtifactSlotIds.Count == 0)
        {
            return null;
        }

        var primaryRef = BuildManagedStepArtifactPath(assignment);
        var readResult = workspaceFiles.ReadTextFile(primaryRef, maxCharacters: 200000);
        if (!readResult.Succeeded || string.IsNullOrWhiteSpace(readResult.Content))
        {
            return null;
        }

        var content = readResult.Content;
        if (ProcessOutcomeCitationSanitizer.TryRemoveNonCitableSourceMetadataLines(content, out var sanitizedContent))
        {
            var writeResult = workspaceFiles.WriteTextFile(primaryRef, sanitizedContent, overwrite: true);
            if (writeResult.Succeeded)
            {
                content = sanitizedContent;
            }
        }

        var contentForGrounding = RemoveVerifiedForwardedContextEnvelope(
            content,
            ResolveVerifiedForwardedContextEnvelope(verifiedSubprocessOutcome, toolReceipts));
        var ungroundedRefs = FindUngroundedPathReferences(
            assignment,
            EnumerateTextPathReferences(contentForGrounding),
            toolReceipts,
            ReadTrustedManagedArtifactGroundingTexts(assignment));
        if (ungroundedRefs.Length == 0)
        {
            return null;
        }

        var refSummary = DescribeUngroundedReferenceSet(ungroundedRefs);
        return new ProcessCompletionIssue(
            "process.adapter.ungrounded_managed_artifact_reference",
            $"Step '{assignment.StepKey}' wrote primary managed artifact '{primaryRef}' with {refSummary}. Those refs are not grounded in the current step brief, launch variables, required upstream refs, or current-run successful tool receipts. Retry the same step, overwrite the artifact, and remove rejected path-like refs from the artifact body, reason, summary, next actions, and evidence refs. Do not quote or restate rejected literal path strings from diagnostics or earlier attempts. Keep a path-like ref only if this same retry first reads or writes current-run evidence that grounds the exact ref.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:ungrounded-managed-artifact-reference:{ComputeHash(string.Join("|", ungroundedRefs))}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static string? ResolveVerifiedForwardedContextEnvelope(
        ParentSubprocessBridgedOutcome? verifiedSubprocessOutcome,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts)
    {
        if (verifiedSubprocessOutcome is null ||
            verifiedSubprocessOutcome.ForwardedContextArtifacts.Count == 0 ||
            !HasRuntimeSubprocessBridgeReceipt(verifiedSubprocessOutcome, toolReceipts))
        {
            return null;
        }

        var envelope = ParentSubprocessForwardedContextEnvelope.Format(
            verifiedSubprocessOutcome.ForwardedContextArtifacts);
        return string.IsNullOrWhiteSpace(envelope)
            ? null
            : envelope;
    }

    private static bool HasRuntimeSubprocessBridgeReceipt(
        ParentSubprocessBridgedOutcome verifiedSubprocessOutcome,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts)
        => toolReceipts?.Any(receipt =>
            receipt.ExecutionRunId == verifiedSubprocessOutcome.SyntheticExecutionRunId &&
            string.Equals(receipt.ToolFamily, "process-runtime", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(receipt.ToolName, ProcessSubprocessState.SubprocessLaunchToolName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(receipt.RiskClass, "ProcessRuntime", StringComparison.OrdinalIgnoreCase) &&
            IsGroundingToolReceipt(receipt) &&
            receipt.ExitSummary.Contains(
                verifiedSubprocessOutcome.ChildRunId.Value.ToString("D"),
                StringComparison.OrdinalIgnoreCase)) == true;

    private static string RemoveVerifiedForwardedContextEnvelope(
        string content,
        string? verifiedForwardedContextEnvelope)
    {
        if (string.IsNullOrWhiteSpace(verifiedForwardedContextEnvelope))
        {
            return content;
        }

        return content.Replace(verifiedForwardedContextEnvelope, string.Empty, StringComparison.Ordinal);
    }

    internal static string DescribeUngroundedReferenceSet(IReadOnlyList<string> ungroundedRefs)
        => ungroundedRefs.Count == 1
            ? "1 ungrounded path-like ref"
            : $"{ungroundedRefs.Count} ungrounded path-like refs";

    internal static string[] FindUngroundedPathReferences(
        ProcessRuntimeStepAssignment assignment,
        IEnumerable<string> candidateRefs,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        IEnumerable<string>? additionalGroundingTexts = null)
    {
        var distinctRefs = candidateRefs
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (distinctRefs.Length == 0)
        {
            return [];
        }

        var groundingTexts = BuildOutcomeReferenceGroundingTexts(assignment, toolReceipts, additionalGroundingTexts);
        return distinctRefs
            .Where(candidateRef => !IsOutcomeReferenceGrounded(assignment, candidateRef, groundingTexts))
            .Take(5)
            .ToArray();
    }

    internal static IReadOnlyList<string> BuildOutcomeReferenceGroundingTexts(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        IEnumerable<string>? additionalGroundingTexts = null)
    {
        var groundingTexts = new List<string>
        {
            assignment.Prompt,
            BuildManagedStepArtifactPath(assignment),
            BuildManagedStepArtifactRoot(assignment)
        };
        groundingTexts.AddRange(assignment.LaunchVariables.SelectMany(item => new[] { item.Key, item.Value }));
        groundingTexts.AddRange(assignment.RequiredArtifactSlotIds.SelectMany(slotId => EnumerateManagedArtifactEvidenceRefs(assignment, slotId)));
        groundingTexts.AddRange(assignment.ProducedArtifactSlotIds.SelectMany(slotId => EnumerateManagedArtifactEvidenceRefs(assignment, slotId)));

        if (toolReceipts is not null)
        {
            groundingTexts.AddRange(toolReceipts
                .Where(IsGroundingToolReceipt)
                .SelectMany(receipt => new[]
                {
                    receipt.ToolName,
                    receipt.RequestSummary,
                    receipt.ExitSummary,
                    receipt.WorkingDirectory
                }));
        }

        if (additionalGroundingTexts is not null)
        {
            groundingTexts.AddRange(additionalGroundingTexts);
        }

        return groundingTexts
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeOutcomeReferenceText)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<string> ReadTrustedManagedArtifactGroundingTexts(ProcessRuntimeStepAssignment assignment)
    {
        var primaryRef = NormalizeManagedArtifactRef(BuildManagedStepArtifactPath(assignment));
        return EnumerateTrustedManagedArtifactRefs(assignment, primaryRef)
            .Select(ReadManagedArtifactGroundingText)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    private string ReadManagedArtifactGroundingText(string artifactRef)
    {
        var readResult = workspaceFiles.ReadTextFile(artifactRef, maxCharacters: 200000);
        return readResult.Succeeded && !string.IsNullOrWhiteSpace(readResult.Content)
            ? readResult.Content
            : string.Empty;
    }

    internal static IEnumerable<string> EnumerateTrustedManagedArtifactRefs(
        ProcessRuntimeStepAssignment assignment,
        string primaryRef)
    {
        var trustedTexts = new List<string?> { assignment.Prompt };
        trustedTexts.AddRange(assignment.LaunchVariables.SelectMany(item => new[] { item.Key, item.Value }));

        foreach (var trustedText in trustedTexts)
        {
            foreach (var candidateRef in EnumerateTextPathReferences(trustedText))
            {
                var normalizedRef = NormalizeManagedArtifactRef(candidateRef);
                if (IsManagedMarkdownArtifactRef(normalizedRef) &&
                    !string.Equals(normalizedRef, primaryRef, StringComparison.OrdinalIgnoreCase))
                {
                    yield return normalizedRef;
                }
            }
        }
    }

    internal static bool IsManagedMarkdownArtifactRef(string value)
        => Regex.IsMatch(
            NormalizeManagedArtifactRef(value),
            @"^artifacts/process-runs/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/steps/[^/\\]+\.md$",
            RegexOptions.CultureInvariant);

    internal static bool IsOutcomeReferenceGrounded(
        ProcessRuntimeStepAssignment assignment,
        string candidateRef,
        IReadOnlyList<string> groundingTexts)
    {
        var normalizedCandidate = NormalizeOutcomeReferenceText(candidateRef);
        if (normalizedCandidate.Length == 0)
        {
            return true;
        }

        var currentRunManagedRoot = $"process-runs/{assignment.RunId.Value:D}";
        if (normalizedCandidate.Contains(currentRunManagedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return groundingTexts.Any(groundingText =>
            groundingText.Contains(normalizedCandidate, StringComparison.OrdinalIgnoreCase) ||
            IsPathReferenceUnderGroundedRoot(normalizedCandidate, groundingText));
    }

    internal static bool IsPathReferenceUnderGroundedRoot(string normalizedCandidate, string groundingText)
    {
        var normalizedRoot = NormalizeOutcomeReferenceText(groundingText).TrimEnd('/');
        if (normalizedRoot.Length == 0 ||
            normalizedCandidate.Length <= normalizedRoot.Length ||
            !normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return normalizedCandidate[normalizedRoot.Length] == '/';
    }

    internal static bool IsGroundingToolReceipt(ToolExecutionReceiptRecord receipt)
        => receipt.ExitSummary.StartsWith("Succeeded", StringComparison.OrdinalIgnoreCase);

    internal static IEnumerable<string> EnumerateOutcomePathReferences(
        ProcessStepOutcomeResult output,
        string? verifiedForwardedContextEnvelope = null)
    {
        foreach (var text in EnumerateOutcomeNarrativeText(output))
        {
            var textForGrounding = RemoveVerifiedForwardedContextEnvelope(
                text ?? string.Empty,
                verifiedForwardedContextEnvelope);
            foreach (var candidate in EnumerateTextPathReferences(textForGrounding))
            {
                yield return candidate;
            }
        }

        foreach (var evidenceRef in output.EvidenceRefs)
        {
            foreach (var candidate in EnumerateTextPathReferences(evidenceRef))
            {
                yield return candidate;
            }
        }
    }

    internal static IEnumerable<string> EnumerateTextPathReferences(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (Match match in OutcomePathReferenceRegex().Matches(text))
        {
            var candidate = TrimOutcomeReference(match.Value);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                yield return candidate;
            }
        }
    }

    internal static IEnumerable<string?> EnumerateOutcomeNarrativeText(ProcessStepOutcomeResult output)
    {
        yield return output.Reason;
        yield return output.BranchOutcomeKey;
        yield return output.BranchOutcomeTitle;
        yield return output.HumanReadableSummaryMarkdown;

        foreach (var nextAction in output.NextActions)
        {
            yield return nextAction;
        }
    }

    internal static string TrimOutcomeReference(string value)
        => value.Trim().Trim('`', '"', '\'', '*', '.', ',', ';', ':', ')', ']', '}');

    internal static string NormalizeOutcomeReferenceText(string value)
        => TrimOutcomeReference(value)
            .Replace('\\', '/')
            .Replace("%5C", "/", StringComparison.OrdinalIgnoreCase)
            .Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);

    internal static bool LooksLikeAgentOutputContractFailure(Exception exception)
    {
        var text = exception.ToString();
        return ContainsAny(
            text,
            "submit_process_step_outcome",
            "Required finalizer tool",
            "process_step_outcome_result",
            "ProcessStepOutcomeResult",
            "process.step_outcome",
            "agent.finalizer",
            "agent.output");
    }

    internal static bool LooksLikeTransientAgentExecutionFailure(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var hasTransientMarker = ContainsAny(
            text,
            "Service request failed",
            "Status: 408",
            "Status: 429",
            "Status: 500",
            "Status: 502",
            "Status: 503",
            "Status: 504",
            "Status: 520",
            "Status: 529",
            "temporarily unavailable",
            "temporary failure",
            "transient",
            "rate limit",
            "timeout",
            "timed out",
            "connection reset",
            "connection refused",
            "transport error");
        if (!hasTransientMarker)
        {
            return false;
        }

        return !LooksLikeRightsOrToolBoundary(text) ||
               LooksLikeProviderRuntimeTransientFailure(text);
    }

    internal static bool LooksLikeProviderRuntimeTransientFailure(string text)
    {
        return ContainsAny(
            text,
            "provider detail",
            "provider runtime",
            "service request failed",
            "initialization timed out",
            "initialisation timed out",
            "runtime initialization timed out",
            "runtime initialisation timed out");
    }

    internal static string LimitDiagnosticText(string text, int maxLength = 800)
    {
        var normalized = text.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength] + "...";
    }

}
