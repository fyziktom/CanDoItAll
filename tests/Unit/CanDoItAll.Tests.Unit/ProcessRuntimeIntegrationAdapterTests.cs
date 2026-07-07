using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRuntimeIntegrationAdapterTests
{
    private static readonly ProcessStrategyBindingSnapshot Binding = new(
        new DriverId("driver.runtime"),
        new StrategyId("strategy.execute"),
        "1.0.0",
        "factory.1.0.0",
        "runtime.1",
        "runtime.1",
        "sha256:binding",
        []);

    [Fact]
    public void Product_mutation_completion_requires_evidence_refs()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(outputRoot, "TetrisGame.csproj"), "<Project />");
            var result = ToAdapterResult(
                CreateProductMutationAssignment(outputRoot),
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Implemented the app.",
                    EvidenceRefs = [],
                    NextActions = []
                });

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.product_output_evidence_missing");
            Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.product_output_evidence_missing");
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Subprocess_step_blocker_without_launch_receipt_requests_safe_retry()
    {
        var assignment = CreateSubprocessAssignment();
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Blocked,
                Reason = "The required child subprocess was not launched, so no child run receipt exists.",
                EvidenceRefs = [BuildStepArtifactRef(assignment)],
                NextActions = ["Launch the mapped child subprocess."],
                HumanReadableSummaryMarkdown = "Blocked because there is no current child run."
            },
            [CreateToolReceipt("workspace_write_file", BuildStepArtifactRef(assignment), "Succeeded: Created file.")]);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.subprocess_launch_skipped_retry" &&
            diagnostic.RetrySafety == ProcessDiagnosticRetrySafety.SafeToRetry);
        Assert.Contains("project_structure_process_subprocess_launch", result.UserSafeSummary, StringComparison.Ordinal);
        Assert.Empty(result.ProducedArtifacts);
    }

    [Fact]
    public void Subprocess_step_blocker_that_expected_direct_child_tools_requests_safe_retry()
    {
        var assignment = CreateSubprocessAssignment();
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Blocked,
                Reason = "The requested subprocess step cannot proceed because direct implementation and scaffold tools are not available in the current parent toolset. The step contract explicitly says to launch the mapped child process, but only project-structure subprocess launch tools are available here.",
                EvidenceRefs = [BuildStepArtifactRef(assignment)],
                NextActions = ["Grant direct child-work tools or route this step elsewhere."],
                HumanReadableSummaryMarkdown = "Blocked on missing direct child-work capability."
            },
            [CreateToolReceipt("workspace_write_file", BuildStepArtifactRef(assignment), "Succeeded: Created file.")]);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.subprocess_launch_skipped_retry" &&
            diagnostic.RetrySafety == ProcessDiagnosticRetrySafety.SafeToRetry);
        Assert.Contains("call project_structure_process_subprocess_launch", result.UserSafeSummary, StringComparison.Ordinal);
        Assert.Empty(result.ProducedArtifacts);
    }

    [Fact]
    public void Subprocess_step_unverified_missing_launch_capability_requests_safe_retry()
    {
        var assignment = CreateSubprocessAssignment();
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Blocked,
                Reason = "The implementation subprocess cannot proceed because the required child launch capability is unavailable in this run. The available tool set for this step does not expose the mandated `project_structure_process_subprocess_launch` / ExecuteExternalAction path, so I cannot launch or observe the child implementation slice.",
                EvidenceRefs = [BuildStepArtifactRef(assignment)],
                NextActions = ["Grant ExecuteExternalAction or reassign this step."],
                HumanReadableSummaryMarkdown = "Blocked on the mandated child subprocess launch path."
            },
            [CreateToolReceipt("workspace_read_file", BuildStepArtifactRef(assignment), "Succeeded: Read file.")]);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.subprocess_launch_skipped_retry" &&
            diagnostic.RetrySafety == ProcessDiagnosticRetrySafety.SafeToRetry);
        Assert.Contains("call project_structure_process_subprocess_launch", result.UserSafeSummary, StringComparison.Ordinal);
        Assert.Empty(result.ProducedArtifacts);
    }

    [Fact]
    public void Subprocess_step_completion_without_launch_receipt_or_child_evidence_requests_safe_retry()
    {
        var assignment = CreateSubprocessAssignment();
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "The scaffold already matches the target shape, so no product files were mutated.",
                EvidenceRefs = [BuildStepArtifactRef(assignment)],
                NextActions = [],
                HumanReadableSummaryMarkdown = "Completed after read-only inspection."
            },
            [CreateToolReceipt("workspace_write_file", BuildStepArtifactRef(assignment), "Succeeded: Created file.")]);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.subprocess_launch_skipped_retry" &&
            diagnostic.RetrySafety == ProcessDiagnosticRetrySafety.SafeToRetry);
        Assert.Contains("before invoking project_structure_process_subprocess_launch", result.UserSafeSummary, StringComparison.Ordinal);
        Assert.Empty(result.ProducedArtifacts);
    }

    [Fact]
    public void Subprocess_step_completion_with_child_evidence_can_succeed_without_current_launch_receipt()
    {
        var assignment = CreateSubprocessAssignment();
        var childEvidenceRef = $"artifacts/process-runs/{Guid.NewGuid():D}/steps/slice-handoff.md";
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Child subprocess completed and the handoff evidence was accepted.",
                EvidenceRefs = [BuildStepArtifactRef(assignment), childEvidenceRef],
                NextActions = [],
                HumanReadableSummaryMarkdown = "Completed from stopped child evidence."
            },
            [CreateToolReceipt("workspace_write_file", BuildStepArtifactRef(assignment), "Succeeded: Created file.")]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.subprocess_launch_skipped_retry");
        Assert.NotEmpty(result.ProducedArtifacts);
    }

    [Fact]
    public void Subprocess_step_launch_tool_boundary_remains_manager_request()
    {
        var assignment = CreateSubprocessAssignment();
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Blocked,
                Reason = "Tool 'project_structure_process_subprocess_launch' is not part of the composed capability set for this run.",
                EvidenceRefs = [],
                NextActions = ["Grant the missing subprocess launch tool."]
            },
            []);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.subprocess_launch_skipped_retry");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.agent_rights_request");
    }

    [Fact]
    public void Product_mutation_completion_requires_product_files_in_output_root()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var result = ToAdapterResult(
                CreateProductMutationAssignment(outputRoot),
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Implemented the app.",
                    EvidenceRefs = ["artifacts/process-runs/run-001/steps/implementation.md"],
                    NextActions = []
                });

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.product_output_missing");
            Assert.Contains(outputRoot, result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_completion_succeeds_when_output_root_contains_product_file_and_evidence()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(outputRoot, "TetrisGame.csproj"), "<Project />");
            var assignment = CreateProductMutationAssignment(outputRoot);
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Implemented the app.",
                    EvidenceRefs = [BuildStepArtifactRef(assignment)],
                    NextActions = []
                });

            Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
            Assert.NotEmpty(result.ProducedArtifacts);
            Assert.Empty(result.Diagnostics);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_completion_requires_declared_product_output_path()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(outputRoot, "Calculator.slnx"), "<Solution />");
            var requiredProjectPath = Path.Combine(outputRoot, "src", "Calculator", "Calculator.csproj");
            var baseAssignment = CreateProductMutationAssignment(outputRoot);
            var assignment = baseAssignment with
            {
                StepKey = "create-dotnet-project",
                LaunchVariables = WithLaunchVariables(
                    baseAssignment,
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths, requiredProjectPath))
            };
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Created the product root, src folder, and solution file.",
                    EvidenceRefs = [BuildStepArtifactRef(assignment)],
                    NextActions = []
                },
                [
                    CreateToolReceipt(
                        "workspace_dotnet_new",
                        "new sln -n Calculator",
                        "Succeeded (exit 0)",
                        productAlias),
                    CreateToolReceipt("workspace_write_file", BuildStepArtifactRef(assignment), "Succeeded: Created file.")
                ]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("process.adapter.product_required_output_missing", diagnostic.Code.Value);
            Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
            Assert.Contains(requiredProjectPath, result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_completion_uses_per_step_required_product_paths()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(outputRoot, "Calculator.slnx"), "<Solution />");
            var requiredProjectPath = Path.Combine(outputRoot, "src", "Calculator", "Calculator.csproj");
            var requiredPathMap = JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["create-dotnet-project"] = [requiredProjectPath]
            });
            var baseAssignment = CreateProductMutationAssignment(outputRoot);
            var assignment = baseAssignment with
            {
                StepKey = "create-dotnet-project",
                LaunchVariables = WithLaunchVariables(
                    baseAssignment,
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredPathsByStep, requiredPathMap))
            };
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Created the product root, src folder, and solution file.",
                    EvidenceRefs = [BuildStepArtifactRef(assignment)],
                    NextActions = []
                },
                [
                    CreateToolReceipt(
                        "workspace_dotnet_new",
                        "new sln -n Calculator",
                        "Succeeded (exit 0)",
                        productAlias),
                    CreateToolReceipt("workspace_write_file", BuildStepArtifactRef(assignment), "Succeeded: Created file.")
                ]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.product_required_output_missing");
            Assert.Contains(requiredProjectPath, result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_completion_uses_per_step_required_product_tool_receipts()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var appProjectFile = Path.Combine(outputRoot, "src", "Calculator", "Calculator.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(appProjectFile)!);
            File.WriteAllText(appProjectFile, "<Project />");

            var requiredToolReceiptMap = JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["add-test-project"] = ["workspace_pwsh_run_script"]
            });
            var baseAssignment = CreateProductMutationAssignment(outputRoot);
            var assignment = baseAssignment with
            {
                StepKey = "add-test-project",
                LaunchVariables = WithLaunchVariables(
                    baseAssignment,
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep, requiredToolReceiptMap),
                    ("DotNetAddTestProjectScript", "$ErrorActionPreference = 'Stop'"),
                    ("DotNetAddTestProjectScriptRef", "artifacts/process-runs/{CurrentProcessRunId}/scripts/add-test-project.ps1"),
                    ("DotNetAddTestProjectSideEffectManifest", """{"version":1,"mode":"ProductMutation"}"""))
            };
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Created the app and test projects.",
                    EvidenceRefs = [BuildStepArtifactRef(assignment)],
                    NextActions = []
                },
                [
                    CreateToolReceipt(
                        "workspace_dotnet_new",
                        $"{productAlias}/tests/Calculator.Tests",
                        "Succeeded (exit 0)",
                        productAlias),
                    CreateToolReceipt("workspace_write_file", BuildStepArtifactRef(assignment), "Succeeded: Created file.")
                ]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("process.adapter.product_required_tool_receipt_missing", diagnostic.Code.Value);
            Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
            Assert.Contains("workspace_pwsh_run_script", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DotNetAddTestProjectScript", diagnostic.SafeSummary, StringComparison.Ordinal);
            Assert.Contains("artifacts/process-runs/{CurrentProcessRunId}/scripts/add-test-project.ps1", diagnostic.SafeSummary, StringComparison.Ordinal);
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_completion_requires_successful_required_product_tool_receipt()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var appProjectFile = Path.Combine(outputRoot, "src", "Calculator", "Calculator.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(appProjectFile)!);
            File.WriteAllText(appProjectFile, "<Project />");

            var baseAssignment = CreateProductMutationAssignment(outputRoot);
            var assignment = baseAssignment with
            {
                StepKey = "repair-solution-setup",
                LaunchVariables = WithLaunchVariables(
                    baseAssignment,
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts, "workspace_pwsh_run_script"))
            };
            var primaryRef = BuildStepArtifactRef(assignment);
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Repair script was attempted.",
                    EvidenceRefs = [primaryRef],
                    NextActions = []
                },
                [
                    CreateToolReceipt("workspace_write_file", $"{productAlias}/src/Calculator/Calculator.csproj", "Succeeded: Updated file."),
                    CreateToolReceipt("workspace_pwsh_run_script", "repair-solution-setup.ps1", "Failed (exit 1)"),
                    CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")
                ]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("process.adapter.product_required_tool_receipt_missing", diagnostic.Code.Value);
            Assert.Contains("workspace_pwsh_run_script", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("present but failed", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("mutate the product target", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Validation_completion_requires_declared_product_tool_receipts()
    {
        var assignment = CreateManagedArtifactAssignment(
            "validate-first-build",
            allowedOperations: [ProcessOperationContractNames.RunValidation]) with
        {
            LaunchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] =
                    "workspace_dotnet_restore;workspace_dotnet_build;workspace_dotnet_test"
            }
        };
        var primaryRef = BuildStepArtifactRef(assignment);
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Validation evidence was summarized.",
                EvidenceRefs = [primaryRef],
                NextActions = []
            },
            [
                CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")
            ]);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("process.adapter.product_required_tool_receipt_missing", diagnostic.Code.Value);
        Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
        Assert.Contains("workspace_dotnet_restore", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_dotnet_build", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_dotnet_test", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.ProducedArtifacts);
    }

    [Fact]
    public void External_action_completion_requires_declared_project_structure_tool_receipts()
    {
        var assignment = CreateManagedArtifactAssignment(
            "store-ui-screenshots",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.CaptureRuntimeProof,
                ProcessOperationContractNames.WriteManagedProcessArtifacts,
                ProcessOperationContractNames.ExecuteExternalAction
            ]) with
        {
            OperationTargetScope = ProcessOperationContractNames.ExternalActionControlled,
            LaunchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] =
                    "project_structure_node_create;project_structure_asset_create"
            }
        };
        var primaryRef = BuildStepArtifactRef(assignment);

        var readinessRequest = CreateRuntimeReadinessRequest(assignment);
        Assert.Contains("project_structure_node_create", readinessRequest.RequiredRuntimeToolNames!);
        Assert.Contains("project_structure_asset_create", readinessRequest.RequiredRuntimeToolNames!);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Screenshot storage receipts were summarized, but project_structure_node_create and project_structure_asset_create were not exposed.",
                EvidenceRefs = [primaryRef],
                NextActions = [],
                HumanReadableSummaryMarkdown = """
                Status: Completed

                ## Storage receipts
                Project-structure writeback was not completed because `project_structure_node_create` and `project_structure_asset_create` were not exposed.
                """
            },
            [
                CreateToolReceipt("project_structure_read", "read process-run node", "Succeeded: Read project structure."),
                CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")
            ]);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("process.adapter.product_required_tool_receipt_missing", diagnostic.Code.Value);
        Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
        Assert.Contains("project_structure_node_create", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("project_structure_asset_create", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.ProducedArtifacts);
    }

    [Fact]
    public void Runtime_capture_blocker_with_missing_required_tool_receipts_requests_safe_retry()
    {
        var requiredToolReceiptMap = JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["capture-ui-screenshots"] =
            [
                "workspace_dotnet_run",
                "browser_navigate",
                "browser_snapshot",
                "browser_take_screenshot",
                "browser_console_messages",
                "workspace_dotnet_stop"
            ]
        });
        var assignment = CreateManagedArtifactAssignment(
            "capture-ui-screenshots",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.LaunchRuntime,
                ProcessOperationContractNames.CaptureRuntimeProof,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ]) with
        {
            LaunchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep] = requiredToolReceiptMap
            }
        };
        var primaryRef = BuildStepArtifactRef(assignment);

        var readinessRequest = CreateRuntimeReadinessRequest(assignment);
        Assert.Contains("workspace_dotnet_run", readinessRequest.RequiredRuntimeToolNames!);
        Assert.Contains("browser_take_screenshot", readinessRequest.RequiredRuntimeToolNames!);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Blocked,
                Reason = "Base URL was not provided and no current-run browser URL, screenshot, snapshot, console log, startup receipt, or cleanup receipt exists.",
                EvidenceRefs = [primaryRef],
                NextActions = ["Provide a base URL for screenshot capture."],
                HumanReadableSummaryMarkdown = """
                Status: Blocked

                No browser proof was captured because no base URL was present.
                """
            },
            [
                CreateToolReceipt("workspace_stat_path", "external-target/C/programovani/dotnet/calculator-output", "Succeeded: Path exists."),
                CreateToolReceipt("workspace_read_file", primaryRef, "Succeeded: Read file."),
                CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")
            ]);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("process.adapter.product_required_tool_receipt_blocked_retry", diagnostic.Code.Value);
        Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
        Assert.Contains("workspace_dotnet_run", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("browser_take_screenshot", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.ProducedArtifacts);
    }

    [Fact]
    public void Completion_requires_process_capability_scope_tool_receipts()
    {
        var assignment = CreateManagedArtifactAssignment(
            "capture-ui-screenshots",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.LaunchRuntime,
                ProcessOperationContractNames.CaptureRuntimeProof,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ]) with
        {
            CapabilityScope = new ProcessCapabilityScope
            {
                RequiredReceipts =
                [
                    new ProcessRequiredToolReceipt
                    {
                        Key = "ui-screenshot-proof",
                        Kind = ProcessRequiredToolReceiptKind.RuntimeToolName,
                        ToolName = "browser_take_screenshot",
                        Reason = "Current-run UI proof must include a screenshot."
                    }
                ]
            }
        };
        var primaryRef = BuildStepArtifactRef(assignment);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Screenshot evidence was summarized.",
                EvidenceRefs = [primaryRef],
                NextActions = [],
                HumanReadableSummaryMarkdown = "Status: Completed"
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")]);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("process.adapter.required_tool_receipt_missing", diagnostic.Code.Value);
        Assert.Contains("browser_take_screenshot", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.ProducedArtifacts);
    }

    [Fact]
    public void Completion_accepts_process_capability_scope_current_run_tool_receipt()
    {
        var executionRunId = Guid.NewGuid();
        var assignment = CreateManagedArtifactAssignment(
            "capture-ui-screenshots",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.LaunchRuntime,
                ProcessOperationContractNames.CaptureRuntimeProof,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ]) with
        {
            CapabilityScope = new ProcessCapabilityScope
            {
                RequiredReceipts =
                [
                    new ProcessRequiredToolReceipt
                    {
                        Key = "ui-screenshot-proof",
                        Kind = ProcessRequiredToolReceiptKind.RuntimeToolName,
                        ToolName = "browser_take_screenshot",
                        Reason = "Current-run UI proof must include a screenshot."
                    }
                ]
            }
        };
        var primaryRef = BuildStepArtifactRef(assignment);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Screenshot evidence was captured.",
                EvidenceRefs = [primaryRef],
                NextActions = [],
                HumanReadableSummaryMarkdown = "Status: Completed"
            },
            [
                CreateToolReceipt("browser_take_screenshot", "capture UI screenshot", "Succeeded (exit 0)", executionRunId: executionRunId),
                CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.", executionRunId: executionRunId)
            ],
            executionRunId);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
    }

    [Fact]
    public void Completion_rejects_stale_process_capability_scope_tool_receipt()
    {
        var currentExecutionRunId = Guid.NewGuid();
        var previousExecutionRunId = Guid.NewGuid();
        var assignment = CreateManagedArtifactAssignment(
            "capture-ui-screenshots",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.LaunchRuntime,
                ProcessOperationContractNames.CaptureRuntimeProof,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ]) with
        {
            CapabilityScope = new ProcessCapabilityScope
            {
                RequiredReceipts =
                [
                    new ProcessRequiredToolReceipt
                    {
                        Key = "ui-screenshot-proof",
                        Kind = ProcessRequiredToolReceiptKind.RuntimeToolName,
                        ToolName = "browser_take_screenshot",
                        Reason = "Current-run UI proof must include a screenshot."
                    }
                ]
            }
        };
        var primaryRef = BuildStepArtifactRef(assignment);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Screenshot evidence from an earlier attempt was reused.",
                EvidenceRefs = [primaryRef],
                NextActions = [],
                HumanReadableSummaryMarkdown = "Status: Completed"
            },
            [
                CreateToolReceipt("browser_take_screenshot", "capture UI screenshot", "Succeeded (exit 0)", executionRunId: previousExecutionRunId),
                CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.", executionRunId: currentExecutionRunId)
            ],
            currentExecutionRunId);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.required_tool_receipt_missing");
    }

    [Fact]
    public void Blocked_step_with_missing_process_receipt_gets_manager_retry_diagnostic()
    {
        var executionRunId = Guid.NewGuid();
        var assignment = CreateManagedArtifactAssignment(
            "capture-ui-screenshots",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.LaunchRuntime,
                ProcessOperationContractNames.CaptureRuntimeProof,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ]) with
        {
            CapabilityScope = new ProcessCapabilityScope
            {
                RequiredReceipts =
                [
                    new ProcessRequiredToolReceipt
                    {
                        Key = "ui-screenshot-proof",
                        Kind = ProcessRequiredToolReceiptKind.RuntimeToolName,
                        ToolName = "browser_take_screenshot",
                        Reason = "Current-run UI proof must include a screenshot."
                    }
                ]
            }
        };
        var primaryRef = BuildStepArtifactRef(assignment);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Blocked,
                Reason = "Missing required screenshot receipt browser_take_screenshot.",
                EvidenceRefs = [primaryRef],
                NextActions = ["Retry with browser screenshot proof."],
                HumanReadableSummaryMarkdown = "Status: Blocked"
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.", executionRunId: executionRunId)],
            executionRunId);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.required_tool_receipt_blocked_retry");
        Assert.Contains("browser_take_screenshot", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Conditional_process_capability_scope_receipts_activate_from_launch_context()
    {
        var baseAssignment = CreateManagedArtifactAssignment("conditional-receipt-check") with
        {
            CapabilityScope = new ProcessCapabilityScope
            {
                RequiredReceipts =
                [
                    new ProcessRequiredToolReceipt
                    {
                        Key = "conditional-ui-screenshot-proof",
                        Kind = ProcessRequiredToolReceiptKind.RuntimeToolName,
                        ToolName = "browser_take_screenshot",
                        Activation = ProcessRequiredToolReceiptActivation.WhenLaunchContextDeclaresTool,
                        Reason = "Only UI targets require screenshot capture."
                    }
                ]
            }
        };
        var inactivePrimaryRef = BuildStepArtifactRef(baseAssignment);

        var inactiveResult = ToAdapterResult(
            baseAssignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "No UI evidence was required.",
                EvidenceRefs = [inactivePrimaryRef],
                NextActions = [],
                HumanReadableSummaryMarkdown = "Status: Completed"
            },
            [CreateToolReceipt("workspace_write_file", inactivePrimaryRef, "Succeeded: Created file.")]);

        Assert.Equal(StrategyOutcome.Succeeded, inactiveResult.Outcome);

        var activeAssignment = baseAssignment with
        {
            LaunchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] = "browser_take_screenshot"
            }
        };
        var activePrimaryRef = BuildStepArtifactRef(activeAssignment);

        var activeResult = ToAdapterResult(
            activeAssignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Screenshot evidence was summarized.",
                EvidenceRefs = [activePrimaryRef],
                NextActions = [],
                HumanReadableSummaryMarkdown = "Status: Completed"
            },
            [CreateToolReceipt("workspace_write_file", activePrimaryRef, "Succeeded: Created file.")]);

        Assert.Equal(StrategyOutcome.NeedsManager, activeResult.Outcome);
        Assert.Contains(activeResult.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.required_tool_receipt_missing");
    }

    [Fact]
    public void Validation_completion_accepts_failed_required_product_tool_receipts_as_evidence()
    {
        var baseAssignment = CreateManagedArtifactAssignment(
            "targeted-validation",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ]);
        var assignment = baseAssignment with
        {
            Prompt = """
            Branch outcomes:
            - feature-accepted: Feature accepted - Validation passed.
            - feature-repair-required: Repair required - Validation failed but repair is possible.
            """,
            LaunchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] =
                    "workspace_dotnet_restore;workspace_dotnet_build;workspace_dotnet_test"
            }
        };
        var primaryRef = BuildStepArtifactRef(assignment);
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Focused validation found a compile blocker.",
                EvidenceRefs = [primaryRef],
                NextActions = ["Repair the compile error and rerun validation."],
                HumanReadableSummaryMarkdown = "Status: Completed\n\n## Validation decision\nBranch outcome key: feature-repair-required"
            },
            [
                CreateToolReceipt("workspace_dotnet_restore", "restore Calculator.slnx", "Succeeded (exit 0)"),
                CreateToolReceipt("workspace_dotnet_build", "build Calculator.slnx", "Failed (exit 1)"),
                CreateToolReceipt("workspace_dotnet_test", "test Calculator.slnx", "Failed (exit 1)"),
                CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")
            ]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Empty(result.Diagnostics);
        Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == ProcessBranchSignalCodes.Outcome("feature-repair-required").Value);
    }

    [Fact]
    public void Validation_blocker_with_missing_required_tool_receipts_requests_safe_retry()
    {
        var assignment = CreateManagedArtifactAssignment(
            "validate-first-build",
            allowedOperations: [ProcessOperationContractNames.RunValidation]) with
        {
            LaunchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] =
                    "workspace_dotnet_restore;workspace_dotnet_build;workspace_dotnet_test"
            }
        };
        var primaryRef = BuildStepArtifactRef(assignment);
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Blocked,
                Reason = "Required build and test discovery evidence has not yet been produced in this step.",
                EvidenceRefs = [primaryRef],
                NextActions = ["Run restore, build, and test before finalizing."]
            },
            [
                CreateToolReceipt("workspace_stat_path", "Calculator.slnx", "Succeeded: File exists."),
                CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")
            ]);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("process.adapter.product_required_tool_receipt_blocked_retry", diagnostic.Code.Value);
        Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
        Assert.Equal(ProcessDiagnosticIdempotencyClassification.Idempotent, diagnostic.Idempotency);
        Assert.Contains("workspace_dotnet_restore", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.product_required_tool_receipt_blocked_retry");
        Assert.Contains(result.RequestedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
        Assert.Empty(result.ProducedArtifacts);
    }

    [Fact]
    public void Validation_blocker_reporting_missing_required_tool_receipts_requests_retry_even_when_aggregate_receipts_exist()
    {
        var assignment = CreateManagedArtifactAssignment(
            "validate-first-build",
            allowedOperations: [ProcessOperationContractNames.RunValidation]) with
        {
            LaunchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] =
                    "workspace_dotnet_restore;workspace_dotnet_build;workspace_dotnet_test"
            }
        };
        var primaryRef = BuildStepArtifactRef(assignment);
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Blocked,
                BranchOutcomeKey = "setup-repair-required",
                Reason = "Required current-run restore, build, and test receipts are missing from the validation artifact.",
                EvidenceRefs = [primaryRef],
                NextActions = ["Run restore/build/test before routing setup repair."]
            },
            [
                CreateToolReceipt("workspace_dotnet_restore", "restore Calculator.slnx", "Succeeded (exit 0)"),
                CreateToolReceipt("workspace_dotnet_build", "build Calculator.slnx", "Succeeded (exit 0)"),
                CreateToolReceipt("workspace_dotnet_test", "test Calculator.slnx", "Succeeded (exit 0)"),
                CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")
            ]);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("process.adapter.product_required_tool_receipt_blocked_retry", diagnostic.Code.Value);
        Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
        Assert.Contains("reported missing required receipt evidence", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.RequestedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
        Assert.Empty(result.ProducedArtifacts);
    }

    [Fact]
    public void Validation_completion_does_not_satisfy_required_tool_receipts_from_artifact_write_text()
    {
        var assignment = CreateManagedArtifactAssignment(
            "validate-first-build",
            allowedOperations: [ProcessOperationContractNames.RunValidation]) with
        {
            Prompt = """
            Available branch outcomes:
            - setup-validated: Setup validated - Restore, build, and targeted test discovery are green enough for handoff.
            - setup-repair-required: Setup repair required - Repairable validation failure.
            """,
            LaunchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] =
                    "workspace_dotnet_restore;workspace_dotnet_build;workspace_dotnet_test"
            }
        };
        var primaryRef = BuildStepArtifactRef(assignment);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Recovered governed process step outcome from primary managed artifact after provider timeout.",
                EvidenceRefs = [primaryRef],
                NextActions = [],
                HumanReadableSummaryMarkdown = """
                Status: Completed
                Branch outcome key: setup-validated

                ## Validation command receipts required by this step
                - Restore: required current-run `workspace_dotnet_restore` receipt not yet captured in this attempt.
                - Build: required current-run `workspace_dotnet_build` receipt not yet captured in this attempt.
                - Test discovery: required current-run `workspace_dotnet_test` receipt not yet captured in this attempt.
                """
            },
            [
                CreateToolReceipt("workspace_read_file", "read upstream setup artifact mentioning workspace_dotnet_restore", "Succeeded: Read file."),
                CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file containing workspace_dotnet_restore, workspace_dotnet_build, and workspace_dotnet_test text.")
            ]);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("process.adapter.product_required_tool_receipt_missing", diagnostic.Code.Value);
        Assert.Contains("workspace_dotnet_restore", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_dotnet_build", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_dotnet_test", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.product_required_tool_receipt_missing");
        Assert.Empty(result.ProducedArtifacts);
    }

    [Fact]
    public void Product_mutation_completion_accepts_template_specific_dotnet_new_receipts()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var solutionFile = Path.Combine(outputRoot, "Calculator.slnx");
            var appProjectFile = Path.Combine(outputRoot, "src", "Calculator", "Calculator.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(appProjectFile)!);
            File.WriteAllText(solutionFile, "<Solution />");
            File.WriteAllText(appProjectFile, "<Project />");

            var requiredToolReceiptMap = JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["create-dotnet-project"] = ["template=sln", "template=blazorwasm"]
            });
            var baseAssignment = CreateProductMutationAssignment(outputRoot);
            var assignment = baseAssignment with
            {
                StepKey = "create-dotnet-project",
                LaunchVariables = WithLaunchVariables(
                    baseAssignment,
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep, requiredToolReceiptMap),
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredPathsByStep, JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["create-dotnet-project"] = [solutionFile, appProjectFile]
                    })))
            };
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Created the solution and Blazor WebAssembly app project.",
                    EvidenceRefs = [BuildStepArtifactRef(assignment)],
                    NextActions = []
                },
                [
                    CreateToolReceipt(
                        "workspace_dotnet_new",
                        "new sln -n Calculator",
                        "Succeeded (exit 0)",
                        productAlias),
                    CreateToolReceipt(
                        "workspace_dotnet_new",
                        "new blazorwasm -n Calculator",
                        "Succeeded (exit 0)",
                        $"{productAlias}/src"),
                    CreateToolReceipt("workspace_write_file", BuildStepArtifactRef(assignment), "Succeeded: Created file.")
                ]);

            Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
            Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            Assert.DoesNotContain(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.product_required_tool_receipt_missing");
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_recovers_completed_primary_artifact_and_prior_product_receipts_on_retry()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var solutionFile = Path.Combine(outputRoot, "Calculator.slnx");
            var appProjectFile = Path.Combine(outputRoot, "src", "Calculator", "Calculator.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(appProjectFile)!);
            File.WriteAllText(solutionFile, "<Solution />");
            File.WriteAllText(appProjectFile, "<Project />");

            var requiredToolReceiptMap = JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["create-dotnet-project"] = ["template=sln", "template=blazorwasm"]
            });
            var baseAssignment = CreateProductMutationAssignment(outputRoot);
            var assignment = baseAssignment with
            {
                StepKey = "create-dotnet-project",
                LaunchVariables = WithLaunchVariables(
                    baseAssignment,
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep, requiredToolReceiptMap),
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredPathsByStep, JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["create-dotnet-project"] = [solutionFile, appProjectFile]
                    })))
            };
            var agent = NewAgent(
                ".NET Application Developer",
                ".NET developer",
                AgentWorkloadKind.Programming,
                ["dotnet", "blazor", "software-engineer"],
                AgentWorkspaceToolProfileKind.SoftwareDevelopment);
            assignment = assignment with
            {
                ExecutorId = agent.Id.ToString("D"),
                ExecutorDisplayName = agent.Name
            };

            var currentExecutionRunId = Guid.NewGuid();
            var previousExecutionRunId = Guid.NewGuid();
            var primaryRef = BuildStepArtifactRef(assignment);
            var responseText = $$"""
                {
                  "status": "Blocked",
                  "reason": "Retry saw an existing completed scaffold artifact and did not rerun scaffold commands.",
                  "branchOutcomeKey": "",
                  "branchOutcomeTitle": "",
                  "evidenceRefs": [
                    "{{primaryRef}}"
                  ],
                  "nextActions": [
                    "Manager should inspect the completed scaffold artifact."
                  ],
                  "humanReadableSummaryMarkdown": "The primary scaffold artifact already exists."
                }
                """;
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var currentDetail = CreateExecutionRunDetail(agent.Id, currentExecutionRunId, responseText, []);
            var previousDetail = CreateExecutionRunDetail(
                agent.Id,
                previousExecutionRunId,
                "previous completed scaffold response",
                [
                    CreateToolReceipt(
                        "workspace_dotnet_new",
                        "new sln -n Calculator",
                        "Succeeded (exit 0)",
                        productAlias),
                    CreateToolReceipt(
                        "workspace_dotnet_new",
                        "new blazorwasm -n Calculator",
                        "Succeeded (exit 0)",
                        $"{productAlias}/src"),
                    CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")
                ]);
            var workspace = new ThrowingWorkspaceService(
                agent,
                executeException: null,
                executeResult: CreateExecutionRunResult(agent.Id, currentExecutionRunId, responseText),
                executionDetails: [currentDetail, previousDetail]);
            var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
            try
            {
                var artifactPath = Path.Combine(
                    workspaceRoot,
                    "artifacts",
                    "process-runs",
                    assignment.RunId.Value.ToString("D"),
                    "steps",
                    "create-dotnet-project.md");
                Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
                await File.WriteAllTextAsync(
                    artifactPath,
                    $$"""
                    Status: Completed

                    # Solution skeleton change set

                    - Run id: {{assignment.RunId.Value:D}}
                    - Step id: {{assignment.StepInstanceId.Value:D}}
                    - Step key: {{assignment.StepKey}}

                    ## Product mutations completed
                    - Scaffolded solution with `workspace_dotnet_new` template `sln`
                    - Scaffolded app project with `workspace_dotnet_new` template `blazorwasm`
                    """);

                var adapter = new AgentFrameworkProcessExecutionAdapter(
                    new FakeWorkspaceFactory(workspace),
                    CreateReferenceDataProvider(workspace),
                    new InMemoryAssignmentStore(assignment),
                    new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                    workspaceFiles);

                var result = await adapter.ExecuteAsync(
                    new ProcessExecutionAdapterRequest(
                        assignment.RunId,
                        assignment.StepInstanceId,
                        ProcessExecutionAdapterKind.Workflow,
                        new ProcessExecutionAdapterOperationKey("execute"),
                        Binding,
                        [],
                        []));

                Assert.True(result.Outcome == StrategyOutcome.Succeeded, result.UserSafeSummary);
                Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
                Assert.DoesNotContain(result.Diagnostics, diagnostic =>
                    diagnostic.Code.Value == "process.adapter.product_required_tool_receipt_missing");
                var content = await File.ReadAllTextAsync(artifactPath);
                Assert.Contains("# Solution skeleton change set", content, StringComparison.Ordinal);
                Assert.Contains("Runtime Validated Structured Outcome", content, StringComparison.Ordinal);
                Assert.Contains("accepted the completed primary managed artifact", content, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                DeleteDirectory(workspaceRoot);
            }
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_completion_accepts_required_product_script_receipt()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var appProjectFile = Path.Combine(outputRoot, "src", "Calculator", "Calculator.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(appProjectFile)!);
            File.WriteAllText(appProjectFile, "<Project />");

            var baseAssignment = CreateProductMutationAssignment(outputRoot);
            var assignment = baseAssignment with
            {
                StepKey = "create-dotnet-project",
                LaunchVariables = WithLaunchVariables(
                    baseAssignment,
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts, "workspace_pwsh_run_script"))
            };
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Created the app project and added it to the solution.",
                    EvidenceRefs = [BuildStepArtifactRef(assignment)],
                    NextActions = []
                },
                [
                    CreateToolReceipt(
                        "workspace_pwsh_run_script",
                        $"dotnet sln add {productAlias}/src/Calculator/Calculator.csproj",
                        "Succeeded (exit 0)",
                        productAlias),
                    CreateToolReceipt("workspace_write_file", BuildStepArtifactRef(assignment), "Succeeded: Created file.")
                ]);

            Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
            Assert.NotEmpty(result.ProducedArtifacts);
            Assert.Empty(result.Diagnostics);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_completion_requires_declared_product_file_content_check()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var solutionFile = Path.Combine(outputRoot, "Calculator.slnx");
            File.WriteAllText(solutionFile, "<Solution></Solution>");
            var appProjectFile = Path.Combine(outputRoot, "src", "Calculator", "Calculator.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(appProjectFile)!);
            File.WriteAllText(appProjectFile, "<Project />");

            var requiredFileContentChecks = JsonSerializer.Serialize(new object[]
            {
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["pathCandidates"] = new[] { solutionFile },
                    ["requiredTextAnyGroups"] = new[]
                    {
                        new[]
                        {
                            Path.Combine("src", "Calculator", "Calculator.csproj"),
                            "src/Calculator/Calculator.csproj"
                        }
                    }
                }
            });
            var baseAssignment = CreateProductMutationAssignment(outputRoot);
            var assignment = baseAssignment with
            {
                StepKey = "create-dotnet-project",
                LaunchVariables = WithLaunchVariables(
                    baseAssignment,
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks, requiredFileContentChecks),
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts, "workspace_pwsh_run_script"))
            };
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Created the app project and ran the solution membership helper.",
                    EvidenceRefs = [BuildStepArtifactRef(assignment)],
                    NextActions = []
                },
                [
                    CreateToolReceipt(
                        "workspace_pwsh_run_script",
                        $"dotnet sln add {productAlias}/src/Calculator/Calculator.csproj",
                        "Succeeded (exit 0)",
                        productAlias),
                    CreateToolReceipt("workspace_write_file", BuildStepArtifactRef(assignment), "Succeeded: Created file.")
                ]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("process.adapter.product_required_file_content_missing", diagnostic.Code.Value);
            Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
            Assert.Contains("Calculator.slnx", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("src/Calculator/Calculator.csproj", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_blocked_after_script_denial_and_helper_write_requests_ordering_retry()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var appProjectFile = Path.Combine(outputRoot, "src", "Calculator", "Calculator.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(appProjectFile)!);
            File.WriteAllText(appProjectFile, "<Project />");

            var requiredToolReceiptMap = JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["add-test-project"] = ["workspace_pwsh_run_script"]
            });
            var baseAssignment = CreateProductMutationAssignment(outputRoot);
            var assignment = baseAssignment with
            {
                StepKey = "add-test-project",
                LaunchVariables = WithLaunchVariables(
                    baseAssignment,
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep, requiredToolReceiptMap))
            };
            var helperRef = $"artifacts/process-runs/{assignment.RunId.Value:D}/steps/add-test-project.ps1";
            var primaryRef = BuildStepArtifactRef(assignment);
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Blocked,
                    Reason = "Manager action request: grant or reassign workspace_pwsh_run_script because the first script invocation was denied before the helper existed.",
                    EvidenceRefs = [primaryRef],
                    NextActions = ["Manager action request: grant or reassign the script tool."],
                    HumanReadableSummaryMarkdown = "Blocked after script denial."
                },
                [
                    CreateToolReceipt(
                        "workspace_pwsh_run_script",
                        "pwsh_run_script",
                        "Denied"),
                    CreateToolReceipt(
                        "workspace_write_file",
                        helperRef,
                        "Succeeded: Created file."),
                    CreateToolReceipt(
                        "workspace_write_file",
                        primaryRef,
                        "Succeeded: Created file.")
                ]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("process.adapter.product_required_tool_receipt_blocked_retry", diagnostic.Code.Value);
            Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
            Assert.Contains("helper script write receipt", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not a manager grant or reassignment case", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_completion_requires_product_target_mutation_receipt_when_receipts_are_available()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(outputRoot, "TetrisGame.csproj"), "<Project />");
            var assignment = CreateProductMutationAssignment(outputRoot);
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Implemented the app.",
                    EvidenceRefs = [BuildStepArtifactRef(assignment)],
                    NextActions = []
                },
                [CreateToolReceipt("workspace_write_file", BuildStepArtifactRef(assignment), "Succeeded: Created file.")]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.product_mutation_receipt_missing");
            Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.product_mutation_receipt_missing");
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Contains("writing only artifacts/process-runs", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_blocker_with_only_managed_artifact_write_requests_safe_retry()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(outputRoot, "Calculator.csproj"), "<Project />");
            var assignment = CreateProductMutationAssignment(outputRoot);
            var primaryRef = BuildStepArtifactRef(assignment);
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Blocked,
                    Reason = "The feature change cannot be accepted because no product-target mutation receipt exists yet.",
                    EvidenceRefs = [primaryRef],
                    NextActions = ["Apply the feature files under the product target and update the change-set artifact."]
                },
                [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("process.adapter.product_mutation_blocked_retry", diagnostic.Code.Value);
            Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
            Assert.Equal(ProcessDiagnosticIdempotencyClassification.Idempotent, diagnostic.Idempotency);
            Assert.Contains("product-target mutation receipt", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.product_mutation_blocked_retry");
            Assert.Contains(result.RequestedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_blocker_with_speculative_rights_text_requests_safe_retry_without_denial_receipt()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var assignment = CreateProductMutationAssignment(outputRoot) with
            {
                StepKey = "create-dotnet-project"
            };
            var primaryRef = BuildStepArtifactRef(assignment);
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Blocked,
                    Reason = "No scaffold command was executed yet, so the contracted solution/app files are not present.",
                    EvidenceRefs = [primaryRef],
                    NextActions =
                    [
                        "Run the approved scaffold commands.",
                        "If the current process contract expects a later step to perform scaffold execution, grant the missing MutateProductTarget/scaffold execution right to the current agent."
                    ],
                    HumanReadableSummaryMarkdown = "Blocked because the scaffold work was not attempted yet."
                },
                [
                    CreateToolReceipt("workspace_create_directory", productAlias, "Succeeded: Created directory."),
                    CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")
                ]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.product_mutation_blocked_retry" &&
                diagnostic.RetrySafety == ProcessDiagnosticRetrySafety.SafeToRetry);
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.agent_rights_request");
            Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.product_mutation_blocked_retry");
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_blocker_with_missing_required_state_requests_safe_retry_even_after_partial_mutation()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var solutionFile = Path.Combine(outputRoot, "Calculator.slnx");
            File.WriteAllText(solutionFile, "<Solution></Solution>");
            Directory.CreateDirectory(Path.Combine(outputRoot, "src"));
            var appProjectFile = Path.Combine(outputRoot, "src", "Calculator", "Calculator.csproj");
            var requiredFileContentChecks = JsonSerializer.Serialize(new object[]
            {
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["pathCandidates"] = new[] { solutionFile },
                    ["requiredTextAnyGroups"] = new[]
                    {
                        new[]
                        {
                            Path.Combine("src", "Calculator", "Calculator.csproj"),
                            "src/Calculator/Calculator.csproj"
                        }
                    }
                }
            });
            var baseAssignment = CreateProductMutationAssignment(outputRoot);
            var assignment = baseAssignment with
            {
                StepKey = "create-dotnet-project",
                LaunchVariables = WithLaunchVariables(
                    baseAssignment,
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths, appProjectFile),
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks, requiredFileContentChecks),
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts, "workspace_dotnet_new;workspace_pwsh_run_script"))
            };
            var primaryRef = BuildStepArtifactRef(assignment);
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Blocked,
                    Reason = "The solution membership helper failed because the app project file is missing.",
                    EvidenceRefs = [primaryRef],
                    NextActions = ["Retry setup by scaffolding the app project before running the solution membership helper."]
                },
                [
                    CreateToolReceipt(
                        "workspace_dotnet_new",
                        "new sln -n Calculator",
                        "Succeeded (exit 0)",
                        productAlias),
                    CreateToolReceipt(
                        "workspace_pwsh_run_script",
                        "dotnet sln Calculator.slnx add src/Calculator/Calculator.csproj",
                        "Succeeded (exit 0)",
                        productAlias),
                    CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")
                ]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("process.adapter.product_required_state_blocked_retry", diagnostic.Code.Value);
            Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
            Assert.Contains("required product output/readback", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(appProjectFile, result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.product_required_state_blocked_retry");
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_blocker_with_missing_required_state_requests_safe_retry_without_managed_artifact()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var solutionFile = Path.Combine(outputRoot, "Calculator.slnx");
            File.WriteAllText(solutionFile, "<Solution></Solution>");
            Directory.CreateDirectory(Path.Combine(outputRoot, "src"));
            var appProjectFile = Path.Combine(outputRoot, "src", "Calculator", "Calculator.csproj");
            var baseAssignment = CreateProductMutationAssignment(outputRoot);
            var assignment = baseAssignment with
            {
                StepKey = "create-dotnet-project",
                LaunchVariables = WithLaunchVariables(
                    baseAssignment,
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths, appProjectFile))
            };
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Blocked,
                    Reason = "The contracted app project file is missing after creating the solution file.",
                    EvidenceRefs = [],
                    NextActions = ["Run the app-project scaffold before finalizing setup."]
                },
                [
                    CreateToolReceipt(
                        "workspace_dotnet_new",
                        "new sln -n Calculator",
                        "Succeeded (exit 0)",
                        productAlias)
                ]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("process.adapter.product_required_state_blocked_retry", diagnostic.Code.Value);
            Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
            Assert.Contains(appProjectFile, result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.product_required_state_blocked_retry");
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_blocker_with_missing_required_tool_receipt_requests_safe_retry()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var appProjectFile = Path.Combine(outputRoot, "src", "Calculator", "Calculator.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(appProjectFile)!);
            File.WriteAllText(Path.Combine(outputRoot, "Calculator.slnx"), "<Solution />");
            File.WriteAllText(appProjectFile, "<Project />");

            var requiredToolReceiptMap = JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["create-dotnet-project"] = ["workspace_dotnet_new", "workspace_pwsh_run_script"]
            });
            var baseAssignment = CreateProductMutationAssignment(outputRoot);
            var assignment = baseAssignment with
            {
                StepKey = "create-dotnet-project",
                LaunchVariables = WithLaunchVariables(
                    baseAssignment,
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep, requiredToolReceiptMap))
            };
            var primaryRef = BuildStepArtifactRef(assignment);
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Blocked,
                    Reason = "The solution and app project were scaffolded, but the project still needs to be added to the solution with the required helper.",
                    EvidenceRefs = [primaryRef],
                    NextActions = ["Run the required helper and record the successful receipt."]
                },
                [
                    CreateToolReceipt(
                        "workspace_dotnet_new",
                        $"{productAlias}/src/Calculator",
                        "Succeeded (exit 0)",
                        productAlias),
                    CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")
                ]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("process.adapter.product_required_tool_receipt_blocked_retry", diagnostic.Code.Value);
            Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
            Assert.Equal(ProcessDiagnosticIdempotencyClassification.Idempotent, diagnostic.Idempotency);
            Assert.Contains("workspace_pwsh_run_script", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.product_required_tool_receipt_blocked_retry");
            Assert.Contains(result.RequestedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_blocker_with_missing_required_tool_receipt_requests_safe_retry_without_managed_artifact()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var appProjectFile = Path.Combine(outputRoot, "src", "Calculator", "Calculator.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(appProjectFile)!);
            File.WriteAllText(Path.Combine(outputRoot, "Calculator.slnx"), "<Solution />");
            File.WriteAllText(appProjectFile, "<Project />");

            var requiredToolReceiptMap = JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["create-dotnet-project"] = ["workspace_pwsh_run_script"]
            });
            var baseAssignment = CreateProductMutationAssignment(outputRoot);
            var assignment = baseAssignment with
            {
                StepKey = "create-dotnet-project",
                LaunchVariables = WithLaunchVariables(
                    baseAssignment,
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep, requiredToolReceiptMap))
            };
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Blocked,
                    Reason = "The app project exists, but the required solution-membership helper was not run.",
                    EvidenceRefs = [],
                    NextActions = ["Run the required helper and record the successful receipt."]
                },
                [
                    CreateToolReceipt(
                        "workspace_dotnet_new",
                        $"{productAlias}/src/Calculator",
                        "Succeeded (exit 0)",
                        productAlias)
                ]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("process.adapter.product_required_tool_receipt_blocked_retry", diagnostic.Code.Value);
            Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
            Assert.Contains("workspace_pwsh_run_script", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.product_required_tool_receipt_blocked_retry");
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_completion_accepts_product_target_and_managed_artifact_write_receipts()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(outputRoot, "TetrisGame.csproj"), "<Project />");
            var assignment = CreateProductMutationAssignment(outputRoot);
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Implemented the app.",
                    EvidenceRefs = [BuildStepArtifactRef(assignment)],
                    NextActions = []
                },
                [
                    CreateToolReceipt("workspace_write_file", $"{productAlias}/TetrisGame.csproj", "Succeeded: Created file."),
                    CreateToolReceipt("workspace_write_file", BuildStepArtifactRef(assignment), "Succeeded: Created file.")
                ]);

            Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
            Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            Assert.Empty(result.Diagnostics);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_completion_accepts_product_target_mutation_receipt_working_directory()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(outputRoot, "Calculator.csproj"), "<Project />");
            var assignment = CreateProductMutationAssignment(outputRoot);
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Created the app scaffold.",
                    EvidenceRefs = [BuildStepArtifactRef(assignment)],
                    NextActions = []
                },
                [
                    CreateToolReceipt(
                        "workspace_dotnet_new",
                        "new blazorwasm -n Calculator",
                        "Succeeded (exit 0)",
                        productAlias),
                    CreateToolReceipt("workspace_write_file", BuildStepArtifactRef(assignment), "Succeeded: Created file.")
                ]);

            Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
            Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            Assert.Empty(result.Diagnostics);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Branch_gated_product_mutation_repair_accepts_validation_receipt_without_new_mutation()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(outputRoot, "Calculator.csproj"), "<Project />");
            var assignment = CreateProductMutationAssignment(outputRoot) with
            {
                StepKey = "repair-solution-setup",
                AllowedOperations =
                [
                    ProcessOperationContractNames.MutateProductTarget,
                    ProcessOperationContractNames.RunValidation,
                    ProcessOperationContractNames.WriteManagedProcessArtifacts
                ],
                BranchGate = new ProcessRuntimeBranchGate("validate-first-build", "repair-required")
            };
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "The repair path confirmed the product target now builds.",
                    EvidenceRefs = [BuildStepArtifactRef(assignment)],
                    NextActions = []
                },
                [
                    CreateToolReceipt(
                        "workspace_dotnet_build",
                        $"{productAlias}/Calculator.slnx",
                        "Succeeded (exit 0)"),
                    CreateToolReceipt("workspace_write_file", BuildStepArtifactRef(assignment), "Succeeded: Created file.")
                ]);

            Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
            Assert.Empty(result.Diagnostics);
            Assert.NotEmpty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Non_branch_product_mutation_completion_still_requires_product_mutation_receipt_after_validation()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(outputRoot, "Calculator.csproj"), "<Project />");
            var assignment = CreateProductMutationAssignment(outputRoot) with
            {
                AllowedOperations =
                [
                    ProcessOperationContractNames.MutateProductTarget,
                    ProcessOperationContractNames.RunValidation,
                    ProcessOperationContractNames.WriteManagedProcessArtifacts
                ]
            };
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Validated the existing app without changing product files.",
                    EvidenceRefs = [BuildStepArtifactRef(assignment)],
                    NextActions = []
                },
                [
                    CreateToolReceipt(
                        "workspace_dotnet_build",
                        $"{productAlias}/Calculator.slnx",
                        "Succeeded (exit 0)"),
                    CreateToolReceipt("workspace_write_file", BuildStepArtifactRef(assignment), "Succeeded: Created file.")
                ]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.product_mutation_receipt_missing");
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Non_terminal_primary_artifact_blocker_requests_safe_retry()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var assignment = CreateProductMutationAssignment(outputRoot);
            var primaryRef = BuildStepArtifactRef(assignment);
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Blocked,
                    Reason = "Recovered governed process step outcome from primary managed artifact after provider timeout.",
                    EvidenceRefs = [primaryRef],
                    NextActions = ["Review the primary managed artifact."],
                    HumanReadableSummaryMarkdown =
                        $"""
                        Recovered outcome from primary process artifact `{primaryRef}` after provider timeout.

                        # Product setup change set

                        Status: InProgress  # Product setup change set
                        """
                },
                [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("process.adapter.non_terminal_primary_artifact_retry", diagnostic.Code.Value);
            Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
            Assert.Equal(ProcessDiagnosticIdempotencyClassification.Idempotent, diagnostic.Idempotency);
            Assert.Contains("final Status line", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.non_terminal_primary_artifact_retry");
            Assert.Contains(result.RequestedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Managed_artifact_completion_requires_evidence_for_produced_slot()
    {
        var assignment = CreateManagedArtifactAssignment("review-architecture-design");
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Reviewed the architecture.",
                EvidenceRefs = [$"artifacts/process-runs/{assignment.RunId.Value:D}/steps/other-step.md"],
                NextActions = []
            });

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.produced_artifact_evidence_missing");
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.produced_artifact_evidence_missing");
        Assert.Contains(result.RequestedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
        Assert.Empty(result.ProducedArtifacts);
    }

    [Fact]
    public void Managed_artifact_completion_accepts_step_directory_evidence()
    {
        var assignment = CreateManagedArtifactAssignment("review-architecture-design");
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Reviewed the architecture.",
                EvidenceRefs = [BuildStepDirectoryArtifactRef(assignment, "architecture-review-findings.md")],
                NextActions = []
            });

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Managed_artifact_completion_accepts_scoped_workspace_step_evidence()
    {
        var assignment = CreateManagedArtifactAssignment("review-architecture-design");
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Reviewed the architecture.",
                EvidenceRefs = [$"artifacts/scopes/organization/e5df9ad633dbc6974a0678a74976013c/process-runs/{assignment.RunId.Value:D}/steps/{assignment.StepKey}.md"],
                NextActions = []
            });

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Managed_artifact_completion_requires_write_receipt_when_receipts_are_available()
    {
        var assignment = CreateManagedArtifactAssignment("feature-intake");
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Wrote the scope packet.",
                EvidenceRefs = [BuildStepArtifactRef(assignment)],
                NextActions = []
            },
            [CreateToolReceipt("workspace_read_file", BuildStepArtifactRef(assignment), "Succeeded: Read file.")]);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.produced_artifact_write_receipt_missing");
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.produced_artifact_write_receipt_missing");
        Assert.Empty(result.ProducedArtifacts);
    }

    [Fact]
    public void Managed_artifact_completion_rejects_completed_outcome_that_declares_remaining_blocker()
    {
        var assignment = CreateManagedArtifactAssignment("runtime-command-handoff");
        var primaryRef = BuildStepArtifactRef(assignment);
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Runtime handoff is complete. Remaining blocker: launcher-compatible readback receipts are missing.",
                EvidenceRefs = [primaryRef],
                NextActions = []
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote file.")]);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.completed_outcome_declares_unresolved_blocker" &&
            diagnostic.RetrySafety == ProcessDiagnosticRetrySafety.SafeToRetry);
        Assert.Empty(result.ProducedArtifacts);
    }

    [Fact]
    public void Read_only_qa_acceptance_enforces_branch_specific_product_file_content_checks()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var scaffoldPage = Path.Combine(outputRoot, "src", "App", "Pages", "Counter.razor");
            Directory.CreateDirectory(Path.GetDirectoryName(scaffoldPage)!);
            File.WriteAllText(scaffoldPage, "@page \"/counter\"");
            var baseAssignment = CreateManagedArtifactAssignment("qa-validation");
            var checks = JsonSerializer.Serialize(new Dictionary<string, object[]>
            {
                ["qa-validation"] =
                [
                    new Dictionary<string, object>
                    {
                        ["pathCandidates"] = new[] { scaffoldPage },
                        ["mustExist"] = false,
                        ["enforceBranchOutcomeKeys"] = new[] { "quality-accepted" },
                        ["forbiddenTextAny"] = new[] { "@page \"/counter\"" }
                    }
                ]
            });
            var assignment = baseAssignment with
            {
                LaunchVariables = WithLaunchVariables(
                    baseAssignment,
                    ("ProductRoot", outputRoot),
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep, checks))
            };
            var primaryRef = BuildStepArtifactRef(assignment);

            var acceptedResult = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "QA accepted.",
                    BranchOutcomeKey = "quality-accepted",
                    EvidenceRefs = [primaryRef],
                    NextActions = []
                },
                [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote file.")]);
            var repairResult = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "QA found a scaffold defect and routed repair.",
                    BranchOutcomeKey = "repair-required",
                    EvidenceRefs = [primaryRef],
                    NextActions = []
                },
                [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote file.")]);

            Assert.Equal(StrategyOutcome.NeedsManager, acceptedResult.Outcome);
            Assert.Contains(acceptedResult.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.product_required_file_content_missing");
            Assert.Equal(StrategyOutcome.Succeeded, repairResult.Outcome);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Managed_artifact_completion_accepts_scoped_workspace_write_receipt()
    {
        var assignment = CreateManagedArtifactAssignment("feature-intake");
        var scopedPath = $"artifacts/scopes/organization/e5df9ad633dbc6974a0678a74976013c/process-runs/{assignment.RunId.Value:D}/steps/{assignment.StepKey}.md";
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Wrote the scope packet.",
                EvidenceRefs = [BuildStepArtifactRef(assignment)],
                NextActions = []
            },
            [CreateToolReceipt("workspace_write_file", scopedPath, "Succeeded: Wrote file.")]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Managed_artifact_completion_rejects_ungrounded_path_like_outcome_refs()
    {
        var assignment = CreateManagedArtifactAssignment("feature-intake");
        var primaryRef = BuildStepArtifactRef(assignment);
        var staleRef = @"C:\other-projects\stale-source\scope.md";
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Wrote the current scope packet.",
                EvidenceRefs = [primaryRef],
                NextActions = [],
                HumanReadableSummaryMarkdown = $"Completed from source document `{staleRef}`."
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote file.")]);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.ungrounded_outcome_reference" &&
            diagnostic.RetrySafety == ProcessDiagnosticRetrySafety.SafeToRetry);
        Assert.Contains("not grounded in the current step brief", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(staleRef, result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.ProducedArtifacts);
    }

    [Fact]
    public void Managed_artifact_completion_removes_non_citable_source_metadata_from_structured_outcome()
    {
        var assignment = CreateManagedArtifactAssignment("feature-intake");
        var primaryRef = BuildStepArtifactRef(assignment);
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason =
                    """
                    Wrote the current scope packet.
                    SourceDocName: managed-files/project-media/calculator/proposal.md
                    """,
                EvidenceRefs =
                [
                    primaryRef,
                    "SourceDocLink: artifacts/scopes/organization/demo/managed-files/project-media/calculator/proposal.md"
                ],
                NextActions =
                [
                    "Review the current managed process artifact.",
                    @"SourceDocName: C:\Users\lucys\AppData\Local\CanDoItAll\workspace\managed-files\proposal.md"
                ],
                HumanReadableSummaryMarkdown =
                    """
                    Completed with current-run evidence.
                    SourceDocLink: managed-files/project-media/calculator/proposal.md
                    """
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote file.")]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.ungrounded_outcome_reference");
        Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
        Assert.DoesNotContain("SourceDocName", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SourceDocLink", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("managed-files", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Managed_artifact_completion_accepts_path_like_refs_grounded_by_launch_variables()
    {
        var baseAssignment = CreateManagedArtifactAssignment("feature-intake");
        var groundedRef = "managed-files/project-media/images/be2ebfd7776643f99b2e8051d0b0d99d/generated-image-0502ef6bcfb84a00bd8fae708fbf7c14.png";
        var assignment = baseAssignment with
        {
            LaunchVariables = WithLaunchVariables(
                baseAssignment,
                ("ProjectStructureContextSummary", $"Visual target asset: {groundedRef}"))
        };
        var primaryRef = BuildStepArtifactRef(assignment);
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Wrote the current scope packet.",
                EvidenceRefs = [primaryRef],
                NextActions = [],
                HumanReadableSummaryMarkdown = $"Completed from visual target `{groundedRef}`."
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote file.")]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.ungrounded_outcome_reference");
        Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
    }

    [Fact]
    public void Managed_artifact_completion_accepts_child_path_refs_under_grounded_launch_variable_root()
    {
        var baseAssignment = CreateManagedArtifactAssignment("add-tests-and-proof");
        const string productRoot = "external-target/C/programovani/dotnet/calculator-output";
        const string productFile = $"{productRoot}/tests/Calculator.Tests/Calculator.Tests.csproj";
        var assignment = baseAssignment with
        {
            LaunchVariables = WithLaunchVariables(
                baseAssignment,
                ("ExternalTargetRoot", productRoot))
        };
        var primaryRef = BuildStepArtifactRef(assignment);
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Recorded current product validation proof.",
                EvidenceRefs = [primaryRef],
                NextActions = [],
                HumanReadableSummaryMarkdown = $"Validation included the grounded product file `{productFile}`."
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote file.")]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.ungrounded_outcome_reference");
        Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
    }

    [Fact]
    public void Managed_artifact_self_evidence_blocker_with_required_input_is_retryable()
    {
        var assignment = CreateManagedArtifactAssignment(
            "implementation-approach",
            requiredArtifactSlotIds: [ArtifactSlotId.New()]);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Blocked,
                Reason = "No prior assistant text, tool output, or process artifact evidence is available in the conversation to support a completed submission.",
                BranchOutcomeKey = "insufficient_evidence",
                BranchOutcomeTitle = "Insufficient Evidence",
                EvidenceRefs = [],
                NextActions = ["Provide current-run evidence references before completing."],
                HumanReadableSummaryMarkdown = "I cannot complete the process-step outcome because there is no available evidence."
            });

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("process.adapter.managed_artifact_self_evidence_retry", diagnostic.Code.Value);
        Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
        Assert.Equal(ProcessDiagnosticIdempotencyClassification.Idempotent, diagnostic.Idempotency);
        Assert.Contains(BuildStepArtifactRef(assignment), diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.managed_artifact_self_evidence_retry");
        Assert.Contains(result.RequestedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
    }

    [Fact]
    public void Managed_artifact_missing_primary_output_blocker_is_retryable_when_step_can_write_artifacts()
    {
        var assignment = CreateManagedArtifactAssignment(
            "qa-validation",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            requiredArtifactSlotIds: [ArtifactSlotId.New()]);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Blocked,
                Reason = $"The current-run QA validation primary output ref was not created. Create the required primary managed artifact at {BuildStepArtifactRef(assignment)} with workspace_write_file.",
                BranchOutcomeKey = "qa-validation.blocked.missing-primary-output",
                BranchOutcomeTitle = "Blocked: QA validation primary managed output not written",
                EvidenceRefs = ["artifacts/process-runs/run-001/steps/implementation.md"],
                NextActions = [$"Create the required primary managed QA artifact at {BuildStepArtifactRef(assignment)} with the validation proof pack."],
                HumanReadableSummaryMarkdown = "QA validation cannot be finalized because the required primary managed output was not written."
            });

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("process.adapter.managed_artifact_missing_primary_output_retry", diagnostic.Code.Value);
        Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
        Assert.Equal(ProcessDiagnosticIdempotencyClassification.Idempotent, diagnostic.Idempotency);
        Assert.Contains(BuildStepArtifactRef(assignment), diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.managed_artifact_missing_primary_output_retry");
        Assert.Contains(result.RequestedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
    }

    [Fact]
    public void Blocked_result_with_branch_outcome_and_evidence_routes_branch()
    {
        var assignment = CreateManagedArtifactAssignment(
            "validate-first-build",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            requiredArtifactSlotIds: [ArtifactSlotId.New()]);
        var primaryRef = BuildStepArtifactRef(assignment);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Blocked,
                Reason = "Restore/build/test discovery could not validate the scaffold and repair is required.",
                BranchOutcomeKey = "setup-repair-required",
                BranchOutcomeTitle = "Setup repair required",
                EvidenceRefs = [primaryRef],
                NextActions = ["Repair the scaffold using the recorded validation evidence."],
                HumanReadableSummaryMarkdown = "Setup repair is required."
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
        Assert.Empty(result.Diagnostics);
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == ProcessBranchSignalCodes.Outcome("setup-repair-required").Value);
    }

    [Fact]
    public void Blocked_result_that_textually_selects_single_declared_branch_routes_branch()
    {
        var baseAssignment = CreateManagedArtifactAssignment(
            "targeted-validation",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            requiredArtifactSlotIds: [ArtifactSlotId.New()]);
        var assignment = baseAssignment with
        {
            Prompt = """
            Branch outcomes:
            - feature-accepted: Feature accepted - Validation passed.
            - feature-repair-required: Repair required - Validation failed but repair is possible.
            """
        };
        var primaryRef = BuildStepArtifactRef(assignment);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Blocked,
                Reason = "Build proof failed after validation commands ran.",
                EvidenceRefs = [primaryRef],
                NextActions = ["Route to the implementation repair lane."],
                HumanReadableSummaryMarkdown = "Branch outcome: feature-repair-required"
            },
            [
                CreateToolReceipt("workspace_dotnet_build", "build Calculator.slnx", "Failed (exit 1)"),
                CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")
            ]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
        Assert.Empty(result.Diagnostics);
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == ProcessBranchSignalCodes.Outcome("feature-repair-required").Value);
    }

    [Fact]
    public void Completed_result_that_textually_selects_single_declared_branch_emits_branch_signal()
    {
        var baseAssignment = CreateManagedArtifactAssignment(
            "targeted-validation",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            requiredArtifactSlotIds: [ArtifactSlotId.New()]);
        var assignment = baseAssignment with
        {
            Prompt = """
            Branch outcomes:
            - feature-accepted: Feature accepted - Validation passed.
            - feature-repair-required: Repair required - Validation failed but repair is possible.
            """
        };
        var primaryRef = BuildStepArtifactRef(assignment);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Recovered governed process step outcome from primary managed artifact after provider timeout.",
                EvidenceRefs = [primaryRef],
                NextActions = ["Repair the compile error and rerun focused validation."],
                HumanReadableSummaryMarkdown = "Status: Completed\n\n## Validation result\n**Branch outcome:** feature-repair-required"
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
        Assert.Empty(result.Diagnostics);
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == ProcessBranchSignalCodes.Outcome("feature-repair-required").Value);
    }

    [Fact]
    public void Completed_result_recovered_from_explicit_branch_outcome_key_ignores_boundary_words()
    {
        var baseAssignment = CreateManagedArtifactAssignment(
            "targeted-validation",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            requiredArtifactSlotIds: [ArtifactSlotId.New()]);
        var assignment = baseAssignment with
        {
            Prompt = """
            Available branch outcomes:
            - feature-accepted: Feature accepted - Validation passed.
            - feature-repair-required: Repair required - Validation failed but repair is possible.
            """,
            LaunchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] =
                    "workspace_dotnet_restore;workspace_dotnet_build;workspace_dotnet_test"
            }
        };
        var primaryRef = BuildStepArtifactRef(assignment);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Recovered governed process step outcome from primary managed artifact after provider timeout.",
                EvidenceRefs = [primaryRef],
                NextActions = ["Repair the failed focused test and rerun targeted validation."],
                HumanReadableSummaryMarkdown = """
                Status: Completed
                Branch outcome key: feature-repair-required

                ## Focused validation commands
                - workspace_dotnet_restore succeeded for the current run.
                - workspace_dotnet_build succeeded for the current run.
                - workspace_dotnet_test failed for the current run.

                ## Failure summary
                - The right operand behavior needs repair before acceptance.
                """
            },
            [
                CreateToolReceipt("workspace_dotnet_restore", "restore Calculator.slnx", "Succeeded (exit 0)"),
                CreateToolReceipt("workspace_dotnet_build", "build Calculator.slnx", "Succeeded (exit 0)"),
                CreateToolReceipt("workspace_dotnet_test", "test Calculator.slnx", "Failed (exit 1)"),
                CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")
            ]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
        Assert.Empty(result.Diagnostics);
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == ProcessBranchSignalCodes.Outcome("feature-repair-required").Value);
    }

    [Fact]
    public void Completed_result_recovered_from_branch_outcome_key_section_emits_branch_signal()
    {
        var baseAssignment = CreateManagedArtifactAssignment(
            "targeted-validation",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            requiredArtifactSlotIds: [ArtifactSlotId.New()]);
        var assignment = baseAssignment with
        {
            Prompt = """
            Available branch outcomes:
            - feature-accepted: Feature accepted - Validation passed.
            - feature-repair-required: Repair required - Validation failed but repair is possible.
            """
        };
        var primaryRef = BuildStepArtifactRef(assignment);
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Recovered governed process step outcome from primary managed artifact after provider timeout.",
            EvidenceRefs = [primaryRef],
            HumanReadableSummaryMarkdown = """
            Status: Completed

            ## Branch outcome key
            feature-accepted

            ## Focused validation commands
            - workspace_dotnet_restore succeeded for the current run.
            - workspace_dotnet_build succeeded for the current run.
            - workspace_dotnet_test succeeded for the current run.
            """
        };

        var result = ToAdapterResult(
            assignment,
            output,
            [
                CreateToolReceipt("workspace_dotnet_restore", "restore Calculator.slnx", "Succeeded (exit 0)"),
                CreateToolReceipt("workspace_dotnet_build", "build Calculator.slnx", "Succeeded (exit 0)"),
                CreateToolReceipt("workspace_dotnet_test", "test Calculator.slnx", "Succeeded (exit 0)"),
                CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")
            ]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
        Assert.Empty(result.Diagnostics);
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == ProcessBranchSignalCodes.Outcome("feature-accepted").Value);
    }

    [Fact]
    public void Completed_result_that_textually_declares_validation_decision_emits_branch_signal()
    {
        var baseAssignment = CreateManagedArtifactAssignment(
            "targeted-validation",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            requiredArtifactSlotIds: [ArtifactSlotId.New()]);
        var assignment = baseAssignment with
        {
            Prompt = """
            Branch outcomes:
            - feature-accepted: Feature accepted - Validation passed.
            - feature-repair-required: Repair required - Validation failed but repair is possible.
            """
        };
        var primaryRef = BuildStepArtifactRef(assignment);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Recovered governed process step outcome from primary managed artifact after provider timeout.",
                EvidenceRefs = [primaryRef],
                NextActions = [],
                HumanReadableSummaryMarkdown = "Status: Completed\n\n## Validation decision\nfeature-accepted"
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == ProcessBranchSignalCodes.Outcome("feature-accepted").Value);
    }

    [Fact]
    public void Completed_result_with_ambiguous_validation_decision_section_does_not_emit_branch_signal()
    {
        var baseAssignment = CreateManagedArtifactAssignment(
            "targeted-validation",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            requiredArtifactSlotIds: [ArtifactSlotId.New()]);
        var assignment = baseAssignment with
        {
            Prompt = """
            Branch outcomes:
            - feature-accepted: Feature accepted - Validation passed.
            - feature-repair-required: Repair required - Validation failed but repair is possible.
            """
        };
        var primaryRef = BuildStepArtifactRef(assignment);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Recovered governed process step outcome from primary managed artifact after provider timeout.",
                EvidenceRefs = [primaryRef],
                NextActions = [],
                HumanReadableSummaryMarkdown = """
                Status: Completed

                ## Validation decision
                feature-accepted
                feature-repair-required
                """
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.ManagerSignals);
    }

    [Fact]
    public void Completed_result_recovered_from_artifact_can_infer_single_declared_branch_title()
    {
        var baseAssignment = CreateManagedArtifactAssignment(
            "validate-first-build",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            requiredArtifactSlotIds: [ArtifactSlotId.New()]);
        var assignment = baseAssignment with
        {
            Prompt = """
            Available branch outcomes:
            - setup-validated: Setup validated - Restore, build, and tests are green enough for handoff.
            - setup-repair-required: Setup repair required - Repairable scaffold failure.
            """
        };
        var primaryRef = BuildStepArtifactRef(assignment);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Recovered governed process step outcome from primary managed artifact after provider timeout.",
                EvidenceRefs = [primaryRef],
                NextActions = [],
                HumanReadableSummaryMarkdown = "Status: Completed\n\n## Outcome\n- Setup validated for handoff.\n- Restore, build, and test discovery all succeeded."
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == ProcessBranchSignalCodes.Outcome("setup-validated").Value);
    }

    [Fact]
    public void Managed_artifact_rights_blocker_is_not_reclassified_as_self_evidence_retry()
    {
        var assignment = CreateManagedArtifactAssignment(
            "implementation-approach",
            requiredArtifactSlotIds: [ArtifactSlotId.New()]);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Blocked,
                Reason = "The step is blocked because workspace_write_file was blocked by policy.",
                EvidenceRefs = [],
                NextActions = ["Manager action request: grant workspace_write_file or reassign the step."],
                HumanReadableSummaryMarkdown = "Denied tool: workspace_write_file."
            });

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.managed_artifact_self_evidence_retry");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.agent_rights_request");
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.agent_rights_request");
    }


    [Fact]
    public void Runtime_readiness_rejects_delivery_manager_for_implementation_step()
    {
        var deliveryManager = NewAgent(
            "Delivery Manager",
            "Delivery Manager",
            AgentWorkloadKind.Management,
            [
                "delivery-manager",
                "process-mock-role:delivery-manager"
            ],
            AgentWorkspaceToolProfileKind.ReadOnly);

        var readiness = AgentProcessReadinessEvaluator.Evaluate(
            deliveryManager,
            new AgentProcessRoleReadinessRequest(
                "implement-code-change",
                "implement-code-change",
                "delivery-manager",
                "delivery-manager",
                "Delivery Manager",
                [ProcessOperationContractNames.MutateProductTarget],
                ProcessOperationContractNames.ExternalProductTargetMutable));

        Assert.False(readiness.HasRoleFit);
        Assert.False(readiness.IsExecutionReady);
        Assert.Contains(readiness.Findings, finding => finding.Code == "agent.readiness.role-family-mismatch");
        Assert.Contains(readiness.Findings, finding => finding.Code == "agent.readiness.workspace-write-files-missing");
    }

    [Fact]
    public void Runtime_readiness_accepts_delivery_manager_for_local_runtime_command_role()
    {
        var deliveryManager = NewAgent(
            "Delivery Manager",
            "Delivery Manager",
            AgentWorkloadKind.Management,
            [
                "delivery-manager",
                "process-mock-role:delivery-manager"
            ],
            AgentWorkspaceToolProfileKind.BusinessAnalysis);
        var assignment = new ProcessRuntimeStepAssignment(
            ProcessRunId.New(),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "resolve-dotnet-run-commands",
            "runtime-command-recorder",
            "delivery-manager",
            "Runtime command recorder",
            ProcessLaunchExecutorKinds.Agent,
            deliveryManager.Id.ToString("D"),
            deliveryManager.Name,
            "Resolve runtime commands.",
            "sha256:readiness",
            "Resolved from role fit.",
            [ArtifactSlotId.New()],
            [],
            [ProcessOperationContractNames.WriteManagedProcessArtifacts],
            ProcessOperationContractNames.ExternalProductTargetReadOnly,
            new Dictionary<string, string>(),
            BranchGate: null,
            DateTimeOffset.UtcNow);

        var request = CreateRuntimeReadinessRequest(assignment);
        var readiness = AgentProcessReadinessEvaluator.Evaluate(deliveryManager, request);

        Assert.Equal("runtime-command-recorder", request.RoleKey);
        Assert.Equal("delivery-manager", request.RoleResourceKey);
        Assert.Equal("Runtime command recorder", request.RoleDisplayName);
        Assert.True(readiness.HasRoleFit);
        Assert.True(readiness.IsExecutionReady);
    }

    [Fact]
    public void Runtime_readiness_rejects_required_script_receipt_when_agent_lacks_local_scripts()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var developer = NewAgent(
                ".NET Developer",
                ".NET Developer",
                AgentWorkloadKind.Programming,
                [
                    "dotnet-developer",
                    "application-developer"
                ],
                AgentWorkspaceToolProfileKind.Custom) with
            {
                ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write(
                    "{}",
                    new AgentWorkspaceToolAccessSettings
                    {
                        Profile = AgentWorkspaceToolProfileKind.Custom,
                        CanReadFiles = true,
                        CanWriteFiles = true,
                        CanRunValidationCommands = true,
                        CanScaffoldProjects = true,
                        CanManageWorkspacePaths = true
                    })
            };
            var assignment = CreateProductMutationAssignment(outputRoot) with
            {
                StepKey = "add-test-project",
                RoleKey = "dotnet-developer",
                RoleResourceKey = "dotnet-developer",
                RoleDisplayName = ".NET developer",
                LaunchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] = "workspace_pwsh_run_script"
                }
            };

            var request = CreateRuntimeReadinessRequest(assignment);
            var readiness = AgentProcessReadinessEvaluator.Evaluate(developer, request);

            Assert.Contains("workspace_pwsh_run_script", request.RequiredRuntimeToolNames ?? []);
            Assert.True(readiness.HasRoleFit);
            Assert.False(readiness.IsExecutionReady);
            Assert.Contains(readiness.Findings, finding => finding.Code == "agent.readiness.required-tool-local-scripts-missing");
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Runtime_readiness_rejects_required_workspace_tool_when_agent_lacks_capability_assignment()
    {
        var qaAgent = NewAgent(
            "QA Capture",
            "QA lead",
            AgentWorkloadKind.Qa,
            [
                "qa-lead",
                "browser"
            ],
            AgentWorkspaceToolProfileKind.QualityValidation,
            [
                new AgentCapabilityAssignment(
                    Guid.NewGuid(),
                    "workspace-dotnet-build",
                    CapabilityKind.Tool,
                    CapabilityProofStatus.Verified,
                    DateTimeOffset.UtcNow,
                    "Build tool is assigned.")
            ]);
        var assignment = CreateManagedArtifactAssignment(
            "capture-ui-screenshots",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.LaunchRuntime,
                ProcessOperationContractNames.CaptureRuntimeProof,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ]) with
        {
            RoleKey = "qa-lead",
            RoleResourceKey = "qa-lead",
            RoleDisplayName = "QA lead",
            LaunchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] = "workspace_dotnet_run"
            }
        };

        var request = CreateRuntimeReadinessRequest(assignment);
        var readiness = AgentProcessReadinessEvaluator.Evaluate(qaAgent, request);

        Assert.Contains("workspace_dotnet_run", request.RequiredRuntimeToolNames ?? []);
        Assert.True(readiness.HasRoleFit);
        Assert.False(readiness.IsExecutionReady);
        Assert.Contains(readiness.Findings, finding => finding.Code == "agent.readiness.required-tool-capability-missing");
    }

    [Fact]
    public void Runtime_readiness_rejects_required_browser_tool_when_agent_lacks_playwright_mcp()
    {
        var qaAgent = NewAgent(
            "QA Capture",
            "QA lead",
            AgentWorkloadKind.Qa,
            [
                "qa-lead",
                "browser"
            ],
            AgentWorkspaceToolProfileKind.QualityValidation);
        var assignment = CreateManagedArtifactAssignment(
            "capture-ui-screenshots",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.LaunchRuntime,
                ProcessOperationContractNames.CaptureRuntimeProof,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ]) with
        {
            RoleKey = "qa-lead",
            RoleResourceKey = "qa-lead",
            RoleDisplayName = "QA lead",
            CapabilityScope = new ProcessCapabilityScope
            {
                RequiredReceipts =
                [
                    new ProcessRequiredToolReceipt
                    {
                        Key = "browser-proof",
                        Kind = ProcessRequiredToolReceiptKind.RuntimeToolName,
                        ToolName = "browser_take_screenshot"
                    }
                ]
            }
        };

        var request = CreateRuntimeReadinessRequest(assignment);
        var readiness = AgentProcessReadinessEvaluator.Evaluate(qaAgent, request);

        Assert.Contains("browser_take_screenshot", request.RequiredRuntimeToolNames ?? []);
        Assert.True(readiness.HasRoleFit);
        Assert.False(readiness.IsExecutionReady);
        Assert.Contains(readiness.Findings, finding => finding.Code == "agent.readiness.required-browser-tool-missing");
    }

    [Fact]
    public void Runtime_readiness_rejects_required_project_structure_write_tool_when_agent_is_read_only()
    {
        var qaAgent = NewAgent(
            "QA Capture",
            "QA lead",
            AgentWorkloadKind.Qa,
            [
                "qa-lead",
                "browser"
            ],
            AgentWorkspaceToolProfileKind.QualityValidation) with
        {
            ConfigurationJson = AgentProjectStructureAccessMetadata.Write(
                AgentWorkspaceToolAccessMetadata.Write(
                    "{}",
                    new AgentWorkspaceToolAccessSettings
                    {
                        Profile = AgentWorkspaceToolProfileKind.QualityValidation
                    }),
                new AgentProjectStructureAccessSettings
                {
                    CanRead = true,
                    CanWrite = false,
                    AllowAllProjects = true
                })
        };
        var assignment = CreateManagedArtifactAssignment(
            "store-ui-screenshots",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.CaptureRuntimeProof,
                ProcessOperationContractNames.WriteManagedProcessArtifacts,
                ProcessOperationContractNames.ExecuteExternalAction
            ]) with
        {
            RoleKey = "qa-lead",
            RoleResourceKey = "qa-lead",
            RoleDisplayName = "QA lead",
            LaunchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] = "project_structure_node_create;project_structure_asset_create"
            }
        };

        var request = CreateRuntimeReadinessRequest(assignment);
        var readiness = AgentProcessReadinessEvaluator.Evaluate(qaAgent, request);

        Assert.Contains("project_structure_node_create", request.RequiredRuntimeToolNames ?? []);
        Assert.Contains("project_structure_asset_create", request.RequiredRuntimeToolNames ?? []);
        Assert.True(readiness.HasRoleFit);
        Assert.False(readiness.IsExecutionReady);
        Assert.Contains(readiness.Findings, finding =>
            finding.Code == "agent.readiness.required-project-structure-write-missing" &&
            finding.Message.Contains("project_structure_asset_create", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Runtime_readiness_rejects_qa_review_lead_for_solution_architect_role()
    {
        var qaReviewLead = NewAgent(
            ".NET QA Review Lead",
            ".NET QA Review Lead",
            AgentWorkloadKind.Qa,
            [
                "qa-lead",
                "dotnet",
                "architecture",
                "review"
            ],
            AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var architect = NewAgent(
            ".NET Architect",
            ".NET Architect",
            AgentWorkloadKind.Programming,
            [
                "solution-architect",
                "dotnet-architect",
                "architecture"
            ],
            AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var request = new AgentProcessRoleReadinessRequest(
            "architecture-review",
            "Run .NET architecture design and review subprocess",
            "solution-architect",
            "solution-architect",
            "Solution architect",
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.WriteManagedProcessArtifacts,
                ProcessOperationContractNames.ExecuteExternalAction,
                ProcessOperationContractNames.LaunchRuntime,
                ProcessOperationContractNames.CaptureRuntimeProof
            ],
            ProcessOperationContractNames.ExternalActionControlled);

        var qaReadiness = AgentProcessReadinessEvaluator.Evaluate(qaReviewLead, request);
        var architectReadiness = AgentProcessReadinessEvaluator.Evaluate(architect, request);

        Assert.False(qaReadiness.HasRoleFit);
        Assert.False(qaReadiness.IsExecutionReady);
        Assert.Contains(qaReadiness.Findings, finding => finding.Code == "agent.readiness.role-family-mismatch");
        Assert.True(architectReadiness.HasRoleFit);
        Assert.True(architectReadiness.IsExecutionReady);
        Assert.True(architectReadiness.Score > qaReadiness.Score);
    }

    [Fact]
    public void Runtime_readiness_rejects_generic_code_reviewer_capability_for_solution_architect_role()
    {
        var codeReviewLead = NewAgent(
            "Code Review Lead",
            "Code reviewer",
            AgentWorkloadKind.Qa,
            [
                "review",
                "code",
                "quality"
            ],
            AgentWorkspaceToolProfileKind.QualityValidation,
            [
                new AgentCapabilityAssignment(
                    Guid.NewGuid(),
                    "architecture-source-rag",
                    CapabilityKind.Rag,
                    CapabilityProofStatus.Verified,
                    DateTimeOffset.UtcNow,
                    "Available for architecture source lookup.")
            ]);
        var dotnetArchitect = NewAgent(
            ".NET Solution Architect",
            ".NET architecture specialist",
            AgentWorkloadKind.Programming,
            [
                "dotnet",
                "architecture",
                "blazor"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var request = new AgentProcessRoleReadinessRequest(
            "architecture-review",
            "Run .NET architecture design and review subprocess",
            "solution-architect",
            "solution-architect",
            ".NET Architect",
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.WriteManagedProcessArtifacts,
                ProcessOperationContractNames.ExecuteExternalAction
            ],
            ProcessOperationContractNames.ExternalActionControlled);

        var reviewerReadiness = AgentProcessReadinessEvaluator.Evaluate(codeReviewLead, request);
        var architectReadiness = AgentProcessReadinessEvaluator.Evaluate(dotnetArchitect, request);

        Assert.False(reviewerReadiness.HasRoleFit);
        Assert.False(reviewerReadiness.IsExecutionReady);
        Assert.Contains(reviewerReadiness.Findings, finding => finding.Code == "agent.readiness.role-family-mismatch");
        Assert.True(architectReadiness.HasRoleFit);
        Assert.True(architectReadiness.IsExecutionReady);
        Assert.True(architectReadiness.Score > reviewerReadiness.Score);
    }

    [Fact]
    public void Blocked_missing_tool_result_adds_manager_rights_request_signal()
    {
        var assignment = CreateControlledExternalActionAssignment(ProcessRunId.New());
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Blocked,
                Reason = "PolicyDenied: Tool 'workspace_read_file' was denied for this governed process step because the external-target path is outside the workspace boundary.",
                EvidenceRefs = [],
                NextActions =
                [
                    "Manager action: grant workspace_read_file access to the assigned agent or reassign the step."
                ]
            });

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.agent_rights_request");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.agent_rights_request");
        Assert.Contains(".NET Solution Architect", result.UserSafeSummary, StringComparison.Ordinal);
        Assert.Contains("workspace_read_file", result.UserSafeSummary, StringComparison.Ordinal);
        Assert.Contains(ProcessOperationContractNames.ExecuteExternalAction, result.UserSafeSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pending_child_run_detection_defers_blocked_controlled_subprocess_step()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var assignment = CreateControlledExternalActionAssignment(parentRunId);
        var stateStore = new InMemoryRuntimeStateStore(
            NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
            NewRuntimeState(parentRunId, childRunId, ProcessRuntimeStatus.Active));
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Blocked,
            Reason = $"Child process run {childRunId} is still producing architecture evidence.",
            EvidenceRefs = [$"artifacts/process-runs/{childRunId}/steps/classify-dotnet-application.md"],
            NextActions = []
        };

        var pendingRunId = await AgentFrameworkProcessExecutionAdapter.TryResolvePendingChildRunAsync(
            assignment,
            output,
            stateStore);

        Assert.Equal(childRunId, pendingRunId);
    }

    [Fact]
    public async Task Pending_child_run_detection_ignores_terminal_child_run()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var assignment = CreateControlledExternalActionAssignment(parentRunId);
        var stateStore = new InMemoryRuntimeStateStore(
            NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
            NewRuntimeState(parentRunId, childRunId, ProcessRuntimeStatus.Completed));
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Blocked,
            Reason = $"Child process run {childRunId} completed without required evidence.",
            EvidenceRefs = [$"artifacts/process-runs/{childRunId}/steps/architecture-handoff.md"],
            NextActions = []
        };

        var pendingRunId = await AgentFrameworkProcessExecutionAdapter.TryResolvePendingChildRunAsync(
            assignment,
            output,
            stateStore);

        Assert.Null(pendingRunId);
    }

    [Fact]
    public async Task Pending_child_run_detection_ignores_current_root_and_parent_run_references()
    {
        var rootRunId = ProcessRunId.New();
        var parentRunId = ProcessRunId.New();
        var currentRunId = ProcessRunId.New();
        var assignment = CreateControlledExternalActionAssignment(currentRunId) with
        {
            StepKey = "store-ui-screenshots",
            LaunchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = parentRunId.ToString()
            }
        };
        var stateStore = new InMemoryRuntimeStateStore(
            NewRuntimeState(rootRunId, rootRunId, ProcessRuntimeStatus.Active),
            NewRuntimeState(rootRunId, parentRunId, ProcessRuntimeStatus.Active),
            NewRuntimeState(rootRunId, currentRunId, ProcessRuntimeStatus.Active));
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Blocked,
            Reason = $"Current run does not include accepted screenshots for parent run {parentRunId} in root run {rootRunId}.",
            EvidenceRefs =
            [
                $"artifacts/process-runs/{rootRunId}/steps/capture-ui-screenshots.md",
                $"artifacts/process-runs/{parentRunId}/steps/capture-ui-screenshots.md"
            ],
            NextActions =
            [
                $"Re-run capture for the current process run {currentRunId}."
            ]
        };

        var pendingRunId = await AgentFrameworkProcessExecutionAdapter.TryResolvePendingChildRunAsync(
            assignment,
            output,
            stateStore);

        Assert.Null(pendingRunId);
    }

    [Fact]
    public async Task Existing_child_run_detection_defers_before_reinvoking_parent_agent()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var assignment = CreateControlledExternalActionAssignment(parentRunId);
        var childAssignment = CreateChildAssignment(
            childRunId,
            parentRunId,
            assignment.StepInstanceId,
            assignment.StepKey);
        var assignmentStore = new InMemoryAssignmentStore(childAssignment);
        var stateStore = new InMemoryRuntimeStateStore(
            NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
            NewRuntimeState(parentRunId, childRunId, ProcessRuntimeStatus.Active));

        var pendingRunId = await AgentFrameworkProcessExecutionAdapter.TryResolveExistingPendingChildRunAsync(
            assignment,
            assignmentStore,
            stateStore);

        Assert.Equal(childRunId, pendingRunId);
    }

    [Fact]
    public async Task ExecuteAsync_launches_mapped_subprocess_before_invoking_parent_agent()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var agent = NewAgent(
            ".NET Application Developer",
            ".NET implementation specialist",
            AgentWorkloadKind.Programming,
            [
                "software-engineer",
                "dotnet"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateSubprocessAssignment() with
        {
            RunId = parentRunId,
            ExecutorId = agent.Id.ToString("D")
        };
        var childEvidenceRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/slice-handoff.md";
        var parentDeferredOutcomeJson = $$"""
            {
              "status": "Blocked",
              "reason": "Waiting for active child process run {{childRunId.Value:D}} to finish and materialize required evidence.",
              "branchOutcomeKey": "",
              "branchOutcomeTitle": "",
              "evidenceRefs": [
                "{{childEvidenceRef}}"
              ],
              "nextActions": [
                "Wait for active child process run {{childRunId.Value:D}} to produce required evidence."
              ],
              "humanReadableSummaryMarkdown": "Waiting for active child process run `{{childRunId.Value:D}}`."
            }
            """;
        var launchCoordinator = new FakeSubprocessLaunchCoordinator(
            new ProcessSubprocessLaunchCoordinatorResult(
                "dotnet-development-slice",
                childRunId,
                "Running",
                parentDeferredOutcomeJson,
                [childEvidenceRef],
                []));
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The parent agent should not be invoked for a mapped subprocess launch."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);

        try
        {
            var adapter = new AgentFrameworkProcessExecutionAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(
                    NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                    NewRuntimeState(parentRunId, childRunId, ProcessRuntimeStatus.Active)),
                workspaceFiles,
                [launchCoordinator]);

            var exception = await Assert.ThrowsAsync<ProcessRuntimeDispatchDeferredException>(() =>
                adapter.ExecuteAsync(
                    new ProcessExecutionAdapterRequest(
                        parentRunId,
                        assignment.StepInstanceId,
                        ProcessExecutionAdapterKind.Workflow,
                        new ProcessExecutionAdapterOperationKey("execute"),
                        Binding,
                        [],
                        [])).AsTask());

            Assert.Equal(childRunId, exception.DeferredRunId);
            Assert.True(launchCoordinator.Called);
            Assert.False(workspace.ExecuteRunCalled);
            Assert.Equal(assignment.StepInstanceId, launchCoordinator.LastRequest?.ParentAssignment.StepInstanceId);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_defers_mapped_subprocess_when_running_child_is_not_yet_state_visible()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var agent = NewAgent(
            ".NET Application Developer",
            ".NET implementation specialist",
            AgentWorkloadKind.Programming,
            [
                "software-engineer",
                "dotnet"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateSubprocessAssignment() with
        {
            RunId = parentRunId,
            ExecutorId = agent.Id.ToString("D")
        };
        var childEvidenceRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/screenshot-handoff.md";
        var parentDeferredOutcomeJson = $$"""
            {
              "status": "Blocked",
              "reason": "Waiting for active child process run {{childRunId.Value:D}} to finish and materialize required evidence.",
              "branchOutcomeKey": "",
              "branchOutcomeTitle": "",
              "evidenceRefs": [
                "{{childEvidenceRef}}"
              ],
              "nextActions": [
                "Wait for active child process run {{childRunId.Value:D}} to produce required evidence."
              ],
              "humanReadableSummaryMarkdown": "Waiting for active child process run `{{childRunId.Value:D}}`."
            }
            """;
        var launchCoordinator = new FakeSubprocessLaunchCoordinator(
            new ProcessSubprocessLaunchCoordinatorResult(
                "dotnet-ui-screenshot-writeback",
                childRunId,
                ProcessLaunchStage.Running.ToString(),
                parentDeferredOutcomeJson,
                [childEvidenceRef],
                []));
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The parent agent should not be invoked for a mapped subprocess launch."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);

        try
        {
            var adapter = new AgentFrameworkProcessExecutionAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active)),
                workspaceFiles,
                [launchCoordinator]);

            var exception = await Assert.ThrowsAsync<ProcessRuntimeDispatchDeferredException>(() =>
                adapter.ExecuteAsync(
                    new ProcessExecutionAdapterRequest(
                        parentRunId,
                        assignment.StepInstanceId,
                        ProcessExecutionAdapterKind.Workflow,
                        new ProcessExecutionAdapterOperationKey("execute"),
                        Binding,
                        [],
                        [])).AsTask());

            Assert.Equal(childRunId, exception.DeferredRunId);
            Assert.True(launchCoordinator.Called);
            Assert.False(workspace.ExecuteRunCalled);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_blocks_mapped_subprocess_without_coordinator_before_invoking_parent_agent()
    {
        var parentRunId = ProcessRunId.New();
        var agent = NewAgent(
            ".NET Application Developer",
            ".NET implementation specialist",
            AgentWorkloadKind.Programming,
            [
                "software-engineer",
                "dotnet"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateSubprocessAssignment() with
        {
            RunId = parentRunId,
            ExecutorId = agent.Id.ToString("D")
        };
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The parent agent should not be invoked when no subprocess launch coordinator is registered."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = new AgentFrameworkProcessExecutionAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                new ProcessExecutionAdapterRequest(
                    parentRunId,
                    assignment.StepInstanceId,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.False(workspace.ExecuteRunCalled);
            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Code.Value == "process.adapter.subprocess_launch_coordinator_unavailable");
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_completes_subprocess_parent_from_completed_child_without_reinvoking_agent()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var agent = NewAgent(
            ".NET Application Developer",
            ".NET implementation specialist",
            AgentWorkloadKind.Programming,
            [
                "software-engineer",
                "dotnet"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateSubprocessAssignment() with
        {
            RunId = parentRunId,
            ExecutorId = agent.Id.ToString("D")
        };
        var childAssignment = CreateChildAssignment(
            childRunId,
            parentRunId,
            assignment.StepInstanceId,
            assignment.StepKey);
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        var childEvidenceRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/{childAssignment.StepKey}.md";
        var writeChildEvidence = workspaceFiles.WriteTextFile(
            childEvidenceRef,
            "Status: Completed\n\nChild evidence.",
            overwrite: true);
        Assert.True(writeChildEvidence.Succeeded);
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The parent agent should not be reinvoked for a completed child run."));
        var adapter = new AgentFrameworkProcessExecutionAdapter(
            new FakeWorkspaceFactory(workspace),
            CreateReferenceDataProvider(workspace),
            new InMemoryAssignmentStore(assignment, childAssignment),
            new InMemoryRuntimeStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                NewRuntimeState(parentRunId, childRunId, ProcessRuntimeStatus.Completed)),
            workspaceFiles);

        try
        {
            var result = await adapter.ExecuteAsync(
                new ProcessExecutionAdapterRequest(
                    parentRunId,
                    assignment.StepInstanceId,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
            Assert.False(workspace.ExecuteRunCalled);
            Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            var parentArtifact = workspaceFiles.ReadTextFile(BuildStepArtifactRef(assignment));
            Assert.True(parentArtifact.Succeeded);
            Assert.Contains(childRunId.Value.ToString("D"), parentArtifact.Content, StringComparison.Ordinal);
            Assert.Contains(childEvidenceRef, parentArtifact.Content, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_defers_when_agent_execution_fails_after_child_run_was_created()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var agent = NewAgent(
            ".NET Solution Architect",
            ".NET architecture specialist",
            AgentWorkloadKind.Programming,
            [
                "solution-architect",
                "dotnet",
                "architecture"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var parentAssignment = CreateControlledExternalActionAssignment(parentRunId, agent.Id);
        var childAssignment = CreateChildAssignment(
            childRunId,
            parentRunId,
            parentAssignment.StepInstanceId,
            parentAssignment.StepKey);
        var assignmentStore = new InMemoryAssignmentStore(parentAssignment);
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("Provider runtime failed after provider activity."),
            () => assignmentStore.Add(childAssignment));
        var adapter = new AgentFrameworkProcessExecutionAdapter(
            new FakeWorkspaceFactory(workspace),
            CreateReferenceDataProvider(workspace),
            assignmentStore,
            new InMemoryRuntimeStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                NewRuntimeState(parentRunId, childRunId, ProcessRuntimeStatus.Active)),
            CreateWorkspaceFileService(out _));

        var exception = await Assert.ThrowsAsync<ProcessRuntimeDispatchDeferredException>(() =>
            adapter.ExecuteAsync(
                new ProcessExecutionAdapterRequest(
                    parentRunId,
                    parentAssignment.StepInstanceId,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    [])).AsTask());

        Assert.Equal(childRunId, exception.DeferredRunId);
        Assert.True(workspace.ExecuteRunCalled);
    }

    [Fact]
    public async Task ExecuteAsync_retries_when_agent_misses_required_process_finalizer()
    {
        var agent = NewAgent(
            ".NET Solution Architect",
            "Solution architect",
            AgentWorkloadKind.Programming,
            [
                "solution-architect",
                "dotnet",
                "architecture"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateManagedArtifactAssignment("feature-intake", agent.Id);
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("Finalizer tool 'submit_process_step_outcome' in Required mode failed validation. Errors: agent.finalizer.missing."));
        var adapter = new AgentFrameworkProcessExecutionAdapter(
            new FakeWorkspaceFactory(workspace),
            CreateReferenceDataProvider(workspace),
            new InMemoryAssignmentStore(assignment),
            new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
            CreateWorkspaceFileService(out _));

        var result = await adapter.ExecuteAsync(
            new ProcessExecutionAdapterRequest(
                assignment.RunId,
                assignment.StepInstanceId,
                ProcessExecutionAdapterKind.Workflow,
                new ProcessExecutionAdapterOperationKey("execute"),
                Binding,
                [],
                []));

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("process.adapter.agent_output_contract_retryable", diagnostic.Code.Value);
        Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
        Assert.Equal(ProcessDiagnosticIdempotencyClassification.Idempotent, diagnostic.Idempotency);
        Assert.Contains(BuildStepArtifactRef(assignment), result.UserSafeSummary, StringComparison.Ordinal);
        Assert.Contains(result.RequestedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
        Assert.True(workspace.ExecuteRunCalled);
    }

    [Fact]
    public async Task ExecuteAsync_retries_transient_provider_failure_result()
    {
        var agent = NewAgent(
            ".NET Solution Architect",
            "Solution architect",
            AgentWorkloadKind.Programming,
            [
                "solution-architect",
                "dotnet",
                "architecture"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateManagedArtifactAssignment("architecture-handoff", agent.Id);
        var executionRunId = Guid.NewGuid();
        var responseText = "The agent run failed while using provider 'OpenAI default'. Provider detail: Service request failed. Status: 520 (<none>)";
        var workspace = new ThrowingWorkspaceService(
            agent,
            executeException: null,
            executeResult: CreateExecutionRunResult(agent.Id, executionRunId, responseText, RunOutcome.Failed));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = new AgentFrameworkProcessExecutionAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                new ProcessExecutionAdapterRequest(
                    assignment.RunId,
                    assignment.StepInstanceId,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("process.adapter.agent_transient_execution_retry", diagnostic.Code.Value);
            Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
            Assert.Equal(ProcessDiagnosticIdempotencyClassification.Idempotent, diagnostic.Idempotency);
            Assert.Contains("Status: 520", result.UserSafeSummary, StringComparison.Ordinal);
            Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.agent_transient_execution_retry");
            Assert.Contains(result.RequestedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            Assert.True(workspace.ExecuteRunCalled);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_retries_transient_provider_initialization_exception()
    {
        var agent = NewAgent(
            ".NET Solution Architect",
            "Solution architect",
            AgentWorkloadKind.Programming,
            [
                "solution-architect",
                "dotnet",
                "architecture"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateManagedArtifactAssignment("targeted-validation", agent.Id);
        var executionRunId = Guid.NewGuid();
        var exception = new AgentRunFailedException(
            agent.Id,
            executionRunId,
            chatSessionId: null,
            "OpenAI default",
            "gpt-5.4-mini",
            new TimeoutException("Initialization timed out while composing the provider capability set."),
            "The agent run failed while using provider 'OpenAI default'. Provider detail: Initialization timed out while composing the provider capability set.");
        var workspace = new ThrowingWorkspaceService(agent, exception);
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = new AgentFrameworkProcessExecutionAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                new ProcessExecutionAdapterRequest(
                    assignment.RunId,
                    assignment.StepInstanceId,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("process.adapter.agent_transient_execution_retry", diagnostic.Code.Value);
            Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
            Assert.Equal(ProcessDiagnosticIdempotencyClassification.Idempotent, diagnostic.Idempotency);
            Assert.Contains("Initialization timed out", result.UserSafeSummary, StringComparison.Ordinal);
            Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.agent_transient_execution_retry");
            Assert.Contains(result.RequestedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            Assert.True(workspace.ExecuteRunCalled);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_materializes_managed_artifact_from_valid_structured_outcome()
    {
        var agent = NewAgent(
            ".NET Solution Architect",
            "Solution architect",
            AgentWorkloadKind.Programming,
            [
                "solution-architect",
                "dotnet",
                "architecture"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateManagedArtifactAssignment("feature-intake", agent.Id);
        var executionRunId = Guid.NewGuid();
        var responseText = """
            {
              "status": "Completed",
              "reason": "Clarified the requested software scope.",
              "branchOutcomeKey": "scope-clarified",
              "branchOutcomeTitle": "Scope clarified",
              "evidenceRefs": [
                "https://example.invalid/external-evidence.md"
              ],
              "nextActions": [],
              "humanReadableSummaryMarkdown": "The software scope is clarified and ready for architecture."
            }
            """;
        var workspace = new ThrowingWorkspaceService(
            agent,
            executeException: null,
            executeResult: CreateExecutionRunResult(agent.Id, executionRunId, responseText),
            executionDetail: CreateExecutionRunDetail(agent.Id, executionRunId, responseText, []));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = new AgentFrameworkProcessExecutionAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                new ProcessExecutionAdapterRequest(
                    assignment.RunId,
                    assignment.StepInstanceId,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.True(
                result.Outcome == StrategyOutcome.Succeeded,
                result.UserSafeSummary);
            Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            Assert.Empty(result.Diagnostics);
            var artifactPath = Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                assignment.RunId.Value.ToString("D"),
                "steps",
                "feature-intake.md");
            Assert.True(File.Exists(artifactPath));
            var content = await File.ReadAllTextAsync(artifactPath);
            Assert.Contains("Runtime persisted this managed artifact", content, StringComparison.Ordinal);
            Assert.Contains("Clarified the requested software scope.", content, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_appends_validated_outcome_to_existing_managed_artifact()
    {
        var agent = NewAgent(
            ".NET Solution Architect",
            "Solution architect",
            AgentWorkloadKind.Programming,
            [
                "dotnet",
                "solution-architect",
                "architecture"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateManagedArtifactAssignment("validate-first-build", agent.Id);
        var executionRunId = Guid.NewGuid();
        var primaryRef = BuildStepArtifactRef(assignment);
        var responseText = $$"""
            {
              "status": "Completed",
              "reason": "Restore, build, and tests completed successfully. Restore exit code 0; build exit code 0 with 0 warnings and 0 errors; tests exit code 0.",
              "branchOutcomeKey": "",
              "branchOutcomeTitle": "",
              "evidenceRefs": [
                "{{primaryRef}}"
              ],
              "nextActions": [],
              "humanReadableSummaryMarkdown": "Restore, build, and test proof is green."
            }
            """;
        var workspace = new ThrowingWorkspaceService(
            agent,
            executeException: null,
            executeResult: CreateExecutionRunResult(agent.Id, executionRunId, responseText),
            executionDetail: CreateExecutionRunDetail(
                agent.Id,
                executionRunId,
                responseText,
                [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")]));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var artifactPath = Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                assignment.RunId.Value.ToString("D"),
                "steps",
                "validate-first-build.md");
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            await File.WriteAllTextAsync(
                artifactPath,
                """
                # Validate first build

                ## Results

                """);

            var adapter = new AgentFrameworkProcessExecutionAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                new ProcessExecutionAdapterRequest(
                    assignment.RunId,
                    assignment.StepInstanceId,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.True(
                result.Outcome == StrategyOutcome.Succeeded,
                result.UserSafeSummary);
            Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            var content = await File.ReadAllTextAsync(artifactPath);
            Assert.Contains("# Validate first build", content, StringComparison.Ordinal);
            Assert.Contains("Runtime Validated Structured Outcome", content, StringComparison.Ordinal);
            Assert.Contains("Restore, build, and tests completed successfully", content, StringComparison.Ordinal);
            Assert.Contains("Restore, build, and test proof is green.", content, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_rejects_ungrounded_refs_in_written_managed_artifact()
    {
        var agent = NewAgent(
            ".NET Solution Architect",
            "Solution architect",
            AgentWorkloadKind.Programming,
            [
                "solution-architect",
                "dotnet",
                "architecture"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateManagedArtifactAssignment("feature-intake", agent.Id);
        var executionRunId = Guid.NewGuid();
        var primaryRef = BuildStepArtifactRef(assignment);
        var staleRef = @"C:\other-projects\stale-source\scope.md";
        var responseText = $$"""
            {
              "status": "Completed",
              "reason": "Clarified the requested software scope.",
              "branchOutcomeKey": "scope-clarified",
              "branchOutcomeTitle": "Scope clarified",
              "evidenceRefs": [
                "{{primaryRef}}"
              ],
              "nextActions": [],
              "humanReadableSummaryMarkdown": "The software scope is clarified and ready for architecture."
            }
            """;
        var workspace = new ThrowingWorkspaceService(
            agent,
            executeException: null,
            executeResult: CreateExecutionRunResult(agent.Id, executionRunId, responseText),
            executionDetail: CreateExecutionRunDetail(
                agent.Id,
                executionRunId,
                responseText,
                [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")]));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var artifactPath = Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                assignment.RunId.Value.ToString("D"),
                "steps",
                "feature-intake.md");
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            await File.WriteAllTextAsync(
                artifactPath,
                $$"""
                # Feature intake

                Status: Completed

                ## Current Scope

                The current scope packet cites unrelated source material at `{{staleRef}}`.
                """);

            var adapter = new AgentFrameworkProcessExecutionAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                new ProcessExecutionAdapterRequest(
                    assignment.RunId,
                    assignment.StepInstanceId,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.ungrounded_managed_artifact_reference" &&
                diagnostic.RetrySafety == ProcessDiagnosticRetrySafety.SafeToRetry);
            Assert.Contains(primaryRef, result.UserSafeSummary, StringComparison.Ordinal);
            Assert.DoesNotContain(staleRef, result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_removes_non_citable_source_metadata_lines_from_written_managed_artifact()
    {
        var agent = NewAgent(
            ".NET Solution Architect",
            "Solution architect",
            AgentWorkloadKind.Programming,
            [
                "solution-architect",
                "dotnet",
                "architecture"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateManagedArtifactAssignment("feature-intake", agent.Id);
        var executionRunId = Guid.NewGuid();
        var primaryRef = BuildStepArtifactRef(assignment);
        var sourceDocName = @"managed-files\project-media\files\ee266fad590440ff9b30d96804aadcb2\office365-category-email-summary-c5314b7569594cf78a14ca05b234cca9.md";
        var sourceDocLink = @"C:\Users\lucys\AppData\Local\CanDoItAll\workspace\managed-files\project-media\files\ee266fad590440ff9b30d96804aadcb2\office365-category-email-summary-c5314b7569594cf78a14ca05b234cca9.md";
        var responseText = $$"""
            {
              "status": "Completed",
              "reason": "Clarified the requested software scope.",
              "branchOutcomeKey": "scope-clarified",
              "branchOutcomeTitle": "Scope clarified",
              "evidenceRefs": [
                "{{primaryRef}}"
              ],
              "nextActions": [],
              "humanReadableSummaryMarkdown": "The software scope is clarified and ready for architecture."
            }
            """;
        var workspace = new ThrowingWorkspaceService(
            agent,
            executeException: null,
            executeResult: CreateExecutionRunResult(agent.Id, executionRunId, responseText),
            executionDetail: CreateExecutionRunDetail(
                agent.Id,
                executionRunId,
                responseText,
                [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")]));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var artifactPath = Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                assignment.RunId.Value.ToString("D"),
                "steps",
                "feature-intake.md");
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            await File.WriteAllTextAsync(
                artifactPath,
                $$"""
                # Feature intake

                Status: Completed

                ## Source notes used
                - SourceDocName: {{sourceDocName}}
                - SourceDocLink: {{sourceDocLink}}
                - Source document cited: {{sourceDocLink}}
                - ProjectStructureContextSummary facts for current node.
                """);

            var adapter = new AgentFrameworkProcessExecutionAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                new ProcessExecutionAdapterRequest(
                    assignment.RunId,
                    assignment.StepInstanceId,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
            Assert.Empty(result.Diagnostics);
            Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            var content = await File.ReadAllTextAsync(artifactPath);
            Assert.DoesNotContain("SourceDocName", content, StringComparison.Ordinal);
            Assert.DoesNotContain("SourceDocLink", content, StringComparison.Ordinal);
            Assert.DoesNotContain("Source document cited", content, StringComparison.Ordinal);
            Assert.DoesNotContain(sourceDocName, content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(sourceDocLink, content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ProjectStructureContextSummary facts for current node", content, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_accepts_grounded_refs_in_written_managed_artifact()
    {
        var agent = NewAgent(
            ".NET Solution Architect",
            "Solution architect",
            AgentWorkloadKind.Programming,
            [
                "solution-architect",
                "dotnet",
                "architecture"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var baseAssignment = CreateManagedArtifactAssignment("feature-intake", agent.Id);
        var groundedRef = "managed-files/project-media/images/be2ebfd7776643f99b2e8051d0b0d99d/generated-image-0502ef6bcfb84a00bd8fae708fbf7c14.png";
        var assignment = baseAssignment with
        {
            LaunchVariables = WithLaunchVariables(
                baseAssignment,
                ("ProjectStructureContextSummary", $"Visual target asset: {groundedRef}"))
        };
        var executionRunId = Guid.NewGuid();
        var primaryRef = BuildStepArtifactRef(assignment);
        var responseText = $$"""
            {
              "status": "Completed",
              "reason": "Clarified the requested software scope.",
              "branchOutcomeKey": "scope-clarified",
              "branchOutcomeTitle": "Scope clarified",
              "evidenceRefs": [
                "{{primaryRef}}"
              ],
              "nextActions": [],
              "humanReadableSummaryMarkdown": "The software scope is clarified and ready for architecture."
            }
            """;
        var workspace = new ThrowingWorkspaceService(
            agent,
            executeException: null,
            executeResult: CreateExecutionRunResult(agent.Id, executionRunId, responseText),
            executionDetail: CreateExecutionRunDetail(
                agent.Id,
                executionRunId,
                responseText,
                [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")]));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var artifactPath = Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                assignment.RunId.Value.ToString("D"),
                "steps",
                "feature-intake.md");
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            await File.WriteAllTextAsync(
                artifactPath,
                $$"""
                # Feature intake

                Status: Completed

                ## Current Scope

                The current scope packet is grounded by the project visual target at `{{groundedRef}}`.
                """);

            var adapter = new AgentFrameworkProcessExecutionAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                new ProcessExecutionAdapterRequest(
                    assignment.RunId,
                    assignment.StepInstanceId,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.True(
                result.Outcome == StrategyOutcome.Succeeded,
                result.UserSafeSummary);
            Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            Assert.DoesNotContain(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.ungrounded_managed_artifact_reference");
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_accepts_child_path_refs_under_grounded_launch_variable_root_in_written_managed_artifact()
    {
        var agent = NewAgent(
            ".NET Solution Architect",
            "Solution architect",
            AgentWorkloadKind.Programming,
            [
                "solution-architect",
                "dotnet",
                "architecture"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var baseAssignment = CreateManagedArtifactAssignment("add-tests-and-proof", agent.Id);
        const string productRoot = "external-target/C/programovani/dotnet/calculator-output";
        const string productFile = $"{productRoot}/tests/Calculator.Tests/Calculator.Tests.csproj";
        var assignment = baseAssignment with
        {
            LaunchVariables = WithLaunchVariables(
                baseAssignment,
                ("ExternalTargetRoot", productRoot))
        };
        var executionRunId = Guid.NewGuid();
        var primaryRef = BuildStepArtifactRef(assignment);
        var responseText = $$"""
            {
              "status": "Completed",
              "reason": "Validated the current product slice.",
              "branchOutcomeKey": "slice-accepted",
              "branchOutcomeTitle": "Slice accepted",
              "evidenceRefs": [
                "{{primaryRef}}"
              ],
              "nextActions": [],
              "humanReadableSummaryMarkdown": "The current product slice passed validation."
            }
            """;
        var workspace = new ThrowingWorkspaceService(
            agent,
            executeException: null,
            executeResult: CreateExecutionRunResult(agent.Id, executionRunId, responseText),
            executionDetail: CreateExecutionRunDetail(
                agent.Id,
                executionRunId,
                responseText,
                [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")]));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var artifactPath = Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                assignment.RunId.Value.ToString("D"),
                "steps",
                "add-tests-and-proof.md");
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            await File.WriteAllTextAsync(
                artifactPath,
                $$"""
                # Validate tests and targeted proof

                Status: Completed

                ## Evidence

                The current validation covered the grounded product file `{{productFile}}`.
                """);

            var adapter = new AgentFrameworkProcessExecutionAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                new ProcessExecutionAdapterRequest(
                    assignment.RunId,
                    assignment.StepInstanceId,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.True(
                result.Outcome == StrategyOutcome.Succeeded,
                result.UserSafeSummary);
            Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            Assert.DoesNotContain(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.ungrounded_managed_artifact_reference");
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_materializes_pure_producer_self_evidence_blocker()
    {
        var agent = NewAgent(
            ".NET Solution Architect",
            "Solution architect",
            AgentWorkloadKind.Programming,
            [
                "solution-architect",
                "dotnet",
                "architecture"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateManagedArtifactAssignment("feature-intake", agent.Id);
        var executionRunId = Guid.NewGuid();
        var responseText = """
            {
              "status": "Blocked",
              "reason": "Insufficient concrete current-run evidence references found.",
              "branchOutcomeKey": "",
              "branchOutcomeTitle": "",
              "evidenceRefs": [],
              "nextActions": [
                "actionType: CreateConcreteCurrentRunEvidenceReference"
              ],
              "humanReadableSummaryMarkdown": ""
            }
            """;
        var workspace = new ThrowingWorkspaceService(
            agent,
            executeException: null,
            executeResult: CreateExecutionRunResult(agent.Id, executionRunId, responseText),
            executionDetail: CreateExecutionRunDetail(agent.Id, executionRunId, responseText, []));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = new AgentFrameworkProcessExecutionAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                new ProcessExecutionAdapterRequest(
                    assignment.RunId,
                    assignment.StepInstanceId,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.True(
                result.Outcome == StrategyOutcome.Succeeded,
                result.UserSafeSummary);
            Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            var artifactPath = Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                assignment.RunId.Value.ToString("D"),
                "steps",
                "feature-intake.md");
            Assert.True(File.Exists(artifactPath));
            var content = await File.ReadAllTextAsync(artifactPath);
            Assert.Contains("Insufficient concrete current-run evidence references found.", content, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_materializes_pure_producer_missing_own_primary_artifact_blocker()
    {
        var agent = NewAgent(
            ".NET Solution Architect",
            "Solution architect",
            AgentWorkloadKind.Programming,
            [
                "solution-architect",
                "dotnet",
                "architecture"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateManagedArtifactAssignment("feature-intake", agent.Id);
        var executionRunId = Guid.NewGuid();
        var primaryRef = BuildStepArtifactRef(assignment);
        var responseText = $$"""
            {
              "status": "Blocked",
              "reason": "File '{{primaryRef}}' does not exist in the workspace.",
              "branchOutcomeKey": "",
              "branchOutcomeTitle": "",
              "evidenceRefs": [],
              "nextActions": [
                "actionType: SearchForFile; description: Search for file '{{primaryRef}}' in the workspace."
              ],
              "humanReadableSummaryMarkdown": "Workspace tool failed to find requested file '{{primaryRef}}'."
            }
            """;
        var workspace = new ThrowingWorkspaceService(
            agent,
            executeException: null,
            executeResult: CreateExecutionRunResult(agent.Id, executionRunId, responseText),
            executionDetail: CreateExecutionRunDetail(agent.Id, executionRunId, responseText, []));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = new AgentFrameworkProcessExecutionAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                new ProcessExecutionAdapterRequest(
                    assignment.RunId,
                    assignment.StepInstanceId,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.True(
                result.Outcome == StrategyOutcome.Succeeded,
                result.UserSafeSummary);
            Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            var artifactPath = Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                assignment.RunId.Value.ToString("D"),
                "steps",
                "feature-intake.md");
            Assert.True(File.Exists(artifactPath));
            var content = await File.ReadAllTextAsync(artifactPath);
            Assert.Contains(primaryRef, content, StringComparison.Ordinal);
            Assert.Contains("Workspace tool failed to find requested file", content, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    private static ProcessExecutionAdapterResult ToAdapterResult(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts = null,
        Guid? currentExecutionRunId = null)
    {
        var adapterType = typeof(ProcessesModuleServiceCollectionExtensions)
            .Assembly
            .GetType("CanDoItAll.Modules.Processes.AgentFrameworkProcessExecutionAdapter")
            ?? throw new InvalidOperationException("Process execution adapter type was not found.");
        var method = adapterType.GetMethod(
            "ToAdapterResult",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Process execution result mapper was not found.");

        var effectiveExecutionRunId = currentExecutionRunId ?? toolReceipts?.FirstOrDefault()?.ExecutionRunId;
        return Assert.IsType<ProcessExecutionAdapterResult>(method.Invoke(
            null,
            [assignment, output, "sha256:raw", toolReceipts, effectiveExecutionRunId]));
    }

    private static AgentProcessRoleReadinessRequest CreateRuntimeReadinessRequest(ProcessRuntimeStepAssignment assignment)
    {
        var adapterType = typeof(ProcessesModuleServiceCollectionExtensions)
            .Assembly
            .GetType("CanDoItAll.Modules.Processes.AgentFrameworkProcessExecutionAdapter")
            ?? throw new InvalidOperationException("Process execution adapter type was not found.");
        var method = adapterType.GetMethod(
            "CreateRuntimeReadinessRequest",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Process runtime readiness request builder was not found.");

        return Assert.IsType<AgentProcessRoleReadinessRequest>(method.Invoke(null, [assignment]));
    }

    private static ProcessRuntimeStepAssignment CreateProductMutationAssignment(string outputRoot)
    {
        return new ProcessRuntimeStepAssignment(
            ProcessRunId.New(),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "implement-dotnet-app",
            "dotnet-developer",
            "dotnet-developer",
            ".NET developer",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            ".NET Developer",
            "Implement the app in the configured output root.",
            "sha256:readiness",
            "Resolved from role fit.",
            [ArtifactSlotId.New()],
            [],
            [ProcessOperationContractNames.MutateProductTarget],
            ProcessOperationContractNames.ExternalProductTargetMutable,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["OutputRoot"] = outputRoot,
                ["ProductRoot"] = outputRoot,
                ["ExternalTargetRoot"] = outputRoot
            },
            BranchGate: null,
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyDictionary<string, string> WithLaunchVariables(
        ProcessRuntimeStepAssignment assignment,
        params (string Key, string Value)[] values)
    {
        var launchVariables = new Dictionary<string, string>(assignment.LaunchVariables, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            launchVariables[key] = value;
        }

        return launchVariables;
    }

    private static ProcessRuntimeStepAssignment CreateManagedArtifactAssignment(
        string stepKey,
        Guid? agentId = null,
        IReadOnlyList<ArtifactSlotId>? requiredArtifactSlotIds = null,
        IReadOnlyList<string>? allowedOperations = null)
    {
        return new ProcessRuntimeStepAssignment(
            ProcessRunId.New(),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            stepKey,
            "solution-architect",
            "solution-architect",
            "Solution architect",
            ProcessLaunchExecutorKinds.Agent,
            (agentId ?? Guid.NewGuid()).ToString("D"),
            ".NET Solution Architect",
            "Produce managed process evidence.",
            "sha256:readiness",
            "Resolved from role fit.",
            [ArtifactSlotId.New()],
            requiredArtifactSlotIds ?? [],
            allowedOperations ?? [ProcessOperationContractNames.WriteManagedProcessArtifacts],
            ProcessOperationContractNames.ExternalProductTargetReadOnly,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            BranchGate: null,
            DateTimeOffset.UtcNow);
    }

    private static ProcessRuntimeStepAssignment CreateSubprocessAssignment()
    {
        return new ProcessRuntimeStepAssignment(
            ProcessRunId.New(),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "implementation",
            "lead-engineer",
            "lead-engineer",
            "Lead engineer",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            ".NET Application Developer",
            "Launch and observe child subprocess.",
            "sha256:readiness",
            "Resolved from role fit.",
            [ArtifactSlotId.New()],
            [],
            [
                ProcessOperationContractNames.ExecuteExternalAction,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            ProcessOperationContractNames.ExternalActionControlled,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProcessStepKind] = ProcessTemplateStepKinds.Subprocess,
                [ProcessRuntimeLaunchVariables.ProcessStepSubprocessDefinitionKey] = "dotnet-development-slice"
            },
            BranchGate: null,
            DateTimeOffset.UtcNow);
    }

    private static string BuildStepArtifactRef(ProcessRuntimeStepAssignment assignment)
        => $"artifacts/process-runs/{assignment.RunId.Value:D}/steps/{assignment.StepKey}.md";

    private static string BuildStepDirectoryArtifactRef(
        ProcessRuntimeStepAssignment assignment,
        string fileName)
        => $"artifacts/process-runs/{assignment.RunId.Value:D}/{assignment.StepKey}/{fileName}";

    private static ToolExecutionReceiptRecord CreateToolReceipt(
        string toolName,
        string requestSummary,
        string exitSummary,
        string workingDirectory = ".",
        Guid? executionRunId = null)
        => new(
            Guid.NewGuid(),
            executionRunId ?? Guid.NewGuid(),
            "workspace-file",
            toolName,
            "ReadOnlyWorkspace",
            "NotRequired",
            "Workspace file service.",
            requestSummary,
            workingDirectory,
            exitSummary,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private static IWorkspaceFileService CreateWorkspaceFileService(out string workspaceRoot)
    {
        workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.ProcessAdapter.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        return new WorkspaceFileService(workspaceRoot);
    }

    private static ExecutionRunResult CreateExecutionRunResult(
        Guid agentId,
        Guid executionRunId,
        string responseText,
        RunOutcome outcome = RunOutcome.Succeeded)
        => new(
            executionRunId,
            null,
            responseText,
            null,
            new AgentRunMetric(
                Guid.NewGuid(),
                agentId,
                null,
                DateTimeOffset.UtcNow,
                outcome,
                "test-provider",
                "test-model",
                1,
                10,
                2,
                0));

    private static ExecutionRunDetail CreateExecutionRunDetail(
        Guid agentId,
        Guid executionRunId,
        string responseText,
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts)
        => new(
            new ExecutionRunRecord(
                executionRunId,
                agentId,
                ChatSessionId: null,
                "Test execution",
                "process-step",
                "feature-intake",
                "correlation",
                "causation",
                "process-runtime",
                "system",
                "{}",
                "Input",
                responseText,
                "test-provider",
                "test-model",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                string.Empty,
                null,
                [],
                true),
            null,
            [],
            [])
        {
            ToolReceipts = toolReceipts
        };

    private static ProcessRuntimeStepAssignment CreateControlledExternalActionAssignment(
        ProcessRunId runId,
        Guid? agentId = null)
    {
        return new ProcessRuntimeStepAssignment(
            runId,
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "architecture-review",
            "solution-architect",
            "solution-architect",
            "Solution architect",
            ProcessLaunchExecutorKinds.Agent,
            (agentId ?? Guid.NewGuid()).ToString("D"),
            ".NET Solution Architect",
            "Launch and observe the governed architecture subprocess.",
            "sha256:readiness",
            "Resolved from role fit.",
            [],
            [],
            [ProcessOperationContractNames.ExecuteExternalAction],
            ProcessOperationContractNames.ExternalActionControlled,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            BranchGate: null,
            DateTimeOffset.UtcNow);
    }

    private static ProcessRuntimeStepAssignment CreateChildAssignment(
        ProcessRunId childRunId,
        ProcessRunId parentRunId,
        ProcessStepInstanceId parentStepId,
        string parentStepKey)
    {
        return new ProcessRuntimeStepAssignment(
            childRunId,
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "classify-dotnet-application",
            "architecture-designer",
            "solution-architect",
            "Architecture designer",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            ".NET Solution Architect",
            "Classify the child run.",
            "sha256:readiness",
            "Resolved from role fit.",
            [],
            [],
            [ProcessOperationContractNames.ReadProjectStructure],
            ProcessOperationContractNames.ExternalProductTargetReadOnly,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ParentProcessRunId"] = parentRunId.ToString(),
                ["ParentProcessStepId"] = parentStepId.ToString(),
                ["ParentProcessStepKey"] = parentStepKey
            },
            BranchGate: null,
            DateTimeOffset.UtcNow);
    }

    private static ProcessRuntimeStateSnapshot NewRuntimeState(
        ProcessRunId rootRunId,
        ProcessRunId runId,
        ProcessRuntimeStatus status)
    {
        return new ProcessRuntimeStateSnapshot(
            rootRunId,
            runId,
            ProcessInstancePlanId.New(),
            "sha256:plan",
            status,
            [],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            DateTimeOffset.UtcNow);
    }

    private static string CreateTempProductRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"CanDoItAll.ProductMutation.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static AgentDefinition NewAgent(
        string name,
        string roleTitle,
        AgentWorkloadKind workload,
        IReadOnlyList<string> tags,
        AgentWorkspaceToolProfileKind toolProfile,
        IReadOnlyList<AgentCapabilityAssignment>? capabilities = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            name,
            roleTitle,
            $"{name} test agent.",
            "Test instructions.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: "test-model",
            workload,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            AgentWorkspaceToolAccessMetadata.Write(
                "{}",
                new AgentWorkspaceToolAccessSettings
                {
                    Profile = toolProfile
                }),
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            capabilities ?? [],
            tags,
            now,
            now);
    }

    private static IAgentReferenceDataProvider CreateReferenceDataProvider(IAgentFrameworkWorkspaceService workspaceService)
    {
        return new WorkspaceBackedAgentReferenceDataProvider(workspaceService, new AgentReferenceDataCache());
    }

    private sealed class FakeWorkspaceFactory(IAgentFrameworkWorkspaceService workspaceService) : ICanDoItAllAgentWorkspaceFactory
    {
        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService()
            => workspaceService;

        public IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor scope)
            => workspaceService;

        public WorkspaceScopeDescriptor GetOrganizationScope()
            => WorkspaceScopeDescriptor.Organization("unit-test");

        public string GetWorkspaceRoot()
            => Path.GetTempPath();
    }

    private sealed class ThrowingWorkspaceService(
        AgentDefinition agent,
        Exception? executeException,
        Action? beforeThrow = null,
        ExecutionRunResult? executeResult = null,
        ExecutionRunDetail? executionDetail = null,
        IReadOnlyList<ExecutionRunDetail>? executionDetails = null) : IAgentFrameworkWorkspaceService
    {
        private readonly IReadOnlyDictionary<Guid, ExecutionRunDetail> executionDetailById =
            (executionDetails ?? (executionDetail is null ? [] : [executionDetail]))
            .ToDictionary(detail => detail.Run.Id);

        public event EventHandler<ExecutionLogEntry>? ExecutionUpdated
        {
            add { }
            remove { }
        }

        public bool ExecuteRunCalled { get; private set; }

        public Task<IReadOnlyList<AgentDefinition>> ListAgentsAsync(
            bool includeTemplates = true,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AgentDefinition>>([agent]);

        public Task<ExecutionRunResult> ExecuteRunAsync(
            ExecutionRunRequest request,
            CancellationToken cancellationToken = default)
        {
            ExecuteRunCalled = true;
            beforeThrow?.Invoke();
            if (executeException is not null)
            {
                throw executeException;
            }

            return executeResult is not null
                ? Task.FromResult(executeResult)
                : throw Unused();
        }

        public Task<SandboxDashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentOverviewSnapshot> GetAgentOverviewAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentUsageDetailSnapshot> GetAgentUsageDetailsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderUsageDetailSnapshot> GetProviderUsageDetailsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<ModelUsageDetailSnapshot> GetModelUsageDetailsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentEditorModel> GetAgentEditorAsync(Guid? agentId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveAgentAsync(AgentEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteAgentAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentTeamDefinition>> ListAgentTeamsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentTeamEditorModel> GetAgentTeamEditorAsync(Guid? teamId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveAgentTeamAsync(AgentTeamEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentTeamDefinition> UpdateAgentTeamMembersAsync(
            Guid teamId,
            IReadOnlyList<Guid> agentIds,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteAgentTeamAsync(Guid teamId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> CloneAgentAsync(Guid agentId, string cloneName, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> ConvertToTemplateAsync(Guid agentId, string templateKey, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentExportResult> ExportAgentAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> ImportAgentAsync(string packagePath, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(Guid? providerId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderHealthResult> TestProviderAsync(Guid providerId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            Guid providerId,
            ProviderTestChatRequest request,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
            Guid providerId,
            ProviderModelMaintenanceEditorRequest request,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<CapabilityCatalogItem>> ListCapabilitiesAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<CapabilityEditorModel> GetCapabilityEditorAsync(Guid? capabilityId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveCapabilityAsync(CapabilityEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteCapabilityAsync(Guid capabilityId, CancellationToken cancellationToken = default) => throw Unused();

        public Task VerifyCapabilityAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ChatSessionRecord>> ListChatSessionsAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatPageBootstrapSnapshot> GetChatPageBootstrapAsync(bool includeTemplates = false, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatAgentWorkspaceSnapshot> GetChatAgentWorkspaceAsync(
            Guid agentId,
            Guid? preferredSessionId = null,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatSessionRecord> GetOrCreateChatSessionAsync(
            Guid agentId,
            Guid? chatSessionId = null,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatSessionRecord> RenameChatSessionAsync(
            Guid agentId,
            Guid chatSessionId,
            string title,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunResult> ContinueExecutionRunAsync(
            Guid executionRunId,
            bool approved,
            bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentChatRunResult> SendMessageAsync(
            Guid agentId,
            Guid? chatSessionId,
            string prompt,
            CancellationToken cancellationToken = default,
            IReadOnlyList<string>? attachmentPaths = null,
            AgentChatRunOptions? options = null)
            => throw Unused();

        public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
            Guid agentId,
            Guid chatSessionId,
            bool approved,
            bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionLogEntry>> ListExecutionLogAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatRuntimeSnapshot> GetChatRuntimeSnapshotAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentRunMetric>> ListMetricsAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentMemoryRecord>> ListMemoryAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveMemoryAsync(MemoryEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteMemoryAsync(Guid memoryId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
            ExecutionRunQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ExecutionRunRecord>>(
                executionDetailById.Values
                    .Select(detail => detail.Run)
                    .ToArray());

        public Task<ExecutionRunDetail> GetExecutionRunDetailAsync(Guid executionRunId, CancellationToken cancellationToken = default)
            => executionDetailById.TryGetValue(executionRunId, out var detail)
                ? Task.FromResult(detail)
                : throw Unused();

        public Task<IReadOnlyList<ExecutionArtifactRecord>> ListExecutionArtifactsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionWorkflowCheckpointRecord>> ListExecutionWorkflowCheckpointsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ToolExecutionReceiptRecord>> ListToolExecutionReceiptsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        private static InvalidOperationException Unused()
            => new("This fake workspace method is not used by the adapter test.");
    }

    private sealed class FakeSubprocessLaunchCoordinator(
        ProcessSubprocessLaunchCoordinatorResult result) : IProcessSubprocessLaunchCoordinator
    {
        public bool Called { get; private set; }

        public ProcessSubprocessLaunchCoordinatorRequest? LastRequest { get; private set; }

        public ValueTask<ProcessSubprocessLaunchCoordinatorResult?> TryLaunchAsync(
            ProcessSubprocessLaunchCoordinatorRequest request,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            LastRequest = request;
            return ValueTask.FromResult<ProcessSubprocessLaunchCoordinatorResult?>(result);
        }
    }

    private sealed class InMemoryRuntimeStateStore(params ProcessRuntimeStateSnapshot[] states) : IProcessRuntimeStateStore
    {
        private readonly IReadOnlyDictionary<ProcessRunId, ProcessRuntimeStateSnapshot> stateByRunId =
            states.ToDictionary(state => state.RunId);

        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
        {
            stateByRunId.TryGetValue(runId, out var state);
            return Task.FromResult(state);
        }
    }

    private sealed class InMemoryAssignmentStore(params ProcessRuntimeStepAssignment[] initialAssignments) : IProcessRuntimeStepAssignmentStore
    {
        private readonly List<ProcessRuntimeStepAssignment> assignments = [.. initialAssignments];

        public void Add(ProcessRuntimeStepAssignment assignment)
        {
            assignments.Add(assignment);
        }

        public ValueTask SaveAsync(
            IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> LoadByRunAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<ProcessRuntimeStepAssignment>>(
                assignments.Where(assignment => assignment.RunId == runId).ToArray());

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> FindByLaunchVariablesAsync(
            IReadOnlyDictionary<string, string> requiredVariables,
            CancellationToken cancellationToken = default)
        {
            var matches = assignments
                .Where(assignment => requiredVariables.All(required =>
                    assignment.LaunchVariables.TryGetValue(required.Key, out var value) &&
                    string.Equals(value, required.Value, StringComparison.Ordinal)))
                .ToArray();

            return ValueTask.FromResult<IReadOnlyList<ProcessRuntimeStepAssignment>>(matches);
        }

        public ValueTask<ProcessRuntimeStepAssignment?> LoadAsync(
            ProcessRunId runId,
            ProcessStepInstanceId stepInstanceId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(assignments.FirstOrDefault(assignment =>
                assignment.RunId == runId &&
                assignment.StepInstanceId == stepInstanceId));
    }
}
