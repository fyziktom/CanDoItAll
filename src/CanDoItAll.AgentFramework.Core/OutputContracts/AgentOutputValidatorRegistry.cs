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
        var nextActions = output.NextActions;
        if (nextActions is null)
        {
            errors.Add(Error("process.step_outcome.next_actions_required", "Process step outcome next actions are required.", "$.nextActions"));
            nextActions = [];
        }

        var evidenceRefs = output.EvidenceRefs;
        if (evidenceRefs is null)
        {
            errors.Add(Error("process.step_outcome.evidence_refs_required", "Process step outcome evidence references are required.", "$.evidenceRefs"));
        }
        else if (output.Status == ProcessStepOutcomeStatus.Completed &&
                 !evidenceRefs.Any(evidenceRef => !string.IsNullOrWhiteSpace(evidenceRef)))
        {
            errors.Add(Error(
                "process.step_outcome.completed_evidence_ref_required",
                "Completed process outcomes must include at least one concrete current-run evidence reference.",
                "$.evidenceRefs"));
        }

        RequireText(output.Reason, "process.step_outcome.reason_required", "Process step outcome reason is required.", "$.reason", errors);

        if (output.Status == ProcessStepOutcomeStatus.Completed &&
            nextActions.Any(action => action.Contains("ask the user", StringComparison.OrdinalIgnoreCase) ||
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
            nextActions.Count == 0)
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "process.step_outcome.next_action_required",
                Message = "Failed or blocked process outcomes must include at least one next action.",
                Path = "$.nextActions"
            });
        }

        if (output.Status == ProcessStepOutcomeStatus.WaitingApproval &&
            nextActions.Count == 0)
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

    private static AgentOutputValidationError Error(string code, string message, string path)
        => new() { Code = code, Message = message, Path = path };
}

public sealed class CodeReviewResultValidator : IAgentOutputValidator<CodeReviewResult>
{
    public AgentOutputValidationResult Validate(CodeReviewResult output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var errors = new List<AgentOutputValidationError>();
        var findings = RequireCollection(output.Findings, "code_review.findings_required", "Code review findings are required.", "$.findings", errors);
        var requiredActions = RequireCollection(output.RequiredActions, "code_review.required_actions_required", "Code review required actions are required.", "$.requiredActions", errors);
        _ = RequireCollection(output.EvidenceRefs, "code_review.evidence_refs_required", "Code review evidence references are required.", "$.evidenceRefs", errors);

        if (output.Status == CodeReviewStatus.Passed && (findings.Count > 0 || requiredActions.Count > 0))
        {
            errors.Add(Error("code_review.passed_with_findings", "Passed code reviews must not contain findings or required actions.", "$.status"));
        }

        if (output.Status is (CodeReviewStatus.NeedsChanges or CodeReviewStatus.Failed) && findings.Count == 0)
        {
            errors.Add(Error("code_review.findings_required", "Code reviews that need changes or fail must include findings.", "$.findings"));
        }

        for (var index = 0; index < findings.Count; index++)
        {
            var finding = findings[index];
            if (finding is null)
            {
                errors.Add(Error("code_review.finding_required", "Code review finding entries must not be null.", $"$.findings[{index}]"));
                continue;
            }

            if (string.IsNullOrWhiteSpace(finding.Title) ||
                string.IsNullOrWhiteSpace(finding.Body) ||
                string.IsNullOrWhiteSpace(finding.FilePath))
            {
                errors.Add(Error("code_review.finding_detail_required", "Code review findings must include title, body, and file path.", $"$.findings[{index}]"));
            }
        }

        return ToResult(errors);
    }

    private static IReadOnlyList<T> RequireCollection<T>(
        IReadOnlyList<T>? values,
        string code,
        string message,
        string path,
        List<AgentOutputValidationError> errors)
    {
        if (values is not null)
        {
            return values;
        }

        errors.Add(Error(code, message, path));
        return [];
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
        var boundaryConcerns = RequireCollection(output.BoundaryConcerns, "architecture_review.boundary_concerns_required", "Architecture review boundary concerns are required.", "$.boundaryConcerns", errors);
        var requiredActions = RequireCollection(output.RequiredActions, "architecture_review.required_actions_required", "Architecture review required actions are required.", "$.requiredActions", errors);
        _ = RequireCollection(output.EvidenceRefs, "architecture_review.evidence_refs_required", "Architecture review evidence references are required.", "$.evidenceRefs", errors);

        if (output.Status == ArchitectureReviewStatus.Approved && requiredActions.Count > 0)
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "architecture_review.approved_with_required_actions",
                Message = "Approved architecture reviews must not include required actions.",
                Path = "$.requiredActions"
            });
        }

        if (output.Status is (ArchitectureReviewStatus.NeedsChanges or ArchitectureReviewStatus.Rejected) && boundaryConcerns.Count == 0)
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

    private static IReadOnlyList<T> RequireCollection<T>(
        IReadOnlyList<T>? values,
        string code,
        string message,
        string path,
        List<AgentOutputValidationError> errors)
    {
        if (values is not null)
        {
            return values;
        }

        errors.Add(new AgentOutputValidationError
        {
            Code = code,
            Message = message,
            Path = path
        });
        return [];
    }
}

