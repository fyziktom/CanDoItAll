using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal interface IParentSubprocessForwardedContextResolver
{
    bool TryResolve(
        ProcessRunId childRunId,
        ProcessRuntimeStepAssignment childOutputAssignment,
        ProcessRuntimeStateSnapshot childState,
        IReadOnlyList<ProcessSubprocessForwardedChildContextArtifactContract> contracts,
        out IReadOnlyList<ParentSubprocessForwardedContextArtifact> forwardedArtifacts,
        out ParentSubprocessForwardedContextIssue? issue);
}

internal sealed record ParentSubprocessForwardedContextArtifact(
    string BindingKey,
    string SourceStepKey,
    string ArtifactExpectationKey,
    string PayloadSchema,
    string ChildArtifactRef,
    string Content);

internal sealed record ParentSubprocessForwardedContextIssue(
    string Code,
    string SafeSummary,
    string Evidence);

internal sealed class ParentSubprocessForwardedContextResolver(
    IWorkspaceFileService workspaceFiles) : IParentSubprocessForwardedContextResolver
{
    private const int MaxForwardedContextArtifacts = 4;

    public bool TryResolve(
        ProcessRunId childRunId,
        ProcessRuntimeStepAssignment childOutputAssignment,
        ProcessRuntimeStateSnapshot childState,
        IReadOnlyList<ProcessSubprocessForwardedChildContextArtifactContract> contracts,
        out IReadOnlyList<ParentSubprocessForwardedContextArtifact> forwardedArtifacts,
        out ParentSubprocessForwardedContextIssue? issue)
    {
        forwardedArtifacts = [];
        issue = null;
        if (contracts.Count == 0)
        {
            return true;
        }

        if (contracts.Count > MaxForwardedContextArtifacts)
        {
            issue = CreateIssue(
                childRunId,
                childOutputAssignment,
                "process.adapter.subprocess_forwarded_context_limit_exceeded",
                $"The child subprocess contract requests {contracts.Count} forwarded context artifacts, which exceeds the runtime limit {MaxForwardedContextArtifacts}.",
                string.Join("|", contracts.Select(contract => contract.BindingKey)));
            return false;
        }

        var childOutputStep = childState.Steps.FirstOrDefault(step =>
            step.StepInstanceId == childOutputAssignment.StepInstanceId);
        if (childOutputStep is null)
        {
            issue = CreateIssue(
                childRunId,
                childOutputAssignment,
                "process.adapter.subprocess_forwarded_context_step_missing",
                "The child output step state is unavailable, so its declared context artifacts cannot be forwarded.",
                childOutputAssignment.StepInstanceId.Value.ToString("D"));
            return false;
        }

        var resolvedArtifacts = new List<ParentSubprocessForwardedContextArtifact>(contracts.Count);
        foreach (var contract in contracts)
        {
            if (!TryResolveBindingDescriptor(
                    childRunId,
                    childOutputAssignment,
                    childOutputStep,
                    contract,
                    out var descriptor,
                    out issue))
            {
                return false;
            }

            if (!TryResolveAvailableInputReceipt(
                    childRunId,
                    childOutputAssignment,
                    childState,
                    descriptor.SlotId,
                    contract.BindingKey,
                    out var inputReceipt,
                    out issue))
            {
                return false;
            }

            if (!WorkspaceProcessRunArtifactPath.TryResolveRunId(
                    descriptor.PrimaryManagedRef,
                    out var artifactRunId,
                    out _) ||
                !string.Equals(artifactRunId, childRunId.Value.ToString("D"), StringComparison.OrdinalIgnoreCase))
            {
                issue = CreateIssue(
                    childRunId,
                    childOutputAssignment,
                    "process.adapter.subprocess_forwarded_context_cross_run_denied",
                    $"Forwarded binding '{contract.BindingKey}' must resolve to a managed artifact from the completed child run, not '{descriptor.PrimaryManagedRef}'.",
                    $"{contract.BindingKey}:{descriptor.PrimaryManagedRef}");
                return false;
            }

            var readResult = workspaceFiles.ReadTextFile(
                descriptor.PrimaryManagedRef,
                WorkspaceFileLimits.MaxTextReadCharacters);
            if (!readResult.Succeeded ||
                string.IsNullOrWhiteSpace(readResult.Content) ||
                readResult.IsTruncated)
            {
                var reason = !readResult.Succeeded
                    ? readResult.Message
                    : readResult.IsTruncated
                        ? $"content exceeds {WorkspaceFileLimits.MaxTextReadCharacters} characters"
                        : "content is empty";
                issue = CreateIssue(
                    childRunId,
                    childOutputAssignment,
                    "process.adapter.subprocess_forwarded_context_read_failed",
                    $"Forwarded binding '{contract.BindingKey}' could not be read as bounded child context: {reason}.",
                    $"{contract.BindingKey}:{descriptor.PrimaryManagedRef}:{reason}");
                return false;
            }

            var actualContentHash = ComputeContentHash(readResult.Content);
            if (!string.Equals(inputReceipt.ContentHash.Trim(), actualContentHash, StringComparison.OrdinalIgnoreCase))
            {
                issue = CreateIssue(
                    childRunId,
                    childOutputAssignment,
                    "process.adapter.subprocess_forwarded_context_hash_mismatch",
                    $"Forwarded binding '{contract.BindingKey}' content no longer matches the child output-step input ledger.",
                    $"{contract.BindingKey}:{descriptor.PrimaryManagedRef}:{inputReceipt.ContentHash}:{actualContentHash}");
                return false;
            }

            resolvedArtifacts.Add(new ParentSubprocessForwardedContextArtifact(
                contract.BindingKey.Trim(),
                contract.SourceStepKey.Trim(),
                contract.ArtifactExpectationKey.Trim(),
                contract.PayloadSchema.Trim(),
                descriptor.PrimaryManagedRef,
                readResult.Content));
        }

        forwardedArtifacts = resolvedArtifacts;
        return true;
    }

    private static bool TryResolveBindingDescriptor(
        ProcessRunId childRunId,
        ProcessRuntimeStepAssignment childOutputAssignment,
        ProcessRuntimeStepState childOutputStep,
        ProcessSubprocessForwardedChildContextArtifactContract contract,
        out ProcessArtifactSlotDescriptor descriptor,
        out ParentSubprocessForwardedContextIssue? issue)
    {
        descriptor = null!;
        issue = null;
        var matchingDescriptors = childOutputStep.ArtifactDescriptors
            .Where(candidate =>
                childOutputStep.RequiredArtifactSlots.Contains(candidate.SlotId) &&
                string.Equals(candidate.StepKey, contract.SourceStepKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.ArtifactExpectationKey, contract.ArtifactExpectationKey, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matchingDescriptors.Length != 1)
        {
            issue = CreateIssue(
                childRunId,
                childOutputAssignment,
                "process.adapter.subprocess_forwarded_context_binding_unresolved",
                $"The child output step '{childOutputAssignment.StepKey}' does not expose exactly one required artifact for forwarded binding '{contract.BindingKey}' ({contract.SourceStepKey}/{contract.ArtifactExpectationKey}).",
                $"{contract.BindingKey}:{contract.SourceStepKey}:{contract.ArtifactExpectationKey}:{matchingDescriptors.Length}");
            return false;
        }

        descriptor = matchingDescriptors[0];
        return true;
    }

    private static bool TryResolveAvailableInputReceipt(
        ProcessRunId childRunId,
        ProcessRuntimeStepAssignment childOutputAssignment,
        ProcessRuntimeStateSnapshot childState,
        ArtifactSlotId requiredSlotId,
        string bindingKey,
        out ProcessRuntimeInputArtifactReceipt inputReceipt,
        out ParentSubprocessForwardedContextIssue? issue)
    {
        inputReceipt = null!;
        issue = null;
        var matchingReceipts = childState.ConnectedInputArtifacts
            .Where(receipt =>
                receipt.ConsumerStepInstanceId == childOutputAssignment.StepInstanceId &&
                receipt.RequiredSlotId == requiredSlotId)
            .ToArray();
        if (matchingReceipts.Length != 1)
        {
            issue = CreateIssue(
                childRunId,
                childOutputAssignment,
                "process.adapter.subprocess_forwarded_context_input_ledger_unresolved",
                $"Forwarded binding '{bindingKey}' does not have exactly one child output-step input ledger receipt.",
                $"{bindingKey}:{requiredSlotId.Value:D}:{matchingReceipts.Length}");
            return false;
        }

        inputReceipt = matchingReceipts[0];
        if (inputReceipt.Availability != ProcessArtifactInputAvailability.Available ||
            inputReceipt.ArtifactId is null ||
            string.IsNullOrWhiteSpace(inputReceipt.ContentHash))
        {
            issue = CreateIssue(
                childRunId,
                childOutputAssignment,
                "process.adapter.subprocess_forwarded_context_input_unavailable",
                $"Forwarded binding '{bindingKey}' is not an available, content-grounded child output-step input.",
                $"{bindingKey}:{requiredSlotId.Value:D}:{inputReceipt.Availability}:{inputReceipt.ArtifactId}:{inputReceipt.ContentHash}");
            return false;
        }

        return true;
    }

    private static ParentSubprocessForwardedContextIssue CreateIssue(
        ProcessRunId childRunId,
        ProcessRuntimeStepAssignment childOutputAssignment,
        string code,
        string summary,
        string evidence)
        => new(
            code,
            summary,
            $"{childRunId.Value:D}:{childOutputAssignment.StepInstanceId.Value:D}:{code}:{evidence}");

    private static string ComputeContentHash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}

internal static class ParentSubprocessForwardedContextEnvelope
{
    internal const string BeginMarker = "<!-- candoitall:runtime-forwarded-child-context:begin -->";
    internal const string EndMarker = "<!-- candoitall:runtime-forwarded-child-context:end -->";

    internal enum MatchResult
    {
        NotPresent,
        Removed,
        Invalid
    }

    internal static string Format(IReadOnlyList<ParentSubprocessForwardedContextArtifact> forwardedContextArtifacts)
    {
        if (forwardedContextArtifacts.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder()
            .AppendLine(BeginMarker)
            .AppendLine("## Runtime-forwarded child context")
            .AppendLine()
            .AppendLine("The runtime copied the following typed child inputs from the completed child run after verifying their declared slot bindings and content hashes. Their child refs are trace-only; use the copied content below and do not attempt an additional cross-run read.");
        foreach (var artifact in forwardedContextArtifacts)
        {
            builder
                .AppendLine()
                .AppendLine($"### {artifact.BindingKey}")
                .AppendLine()
                .AppendLine($"- Source step: `{artifact.SourceStepKey}`")
                .AppendLine($"- Artifact expectation: `{artifact.ArtifactExpectationKey}`")
                .AppendLine($"- Payload schema: `{artifact.PayloadSchema}`")
                .AppendLine($"- Child artifact ref (trace only): `{artifact.ChildArtifactRef}`")
                .AppendLine()
                .AppendLine(SelectFence(artifact.Content))
                .AppendLine(artifact.Content)
                .AppendLine(SelectFence(artifact.Content));
        }

        return builder
            .AppendLine(EndMarker)
            .ToString()
            .TrimEnd();
    }

    internal static MatchResult TryRemoveSingleVerified(
        string content,
        string? verifiedEnvelope,
        out string contentWithoutEnvelope)
    {
        return ParentSubprocessRuntimeEnvelopeFraming.TryRemoveSingleVerified(
            content,
            verifiedEnvelope,
            BeginMarker,
            EndMarker,
            out contentWithoutEnvelope,
            out var wasPresent)
                ? wasPresent
                    ? MatchResult.Removed
                    : MatchResult.NotPresent
                : MatchResult.Invalid;
    }

    internal static bool ContainsReservedMarker(string? content)
        => !string.IsNullOrEmpty(content) &&
           (content.Contains(BeginMarker, StringComparison.Ordinal) ||
            content.Contains(EndMarker, StringComparison.Ordinal));

    private static string SelectFence(string content)
    {
        var longestRun = 0;
        var currentRun = 0;
        foreach (var character in content)
        {
            if (character == '`')
            {
                currentRun++;
                longestRun = Math.Max(longestRun, currentRun);
                continue;
            }

            currentRun = 0;
        }

        return new string('`', Math.Max(3, longestRun + 1));
    }
}

internal static class ParentSubprocessVerifiedChildOutputEnvelope
{
    internal const string BeginMarker = "<!-- candoitall:runtime-verified-child-output:begin -->";
    internal const string EndMarker = "<!-- candoitall:runtime-verified-child-output:end -->";

    internal enum MatchResult
    {
        NotPresent,
        Removed,
        Invalid
    }

    internal static string Format(ProcessSubprocessVerifiedChildArtifact artifact)
    {
        var fence = SelectFence(artifact.Content);
        return new StringBuilder()
            .AppendLine(BeginMarker)
            .AppendLine("## Runtime-verified child output")
            .AppendLine()
            .AppendLine("The runtime copied the selected child output only after verifying its declared produced slot, accepted managed-artifact lifecycle, and ledger content hash. The child ref is trace-only; use the authenticated payload below and do not attempt an additional cross-run read.")
            .AppendLine()
            .AppendLine($"- Child output step: `{artifact.StepKey}`")
            .AppendLine($"- Artifact expectation: `{artifact.ArtifactExpectationKey}`")
            .AppendLine($"- Child artifact ref (trace only): `{artifact.ArtifactRef}`")
            .AppendLine($"- Ledger content hash: `{artifact.ContentHash}`")
            .AppendLine()
            .AppendLine(fence)
            .AppendLine(artifact.Content)
            .AppendLine(fence)
            .AppendLine(EndMarker)
            .ToString()
            .TrimEnd();
    }

    internal static MatchResult TryRemoveSingleVerified(
        string content,
        string? verifiedEnvelope,
        out string contentWithoutEnvelope)
    {
        return ParentSubprocessRuntimeEnvelopeFraming.TryRemoveSingleVerified(
            content,
            verifiedEnvelope,
            BeginMarker,
            EndMarker,
            out contentWithoutEnvelope,
            out var wasPresent)
                ? wasPresent
                    ? MatchResult.Removed
                    : MatchResult.NotPresent
                : MatchResult.Invalid;
    }

    internal static bool ContainsReservedMarker(string? content)
        => !string.IsNullOrEmpty(content) &&
           (content.Contains(BeginMarker, StringComparison.Ordinal) ||
            content.Contains(EndMarker, StringComparison.Ordinal));

    private static string SelectFence(string content)
    {
        var longestRun = 0;
        var currentRun = 0;
        foreach (var character in content)
        {
            if (character == '`')
            {
                currentRun++;
                longestRun = Math.Max(longestRun, currentRun);
                continue;
            }

            currentRun = 0;
        }

        return new string('`', Math.Max(3, longestRun + 1));
    }
}
