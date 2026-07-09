using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessStepRecoveryInstructionBuilderTests
{
    private static readonly ProcessRunId RunId = ProcessRunId.New();
    private static readonly ProcessInstancePlanId PlanId = ProcessInstancePlanId.New();
    private static readonly ProcessStepInstanceId StepId = ProcessStepInstanceId.New();

    [Fact]
    public void RecoveryInstructionBuilder_dotnet_create_project_mentions_missing_pwsh_receipt_and_resolved_script_ref()
    {
        var assignment = CreateDotNetAssignment();
        var result = CreateIncidentResult();
        var receipt = CreateReceipt(result, CreateSafeRetryDecision());

        var instruction = ProcessStepRecoveryInstructionBuilder.Instance.Build(new ProcessStepRecoveryInstructionBuildRequest(
            RunId,
            StepId,
            assignment.StepKey,
            assignment,
            result,
            receipt,
            OperatorReason: string.Empty));

        Assert.True(instruction.HasInstruction);
        Assert.Contains("workspace_pwsh_run_script", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("artifacts/process-runs/", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("scripts/create-dotnet-project.ps1", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("Calculator.slnx", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("src/Calculator/Calculator.csproj", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_new", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("Do not rerun", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("SafeRetry/CurrentStepRetry", instruction.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void RecoveryInstructionBuilder_does_not_include_unresolved_current_process_run_id_placeholder()
    {
        var assignment = CreateDotNetAssignment() with
        {
            LaunchVariables = CreateDotNetLaunchVariables(
                "artifacts/process-runs/{CurrentProcessRunId}/scripts/create-dotnet-project.ps1")
        };
        var result = CreateIncidentResult();

        var instruction = ProcessStepRecoveryInstructionBuilder.Instance.Build(new ProcessStepRecoveryInstructionBuildRequest(
            RunId,
            StepId,
            assignment.StepKey,
            assignment,
            result,
            CreateReceipt(result, CreateSafeRetryDecision()),
            OperatorReason: string.Empty));

        Assert.True(instruction.HasInstruction);
        Assert.DoesNotContain("{CurrentProcessRunId}", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("Resolved DotNetCreateProjectScriptRef is unavailable", instruction.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void RecoveryInstructionBuilder_budget_exhausted_packet_includes_attempted_repair_plan()
    {
        var assignment = CreateDotNetAssignment();
        var result = CreateIncidentResult();

        var instruction = ProcessStepRecoveryInstructionBuilder.Instance.Build(new ProcessStepRecoveryInstructionBuildRequest(
            RunId,
            StepId,
            assignment.StepKey,
            assignment,
            result,
            CreateReceipt(result, CreateBudgetExhaustedDecision()),
            OperatorReason: string.Empty));

        Assert.True(instruction.HasInstruction);
        Assert.Contains("Safe retry budget is exhausted", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("workspace_pwsh_run_script", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("scripts/create-dotnet-project.ps1", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("Only then rewrite", instruction.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void RecoveryInstructionBuilder_ungrounded_reference_packet_does_not_use_dotnet_setup_guidance()
    {
        var assignment = CreatePeerReviewAssignment();
        var result = CreateUngroundedReferenceResult();
        var receipt = CreateReceipt(
            result,
            CreateSafeRetryDecision("process.adapter.ungrounded_outcome_reference"));

        var instruction = ProcessStepRecoveryInstructionBuilder.Instance.Build(new ProcessStepRecoveryInstructionBuildRequest(
            RunId,
            StepId,
            assignment.StepKey,
            assignment,
            result,
            receipt,
            OperatorReason: string.Empty));

        Assert.True(instruction.HasInstruction);
        Assert.Contains("Ungrounded path-like reference repair", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("The rejected literal ref is intentionally withheld", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("reason, summary, next actions, or evidenceRefs", instruction.Text, StringComparison.Ordinal);
        Assert.Contains($"artifacts/process-runs/{RunId.Value:D}/steps/peer-review.md", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("current-run workspace tool receipt refs", instruction.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Read back the solution", instruction.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("workspace_pwsh_run_script", instruction.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("helper runs", instruction.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void RecoveryInstructionBuilder_qa_product_readback_failure_selects_repair_required_branch()
    {
        var assignment = CreateQaValidationAssignment();
        var result = CreateQaProductReadbackResult();
        var receipt = CreateReceipt(
            result,
            CreateSafeRetryDecision("process.adapter.product_required_file_content_missing"));

        var instruction = ProcessStepRecoveryInstructionBuilder.Instance.Build(new ProcessStepRecoveryInstructionBuildRequest(
            RunId,
            StepId,
            assignment.StepKey,
            assignment,
            result,
            receipt,
            OperatorReason: string.Empty));

        Assert.True(instruction.HasInstruction);
        Assert.Contains("product content/readback check failed", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("Do not return Blocked", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("branchOutcomeKey 'repair-required'", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("external-target/C/programovani/dotnet/output/src/TetrisGame/Layout/NavMenu.razor", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("href=\"counter\"", instruction.Text, StringComparison.Ordinal);
        Assert.Contains($"artifacts/process-runs/{RunId.Value:D}/steps/qa-validation.md", instruction.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\programovani", instruction.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Read back the solution or product output after the helper runs", instruction.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("verify the required membership/content check passes", instruction.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("workspace_pwsh_run_script", instruction.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void RecoveryInstructionBuilder_qa_recheck_missing_receipts_preserves_branch_contract()
    {
        var assignment = CreateQaValidationAssignment("qa-recheck");
        var result = CreateQaRecheckMissingReceiptResult();
        var receipt = CreateReceipt(
            result,
            CreateBudgetExhaustedDecision("process.adapter.product_required_tool_receipt_missing"));

        var instruction = ProcessStepRecoveryInstructionBuilder.Instance.Build(new ProcessStepRecoveryInstructionBuildRequest(
            RunId,
            StepId,
            assignment.StepKey,
            assignment,
            result,
            receipt,
            OperatorReason: string.Empty));

        Assert.True(instruction.HasInstruction);
        Assert.Contains("QA current-run validation receipt repair", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_restore with restore target external-target/C/programovani/dotnet/output/TetrisGame.slnx", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_build with build target external-target/C/programovani/dotnet/output/TetrisGame.slnx", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_test with test target external-target/C/programovani/dotnet/output/tests/TetrisGame.Tests/TetrisGame.Tests.csproj", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_run with run target external-target/C/programovani/dotnet/output/src/TetrisGame/TetrisGame.csproj", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("browser_snapshot", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_stop", instruction.Text, StringComparison.Ordinal);
        Assert.Contains("branchOutcomeKey 'quality-accepted' or 'repair-escalation'", instruction.Text, StringComparison.Ordinal);
        Assert.Contains($"artifacts/process-runs/{RunId.Value:D}/steps/qa-recheck.md", instruction.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Only then rewrite", instruction.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("and submit Completed", instruction.Text, StringComparison.Ordinal);
    }

    private static ProcessRuntimeStepAssignment CreateDotNetAssignment()
        => new(
            RunId,
            PlanId,
            StepId,
            "create-dotnet-project",
            "dotnet-developer",
            "dotnet-developer",
            ".NET developer",
            ProcessLaunchExecutorKinds.Agent,
            "agent",
            "Agent",
            "Create the .NET project.",
            "sha256:readiness",
            "test",
            [],
            [],
            ["MutateProductTarget"],
            "ExternalProductTargetMutable",
            CreateDotNetLaunchVariables($"artifacts/process-runs/{RunId.Value:D}/scripts/create-dotnet-project.ps1"),
            BranchGate: null,
            DateTimeOffset.UtcNow);

    private static ProcessRuntimeStepAssignment CreatePeerReviewAssignment()
        => new(
            RunId,
            PlanId,
            StepId,
            "peer-review",
            "lead-engineer",
            "lead-engineer",
            "Lead engineer",
            ProcessLaunchExecutorKinds.Agent,
            "agent",
            "Agent",
            "Review the change set and write the peer review note.",
            "sha256:readiness",
            "test",
            [],
            [],
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            ProcessOperationContractNames.ExternalProductTargetReadOnly,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ExternalTargetRoot"] = "external-target/C/programovani/dotnet/output",
                ["WorkspaceAlias"] = "external-target/C/programovani/dotnet/output"
            },
            BranchGate: null,
            DateTimeOffset.UtcNow);

    private static ProcessRuntimeStepAssignment CreateQaValidationAssignment(string stepKey = "qa-validation")
        => new(
            RunId,
            PlanId,
            StepId,
            stepKey,
            "qa-lead",
            "qa-lead",
            "QA lead",
            ProcessLaunchExecutorKinds.Agent,
            "agent",
            "Agent",
            "Run QA validation and choose a branch outcome.",
            "sha256:readiness",
            "test",
            [],
            [],
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            ProcessOperationContractNames.ExternalProductTargetReadOnly,
            CreateQaLaunchVariables(),
            BranchGate: null,
            DateTimeOffset.UtcNow);

    private static IReadOnlyDictionary<string, string> CreateDotNetLaunchVariables(string scriptRef)
    {
        var requiredFileContentChecks = JsonSerializer.Serialize(new object[]
        {
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["pathCandidates"] = new[] { "Calculator.slnx" },
                ["requiredTextAnyGroups"] = new[]
                {
                    new[] { "src/Calculator/Calculator.csproj" }
                }
            }
        });

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] = "workspace_pwsh_run_script",
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] = requiredFileContentChecks,
            ["DotNetCreateProjectScript"] = "$ErrorActionPreference = 'Stop'",
            ["DotNetCreateProjectScriptRef"] = scriptRef,
            ["DotNetCreateProjectSideEffectManifest"] = """{"version":1,"mode":"ProductMutation"}""",
            ["WorkspaceAlias"] = "external-target/C/repositories/calculator"
        };
    }

    private static IReadOnlyDictionary<string, string> CreateQaLaunchVariables()
    {
        var requiredFileContentChecks = JsonSerializer.Serialize(new object[]
        {
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["pathCandidates"] = new[] { @"C:\programovani\dotnet\output\src\TetrisGame\Layout\NavMenu.razor" },
                ["forbiddenTextAny"] = new[] { "href=\"counter\"", "href=\"weather\"" },
                ["description"] = "TetrisGame visible UI must not ship default template scaffold content."
            }
        });

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] = requiredFileContentChecks,
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] = string.Join(
                '\n',
                [
                    "workspace_dotnet_restore",
                    "workspace_dotnet_build",
                    "workspace_dotnet_test",
                    "workspace_dotnet_run",
                    "browser_navigate",
                    "browser_snapshot",
                    "browser_take_screenshot",
                    "browser_console_messages",
                    "workspace_dotnet_stop"
                ]),
            ["DotNetSolutionFileAlias"] = "external-target/C/programovani/dotnet/output/TetrisGame.slnx",
            ["DotNetTestProjectFileAlias"] = "external-target/C/programovani/dotnet/output/tests/TetrisGame.Tests/TetrisGame.Tests.csproj",
            ["DotNetAppProjectFileAlias"] = "external-target/C/programovani/dotnet/output/src/TetrisGame/TetrisGame.csproj",
            ["ProductRoot"] = @"C:\programovani\dotnet\output",
            ["ProductRootAlias"] = "external-target/C/programovani/dotnet/output",
            ["WorkspaceAlias"] = "external-target/C/programovani/dotnet/output"
        };
    }

    private static StrategyResultEnvelope CreateIncidentResult()
    {
        return new StrategyResultEnvelope(
            new StrategyId("strategy.execute"),
            "1.0.0",
            Guid.NewGuid(),
            StrategyOutcome.NeedsManager,
            [],
            [],
            [
                new StrategyDiagnosticRef(
                    new StrategyDiagnosticCode("process.adapter.product_required_tool_receipt_missing"),
                    StrategyDiagnosticSensitivity.Normal,
                    "sha256:receipt",
                    "Step 'create-dotnet-project' claimed completion but required current-run product tool receipt(s) are missing: workspace_pwsh_run_script.",
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.SafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent),
                new StrategyDiagnosticRef(
                    new StrategyDiagnosticCode("process.adapter.product_required_file_content_missing"),
                    StrategyDiagnosticSensitivity.Normal,
                    "sha256:readback",
                    "Step 'create-dotnet-project' claimed completion but required product file content/readback check(s) failed: Calculator.slnx does not contain src/Calculator/Calculator.csproj.",
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.SafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [
                new ManagerSignal(
                    new ManagerSignalCode("process.adapter.product_required_tool_receipt_missing"),
                    "sha256:receipt",
                    "Missing receipt.")
            ],
            "sha256:incident");
    }

    private static StrategyResultEnvelope CreateUngroundedReferenceResult()
    {
        return new StrategyResultEnvelope(
            new StrategyId("strategy.execute"),
            "1.0.0",
            Guid.NewGuid(),
            StrategyOutcome.NeedsManager,
            [],
            [],
            [
                new StrategyDiagnosticRef(
                    new StrategyDiagnosticCode("process.adapter.ungrounded_outcome_reference"),
                    StrategyDiagnosticSensitivity.Normal,
                    "sha256:ungrounded",
                    "Step 'peer-review' claimed completion but cited 1 ungrounded path-like ref.",
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.SafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [
                new ManagerSignal(
                    new ManagerSignalCode("process.adapter.ungrounded_outcome_reference"),
                    "sha256:ungrounded",
                    "Ungrounded outcome reference.")
            ],
            "sha256:ungrounded");
    }

    private static StrategyResultEnvelope CreateQaProductReadbackResult()
    {
        return new StrategyResultEnvelope(
            new StrategyId("strategy.execute"),
            "1.0.0",
            Guid.NewGuid(),
            StrategyOutcome.NeedsManager,
            [],
            [],
            [
                new StrategyDiagnosticRef(
                    new StrategyDiagnosticCode("process.adapter.product_required_file_content_missing"),
                    StrategyDiagnosticSensitivity.Normal,
                    "sha256:qa-readback",
                    @"Step 'qa-validation' claimed completion but required product file content/readback check(s) failed: C:\programovani\dotnet\output\src\TetrisGame\Layout\NavMenu.razor contains forbidden text [href=""counter""].",
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.SafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [
                new ManagerSignal(
                    new ManagerSignalCode("process.adapter.product_required_file_content_missing"),
                    "sha256:qa-readback",
                    "Product content readback failed.")
            ],
            "sha256:qa-readback");
    }

    private static StrategyResultEnvelope CreateQaRecheckMissingReceiptResult()
    {
        return new StrategyResultEnvelope(
            new StrategyId("strategy.execute"),
            "1.0.0",
            Guid.NewGuid(),
            StrategyOutcome.NeedsManager,
            [],
            [],
            [
                new StrategyDiagnosticRef(
                    new StrategyDiagnosticCode("process.adapter.product_required_tool_receipt_missing"),
                    StrategyDiagnosticSensitivity.Normal,
                    "sha256:qa-receipts",
                    "Step 'qa-recheck' claimed completion but required current-run product tool receipt(s) are missing: workspace_dotnet_restore; workspace_dotnet_build; workspace_dotnet_test; workspace_dotnet_run; browser_navigate; browser_snapshot; browser_take_screenshot; browser_console_messages; workspace_dotnet_stop.",
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.SafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent),
                new StrategyDiagnosticRef(
                    new StrategyDiagnosticCode("process.adapter.required_tool_receipt_missing"),
                    StrategyDiagnosticSensitivity.Normal,
                    "sha256:qa-process-receipts",
                    "Step 'qa-recheck' claimed completion but required current-run process tool receipt(s) are missing.",
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.SafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [
                new ManagerSignal(
                    new ManagerSignalCode("process.adapter.product_required_tool_receipt_missing"),
                    "sha256:qa-receipts",
                    "Missing QA recheck receipts.")
            ],
            "sha256:qa-receipts");
    }

    private static StrategyResultReceipt CreateReceipt(
        StrategyResultEnvelope result,
        ProcessRecoveryDecisionReceipt decision)
    {
        return new StrategyResultReceipt(
            StepId,
            result.StrategyId,
            StrategyResultIdempotencyKey.New(),
            result.Outcome,
            decision.DecisionKind == ProcessRecoveryDecisionKind.SafeRetry
                ? ProcessRuntimeStepStatus.Ready
                : ProcessRuntimeStepStatus.Blocked,
            result.ResultHash,
            result.Diagnostics
                .Select(diagnostic => new StrategyResultDiagnosticReceipt(
                    diagnostic.Code.Value,
                    diagnostic.Sensitivity,
                    diagnostic.EvidenceHash,
                    diagnostic.SafeSummary,
                    diagnostic.RestrictedEvidenceReference,
                    diagnostic.RetrySafety,
                    diagnostic.Idempotency))
                .ToArray(),
            [],
            decision);
    }

    private static ProcessRecoveryDecisionReceipt CreateSafeRetryDecision(
        string sourceDiagnosticCode = "process.adapter.product_required_tool_receipt_missing")
        => new(
            ProcessFailureCategory.ProductCompletionGate,
            ProcessRecoveryDecisionKind.SafeRetry,
            sourceDiagnosticCode,
            "process.current-step-safe-retry",
            "safe and idempotent completion gate")
        {
            RouteKind = ProcessRecoveryRouteKind.CurrentStepRetry,
            DiagnosticFingerprint = "sha256:fingerprint",
            AutomaticRetryAttempt = 1,
            MaximumAutomaticRetryAttempts = 3,
            SameDiagnosticFingerprintAttempt = 1,
            MaximumSameDiagnosticFingerprintAttempts = 1
        };

    private static ProcessRecoveryDecisionReceipt CreateBudgetExhaustedDecision(
        string sourceDiagnosticCode = "process.adapter.product_required_tool_receipt_missing")
        => CreateSafeRetryDecision(sourceDiagnosticCode) with
        {
            DecisionKind = ProcessRecoveryDecisionKind.ManagerRequired,
            RouteKind = ProcessRecoveryRouteKind.ManagerAction,
            Policy = "process.current-step-safe-retry-budget-exhausted",
            AutomaticRetryAttempt = 2,
            SameDiagnosticFingerprintAttempt = 2
        };
}