public sealed class ImplementationPlanResultValidator : IAgentOutputValidator<ImplementationPlanResult>
{
    public AgentOutputValidationResult Validate(ImplementationPlanResult output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var errors = new List<AgentOutputValidationError>();
        var tasks = RequireCollection(output.Tasks, "implementation_plan.tasks_required", "Implementation plan tasks are required.", "$.tasks", errors);
        _ = RequireCollection(output.Risks, "implementation_plan.risks_required", "Implementation plan risks are required.", "$.risks", errors);
        _ = RequireCollection(output.EvidenceRefs, "implementation_plan.evidence_refs_required", "Implementation plan evidence references are required.", "$.evidenceRefs", errors);

        if (tasks.Count == 0)
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "implementation_plan.tasks_required",
                Message = "Implementation plans must include at least one task.",
                Path = "$.tasks"
            });
        }

        for (var index = 0; index < tasks.Count; index++)
        {
            var task = tasks[index];
            if (task is null)
            {
                errors.Add(new AgentOutputValidationError
                {
                    Code = "implementation_plan.task_required",
                    Message = "Implementation task entries must not be null.",
                    Path = $"$.tasks[{index}]"
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(task.Id) || string.IsNullOrWhiteSpace(task.Title))
            {
                errors.Add(new AgentOutputValidationError
                {
                    Code = "implementation_plan.task_identity_required",
                    Message = "Each implementation task must include an id and title.",
                    Path = $"$.tasks[{index}]"
                });
            }

            if (string.IsNullOrWhiteSpace(task.Description))
            {
                errors.Add(new AgentOutputValidationError
                {
                    Code = "implementation_plan.task_description_required",
                    Message = "Each implementation task must include a description.",
                    Path = $"$.tasks[{index}].description"
                });
            }

            _ = RequireCollection(task.OwnedPaths, "implementation_plan.task_owned_paths_required", "Each implementation task must include owned paths.", $"$.tasks[{index}].ownedPaths", errors);
            _ = RequireCollection(task.ValidationSteps, "implementation_plan.task_validation_steps_required", "Each implementation task must include validation steps.", $"$.tasks[{index}].validationSteps", errors);
        }

        return errors.Count == 0 ? AgentOutputValidationResult.Success() : AgentOutputValidationResult.Failure([.. errors]);
    }

    private static IReadOnlyList<T> RequireCollection<T>(
        IReadOnlyList<T>? values,
        string code,
        string message,
        string path,
        List<AgentOutputValidationError> errors)
    {
        if (values is not null)
        {
            return values;
        }

        errors.Add(new AgentOutputValidationError
        {
            Code = code,
            Message = message,
            Path = path
        });
        return [];
    }
}

public sealed class TestPlanResultValidator : IAgentOutputValidator<TestPlanResult>
{
    public AgentOutputValidationResult Validate(TestPlanResult output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var errors = new List<AgentOutputValidationError>();
        var testCases = RequireCollection(output.TestCases, "test_plan.test_cases_required", "Test plan test cases are required.", "$.testCases", errors);
        _ = RequireCollection(output.CoverageGaps, "test_plan.coverage_gaps_required", "Test plan coverage gaps are required.", "$.coverageGaps", errors);
        _ = RequireCollection(output.EvidenceRefs, "test_plan.evidence_refs_required", "Test plan evidence references are required.", "$.evidenceRefs", errors);

        if (output.Status == TestPlanStatus.Ready && testCases.Count == 0)
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "test_plan.test_cases_required",
                Message = "Ready test plans must include test cases.",
                Path = "$.testCases"
            });
        }

        return errors.Count == 0 ? AgentOutputValidationResult.Success() : AgentOutputValidationResult.Failure([.. errors]);
    }

    private static IReadOnlyList<T> RequireCollection<T>(
        IReadOnlyList<T>? values,
        string code,
        string message,
        string path,
        List<AgentOutputValidationError> errors)
    {
        if (values is not null)
        {
            return values;
        }

        errors.Add(new AgentOutputValidationError
        {
            Code = code,
            Message = message,
            Path = path
        });
        return [];
    }
}

public sealed class ToolExecutionDecisionResultValidator : IAgentOutputValidator<ToolExecutionDecisionResult>
{
    public AgentOutputValidationResult Validate(ToolExecutionDecisionResult output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var errors = new List<AgentOutputValidationError>();
        if (output.EvidenceRefs is null)
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "tool_decision.evidence_refs_required",
                Message = "Tool execution decisions must include evidence references.",
                Path = "$.evidenceRefs"
            });
        }

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
        else if (output.Escalation is not null)
        {
            errors.AddRange(new HumanEscalationRequestValidator().Validate(output.Escalation).Errors);
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
        if (output.ValidationErrors is null)
        {
            errors.Add(new AgentOutputValidationError
            {
                Code = "human_escalation.validation_errors_required",
                Message = "Human escalation requests must include validation errors.",
                Path = "$.validationErrors"
            });
        }

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
