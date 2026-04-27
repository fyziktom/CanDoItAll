using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static AgentStructuredOutputContract ProcessStepOutcomeStructuredOutputContract { get; } =
        AgentStructuredOutputContracts.ProcessStepOutcomeResult;

    private static string ResolveOutputInspectionText(string? responseText)
    {
        return TryReadProcessStepOutcome(responseText, out var outcome, out _)
            ? outcome.HumanReadableSummaryMarkdown ?? outcome.Reason
            : responseText ?? string.Empty;
    }

    private static bool TryReadProcessStepOutcome(
        string? responseText,
        out ProcessStepOutcomeResult outcome,
        out AgentOutputValidationResult validation)
    {
        var result = AgentOutputJson.DeserializeAndValidate(
            responseText,
            new ProcessStepOutcomeValidator());
        if (result.Succeeded && result.Output is not null)
        {
            outcome = result.Output;
            validation = result.Validation;
            return true;
        }

        outcome = default!;
        validation = result.Validation;
        return false;
    }

    private static ProcessStepRunStatus MapProcessStepOutcomeStatus(ProcessStepOutcomeStatus status)
    {
        return status switch
        {
            ProcessStepOutcomeStatus.Completed => ProcessStepRunStatus.Completed,
            ProcessStepOutcomeStatus.Blocked => ProcessStepRunStatus.Blocked,
            ProcessStepOutcomeStatus.Failed => ProcessStepRunStatus.Failed,
            ProcessStepOutcomeStatus.WaitingApproval => ProcessStepRunStatus.WaitingApproval,
            ProcessStepOutcomeStatus.Refused => ProcessStepRunStatus.Refused,
            _ => ProcessStepRunStatus.Failed
        };
    }

}
