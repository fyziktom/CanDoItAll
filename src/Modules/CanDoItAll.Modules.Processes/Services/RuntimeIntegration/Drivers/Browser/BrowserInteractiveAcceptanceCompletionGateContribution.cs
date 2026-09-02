using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

using static CanDoItAll.Modules.Processes.ProcessManagedArtifactEvidence;
using static CanDoItAll.Modules.Processes.ProcessRequiredReceiptMatcher;

namespace CanDoItAll.Modules.Processes;

internal sealed class BrowserInteractiveAcceptanceCompletionGateContribution : IProcessCompletionGateContribution
{
    private static readonly HashSet<string> InteractionToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ToolContractCatalog.BrowserClick,
        ToolContractCatalog.BrowserFillForm,
        ToolContractCatalog.BrowserSelectOption,
        ToolContractCatalog.BrowserPressKey,
        ToolContractCatalog.BrowserType,
        ToolContractCatalog.BrowserDrag,
        ToolContractCatalog.BrowserWaitFor
    };

    private static readonly HashSet<string> StateEvidenceToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ToolContractCatalog.BrowserEvaluate,
        ToolContractCatalog.BrowserSnapshot,
        ToolContractCatalog.BrowserTakeScreenshot
    };

    public string ContributionKey => "browser.interactive-acceptance";

    public int Order => 200;

    public ProcessCompletionIssue? Validate(ProcessCompletionGateContext context)
    {
        if (context.Output.Status != ProcessStepOutcomeStatus.Completed ||
            context.CurrentExecutionRunId is null ||
            context.ToolReceipts is null ||
            !DeclaresInteractiveBrowserAcceptance(context))
        {
            return null;
        }

        var currentReceipts = context.ToolReceipts
            .Where(receipt => receipt.ExecutionRunId == context.CurrentExecutionRunId.Value)
            .ToArray();
        var interactionReceipts = currentReceipts
            .Where(receipt => InteractionToolNames.Contains(receipt.ToolName) &&
                              IsSuccessfulReceipt(receipt.ExitSummary))
            .ToArray();
        if (interactionReceipts.Length == 0)
        {
            return CreateIssue(
                context.Assignment,
                ProcessCompletionDiagnosticCodes.UiInteractionEvidenceMissing,
                $"Step '{context.Assignment.StepKey}' claimed browser-workflow acceptance without a current-execution click, fill, selection, key, type, drag, or elapsed-time interaction receipt. Navigation and screenshots prove reachability only.",
                "representative-browser-interaction");
        }

        var postInteractionStateReceipts = currentReceipts
            .Where(receipt => StateEvidenceToolNames.Contains(receipt.ToolName) &&
                              IsSuccessfulReceipt(receipt.ExitSummary) &&
                              interactionReceipts.Any(interaction =>
                                  receipt.StartedAtUtc >= interaction.CompletedAtUtc))
            .ToArray();
        if (postInteractionStateReceipts.Length == 0)
        {
            return CreateIssue(
                context.Assignment,
                ProcessCompletionDiagnosticCodes.UiPostInteractionStateEvidenceMissing,
                $"Step '{context.Assignment.StepKey}' claimed browser-workflow acceptance without snapshot, evaluation, or screenshot evidence captured after the representative interaction.",
                "post-interaction-state");
        }

        return postInteractionStateReceipts.Any(receipt =>
                IsCurrentRunManagedBrowserEvidence(context.Assignment, receipt))
            ? null
            : CreateIssue(
                context.Assignment,
                ProcessCompletionDiagnosticCodes.UiPostInteractionStateEvidenceMissing,
                $"Step '{context.Assignment.StepKey}' claimed browser-workflow acceptance without post-interaction state evidence persisted beneath its current process-run managed artifact root. Pass a filename under '{BuildManagedArtifactRoot(context.Assignment)}/browser/'.",
                "post-interaction-managed-state");
    }

    private static bool IsCurrentRunManagedBrowserEvidence(
        ProcessRuntimeStepAssignment assignment,
        ToolExecutionReceiptRecord receipt)
    {
        if (!ProcessToolReceiptRequestArgumentReader.TryReadString(
                receipt.RequestSummary,
                "filename",
                out var artifactPath))
        {
            return false;
        }

        var normalizedPath = NormalizeBrowserEvidencePath(artifactPath);
        if (Path.IsPathRooted(normalizedPath) ||
            normalizedPath.StartsWith("artifacts/scopes/", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(segment => string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            return false;
        }

        var currentRunBrowserRoot =
            NormalizeManagedArtifactRef(BuildManagedArtifactRoot(assignment)) + "/browser/";
        return normalizedPath.StartsWith(currentRunBrowserRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeBrowserEvidencePath(string value)
    {
        var normalized = value.Trim().Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized.TrimEnd('/');
    }

    private static bool DeclaresInteractiveBrowserAcceptance(ProcessCompletionGateContext context)
    {
        var applicableRules = ResolveApplicableProductCompletionRequiredToolReceiptRules(
            context.Assignment,
            context.Output.BranchOutcomeKey);
        return applicableRules.Any(rule =>
                   string.Equals(rule.ToolReceipt, ToolContractCatalog.BrowserNavigate, StringComparison.OrdinalIgnoreCase)) &&
               applicableRules.Any(rule =>
                   StateEvidenceToolNames.Contains(rule.ToolReceipt));
    }

    private static ProcessCompletionIssue CreateIssue(
        ProcessRuntimeStepAssignment assignment,
        string code,
        string summary,
        string evidenceKind)
        => new(
            code,
            summary,
            $"{assignment.RunId.Value:D}:{assignment.StepInstanceId.Value:D}:{evidenceKind}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
}
