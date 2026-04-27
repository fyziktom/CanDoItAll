using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentOutputContractValidator
{
    Type OutputType { get; }

    AgentOutputContractValidationResult DeserializeAndValidate(string? rawOutput);
}

public interface IAgentOutputValidatorRegistry
{
    bool TryResolve(Type outputType, out IAgentOutputContractValidator validator);
}

public sealed record AgentOutputContractValidationResult(
    bool Succeeded,
    Type OutputType,
    string RawOutput,
    string RawOutputHash,
    AgentOutputValidationResult Validation,
    object? Output);

public sealed class DefaultAgentOutputValidatorRegistry : IAgentOutputValidatorRegistry
{
    public static DefaultAgentOutputValidatorRegistry Instance { get; } = new();

    private readonly IReadOnlyDictionary<Type, IAgentOutputContractValidator> validators;

    public DefaultAgentOutputValidatorRegistry()
    {
        validators = new IAgentOutputContractValidator[]
            {
                new AgentOutputContractValidator<ProcessStepOutcomeResult>(new ProcessStepOutcomeValidator()),
                new AgentOutputContractValidator<ProcessStatePatch>(
                    new ProcessStatePatchValidator(
                        new HashSet<string>(StringComparer.Ordinal)
                        {
                            "/artifacts",
                            "/assignments",
                            "/decisions",
                            "/evidence",
                            "/steps",
                            "/status"
                        },
                        new HashSet<string>(StringComparer.Ordinal)
                        {
                            "/credentials",
                            "/secrets",
                            "/system"
                        })),
                new AgentOutputContractValidator<CodeReviewResult>(new CodeReviewResultValidator()),
                new AgentOutputContractValidator<ArchitectureReviewResult>(new ArchitectureReviewResultValidator()),
                new AgentOutputContractValidator<ImplementationPlanResult>(new ImplementationPlanResultValidator()),
                new AgentOutputContractValidator<TestPlanResult>(new TestPlanResultValidator()),
                new AgentOutputContractValidator<ToolExecutionDecisionResult>(new ToolExecutionDecisionResultValidator()),
                new AgentOutputContractValidator<HumanEscalationRequest>(new HumanEscalationRequestValidator())
            }
            .ToDictionary(item => item.OutputType);
    }

    public bool TryResolve(Type outputType, out IAgentOutputContractValidator validator)
    {
        ArgumentNullException.ThrowIfNull(outputType);
        return validators.TryGetValue(outputType, out validator!);
    }
}

public sealed class AgentOutputContractValidator<TOutput>(
    IAgentOutputValidator<TOutput> validator) : IAgentOutputContractValidator
{
    public Type OutputType { get; } = typeof(TOutput);

    public AgentOutputContractValidationResult DeserializeAndValidate(string? rawOutput)
    {
        var result = AgentOutputJson.DeserializeAndValidate(rawOutput, validator);
        return new AgentOutputContractValidationResult(
            result.Succeeded,
            typeof(TOutput),
            result.RawOutput,
            result.RawOutputHash,
            result.Validation,
            result.Output);
    }
}

public sealed class ProcessStepOutcomeValidator : IAgentOutputValidator<ProcessStepOutcomeResult>
{
    public AgentOutputValidationResult Validate(ProcessStepOutcomeResult output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var errors = new List<AgentOutputValidationError>();
        RequireText(output.Reason, "process.step_outcome.reason_required", "Process step outcome reason is required.", "$.reason", errors);

        if (output.Status == ProcessStepOutcomeStatus.Completed &&
            output.NextActions.Any(action => action.Contains("ask the user", StringComparison.OrdinalIgnoreCase) ||
                                             action.Contains("human input", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "process.step_outcome.completed_next_action_inconsistent",
                Message = "Completed process outcomes must not ask for follow-up input as a next action.",
                Path = "$.nextActions"
            });
        }

        if (output.Status is ProcessStepOutcomeStatus.Failed or ProcessStepOutcomeStatus.Blocked &&
            output.NextActions.Count == 0)
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "process.step_outcome.next_action_required",
                Message = "Failed or blocked process outcomes must include at least one next action.",
                Path = "$.nextActions"
            });
        }

        if (output.Status == ProcessStepOutcomeStatus.WaitingApproval &&
            output.NextActions.Count == 0)
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "process.step_outcome.approval_next_action_required",
                Message = "WaitingApproval outcomes must state the required approval or escalation next action.",
                Path = "$.nextActions"
            });
        }

        if (!string.IsNullOrWhiteSpace(output.BranchOutcomeTitle) &&
            string.IsNullOrWhiteSpace(output.BranchOutcomeKey))
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "process.step_outcome.branch_key_required",
                Message = "A branch outcome title must be paired with a stable BranchOutcomeKey.",
                Path = "$.branchOutcomeKey"
            });
        }

        return ToResult(errors);
    }

    private static void RequireText(
        string? value,
        string code,
        string message,
        string path,
        List<AgentOutputValidationError> errors)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        errors.Add(new AgentOutputValidationError
        {
            Code = code,
            Message = message,
            Path = path
        });
    }

    private static AgentOutputValidationResult ToResult(IReadOnlyList<AgentOutputValidationError> errors)
        => errors.Count == 0
            ? AgentOutputValidationResult.Success()
            : AgentOutputValidationResult.Failure([.. errors]);
}

