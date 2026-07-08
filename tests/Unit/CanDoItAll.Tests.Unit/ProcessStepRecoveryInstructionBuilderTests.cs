using System.Text.Json;
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

    private static ProcessRecoveryDecisionReceipt CreateSafeRetryDecision()
        => new(
            ProcessFailureCategory.ProductCompletionGate,
            ProcessRecoveryDecisionKind.SafeRetry,
            "process.adapter.product_required_tool_receipt_missing",
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

    private static ProcessRecoveryDecisionReceipt CreateBudgetExhaustedDecision()
        => CreateSafeRetryDecision() with
        {
            DecisionKind = ProcessRecoveryDecisionKind.ManagerRequired,
            RouteKind = ProcessRecoveryRouteKind.ManagerAction,
            Policy = "process.current-step-safe-retry-budget-exhausted",
            AutomaticRetryAttempt = 2,
            SameDiagnosticFingerprintAttempt = 2
        };
}
