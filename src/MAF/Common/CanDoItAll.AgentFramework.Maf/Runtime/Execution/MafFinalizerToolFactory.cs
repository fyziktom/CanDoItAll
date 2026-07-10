using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafFinalizerToolFactory
{
    private const string ProcessStepOutcomeArgumentDescription =
        "Final governed process-step outcome. Include status, reason, branchOutcomeKey, branchOutcomeTitle, evidenceRefs, nextActions, and humanReadableSummaryMarkdown. " +
        "branchOutcomeTitle requires a non-empty stable branchOutcomeKey declared by the current process brief. When no branch is selected, both branch fields must be empty strings. " +
        "Completed outcomes require at least one concrete current-run evidence reference.";

    public static FinalizerCapture? CreateCapture(
        AgentStructuredOutputContract? structuredOutput,
        AgentFinalizerMode finalizerMode)
    {
        if (finalizerMode == AgentFinalizerMode.Disabled ||
            !AgentFinalizerPolicies.TryResolveForStructuredOutput(structuredOutput, out var policy))
        {
            return null;
        }

        var capture = new FinalizerCapture(policy);
        var tool = CreateTool(capture, policy);
        if (tool is null)
        {
            return null;
        }

        capture.Tools.Add(tool);
        return capture;
    }

    private static AITool? CreateTool(FinalizerCapture capture, AgentFinalizerPolicy policy)
    {
        return policy.OutputType switch
        {
            Type type when type == typeof(ProcessStepOutcomeResult) => CreateProcessStepOutcomeTool(capture, policy),
            Type type when type == typeof(CodeReviewResult) => AIFunctionFactory.Create(
                capture.SubmitCodeReviewResult,
                policy.ToolName,
                "Submits the final code-review result exactly once as typed machine-readable arguments.",
                AgentOutputJson.SerializerOptions),
            Type type when type == typeof(ArchitectureReviewResult) => AIFunctionFactory.Create(
                capture.SubmitArchitectureReviewResult,
                policy.ToolName,
                "Submits the final architecture-review result exactly once as typed machine-readable arguments.",
                AgentOutputJson.SerializerOptions),
            Type type when type == typeof(ImplementationPlanResult) => AIFunctionFactory.Create(
                capture.SubmitImplementationPlan,
                policy.ToolName,
                "Submits the final implementation plan exactly once as typed machine-readable arguments.",
                AgentOutputJson.SerializerOptions),
            Type type when type == typeof(TestPlanResult) => AIFunctionFactory.Create(
                capture.SubmitTestPlan,
                policy.ToolName,
                "Submits the final test plan exactly once as typed machine-readable arguments.",
                AgentOutputJson.SerializerOptions),
            Type type when type == typeof(ToolExecutionDecisionResult) => AIFunctionFactory.Create(
                capture.SubmitToolExecutionDecision,
                policy.ToolName,
                "Submits the final tool-execution decision exactly once as typed machine-readable arguments.",
                AgentOutputJson.SerializerOptions),
            Type type when type == typeof(ProcessStatePatch) => AIFunctionFactory.Create(
                capture.SubmitProcessStatePatch,
                policy.ToolName,
                "Submits the final process-state patch exactly once as typed machine-readable arguments.",
                AgentOutputJson.SerializerOptions),
            Type type when type == typeof(HumanEscalationRequest) => AIFunctionFactory.Create(
                capture.SubmitHumanEscalationRequest,
                policy.ToolName,
                "Submits the final human-escalation request exactly once as typed machine-readable arguments.",
                AgentOutputJson.SerializerOptions),
            _ => null
        };
    }

    private static AIFunction CreateProcessStepOutcomeTool(
        FinalizerCapture capture,
        AgentFinalizerPolicy policy)
    {
        return AIFunctionFactory.Create(
            capture.SubmitProcessStepOutcome,
            new AIFunctionFactoryOptions
            {
                Name = policy.ToolName,
                Description = "Submits the final process-step outcome exactly once as typed machine-readable arguments.",
                SerializerOptions = AgentOutputJson.SerializerOptions,
                JsonSchemaCreateOptions = new AIJsonSchemaCreateOptions
                {
                    ParameterDescriptionProvider = static parameter =>
                        string.Equals(parameter.Name, "result", StringComparison.Ordinal)
                            ? ProcessStepOutcomeArgumentDescription
                            : null
                }
            });
    }
}
