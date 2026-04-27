using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentOutputContractTests
{
    [Fact]
    public void ProcessStepOutcomeResult_serializes_enum_as_string()
    {
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Implementation and validation completed.",
            EvidenceRefs = ["artifact://implementation-plan"],
            NextActions = [],
            HumanReadableSummaryMarkdown = "Completed."
        };

        var json = JsonSerializer.Serialize(output, AgentOutputJson.SerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<ProcessStepOutcomeResult>(json, AgentOutputJson.SerializerOptions);

        Assert.Contains("\"status\":\"Completed\"", json, StringComparison.Ordinal);
        Assert.NotNull(roundTripped);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, roundTripped.Status);
        Assert.Equal("Implementation and validation completed.", roundTripped.Reason);
    }

    [Fact]
    public void AgentOutputJson_rejects_markdown_comment_as_machine_contract()
    {
        var result = AgentOutputJson.DeserializeAndValidate(
            "Review complete. <!-- PROCESS_STEP_OUTCOME {\"status\":\"Completed\",\"reason\":\"looks good\"} -->",
            new AlwaysValidProcessOutcomeValidator());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Validation.Errors, error => error.Code == "agent.output.malformed_json");
    }

    [Fact]
    public void AgentOutputJson_revalidates_deserialized_output()
    {
        var json = JsonSerializer.Serialize(
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Failed,
                Reason = "",
                EvidenceRefs = [],
                NextActions = []
            },
            AgentOutputJson.SerializerOptions);

        var result = AgentOutputJson.DeserializeAndValidate(json, new ReasonRequiredProcessOutcomeValidator());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Validation.Errors, error => error.Code == "reason_required");
    }

    [Fact]
    public void ProcessStatePatchValidator_rejects_protected_path_mutation()
    {
        var patch = new ProcessStatePatch
        {
            Operations =
            [
                new ProcessPatchOperation
                {
                    Op = ProcessPatchOperationKind.Replace,
                    Path = "/system/owner",
                    Value = JsonDocument.Parse("\"attacker\"").RootElement.Clone(),
                    Reason = "Change owner."
                }
            ]
        };
        var validator = new ProcessStatePatchValidator(
            new HashSet<string>(StringComparer.Ordinal) { "/system", "/steps" },
            new HashSet<string>(StringComparer.Ordinal) { "/system/owner" });

        var result = validator.Validate(patch);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "process.patch.path_protected");
    }

    [Fact]
    public void ProcessStepOutcomeValidator_accepts_completed_machine_outcome()
    {
        var validator = new ProcessStepOutcomeValidator();
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "The implementation was completed and validated.",
            EvidenceRefs = ["execution://run-001"],
            NextActions = [],
            HumanReadableSummaryMarkdown = "Completed."
        };

        var result = validator.Validate(output);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ProcessStepOutcomeValidator_rejects_completed_outcome_that_asks_for_human_input()
    {
        var validator = new ProcessStepOutcomeValidator();
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "The implementation was completed.",
            EvidenceRefs = [],
            NextActions = ["Ask the user what to do next."]
        };

        var result = validator.Validate(output);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "process.step_outcome.completed_next_action_inconsistent");
    }

    [Fact]
    public void ProcessStepOutcomeValidator_requires_next_action_for_blocked_or_failed_outcome()
    {
        var validator = new ProcessStepOutcomeValidator();
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Blocked,
            Reason = "A required approval is missing.",
            EvidenceRefs = [],
            NextActions = []
        };

        var result = validator.Validate(output);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "process.step_outcome.next_action_required");
    }

    [Fact]
    public void DefaultAgentOutputValidatorRegistry_resolves_process_step_outcome_contract()
    {
        var resolved = DefaultAgentOutputValidatorRegistry.Instance.TryResolve(
            typeof(ProcessStepOutcomeResult),
            out var validator);

        Assert.True(resolved);
        Assert.Equal(typeof(ProcessStepOutcomeResult), validator.OutputType);
    }

    [Fact]
    public void List_outputs_are_wrapped_in_object_dtos()
    {
        var plan = new ImplementationPlanResult
        {
            Tasks =
            [
                new ImplementationTask
                {
                    Id = "task-1",
                    Title = "Add validator",
                    Description = "Add typed validation.",
                    OwnedPaths = ["src/CanDoItAll.AgentFramework.Core"],
                    ValidationSteps = ["dotnet test"]
                }
            ],
            Risks = [],
            EvidenceRefs = ["source://request"]
        };

        var json = JsonSerializer.Serialize(plan, AgentOutputJson.SerializerOptions);

        Assert.StartsWith("{", json, StringComparison.Ordinal);
        Assert.Contains("\"tasks\"", json, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => AgentStructuredOutputContract.For<IReadOnlyList<ImplementationTask>>());
    }

    private sealed class AlwaysValidProcessOutcomeValidator : IAgentOutputValidator<ProcessStepOutcomeResult>
    {
        public AgentOutputValidationResult Validate(ProcessStepOutcomeResult output)
            => AgentOutputValidationResult.Success();
    }

    private sealed class ReasonRequiredProcessOutcomeValidator : IAgentOutputValidator<ProcessStepOutcomeResult>
    {
        public AgentOutputValidationResult Validate(ProcessStepOutcomeResult output)
        {
            return string.IsNullOrWhiteSpace(output.Reason)
                ? AgentOutputValidationResult.Failure(new AgentOutputValidationError
                {
                    Code = "reason_required",
                    Message = "Reason is required.",
                    Path = "$.reason"
                })
                : AgentOutputValidationResult.Success();
        }
    }
}