public sealed class CodeReviewResultValidator : IAgentOutputValidator<CodeReviewResult>
{
    public AgentOutputValidationResult Validate(CodeReviewResult output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var errors = new List<AgentOutputValidationError>();
        if (output.Status == CodeReviewStatus.Passed && (output.Findings.Count > 0 || output.RequiredActions.Count > 0))
        {
            errors.Add(Error("code_review.passed_with_findings", "Passed code reviews must not contain findings or required actions.", "$.status"));
        }

        if (output.Status is (CodeReviewStatus.NeedsChanges or CodeReviewStatus.Failed) && output.Findings.Count == 0)
        {
            errors.Add(Error("code_review.findings_required", "Code reviews that need changes or fail must include findings.", "$.findings"));
        }

        return ToResult(errors);
    }

    private static AgentOutputValidationError Error(string code, string message, string path)
        => new() { Code = code, Message = message, Path = path };

    private static AgentOutputValidationResult ToResult(IReadOnlyList<AgentOutputValidationError> errors)
        => errors.Count == 0 ? AgentOutputValidationResult.Success() : AgentOutputValidationResult.Failure([.. errors]);
}

public sealed class ArchitectureReviewResultValidator : IAgentOutputValidator<ArchitectureReviewResult>
{
    public AgentOutputValidationResult Validate(ArchitectureReviewResult output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var errors = new List<AgentOutputValidationError>();
        if (output.Status == ArchitectureReviewStatus.Approved && output.RequiredActions.Count > 0)
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "architecture_review.approved_with_required_actions",
                Message = "Approved architecture reviews must not include required actions.",
                Path = "$.requiredActions"
            });
        }

        if (output.Status is (ArchitectureReviewStatus.NeedsChanges or ArchitectureReviewStatus.Rejected) && output.BoundaryConcerns.Count == 0)
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "architecture_review.concerns_required",
                Message = "Architecture reviews that need changes or are rejected must include boundary concerns.",
                Path = "$.boundaryConcerns"
            });
        }

        return errors.Count == 0 ? AgentOutputValidationResult.Success() : AgentOutputValidationResult.Failure([.. errors]);
    }
}

public sealed class ImplementationPlanResultValidator : IAgentOutputValidator<ImplementationPlanResult>
{
    public AgentOutputValidationResult Validate(ImplementationPlanResult output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var errors = new List<AgentOutputValidationError>();
        if (output.Tasks.Count == 0)
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "implementation_plan.tasks_required",
                Message = "Implementation plans must include at least one task.",
                Path = "$.tasks"
            });
        }

        for (var index = 0; index < output.Tasks.Count; index++)
        {
            var task = output.Tasks[index];
            if (string.IsNullOrWhiteSpace(task.Id) || string.IsNullOrWhiteSpace(task.Title))
            {
                errors.Add(new AgentOutputValidationError
                {
                    Code = "implementation_plan.task_identity_required",
                    Message = "Each implementation task must include an id and title.",
                    Path = $"$.tasks[{index}]"
                });
            }
        }

        return errors.Count == 0 ? AgentOutputValidationResult.Success() : AgentOutputValidationResult.Failure([.. errors]);
    }
}

public sealed class TestPlanResultValidator : IAgentOutputValidator<TestPlanResult>
{
    public AgentOutputValidationResult Validate(TestPlanResult output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (output.Status == TestPlanStatus.Ready && output.TestCases.Count == 0)
        {
            return AgentOutputValidationResult.Failure(new AgentOutputValidationError
            {
                Code = "test_plan.test_cases_required",
                Message = "Ready test plans must include test cases.",
                Path = "$.testCases"
            });
        }

        return AgentOutputValidationResult.Success();
    }
}

public sealed class ToolExecutionDecisionResultValidator : IAgentOutputValidator<ToolExecutionDecisionResult>
{
    public AgentOutputValidationResult Validate(ToolExecutionDecisionResult output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var errors = new List<AgentOutputValidationError>();
        if (string.IsNullOrWhiteSpace(output.ToolName))
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "tool_decision.tool_name_required",
                Message = "Tool execution decisions must name the target tool.",
                Path = "$.toolName"
            });
        }

        if (string.IsNullOrWhiteSpace(output.Reason))
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "tool_decision.reason_required",
                Message = "Tool execution decisions must include a reason.",
                Path = "$.reason"
            });
        }

        if (output.Decision == AgentToolExecutionDecision.NeedsHumanApproval && output.Escalation is null)
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "tool_decision.escalation_required",
                Message = "Human-approval tool decisions must include an escalation request.",
                Path = "$.escalation"
            });
        }

        return errors.Count == 0 ? AgentOutputValidationResult.Success() : AgentOutputValidationResult.Failure([.. errors]);
    }
}

public sealed class HumanEscalationRequestValidator : IAgentOutputValidator<HumanEscalationRequest>
{
    public AgentOutputValidationResult Validate(HumanEscalationRequest output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var errors = new List<AgentOutputValidationError>();
        if (string.IsNullOrWhiteSpace(output.Reason))
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "human_escalation.reason_required",
                Message = "Human escalation requests must include a reason.",
                Path = "$.reason"
            });
        }

        if (string.IsNullOrWhiteSpace(output.RequestedRole))
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "human_escalation.role_required",
                Message = "Human escalation requests must include a requested role.",
                Path = "$.requestedRole"
            });
        }

        return errors.Count == 0 ? AgentOutputValidationResult.Success() : AgentOutputValidationResult.Failure([.. errors]);
    }
}
