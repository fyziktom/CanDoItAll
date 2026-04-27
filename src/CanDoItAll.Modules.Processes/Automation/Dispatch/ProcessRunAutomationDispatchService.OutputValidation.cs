using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static AgentStructuredOutputContract ProcessStepOutcomeStructuredOutputContract { get; } =
        AgentStructuredOutputContract.For<ProcessStepOutcomeResult>(
            "process_step_outcome_result",
            "Validated machine contract for process step completion, branch selection, next actions, and display-only markdown summary.");

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

    private sealed class ProcessStepOutcomeValidator : IAgentOutputValidator<ProcessStepOutcomeResult>
    {
        public AgentOutputValidationResult Validate(ProcessStepOutcomeResult output)
        {
            ArgumentNullException.ThrowIfNull(output);

            var errors = new List<AgentOutputValidationError>();
            if (string.IsNullOrWhiteSpace(output.Reason))
            {
                errors.Add(new AgentOutputValidationError
                {
                    Code = "process.step_outcome.reason_required",
                    Message = "Process step outcome reason is required.",
                    Path = "$.reason"
                });
            }

            if (output.Status == ProcessStepOutcomeStatus.Completed &&
                output.NextActions.Any(action => action.Contains("ask the user", StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add(new AgentOutputValidationError
                {
                    Code = "process.step_outcome.completed_next_action_inconsistent",
                    Message = "Completed process outcomes must not ask for follow-up input as a next action.",
                    Path = "$.nextActions"
                });
            }

            if (output.Status == ProcessStepOutcomeStatus.Failed &&
                output.NextActions.Count == 0)
            {
                errors.Add(new AgentOutputValidationError
                {
                    Code = "process.step_outcome.failed_next_action_required",
                    Message = "Failed process outcomes must include at least one next action.",
                    Path = "$.nextActions"
                });
            }

            return errors.Count == 0
                ? AgentOutputValidationResult.Success()
                : AgentOutputValidationResult.Failure([.. errors]);
        }
    }
}
