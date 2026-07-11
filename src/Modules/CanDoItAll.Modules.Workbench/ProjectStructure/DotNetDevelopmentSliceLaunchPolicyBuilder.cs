using System.Text.Json;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal static class DotNetDevelopmentSliceLaunchPolicyBuilder
{
    internal static string BuildCompletionIssueRouteMap()
        => JsonSerializer.Serialize(new Dictionary<string, object[]>(StringComparer.Ordinal)
        {
            ["add-tests-and-proof"] =
            [
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["issueCode"] = ProcessCompletionDiagnosticCodes.ToolReceiptEvidenceContentRejected,
                    ["sourceBranchOutcomeKeys"] = new[] { "slice-accepted" },
                    ["targetBranchOutcomeKey"] = "slice-repair-required",
                    ["targetBranchOutcomeTitle"] = "Slice repair required",
                    ["requiresDefectEvidence"] = false
                }
            ],
            ["add-tests-recheck"] =
            [
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["issueCode"] = ProcessCompletionDiagnosticCodes.ToolReceiptEvidenceContentRejected,
                    ["sourceBranchOutcomeKeys"] = new[] { "slice-accepted" },
                    ["targetBranchOutcomeKey"] = "slice-repair-escalation",
                    ["targetBranchOutcomeTitle"] = "Slice repair escalation",
                    ["requiresDefectEvidence"] = false
                }
            ]
        });
}
