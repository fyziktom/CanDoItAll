using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessToolReceiptEvidenceGate(
    IWorkspaceFileService workspaceFiles,
    IEnumerable<IProcessToolReceiptEvidencePolicyContribution> policyContributions)
{
    private const int MaximumEvidenceCharacters = 200000;
    private readonly IReadOnlyList<IProcessToolReceiptEvidencePolicyContribution> policyContributions =
        policyContributions.ToArray();

    internal ProcessCompletionIssue? Validate(ProcessCompletionGateContext context)
    {
        if (context.Output.Status != ProcessStepOutcomeStatus.Completed ||
            context.CurrentExecutionRunId is null ||
            context.ToolReceipts is null)
        {
            return null;
        }

        var rules = policyContributions
            .SelectMany(contribution => contribution.ResolveRules(context.Assignment, context.Output))
            .ToArray();
        foreach (var rule in rules)
        {
            var receipts = context.ToolReceipts
                .Where(receipt =>
                    receipt.ExecutionRunId == context.CurrentExecutionRunId.Value &&
                    string.Equals(receipt.ToolName, rule.ToolName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(receipt => receipt.CompletedAtUtc)
                .ThenBy(receipt => receipt.Id)
                .ToArray();
            var matchedArtifactReceipt = false;
            foreach (var receipt in receipts)
            {
                if (!TryReadArgument(receipt.RequestSummary, rule.ArtifactPathArgumentName, out var artifactPath))
                {
                    return CreateIssue(
                        context.Assignment,
                        rule,
                        "The current-run receipt does not expose a readable artifact path.",
                        $"missing-path:{receipt.Id:D}");
                }

                if (!string.IsNullOrWhiteSpace(rule.RequiredArtifactPathFragment) &&
                    !artifactPath.Replace('\\', '/').Contains(
                        rule.RequiredArtifactPathFragment.Replace('\\', '/'),
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                matchedArtifactReceipt = true;
                if (!IsCurrentRunArtifactPath(context.Assignment, artifactPath))
                {
                    return CreateIssue(
                        context.Assignment,
                        rule,
                        $"The current-run receipt points outside this process run's managed artifact root: '{artifactPath}'.",
                        $"wrong-run-path:{artifactPath}");
                }

                var readResult = workspaceFiles.ReadTextFile(artifactPath, MaximumEvidenceCharacters);
                if (!readResult.Succeeded || readResult.IsTruncated)
                {
                    var reason = readResult.Succeeded
                        ? $"Evidence artifact '{artifactPath}' exceeds the deterministic inspection limit."
                        : $"Evidence artifact '{artifactPath}' could not be read: {readResult.Message}";
                    return CreateIssue(
                        context.Assignment,
                        rule,
                        reason,
                        $"read-failed:{artifactPath}:{readResult.Message}");
                }

                var rejectedMarker = rule.ForbiddenContentMarkers.FirstOrDefault(marker =>
                    !string.IsNullOrWhiteSpace(marker) &&
                    readResult.Content.Contains(marker, StringComparison.OrdinalIgnoreCase));
                if (rejectedMarker is not null)
                {
                    return CreateIssue(
                        context.Assignment,
                        rule,
                        $"{rule.RejectionSummary} Current-run evidence: '{artifactPath}'.",
                        $"forbidden-content:{artifactPath}:{rejectedMarker}");
                }
            }

            if (!string.IsNullOrWhiteSpace(rule.RequiredArtifactPathFragment) && !matchedArtifactReceipt)
            {
                return CreateIssue(
                    context.Assignment,
                    rule,
                    $"No current-run '{rule.ToolName}' receipt inspected the required managed artifact matching '{rule.RequiredArtifactPathFragment}'.",
                    $"missing-required-artifact-read:{rule.RequiredArtifactPathFragment}");
            }
        }

        return null;
    }

    private static ProcessCompletionIssue CreateIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessToolReceiptTextEvidenceRule rule,
        string reason,
        string evidence)
        => new(
            ProcessCompletionDiagnosticCodes.ToolReceiptEvidenceContentRejected,
            $"Step '{assignment.StepKey}' claimed completion but tool receipt evidence policy '{rule.PolicyKey}' rejected the evidence. {reason}",
            $"{assignment.RunId.Value:D}:{assignment.StepInstanceId.Value:D}:{rule.PolicyKey}:{evidence}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);

    private static bool IsCurrentRunArtifactPath(
        ProcessRuntimeStepAssignment assignment,
        string artifactPath)
    {
        var normalizedPath = $"/{artifactPath.Trim().Replace('\\', '/').Trim('/')}";
        var expectedRunSegment = $"/process-runs/{assignment.RunId.Value:D}/";
        return normalizedPath.Contains(expectedRunSegment, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadArgument(
        string requestSummary,
        string argumentName,
        out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(requestSummary) || string.IsNullOrWhiteSpace(argumentName))
        {
            return false;
        }

        var marker = $"{argumentName}=";
        var markerIndex = requestSummary.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            value = requestSummary.Trim().Trim('"', '\'');
            return value.Length > 0;
        }

        var valueStart = markerIndex + marker.Length;
        while (valueStart < requestSummary.Length && char.IsWhiteSpace(requestSummary[valueStart]))
        {
            valueStart++;
        }

        if (valueStart >= requestSummary.Length)
        {
            return false;
        }

        var quote = requestSummary[valueStart] is '"' or '\''
            ? requestSummary[valueStart++]
            : '\0';
        var valueEnd = quote == '\0'
            ? requestSummary.IndexOf(',', valueStart)
            : requestSummary.IndexOf(quote, valueStart);
        if (valueEnd < 0)
        {
            valueEnd = requestSummary.Length;
        }

        value = requestSummary[valueStart..valueEnd].Trim();
        return value.Length > 0;
    }
}
