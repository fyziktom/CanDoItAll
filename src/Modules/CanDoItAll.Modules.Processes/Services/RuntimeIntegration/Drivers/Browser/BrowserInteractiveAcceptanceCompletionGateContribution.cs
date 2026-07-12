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
            .OrderBy(receipt => receipt.CompletedAtUtc)
            .ThenBy(receipt => receipt.Id)
            .ToArray();
        var interactionReceiptIndex = Array.FindIndex(
            currentReceipts,
            receipt => InteractionToolNames.Contains(receipt.ToolName) &&
                       IsSuccessfulReceipt(receipt.ExitSummary));
        if (interactionReceiptIndex < 0)
        {
            return CreateIssue(
                context.Assignment,
                ProcessCompletionDiagnosticCodes.UiInteractionEvidenceMissing,
                $"Step '{context.Assignment.StepKey}' claimed browser-workflow acceptance without a current-execution click, fill, selection, key, type, drag, or elapsed-time interaction receipt. Navigation and screenshots prove reachability only.",
                "representative-browser-interaction");
        }

        var hasPostInteractionState = currentReceipts
            .Skip(interactionReceiptIndex + 1)
            .Any(receipt => StateEvidenceToolNames.Contains(receipt.ToolName) &&
                            IsSuccessfulReceipt(receipt.ExitSummary));
        return hasPostInteractionState
            ? null
            : CreateIssue(
                context.Assignment,
                ProcessCompletionDiagnosticCodes.UiPostInteractionStateEvidenceMissing,
                $"Step '{context.Assignment.StepKey}' claimed browser-workflow acceptance without snapshot, evaluation, or screenshot evidence captured after the representative interaction.",
                "post-interaction-state");
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
