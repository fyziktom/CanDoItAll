using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit;

/// <summary>
/// Characterizes the policy-driven <see cref="MafFinalizerToolFactory"/> contract for all finalizers. Strictly
/// typed finalizers retain their generated schemas, while tolerant finalizers capture a raw <see cref="JsonElement"/>
/// but advertise their policy output type's full nested schema.
/// </summary>
public sealed class MafFinalizerToolFactorySchemaCharacterizationTests
{
    public static TheoryData<AgentStructuredOutputContract, string, string, string> Contracts()
    {
        var data = new TheoryData<AgentStructuredOutputContract, string, string, string>();
        foreach (var (contract, expectedName, expectedDescription, expectedSchema) in ExpectedBaselines())
        {
            data.Add(contract, expectedName, expectedDescription, expectedSchema);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Contracts))]
    public void CreateCapture_produces_the_policy_tool_name_description_and_json_schema(
        AgentStructuredOutputContract contract,
        string expectedName,
        string expectedDescription,
        string expectedSchema)
    {
        // Ensures AgentOutputJson.SerializerOptions already has a resolver attached before AIFunctionFactory calls
        // JsonSerializerOptions.MakeReadOnly() on it; otherwise an isolated run of only this test class throws
        // before reaching production code (pre-existing environment quirk, unrelated to SB13).
        JsonSerializer.Serialize(
            new ProcessStepOutcomeResult { Status = ProcessStepOutcomeStatus.Completed, Reason = string.Empty },
            AgentOutputJson.SerializerOptions);

        var capture = Assert.IsType<FinalizerCapture>(
            MafFinalizerToolFactory.CreateCapture(contract, AgentFinalizerMode.Required));
        var function = Assert.IsAssignableFrom<AIFunction>(Assert.Single(capture!.Tools));

        Assert.Equal(expectedName, function.Name);
        Assert.Equal(expectedDescription, function.Description);
        Assert.Equal(expectedSchema, function.JsonSchema.GetRawText());
    }

    [Fact]
    public async Task CreateCapture_rejects_every_noncanonical_argument_set_before_binding_for_all_finalizers()
    {
        foreach (var (contract, _, _, _) in ExpectedBaselines())
        {
            var capture = Assert.IsType<FinalizerCapture>(
                MafFinalizerToolFactory.CreateCapture(contract, AgentFinalizerMode.Required));
            var function = Assert.IsAssignableFrom<AIFunction>(Assert.Single(capture.Tools));
            var invalidArgumentSets = new AIFunctionArguments[]
            {
                [],
                new() { ["status"] = "Completed" },
                new()
                {
                    ["result"] = JsonSerializer.SerializeToElement(new { }),
                    ["acceptanceCriteriaEvidence"] = JsonSerializer.SerializeToElement(Array.Empty<object>())
                }
            };

            foreach (var arguments in invalidArgumentSets)
            {
                var response = Assert.IsType<FinalizerSubmissionResult>(
                    await function.InvokeAsync(arguments, CancellationToken.None));

                Assert.False(response.Succeeded);
                Assert.Contains("expected exactly one argument named `result`", response.Message, StringComparison.Ordinal);
                Assert.False(MafRuntimeToolInvocationResultClassifier.IsSuccessful(response));
                Assert.Equal(response.Message, MafRuntimeToolInvocationResultClassifier.ResolveFailureMessage(response));
            }

            Assert.Empty(capture.Snapshot());
        }
    }

    [Fact]
    public async Task Process_step_tolerant_finalizer_with_exact_result_captures_acceptance_criteria_evidence()
    {
        var capture = Assert.IsType<FinalizerCapture>(MafFinalizerToolFactory.CreateCapture(
            AgentStructuredOutputContracts.ProcessStepOutcomeResult,
            AgentFinalizerMode.Required));
        var function = Assert.IsAssignableFrom<AIFunction>(Assert.Single(capture.Tools));
        var outcome = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Current-run validation found a defect.",
            BranchOutcomeKey = "repair-required",
            BranchOutcomeTitle = "Repair required",
            EvidenceRefs = ["artifacts/process-runs/run-1/steps/qa.md"],
            AcceptanceCriteriaEvidence =
            [
                new ProcessAcceptanceCriterionEvidence
                {
                    CriterionId = "AC-002",
                    Status = ProcessAcceptanceCriterionEvidenceStatus.Failed,
                    Summary = "The browser exposed a runtime error surface.",
                    EvidenceRefs = ["artifacts/process-runs/run-1/browser/after.yml"]
                }
            ]
        };
        var arguments = new AIFunctionArguments
        {
            ["result"] = JsonSerializer.SerializeToElement(outcome, AgentOutputJson.SerializerOptions)
        };

        var response = Assert.IsType<JsonElement>(
            await function.InvokeAsync(arguments, CancellationToken.None));

        Assert.True(response.GetProperty("succeeded").GetBoolean());
        var invocation = Assert.Single(capture.Snapshot());
        var validation = new DefaultAgentFinalizerValidator().Validate(capture.Policy, [invocation]);
        Assert.True(validation.Succeeded);
        var captured = Assert.IsType<ProcessStepOutcomeResult>(validation.Output);
        var criterion = Assert.Single(captured.AcceptanceCriteriaEvidence);
        Assert.Equal("AC-002", criterion.CriterionId);
        Assert.Equal(ProcessAcceptanceCriterionEvidenceStatus.Failed, criterion.Status);
    }

    private static IEnumerable<(AgentStructuredOutputContract Contract, string Name, string Description, string Schema)> ExpectedBaselines()
    {
        yield return (
            AgentStructuredOutputContracts.ProcessStepOutcomeResult,
            "submit_process_step_outcome",
            "Submits the final process-step outcome exactly once as typed machine-readable arguments.",
            """{"type":"object","properties":{"result":{"description":"Final governed process-step outcome. Include status, reason, branchOutcomeKey, branchOutcomeTitle, evidenceRefs, acceptanceCriteriaEvidence, nextActions, and humanReadableSummaryMarkdown. acceptanceCriteriaEvidence must be an array whose entries use criterionId, status, summary, and evidenceRefs. branchOutcomeTitle requires a non-empty stable branchOutcomeKey declared by the current process brief. When no branch is selected, both branch fields must be empty strings. Completed outcomes require at least one concrete current-run evidence reference.","type":"object","properties":{"status":{"type":"string","enum":["Completed","Blocked","Failed","WaitingApproval","Refused"]},"reason":{"type":"string"},"branchOutcomeKey":{"type":"string"},"branchOutcomeTitle":{"type":"string"},"evidenceRefs":{"type":"array","items":{"type":"string"}},"acceptanceCriteriaEvidence":{"type":"array","items":{"type":"object","properties":{"criterionId":{"type":"string"},"status":{"type":"string","enum":["Passed","Failed","NotVerified"]},"summary":{"type":"string"},"evidenceRefs":{"type":"array","items":{"type":"string"}}},"required":["criterionId","status","summary"]}},"nextActions":{"type":"array","items":{"type":"string"}},"humanReadableSummaryMarkdown":{"type":["string","null"]}},"required":["status","reason"]}},"required":["result"]}""");

        yield return (
            AgentStructuredOutputContracts.CodeReviewResult,
            "submit_code_review_result",
            "Submits the final code-review result exactly once as typed machine-readable arguments.",
            """{"type":"object","properties":{"result":{"type":"object","properties":{"status":{"type":"string","enum":["Passed","NeedsChanges","Failed","Blocked"]},"findings":{"type":"array","items":{"type":"object","properties":{"title":{"type":"string"},"body":{"type":"string"},"filePath":{"type":"string"},"startLine":{"type":["integer","null"]},"endLine":{"type":["integer","null"]},"severity":{"type":["string","null"]}},"required":["title","body","filePath"]}},"requiredActions":{"type":"array","items":{"type":"string"}},"evidenceRefs":{"type":"array","items":{"type":"string"}},"humanReadableSummaryMarkdown":{"type":["string","null"]}},"required":["status","findings","requiredActions","evidenceRefs"]}},"required":["result"]}""");

        yield return (
            AgentStructuredOutputContracts.ArchitectureReviewResult,
            "submit_architecture_review_result",
            "Submits the final architecture-review result exactly once as typed machine-readable arguments.",
            """{"type":"object","properties":{"result":{"type":"object","properties":{"status":{"type":"string","enum":["Approved","NeedsChanges","Rejected","Blocked"]},"boundaryConcerns":{"type":"array","items":{"type":"string"}},"requiredActions":{"type":"array","items":{"type":"string"}},"evidenceRefs":{"type":"array","items":{"type":"string"}},"humanReadableSummaryMarkdown":{"type":["string","null"]}},"required":["status","boundaryConcerns","requiredActions","evidenceRefs"]}},"required":["result"]}""");

        yield return (
            AgentStructuredOutputContracts.ImplementationPlanResult,
            "submit_implementation_plan",
            "Submits the final implementation plan exactly once as typed machine-readable arguments.",
            """{"type":"object","properties":{"result":{"type":"object","properties":{"tasks":{"type":"array","items":{"type":"object","properties":{"id":{"type":"string"},"title":{"type":"string"},"description":{"type":"string"},"ownedPaths":{"type":"array","items":{"type":"string"}},"validationSteps":{"type":"array","items":{"type":"string"}}},"required":["id","title","description","ownedPaths","validationSteps"]}},"risks":{"type":"array","items":{"type":"string"}},"evidenceRefs":{"type":"array","items":{"type":"string"}},"humanReadableSummaryMarkdown":{"type":["string","null"]}},"required":["tasks","risks","evidenceRefs"]}},"required":["result"]}""");

        yield return (
            AgentStructuredOutputContracts.TestPlanResult,
            "submit_test_plan",
            "Submits the final test plan exactly once as typed machine-readable arguments.",
            """{"type":"object","properties":{"result":{"type":"object","properties":{"status":{"type":"string","enum":["Ready","NeedsChanges","Blocked"]},"testCases":{"type":"array","items":{"type":"string"}},"coverageGaps":{"type":"array","items":{"type":"string"}},"evidenceRefs":{"type":"array","items":{"type":"string"}},"humanReadableSummaryMarkdown":{"type":["string","null"]}},"required":["status","testCases","coverageGaps","evidenceRefs"]}},"required":["result"]}""");

        yield return (
            AgentStructuredOutputContracts.ToolExecutionDecisionResult,
            "submit_tool_execution_decision",
            "Submits the final tool-execution decision exactly once as typed machine-readable arguments.",
            """{"type":"object","properties":{"result":{"type":"object","properties":{"decision":{"type":"string","enum":["Allow","Deny","NeedsHumanApproval"]},"toolName":{"type":"string"},"reason":{"type":"string"},"evidenceRefs":{"type":"array","items":{"type":"string"}},"escalation":{"type":["object","null"],"properties":{"reason":{"type":"string"},"requestedRole":{"type":"string"},"validationErrors":{"type":"array","items":{"type":"object","properties":{"code":{"type":"string"},"message":{"type":"string"},"path":{"type":["string","null"]},"severity":{"type":"string","enum":["Info","Warning","Error","Critical"]}},"required":["code","message"]}},"processInstanceId":{"type":["string","null"]},"stepId":{"type":["string","null"]},"humanReadableSummaryMarkdown":{"type":["string","null"]}},"required":["reason","requestedRole","validationErrors"]}},"required":["decision","toolName","reason","evidenceRefs"]}},"required":["result"]}""");

        yield return (
            AgentStructuredOutputContracts.ProcessStatePatch,
            "submit_process_state_patch",
            "Submits the final process-state patch exactly once as typed machine-readable arguments.",
            """{"type":"object","properties":{"result":{"type":"object","properties":{"operations":{"type":"array","items":{"type":"object","properties":{"op":{"type":"string","enum":["Add","Replace","Remove"]},"path":{"type":"string"},"value":true,"reason":{"type":"string"}},"required":["op","path","reason"]}}},"required":["operations"]}},"required":["result"]}""");

        yield return (
            AgentStructuredOutputContracts.HumanEscalationRequest,
            "submit_human_escalation_request",
            "Submits the final human-escalation request exactly once as typed machine-readable arguments.",
            """{"type":"object","properties":{"result":{"type":"object","properties":{"reason":{"type":"string"},"requestedRole":{"type":"string"},"validationErrors":{"type":"array","items":{"type":"object","properties":{"code":{"type":"string"},"message":{"type":"string"},"path":{"type":["string","null"]},"severity":{"type":"string","enum":["Info","Warning","Error","Critical"]}},"required":["code","message"]}},"processInstanceId":{"type":["string","null"]},"stepId":{"type":["string","null"]},"humanReadableSummaryMarkdown":{"type":["string","null"]}},"required":["reason","requestedRole","validationErrors"]}},"required":["result"]}""");
    }
}
