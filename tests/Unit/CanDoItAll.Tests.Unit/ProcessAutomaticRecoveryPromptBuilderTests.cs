using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessAutomaticRecoveryPromptBuilderTests
{
    private static readonly ProcessRunId RunId = ProcessRunId.New();
    private static readonly ProcessInstancePlanId PlanId = ProcessInstancePlanId.New();
    private static readonly ProcessStepInstanceId StepId = ProcessStepInstanceId.New();

    [Fact]
    public void Build_LargeOriginalPrompt_KeepsRecoveryObligationAndContractsWithoutReplayingNoise()
    {
        var noise = string.Concat(Enumerable.Repeat("irrelevant-discovery-history ", 3000));
        var assignment = CreateAssignment($"""
            {noise}

            Step instructions:
            Repair the rejected completion gate in external-target/C/work/product.

            Input contract:
            Read artifacts/process-runs/{RunId.Value:D}/inputs/requirements.md.

            Output contract:
            Write artifacts/process-runs/{RunId.Value:D}/steps/code-change/implementation.md.

            Evidence contract:
            Include a successful current-execution mutation receipt.

            Available branch outcomes:
            - repaired
            - bughunt-required
            """);
        const string recoveryInstruction = """
            Runtime diagnostic rework instruction:
            The prior execution did not mutate the product. Run the prepared helper and verify its receipt.
            """;

        var prompt = ProcessAutomaticRecoveryPromptBuilder.Build(assignment, recoveryInstruction);

        Assert.StartsWith("Runtime diagnostic rework instruction:", prompt, StringComparison.Ordinal);
        Assert.Contains(ProcessAutomaticRecoveryPromptBuilder.ExecutionFocusHeading, prompt, StringComparison.Ordinal);
        Assert.Contains("The prior execution did not mutate the product", prompt, StringComparison.Ordinal);
        Assert.Contains($"run id: {RunId}", prompt, StringComparison.Ordinal);
        Assert.Contains("allowed operations: WorkspaceRead, WorkspaceWrite", prompt, StringComparison.Ordinal);
        Assert.Contains("external-target/C/work/product", prompt, StringComparison.Ordinal);
        Assert.Contains($"artifacts/process-runs/{RunId.Value:D}/inputs/requirements.md", prompt, StringComparison.Ordinal);
        Assert.Contains("Step instructions:", prompt, StringComparison.Ordinal);
        Assert.Contains("Evidence contract:", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("irrelevant-discovery-history", prompt, StringComparison.Ordinal);
        Assert.True(prompt.Length < assignment.Prompt.Length / 4);
    }

    [Fact]
    public void Build_LaunchContextExceedsBudget_PrioritizesGroundedReferencesAndReportsOmissions()
    {
        var launchVariables = Enumerable.Range(0, 40)
            .ToDictionary(
                index => $"Noise{index:D2}",
                index => new string((char)('a' + index % 20), 600),
                StringComparer.Ordinal);
        launchVariables["ProductRoot"] = "external-target/C/work/product";
        launchVariables["ManagedArtifact"] = $"artifacts/process-runs/{RunId.Value:D}/steps/code-change/implementation.md";
        var assignment = CreateAssignment("Unstructured prompt", launchVariables);

        var prompt = ProcessAutomaticRecoveryPromptBuilder.Build(
            assignment,
            "Runtime diagnostic rework instruction:\nRepair the rejected gate.");

        Assert.Contains("ProductRoot: external-target/C/work/product", prompt, StringComparison.Ordinal);
        Assert.Contains($"ManagedArtifact: artifacts/process-runs/{RunId.Value:D}/steps/code-change/implementation.md", prompt, StringComparison.Ordinal);
        Assert.Contains("lower-priority launch variable(s) omitted", prompt, StringComparison.Ordinal);
        Assert.True(prompt.Length < 15000);
    }

    [Fact]
    public void Build_HidesOrchestrationProvenanceFromRecoveryContext()
    {
        var assignment = CreateAssignment(
            "Repair the product.",
            new Dictionary<string, string>
            {
                ["BranchName"] = "memory-providers",
                ["RepositoryRoot"] = @"C:\repositories\CanDoItAll",
                ["AgentName"] = "Codex observer manager",
                ["ProductRootAlias"] = "external-target/C/work/product"
            });

        var prompt = ProcessAutomaticRecoveryPromptBuilder.Build(
            assignment,
            "Runtime diagnostic rework instruction:\nRepair the rejected gate.");

        Assert.Contains("ProductRootAlias: external-target/C/work/product", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("memory-providers", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\repositories\CanDoItAll", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Codex observer manager", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_MissingReceiptAndSourceInspectionDiagnostics_ProducesOrderedCompletionChecklist()
    {
        var assignment = CreateAssignment("""
            Step instructions:
            Validate the repaired interactive workflow.

            Evidence contract:
            Current-execution product source and browser proof are required.
            """);
        const string recoveryInstruction = """
            Runtime diagnostic rework instruction:
            process.adapter.product_required_tool_receipt_missing: Required current-run product tool receipt(s) are missing: browser_evaluate; workspace_dotnet_test.
            process.adapter.product_source_inspection_evidence_missing: Concrete product source was not read.
            process.adapter.runtime_lifecycle_correlation_missing: Runtime lifecycle proof was stale.
            """;

        var prompt = ProcessAutomaticRecoveryPromptBuilder.Build(assignment, recoveryInstruction);

        Assert.Contains(ProcessAutomaticRecoveryPromptBuilder.CompletionChecklistHeading, prompt, StringComparison.Ordinal);
        Assert.Contains("Invoke `browser_evaluate` successfully in this exact execution attempt.", prompt, StringComparison.Ordinal);
        Assert.Contains("Invoke `workspace_dotnet_test` successfully in this exact execution attempt.", prompt, StringComparison.Ordinal);
        Assert.Contains("Read concrete owning product source", prompt, StringComparison.Ordinal);
        Assert.Contains("Run `browser_evaluate` after the representative interaction and before stopping the runtime.", prompt, StringComparison.Ordinal);
        Assert.Contains("compare the checklist with the current execution's actual tool calls", prompt, StringComparison.Ordinal);
    }

    private static ProcessRuntimeStepAssignment CreateAssignment(
        string prompt,
        IReadOnlyDictionary<string, string>? launchVariables = null)
    {
        return new ProcessRuntimeStepAssignment(
            RunId,
            PlanId,
            StepId,
            "code-change",
            "developer",
            "developer",
            "Developer",
            ProcessLaunchExecutorKinds.Agent,
            "agent",
            "Agent",
            prompt,
            "sha256:readiness",
            "Test assignment",
            [ArtifactSlotId.New()],
            [ArtifactSlotId.New()],
            ["WorkspaceRead", "WorkspaceWrite"],
            "ExternalProductTargetReadWrite",
            launchVariables ?? new Dictionary<string, string>(),
            new ProcessRuntimeBranchGate("quality-check", "repair-required"),
            DateTimeOffset.UtcNow);
    }
}
