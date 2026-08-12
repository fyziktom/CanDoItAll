using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

[Trait("Category", "UnixRuntimePortability")]
public sealed class ProcessRuntimeIntegrationAdapterTests
{
    public enum RuntimeOwnedReadOnlyScopeViolation
    {
        MissingMutationAuthority,
        NonMutableTarget,
        MissingManagedArtifactWriteAuthority
    }

    private enum ManagedArtifactReplayMode
    {
        Sequential,
        Concurrent
    }

    private const string ForwardedRuntimeProjectPath =
        @"C:\programovani\dotnet\output\src\TetrisGame.Client\TetrisGame.Client.csproj";

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
    public void Child_specific_completion_issues_preserve_typed_child_run_identity()
    {
        var assignment = CreateSubprocessAssignment();
        var childRunId = ProcessRunId.New();
        var issues = new[]
        {
            ProcessSubprocessCompletionPolicy.CreateSubprocessLaunchCoordinatorMissingOutcomeIssue(
                assignment,
                new ProcessSubprocessLaunchCoordinatorResult(
                    "child-process",
                    childRunId,
                    ProcessLaunchStage.Running.ToString(),
                    string.Empty,
                    [],
                    [])),
            ProcessSubprocessCompletionPolicy.CreateSubprocessChildNoGoIssue(
                assignment,
                childRunId,
                ["artifacts/child-no-go.md"]),
            ProcessSubprocessCompletionPolicy.CreateSubprocessChildAcceptedOutputMissingIssue(
                assignment,
                childRunId,
                new ProcessSubprocessContract()),
            ProcessSubprocessCompletionPolicy.CreateSubprocessChildForwardedContextIssue(
                assignment,
                childRunId,
                new ParentSubprocessForwardedContextIssue(
                    "process.adapter.forwarded_context_unavailable",
                    "Forwarded child context is unavailable.",
                    "sha256:forwarded-context-unavailable"))
        };

        Assert.All(issues, issue => Assert.Equal(childRunId, issue.RelatedChildRunId));
    }

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
    public void Blocked_step_without_specific_classifier_preserves_agent_reason_as_diagnostic()
    {
        var assignment = CreateManagedArtifactAssignment("implementation-approach") with
        {
            ProducedArtifactSlotIds = []
        };
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Blocked,
                Reason = "workspace_analyze_image failed because managed-files/project-media/images/project-structure-ui/generated-image.png was not found.",
                EvidenceRefs = [],
                NextActions =
                [
                    "Retry with exact media=managed-files/project-media/images/be2ebfd7776643f99b2e8051d0b0d99d/generated-image.png from ProjectStructureContextSummary."
                ],
                HumanReadableSummaryMarkdown = "Blocked before visual target analysis could be completed."
            });

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("process.adapter.agent_blocked", diagnostic.Code.Value);
        Assert.Equal(ProcessDiagnosticRetrySafety.Unknown, diagnostic.RetrySafety);
        Assert.Contains("implementation-approach", diagnostic.SafeSummary, StringComparison.Ordinal);
        Assert.Contains("workspace_analyze_image failed", diagnostic.SafeSummary, StringComparison.Ordinal);
        Assert.Contains("[non-citable source path removed]", diagnostic.SafeSummary, StringComparison.Ordinal);
        Assert.Contains("Retry with exact media=", diagnostic.SafeSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("managed-files", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
        var signal = Assert.Single(result.ManagerSignals);
        Assert.Equal("process.adapter.agent_blocked", signal.Code.Value);
        Assert.Equal("Runtime removed non-citable source metadata from the structured outcome; no citable reason text remained.", result.UserSafeSummary);
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
    public void Subprocess_step_completion_with_grounded_child_evidence_can_succeed_without_current_launch_receipt()
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
            [
                CreateToolReceipt(
                    "workspace_read_file",
                    childEvidenceRef,
                    $"Succeeded: Read {childEvidenceRef}."),
                CreateToolReceipt(
                    "workspace_write_file",
                    BuildStepArtifactRef(assignment),
                    "Succeeded: Created file.")
            ]);

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
    public void Structured_outcome_narrative_omits_secret_and_configured_native_root_from_receipts()
    {
        const string secretValue = "raw-structured-outcome-token";
        var productRoot = CreateTempProductRoot();
        try
        {
            var baseAssignment = CreateSubprocessAssignment();
            var assignment = baseAssignment with
            {
                LaunchVariables = WithLaunchVariables(
                    baseAssignment,
                    (ProcessRuntimeLaunchVariables.ProductRoot, productRoot))
            };
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Blocked,
                    Reason = $"The configured target {productRoot} is blocked; password={secretValue}",
                    EvidenceRefs = [],
                    NextActions = [$"Retry {productRoot} after token={secretValue} is replaced."]
                });

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains("[configured product root]", result.UserSafeSummary, StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", result.UserSafeSummary, StringComparison.Ordinal);
            AssertPublicAndPersistedReceiptExclude(
                assignment,
                result,
                productRoot,
                secretValue);
        }
        finally
        {
            DeleteDirectory(productRoot);
        }
    }

    [Fact]
    public void Completion_issue_factory_sanitizes_owner_failure_detail_before_returning_public_result()
    {
        const string configuredRoot = @"C:\Private Product\Output";
        const string secretValue = "raw-owner-failure-token";
        var assignment = CreateManagedArtifactAssignment("code-change") with
        {
            LaunchVariables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ProcessRuntimeLaunchVariables.ProductRoot] = configuredRoot
            }
        };
        var issue = new ProcessCompletionIssue(
            "process.adapter.owner_failure",
            $"Owner failed at '{configuredRoot}\artifact.txt'; password={secretValue}",
            $"restricted:{configuredRoot}:{secretValue}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);

        var result = ProcessCompletionIssueResultFactory.NeedsManagerForCompletionIssue(
            assignment,
            "sha256:raw-output",
            issue);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains("[configured product root]", result.UserSafeSummary, StringComparison.Ordinal);
        Assert.DoesNotContain(configuredRoot, JsonSerializer.Serialize(result), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secretValue, JsonSerializer.Serialize(result), StringComparison.Ordinal);
        Assert.True(ProcessPublicReceiptTextPolicy.IsSafe(
            result.UserSafeSummary,
            ProcessStrategyResultLimits.MaximumUserSafeSummaryLength));
        Assert.All(result.Diagnostics, diagnostic => Assert.True(ProcessPublicReceiptTextPolicy.IsSafe(
            diagnostic.SafeSummary,
            ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength)));
        Assert.All(result.ManagerSignals, signal => Assert.True(ProcessPublicReceiptTextPolicy.IsSafe(
            signal.SafeSummary,
            ProcessStrategyResultLimits.MaximumManagerSignalSummaryLength)));
    }

    [Fact]
    public void Routed_completion_issue_sanitizes_owner_and_route_text_before_returning_public_result()
    {
        const string configuredRoot = @"C:\Private Product\Output";
        const string foreignRoot = "/home/private/workspace";
        const string secretValue = "raw-routed-owner-token";
        const string issueCode = "process.adapter.synthetic_routed_issue";
        var assignment = CreateManagedArtifactAssignment("qa-validation") with
        {
            LaunchVariables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ProcessRuntimeLaunchVariables.ProductRoot] = configuredRoot,
                [ProcessRuntimeLaunchVariables.CompletionIssueRoutes] = JsonSerializer.Serialize<object[]>(
                [
                    new
                    {
                        issueCode,
                        sourceBranchOutcomeKeys = new[] { "quality-accepted" },
                        targetBranchOutcomeKey = "repair-required",
                        targetBranchOutcomeTitle = $"Repair {foreignRoot}; token={secretValue}",
                        requiresDefectEvidence = false
                    }
                ])
            }
        };
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Quality validation completed.",
            BranchOutcomeKey = "quality-accepted",
            BranchOutcomeTitle = "Quality accepted",
            EvidenceRefs = [],
            NextActions = []
        };
        var issue = new ProcessCompletionIssue(
            issueCode,
            $"Owner failed at '{configuredRoot}\artifact.txt'; password={secretValue}",
            $"restricted:{configuredRoot}:{secretValue}",
            [],
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
        var factory = new ProcessCompletionIssueResultFactory(
            TestWorkspaceServices.CreateFileService(Path.GetTempPath()),
            ProcessCompletionDefectEvidenceCatalog.Empty);

        var routed = factory.TryCreateRoutedCompletionIssueResult(
            assignment,
            output,
            "sha256:raw-output",
            new ProcessCompletionGateEvaluation([issue], [issue]),
            toolReceipts: null,
            currentExecutionRunId: null,
            producedArtifactContentHashes: null,
            out var result);

        Assert.True(routed);
        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        var serialized = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(configuredRoot, serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(foreignRoot, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(secretValue, serialized, StringComparison.Ordinal);
        Assert.All(result.Diagnostics, diagnostic => Assert.True(ProcessPublicReceiptTextPolicy.IsSafe(
            diagnostic.SafeSummary,
            ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength)));
        Assert.All(result.ManagerSignals, signal => Assert.True(ProcessPublicReceiptTextPolicy.IsSafe(
            signal.SafeSummary,
            ProcessStrategyResultLimits.MaximumManagerSignalSummaryLength)));
        Assert.True(ProcessPublicReceiptTextPolicy.IsSafe(
            result.UserSafeSummary,
            ProcessStrategyResultLimits.MaximumUserSafeSummaryLength));
    }

    [Fact]
    public void Routed_completion_issue_sanitizes_runtime_gate_findings_before_appending_managed_artifact()
    {
        const string configuredRoot = @"C:\Private Product\Output";
        const string foreignRoot = "/home/private/workspace";
        const string secretValue = "raw-routed-artifact-token";
        const string issueCode = "process.adapter.synthetic_routed_issue";
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var assignment = CreateManagedArtifactAssignment("qa-validation") with
            {
                LaunchVariables = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRoot] = configuredRoot,
                    [ProcessRuntimeLaunchVariables.CompletionIssueRoutes] = JsonSerializer.Serialize<object[]>(
                    [
                        new
                        {
                            issueCode,
                            sourceBranchOutcomeKeys = new[] { "quality-accepted" },
                            targetBranchOutcomeKey = "repair-required",
                            targetBranchOutcomeTitle = $"Repair {foreignRoot}; token={secretValue}",
                            requiresDefectEvidence = false
                        }
                    ])
                }
            };
            var primaryRef = BuildStepArtifactRef(assignment);
            var writeResult = workspaceFiles.WriteTextFile(primaryRef, "# Existing artifact", overwrite: true);
            var output = new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Quality validation completed.",
                BranchOutcomeKey = "quality-accepted",
                BranchOutcomeTitle = "Quality accepted",
                EvidenceRefs = [primaryRef],
                NextActions = []
            };
            var issue = new ProcessCompletionIssue(
                issueCode,
                $"Owner failed at '{configuredRoot}\artifact.txt'; password={secretValue}",
                $"restricted:{configuredRoot}:{secretValue}",
                [],
                ProcessDiagnosticRetrySafety.SafeToRetry,
                ProcessDiagnosticIdempotencyClassification.Idempotent);
            var factory = new ProcessCompletionIssueResultFactory(
                workspaceFiles,
                ProcessCompletionDefectEvidenceCatalog.Empty);

            var appendIssue = factory.AppendRuntimeGateFindingsForRoutedCompletionIssue(
                assignment,
                output,
                Guid.NewGuid(),
                new ProcessCompletionGateEvaluation([issue], [issue]),
                toolReceipts: null);
            var readResult = workspaceFiles.ReadTextFile(
                primaryRef,
                WorkspaceFileLimits.MaxTextReadCharacters);

            Assert.True(writeResult.Succeeded, writeResult.Message);
            Assert.Null(appendIssue);
            Assert.True(readResult.Succeeded, readResult.Message);
            Assert.Contains("## Runtime gate findings", readResult.Content, StringComparison.Ordinal);
            Assert.DoesNotContain(configuredRoot, readResult.Content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(foreignRoot, readResult.Content, StringComparison.Ordinal);
            Assert.DoesNotContain(secretValue, readResult.Content, StringComparison.Ordinal);
            Assert.Contains("[configured product root]", readResult.Content, StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", readResult.Content, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
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
            Assert.Contains("configured product output root", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(outputRoot, result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
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

            Assert.True(result.Outcome == StrategyOutcome.Succeeded, DescribeResult(result));
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
            Assert.Contains("1 required product output path", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(requiredProjectPath, result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
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
            Assert.Contains("1 required product output path", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(requiredProjectPath, result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
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
                    (ProcessRuntimeLaunchVariables.ProcessStepScriptHelperDescriptorJson,
                        ProcessRuntimeLaunchVariables.SerializeProcessStepScriptHelperDescriptor(
                            new ProcessRuntimeScriptHelperDescriptor(
                                "RequiredProductMutationHelper",
                                "RequiredProductMutationHelperRef",
                                "RequiredProductMutationSideEffectManifest"))),
                    ("RequiredProductMutationHelper", "$ErrorActionPreference = 'Stop'"),
                    ("RequiredProductMutationHelperRef", "artifacts/process-runs/{CurrentProcessRunId}/scripts/add-test-project.ps1"),
                    ("RequiredProductMutationSideEffectManifest", """{"version":1,"mode":"ProductMutation"}"""))
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
            Assert.Contains("RequiredProductMutationHelper", diagnostic.SafeSummary, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "artifacts/process-runs/{CurrentProcessRunId}/scripts/add-test-project.ps1",
                diagnostic.SafeSummary,
                StringComparison.Ordinal);
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
                    (
                        ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts,
                        JsonSerializer.Serialize(new[]
                        {
                            new
                            {
                                toolName = "workspace_pwsh_run_script",
                                allowFailedExecutionReceipt = true
                            }
                        })))
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
    public void Missing_required_receipt_identity_ignores_incidental_receipt_churn()
    {
        var assignment = CreateManagedArtifactAssignment(
            "targeted-validation",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ]) with
        {
            LaunchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] =
                    "workspace_dotnet_restore;workspace_dotnet_build;workspace_dotnet_test"
            }
        };
        var primaryRef = BuildStepArtifactRef(assignment);
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Validation was submitted before all required tools ran.",
            BranchOutcomeKey = "feature-repair-required",
            EvidenceRefs = [primaryRef],
            NextActions = []
        };

        var first = ToAdapterResult(
            assignment,
            output,
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")]);
        var second = ToAdapterResult(
            assignment,
            output,
            [
                CreateToolReceipt("workspace_read_file", primaryRef, "Succeeded: Read file."),
                CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Updated file.")
            ]);

        var firstDiagnostic = Assert.Single(first.Diagnostics);
        var secondDiagnostic = Assert.Single(second.Diagnostics);
        Assert.Equal("process.adapter.product_required_tool_receipt_missing", firstDiagnostic.Code.Value);
        Assert.Equal(firstDiagnostic.EvidenceHash, secondDiagnostic.EvidenceHash);
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
    public void Validation_completion_accepts_failed_required_product_tool_receipts_when_the_rule_explicitly_allows_it()
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
                    JsonSerializer.Serialize(new[]
                    {
                        new
                        {
                            toolName = "workspace_dotnet_restore",
                            allowFailedExecutionReceipt = true
                        },
                        new
                        {
                            toolName = "workspace_dotnet_build",
                            allowFailedExecutionReceipt = true
                        },
                        new
                        {
                            toolName = "workspace_dotnet_test",
                            allowFailedExecutionReceipt = true
                        }
                    })
            }
        };
        var primaryRef = BuildStepArtifactRef(assignment);
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Focused validation found a compile blocker.",
                BranchOutcomeKey = "feature-repair-required",
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

    [Theory]
    [InlineData("setup-validated", false)]
    [InlineData("setup-repair-required", true)]
    [InlineData("setup-repair-escalation", true)]
    public void Validation_completion_scopes_failed_receipt_evidence_to_the_selected_branch(
        string branchOutcomeKey,
        bool expectedSuccess)
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
            - setup-validated: Setup validated - Validation passed.
            - setup-repair-required: Repair required - Validation failed and a bounded repair is available.
            - setup-repair-escalation: Repair escalation - Validation still fails after repair.
            """,
            LaunchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] =
                    JsonSerializer.Serialize(new[]
                    {
                        new
                        {
                            key = "build-success",
                            toolName = "workspace_dotnet_build",
                            applicableBranchOutcomeKeys = new[] { "setup-validated" },
                            allowFailedExecutionReceipt = false
                        },
                        new
                        {
                            key = "build-attempt",
                            toolName = "workspace_dotnet_build",
                            applicableBranchOutcomeKeys = new[]
                            {
                                "setup-repair-required",
                                "setup-repair-escalation"
                            },
                            allowFailedExecutionReceipt = true
                        }
                    })
            },
            CapabilityScope = new ProcessCapabilityScope
            {
                RequiredReceipts =
                [
                    new ProcessRequiredToolReceipt
                    {
                        Key = "build",
                        Kind = ProcessRequiredToolReceiptKind.RuntimeToolName,
                        ToolName = "workspace_dotnet_build",
                        RequireSuccessfulExit = true,
                        RequireCurrentRun = false
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
                Reason = "The current build attempt failed.",
                BranchOutcomeKey = branchOutcomeKey,
                EvidenceRefs = [primaryRef],
                NextActions = ["Route using the selected validation branch."],
                HumanReadableSummaryMarkdown =
                    $"Status: Completed\n\nBranch outcome key: {branchOutcomeKey}"
            },
            [
                CreateToolReceipt(
                    "workspace_dotnet_build",
                    "build Calculator.slnx",
                    "Failed (exit 1)"),
                CreateToolReceipt(
                    "workspace_write_file",
                    primaryRef,
                    "Succeeded: Created file.")
            ]);

        if (expectedSuccess)
        {
            Assert.True(result.Outcome == StrategyOutcome.Succeeded, DescribeResult(result));
            Assert.Empty(result.Diagnostics);
            Assert.Contains(result.ManagerSignals, signal =>
                signal.Code.Value == ProcessBranchSignalCodes.Outcome(branchOutcomeKey).Value);
            return;
        }

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.product_required_tool_receipt_missing");
    }

    [Fact]
    public void Validation_completion_rejects_failed_required_product_tool_receipts_without_explicit_policy()
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

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.product_required_tool_receipt_missing");
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
        var diagnostic = result.Diagnostics.Single(
            item => item.Code.Value == "process.adapter.product_required_tool_receipt_missing");
        Assert.Equal("process.adapter.product_required_tool_receipt_missing", diagnostic.Code.Value);
        Assert.Contains("workspace_dotnet_restore", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_dotnet_build", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_dotnet_test", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            result.Diagnostics,
            item => item.Code.Value == "process.adapter.completed_outcome_declares_unresolved_blocker");
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.product_required_tool_receipt_missing");
        Assert.Empty(result.ProducedArtifacts);
    }

    [Fact]
    public void Validation_completion_rejects_recovered_artifact_that_defers_current_run_receipts()
    {
        var assignment = CreateManagedArtifactAssignment(
            "add-tests-and-proof",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ]) with
        {
            Prompt = """
            Available branch outcomes:
            - slice-accepted: Slice accepted - Validation passed.
            - slice-repair-required: Slice repair required - Validation failed but repair is possible.
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
                Branch outcome key: slice-accepted

                ## Reason

                No current-run validation receipts exist yet in this managed artifact; they will be added from the validation tools in this step before finalization.

                ## Validation plan

                1. Restore the solution from the grounded product root.
                2. Build the solution without restore.
                3. Run the xUnit test project without rebuild/restore after successful build.
                """
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file containing a draft validation plan.")]);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("process.adapter.completed_outcome_declares_unresolved_blocker", diagnostic.Code.Value);
        Assert.Contains("current-run validation receipts", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.completed_outcome_declares_unresolved_blocker");
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
    public async Task ExecuteAsync_does_not_promote_existing_completed_primary_artifact_over_current_blocked_result()
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

                var adapter = CreateAdapter(
                    new FakeWorkspaceFactory(workspace),
                    CreateReferenceDataProvider(workspace),
                    new InMemoryAssignmentStore(assignment),
                    new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                    workspaceFiles);

                var result = await adapter.ExecuteAsync(
                    CreateAdapterRequest(
                        assignment,
                        ProcessExecutionAdapterKind.Workflow,
                        new ProcessExecutionAdapterOperationKey("execute"),
                        Binding,
                        [],
                        []));

                Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
                Assert.Empty(result.ProducedArtifacts);
                var content = await File.ReadAllTextAsync(artifactPath);
                Assert.Contains("# Solution skeleton change set", content, StringComparison.Ordinal);
                Assert.DoesNotContain("Runtime Accepted Completion Gates", content, StringComparison.Ordinal);
                Assert.DoesNotContain("staged the completed primary managed artifact", content, StringComparison.OrdinalIgnoreCase);
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
                        productAlias) with
                    {
                        DeclaredSideEffectMode = ToolExecutionSideEffectMode.ProductMutation
                    },
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
            const string protectedConfiguredText = "secret: C:\\private\\host\\token=raw-product-token";
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
                            "src/Calculator/Calculator.csproj",
                            protectedConfiguredText
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
                        productAlias) with
                    {
                        DeclaredSideEffectMode = ToolExecutionSideEffectMode.ProductMutation
                    },
                    CreateToolReceipt("workspace_write_file", BuildStepArtifactRef(assignment), "Succeeded: Created file.")
                ]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("process.adapter.product_required_file_content_missing", diagnostic.Code.Value);
            Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
            var publicReceipt = JsonSerializer.Serialize(result);
            Assert.Contains("check[0].path[0]", result.UserSafeSummary, StringComparison.Ordinal);
            Assert.DoesNotContain(outputRoot, publicReceipt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(solutionFile, publicReceipt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(protectedConfiguredText, publicReceipt, StringComparison.Ordinal);
            Assert.DoesNotContain("raw-product-token", publicReceipt, StringComparison.Ordinal);
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_completion_without_evidence_does_not_add_missing_product_output()
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
                    EvidenceRefs = [],
                    NextActions = []
                });

            Assert.Contains(
                result.Diagnostics,
                candidate => candidate.Code.Value == "process.adapter.product_output_evidence_missing");
            Assert.DoesNotContain(
                result.Diagnostics,
                candidate => candidate.Code.Value == "process.adapter.product_output_missing");
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Completion_gate_evaluator_reports_missing_required_script_receipt_and_failed_solution_readback()
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
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts, "workspace_pwsh_run_script"),
                    ("DotNetCreateProjectScript", "$ErrorActionPreference = 'Stop'"),
                    ("DotNetCreateProjectScriptRef", "artifacts/process-runs/{CurrentProcessRunId}/scripts/create-dotnet-project.ps1"),
                    ("DotNetCreateProjectSideEffectManifest", """{"version":1,"mode":"ProductMutation"}"""))
            };
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Created the app project and claimed the solution membership helper ran.",
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

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Equal("process.adapter.product_required_tool_receipt_missing", result.Diagnostics[0].Code.Value);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.product_required_file_content_missing");
            Assert.Equal(2, result.Diagnostics.Count);
            Assert.Contains("workspace_pwsh_run_script", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("check[0].path[0]", result.UserSafeSummary, StringComparison.Ordinal);
            Assert.DoesNotContain(solutionFile, result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Additional completion gate", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
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
            Assert.DoesNotContain(appProjectFile, result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
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
            Assert.Contains("required product output/readback", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(appProjectFile, result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
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
    public void Product_mutation_completion_rejects_read_only_script_receipt_targeting_product()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(outputRoot, "SampleApp.csproj"), "<Project />");
            var assignment = CreateProductMutationAssignment(outputRoot);
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Inspected the current product state.",
                    EvidenceRefs = [BuildStepArtifactRef(assignment)],
                    NextActions = []
                },
                [
                    CreateToolReceipt(
                        "workspace_pwsh_run_script",
                        "scripts/inspect-product.ps1",
                        "Succeeded (exit 0)",
                        productAlias),
                    CreateToolReceipt("workspace_write_file", BuildStepArtifactRef(assignment), "Succeeded: Created file.")
                ]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == ProcessCompletionDiagnosticCodes.ProductMutationReceiptMissing);
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_completion_accepts_script_receipt_with_declared_product_mutation()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(outputRoot, "SampleApp.csproj"), "<Project />");
            var assignment = CreateProductMutationAssignment(outputRoot);
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Applied the requested product update.",
                    EvidenceRefs = [BuildStepArtifactRef(assignment)],
                    NextActions = []
                },
                [
                    CreateToolReceipt(
                        "workspace_pwsh_run_script",
                        "scripts/apply-product-update.ps1",
                        "Succeeded (exit 0)",
                        productAlias) with
                    {
                        DeclaredSideEffectMode = ToolExecutionSideEffectMode.ProductMutation
                    },
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
    public void Branch_gate_does_not_implicitly_turn_product_mutation_step_into_validation_only_completion()
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

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == ProcessCompletionDiagnosticCodes.ProductMutationReceiptMissing);
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Proof_only_repair_branch_accepts_current_run_validation_without_manufactured_mutation()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(outputRoot, "BusinessApp.csproj"), "<Project />");
            var mutationBranchMap = JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["implement-quality-repair"] = ["product-repair-applied"]
            });
            var baseAssignment = CreateProductMutationAssignment(outputRoot);
            var assignment = baseAssignment with
            {
                StepKey = "implement-quality-repair",
                AllowedOperations =
                [
                    ProcessOperationContractNames.MutateProductTarget,
                    ProcessOperationContractNames.RunValidation,
                    ProcessOperationContractNames.WriteManagedProcessArtifacts
                ],
                LaunchVariables = WithLaunchVariables(
                    baseAssignment,
                    (ProcessRuntimeLaunchVariables.ProductMutationRequiredBranchOutcomeKeysByStep, mutationBranchMap))
            };
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Corrected and executed the concrete proof plan without changing a clean product.",
                    BranchOutcomeKey = "proof-only-revalidation-prepared",
                    EvidenceRefs = [BuildStepArtifactRef(assignment)],
                    NextActions = []
                },
                [
                    CreateToolReceipt(
                        "workspace_dotnet_build",
                        $"{productAlias}/BusinessApp.slnx",
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
    public void Product_repair_branch_still_requires_current_run_mutation_when_validation_is_green()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(outputRoot, "BusinessApp.csproj"), "<Project />");
            var baseAssignment = CreateProductMutationAssignment(outputRoot);
            var mutationBranchMap = JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["implement-quality-repair"] = ["product-repair-applied"]
            });
            var assignment = baseAssignment with
            {
                StepKey = "implement-quality-repair",
                AllowedOperations =
                [
                    ProcessOperationContractNames.MutateProductTarget,
                    ProcessOperationContractNames.RunValidation,
                    ProcessOperationContractNames.WriteManagedProcessArtifacts
                ],
                LaunchVariables = WithLaunchVariables(
                    baseAssignment,
                    (ProcessRuntimeLaunchVariables.ProductMutationRequiredBranchOutcomeKeysByStep, mutationBranchMap))
            };
            var productAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot) ?? outputRoot;
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Claimed a product repair after validation without mutating the product.",
                    BranchOutcomeKey = "product-repair-applied",
                    EvidenceRefs = [BuildStepArtifactRef(assignment)],
                    NextActions = []
                },
                [
                    CreateToolReceipt(
                        "workspace_dotnet_build",
                        $"{productAlias}/BusinessApp.slnx",
                        "Succeeded (exit 0)"),
                    CreateToolReceipt("workspace_write_file", BuildStepArtifactRef(assignment), "Succeeded: Created file.")
                ]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.product_mutation_receipt_missing");
            Assert.Empty(result.ProducedArtifacts);
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
    public void Managed_artifact_completion_accepts_configured_completion_issue_route_target_with_blocker_evidence()
    {
        var assignment = CreateManagedArtifactAssignment("code-change");
        var routes = JsonSerializer.Serialize(new Dictionary<string, object[]>(StringComparer.Ordinal)
        {
            ["code-change"] =
            [
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["issueCode"] = ProcessCompletionDiagnosticCodes.ProductMutationReceiptMissing,
                    ["sourceBranchOutcomeKeys"] = Array.Empty<string>(),
                    ["targetBranchOutcomeKey"] = "implementation-attempt-incomplete",
                    ["targetBranchOutcomeTitle"] = "Implementation attempt incomplete",
                    ["requiresDefectEvidence"] = false
                }
            ]
        });
        assignment = assignment with
        {
            LaunchVariables = WithLaunchVariables(
                assignment,
                (ProcessRuntimeLaunchVariables.CompletionIssueRoutesByStep, routes))
        };
        var primaryRef = BuildStepArtifactRef(assignment);
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Implementation evidence recorded. The requested interaction remains incomplete and needs bounded repair.",
                BranchOutcomeKey = "implementation-attempt-incomplete",
                BranchOutcomeTitle = "Implementation attempt incomplete",
                EvidenceRefs = [primaryRef],
                NextActions = ["Route the incomplete implementation evidence to targeted validation and repair."]
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote incomplete implementation evidence.")]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.completed_outcome_declares_unresolved_blocker");
    }

    [Fact]
    public void Managed_artifact_completion_accepts_no_go_escalation_record_that_declares_unresolved_blockers()
    {
        var baseAssignment = CreateManagedArtifactAssignment("repair-escalation");
        var assignment = baseAssignment with
        {
            BranchGate = new ProcessRuntimeBranchGate("qa-recheck", "repair-escalation"),
            LaunchVariables = WithLaunchVariables(
                baseAssignment,
                (ProcessRuntimeLaunchVariables.ProcessStepCompletionDispositionJson,
                    ProcessRuntimeLaunchVariables.SerializeProcessStepCompletionDisposition(
                        new ProcessRuntimeCompletionDisposition(true, []))))
        };
        var primaryRef = BuildStepArtifactRef(assignment);
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "No-go escalation record completed. Unresolved blockers remain after repair.",
                EvidenceRefs = [primaryRef],
                NextActions = ["Start a new bounded repair scope for the listed defects."]
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote no-go escalation record.")]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Empty(result.Diagnostics);
        Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
    }

    [Theory]
    [InlineData(
        "revalidate-bughunt-repair",
        "final-specialist-repair-required",
        "Final specialist repair required")]
    [InlineData(
        "revalidate-final-repair",
        "quality-repair-no-go",
        "Quality repair no-go")]
    public void Managed_artifact_completion_accepts_declared_open_issue_branch_with_failed_proof(
        string stepKey,
        string branchOutcomeKey,
        string branchOutcomeTitle)
    {
        var baseAssignment = CreateManagedArtifactAssignment(stepKey);
        var assignment = baseAssignment with
        {
            LaunchVariables = WithLaunchVariables(
                baseAssignment,
                (ProcessRuntimeLaunchVariables.ProcessStepCompletionDispositionJson,
                    ProcessRuntimeLaunchVariables.SerializeProcessStepCompletionDisposition(
                        new ProcessRuntimeCompletionDisposition(
                            false,
                            [branchOutcomeKey]))))
        };
        var primaryRef = BuildStepArtifactRef(assignment);
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Current-run proof remains failed, so this validation cannot accept the repair.",
                BranchOutcomeKey = branchOutcomeKey,
                BranchOutcomeTitle = branchOutcomeTitle,
                EvidenceRefs = [primaryRef],
                NextActions = ["Route the failed proof through the declared bounded branch."]
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote no-go validation record.")]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.completed_outcome_declares_unresolved_blocker");
    }

    [Fact]
    public void Managed_artifact_completion_accepts_dotnet_repair_routing_branch_with_defect_evidence()
    {
        var assignment = CreateManagedArtifactAssignment("targeted-validation");
        var primaryRef = BuildStepArtifactRef(assignment);
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Focused validation completed, but the production behavior remains incomplete and requires repair.",
                BranchOutcomeKey = "feature-repair-required",
                BranchOutcomeTitle = "Feature repair required",
                EvidenceRefs = [primaryRef],
                NextActions = ["Route the grounded production and test defects to the feature repair step."]
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote focused validation defect record.")]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.completed_outcome_declares_unresolved_blocker");
    }

    [Fact]
    public void Repair_implementation_accepts_proof_preparation_for_downstream_validation()
    {
        var assignment = CreateManagedArtifactAssignment("implement-quality-repair");
        var primaryRef = BuildStepArtifactRef(assignment);
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Prepared corrected browser proof; the original interaction evidence remains pending independent validation.",
                BranchOutcomeKey = "proof-only-revalidation-prepared",
                BranchOutcomeTitle = "Proof-only revalidation prepared",
                EvidenceRefs = [primaryRef],
                NextActions = ["Independent validator must reproduce the interaction proof."]
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote proof preparation record.")]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.completed_outcome_declares_unresolved_blocker");
    }

    [Fact]
    public void Repair_implementation_rejects_product_repair_that_still_declares_blocker()
    {
        var assignment = CreateManagedArtifactAssignment("implement-quality-repair");
        var primaryRef = BuildStepArtifactRef(assignment);
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Product repair applied. Remaining blocker: interaction proof is missing.",
                BranchOutcomeKey = "product-repair-applied",
                BranchOutcomeTitle = "Product repair applied",
                EvidenceRefs = [primaryRef],
                NextActions = []
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote product repair record.")]);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.completed_outcome_declares_unresolved_blocker");
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
    public void Product_mutation_missing_receipt_identity_ignores_incidental_read_receipts()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(outputRoot, "App.csproj"), "<Project />");
            var assignment = CreateProductMutationAssignment(outputRoot);
            var primaryRef = BuildStepArtifactRef(assignment);
            var output = new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Inspected the product but did not change it.",
                EvidenceRefs = [primaryRef],
                NextActions = []
            };

            var first = ToAdapterResult(
                assignment,
                output,
                [
                    CreateToolReceipt("workspace_read_file", "external-target/product/Program.cs", "Succeeded: Read file."),
                    CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")
                ]);
            var second = ToAdapterResult(
                assignment,
                output,
                [
                    CreateToolReceipt("workspace_read_file", "external-target/product/App.csproj", "Succeeded: Read file."),
                    CreateToolReceipt("workspace_list_directory", "external-target/product", "Succeeded: Listed directory."),
                    CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Updated file.")
                ]);

            var firstDiagnostic = Assert.Single(first.Diagnostics);
            var secondDiagnostic = Assert.Single(second.Diagnostics);
            Assert.Equal("process.adapter.product_mutation_receipt_missing", firstDiagnostic.Code.Value);
            Assert.Equal(firstDiagnostic.EvidenceHash, secondDiagnostic.EvidenceHash);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Quality_diagnosis_cannot_complete_from_excluded_program_bootstrap_read()
    {
        var assignment = CreateManagedArtifactAssignment("diagnose-quality-failure");
        assignment = assignment with
        {
            LaunchVariables = WithLaunchVariables(
                assignment,
                (ProcessRuntimeLaunchVariables.ProductRootAlias, "external-target/C/programovani/dotnet/output"),
                (ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredStepKeys,
                    JsonSerializer.Serialize(new[] { "diagnose-quality-failure" })),
                (ProcessRuntimeLaunchVariables.ProductSourceInspectionExcludedPathFragmentsByStep,
                    JsonSerializer.Serialize(new Dictionary<string, string[]>
                    {
                        ["diagnose-quality-failure"] =
                        [
                            "/Layout/",
                            "/wwwroot/",
                            "/Pages/Counter.razor",
                            "/Pages/Weather.razor",
                            "/Program.cs",
                            "/App.razor",
                            "/_Imports.razor",
                            ".csproj"
                        ]
                    })))
        };
        var executionRunId = Guid.NewGuid();
        var primaryRef = BuildStepArtifactRef(assignment);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Diagnosis completed from the application bootstrap.",
                EvidenceRefs =
                [
                    primaryRef,
                    "external-target/C/programovani/dotnet/output/src/App/Program.cs"
                ],
                NextActions = []
            },
            [
                CreateToolReceipt(
                    "workspace_read_file",
                    "external-target/C/programovani/dotnet/output/src/App/Program.cs",
                    "Succeeded: Read file.",
                    executionRunId: executionRunId),
                CreateToolReceipt(
                    "workspace_write_file",
                    primaryRef,
                    "Succeeded: Created file.",
                    executionRunId: executionRunId)
            ],
            executionRunId);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == ProcessCompletionDiagnosticCodes.ProductSourceInspectionEvidenceMissing);
    }

    [Fact]
    public void QualityAccepted_with_full_browser_receipts_and_scaffold_content_routes_repair_branch()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var scaffoldPage = Path.Combine(outputRoot, "src", "App", "Pages", "Counter.razor");
            Directory.CreateDirectory(Path.GetDirectoryName(scaffoldPage)!);
            File.WriteAllText(scaffoldPage, "@page \"/counter\"");
            var assignment = CreateQaValidationAssignmentWithBranchAwareCompletionRules(
                outputRoot,
                scaffoldPage);
            var primaryRef = BuildStepArtifactRef(assignment);
            var executionRunId = Guid.NewGuid();

            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "QA accepted after full runtime and browser proof, but scaffold content remains.",
                    BranchOutcomeKey = "quality-accepted",
                    BranchOutcomeTitle = "Quality accepted",
                    EvidenceRefs = [primaryRef],
                    NextActions = [],
                    HumanReadableSummaryMarkdown = """
                    Incident root run c4888f4f-eabd-469f-80a6-3fccf6018a12.
                    Step instance 1ebeadbe-98c9-4e9d-af3b-1e9f69a75c62.
                    Browser proof ran, but Counter.razor scaffold content remains.
                    """
                },
                CreateFullQaValidationReceipts(primaryRef, executionRunId),
                executionRunId);

            Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
            Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == ProcessBranchSignalCodes.Outcome("repair-required").Value);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.completion_issue_routed");
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.product_required_file_content_missing");
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void QualityAccepted_with_reachability_only_browser_proof_routes_repair_branch()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var page = Path.Combine(outputRoot, "src", "App", "Pages", "Home.razor");
            Directory.CreateDirectory(Path.GetDirectoryName(page)!);
            File.WriteAllText(page, "<h1>Implemented app</h1>");
            var assignment = CreateQaValidationAssignmentWithBranchAwareCompletionRules(outputRoot, page);
            var primaryRef = BuildStepArtifactRef(assignment);
            var executionRunId = Guid.NewGuid();
            var receipts = CreateFullQaValidationReceipts(primaryRef, executionRunId)
                .Where(receipt =>
                    !string.Equals(receipt.ToolName, "workspace_read_file", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(receipt.ToolName, "browser_press_key", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "QA accepted route reachability without product inspection or interaction.",
                    BranchOutcomeKey = "quality-accepted",
                    BranchOutcomeTitle = "Quality accepted",
                    EvidenceRefs = [primaryRef],
                    NextActions = []
                },
                receipts,
                executionRunId);

            Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
            Assert.Contains(result.ManagerSignals, signal =>
                signal.Code.Value == ProcessBranchSignalCodes.Outcome("repair-required").Value);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.completion_issue_routed");
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Routed_completion_issue_uses_current_browser_console_receipt_as_defect_evidence()
    {
        var assignment = CreateManagedArtifactAssignment("qa-validation") with
        {
            LaunchVariables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ProcessRuntimeLaunchVariables.CompletionIssueRoutes] = JsonSerializer.Serialize<object[]>(
                [
                    new
                    {
                        issueCode = "process.adapter.synthetic_console_defect",
                        sourceBranchOutcomeKeys = new[] { "quality-accepted" },
                        targetBranchOutcomeKey = "repair-required",
                        targetBranchOutcomeTitle = "Repair required",
                        requiresDefectEvidence = true
                    }
                ])
            }
        };
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Validation observed 2 console errors on the product route.",
            BranchOutcomeKey = "quality-accepted",
            BranchOutcomeTitle = "Quality accepted",
            EvidenceRefs = [BuildStepArtifactRef(assignment)],
            NextActions = []
        };
        var issue = new ProcessCompletionIssue(
            "process.adapter.synthetic_console_defect",
            "The configured completion rule found a product defect.",
            "synthetic-console-defect",
            [],
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
        var executionRunId = Guid.NewGuid();
        IReadOnlyList<ToolExecutionReceiptRecord> receipts =
        [
            CreateToolReceipt(
                ToolContractCatalog.BrowserConsoleMessages,
                $"artifacts/process-runs/{assignment.RunId.Value:D}/browser/console.log",
                "Succeeded: Collected 2 console errors.",
                executionRunId: executionRunId)
        ];

        var completionIssueResultFactory = new ProcessCompletionIssueResultFactory(
            TestWorkspaceServices.CreateFileService(Path.GetTempPath()),
            new ProcessCompletionDefectEvidenceCatalog(
            [
                new BrowserConsoleDefectEvidenceContribution()
            ]));
        var routed = completionIssueResultFactory.TryCreateRoutedCompletionIssueResult(
            assignment,
            output,
            "sha256:raw-output",
            new ProcessCompletionGateEvaluation([issue], [issue]),
            receipts,
            executionRunId,
            producedArtifactContentHashes: null,
            out var result);

        Assert.True(routed);
        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Contains(result.ManagerSignals, signal =>
            signal.Code.Value == ProcessBranchSignalCodes.Outcome("repair-required").Value);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.branch_route_defect_evidence_missing");
    }

    [Fact]
    public void Blocked_finalizer_with_matching_completion_issue_route_remains_needs_manager()
    {
        const string issueCode = "process.adapter.synthetic_routed_issue";
        var assignment = CreateManagedArtifactAssignment("qa-validation") with
        {
            LaunchVariables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ProcessRuntimeLaunchVariables.CompletionIssueRoutes] = JsonSerializer.Serialize<object[]>(
                [
                    new
                    {
                        issueCode,
                        sourceBranchOutcomeKeys = new[] { "quality-blocked" },
                        targetBranchOutcomeKey = "repair-required",
                        targetBranchOutcomeTitle = "Repair required",
                        requiresDefectEvidence = false
                    }
                ])
            }
        };
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Blocked,
            Reason = "The finalizer reported a genuine blocker.",
            BranchOutcomeKey = "quality-blocked",
            BranchOutcomeTitle = "Quality blocked",
            EvidenceRefs = [],
            NextActions = ["Resolve the blocker before retrying completion."]
        };
        var issue = new ProcessCompletionIssue(
            issueCode,
            "A synthetic completion gate failed.",
            "synthetic-routed-issue",
            [],
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
        var workspaceFiles = TestWorkspaceServices.CreateFileService(Path.GetTempPath());
        var completionIssueResultFactory = new ProcessCompletionIssueResultFactory(
            workspaceFiles,
            ProcessCompletionDefectEvidenceCatalog.Empty);
        var completionGateEvaluator = new ProcessCompletionGateEvaluator([_ => issue]);
        var toolReceiptPolicies = CreateToolReceiptPolicyCatalog();
        var resultConverter = new ProcessExecutionResultConverter(
            completionGateEvaluator,
            toolReceiptPolicies,
            completionIssueResultFactory);
        var completionCoordinator = new ProcessStepCompletionCoordinator(
            completionIssueResultFactory,
            new ProcessManagedArtifactService(workspaceFiles),
            new ProcessOutcomeGroundingValidator(workspaceFiles),
            completionGateEvaluator,
            resultConverter,
            NullLogger<ProcessStepCompletionCoordinator>.Instance);

        var result = completionCoordinator.Complete(
            assignment,
            ProcessManagedArtifactService.ManagedOutcomeArtifactMaterialization.Unchanged(output, []),
            "sha256:raw-output",
            Guid.NewGuid(),
            []);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Empty(result.ProducedArtifacts);
        Assert.DoesNotContain(result.ManagerSignals, signal =>
            signal.Code.Value == ProcessBranchSignalCodes.Outcome("repair-required").Value);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.completion_issue_routed");
    }

    [Theory]
    [InlineData(true, "Failed (exit 1): browser transport timed out.")]
    [InlineData(false, "Succeeded: Collected 2 console errors.")]
    public void Routed_completion_issue_rejects_failed_or_stale_browser_console_evidence(
        bool receiptUsesCurrentExecution,
        string exitSummary)
    {
        var assignment = CreateManagedArtifactAssignment("qa-validation") with
        {
            LaunchVariables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ProcessRuntimeLaunchVariables.CompletionIssueRoutes] = JsonSerializer.Serialize<object[]>(
                [
                    new
                    {
                        issueCode = "process.adapter.synthetic_console_defect",
                        sourceBranchOutcomeKeys = new[] { "quality-accepted" },
                        targetBranchOutcomeKey = "repair-required",
                        targetBranchOutcomeTitle = "Repair required",
                        requiresDefectEvidence = true
                    }
                ])
            }
        };
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Validation observed 2 console errors on the product route.",
            BranchOutcomeKey = "quality-accepted",
            BranchOutcomeTitle = "Quality accepted",
            EvidenceRefs = [BuildStepArtifactRef(assignment)],
            NextActions = []
        };
        var issue = new ProcessCompletionIssue(
            "process.adapter.synthetic_console_defect",
            "The configured completion rule found a product defect.",
            "synthetic-console-defect",
            [],
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
        var currentExecutionRunId = Guid.NewGuid();
        var receiptExecutionRunId = receiptUsesCurrentExecution
            ? currentExecutionRunId
            : Guid.NewGuid();
        IReadOnlyList<ToolExecutionReceiptRecord> receipts =
        [
            CreateToolReceipt(
                ToolContractCatalog.BrowserConsoleMessages,
                $"artifacts/process-runs/{assignment.RunId.Value:D}/browser/console.log",
                exitSummary,
                executionRunId: receiptExecutionRunId)
        ];
        var completionIssueResultFactory = new ProcessCompletionIssueResultFactory(
            TestWorkspaceServices.CreateFileService(Path.GetTempPath()),
            new ProcessCompletionDefectEvidenceCatalog(
            [
                new BrowserConsoleDefectEvidenceContribution()
            ]));

        var routed = completionIssueResultFactory.TryCreateRoutedCompletionIssueResult(
            assignment,
            output,
            "sha256:raw-output",
            new ProcessCompletionGateEvaluation([issue], [issue]),
            receipts,
            currentExecutionRunId,
            producedArtifactContentHashes: null,
            out var result);

        Assert.True(routed);
        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.branch_route_defect_evidence_missing");
        Assert.DoesNotContain(result.ManagerSignals, signal =>
            signal.Code.Value == ProcessBranchSignalCodes.Outcome("repair-required").Value);
    }

    [Fact]
    public void Missing_required_browser_interaction_routes_only_after_automatic_retry()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var page = Path.Combine(outputRoot, "src", "App", "Pages", "Home.razor");
            Directory.CreateDirectory(Path.GetDirectoryName(page)!);
            File.WriteAllText(page, "<h1>Implemented app</h1>");
            var assignment = CreateQaValidationAssignmentWithBranchAwareCompletionRules(outputRoot, page);
            var routes = JsonSerializer.Serialize(new Dictionary<string, object[]>(StringComparer.Ordinal)
            {
                ["qa-validation"] =
                [
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["issueCode"] = ProcessCompletionDiagnosticCodes.ProductRequiredToolReceiptMissing,
                        ["sourceBranchOutcomeKeys"] = new[] { "quality-accepted" },
                        ["targetBranchOutcomeKey"] = "repair-required",
                        ["targetBranchOutcomeTitle"] = "Repair required",
                        ["requiresDefectEvidence"] = false,
                        ["onlyAfterAutomaticRetry"] = true
                    }
                ]
            });
            var requiredReceipts = JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["qa-validation"] = [BrowserInteractionToolReceiptPolicyContribution.InteractionProofRequirement]
            });
            assignment = assignment with
            {
                LaunchVariables = WithLaunchVariables(
                    assignment,
                    (ProcessRuntimeLaunchVariables.CompletionIssueRoutesByStep, routes),
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep, requiredReceipts))
            };
            var primaryRef = BuildStepArtifactRef(assignment);
            var executionRunId = Guid.NewGuid();
            var receipts = CreateFullQaValidationReceipts(primaryRef, executionRunId)
                .Where(receipt => !string.Equals(
                    receipt.ToolName,
                    ToolContractCatalog.BrowserPressKey,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var output = new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "QA accepted without exercising a representative interaction.",
                BranchOutcomeKey = "quality-accepted",
                BranchOutcomeTitle = "Quality accepted",
                EvidenceRefs = [primaryRef],
                NextActions = []
            };

            var first = ToAdapterResult(assignment, output, receipts, executionRunId);
            var retryAssignment = assignment with
            {
                Prompt = $"{assignment.Prompt}\n\n{ProcessRuntimeRecoveryInstructionHeadings.RuntimeDiagnosticRecovery}: retry missing proof."
            };
            var repeated = ToAdapterResult(retryAssignment, output, receipts, executionRunId);

            Assert.Equal(StrategyOutcome.NeedsManager, first.Outcome);
            Assert.Contains(first.Diagnostics, diagnostic =>
                diagnostic.Code.Value == ProcessCompletionDiagnosticCodes.ProductRequiredToolReceiptMissing);
            Assert.True(repeated.Outcome == StrategyOutcome.Succeeded, DescribeResult(repeated));
            Assert.Contains(repeated.ManagerSignals, signal =>
                signal.Code.Value == ProcessBranchSignalCodes.Outcome("repair-required").Value);
            Assert.Contains(repeated.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.completion_issue_routed");
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void QualityAccepted_with_textual_criterion_ids_requires_typed_criterion_evidence()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var scaffoldPage = Path.Combine(outputRoot, "src", "App", "Pages", "Counter.razor");
            var assignment = CreateQaValidationAssignmentWithAcceptanceMatrix(outputRoot, scaffoldPage);
            var primaryRef = BuildStepArtifactRef(assignment);
            var executionRunId = Guid.NewGuid();

            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "QA accepted after runtime and browser proof.",
                    BranchOutcomeKey = "quality-accepted",
                    BranchOutcomeTitle = "Quality accepted",
                    EvidenceRefs = [primaryRef],
                    NextActions = [],
                    HumanReadableSummaryMarkdown = "Browser proof and build/test proof completed for AC-001 and AC-002, but no typed criterion evidence was submitted."
                },
                CreateFullQaValidationReceipts(primaryRef, executionRunId),
                executionRunId);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.acceptance_criteria_missing");
            Assert.DoesNotContain(result.ManagerSignals, signal =>
                signal.Code.Value == ProcessBranchSignalCodes.Outcome("quality-accepted").Value);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void QualityAccepted_with_blank_present_acceptance_matrix_rejects_the_invalid_contract()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var scaffoldPage = Path.Combine(outputRoot, "src", "App", "Pages", "Counter.razor");
            var assignment = CreateQaValidationAssignmentWithAcceptanceMatrix(outputRoot, scaffoldPage);
            assignment = assignment with
            {
                LaunchVariables = WithLaunchVariables(
                    assignment,
                    (ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix, " "))
            };
            var primaryRef = BuildStepArtifactRef(assignment);
            var executionRunId = Guid.NewGuid();

            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "QA selected acceptance with a blank internal acceptance contract.",
                    BranchOutcomeKey = "quality-accepted",
                    BranchOutcomeTitle = "Quality accepted",
                    EvidenceRefs = [primaryRef],
                    NextActions = []
                },
                CreateFullQaValidationReceipts(primaryRef, executionRunId),
                executionRunId);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.acceptance_criteria_contract_invalid");
            Assert.DoesNotContain(result.ManagerSignals, signal =>
                signal.Code.Value == ProcessBranchSignalCodes.Outcome("quality-accepted").Value);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void QualityAccepted_with_full_browser_receipts_accepts_criterion_by_criterion_proof()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var scaffoldPage = Path.Combine(outputRoot, "src", "App", "Pages", "Counter.razor");
            var assignment = CreateQaValidationAssignmentWithAcceptanceMatrix(outputRoot, scaffoldPage);
            var primaryRef = BuildStepArtifactRef(assignment);
            var executionRunId = Guid.NewGuid();

            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "QA accepted after proving AC-001 and AC-002.",
                    BranchOutcomeKey = "quality-accepted",
                    BranchOutcomeTitle = "Quality accepted",
                    EvidenceRefs = [primaryRef],
                    AcceptanceCriteriaEvidence =
                    [
                        new ProcessAcceptanceCriterionEvidence
                        {
                            CriterionId = "AC-001",
                            Status = ProcessAcceptanceCriterionEvidenceStatus.Passed,
                            Summary = "Browser proof shows falling blocks can move and rotate.",
                            EvidenceRefs = [primaryRef]
                        },
                        new ProcessAcceptanceCriterionEvidence
                        {
                            CriterionId = "AC-002",
                            Status = ProcessAcceptanceCriterionEvidenceStatus.Passed,
                            Summary = "Test proof shows completed lines update score.",
                            EvidenceRefs = [primaryRef]
                        }
                    ],
                    NextActions = [],
                    HumanReadableSummaryMarkdown = """
                    AC-001: Browser proof shows falling blocks can move and rotate.
                    AC-002: Test proof shows completed lines update score.
                    """
                },
                CreateFullQaValidationReceipts(primaryRef, executionRunId),
                executionRunId);

            Assert.True(result.Outcome == StrategyOutcome.Succeeded, DescribeResult(result));
            Assert.Empty(result.Diagnostics);
            Assert.Contains(result.ManagerSignals, signal =>
                signal.Code.Value == ProcessBranchSignalCodes.Outcome("quality-accepted").Value);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void QualityAccepted_rejects_stale_runtime_host_receipt()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var scaffoldPage = Path.Combine(outputRoot, "src", "App", "Pages", "Counter.razor");
            var assignment = CreateQaValidationAssignmentWithAcceptanceMatrix(outputRoot, scaffoldPage);
            var primaryRef = BuildStepArtifactRef(assignment);
            var currentExecutionRunId = Guid.NewGuid();
            var previousExecutionRunId = Guid.NewGuid();

            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "QA accepted after proving AC-001 and AC-002.",
                    BranchOutcomeKey = "quality-accepted",
                    BranchOutcomeTitle = "Quality accepted",
                    EvidenceRefs = [primaryRef],
                    NextActions = [],
                    HumanReadableSummaryMarkdown = "AC-001 proved. AC-002 proved."
                },
                CreateFullQaValidationReceipts(primaryRef, currentExecutionRunId, runtimeExecutionRunId: previousExecutionRunId),
                currentExecutionRunId);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.runtime_lifecycle_correlation_missing");
            Assert.DoesNotContain(result.ManagerSignals, signal =>
                signal.Code.Value == ProcessBranchSignalCodes.Outcome("quality-accepted").Value);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void QualityAccepted_rejects_browser_proof_from_different_runtime_host()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var scaffoldPage = Path.Combine(outputRoot, "src", "App", "Pages", "Counter.razor");
            var assignment = CreateQaValidationAssignmentWithAcceptanceMatrix(outputRoot, scaffoldPage);
            var primaryRef = BuildStepArtifactRef(assignment);
            var executionRunId = Guid.NewGuid();

            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "QA accepted after proving AC-001 and AC-002.",
                    BranchOutcomeKey = "quality-accepted",
                    BranchOutcomeTitle = "Quality accepted",
                    EvidenceRefs = [primaryRef],
                    NextActions = [],
                    HumanReadableSummaryMarkdown = "AC-001 proved. AC-002 proved."
                },
                CreateFullQaValidationReceipts(
                    primaryRef,
                    executionRunId,
                    hostUrl: "http://127.0.0.1:5173",
                    browserHostUrl: "http://127.0.0.1:5174"),
                executionRunId);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.runtime_lifecycle_correlation_missing");
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void QualityAccepted_rejects_failed_runtime_stop_receipt()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var scaffoldPage = Path.Combine(outputRoot, "src", "App", "Pages", "Counter.razor");
            var assignment = CreateQaValidationAssignmentWithAcceptanceMatrix(outputRoot, scaffoldPage);
            var primaryRef = BuildStepArtifactRef(assignment);
            var executionRunId = Guid.NewGuid();

            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "QA accepted after proving AC-001 and AC-002.",
                    BranchOutcomeKey = "quality-accepted",
                    BranchOutcomeTitle = "Quality accepted",
                    EvidenceRefs = [primaryRef],
                    NextActions = [],
                    HumanReadableSummaryMarkdown = "AC-001 proved. AC-002 proved."
                },
                CreateFullQaValidationReceipts(
                    primaryRef,
                    executionRunId,
                    stopExitSummary: "Failed (exit 1): still running process ids: 12345"),
                executionRunId);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.runtime_lifecycle_correlation_missing");
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void RepairRequired_with_deterministic_content_defect_does_not_require_acceptance_browser_receipts()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var scaffoldPage = Path.Combine(outputRoot, "src", "App", "Pages", "Counter.razor");
            Directory.CreateDirectory(Path.GetDirectoryName(scaffoldPage)!);
            File.WriteAllText(scaffoldPage, "@page \"/counter\"");
            var assignment = CreateQaValidationAssignmentWithBranchAwareCompletionRules(
                outputRoot,
                scaffoldPage);
            var primaryRef = BuildStepArtifactRef(assignment);

            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "QA found deterministic scaffold defect evidence and routed repair.",
                    BranchOutcomeKey = "repair-required",
                    BranchOutcomeTitle = "Repair required",
                    EvidenceRefs = [primaryRef],
                    NextActions = ["Remove scaffold content and rerun validation."]
                },
                [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote repair evidence.")]);

            Assert.True(result.Outcome == StrategyOutcome.Succeeded, DescribeResult(result));
            Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == ProcessBranchSignalCodes.Outcome("repair-required").Value);
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.product_required_tool_receipt_missing");
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.required_tool_receipt_missing");
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Theory]
    [InlineData("Current-run browser navigation reported 1 console error on the primary route.")]
    [InlineData("Current-run browser proof exposed Blazor error UI with console errors.")]
    public void RepairRequired_with_current_run_browser_console_error_is_accepted_as_defect_evidence(string reason)
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var page = Path.Combine(outputRoot, "src", "App", "Pages", "Home.razor");
            Directory.CreateDirectory(Path.GetDirectoryName(page)!);
            File.WriteAllText(page, "<h1>Implemented app</h1>");
            var assignment = CreateQaValidationAssignmentWithBranchAwareCompletionRules(
                outputRoot,
                page);
            var primaryRef = BuildStepArtifactRef(assignment);
            var consoleRef = $"artifacts/process-runs/{assignment.RunId.Value:D}/browser-console.log";
            var executionRunId = Guid.NewGuid();

            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = reason,
                    BranchOutcomeKey = "repair-required",
                    BranchOutcomeTitle = "Repair required",
                    EvidenceRefs = [primaryRef, consoleRef],
                    NextActions = ["Repair the browser runtime error and rerun QA."]
                },
                [
                    CreateToolReceipt(
                        "workspace_write_file",
                        primaryRef,
                        "Succeeded: Wrote QA evidence.",
                        executionRunId: executionRunId),
                    CreateToolReceipt(
                        "browser_console_messages",
                        $"level=\"error\", filename=\"{consoleRef}\"",
                        "Succeeded",
                        executionRunId: executionRunId)
                ],
                executionRunId);

            Assert.True(result.Outcome == StrategyOutcome.Succeeded, DescribeResult(result));
            Assert.Contains(result.ManagerSignals, signal =>
                signal.Code.Value == ProcessBranchSignalCodes.Outcome("repair-required").Value);
            Assert.DoesNotContain(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.branch_outcome_defect_evidence_missing");
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Theory]
    [InlineData("The #blazor-error-ui remained visible and displayed 'An unhandled error has occurred.' while the console remained clean.")]
    [InlineData("Browser showed #blazor-error-ui; no other runtime error occurred.")]
    public void RepairRequired_with_current_run_browser_state_artifact_accepts_visible_unhandled_error_with_clean_console(
        string reason)
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var page = Path.Combine(outputRoot, "src", "App", "Pages", "Home.razor");
            Directory.CreateDirectory(Path.GetDirectoryName(page)!);
            File.WriteAllText(page, "<h1>Implemented app</h1>");
            var assignment = CreateQaValidationAssignmentWithBranchAwareCompletionRules(
                outputRoot,
                page);
            var primaryRef = BuildStepArtifactRef(assignment);
            var browserStateRef = $"artifacts/process-runs/{assignment.RunId.Value:D}/browser/blazor-error-state.json";
            var screenshotRef = $"artifacts/process-runs/{assignment.RunId.Value:D}/browser/blazor-error.png";
            var executionRunId = Guid.NewGuid();

            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = reason,
                    BranchOutcomeKey = "repair-required",
                    BranchOutcomeTitle = "Repair required",
                    EvidenceRefs = [primaryRef, browserStateRef, screenshotRef],
                    NextActions = ["Repair the visible Blazor runtime failure and rerun QA."]
                },
                [
                    CreateToolReceipt(
                        "workspace_write_file",
                        primaryRef,
                        "Succeeded: Wrote QA evidence.",
                        executionRunId: executionRunId),
                    CreateToolReceipt(
                        ToolContractCatalog.BrowserEvaluate,
                        $"selector=#blazor-error-ui,filename={browserStateRef},timeout=5000",
                        "Succeeded",
                        executionRunId: executionRunId),
                    CreateToolReceipt(
                        ToolContractCatalog.BrowserTakeScreenshot,
                        $"filename={screenshotRef},fullPage=False",
                        "Succeeded",
                        executionRunId: executionRunId),
                    CreateToolReceipt(
                        ToolContractCatalog.BrowserConsoleMessages,
                        "level=\"error\"",
                        "Succeeded: No console errors.",
                        executionRunId: executionRunId)
                ],
                executionRunId);

            Assert.True(result.Outcome == StrategyOutcome.Succeeded, DescribeResult(result));
            Assert.Contains(result.ManagerSignals, signal =>
                signal.Code.Value == ProcessBranchSignalCodes.Outcome("repair-required").Value);
            Assert.DoesNotContain(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.branch_outcome_defect_evidence_missing");
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RepairRequired_rejects_stale_or_unmatched_browser_observed_defect_evidence(
        bool receiptUsesCurrentExecution)
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var page = Path.Combine(outputRoot, "src", "App", "Pages", "Home.razor");
            Directory.CreateDirectory(Path.GetDirectoryName(page)!);
            File.WriteAllText(page, "<h1>Implemented app</h1>");
            var assignment = CreateQaValidationAssignmentWithBranchAwareCompletionRules(
                outputRoot,
                page);
            var primaryRef = BuildStepArtifactRef(assignment);
            var citedBrowserStateRef = $"artifacts/process-runs/{assignment.RunId.Value:D}/browser/blazor-error-state.json";
            var receiptBrowserStateRef = receiptUsesCurrentExecution
                ? $"artifacts/process-runs/{assignment.RunId.Value:D}/browser/unmatched-state.json"
                : citedBrowserStateRef;
            var currentExecutionRunId = Guid.NewGuid();
            var receiptExecutionRunId = receiptUsesCurrentExecution
                ? currentExecutionRunId
                : Guid.NewGuid();

            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Current-run browser evaluation showed visible #blazor-error-ui text 'An unhandled error has occurred.'",
                    BranchOutcomeKey = "repair-required",
                    BranchOutcomeTitle = "Repair required",
                    EvidenceRefs = [primaryRef, citedBrowserStateRef],
                    NextActions = ["Repair the visible Blazor runtime failure and rerun QA."]
                },
                [
                    CreateToolReceipt(
                        "workspace_write_file",
                        primaryRef,
                        "Succeeded: Wrote QA evidence.",
                        executionRunId: currentExecutionRunId),
                    CreateToolReceipt(
                        ToolContractCatalog.BrowserEvaluate,
                        $"selector=\"#blazor-error-ui\", filename=\"{receiptBrowserStateRef}\"",
                        "Succeeded",
                        executionRunId: receiptExecutionRunId)
                ],
                currentExecutionRunId);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.branch_outcome_defect_evidence_missing");
            Assert.DoesNotContain(result.ManagerSignals, signal =>
                signal.Code.Value == ProcessBranchSignalCodes.Outcome("repair-required").Value);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Theory]
    [InlineData("Current page displayed no #blazor-error-ui.")]
    [InlineData("Browser evaluation found no runtime error.")]
    [InlineData("#blazor-error-ui was not visible after the repair.")]
    [InlineData("#blazor-error-ui is no longer visible after the repair.")]
    [InlineData("The application error surface was hidden with display: none.")]
    public void RepairRequired_rejects_negated_browser_defect_claims(
        string reason)
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var page = Path.Combine(outputRoot, "src", "App", "Pages", "Home.razor");
            Directory.CreateDirectory(Path.GetDirectoryName(page)!);
            File.WriteAllText(page, "<h1>Implemented app</h1>");
            var assignment = CreateQaValidationAssignmentWithBranchAwareCompletionRules(
                outputRoot,
                page);
            var primaryRef = BuildStepArtifactRef(assignment);
            var browserStateRef = $"artifacts/process-runs/{assignment.RunId.Value:D}/browser/clean-state.json";
            var executionRunId = Guid.NewGuid();

            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = reason,
                    BranchOutcomeKey = "repair-required",
                    BranchOutcomeTitle = "Repair required",
                    EvidenceRefs = [primaryRef, browserStateRef],
                    NextActions = ["Repair a different product defect."]
                },
                [
                    CreateToolReceipt(
                        "workspace_write_file",
                        primaryRef,
                        "Succeeded: Wrote QA evidence.",
                        executionRunId: executionRunId),
                    CreateToolReceipt(
                        ToolContractCatalog.BrowserEvaluate,
                        $"filename={browserStateRef},timeout=5000",
                        "Succeeded",
                        executionRunId: executionRunId)
                ],
                executionRunId);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.branch_outcome_defect_evidence_missing");
            Assert.DoesNotContain(result.ManagerSignals, signal =>
                signal.Code.Value == ProcessBranchSignalCodes.Outcome("repair-required").Value);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Unbranched_completed_outcome_omits_only_ungrounded_not_verified_criterion_ref()
    {
        var assignment = CreateManagedArtifactAssignment("code-change");
        var primaryRef = BuildStepArtifactRef(assignment);
        var ungroundedRef =
            $"artifacts/process-runs/{Guid.NewGuid():D}/steps/code-change.md";
        var passedEvidence = new ProcessAcceptanceCriterionEvidence
        {
            CriterionId = "AC-002",
            Status = ProcessAcceptanceCriterionEvidenceStatus.Passed,
            Summary = "The authoritative criterion passed.",
            EvidenceRefs = [primaryRef]
        };
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Implementation evidence is ready for downstream validation.",
            EvidenceRefs = [primaryRef],
            AcceptanceCriteriaEvidence =
            [
                new ProcessAcceptanceCriterionEvidence
                {
                    CriterionId = "AC-001",
                    Status = ProcessAcceptanceCriterionEvidenceStatus.NotVerified,
                    Summary = "Downstream validation owns the remaining proof.",
                    EvidenceRefs = [primaryRef, ungroundedRef]
                },
                passedEvidence
            ],
            NextActions = ["Run downstream validation."]
        };
        var validator = new ProcessOutcomeGroundingValidator(
            TestWorkspaceServices.CreateFileService(Path.GetTempPath()));

        var normalized = validator.RemoveUngroundedNonAuthoritativeCriterionEvidenceRefs(
            assignment,
            output,
            [],
            ProcessStepExecutionContract.Empty,
            out var removedCount);

        Assert.Equal(1, removedCount);
        Assert.Equal(output.Status, normalized.Status);
        Assert.Equal(output.Reason, normalized.Reason);
        Assert.Same(output.EvidenceRefs, normalized.EvidenceRefs);
        Assert.Same(output.NextActions, normalized.NextActions);
        var notVerifiedEvidence = Assert.Single(
            normalized.AcceptanceCriteriaEvidence,
            evidence => evidence.CriterionId == "AC-001");
        Assert.Equal(ProcessAcceptanceCriterionEvidenceStatus.NotVerified, notVerifiedEvidence.Status);
        Assert.Equal("Downstream validation owns the remaining proof.", notVerifiedEvidence.Summary);
        Assert.Equal([primaryRef], notVerifiedEvidence.EvidenceRefs);
        Assert.Same(
            passedEvidence,
            Assert.Single(
                normalized.AcceptanceCriteriaEvidence,
                evidence => evidence.CriterionId == "AC-002"));
        Assert.Null(validator.ValidateGroundedOutcomeReferences(
            assignment,
            normalized,
            [],
            ProcessStepExecutionContract.Empty));
    }

    [Theory]
    [InlineData(ProcessAcceptanceCriterionEvidenceStatus.Passed)]
    [InlineData(ProcessAcceptanceCriterionEvidenceStatus.Failed)]
    public void Completed_outcome_never_omits_ungrounded_authoritative_criterion_ref(
        ProcessAcceptanceCriterionEvidenceStatus status)
    {
        var assignment = CreateManagedArtifactAssignment("code-change");
        var primaryRef = BuildStepArtifactRef(assignment);
        var ungroundedRef =
            $"artifacts/process-runs/{Guid.NewGuid():D}/steps/code-change.md";
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "The criterion carries authoritative proof.",
            EvidenceRefs = [primaryRef],
            AcceptanceCriteriaEvidence =
            [
                new ProcessAcceptanceCriterionEvidence
                {
                    CriterionId = "AC-001",
                    Status = status,
                    Summary = "This proof must remain strict.",
                    EvidenceRefs = [ungroundedRef]
                }
            ],
            NextActions = []
        };
        var validator = new ProcessOutcomeGroundingValidator(
            TestWorkspaceServices.CreateFileService(Path.GetTempPath()));

        var normalized = validator.RemoveUngroundedNonAuthoritativeCriterionEvidenceRefs(
            assignment,
            output,
            [],
            ProcessStepExecutionContract.Empty,
            out var removedCount);

        Assert.Equal(0, removedCount);
        Assert.Same(output, normalized);
        Assert.NotNull(validator.ValidateGroundedOutcomeReferences(
            assignment,
            normalized,
            [],
            ProcessStepExecutionContract.Empty));
    }

    [Fact]
    public void Completed_outcome_does_not_omit_not_verified_ref_when_criterion_summary_is_ungrounded()
    {
        var assignment = CreateManagedArtifactAssignment("code-change");
        var primaryRef = BuildStepArtifactRef(assignment);
        var ungroundedRef =
            $"artifacts/process-runs/{Guid.NewGuid():D}/steps/code-change.md";
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Downstream validation owns this criterion.",
            EvidenceRefs = [primaryRef],
            AcceptanceCriteriaEvidence =
            [
                new ProcessAcceptanceCriterionEvidence
                {
                    CriterionId = "AC-001",
                    Status = ProcessAcceptanceCriterionEvidenceStatus.NotVerified,
                    Summary = $"Downstream proof is described by {ungroundedRef}.",
                    EvidenceRefs = [ungroundedRef]
                }
            ],
            NextActions = []
        };
        var validator = new ProcessOutcomeGroundingValidator(
            TestWorkspaceServices.CreateFileService(Path.GetTempPath()));

        var normalized = validator.RemoveUngroundedNonAuthoritativeCriterionEvidenceRefs(
            assignment,
            output,
            [],
            ProcessStepExecutionContract.Empty,
            out var removedCount);

        Assert.Equal(0, removedCount);
        Assert.Same(output, normalized);
    }

    [Theory]
    [InlineData("quality-accepted", "quality-accepted")]
    [InlineData("repair-required ", "repair-required")]
    [InlineData("Repair-required", "repair-required")]
    public void Completed_outcome_does_not_omit_not_verified_ref_for_acceptance_or_inexact_branch(
        string branchOutcomeKey,
        string configuredBranchOutcomeKey)
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var page = Path.Combine(outputRoot, "src", "App", "Pages", "Home.razor");
            var assignment = CreateQaValidationAssignmentWithAcceptanceMatrix(outputRoot, page);
            var primaryRef = BuildStepArtifactRef(assignment);
            var ungroundedRef =
                $"artifacts/process-runs/{Guid.NewGuid():D}/steps/acceptance-proof.md";
            var output = new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "The agent selected a branch that is not sanitizer-eligible.",
                BranchOutcomeKey = branchOutcomeKey,
                BranchOutcomeTitle = "Ineligible branch",
                EvidenceRefs = [primaryRef],
                AcceptanceCriteriaEvidence =
                [
                    new ProcessAcceptanceCriterionEvidence
                    {
                        CriterionId = "AC-OPTIONAL",
                        Status = ProcessAcceptanceCriterionEvidenceStatus.NotVerified,
                        Summary = "Optional downstream proof was not verified.",
                        EvidenceRefs = [ungroundedRef]
                    }
                ],
                NextActions = []
            };
            var validator = new ProcessOutcomeGroundingValidator(
                TestWorkspaceServices.CreateFileService(Path.GetTempPath()));

            var normalized = validator.RemoveUngroundedNonAuthoritativeCriterionEvidenceRefs(
                assignment,
                output,
                [],
                BranchStepContract(configuredBranchOutcomeKey),
                out var removedCount);

            Assert.Equal(0, removedCount);
            Assert.Same(output, normalized);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Repair_branch_not_verified_criterion_cannot_substitute_for_failed_defect_evidence()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var page = Path.Combine(outputRoot, "src", "App", "Pages", "Home.razor");
            var assignment = CreateQaValidationAssignmentWithAcceptanceMatrix(outputRoot, page);
            var primaryRef = BuildStepArtifactRef(assignment);
            var ungroundedRef =
                $"artifacts/process-runs/{Guid.NewGuid():D}/steps/defect-proof.md";
            var output = new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "QA could not verify the required behavior.",
                BranchOutcomeKey = "repair-required",
                BranchOutcomeTitle = "Repair required",
                EvidenceRefs = [primaryRef],
                AcceptanceCriteriaEvidence =
                [
                    new ProcessAcceptanceCriterionEvidence
                    {
                        CriterionId = "AC-001",
                        Status = ProcessAcceptanceCriterionEvidenceStatus.NotVerified,
                        Summary = "The criterion still requires downstream proof.",
                        EvidenceRefs = [primaryRef, ungroundedRef]
                    }
                ],
                NextActions = ["Collect authoritative defect evidence."]
            };
            var validator = new ProcessOutcomeGroundingValidator(
                TestWorkspaceServices.CreateFileService(Path.GetTempPath()));
            var stepContract = BranchStepContract("repair-required");

            var normalized = validator.RemoveUngroundedNonAuthoritativeCriterionEvidenceRefs(
                assignment,
                output,
                [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote QA evidence.")],
                stepContract,
                out var removedCount);
            var result = ToAdapterResult(
                assignment,
                normalized,
                [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote QA evidence.")],
                stepContract: stepContract);

            Assert.Equal(1, removedCount);
            Assert.Equal([primaryRef], Assert.Single(normalized.AcceptanceCriteriaEvidence).EvidenceRefs);
            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.branch_outcome_defect_evidence_missing");
            Assert.DoesNotContain(result.ManagerSignals, signal =>
                signal.Code.Value == ProcessBranchSignalCodes.Outcome("repair-required").Value);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Materialization_excludes_ungrounded_not_verified_ref_from_runtime_appendix()
    {
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var assignment = CreateManagedArtifactAssignment("code-change");
            var primaryRef = BuildStepArtifactRef(assignment);
            var ungroundedRef =
                $"artifacts/process-runs/{Guid.NewGuid():D}/steps/code-change.md";
            var output = new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Implementation evidence is ready for downstream validation.",
                EvidenceRefs = [primaryRef],
                AcceptanceCriteriaEvidence =
                [
                    new ProcessAcceptanceCriterionEvidence
                    {
                        CriterionId = "AC-008",
                        Status = ProcessAcceptanceCriterionEvidenceStatus.NotVerified,
                        Summary = "Downstream rendered proof remains required.",
                        EvidenceRefs = [ungroundedRef]
                    }
                ],
                NextActions = ["Run downstream validation."]
            };
            var completionIssueResultFactory = new ProcessCompletionIssueResultFactory(
                workspaceFiles,
                ProcessCompletionDefectEvidenceCatalog.Empty);
            var completionGateEvaluator = new ProcessCompletionGateEvaluator([_ => null]);
            var completionCoordinator = new ProcessStepCompletionCoordinator(
                completionIssueResultFactory,
                new ProcessManagedArtifactService(workspaceFiles),
                new ProcessOutcomeGroundingValidator(workspaceFiles),
                completionGateEvaluator,
                new ProcessExecutionResultConverter(
                    completionGateEvaluator,
                    CreateToolReceiptPolicyCatalog(),
                    completionIssueResultFactory),
                NullLogger<ProcessStepCompletionCoordinator>.Instance);
            var executionRunId = Guid.NewGuid();

            var materialization = completionCoordinator.Materialize(
                assignment,
                output,
                executionRunId,
                [],
                ProcessStepExecutionContract.Empty);
            var result = completionCoordinator.Complete(
                assignment,
                materialization,
                "sha256:raw-output",
                executionRunId,
                materialization.ToolReceipts,
                stepContract: ProcessStepExecutionContract.Empty);
            var readResult = workspaceFiles.ReadTextFile(
                primaryRef,
                WorkspaceFileLimits.MaxTextReadCharacters);

            Assert.True(result.Outcome == StrategyOutcome.Succeeded, DescribeResult(result));
            Assert.DoesNotContain(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.ungrounded_managed_artifact_reference");
            Assert.True(readResult.Succeeded, readResult.Message);
            Assert.False(readResult.IsTruncated);
            Assert.DoesNotContain(ungroundedRef, readResult.Content, StringComparison.Ordinal);
            Assert.Contains("| AC-008 | NotVerified | Downstream rendered proof remains required. |  |", readResult.Content);
            Assert.Empty(
                Assert.Single(materialization.Output.AcceptanceCriteriaEvidence).EvidenceRefs);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Materialization_sanitizes_narratives_and_physical_refs_before_managed_artifact_write()
    {
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            const string configuredRoot = @"C:\Private\Product\";
            const string caseVariantRoot = @"c:\private\product";
            const string foreignUnixRoot = "/home/alice/private/vault";
            const string rawSecret = "raw-managed-artifact-token";
            var assignment = CreateManagedArtifactAssignment("code-change") with
            {
                LaunchVariables = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProductRoot] = configuredRoot
                }
            };
            var primaryRef = BuildStepArtifactRef(assignment);
            var physicalEvidenceRef = $@"{caseVariantRoot}\src\Feature.cs";
            var output = new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = $"Completed {physicalEvidenceRef} and {foreignUnixRoot}; password={rawSecret}",
                EvidenceRefs = [primaryRef, physicalEvidenceRef],
                AcceptanceCriteriaEvidence =
                [
                    new ProcessAcceptanceCriterionEvidence
                    {
                        CriterionId = "AC-SECURE",
                        Status = ProcessAcceptanceCriterionEvidenceStatus.Passed,
                        Summary = $"Validated {physicalEvidenceRef} and {foreignUnixRoot}; token={rawSecret}",
                        EvidenceRefs = [physicalEvidenceRef]
                    }
                ],
                NextActions = [$"Review {physicalEvidenceRef} and {foreignUnixRoot}; secret={rawSecret}"],
                HumanReadableSummaryMarkdown = $"Managed output from {physicalEvidenceRef} and {foreignUnixRoot}; api_key={rawSecret}"
            };
            var completionIssueResultFactory = new ProcessCompletionIssueResultFactory(
                workspaceFiles,
                ProcessCompletionDefectEvidenceCatalog.Empty);
            var completionGateEvaluator = new ProcessCompletionGateEvaluator([_ => null]);
            var completionCoordinator = new ProcessStepCompletionCoordinator(
                completionIssueResultFactory,
                new ProcessManagedArtifactService(workspaceFiles),
                new ProcessOutcomeGroundingValidator(workspaceFiles),
                completionGateEvaluator,
                new ProcessExecutionResultConverter(
                    completionGateEvaluator,
                    CreateToolReceiptPolicyCatalog(),
                    completionIssueResultFactory),
                NullLogger<ProcessStepCompletionCoordinator>.Instance);

            var materialization = completionCoordinator.Materialize(
                assignment,
                output,
                Guid.NewGuid(),
                [],
                ProcessStepExecutionContract.Empty);
            var readResult = workspaceFiles.ReadTextFile(
                primaryRef,
                WorkspaceFileLimits.MaxTextReadCharacters);

            Assert.Null(materialization.Issue);
            Assert.True(readResult.Succeeded, readResult.Message);
            Assert.Contains("[configured product root]", readResult.Content, StringComparison.Ordinal);
            Assert.Contains("[physical path removed]", readResult.Content, StringComparison.Ordinal);
            Assert.DoesNotContain(configuredRoot, readResult.Content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(foreignUnixRoot, readResult.Content, StringComparison.Ordinal);
            Assert.DoesNotContain(rawSecret, readResult.Content, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Materialization_rejects_oversized_structured_shape_before_workspace_mutation()
    {
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var assignment = CreateManagedArtifactAssignment("code-change");
            var primaryRef = BuildStepArtifactRef(assignment);
            var output = new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Implementation completed.",
                EvidenceRefs = [primaryRef],
                NextActions = Enumerable.Range(0, 17)
                    .Select(index => $"Next action {index:D2}")
                    .ToArray()
            };
            var completionIssueResultFactory = new ProcessCompletionIssueResultFactory(
                workspaceFiles,
                ProcessCompletionDefectEvidenceCatalog.Empty);
            var completionGateEvaluator = new ProcessCompletionGateEvaluator([_ => null]);
            var completionCoordinator = new ProcessStepCompletionCoordinator(
                completionIssueResultFactory,
                new ProcessManagedArtifactService(workspaceFiles),
                new ProcessOutcomeGroundingValidator(workspaceFiles),
                completionGateEvaluator,
                new ProcessExecutionResultConverter(
                    completionGateEvaluator,
                    CreateToolReceiptPolicyCatalog(),
                    completionIssueResultFactory),
                NullLogger<ProcessStepCompletionCoordinator>.Instance);

            var materialization = completionCoordinator.Materialize(
                assignment,
                output,
                Guid.NewGuid(),
                [],
                ProcessStepExecutionContract.Empty);

            Assert.Equal(
                ProcessStepCompletionCoordinator.InvalidStructuredOutcomeShapeDiagnosticCode,
                materialization.Issue?.Code);
            Assert.False(File.Exists(Path.Combine(
                workspaceRoot,
                primaryRef.Replace('/', Path.DirectorySeparatorChar))));
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Grounded_defect_outcome_omits_only_ungrounded_supplemental_top_level_evidence_ref()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var page = Path.Combine(outputRoot, "src", "App", "Pages", "Home.razor");
            var assignment = CreateQaValidationAssignmentWithAcceptanceMatrix(outputRoot, page);
            var primaryRef = BuildStepArtifactRef(assignment);
            var ungroundedSupplementalRef =
                $"artifacts/process-runs/{Guid.NewGuid():D}/steps/capture-ui-screenshots.md";
            var output = new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "QA found a repairable acceptance defect.",
                BranchOutcomeKey = "repair-required",
                BranchOutcomeTitle = "Repair required",
                EvidenceRefs = [primaryRef, ungroundedSupplementalRef],
                AcceptanceCriteriaEvidence =
                [
                    new ProcessAcceptanceCriterionEvidence
                    {
                        CriterionId = "AC-001",
                        Status = ProcessAcceptanceCriterionEvidenceStatus.Failed,
                        Summary = "The required movement workflow is incomplete.",
                        EvidenceRefs = [primaryRef]
                    }
                ],
                NextActions = ["Repair the failed criterion and rerun QA."],
                HumanReadableSummaryMarkdown = "QA routed the grounded defect to repair."
            };
            var toolReceipts =
                new[] { CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote QA evidence.") };
            var validator = new ProcessOutcomeGroundingValidator(
                TestWorkspaceServices.CreateFileService(Path.GetTempPath()));
            var stepContract = ProcessStepExecutionContract.Empty with
            {
                ConfiguredBranchOutcomeIds = [new BranchOutcomeId("repair-required")]
            };

            var normalized = validator.RemoveUngroundedSupplementalEvidenceRefsFromGroundedDefectOutcome(
                assignment,
                output,
                toolReceipts,
                stepContract,
                out var removedCount);

            Assert.Equal(1, removedCount);
            Assert.Equal([primaryRef], normalized.EvidenceRefs);
            Assert.Equal(output.Status, normalized.Status);
            Assert.Equal(output.Reason, normalized.Reason);
            Assert.Equal(output.BranchOutcomeKey, normalized.BranchOutcomeKey);
            Assert.Equal(output.BranchOutcomeTitle, normalized.BranchOutcomeTitle);
            Assert.Same(output.AcceptanceCriteriaEvidence, normalized.AcceptanceCriteriaEvidence);
            Assert.Same(output.NextActions, normalized.NextActions);
            Assert.Equal(output.HumanReadableSummaryMarkdown, normalized.HumanReadableSummaryMarkdown);
            Assert.Null(validator.ValidateGroundedOutcomeReferences(
                assignment,
                normalized,
                toolReceipts,
                stepContract));
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Defect_outcome_does_not_omit_supplemental_ref_when_criterion_evidence_is_ungrounded()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var page = Path.Combine(outputRoot, "src", "App", "Pages", "Home.razor");
            var assignment = CreateQaValidationAssignmentWithAcceptanceMatrix(outputRoot, page);
            var primaryRef = BuildStepArtifactRef(assignment);
            var ungroundedRef =
                $"artifacts/process-runs/{Guid.NewGuid():D}/steps/capture-ui-screenshots.md";
            var output = new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "QA found a repairable acceptance defect.",
                BranchOutcomeKey = "repair-required",
                BranchOutcomeTitle = "Repair required",
                EvidenceRefs = [primaryRef, ungroundedRef],
                AcceptanceCriteriaEvidence =
                [
                    new ProcessAcceptanceCriterionEvidence
                    {
                        CriterionId = "AC-001",
                        Status = ProcessAcceptanceCriterionEvidenceStatus.Failed,
                        Summary = "The required movement workflow is incomplete.",
                        EvidenceRefs = [ungroundedRef]
                    }
                ],
                NextActions = ["Repair the failed criterion and rerun QA."]
            };
            var validator = new ProcessOutcomeGroundingValidator(
                TestWorkspaceServices.CreateFileService(Path.GetTempPath()));

            var normalized = validator.RemoveUngroundedSupplementalEvidenceRefsFromGroundedDefectOutcome(
                assignment,
                output,
                [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote QA evidence.")],
                ProcessStepExecutionContract.Empty,
                out var removedCount);

            Assert.Equal(0, removedCount);
            Assert.Same(output, normalized);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Theory]
    [InlineData("unknown-branch")]
    [InlineData("quality-accepted")]
    public void Defect_outcome_does_not_omit_supplemental_ref_for_unknown_or_acceptance_branch(
        string branchOutcomeKey)
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var page = Path.Combine(outputRoot, "src", "App", "Pages", "Home.razor");
            var assignment = CreateQaValidationAssignmentWithAcceptanceMatrix(outputRoot, page);
            var primaryRef = BuildStepArtifactRef(assignment);
            var ungroundedRef =
                $"artifacts/process-runs/{Guid.NewGuid():D}/steps/capture-ui-screenshots.md";
            var output = new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "QA found a repairable acceptance defect.",
                BranchOutcomeKey = branchOutcomeKey,
                BranchOutcomeTitle = "Defect outcome",
                EvidenceRefs = [primaryRef, ungroundedRef],
                AcceptanceCriteriaEvidence =
                [
                    new ProcessAcceptanceCriterionEvidence
                    {
                        CriterionId = "AC-001",
                        Status = ProcessAcceptanceCriterionEvidenceStatus.Failed,
                        Summary = "The required movement workflow is incomplete.",
                        EvidenceRefs = [primaryRef]
                    }
                ],
                NextActions = ["Repair the failed criterion and rerun QA."]
            };
            var validator = new ProcessOutcomeGroundingValidator(
                TestWorkspaceServices.CreateFileService(Path.GetTempPath()));

            var normalized = validator.RemoveUngroundedSupplementalEvidenceRefsFromGroundedDefectOutcome(
                assignment,
                output,
                [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote QA evidence.")],
                ProcessStepExecutionContract.Empty,
                out var removedCount);

            Assert.Equal(0, removedCount);
            Assert.Same(output, normalized);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Defect_outcome_does_not_omit_supplemental_ref_without_canonical_managed_evidence()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var page = Path.Combine(outputRoot, "src", "App", "Pages", "Home.razor");
            var assignment = CreateQaValidationAssignmentWithAcceptanceMatrix(outputRoot, page);
            var primaryRef = BuildStepArtifactRef(assignment);
            var ungroundedRef =
                $"artifacts/process-runs/{Guid.NewGuid():D}/steps/capture-ui-screenshots.md";
            var output = new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "QA found a repairable acceptance defect.",
                BranchOutcomeKey = "repair-required",
                BranchOutcomeTitle = "Repair required",
                EvidenceRefs = [ungroundedRef],
                AcceptanceCriteriaEvidence =
                [
                    new ProcessAcceptanceCriterionEvidence
                    {
                        CriterionId = "AC-001",
                        Status = ProcessAcceptanceCriterionEvidenceStatus.Failed,
                        Summary = "The required movement workflow is incomplete.",
                        EvidenceRefs = [primaryRef]
                    }
                ],
                NextActions = ["Repair the failed criterion and rerun QA."]
            };
            var validator = new ProcessOutcomeGroundingValidator(
                TestWorkspaceServices.CreateFileService(Path.GetTempPath()));

            var normalized = validator.RemoveUngroundedSupplementalEvidenceRefsFromGroundedDefectOutcome(
                assignment,
                output,
                [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote QA evidence.")],
                ProcessStepExecutionContract.Empty,
                out var removedCount);

            Assert.Equal(0, removedCount);
            Assert.Same(output, normalized);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void RepairRequired_with_typed_failed_acceptance_criterion_is_accepted_as_defect_evidence()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var page = Path.Combine(outputRoot, "src", "App", "Pages", "Home.razor");
            Directory.CreateDirectory(Path.GetDirectoryName(page)!);
            File.WriteAllText(page, "<h1>Incomplete app</h1>");
            var assignment = CreateQaValidationAssignmentWithAcceptanceMatrix(outputRoot, page);
            var primaryRef = BuildStepArtifactRef(assignment);

            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "QA found the required workflow missing from the inspected product.",
                    BranchOutcomeKey = "repair-required",
                    BranchOutcomeTitle = "Repair required",
                    EvidenceRefs = [primaryRef],
                    AcceptanceCriteriaEvidence =
                    [
                        new ProcessAcceptanceCriterionEvidence
                        {
                            CriterionId = "AC-001",
                            Status = ProcessAcceptanceCriterionEvidenceStatus.Failed,
                            Summary = "The inspected product does not implement the required movement workflow.",
                            EvidenceRefs = [primaryRef]
                        }
                    ],
                    NextActions = ["Repair the missing workflow and re-run focused validation."]
                },
                [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote QA defect evidence.")]);

            Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
            Assert.Contains(result.ManagerSignals, signal =>
                signal.Code.Value == ProcessBranchSignalCodes.Outcome("repair-required").Value);
            Assert.DoesNotContain(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.branch_outcome_defect_evidence_missing");
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void RepairRequired_with_clean_browser_console_receipt_is_not_accepted_as_defect_evidence()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var page = Path.Combine(outputRoot, "src", "App", "Pages", "Home.razor");
            Directory.CreateDirectory(Path.GetDirectoryName(page)!);
            File.WriteAllText(page, "<h1>Implemented app</h1>");
            var assignment = CreateQaValidationAssignmentWithBranchAwareCompletionRules(
                outputRoot,
                page);
            var primaryRef = BuildStepArtifactRef(assignment);
            var consoleRef = $"artifacts/process-runs/{assignment.RunId.Value:D}/browser-console.log";

            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Current-run browser navigation reported no console errors.",
                    BranchOutcomeKey = "repair-required",
                    BranchOutcomeTitle = "Repair required",
                    EvidenceRefs = [primaryRef, consoleRef],
                    NextActions = ["Run missing deterministic defect validation."]
                },
                [
                    CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote QA evidence."),
                    CreateToolReceipt(
                        "browser_console_messages",
                        $"level=\"error\", filename=\"{consoleRef}\"",
                        "Succeeded")
                ]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.branch_outcome_defect_evidence_missing");
            Assert.DoesNotContain(result.ManagerSignals, signal =>
                signal.Code.Value == ProcessBranchSignalCodes.Outcome("repair-required").Value);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void RepairRequired_without_defect_evidence_and_without_browser_proof_is_not_accepted_as_repair_branch()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var page = Path.Combine(outputRoot, "src", "App", "Pages", "Home.razor");
            Directory.CreateDirectory(Path.GetDirectoryName(page)!);
            File.WriteAllText(page, "<h1>Implemented app</h1>");
            var assignment = CreateQaValidationAssignmentWithBranchAwareCompletionRules(
                outputRoot,
                page);
            var primaryRef = BuildStepArtifactRef(assignment);

            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "QA did not run browser proof and selected repair without concrete defect evidence.",
                    BranchOutcomeKey = "repair-required",
                    BranchOutcomeTitle = "Repair required",
                    EvidenceRefs = [primaryRef],
                    NextActions = ["Run missing browser proof."]
                },
                [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote incomplete QA evidence.")]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.branch_outcome_defect_evidence_missing");
            Assert.DoesNotContain(result.ManagerSignals, signal => signal.Code.Value == ProcessBranchSignalCodes.Outcome("repair-required").Value);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Quality_repair_completion_enforces_ungated_product_file_content_checks()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var scaffoldPage = Path.Combine(outputRoot, "src", "App", "Pages", "Counter.razor");
            Directory.CreateDirectory(Path.GetDirectoryName(scaffoldPage)!);
            File.WriteAllText(scaffoldPage, "@page \"/counter\"");
            var baseAssignment = CreateManagedArtifactAssignment("quality-repair");
            var checks = JsonSerializer.Serialize(new Dictionary<string, object[]>
            {
                ["quality-repair"] =
                [
                    new Dictionary<string, object>
                    {
                        ["pathCandidates"] = new[] { scaffoldPage },
                        ["mustExist"] = false,
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
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Repair completed.",
                    EvidenceRefs = [primaryRef],
                    NextActions = []
                },
                [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote file.")]);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.product_required_file_content_missing");
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
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Wrote the current scope packet.",
            EvidenceRefs = [primaryRef],
            NextActions = [],
            HumanReadableSummaryMarkdown = $"Completed from source document `{ForwardedRuntimeProjectPath}`."
        };
        var issue = ProcessOutcomeGroundingValidator.ValidateGroundedOutcomeReferences(
            assignment,
            output,
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote file.")]);

        Assert.NotNull(issue);
        Assert.Equal("process.adapter.ungrounded_outcome_reference", issue.Code);
        Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, issue.RetrySafety);
        Assert.Contains("not grounded in the current step brief", issue.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ForwardedRuntimeProjectPath, issue.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Managed_artifact_completion_requires_receipt_grounding_for_top_level_evidence_refs()
    {
        var assignment = CreateManagedArtifactAssignment("feature-intake");
        var primaryRef = BuildStepArtifactRef(assignment);
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Wrote the current scope packet.",
            EvidenceRefs = [primaryRef, ForwardedRuntimeProjectPath],
            NextActions = []
        };
        var issue = ProcessOutcomeGroundingValidator.ValidateGroundedOutcomeReferences(
            assignment,
            output,
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote file.")]);

        Assert.NotNull(issue);
        Assert.Equal("process.adapter.ungrounded_outcome_reference", issue.Code);
        Assert.DoesNotContain(ForwardedRuntimeProjectPath, issue.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Acceptance_criterion_evidence_requires_grounded_path_refs()
    {
        var assignment = CreateManagedArtifactAssignment("qa-validation");
        var primaryRef = BuildStepArtifactRef(assignment);
        var ungroundedCriterionRef =
            $"artifacts/process-runs/{Guid.NewGuid():D}/browser/claimed-proof.png";
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Current-run QA completed.",
            EvidenceRefs = [primaryRef],
            AcceptanceCriteriaEvidence =
            [
                new ProcessAcceptanceCriterionEvidence
                {
                    CriterionId = "AC-001",
                    Status = ProcessAcceptanceCriterionEvidenceStatus.Passed,
                    Summary = "The required behavior passed.",
                    EvidenceRefs = [ungroundedCriterionRef]
                }
            ],
            NextActions = []
        };

        var issue = ProcessOutcomeGroundingValidator.ValidateGroundedOutcomeReferences(
            assignment,
            output,
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote file.")]);

        Assert.NotNull(issue);
        Assert.Equal("process.adapter.ungrounded_outcome_reference", issue.Code);
        Assert.DoesNotContain(ungroundedCriterionRef, issue.Summary, StringComparison.OrdinalIgnoreCase);
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
    public void Recovered_blocked_primary_artifact_does_not_hide_missing_validation_tool_receipts()
    {
        var requiredToolReceiptMap = JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["qa-validation"] =
            [
                "workspace_dotnet_restore",
                "workspace_dotnet_build",
                "workspace_dotnet_test"
            ]
        });
        var assignment = CreateManagedArtifactAssignment(
            "qa-validation",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ]) with
        {
            LaunchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep] = requiredToolReceiptMap
            }
        };
        var primaryRef = BuildStepArtifactRef(assignment);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Blocked,
                Reason = $"Recovered governed process step outcome from primary managed artifact '{primaryRef}' after provider completion omitted the required finalizer. The artifact declares status 'Blocked'.",
                EvidenceRefs = [primaryRef],
                NextActions = ["Execute the current-run validation chain."],
                HumanReadableSummaryMarkdown = "Validation is blocked because restore, build, and test have not been executed in this step."
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")]);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("process.adapter.product_required_tool_receipt_blocked_retry", diagnostic.Code.Value);
        Assert.Contains("workspace_dotnet_restore", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("missing primary managed artifact", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Subprocess_entry_context_hydrates_every_inherited_parent_artifact()
    {
        var workspaceFiles = CreateWorkspaceFileService(out _);
        var parentRunId = Guid.NewGuid();
        var parentRefs = new[]
        {
            $"artifacts/process-runs/{parentRunId:D}/steps/implementation.md",
            $"artifacts/process-runs/{parentRunId:D}/steps/qa-validation.md"
        };
        workspaceFiles.WriteTextFile(parentRefs[0], "Status: Completed\nImplementation evidence");
        workspaceFiles.WriteTextFile(parentRefs[1], "Status: Completed\nQA found a visible runtime fault");
        var baseAssignment = CreateManagedArtifactAssignment(
            "diagnose-quality-failure",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ]);
        var assignment = baseAssignment with
        {
            LaunchVariables = WithLaunchVariables(
                baseAssignment,
                (ProcessRuntimeLaunchVariables.ParentRequiredArtifactRefs,
                    ProcessRuntimeLaunchVariables.SerializeParentRequiredArtifactRefs(parentRefs)))
        };
        var hydration = new ProcessParentSubprocessArtifactContextHydrator(workspaceFiles).Hydrate(assignment);

        Assert.Null(hydration.Issue);
        Assert.All(parentRefs, artifactRef =>
            Assert.Contains(artifactRef, hydration.PromptContribution, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Implementation evidence", hydration.PromptContribution, StringComparison.Ordinal);
        Assert.Contains("QA found a visible runtime fault", hydration.PromptContribution, StringComparison.Ordinal);
    }

    [Fact]
    public void Subprocess_entry_context_fails_before_agent_invocation_when_parent_artifact_is_missing()
    {
        var workspaceFiles = CreateWorkspaceFileService(out _);
        var parentRunId = Guid.NewGuid();
        var parentRefs = new[]
        {
            $"artifacts/process-runs/{parentRunId:D}/steps/implementation.md",
            $"artifacts/process-runs/{parentRunId:D}/steps/qa-validation.md"
        };
        workspaceFiles.WriteTextFile(parentRefs[0], "Status: Completed\nImplementation evidence");
        var baseAssignment = CreateManagedArtifactAssignment(
            "diagnose-quality-failure",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ]);
        var assignment = baseAssignment with
        {
            LaunchVariables = WithLaunchVariables(
                baseAssignment,
                (ProcessRuntimeLaunchVariables.ParentRequiredArtifactRefs,
                    ProcessRuntimeLaunchVariables.SerializeParentRequiredArtifactRefs(parentRefs)))
        };
        var hydration = new ProcessParentSubprocessArtifactContextHydrator(workspaceFiles).Hydrate(assignment);

        Assert.Empty(hydration.PromptContribution);
        Assert.NotNull(hydration.Issue);
        Assert.Equal(ProcessParentSubprocessArtifactContextHydrator.MissingContextDiagnosticCode, hydration.Issue!.Code);
        Assert.Contains(parentRefs[1], hydration.Issue.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Blocked_result_with_branch_outcome_and_evidence_is_not_promoted_to_completed()
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

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Empty(result.ProducedArtifacts);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.agent_blocked");
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == ProcessBranchSignalCodes.Outcome("setup-repair-required").Value);
    }

    [Fact]
    public void Blocked_result_does_not_infer_branch_from_summary_prose()
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
            ],
            stepContract: BranchStepContract(
                "feature-accepted",
                "feature-repair-required"));

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Empty(result.ProducedArtifacts);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.agent_blocked");
        Assert.DoesNotContain(result.ManagerSignals, signal =>
            signal.Code.Value == ProcessBranchSignalCodes.Outcome("feature-repair-required").Value);
    }

    [Fact]
    public void Completed_result_does_not_infer_branch_from_summary_prose()
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
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")],
            stepContract: BranchStepContract(
                "feature-accepted",
                "feature-repair-required"));

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Empty(result.ProducedArtifacts);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == ProcessCompletionDiagnosticCodes.RequiredBranchOutcomeMissing);
        Assert.DoesNotContain(result.ManagerSignals, signal =>
            signal.Code.Value == ProcessBranchSignalCodes.Outcome("feature-repair-required").Value);
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
                    JsonSerializer.Serialize(new[]
                    {
                        new
                        {
                            toolName = "workspace_dotnet_restore",
                            allowFailedExecutionReceipt = true
                        },
                        new
                        {
                            toolName = "workspace_dotnet_build",
                            allowFailedExecutionReceipt = true
                        },
                        new
                        {
                            toolName = "workspace_dotnet_test",
                            allowFailedExecutionReceipt = true
                        }
                    })
            }
        };
        var primaryRef = BuildStepArtifactRef(assignment);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Recovered governed process step outcome from primary managed artifact after provider timeout.",
                BranchOutcomeKey = "feature-repair-required",
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
            ],
            stepContract: BranchStepContract(
                "feature-accepted",
                "feature-repair-required"));

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
        Assert.Empty(result.Diagnostics);
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == ProcessBranchSignalCodes.Outcome("feature-repair-required").Value);
    }

    [Theory]
    [InlineData("feature-unknown")]
    [InlineData("Feature-Repair-Required")]
    public void Completed_result_rejects_non_exact_typed_branch_outcome_key(
        string branchOutcomeKey)
    {
        var assignment = CreateManagedArtifactAssignment("targeted-validation");
        var primaryRef = BuildStepArtifactRef(assignment);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Validation completed with a typed branch selection.",
                BranchOutcomeKey = branchOutcomeKey,
                EvidenceRefs = [primaryRef],
                NextActions = []
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")],
            stepContract: BranchStepContract(
                "feature-accepted",
                "feature-repair-required"));

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == ProcessCompletionDiagnosticCodes.RequiredBranchOutcomeMissing);
        Assert.DoesNotContain(result.ManagerSignals, signal =>
            signal.Code.Value == ProcessBranchSignalCodes.Outcome(branchOutcomeKey).Value);
    }

    [Fact]
    public void Completed_result_does_not_infer_branch_from_artifact_section()
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
            ],
            stepContract: BranchStepContract(
                "feature-accepted",
                "feature-repair-required"));

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Empty(result.ProducedArtifacts);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == ProcessCompletionDiagnosticCodes.RequiredBranchOutcomeMissing);
        Assert.DoesNotContain(result.ManagerSignals, signal =>
            signal.Code.Value == ProcessBranchSignalCodes.Outcome("feature-accepted").Value);
    }

    [Fact]
    public void Completed_result_does_not_infer_branch_from_decision_section()
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
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")],
            stepContract: BranchStepContract(
                "feature-accepted",
                "feature-repair-required"));

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == ProcessCompletionDiagnosticCodes.RequiredBranchOutcomeMissing);
        Assert.DoesNotContain(result.ManagerSignals, signal =>
            signal.Code.Value == ProcessBranchSignalCodes.Outcome("feature-accepted").Value);
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
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")],
            stepContract: BranchStepContract(
                "feature-accepted",
                "feature-repair-required"));

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == ProcessCompletionDiagnosticCodes.RequiredBranchOutcomeMissing &&
            diagnostic.RetrySafety == ProcessDiagnosticRetrySafety.SafeToRetry);
    }

    [Fact]
    public void Completed_branch_capable_result_without_selection_requires_rework()
    {
        var baseAssignment = CreateManagedArtifactAssignment("add-tests-and-proof");
        var assignment = baseAssignment with
        {
            Prompt = """
            Available branch outcomes:
            - slice-accepted: Slice accepted - Validation passed.
            - slice-repair-required: Slice repair required - Validation found a repairable defect.
            """
        };
        var primaryRef = BuildStepArtifactRef(assignment);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Recovered the rewritten validation artifact.",
                EvidenceRefs = [primaryRef],
                NextActions = []
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")],
            stepContract: BranchStepContract(
                "slice-accepted",
                "slice-repair-required"));

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == ProcessCompletionDiagnosticCodes.RequiredBranchOutcomeMissing);
        Assert.Empty(result.ProducedArtifacts);
    }

    [Fact]
    public void Completed_non_branching_result_is_not_confused_by_other_bulleted_metadata()
    {
        var baseAssignment = CreateManagedArtifactAssignment("architecture-handoff");
        var assignment = baseAssignment with
        {
            Prompt = """
            Required outcome metadata:
            - Status: Completed
            - Evidence: managed artifact
            """
        };
        var primaryRef = BuildStepArtifactRef(assignment);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Architecture handoff completed.",
                EvidenceRefs = [primaryRef],
                NextActions = []
            },
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")]);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == ProcessCompletionDiagnosticCodes.RequiredBranchOutcomeMissing);
    }

    [Fact]
    public void Completed_result_does_not_infer_branch_from_artifact_title()
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
            [CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Created file.")],
            stepContract: BranchStepContract(
                "setup-validated",
                "setup-repair-required"));

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == ProcessCompletionDiagnosticCodes.RequiredBranchOutcomeMissing);
        Assert.DoesNotContain(result.ManagerSignals, signal =>
            signal.Code.Value == ProcessBranchSignalCodes.Outcome("setup-validated").Value);
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
            },
            [CreateToolReceipt(
                "workspace_write_file",
                BuildStepArtifactRef(assignment),
                "PolicyDenied: workspace_write_file was blocked by policy.")]);

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.managed_artifact_self_evidence_retry");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.agent_rights_request");
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.agent_rights_request");
    }

    [Fact]
    public void Managed_artifact_unsubstantiated_read_access_claim_is_retried_once()
    {
        var assignment = CreateManagedArtifactAssignment(
            "slice-repair-escalation",
            requiredArtifactSlotIds: [ArtifactSlotId.New()]);

        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Blocked,
                Reason = "Current-run review evidence is incomplete because this step did not inspect the required child repair artifacts. Reassign to a reviewer with read access.",
                EvidenceRefs = [],
                NextActions = ["Inspect the current-run child repair implementation and recheck artifacts."],
                HumanReadableSummaryMarkdown = "The required artifacts were available but were not read in this execution."
            });

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("process.adapter.managed_artifact_self_evidence_retry", diagnostic.Code.Value);
        Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
        Assert.DoesNotContain(result.Diagnostics, item => item.Code.Value == "process.adapter.agent_rights_request");
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
    public void Runtime_readiness_prefers_context_specialist_when_role_fit_is_otherwise_equal()
    {
        var genericDeveloper = NewAgent(
            ".NET Application Developer",
            ".NET implementation specialist",
            AgentWorkloadKind.Programming,
            ["dotnet", "programming", "blazor"],
            AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var blazorDeveloper = NewAgent(
            "Blazor Application Developer",
            "Blazor implementation specialist",
            AgentWorkloadKind.Programming,
            ["dotnet", "programming", "blazor", "frontend"],
            AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var request = new AgentProcessRoleReadinessRequest(
            "code-change",
            "Implement the .NET feature or function",
            "software-engineer",
            "software-engineer",
            ".NET feature implementer",
            [
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.MutateProductTarget,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            ProcessOperationContractNames.ExternalProductTargetMutable,
            PreferredSpecializationTags: ["blazor", "wasm", "frontend", "pwa"]);

        var genericReadiness = AgentProcessReadinessEvaluator.Evaluate(genericDeveloper, request);
        var blazorReadiness = AgentProcessReadinessEvaluator.Evaluate(blazorDeveloper, request);

        Assert.True(genericReadiness.IsExecutionReady);
        Assert.True(blazorReadiness.IsExecutionReady);
        Assert.True(blazorReadiness.Score > genericReadiness.Score);
        Assert.Contains("preferred specialization", blazorReadiness.MatchSummary, StringComparison.Ordinal);
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

        var pendingRunId = await ProcessSubprocessState.TryResolvePendingChildRunAsync(
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

        var pendingRunId = await ProcessSubprocessState.TryResolvePendingChildRunAsync(
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

        var pendingRunId = await ProcessSubprocessState.TryResolvePendingChildRunAsync(
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
            assignment.StepKey) with
        {
            StepKey = "slice-handoff"
        };
        var assignmentStore = new InMemoryAssignmentStore(childAssignment);
        var stateStore = new InMemoryRuntimeStateStore(
            NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
            NewRuntimeState(parentRunId, childRunId, ProcessRuntimeStatus.Active));

        var pendingRunId = await ProcessSubprocessState.TryResolveExistingPendingChildRunAsync(
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
            var adapter = CreateAdapter(
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
                    CreateAdapterRequest(
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
    public async Task ExecuteAsync_delegates_typed_workflow_assignment_before_agent_and_subprocess_paths()
    {
        var agent = NewAgent(
            "Workflow process sentinel",
            "Must not execute for a workflow-bound assignment.",
            AgentWorkloadKind.Management,
            ["workflow-sentinel"],
            AgentWorkspaceToolProfileKind.ReadOnly);
        var assignment = CreateSubprocessAssignment() with
        {
            ExecutorKind = ProcessLaunchExecutorKinds.Workflow,
            ExecutorId = "workflow-display-only",
            WorkflowBinding = new ProcessWorkflowExecutorBinding(new ProcessWorkflowId(Guid.NewGuid()))
        };
        var expected = ProcessExecutionResultFactory.Failed(
            "process.adapter.workflow_delegation_test",
            "Workflow assignment reached the dedicated workflow driver.",
            assignment.StepInstanceId.ToString());
        var workflowExecutor = new RecordingProcessWorkflowStepExecutor(expected);
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The agent path must not execute for a workflow assignment."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);

        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(),
                workspaceFiles,
                workflowStepExecutor: workflowExecutor);

            var result = await adapter.ExecuteAsync(CreateAdapterRequest(
                assignment,
                ProcessExecutionAdapterKind.Workflow,
                new ProcessExecutionAdapterOperationKey("execute"),
                Binding,
                [],
                [],
                ProcessStepExecutionContract.Empty));

            Assert.Equal(expected, result);
            Assert.Equal(assignment, workflowExecutor.Assignment);
            Assert.NotNull(workflowExecutor.StepContract);
            Assert.False(workspace.ExecuteRunCalled);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_rejects_oversized_runtime_tool_contract_before_workflow_launch()
    {
        var agent = NewAgent(
            "Workflow process sentinel",
            "Must not execute for an invalid workflow contract.",
            AgentWorkloadKind.Management,
            ["workflow-sentinel"],
            AgentWorkspaceToolProfileKind.ReadOnly);
        var assignment = CreateSubprocessAssignment() with
        {
            ExecutorKind = ProcessLaunchExecutorKinds.Workflow,
            ExecutorId = "workflow-display-only",
            WorkflowBinding = new ProcessWorkflowExecutorBinding(new ProcessWorkflowId(Guid.NewGuid()))
        };
        var workflowExecutor = new RecordingProcessWorkflowStepExecutor(
            ProcessExecutionResultFactory.Failed(
                "process.adapter.workflow_must_not_launch",
                "Workflow execution must remain behind bounded preflight.",
                assignment.StepInstanceId.ToString()));
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The agent path must not execute for a workflow assignment."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(),
                workspaceFiles,
                runtimeToolPreflightService: new ProcessRuntimeToolPreflightService(
                    [],
                    [],
                    ProcessRuntimeToolPreflightContributionCatalog.Empty),
                workflowStepExecutor: workflowExecutor);
            var requiredTools = Enumerable.Range(0, 65)
                .Select(index => $"runtime_tool_{index:D2}")
                .ToArray();

            var result = await adapter.ExecuteAsync(CreateAdapterRequest(
                assignment,
                ProcessExecutionAdapterKind.Workflow,
                new ProcessExecutionAdapterOperationKey("execute"),
                Binding,
                [],
                [],
                new ProcessStepExecutionContract([], [], requiredTools, "sha256:oversized-workflow-tools")));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Equal(0, workflowExecutor.ExecutionCount);
            Assert.False(workspace.ExecuteRunCalled);
            Assert.Equal(
                "process.adapter.runtime_tool_preflight_failed",
                Assert.Single(result.Diagnostics).Code.Value);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_rejects_assignment_runtime_tool_drift_before_workflow_launch()
    {
        var agent = NewAgent(
            "Workflow process sentinel",
            "Must not execute for assignment contract drift.",
            AgentWorkloadKind.Management,
            ["workflow-sentinel"],
            AgentWorkspaceToolProfileKind.ReadOnly);
        var assignment = CreateSubprocessAssignment() with
        {
            ExecutorKind = ProcessLaunchExecutorKinds.Workflow,
            ExecutorId = "workflow-display-only",
            WorkflowBinding = new ProcessWorkflowExecutorBinding(new ProcessWorkflowId(Guid.NewGuid())),
            CapabilityScope = new ProcessCapabilityScope
            {
                RequiredReceipts =
                [
                    new ProcessRequiredToolReceipt
                    {
                        Key = "drifted-python-tool",
                        ToolName = "workspace_python_run_file"
                    }
                ]
            }
        };
        var workflowExecutor = new RecordingProcessWorkflowStepExecutor(
            ProcessExecutionResultFactory.Failed(
                "process.adapter.workflow_must_not_launch",
                "Workflow execution must remain behind immutable contract validation.",
                assignment.StepInstanceId.ToString()));
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The agent path must not execute for assignment contract drift."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(),
                workspaceFiles,
                workflowStepExecutor: workflowExecutor);

            var result = await adapter.ExecuteAsync(CreateAdapterRequest(
                assignment,
                ProcessExecutionAdapterKind.Workflow,
                new ProcessExecutionAdapterOperationKey("execute"),
                Binding,
                [],
                [],
                ProcessStepExecutionContract.Empty));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Equal(0, workflowExecutor.ExecutionCount);
            Assert.False(workspace.ExecuteRunCalled);
            Assert.Equal(
                "process.adapter.runtime_tool_contract_changed",
                Assert.Single(result.Diagnostics).Code.Value);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ExecuteAsync_rejects_malformed_capability_scope_receipt_without_disclosure_or_workflow_launch(
        int invalidShape)
    {
        const string malformedToolName = "secret: C:\\private\\token=raw-capability-value";
        var receipt = new ProcessRequiredToolReceipt
        {
            Key = "malformed-runtime-tool",
            ToolName = invalidShape == 0 ? malformedToolName : "workspace_python_run_file",
            Kind = invalidShape == 1
                ? (ProcessRequiredToolReceiptKind)999
                : ProcessRequiredToolReceiptKind.RuntimeToolName,
            Activation = invalidShape == 2
                ? (ProcessRequiredToolReceiptActivation)999
                : ProcessRequiredToolReceiptActivation.Always
        };
        var agent = NewAgent(
            "Workflow process sentinel",
            "Must not execute for a malformed capability-scope contract.",
            AgentWorkloadKind.Management,
            ["workflow-sentinel"],
            AgentWorkspaceToolProfileKind.ReadOnly);
        var capabilityScope = invalidShape < 3
            ? new ProcessCapabilityScope
            {
                RequiredReceipts = [receipt]
            }
            : new ProcessCapabilityScope
            {
                Directives =
                [
                    new ProcessCapabilityScopeDirective
                    {
                        Kind = invalidShape == 4
                            ? (ProcessCapabilityScopeDirectiveKind)999
                            : ProcessCapabilityScopeDirectiveKind.AllowOnly,
                        Target = new ProcessCapabilityScopeTarget
                        {
                            Kind = ProcessCapabilityScopeTargetKind.Unspecified
                        }
                    }
                ]
            };
        var assignment = CreateSubprocessAssignment() with
        {
            ExecutorKind = ProcessLaunchExecutorKinds.Workflow,
            ExecutorId = "workflow-display-only",
            WorkflowBinding = new ProcessWorkflowExecutorBinding(new ProcessWorkflowId(Guid.NewGuid())),
            CapabilityScope = capabilityScope
        };
        var workflowExecutor = new RecordingProcessWorkflowStepExecutor(
            ProcessExecutionResultFactory.Failed(
                "process.adapter.workflow_must_not_launch",
                "Workflow execution must remain behind canonical preflight.",
                assignment.StepInstanceId.ToString()));
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The agent path must not execute for a malformed workflow contract."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(),
                workspaceFiles,
                runtimeToolPreflightService: new ProcessRuntimeToolPreflightService(
                    [],
                    [],
                    ProcessRuntimeToolPreflightContributionCatalog.Empty),
                workflowStepExecutor: workflowExecutor);

            var result = await adapter.ExecuteAsync(CreateAdapterRequest(
                assignment,
                ProcessExecutionAdapterKind.Workflow,
                new ProcessExecutionAdapterOperationKey("execute"),
                Binding,
                [],
                [],
                ProcessStepExecutionContract.Empty));

            var publicReceipt = JsonSerializer.Serialize(result);
            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Equal(0, workflowExecutor.ExecutionCount);
            Assert.False(workspace.ExecuteRunCalled);
            Assert.Equal(
                "process.adapter.runtime_tool_contract_changed",
                Assert.Single(result.Diagnostics).Code.Value);
            if (invalidShape == 0)
            {
                Assert.DoesNotContain(malformedToolName, publicReceipt, StringComparison.Ordinal);
                Assert.DoesNotContain("raw-capability-value", publicReceipt, StringComparison.Ordinal);
            }
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_rejects_malformed_capability_scope_directive_before_runtime_owned_execution()
    {
        var outputRoot = CreateTempProductRoot();
        var (baseAssignment, agent) = CreateRuntimeOwnedProductAssignment(outputRoot);
        var assignment = baseAssignment with
        {
            CapabilityScope = new ProcessCapabilityScope
            {
                Directives =
                [
                    new ProcessCapabilityScopeDirective
                    {
                        Kind = ProcessCapabilityScopeDirectiveKind.AllowOnly,
                        Target = new ProcessCapabilityScopeTarget
                        {
                            Kind = ProcessCapabilityScopeTargetKind.Unspecified
                        }
                    }
                ]
            }
        };
        var runtimeExecutor = new FakeRuntimeOwnedStepExecutor(result: null);
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The agent must not run for an invalid capability scope."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(
                    assignment.RunId,
                    assignment.RunId,
                    ProcessRuntimeStatus.Active)),
                workspaceFiles,
                runtimeToolPreflightService: new ProcessRuntimeToolPreflightService(
                    [],
                    [],
                    ProcessRuntimeToolPreflightContributionCatalog.Empty),
                runtimeOwnedStepExecutors: [runtimeExecutor]);

            var result = await adapter.ExecuteAsync(CreateAdapterRequest(
                assignment,
                ProcessExecutionAdapterKind.Workflow,
                new ProcessExecutionAdapterOperationKey("execute"),
                Binding,
                [],
                []));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Equal(0, runtimeExecutor.CallCount);
            Assert.False(workspace.ExecuteRunCalled);
        }
        finally
        {
            DeleteDirectory(outputRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_rejects_malformed_branch_scoped_product_completion_tool_before_workflow_launch()
    {
        const string malformedToolName = "workspace_python_run_file!secret=C:\\private\\raw-workflow-token";
        var agent = NewAgent(
            "Workflow process sentinel",
            "Must not execute for a malformed product-completion contract.",
            AgentWorkloadKind.Management,
            ["workflow-sentinel"],
            AgentWorkspaceToolProfileKind.ReadOnly);
        var baseAssignment = CreateSubprocessAssignment();
        var assignment = baseAssignment with
        {
            ExecutorKind = ProcessLaunchExecutorKinds.Workflow,
            ExecutorId = "workflow-display-only",
            WorkflowBinding = new ProcessWorkflowExecutorBinding(new ProcessWorkflowId(Guid.NewGuid())),
            LaunchVariables = WithLaunchVariables(
                baseAssignment,
                (ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts,
                    JsonSerializer.Serialize(new object[]
                    {
                        new
                        {
                            toolReceipt = malformedToolName,
                            applicableBranchOutcomeKeys = new[] { "quality-accepted" }
                        }
                    })))
        };
        var workflowExecutor = new RecordingProcessWorkflowStepExecutor(
            ProcessExecutionResultFactory.Failed(
                "process.adapter.workflow_must_not_launch",
                "Workflow execution must remain behind canonical product-completion preflight.",
                assignment.StepInstanceId.ToString()));
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The agent path must not execute for a malformed workflow contract."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(),
                workspaceFiles,
                runtimeToolPreflightService: new ProcessRuntimeToolPreflightService(
                    [],
                    [],
                    ProcessRuntimeToolPreflightContributionCatalog.Empty),
                workflowStepExecutor: workflowExecutor);

            var result = await adapter.ExecuteAsync(CreateAdapterRequest(
                assignment,
                ProcessExecutionAdapterKind.Workflow,
                new ProcessExecutionAdapterOperationKey("execute"),
                Binding,
                [],
                []));

            var publicReceipt = JsonSerializer.Serialize(result);
            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Equal(0, workflowExecutor.ExecutionCount);
            Assert.False(workspace.ExecuteRunCalled);
            Assert.DoesNotContain(malformedToolName, publicReceipt, StringComparison.Ordinal);
            Assert.DoesNotContain("raw-workflow-token", publicReceipt, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_rejects_overlength_runtime_tool_before_runtime_owned_execution()
    {
        var outputRoot = CreateTempProductRoot();
        var (assignment, agent) = CreateRuntimeOwnedProductAssignment(outputRoot);
        var runtimeExecutor = new FakeRuntimeOwnedStepExecutor(result: null);
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The agent must not run for an invalid runtime-owned contract."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(
                    assignment.RunId,
                    assignment.RunId,
                    ProcessRuntimeStatus.Active)),
                workspaceFiles,
                runtimeToolPreflightService: new ProcessRuntimeToolPreflightService(
                    [],
                    [],
                    ProcessRuntimeToolPreflightContributionCatalog.Empty),
                runtimeOwnedStepExecutors: [runtimeExecutor]);

            var result = await adapter.ExecuteAsync(CreateAdapterRequest(
                assignment,
                ProcessExecutionAdapterKind.Workflow,
                new ProcessExecutionAdapterOperationKey("execute"),
                Binding,
                [],
                [],
                new ProcessStepExecutionContract(
                    [],
                    [],
                    [new string('x', 129)],
                    "sha256:overlength-runtime-owned-tool")));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Equal(0, runtimeExecutor.CallCount);
            Assert.False(workspace.ExecuteRunCalled);
        }
        finally
        {
            DeleteDirectory(outputRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_rejects_malformed_branch_scoped_product_completion_tool_before_runtime_owned_execution()
    {
        const string malformedToolName = "workspace_python_run_file!secret=/home/private/raw-runtime-token";
        var outputRoot = CreateTempProductRoot();
        var (baseAssignment, agent) = CreateRuntimeOwnedProductAssignment(outputRoot);
        var assignment = baseAssignment with
        {
            LaunchVariables = WithLaunchVariables(
                baseAssignment,
                (ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts,
                    JsonSerializer.Serialize(new object[]
                    {
                        new
                        {
                            toolReceipt = malformedToolName,
                            applicableBranchOutcomeKeys = new[] { "quality-accepted" }
                        }
                    })))
        };
        var runtimeExecutor = new FakeRuntimeOwnedStepExecutor(result: null);
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The agent must not run for a malformed runtime-owned contract."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(
                    assignment.RunId,
                    assignment.RunId,
                    ProcessRuntimeStatus.Active)),
                workspaceFiles,
                runtimeToolPreflightService: new ProcessRuntimeToolPreflightService(
                    [],
                    [],
                    ProcessRuntimeToolPreflightContributionCatalog.Empty),
                runtimeOwnedStepExecutors: [runtimeExecutor]);

            var result = await adapter.ExecuteAsync(CreateAdapterRequest(
                assignment,
                ProcessExecutionAdapterKind.Workflow,
                new ProcessExecutionAdapterOperationKey("execute"),
                Binding,
                [],
                []));

            var publicReceipt = JsonSerializer.Serialize(result);
            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Equal(0, runtimeExecutor.CallCount);
            Assert.False(workspace.ExecuteRunCalled);
            Assert.DoesNotContain(malformedToolName, publicReceipt, StringComparison.Ordinal);
            Assert.DoesNotContain("raw-runtime-token", publicReceipt, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(outputRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_rejects_oversized_runtime_tool_contract_before_mapped_subprocess_launch()
    {
        var parentRunId = ProcessRunId.New();
        var agent = NewAgent(
            ".NET Application Developer",
            ".NET implementation specialist",
            AgentWorkloadKind.Programming,
            ["software-engineer", "dotnet"],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateSubprocessAssignment() with
        {
            RunId = parentRunId,
            ExecutorId = agent.Id.ToString("D")
        };
        var launchCoordinator = new FakeSubprocessLaunchCoordinator(
            new ProcessSubprocessLaunchCoordinatorResult(
                "dotnet-development-slice",
                ProcessRunId.New(),
                ProcessLaunchStage.Running.ToString(),
                "{}",
                [],
                []));
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The parent agent must not run for an invalid subprocess contract."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(
                    parentRunId,
                    parentRunId,
                    ProcessRuntimeStatus.Active)),
                workspaceFiles,
                [launchCoordinator],
                runtimeToolPreflightService: new ProcessRuntimeToolPreflightService(
                    [],
                    [],
                    ProcessRuntimeToolPreflightContributionCatalog.Empty));
            var requiredTools = Enumerable.Range(0, 65)
                .Select(index => $"runtime_tool_{index:D2}")
                .ToArray();

            var result = await adapter.ExecuteAsync(CreateAdapterRequest(
                assignment,
                ProcessExecutionAdapterKind.Workflow,
                new ProcessExecutionAdapterOperationKey("execute"),
                Binding,
                [],
                [],
                new ProcessStepExecutionContract(
                    [],
                    [],
                    requiredTools,
                    "sha256:oversized-subprocess-tools")));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.False(launchCoordinator.Called);
            Assert.False(workspace.ExecuteRunCalled);
            Assert.Equal(
                "process.adapter.runtime_tool_preflight_failed",
                Assert.Single(result.Diagnostics).Code.Value);
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
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active)),
                workspaceFiles,
                [launchCoordinator]);

            var exception = await Assert.ThrowsAsync<ProcessRuntimeDispatchDeferredException>(() =>
                adapter.ExecuteAsync(
                    CreateAdapterRequest(
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
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
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
    public async Task ExecuteAsync_reports_subprocess_launch_exception_as_non_retryable_manager_issue_before_invoking_parent_agent()
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
        var launchCoordinator = new ThrowingSubprocessLaunchCoordinator(
            new InvalidOperationException("Declared child launch contract cannot be prepared."));
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The parent agent should not be invoked after a subprocess launch fault."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);

        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active)),
                workspaceFiles,
                [launchCoordinator]);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    parentRunId,
                    assignment.StepInstanceId,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.True(launchCoordinator.Called);
            Assert.False(workspace.ExecuteRunCalled);
            Assert.Contains(
                result.Diagnostics,
                diagnostic =>
                    diagnostic.Code.Value == "process.adapter.subprocess_launch_failed" &&
                    diagnostic.RetrySafety == ProcessDiagnosticRetrySafety.UnsafeToRetry &&
                    diagnostic.Idempotency == ProcessDiagnosticIdempotencyClassification.Unknown);
            Assert.Contains(
                result.ManagerSignals,
                signal => signal.Code.Value == "process.adapter.subprocess_launch_failed");
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_converts_pending_child_reconciliation_failure_to_manager_issue()
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
        var launchCoordinator = new ThrowingSubprocessLaunchCoordinator(
            new InvalidOperationException("Declared child launch contract cannot be prepared."));
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The parent agent should not be invoked after a subprocess launch fault."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);

        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new ThrowingRuntimeStateStore(new InvalidOperationException("Pending child state lookup failed.")),
                workspaceFiles,
                [launchCoordinator],
                parentSubprocessArtifactBridge: new NoMatchingChildSubprocessArtifactBridge());

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    parentRunId,
                    assignment.StepInstanceId,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.True(launchCoordinator.Called);
            Assert.False(workspace.ExecuteRunCalled);
            Assert.Contains(
                result.Diagnostics,
                diagnostic =>
                    diagnostic.Code.Value == "process.adapter.subprocess_launch_failed" &&
                    diagnostic.RetrySafety == ProcessDiagnosticRetrySafety.UnsafeToRetry);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_completes_subprocess_parent_with_hash_verified_child_payload(
        bool routedNoGo)
    {
        const string forwardedRuntimeProjectPath = "src/TetrisGame.Client/TetrisGame.Client.csproj";
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var forwardedSlotId = ArtifactSlotId.New();
        var forwardedArtifactId = ArtifactInstanceId.New();
        var forwardedInternalRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/architecture-intake.md";
        var forwardedContent = string.Join(
            Environment.NewLine,
            "## Runtime project",
            string.Empty,
            string.Empty,
            $"Project path: `{forwardedRuntimeProjectPath}`",
            $"Child context ref: `{forwardedInternalRef}`");
        var agent = NewAgent(
            ".NET Application Developer",
            ".NET implementation specialist",
            AgentWorkloadKind.Programming,
            [
                "software-engineer",
                "dotnet"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var baseAssignment = CreateSubprocessAssignment();
        Assert.True(ProcessRuntimeLaunchVariables.TryReadProcessStepSubprocessContract(
            baseAssignment.LaunchVariables,
            out var subprocessContract));
        subprocessContract.ForwardedChildContextArtifacts =
        [
            new ProcessSubprocessForwardedChildContextArtifactContract
            {
                BindingKey = "runtime-project",
                SourceStepKey = "architecture",
                ArtifactExpectationKey = "runtime-project",
                PayloadSchema = "runtime-project/v1"
            }
        ];
        if (routedNoGo)
        {
            subprocessContract.AcceptedChildOutputs = subprocessContract.AcceptedChildOutputs
                .Where(output => !string.Equals(
                    output.StepKey,
                    "slice-handoff",
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            subprocessContract.NoGoChildOutputs =
            [
                new ProcessSubprocessChildOutputContract
                {
                    StepKey = "slice-handoff",
                    ArtifactExpectationKey = "slice-handoff-packet",
                    ArtifactTitle = "Implementation slice no-go packet",
                    ParentBranchOutcomeKey = "implementation-needs-manager-repair"
                }
            ];
        }

        var assignment = baseAssignment with
        {
            RunId = parentRunId,
            ExecutorId = agent.Id.ToString("D"),
            LaunchVariables = WithLaunchVariables(
                baseAssignment,
                (
                    ProcessRuntimeLaunchVariables.ProcessStepSubprocessContractJson,
                    ProcessRuntimeLaunchVariables.SerializeProcessStepSubprocessContract(subprocessContract)))
        };
        var childArtifactSlotId = ArtifactSlotId.New();
        var childAssignment = CreateChildAssignment(
            childRunId,
            parentRunId,
            assignment.StepInstanceId,
            assignment.StepKey) with
        {
            StepKey = "slice-handoff",
            ProducedArtifactSlotIds = [childArtifactSlotId]
        };
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        var childEvidenceRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/{childAssignment.StepKey}.md";
        var opaquePayload = routedNoGo
            ? "Opaque routed no-go child payload retained by the parent projection."
            : "Opaque accepted child payload retained by the parent projection.";
        var childEvidenceContent = $"""
            {ProcessManagedArtifactService.ManagedOutcomeArtifactAcceptedHeading}
            Status: Completed

            {opaquePayload}
            SourceDocLink: managed-files/project-media/child-proof.md
            """;
        var forwardedRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/architecture.md";
        var writeChildEvidence = workspaceFiles.WriteTextFile(
            childEvidenceRef,
            childEvidenceContent,
            overwrite: true);
        Assert.True(writeChildEvidence.Succeeded);
        var writeForwardedContext = workspaceFiles.WriteTextFile(
            forwardedRef,
            forwardedContent,
            overwrite: true);
        Assert.True(writeForwardedContext.Succeeded);
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The parent agent should not be reinvoked for a completed child run."));
        var childState = NewRuntimeState(
            parentRunId,
            childRunId,
            ProcessRuntimeStatus.Completed,
            childAssignment,
            ProcessRuntimeStepStatus.Completed,
            [CreateProducedArtifactReceipt(
                childAssignment,
                childArtifactSlotId,
                childEvidenceContent)]);
        var childStepState = Assert.Single(childState.Steps);
        childState = childState with
        {
            Steps =
            [
                childStepState with
                {
                    RequiredArtifactSlots = new HashSet<ArtifactSlotId> { forwardedSlotId },
                    ArtifactDescriptors =
                    [
                        new(
                            childArtifactSlotId,
                            "slice-handoff",
                            childAssignment.StepKey,
                            "slice-handoff-packet",
                            "Implementation slice handoff packet",
                            "markdown",
                            childEvidenceRef,
                            ProcessArtifactMaterializationMode.AgentWritten),
                        new(
                            forwardedSlotId,
                            "architecture:runtime-project",
                            "architecture",
                            "runtime-project",
                            "Runtime project",
                            "ManagedMarkdown",
                            forwardedRef,
                            ProcessArtifactMaterializationMode.AgentWritten)
                    ]
                }
            ],
            ConnectedInputArtifacts =
            [
                new ProcessRuntimeInputArtifactReceipt(
                    childAssignment.StepInstanceId,
                    forwardedSlotId,
                    ProcessArtifactInputAvailability.Available,
                    ProcessStepInstanceId.New(),
                    forwardedArtifactId,
                    ComputeContentHash(forwardedContent),
                    "sha256:runtime-project-connection")
            ]
        };
        var adapter = CreateAdapter(
            new FakeWorkspaceFactory(workspace),
            CreateReferenceDataProvider(workspace),
            new InMemoryAssignmentStore(assignment, childAssignment),
            new InMemoryRuntimeStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                childState),
            workspaceFiles);

        try
        {
            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    parentRunId,
                    assignment.StepInstanceId,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    [],
                    routedNoGo
                        ? BranchStepContract("implementation-needs-manager-repair")
                        : ProcessStepExecutionContract.Empty));

            Assert.True(result.Outcome == StrategyOutcome.Succeeded, DescribeResult(result));
            Assert.False(workspace.ExecuteRunCalled);
            Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            Assert.DoesNotContain(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.ungrounded_outcome_reference");
            Assert.DoesNotContain(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.ungrounded_managed_artifact_reference");
            var parentArtifact = workspaceFiles.ReadTextFile(BuildStepArtifactRef(assignment));
            Assert.True(parentArtifact.Succeeded);
            Assert.Contains(childRunId.Value.ToString("D"), parentArtifact.Content, StringComparison.Ordinal);
            Assert.Contains(childEvidenceRef, parentArtifact.Content, StringComparison.Ordinal);
            Assert.Contains(opaquePayload, parentArtifact.Content, StringComparison.Ordinal);
            Assert.Contains(
                "SourceDocLink: managed-files/project-media/child-proof.md",
                parentArtifact.Content,
                StringComparison.Ordinal);
            if (routedNoGo)
            {
                Assert.Contains(
                    result.ManagerSignals,
                    signal => signal.Code.Value.Contains(
                        "implementation-needs-manager-repair",
                        StringComparison.OrdinalIgnoreCase));
                Assert.Contains(
                    "Child output disposition: `no-go`",
                    parentArtifact.Content,
                    StringComparison.Ordinal);
            }
            Assert.Contains(forwardedRuntimeProjectPath, parentArtifact.Content, StringComparison.Ordinal);
            Assert.Contains(forwardedInternalRef, parentArtifact.Content, StringComparison.Ordinal);
            Assert.Equal(
                1,
                parentArtifact.Content.Split(
                    ParentSubprocessForwardedContextEnvelope.BeginMarker,
                    StringSplitOptions.None).Length - 1);
            Assert.Equal(
                1,
                parentArtifact.Content.Split(
                    ParentSubprocessForwardedContextEnvelope.EndMarker,
                    StringSplitOptions.None).Length - 1);
            Assert.Equal(
                1,
                parentArtifact.Content.Split(
                    ParentSubprocessVerifiedChildOutputEnvelope.BeginMarker,
                    StringSplitOptions.None).Length - 1);
            Assert.Equal(
                1,
                parentArtifact.Content.Split(
                    ParentSubprocessVerifiedChildOutputEnvelope.EndMarker,
                    StringSplitOptions.None).Length - 1);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_completes_subprocess_parent_when_authenticated_visual_defect_payload_contains_blocker_text()
    {
        const string branchOutcomeKey = "visual-defect-observed";
        const string childPayload =
            "Accepted image asset node ids: none. No current-run screenshot was accepted as target-aligned visual proof.";
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
        var baseAssignment = CreateSubprocessAssignment();
        Assert.True(ProcessRuntimeLaunchVariables.TryReadProcessStepSubprocessContract(
            baseAssignment.LaunchVariables,
            out var subprocessContract));
        subprocessContract.AcceptedChildOutputs =
        [
            new ProcessSubprocessChildOutputContract
            {
                StepKey = "slice-handoff",
                ArtifactExpectationKey = "slice-handoff-packet",
                ArtifactTitle = "Observed visual evidence handoff",
                BranchOutcomeKey = branchOutcomeKey
            }
        ];
        var assignment = baseAssignment with
        {
            RunId = parentRunId,
            ExecutorId = agent.Id.ToString("D"),
            LaunchVariables = WithLaunchVariables(
                baseAssignment,
                (
                    ProcessRuntimeLaunchVariables.ProcessStepSubprocessContractJson,
                    ProcessRuntimeLaunchVariables.SerializeProcessStepSubprocessContract(subprocessContract)))
        };
        var childArtifactSlotId = ArtifactSlotId.New();
        var childAssignment = CreateChildAssignment(
            childRunId,
            parentRunId,
            assignment.StepInstanceId,
            assignment.StepKey) with
        {
            StepKey = "slice-handoff",
            ProducedArtifactSlotIds = [childArtifactSlotId]
        };
        var childEvidenceRef = $"artifacts/process-runs/{childRunId.Value:D}/steps/{childAssignment.StepKey}.md";
        var childEvidenceContent = $"""
            {ProcessManagedArtifactService.ManagedOutcomeArtifactAcceptedHeading}
            Status: Completed
            Branch outcome key: {branchOutcomeKey}

            {childPayload}
            """;
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        var writeChildEvidence = workspaceFiles.WriteTextFile(
            childEvidenceRef,
            childEvidenceContent,
            overwrite: true);
        Assert.True(writeChildEvidence.Succeeded);
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The parent agent should not be reinvoked for an accepted child output."));
        var childState = NewRuntimeState(
            parentRunId,
            childRunId,
            ProcessRuntimeStatus.Completed,
            childAssignment,
            ProcessRuntimeStepStatus.Completed,
            [CreateProducedArtifactReceipt(
                childAssignment,
                childArtifactSlotId,
                childEvidenceContent)]);
        var childStepState = Assert.Single(childState.Steps);
        childState = childState with
        {
            Steps =
            [
                childStepState with
                {
                    ArtifactDescriptors =
                    [
                        new(
                            childArtifactSlotId,
                            "slice-handoff",
                            childAssignment.StepKey,
                            "slice-handoff-packet",
                            "Observed visual evidence handoff",
                            "markdown",
                            childEvidenceRef,
                            ProcessArtifactMaterializationMode.AgentWritten)
                    ]
                }
            ]
        };
        var adapter = CreateAdapter(
            new FakeWorkspaceFactory(workspace),
            CreateReferenceDataProvider(workspace),
            new InMemoryAssignmentStore(assignment, childAssignment),
            new InMemoryRuntimeStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                childState),
            workspaceFiles);

        try
        {
            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    parentRunId,
                    assignment.StepInstanceId,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.True(
                result.Outcome == StrategyOutcome.Succeeded,
                $"{result.UserSafeSummary}{Environment.NewLine}{string.Join(
                    Environment.NewLine,
                    result.Diagnostics.Select(diagnostic =>
                        $"{diagnostic.Code.Value}: {diagnostic.SafeSummary}"))}");
            Assert.False(workspace.ExecuteRunCalled);
            Assert.DoesNotContain(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.completed_outcome_declares_unresolved_blocker");
            Assert.Contains(
                result.ProducedArtifacts,
                artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            var parentArtifact = workspaceFiles.ReadTextFile(BuildStepArtifactRef(assignment));
            Assert.True(parentArtifact.Succeeded);
            Assert.Contains(childPayload, parentArtifact.Content, StringComparison.Ordinal);
            Assert.Contains(
                ParentSubprocessVerifiedChildOutputEnvelope.BeginMarker,
                parentArtifact.Content,
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Managed_artifact_grounding_and_hashing_reject_truncated_real_workspace_reads()
    {
        var assignment = CreateSubprocessAssignment();
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        var primaryRef = BuildStepArtifactRef(assignment);
        var writeResult = workspaceFiles.WriteTextFile(
            primaryRef,
            new string('x', WorkspaceFileLimits.MaxTextReadCharacters + 1),
            overwrite: true);
        Assert.True(writeResult.Succeeded);
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Completed.",
            EvidenceRefs = [primaryRef],
            NextActions = []
        };
        var managedArtifactService = new ProcessManagedArtifactService(workspaceFiles);
        var groundingValidator = new ProcessOutcomeGroundingValidator(workspaceFiles);

        try
        {
            var hashes = managedArtifactService.BuildProducedArtifactContentHashes(
                assignment,
                output,
                out var hashIssue);
            var groundingIssue = groundingValidator.ValidateManagedArtifactBodyReferences(
                assignment,
                output,
                []);

            Assert.Empty(hashes);
            Assert.NotNull(hashIssue);
            Assert.Equal("process.adapter.managed_artifact_readback_failed", hashIssue.Code);
            Assert.Contains("complete-read limit", hashIssue.Summary, StringComparison.Ordinal);
            Assert.NotNull(groundingIssue);
            Assert.Equal("process.adapter.managed_artifact_readback_failed", groundingIssue.Code);
            Assert.Contains("complete-read limit", groundingIssue.Summary, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_reports_blocked_subprocess_child_root_cause_without_reinvoking_parent_agent()
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
            assignment.StepKey) with
        {
            StepKey = "slice-handoff"
        };
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The parent agent should not be reinvoked for a blocked child run."));
        var adapter = CreateAdapter(
            new FakeWorkspaceFactory(workspace),
            CreateReferenceDataProvider(workspace),
            new InMemoryAssignmentStore(assignment, childAssignment),
            new InMemoryRuntimeStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                NewRuntimeState(
                    parentRunId,
                    childRunId,
                    ProcessRuntimeStatus.Blocked,
                    childAssignment,
                    ProcessRuntimeStepStatus.Blocked,
                    [CreateBlockedChildDiagnosticReceipt(childAssignment)])),
            workspaceFiles);

        try
        {
            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    parentRunId,
                    assignment.StepInstanceId,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.False(workspace.ExecuteRunCalled);
            var diagnostic = Assert.Single(
                result.Diagnostics,
                diagnostic => diagnostic.Code.Value == "process.adapter.subprocess_child_blocked");
            Assert.Contains(childRunId.Value.ToString("D"), diagnostic.SafeSummary, StringComparison.Ordinal);
            Assert.Contains(childAssignment.StepKey, diagnostic.SafeSummary, StringComparison.Ordinal);
            Assert.Contains("process.adapter.product_required_file_content_missing", diagnostic.SafeSummary, StringComparison.Ordinal);
            Assert.Contains("workspace_pwsh_run_script", diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(childRunId, diagnostic.RelatedChildRunId);
            Assert.Empty(result.ProducedArtifacts);
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
        var adapter = CreateAdapter(
            new FakeWorkspaceFactory(workspace),
            CreateReferenceDataProvider(workspace),
            assignmentStore,
            new InMemoryRuntimeStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                NewRuntimeState(parentRunId, childRunId, ProcessRuntimeStatus.Active)),
            CreateWorkspaceFileService(out _));

        var exception = await Assert.ThrowsAsync<ProcessRuntimeDispatchDeferredException>(() =>
            adapter.ExecuteAsync(
                CreateAdapterRequest(
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
        var adapter = CreateAdapter(
            new FakeWorkspaceFactory(workspace),
            CreateReferenceDataProvider(workspace),
            new InMemoryAssignmentStore(assignment),
            new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
            CreateWorkspaceFileService(out _));

        var result = await adapter.ExecuteAsync(
            CreateAdapterRequest(
                assignment,
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
        const string secretSentinel = "secret: C:\\private\\host\\api-key=raw-provider-token";
        var responseText = $"The agent run failed while using provider 'OpenAI default'. Provider detail: Service request failed. Status: 520. {secretSentinel}{new string('x', 5000)}";
        var workspace = new ThrowingWorkspaceService(
            agent,
            executeException: null,
            executeResult: CreateExecutionRunResult(agent.Id, executionRunId, responseText, RunOutcome.Failed));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
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
            Assert.Contains("restricted execution logs", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Status: 520", result.UserSafeSummary, StringComparison.Ordinal);
            Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.agent_transient_execution_retry");
            Assert.Contains(result.RequestedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            Assert.True(workspace.ExecuteRunCalled);
            Assert.Equal([executionRunId], workspace.ExecutionDetailRequestIds);
            AssertPublicAndPersistedReceiptExclude(
                assignment,
                result,
                secretSentinel,
                "raw-provider-token",
                @"C:\private\host");
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
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
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
            Assert.DoesNotContain("Initialization timed out", result.UserSafeSummary, StringComparison.Ordinal);
            Assert.Contains("restricted execution logs", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.agent_transient_execution_retry");
            Assert.Contains(result.RequestedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
            Assert.True(workspace.ExecuteRunCalled);
            Assert.Equal([executionRunId], workspace.ExecutionDetailRequestIds);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_attests_transient_failure_results_before_side_effects_with_stable_evidence()
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
        var responseText = "The agent run failed while using provider 'OpenAI default'. Provider detail: Service request failed. Status: 520 (<none>)";
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            async Task<(ProcessExecutionAdapterResult Result, ThrowingWorkspaceService Workspace)> ExecuteFailureAsync(
                Guid executionRunId)
            {
                var workspace = new ThrowingWorkspaceService(
                    agent,
                    executeException: null,
                    executeResult: CreateExecutionRunResult(
                        agent.Id,
                        executionRunId,
                        responseText,
                        RunOutcome.Failed),
                    executionDetail: CreateFailedExecutionRunDetail(
                        assignment,
                        executionRunId,
                        responseText));
                var adapter = CreateAdapter(
                    new FakeWorkspaceFactory(workspace),
                    CreateReferenceDataProvider(workspace),
                    new InMemoryAssignmentStore(assignment),
                    new InMemoryRuntimeStateStore(NewRuntimeState(
                        assignment.RunId,
                        assignment.RunId,
                        ProcessRuntimeStatus.Active)),
                    workspaceFiles);
                var result = await adapter.ExecuteAsync(
                    CreateAdapterRequest(
                        assignment,
                        ProcessExecutionAdapterKind.Workflow,
                        new ProcessExecutionAdapterOperationKey("execute"),
                        Binding,
                        [],
                        []));
                return (result, workspace);
            }

            var firstExecutionRunId = Guid.NewGuid();
            var secondExecutionRunId = Guid.NewGuid();
            var first = await ExecuteFailureAsync(firstExecutionRunId);
            var second = await ExecuteFailureAsync(secondExecutionRunId);
            var firstDiagnostic = Assert.Single(first.Result.Diagnostics);
            var secondDiagnostic = Assert.Single(second.Result.Diagnostics);

            Assert.Equal(StrategyOutcome.NeedsManager, first.Result.Outcome);
            Assert.Equal(
                ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionBeforeSideEffects,
                firstDiagnostic.Code.Value);
            Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, firstDiagnostic.RetrySafety);
            Assert.Equal(ProcessDiagnosticIdempotencyClassification.Idempotent, firstDiagnostic.Idempotency);
            Assert.NotEqual(firstDiagnostic.EvidenceHash, secondDiagnostic.EvidenceHash);
            Assert.NotEqual(first.Result.ResultHash, second.Result.ResultHash);
            var firstAttestation = Assert.IsType<ProcessExecutionSafetyAttestation>(
                firstDiagnostic.ExecutionSafetyAttestation);
            var secondAttestation = Assert.IsType<ProcessExecutionSafetyAttestation>(
                secondDiagnostic.ExecutionSafetyAttestation);
            Assert.True(firstAttestation.IsStructurallyValid());
            Assert.True(secondAttestation.IsStructurallyValid());
            Assert.Equal(
                ProcessExecutionSafetyAttestationKind.FailedBeforeRecordedSideEffects,
                firstAttestation.Kind);
            Assert.Equal(
                ProcessExecutionSafetyAttestor.AgentFrameworkExecutionLedger,
                firstAttestation.Attestor);
            Assert.Equal(
                ProcessExecutionSafetyAttestation.CurrentSchemaVersion,
                firstAttestation.SchemaVersion);
            Assert.Equal(firstExecutionRunId, firstAttestation.ExecutionRunId.Value);
            Assert.Equal(secondExecutionRunId, secondAttestation.ExecutionRunId.Value);
            Assert.Equal(firstAttestation.ExecutionRunId, first.Result.ExecutionRunId);
            Assert.Equal(secondAttestation.ExecutionRunId, second.Result.ExecutionRunId);
            Assert.Equal(assignment.RunId, firstAttestation.ProcessRunId);
            Assert.Equal(assignment.StepInstanceId, firstAttestation.StepInstanceId);
            Assert.Equal(agent.Id, firstAttestation.ExecutorId.Value);
            Assert.NotEqual(firstAttestation.DurableEvidenceDigest, secondAttestation.DurableEvidenceDigest);
            Assert.NotEqual(firstAttestation.EvidenceHash, secondAttestation.EvidenceHash);
            Assert.Contains(firstExecutionRunId.ToString("D"), first.Result.UserSafeSummary, StringComparison.Ordinal);
            Assert.Contains(secondExecutionRunId.ToString("D"), second.Result.UserSafeSummary, StringComparison.Ordinal);
            Assert.Contains("metricToolCalls=0", first.Result.UserSafeSummary, StringComparison.Ordinal);
            Assert.Contains("toolReceipts=0", first.Result.UserSafeSummary, StringComparison.Ordinal);
            Assert.Equal([firstExecutionRunId], first.Workspace.ExecutionDetailRequestIds);
            Assert.Equal([secondExecutionRunId], second.Workspace.ExecutionDetailRequestIds);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_attests_transient_agent_run_exception_before_side_effects()
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
        var workspace = new ThrowingWorkspaceService(
            agent,
            exception,
            executionDetail: CreateFailedExecutionRunDetail(
                assignment,
                executionRunId,
                exception.Message));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(
                    assignment.RunId,
                    assignment.RunId,
                    ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(
                ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionBeforeSideEffects,
                diagnostic.Code.Value);
            Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
            Assert.Equal(ProcessDiagnosticIdempotencyClassification.Idempotent, diagnostic.Idempotency);
            var attestation = Assert.IsType<ProcessExecutionSafetyAttestation>(
                diagnostic.ExecutionSafetyAttestation);
            Assert.True(attestation.IsStructurallyValid());
            Assert.Equal(executionRunId, attestation.ExecutionRunId.Value);
            Assert.Equal(attestation.ExecutionRunId, result.ExecutionRunId);
            Assert.Equal(assignment.RunId, attestation.ProcessRunId);
            Assert.Equal(assignment.StepInstanceId, attestation.StepInstanceId);
            Assert.Equal(agent.Id, attestation.ExecutorId.Value);
            Assert.Contains(executionRunId.ToString("D"), result.UserSafeSummary, StringComparison.Ordinal);
            Assert.DoesNotContain("Initialization timed out", result.UserSafeSummary, StringComparison.Ordinal);
            Assert.Contains("restricted execution logs", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Equal([executionRunId], workspace.ExecutionDetailRequestIds);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_omits_oversized_exception_detail_from_public_and_persisted_receipts()
    {
        const string secretSentinel = "secret: C:\\private\\host\\password=raw-exception-token";
        var agent = NewAgent(
            ".NET Solution Architect",
            "Solution architect",
            AgentWorkloadKind.Programming,
            ["solution-architect", "dotnet"],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateManagedArtifactAssignment("exception-non-disclosure", agent.Id);
        var exception = new InvalidOperationException(secretSentinel + new string('x', 10000));
        var workspace = new ThrowingWorkspaceService(agent, exception);
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(
                    assignment.RunId,
                    assignment.RunId,
                    ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(CreateAdapterRequest(
                assignment,
                ProcessExecutionAdapterKind.Workflow,
                new ProcessExecutionAdapterOperationKey("execute"),
                Binding,
                [],
                []));

            Assert.Equal(StrategyOutcome.Failed, result.Outcome);
            Assert.Contains("restricted execution log", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.True(result.UserSafeSummary.Length < 800);
            Assert.All(result.Diagnostics, diagnostic => Assert.True(diagnostic.SafeSummary.Length < 800));
            AssertPublicAndPersistedReceiptExclude(
                assignment,
                result,
                secretSentinel,
                "raw-exception-token",
                @"C:\private\host");
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Theory]
    [InlineData(AgentTransientDurableSignal.RunNotFailed)]
    [InlineData(AgentTransientDurableSignal.WrongProcessIdentity)]
    [InlineData(AgentTransientDurableSignal.MissingTerminalMetric)]
    [InlineData(AgentTransientDurableSignal.MissingTerminalLog)]
    [InlineData(AgentTransientDurableSignal.ImmediateMetricToolCall)]
    [InlineData(AgentTransientDurableSignal.MetricToolCall)]
    [InlineData(AgentTransientDurableSignal.UsageToolCall)]
    [InlineData(AgentTransientDurableSignal.ToolReceipt)]
    [InlineData(AgentTransientDurableSignal.Artifact)]
    [InlineData(AgentTransientDurableSignal.Checkpoint)]
    [InlineData(AgentTransientDurableSignal.Approval)]
    [InlineData(AgentTransientDurableSignal.RunPendingApproval)]
    [InlineData(AgentTransientDurableSignal.StructuredOutput)]
    [InlineData(AgentTransientDurableSignal.StructuredOutputValidation)]
    [InlineData(AgentTransientDurableSignal.SerializedSessionState)]
    [InlineData(AgentTransientDurableSignal.RuntimeSessionKey)]
    [InlineData(AgentTransientDurableSignal.WaitingOnToolLog)]
    [InlineData(AgentTransientDurableSignal.FailedThenRunningLog)]
    [InlineData(AgentTransientDurableSignal.LogAfterRunCompletion)]
    public async Task ExecuteAsync_keeps_legacy_transient_diagnostic_when_durable_attestation_is_not_exact(
        AgentTransientDurableSignal signal)
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
        var executeResult = CreateExecutionRunResult(
            agent.Id,
            executionRunId,
            responseText,
            RunOutcome.Failed);
        if (signal == AgentTransientDurableSignal.ImmediateMetricToolCall)
        {
            executeResult = executeResult with
            {
                Metric = executeResult.Metric with
                {
                    ToolCalls = 1
                }
            };
        }

        var workspace = new ThrowingWorkspaceService(
            agent,
            executeException: null,
            executeResult: executeResult,
            executionDetail: CreateFailedExecutionRunDetail(
                assignment,
                executionRunId,
                responseText,
                signal));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(
                    assignment.RunId,
                    assignment.RunId,
                    ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionRetry, diagnostic.Code.Value);
            Assert.Null(diagnostic.ExecutionSafetyAttestation);
            Assert.DoesNotContain(
                result.Diagnostics,
                candidate => candidate.Code.Value ==
                    ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionBeforeSideEffects);
            Assert.Contains("did not prove", result.UserSafeSummary, StringComparison.Ordinal);
            Assert.Equal([executionRunId], workspace.ExecutionDetailRequestIds);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_blocks_missing_runtime_tool_preflight_before_invoking_agent()
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
        var assignment = CreateManagedArtifactAssignment("runtime-tool-preflight", agent.Id);
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The agent must not run when required runtime tools are missing."));
        var hostEvidence = new ProcessHostCapabilityEvaluationEvidence(
            new ProcessHostProfileId("linux-dispatch"),
            [
                new ProcessHostCapabilityFact(
                    ProcessHostCapabilityIds.DotNetRuntime,
                    ProcessHostCapabilityAvailability.Available,
                    ProcessHostCapabilityReason.Ready,
                    ProcessHostExecutionPort.ManagedProcessHost)
            ]);
        var preflight = new FakeRuntimeToolPreflightService(
            new ProcessRuntimeToolPreflightResult(
                false,
                ["workspace_dotnet_build"],
                "Required runtime tool(s) are not composed for this process step: workspace_dotnet_build.")
            {
                HostCapabilityEvidence = hostEvidence
            },
            ProcessRuntimeToolPreflightResult.Satisfied with
            {
                HostCapabilityEvidence = hostEvidence
            });
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles,
                runtimeToolPreflightService: preflight);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    [],
                    new ProcessStepExecutionContract(
                        RequiredArtifacts: [],
                        ExpectedProducedArtifacts: [],
                        RequiredRuntimeToolNames: ["workspace_dotnet_build"],
                        ContractHash: "sha256:runtime-tool-preflight")));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.False(workspace.ExecuteRunCalled);
            Assert.True(preflight.Requests.Count == 1, DescribeResult(result));
            var request = preflight.Requests[0];
            Assert.Contains("workspace_dotnet_build", request.RequiredRuntimeToolNames);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("process.adapter.runtime_tool_preflight_failed", diagnostic.Code.Value);
            Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
            Assert.Equal(ProcessDiagnosticIdempotencyClassification.Idempotent, diagnostic.Idempotency);
            Assert.Contains("workspace_dotnet_build", result.UserSafeSummary, StringComparison.Ordinal);
            Assert.NotNull(result.HostCapabilityEvidence);
            Assert.Equal(hostEvidence.ProfileId, result.HostCapabilityEvidence.ProfileId);
            Assert.Equal(
                hostEvidence.Capabilities.ToArray(),
                result.HostCapabilityEvidence.Capabilities.ToArray());
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_preserves_explicit_and_runtime_tool_host_facts_in_failure_receipt()
    {
        var agent = NewAgent(
            ".NET Solution Architect",
            "Solution architect",
            AgentWorkloadKind.Programming,
            ["solution-architect", "python"],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateManagedArtifactAssignment("combined-host-evidence", agent.Id);
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The agent must not run when runtime tool composition fails."));
        var profileId = new ProcessHostProfileId("linux-dispatch");
        var pythonFact = new ProcessHostCapabilityFact(
            ProcessHostCapabilityIds.PythonRuntime,
            ProcessHostCapabilityAvailability.Available,
            ProcessHostCapabilityReason.Ready,
            ProcessHostExecutionPort.ManagedProcessHost);
        var dockerFact = new ProcessHostCapabilityFact(
            ProcessHostCapabilityIds.Docker,
            ProcessHostCapabilityAvailability.Available,
            ProcessHostCapabilityReason.Ready,
            ProcessHostExecutionPort.DockerHostTool);
        var gateEvidence = new ProcessHostCapabilityEvaluationEvidence(
            profileId,
            [dockerFact, pythonFact]);
        var runtimeEvidence = new ProcessHostCapabilityEvaluationEvidence(
            profileId,
            [pythonFact]);
        var preflight = new FakeRuntimeToolPreflightService(
            new ProcessRuntimeToolPreflightResult(
                false,
                [ToolContractCatalog.WorkspaceInspectSpreadsheet],
                "The required tool was not composed.")
            {
                HostCapabilityEvidence = runtimeEvidence
            },
            ProcessRuntimeToolPreflightResult.Satisfied with
            {
                HostCapabilityEvidence = gateEvidence
            });
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(
                    assignment.RunId,
                    assignment.RunId,
                    ProcessRuntimeStatus.Active)),
                workspaceFiles,
                runtimeToolPreflightService: preflight);
            var stepContract = new ProcessStepExecutionContract(
                [],
                [],
                [ToolContractCatalog.WorkspaceInspectSpreadsheet],
                "sha256:combined-host-evidence")
            {
                RequiredHostCapabilities = new HashSet<ProcessHostCapabilityId>
                {
                    ProcessHostCapabilityIds.Docker
                }
            };

            var result = await adapter.ExecuteAsync(CreateAdapterRequest(
                assignment,
                ProcessExecutionAdapterKind.Workflow,
                new ProcessExecutionAdapterOperationKey("execute"),
                Binding,
                [],
                [],
                stepContract));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.False(workspace.ExecuteRunCalled);
            Assert.NotNull(result.HostCapabilityEvidence);
            Assert.Equal(profileId, result.HostCapabilityEvidence.ProfileId);
            Assert.Equal(
                [ProcessHostCapabilityIds.Docker, ProcessHostCapabilityIds.PythonRuntime],
                result.HostCapabilityEvidence.Capabilities.Select(fact => fact.Id).ToArray());
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_rejects_incoherent_host_snapshots_before_agent_execution()
    {
        var agent = NewAgent(
            ".NET Solution Architect",
            "Solution architect",
            AgentWorkloadKind.Programming,
            ["solution-architect", "python"],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateManagedArtifactAssignment("unstable-host-evidence", agent.Id);
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The agent must not run with incoherent host facts."));
        var profileId = new ProcessHostProfileId("linux-dispatch");
        var availableEvidence = new ProcessHostCapabilityEvaluationEvidence(
            profileId,
            [
                new ProcessHostCapabilityFact(
                    ProcessHostCapabilityIds.PythonRuntime,
                    ProcessHostCapabilityAvailability.Available,
                    ProcessHostCapabilityReason.Ready,
                    ProcessHostExecutionPort.ManagedProcessHost)
            ]);
        var unavailableEvidence = new ProcessHostCapabilityEvaluationEvidence(
            profileId,
            [
                new ProcessHostCapabilityFact(
                    ProcessHostCapabilityIds.PythonRuntime,
                    ProcessHostCapabilityAvailability.Unavailable,
                    ProcessHostCapabilityReason.DependencyMissing,
                    ProcessHostExecutionPort.None)
            ]);
        var preflight = new FakeRuntimeToolPreflightService(
            ProcessRuntimeToolPreflightResult.Satisfied with
            {
                HostCapabilityEvidence = unavailableEvidence
            },
            ProcessRuntimeToolPreflightResult.Satisfied with
            {
                HostCapabilityEvidence = availableEvidence
            });
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(
                    assignment.RunId,
                    assignment.RunId,
                    ProcessRuntimeStatus.Active)),
                workspaceFiles,
                runtimeToolPreflightService: preflight);

            var result = await adapter.ExecuteAsync(CreateAdapterRequest(
                assignment,
                ProcessExecutionAdapterKind.Workflow,
                new ProcessExecutionAdapterOperationKey("execute"),
                Binding,
                [],
                [],
                new ProcessStepExecutionContract(
                    [],
                    [],
                    [ToolContractCatalog.WorkspaceInspectSpreadsheet],
                    "sha256:unstable-host-evidence")));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.False(workspace.ExecuteRunCalled);
            Assert.Contains("host-capability-snapshot-changed", result.UserSafeSummary, StringComparison.Ordinal);
            Assert.Equal("unstable", result.HostCapabilityEvidence?.ProfileId.Value);
            var fact = Assert.Single(result.HostCapabilityEvidence!.Capabilities);
            Assert.Equal(ProcessHostCapabilityAvailability.Unverified, fact.Availability);
            Assert.Equal(ProcessHostCapabilityReason.ProbePending, fact.Reason);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_blocks_spreadsheet_inspection_before_agent_when_python_is_unavailable()
    {
        var agent = NewAgent(
            ".NET Solution Architect",
            "Solution architect",
            AgentWorkloadKind.Programming,
            ["solution-architect", "dotnet", "architecture"],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateManagedArtifactAssignment("spreadsheet-preflight", agent.Id);
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The agent must not run when Python is unavailable."));
        var evidence = new ProcessHostCapabilityEvaluationEvidence(
            new ProcessHostProfileId("linux"),
            [
                new ProcessHostCapabilityFact(
                    ProcessHostCapabilityIds.PythonRuntime,
                    ProcessHostCapabilityAvailability.Unavailable,
                    ProcessHostCapabilityReason.DependencyMissing,
                    ProcessHostExecutionPort.None)
            ]);
        var hostPreflight = new ProcessRuntimeToolPreflightResult(
            false,
            [],
            "Python is unavailable for spreadsheet inspection.")
        {
            HostCapabilityFindings =
            [
                new ProcessRuntimeToolHostCapabilityFinding(
                    ToolContractCatalog.WorkspaceInspectSpreadsheet,
                    ProcessHostCapabilityIds.PythonRuntime,
                    ProcessHostCapabilityAvailability.Unavailable,
                    ProcessHostCapabilityReason.DependencyMissing,
                    new ProcessHostProfileId("linux"))
            ],
            HostCapabilityEvidence = evidence
        };
        var preflight = new FakeRuntimeToolPreflightService(
            ProcessRuntimeToolPreflightResult.Satisfied,
            hostPreflight);
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(
                    assignment.RunId,
                    assignment.RunId,
                    ProcessRuntimeStatus.Active)),
                workspaceFiles,
                runtimeToolPreflightService: preflight);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    [],
                    new ProcessStepExecutionContract(
                        RequiredArtifacts: [],
                        ExpectedProducedArtifacts: [],
                        RequiredRuntimeToolNames: [ToolContractCatalog.WorkspaceInspectSpreadsheet],
                        ContractHash: "sha256:spreadsheet-python-preflight")));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.False(workspace.ExecuteRunCalled);
            Assert.Empty(preflight.Requests);
            Assert.Equal(evidence, result.HostCapabilityEvidence);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_filters_product_receipt_predicates_before_runtime_tool_preflight()
    {
        var agent = NewAgent(
            ".NET Application Developer",
            ".NET developer",
            AgentWorkloadKind.Programming,
            [
                "dotnet",
                "developer",
                "implementation"
            ],
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            [
                new AgentCapabilityAssignment(
                    Guid.NewGuid(),
                    "workspace-pwsh-run-script",
                    CapabilityKind.Tool,
                    CapabilityProofStatus.Verified,
                    DateTimeOffset.UtcNow,
                    "PowerShell script tool is assigned.")
            ]);
        var outputRoot = CreateTempProductRoot();
        var assignment = CreateProductMutationAssignment(outputRoot) with
        {
            StepKey = "create-dotnet-project",
            RoleKey = "dotnet-developer",
            RoleResourceKey = "dotnet-developer",
            RoleDisplayName = ".NET developer",
            ExecutorId = agent.Id.ToString("D"),
            ExecutorDisplayName = agent.Name,
            LaunchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] =
                    JsonSerializer.Serialize(new[]
                    {
                        "template=blazorwasm",
                        "template=sln",
                        "workspace_pwsh_run_script"
                    })
            }
        };
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The agent must not run when required runtime tools are missing."));
        var preflight = new FakeRuntimeToolPreflightService(new ProcessRuntimeToolPreflightResult(
            false,
            ["workspace_pwsh_run_script"],
            "Required runtime tool(s) are not composed for this process step: workspace_pwsh_run_script."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles,
                runtimeToolPreflightService: preflight);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    [],
                    new ProcessStepExecutionContract(
                        RequiredArtifacts: [],
                        ExpectedProducedArtifacts: [],
                        RequiredRuntimeToolNames: ["workspace_pwsh_run_script"],
                        ContractHash: "sha256:runtime-tool-preflight-product-receipts")));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.False(workspace.ExecuteRunCalled);
            Assert.True(preflight.Requests.Count == 1, DescribeResult(result));
            var request = preflight.Requests[0];
            Assert.Equal(["workspace_pwsh_run_script"], request.RequiredRuntimeToolNames);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("process.adapter.runtime_tool_preflight_failed", diagnostic.Code.Value);
            Assert.DoesNotContain("template=", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(outputRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_excludes_branch_scoped_product_receipt_tools_from_preflight_before_branch_selection()
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
        var receiptRules = JsonSerializer.Serialize(new Dictionary<string, object[]>(StringComparer.Ordinal)
        {
            ["qa-validation"] =
            [
                BranchAwareReceipt("browser_take_screenshot", "quality-accepted")
            ]
        });
        var baseAssignment = CreateManagedArtifactAssignment("qa-validation", agent.Id);
        var assignment = baseAssignment with
        {
            LaunchVariables = WithLaunchVariables(
                baseAssignment,
                (ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep, receiptRules)),
            CapabilityScope = new ProcessCapabilityScope
            {
                RequiredReceipts =
                [
                    CapabilityReceipt("browser_take_screenshot", "quality-accepted")
                ]
            }
        };
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The agent must not run when required runtime tools are missing."));
        var preflight = new FakeRuntimeToolPreflightService(new ProcessRuntimeToolPreflightResult(
            false,
            ["workspace_dotnet_restore"],
            "Required runtime tool(s) are not composed for this process step: workspace_dotnet_restore."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles,
                runtimeToolPreflightService: preflight);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    [],
                    new ProcessStepExecutionContract(
                        RequiredArtifacts: [],
                        ExpectedProducedArtifacts: [],
                        RequiredRuntimeToolNames: ["workspace_dotnet_restore"],
                        ContractHash: "sha256:branch-scoped-product-receipt-preflight")));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.False(workspace.ExecuteRunCalled);
            var request = Assert.Single(preflight.Requests);
            Assert.Equal(["workspace_dotnet_restore"], request.RequiredRuntimeToolNames);
            Assert.DoesNotContain("browser_take_screenshot", request.RequiredRuntimeToolNames);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_blocks_before_agent_when_dotnet_setup_plan_guard_fails()
    {
        var agent = NewAgent(
            ".NET Application Developer",
            ".NET developer",
            AgentWorkloadKind.Programming,
            [
                "dotnet",
                "developer",
                "implementation"
            ],
            AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var outputRoot = CreateTempProductRoot();
        var assignment = CreateProductMutationAssignment(outputRoot) with
        {
            StepKey = "create-dotnet-project",
            RoleKey = "dotnet-developer",
            RoleResourceKey = "dotnet-developer",
            RoleDisplayName = ".NET developer",
            ExecutorId = agent.Id.ToString("D"),
            ExecutorDisplayName = agent.Name,
            LaunchVariables = CreateDotNetCreateProjectLaunchVariables(
                outputRoot,
                requiredReceipts:
                [
                    "template=sln",
                    "template=blazorwasm"
                ])
        };
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The agent must not run when deterministic setup plan guard fails."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles,
                runtimeToolPreflightService: new ProcessRuntimeToolPreflightService(
                    [],
                    [new DotNetSolutionSetupRuntimeToolPlanGuard(TestWorkspaceServices.PhysicalPathPolicyFactory)],
                    ProcessRuntimeToolPreflightContributionCatalog.Empty));

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.False(workspace.ExecuteRunCalled);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("process.adapter.runtime_tool_preflight_failed", diagnostic.Code.Value);
            Assert.Contains("PlanIssues=2", diagnostic.SafeSummary, StringComparison.Ordinal);
            Assert.DoesNotContain(outputRoot, diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(outputRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_uses_runtime_owned_dotnet_setup_executor_before_agent_execution()
    {
        var agent = NewAgent(
            ".NET Application Developer",
            ".NET developer",
            AgentWorkloadKind.Programming,
            [
                "dotnet",
                "developer",
                "implementation"
            ],
            AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var outputRoot = CreateTempProductRoot();
        var assignment = CreateProductMutationAssignment(outputRoot) with
        {
            StepKey = "create-dotnet-project",
            RoleKey = "dotnet-developer",
            RoleResourceKey = "dotnet-developer",
            RoleDisplayName = ".NET developer",
            ExecutorId = agent.Id.ToString("D"),
            ExecutorDisplayName = agent.Name,
            AllowedOperations =
            [
                ProcessOperationContractNames.MutateProductTarget,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ],
            LaunchVariables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ProcessRuntimeLaunchVariables.ProcessStepRuntimeOwnedExecutorKey] = FakeRuntimeOwnedStepExecutor.RuntimeOwnedExecutorKey
            }
        };
        var externalTargets = TestExternalTargetPathRegistry.Create();
        Assert.True(externalTargets.TryCreateAlias(outputRoot, out var productRootAlias));
        assignment = assignment with
        {
            LaunchVariables = WithLaunchVariables(
                assignment,
                (ProcessRuntimeLaunchVariables.ProductRootAlias, productRootAlias))
        };
        var executionRunId = Guid.NewGuid();
        var runtimeExecutor = new FakeRuntimeOwnedStepExecutor(new ProcessRuntimeOwnedStepExecutionResult(
            true,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Runtime-owned .NET solution setup completed.",
                EvidenceRefs = ["artifacts/process-runs/runtime-owned-dotnet-setup.json"],
                NextActions = [],
                HumanReadableSummaryMarkdown = "Runtime-owned .NET solution setup completed."
            },
            [
                CreateToolReceipt(
                    "workspace_pwsh_run_script",
                    $"runtime-owned setup for {outputRoot}",
                    "Succeeded (exit 0)",
                    workingDirectory: outputRoot,
                    executionRunId: executionRunId),
                CreateToolReceipt(
                    "workspace_write_file",
                    $"{productRootAlias}/Calculator.csproj",
                    "Succeeded: Updated product file.",
                    executionRunId: executionRunId)
            ],
            executionRunId,
            "Runtime-owned .NET setup completed.",
            "runtime-owned-dotnet-setup:completed"));
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "Calculator.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The agent must not run when runtime-owned .NET setup handles the step."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot, externalTargets);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles,
                runtimeOwnedStepExecutors: [runtimeExecutor]);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.True(result.Outcome == StrategyOutcome.Succeeded, DescribeResult(result));
            Assert.False(workspace.ExecuteRunCalled);
            Assert.Equal(1, runtimeExecutor.CallCount);
            Assert.Contains("Runtime-owned .NET solution setup completed", result.UserSafeSummary, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(outputRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_accepts_runtime_owned_read_only_completion_projection_without_product_mutation_receipt()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(outputRoot, "Calculator.sln"), string.Empty);
            var (assignment, agent) = CreateRuntimeOwnedProductAssignment(outputRoot);
            var executionRunId = Guid.NewGuid();
            var runtimeResult = CreateSuccessfulRuntimeOwnedStepResult(
                executionRunId,
                CreateReadOnlyVerificationReceipts(executionRunId, outputRoot),
                ProcessRuntimeOwnedCompletionScope.ReadOnlyProductVerification);

            var execution = await ExecuteRuntimeOwnedStepAsync(assignment, agent, runtimeResult);

            Assert.Equal(StrategyOutcome.Succeeded, execution.Result.Outcome);
            Assert.Empty(execution.Result.Diagnostics);
            Assert.False(execution.AgentWasInvoked);
            Assert.Equal(1, execution.RuntimeExecutorCallCount);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_verifies_existing_dotnet_solution_through_real_runtime_owned_executor_without_legacy_mutation_receipts()
    {
        var outputRoot = CreateTempProductRoot();
        var projectFile = Path.Combine(outputRoot, "src", "Portal", "Portal.csproj");
        var solutionFile = Path.Combine(outputRoot, "EnterpriseSuite.sln");
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        await File.WriteAllTextAsync(
            projectFile,
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        await File.WriteAllTextAsync(solutionFile, "src/Portal/Portal.csproj");
        var requiredFileContentChecks = JsonSerializer.Serialize(new object[]
        {
            new
            {
                pathCandidates = new[] { solutionFile },
                requiredTextAnyGroups = new[] { new[] { "src/Portal/Portal.csproj" } }
            },
            new
            {
                pathCandidates = new[] { projectFile },
                requiredTextAnyGroups = new[] { new[] { "<TargetFramework>net10.0</TargetFramework>" } }
            }
        });
        var (baseAssignment, agent) = CreateRuntimeOwnedProductAssignment(outputRoot);
        var assignment = baseAssignment with
        {
            LaunchVariables = WithLaunchVariables(
                baseAssignment,
                (ProcessRuntimeLaunchVariables.ProcessStepRuntimeOwnedExecutorKey, DotNetSolutionSetupRuntimeExecutor.DriverKey),
                ("DotNetProvisioningMode", "verify-existing"),
                ("DotNetSolutionFile", solutionFile),
                ("DotNetSolutionFileCandidates", solutionFile),
                ("DotNetRequiredProjectFiles", JsonSerializer.Serialize(new[] { projectFile })),
                ("DotNetTestProjectFiles", "[]"),
                (ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths, projectFile),
                (
                    ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks,
                    requiredFileContentChecks),
                (
                    ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts,
                    "workspace_dotnet_new;workspace_pwsh_run_script"))
        };
        var runtimeExecutor = new DotNetSolutionSetupRuntimeExecutor(
            null!,
            null!,
            null!,
            null!,
            new DotNetExistingSolutionVerifier(TestWorkspaceServices.PhysicalPathPolicyFactory),
            TestWorkspaceServices.PhysicalPathPolicyFactory);
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The agent must not run for runtime-owned existing-solution verification."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles,
                runtimeOwnedStepExecutors: [runtimeExecutor]);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
            Assert.False(workspace.ExecuteRunCalled);
            Assert.NotEmpty(result.ProducedArtifacts);
            Assert.DoesNotContain(
                result.Diagnostics,
                diagnostic => diagnostic.Code.Value is
                    "process.adapter.product_mutation_receipt_missing" or
                    "process.adapter.product_required_tool_receipt_missing");
        }
        finally
        {
            DeleteDirectory(outputRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_without_runtime_owned_completion_projection_still_requires_product_mutation_receipt()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(outputRoot, "Calculator.sln"), string.Empty);
            var (assignment, agent) = CreateRuntimeOwnedProductAssignment(outputRoot);
            var executionRunId = Guid.NewGuid();
            var runtimeResult = CreateSuccessfulRuntimeOwnedStepResult(
                executionRunId,
                CreateReadOnlyVerificationReceipts(executionRunId, outputRoot));

            var execution = await ExecuteRuntimeOwnedStepAsync(assignment, agent, runtimeResult);

            Assert.Equal(StrategyOutcome.NeedsManager, execution.Result.Outcome);
            Assert.False(execution.AgentWasInvoked);
            Assert.Equal(1, execution.RuntimeExecutorCallCount);
            var diagnostic = Assert.Single(execution.Result.Diagnostics);
            Assert.Equal("process.adapter.product_mutation_receipt_missing", diagnostic.Code.Value);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Theory]
    [InlineData(RuntimeOwnedReadOnlyScopeViolation.MissingMutationAuthority)]
    [InlineData(RuntimeOwnedReadOnlyScopeViolation.NonMutableTarget)]
    [InlineData(RuntimeOwnedReadOnlyScopeViolation.MissingManagedArtifactWriteAuthority)]
    public async Task ExecuteAsync_rejects_read_only_runtime_owned_scope_for_incompatible_persisted_assignment(
        RuntimeOwnedReadOnlyScopeViolation violation)
    {
        (IReadOnlyList<string> DeclaredOperations,
            string DeclaredTargetScope,
            string ExpectedIssue) configuration = violation switch
        {
            RuntimeOwnedReadOnlyScopeViolation.MissingMutationAuthority => (
                [ProcessOperationContractNames.WriteManagedProcessArtifacts],
                ProcessOperationContractNames.ExternalProductTargetMutable,
                "requires MutateProductTarget"),
            RuntimeOwnedReadOnlyScopeViolation.NonMutableTarget => (
                [
                    ProcessOperationContractNames.MutateProductTarget,
                    ProcessOperationContractNames.WriteManagedProcessArtifacts
                ],
                ProcessOperationContractNames.ExternalActionControlled,
                "cannot narrow target scope"),
            RuntimeOwnedReadOnlyScopeViolation.MissingManagedArtifactWriteAuthority => (
                [ProcessOperationContractNames.MutateProductTarget],
                ProcessOperationContractNames.ExternalProductTargetMutable,
                "must include WriteManagedProcessArtifacts"),
            _ => throw new ArgumentOutOfRangeException(nameof(violation), violation, null)
        };
        var outputRoot = CreateTempProductRoot();
        try
        {
            var (baseAssignment, agent) = CreateRuntimeOwnedProductAssignment(
                outputRoot,
                configuration.DeclaredOperations);
            var assignment = baseAssignment with
            {
                OperationTargetScope = configuration.DeclaredTargetScope
            };
            var executionRunId = Guid.NewGuid();
            var runtimeResult = CreateSuccessfulRuntimeOwnedStepResult(
                executionRunId,
                CreateReadOnlyVerificationReceipts(executionRunId, outputRoot),
                ProcessRuntimeOwnedCompletionScope.ReadOnlyProductVerification);

            var execution = await ExecuteRuntimeOwnedStepAsync(assignment, agent, runtimeResult);

            Assert.Equal(StrategyOutcome.NeedsManager, execution.Result.Outcome);
            Assert.False(execution.AgentWasInvoked);
            Assert.Equal(1, execution.RuntimeExecutorCallCount);
            var diagnostic = Assert.Single(execution.Result.Diagnostics);
            Assert.Equal("process.adapter.runtime_owned_completion_contract_invalid", diagnostic.Code.Value);
            Assert.Equal(ProcessDiagnosticRetrySafety.UnsafeToRetry, diagnostic.RetrySafety);
            Assert.Equal(ProcessDiagnosticIdempotencyClassification.Idempotent, diagnostic.Idempotency);
            Assert.Contains(configuration.ExpectedIssue, diagnostic.SafeSummary, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_read_only_runtime_owned_scope_preserves_non_mutation_completion_obligations()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var (baseAssignment, agent) = CreateRuntimeOwnedProductAssignment(
                outputRoot,
                [
                    ProcessOperationContractNames.MutateProductTarget,
                    ProcessOperationContractNames.RunValidation,
                    ProcessOperationContractNames.WriteManagedProcessArtifacts
                ]);
            var assignment = baseAssignment with
            {
                LaunchVariables = WithLaunchVariables(
                    baseAssignment,
                    (ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts, "workspace_dotnet_test"))
            };
            var executionRunId = Guid.NewGuid();
            var runtimeResult = CreateSuccessfulRuntimeOwnedStepResult(
                executionRunId,
                CreateReadOnlyVerificationReceipts(executionRunId, outputRoot),
                ProcessRuntimeOwnedCompletionScope.ReadOnlyProductVerification);

            var execution = await ExecuteRuntimeOwnedStepAsync(assignment, agent, runtimeResult);

            Assert.Equal(StrategyOutcome.NeedsManager, execution.Result.Outcome);
            Assert.Contains(
                execution.Result.Diagnostics,
                diagnostic => diagnostic.Code.Value == "process.adapter.product_required_tool_receipt_missing");
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Theory]
    [InlineData("Succeeded: Wrote product source.")]
    [InlineData("Failed (exit 1): Product write may have partially completed.")]
    [InlineData("Denied: Product write was not authorized.")]
    public async Task ExecuteAsync_rejects_read_only_runtime_owned_scope_with_any_product_mutation_receipt(
        string mutationExitSummary)
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var (assignment, agent) = CreateRuntimeOwnedProductAssignment(outputRoot);
            var executionRunId = Guid.NewGuid();
            var runtimeResult = CreateSuccessfulRuntimeOwnedStepResult(
                executionRunId,
                [
                    CreateToolReceipt(
                        "workspace_write_file",
                        "external-target/product/Calculator.csproj",
                        mutationExitSummary,
                        workingDirectory: outputRoot,
                        executionRunId: executionRunId)
                ],
                ProcessRuntimeOwnedCompletionScope.ReadOnlyProductVerification);

            var execution = await ExecuteRuntimeOwnedStepAsync(assignment, agent, runtimeResult);

            Assert.Equal(StrategyOutcome.NeedsManager, execution.Result.Outcome);
            Assert.False(execution.AgentWasInvoked);
            Assert.Equal(1, execution.RuntimeExecutorCallCount);
            var diagnostic = Assert.Single(execution.Result.Diagnostics);
            Assert.Equal("process.adapter.runtime_owned_completion_contract_invalid", diagnostic.Code.Value);
            Assert.Contains(
                "cannot contain a product-mutation receipt",
                diagnostic.SafeSummary,
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_preserves_typed_runtime_owned_failure_and_bounded_execution_evidence()
    {
        const string rawSecret = "raw-runtime-owned-failure-token";
        var agent = NewAgent(
            ".NET Application Developer",
            ".NET developer",
            AgentWorkloadKind.Programming,
            [
                "dotnet",
                "developer",
                "implementation"
            ],
            AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var outputRoot = CreateTempProductRoot();
        var assignment = CreateProductMutationAssignment(outputRoot) with
        {
            StepKey = "create-dotnet-project",
            RoleKey = "dotnet-developer",
            RoleResourceKey = "dotnet-developer",
            RoleDisplayName = ".NET developer",
            ExecutorId = agent.Id.ToString("D"),
            ExecutorDisplayName = agent.Name,
            LaunchVariables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ProcessRuntimeLaunchVariables.ProcessStepRuntimeOwnedExecutorKey] = FakeRuntimeOwnedStepExecutor.RuntimeOwnedExecutorKey,
                [ProcessRuntimeLaunchVariables.ProductRoot] = outputRoot
            }
        };
        var executionRunId = Guid.NewGuid();
        var runtimeExecutor = new FakeRuntimeOwnedStepExecutor(new ProcessRuntimeOwnedStepExecutionResult(
            false,
            null,
            [
                CreateToolReceipt(
                    "workspace_pwsh_run_script",
                    $"runtime-owned setup for {outputRoot}",
                    "Succeeded (exit 0)",
                    workingDirectory: outputRoot,
                    executionRunId: executionRunId),
                CreateToolReceipt(
                    "workspace_read_file",
                    "external-target/product/Calculator.sln",
                    "Failed: File was not found.",
                    workingDirectory: outputRoot,
                    executionRunId: executionRunId)
            ],
            executionRunId,
            $"Required readback under {outputRoot.ToUpperInvariant()} was not found; password={rawSecret}. {new string('x', 3000)}",
            "runtime-owned-dotnet-setup:readback-path-missing",
            ProcessRuntimeOwnedStepFailures.ApplyDeclaredIdempotency(
                ProcessRuntimeOwnedStepFailures.ReadbackPathMissing,
                ProcessToolOperationIdempotencyPolicy.CurrentRunRepeatable)));
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The agent must not run when the runtime-owned executor handles the step."));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles,
                runtimeOwnedStepExecutors: [runtimeExecutor]);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.False(workspace.ExecuteRunCalled);
            Assert.Equal(1, runtimeExecutor.CallCount);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("process.adapter.runtime_owned_readback_path_missing", diagnostic.Code.Value);
            Assert.Equal(ProcessDiagnosticRetrySafety.SafeToRetry, diagnostic.RetrySafety);
            Assert.Equal(ProcessDiagnosticIdempotencyClassification.Idempotent, diagnostic.Idempotency);
            Assert.Contains(executionRunId.ToString("D"), diagnostic.SafeSummary, StringComparison.Ordinal);
            Assert.Contains("workspace_pwsh_run_script=Succeeded", diagnostic.SafeSummary, StringComparison.Ordinal);
            Assert.Contains("workspace_read_file=Failed", diagnostic.SafeSummary, StringComparison.Ordinal);
            Assert.True(diagnostic.SafeSummary.Length <= 2000);
            Assert.Contains("[configured product root]", diagnostic.SafeSummary, StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", diagnostic.SafeSummary, StringComparison.Ordinal);
            Assert.DoesNotContain(outputRoot, diagnostic.SafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(rawSecret, diagnostic.SafeSummary, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(outputRoot);
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Prompt_contract_includes_capability_scope_required_receipts()
    {
        var assignment = CreateManagedArtifactAssignment("feature-intake") with
        {
            CapabilityScope = new ProcessCapabilityScope
            {
                RequiredReceipts =
                [
                    new ProcessRequiredToolReceipt
                    {
                        Key = "slice-restore",
                        Kind = ProcessRequiredToolReceiptKind.RuntimeToolName,
                        ToolName = "workspace_dotnet_restore",
                        Reason = "Slice validation must run restore in the current execution before choosing accepted or repair-required."
                    },
                    new ProcessRequiredToolReceipt
                    {
                        Key = "slice-build",
                        Kind = ProcessRequiredToolReceiptKind.RuntimeToolName,
                        ToolName = "workspace_dotnet_build",
                        Reason = "Slice validation must run build in the current execution before choosing accepted or repair-required."
                    },
                    new ProcessRequiredToolReceipt
                    {
                        Key = "slice-test",
                        Kind = ProcessRequiredToolReceiptKind.RuntimeToolName,
                        ToolName = "workspace_dotnet_test",
                        Reason = "Slice validation must run tests in the current execution when a test project exists or tests are expected."
                    }
                ]
            }
        };
        var stepContract = new ProcessStepExecutionContract(
            RequiredArtifacts: [],
            ExpectedProducedArtifacts: [],
            RequiredRuntimeToolNames: [],
            ContractHash: "sha256:capability-scope-receipts");

        var resolvedContract = ResolvePromptStepContract(assignment, stepContract);
        var prompt = ProcessStepContractPromptBuilder.Build("Validate the slice.", resolvedContract);

        Assert.Contains("Required runtime tools:", prompt, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_restore", prompt, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_build", prompt, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_test", prompt, StringComparison.Ordinal);
        Assert.Contains("each listed tool must produce a receipt whose execution id is this exact execution attempt", prompt, StringComparison.Ordinal);
        Assert.Contains("Upstream receipts and receipts from earlier attempts of this step do not count", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not return Completed until every available required input is reflected in the work, every required runtime tool has a current execution-run receipt", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Prompt_contract_includes_product_completion_required_receipts_by_step()
    {
        var requiredToolReceiptMap = JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["qa-recheck"] =
            [
                "template=blazorwasm",
                "workspace_dotnet_run",
                "browser_navigate",
                "browser_take_screenshot",
                "workspace_dotnet_stop"
            ]
        });
        var assignment = CreateManagedArtifactAssignment("qa-recheck") with
        {
            LaunchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep] = requiredToolReceiptMap
            }
        };
        var stepContract = new ProcessStepExecutionContract(
            RequiredArtifacts: [],
            ExpectedProducedArtifacts: [],
            RequiredRuntimeToolNames: [],
            ContractHash: "sha256:product-completion-receipts");

        var resolvedContract = ResolvePromptStepContract(assignment, stepContract);
        var prompt = ProcessStepContractPromptBuilder.Build(
            "Re-run QA validation after repair.",
            resolvedContract,
            assignment.LaunchVariables,
            assignment.StepKey);

        Assert.Contains("Required runtime tools:", prompt, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_run", prompt, StringComparison.Ordinal);
        Assert.Contains("browser_navigate", prompt, StringComparison.Ordinal);
        Assert.Contains("browser_take_screenshot", prompt, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_stop", prompt, StringComparison.Ordinal);
        Assert.Contains("each listed tool must produce a receipt whose execution id is this exact execution attempt", prompt, StringComparison.Ordinal);
        Assert.Contains("Invoke every missing tool now in this execution attempt", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("template=blazorwasm", resolvedContract.RequiredRuntimeToolNames);
        Assert.DoesNotContain("template=blazorwasm", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Prompt_contract_excludes_branch_scoped_product_receipt_tools_before_branch_selection()
    {
        var receiptRules = JsonSerializer.Serialize(new Dictionary<string, object[]>(StringComparer.Ordinal)
        {
            ["qa-validation"] =
            [
                BranchAwareReceipt("workspace_dotnet_restore", "quality-accepted"),
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["toolName"] = "workspace_dotnet_restore"
                },
                BranchAwareReceipt("browser_take_screenshot", "quality-accepted")
            ]
        });
        var baseAssignment = CreateManagedArtifactAssignment("qa-validation");
        var assignment = baseAssignment with
        {
            LaunchVariables = WithLaunchVariables(
                baseAssignment,
                (ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep, receiptRules))
        };
        var stepContract = new ProcessStepExecutionContract(
            RequiredArtifacts: [],
            ExpectedProducedArtifacts: [],
            RequiredRuntimeToolNames: [],
            ContractHash: "sha256:branch-scoped-product-receipt-prompt");

        var resolvedContract = ResolvePromptStepContract(assignment, stepContract);
        var prompt = ProcessStepContractPromptBuilder.Build(
            "Validate the product and select an outcome branch.",
            resolvedContract,
            assignment.LaunchVariables,
            assignment.StepKey);

        Assert.Equal(["workspace_dotnet_restore"], resolvedContract.RequiredRuntimeToolNames);
        Assert.Contains("workspace_dotnet_restore", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("browser_take_screenshot", resolvedContract.RequiredRuntimeToolNames);
        Assert.DoesNotContain("browser_take_screenshot", prompt, StringComparison.Ordinal);
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
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    [],
                    BranchStepContract("scope-clarified")));

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
            Assert.Contains("Runtime Captured Structured Outcome", content, StringComparison.Ordinal);
            Assert.Contains("Runtime Accepted Completion Gates", content, StringComparison.Ordinal);
            Assert.Contains("Clarified the requested software scope.", content, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_rejects_runtime_managed_artifact_materialization_without_assignment_authority()
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
        var assignment = CreateManagedArtifactAssignment(
            "feature-intake",
            agent.Id,
            allowedOperations: [ProcessOperationContractNames.ReadProcessContext]);
        var executionRunId = Guid.NewGuid();
        var responseText = JsonSerializer.Serialize(new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Clarified the requested software scope.",
            EvidenceRefs = ["https://example.invalid/external-evidence.md"],
            HumanReadableSummaryMarkdown = "The software scope is clarified."
        });
        var workspace = new ThrowingWorkspaceService(
            agent,
            executeException: null,
            executeResult: CreateExecutionRunResult(agent.Id, executionRunId, responseText),
            executionDetail: CreateExecutionRunDetail(agent.Id, executionRunId, responseText, []));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Code.Value == "process.adapter.managed_artifact_materialization_not_authorized");
            Assert.False(File.Exists(Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                assignment.RunId.Value.ToString("D"),
                "steps",
                "feature-intake.md")));
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_rejects_oversized_structured_outcome_before_writing_managed_artifact()
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
        var responseText = JsonSerializer.Serialize(new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "Clarified the requested software scope.",
            EvidenceRefs = ["https://example.invalid/external-evidence.md"],
            HumanReadableSummaryMarkdown = new string('x', WorkspaceFileLimits.MaxTextReadCharacters)
        });
        var workspace = new ThrowingWorkspaceService(
            agent,
            executeException: null,
            executeResult: CreateExecutionRunResult(agent.Id, executionRunId, responseText),
            executionDetail: CreateExecutionRunDetail(agent.Id, executionRunId, responseText, []));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Code.Value ==
                    ProcessStepCompletionCoordinator.InvalidStructuredOutcomeShapeDiagnosticCode);
            Assert.False(File.Exists(Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                assignment.RunId.Value.ToString("D"),
                "steps",
                "feature-intake.md")));
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Agent_written_artifact_rejects_oversized_captured_append_before_mutation()
    {
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var assignment = CreateManagedArtifactAssignment("feature-intake");
            var primaryRef = BuildStepArtifactRef(assignment);
            var executionRunId = Guid.NewGuid();
            var originalContent = new string('x', WorkspaceFileLimits.MaxTextReadCharacters - 1);
            var writeResult = workspaceFiles.WriteTextFile(
                primaryRef,
                originalContent,
                overwrite: true);
            var output = new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "The structured outcome is ready.",
                EvidenceRefs = [primaryRef],
                NextActions = []
            };
            var service = new ProcessManagedArtifactService(workspaceFiles);

            var materialization = service.MaterializeManagedOutcomeArtifactIfNeeded(
                assignment,
                output,
                executionRunId,
                [CreateToolReceipt(
                    "workspace_write_file",
                    primaryRef,
                    "Succeeded: Wrote managed artifact.",
                    executionRunId: executionRunId)]);
            var readResult = workspaceFiles.ReadTextFile(
                primaryRef,
                WorkspaceFileLimits.MaxTextReadCharacters);

            Assert.True(writeResult.Succeeded, writeResult.Message);
            Assert.NotNull(materialization.Issue);
            Assert.Equal(
                ProcessManagedArtifactService.ManagedArtifactMaterializationTooLargeDiagnosticCode,
                materialization.Issue.Code);
            Assert.True(readResult.Succeeded, readResult.Message);
            Assert.False(readResult.IsTruncated);
            Assert.Equal(originalContent, readResult.Content);
            Assert.DoesNotContain(
                ProcessManagedArtifactFormatter.BuildManagedOutcomeArtifactLifecycleMarker(
                    executionRunId,
                    ProcessManagedArtifactFormatter.ManagedOutcomeArtifactLifecyclePhase.Captured),
                readResult.Content,
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Staged_artifact_rejects_oversized_acceptance_append_before_mutation()
    {
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var assignment = CreateManagedArtifactAssignment("feature-intake");
            var primaryRef = BuildStepArtifactRef(assignment);
            var executionRunId = Guid.NewGuid();
            var output = new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "The structured outcome is ready.",
                EvidenceRefs = [primaryRef],
                NextActions = []
            };
            var capturedContent = ProcessManagedArtifactFormatter.BuildManagedOutcomeArtifactAppendixContent(
                assignment,
                output,
                executionRunId,
                primaryRef);
            var acceptanceContent = ProcessManagedArtifactFormatter.BuildManagedOutcomeArtifactAcceptanceContent(
                assignment,
                output,
                executionRunId,
                primaryRef);
            var originalContent = new string(
                'x',
                WorkspaceFileLimits.MaxTextReadCharacters - capturedContent.Length - 1);
            var writeResult = workspaceFiles.WriteTextFile(
                primaryRef,
                originalContent,
                overwrite: true);
            var service = new ProcessManagedArtifactService(workspaceFiles);
            var writeReceipt = CreateToolReceipt(
                "workspace_write_file",
                primaryRef,
                "Succeeded: Wrote managed artifact.",
                executionRunId: executionRunId);

            var materialization = service.MaterializeManagedOutcomeArtifactIfNeeded(
                assignment,
                output,
                executionRunId,
                [writeReceipt]);
            var stagedReadResult = workspaceFiles.ReadTextFile(
                primaryRef,
                WorkspaceFileLimits.MaxTextReadCharacters);
            var acceptanceIssue = service.AcceptManagedOutcomeArtifactIfNeeded(
                assignment,
                materialization,
                executionRunId,
                materialization.ToolReceipts,
                out var acceptedToolReceipts,
                out var acceptedArtifactContentHashes);
            var readResult = workspaceFiles.ReadTextFile(
                primaryRef,
                WorkspaceFileLimits.MaxTextReadCharacters);

            Assert.True(writeResult.Succeeded, writeResult.Message);
            Assert.Null(materialization.Issue);
            Assert.True(stagedReadResult.Succeeded, stagedReadResult.Message);
            Assert.False(stagedReadResult.IsTruncated);
            Assert.NotNull(acceptanceIssue);
            Assert.Equal(
                ProcessManagedArtifactService.ManagedArtifactAcceptanceTooLargeDiagnosticCode,
                acceptanceIssue.Code);
            Assert.Same(materialization.ToolReceipts, acceptedToolReceipts);
            Assert.Null(acceptedArtifactContentHashes);
            Assert.True(readResult.Succeeded, readResult.Message);
            Assert.False(readResult.IsTruncated);
            Assert.Equal(stagedReadResult.Content, readResult.Content);
            Assert.True(acceptanceContent.Length > 1);
            Assert.Contains(
                ProcessManagedArtifactFormatter.BuildManagedOutcomeArtifactLifecycleMarker(
                    executionRunId,
                    ProcessManagedArtifactFormatter.ManagedOutcomeArtifactLifecyclePhase.Captured),
                readResult.Content,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                ProcessManagedArtifactFormatter.BuildManagedOutcomeArtifactLifecycleMarker(
                    executionRunId,
                    ProcessManagedArtifactFormatter.ManagedOutcomeArtifactLifecyclePhase.Accepted),
                readResult.Content,
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_blocks_schema_invalid_architecture_artifact_before_dependent_subprocess_launch()
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
        var assignment = CreateManagedArtifactAssignment("slice-architecture-check", agent.Id);
        var executionRunId = Guid.NewGuid();
        var primaryRef = BuildStepArtifactRef(assignment);
        var responseText = $$"""
            {
              "status": "Completed",
              "reason": "The architecture decision is ready for downstream setup.",
              "branchOutcomeKey": "",
              "branchOutcomeTitle": "",
              "evidenceRefs": ["{{primaryRef}}"],
              "nextActions": [],
              "humanReadableSummaryMarkdown": "Architecture decision completed."
            }
            """;
        var workspace = new ThrowingWorkspaceService(
            agent,
            executeException: null,
            executeResult: CreateExecutionRunResult(agent.Id, executionRunId, responseText),
            executionDetail: CreateExecutionRunDetail(agent.Id, executionRunId, responseText, []));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        var descriptor = new ProcessArtifactSlotDescriptor(
            assignment.ProducedArtifactSlotIds[0],
            "slice-architecture-check:dotnet-solution-context",
            assignment.StepKey,
            "dotnet-solution-context",
            ".NET solution context",
            "Decision",
            primaryRef,
            ProcessArtifactMaterializationMode.AgentWritten)
        {
            PayloadSchema = DotNetSolutionContextParser.Schema
        };
        var stepContract = new ProcessStepExecutionContract(
            [],
            [new ExpectedProducedArtifactRef(descriptor.SlotId)],
            [],
            "sha256:architecture-context")
        {
            ArtifactDescriptors = [descriptor]
        };

        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    [],
                    stepContract));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(
                result.Diagnostics,
                diagnostic =>
                    diagnostic.Code.Value == "process.adapter.artifact_payload_schema_invalid" &&
                    diagnostic.RetrySafety == ProcessDiagnosticRetrySafety.SafeToRetry);
            Assert.True(workspace.ExecuteRunCalled);
            var artifact = workspaceFiles.ReadTextFile(primaryRef);
            Assert.True(artifact.Succeeded);
            Assert.Contains("Runtime Captured Structured Outcome", artifact.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("Runtime Accepted Completion Gates", artifact.Content, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void ToAdapterResult_blocks_schema_invalid_artifact_when_step_contract_is_supplied()
    {
        var assignment = CreateManagedArtifactAssignment("slice-architecture-check");
        var primaryRef = BuildStepArtifactRef(assignment);
        var descriptor = new ProcessArtifactSlotDescriptor(
            assignment.ProducedArtifactSlotIds[0],
            "slice-architecture-check:dotnet-solution-context",
            assignment.StepKey,
            "dotnet-solution-context",
            ".NET solution context",
            "Decision",
            primaryRef,
            ProcessArtifactMaterializationMode.AgentWritten)
        {
            PayloadSchema = DotNetSolutionContextParser.Schema
        };
        var stepContract = new ProcessStepExecutionContract(
            [],
            [new ExpectedProducedArtifactRef(descriptor.SlotId)],
            [],
            "sha256:architecture-context")
        {
            ArtifactDescriptors = [descriptor]
        };
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);

        try
        {
            Assert.True(workspaceFiles.WriteTextFile(primaryRef, "Status: Completed\n\nArchitecture narrative only.").Succeeded);
            var toolReceiptPolicies = CreateToolReceiptPolicyCatalog();
            var completionIssueResultFactory = new ProcessCompletionIssueResultFactory(
                workspaceFiles,
                ProcessCompletionDefectEvidenceCatalog.Empty);
            var completionGateEvaluator = new ProcessCompletionGateFactory(
                    toolReceiptPolicies,
                    new ProcessToolReceiptEvidenceGate(workspaceFiles, []),
                    [new DotNetSolutionContextCompletionGateContribution(workspaceFiles)],
                    completionIssueResultFactory,
                    new ProcessOutcomeGroundingValidator(workspaceFiles))
                .CreateCompletionGateEvaluator();
            var resultConverter = new ProcessExecutionResultConverter(
                completionGateEvaluator,
                toolReceiptPolicies,
                completionIssueResultFactory);

            var result = resultConverter.ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Architecture decision completed.",
                    EvidenceRefs = [primaryRef],
                    NextActions = [],
                    HumanReadableSummaryMarkdown = "Architecture decision completed."
                },
                "sha256:raw",
                stepContract: stepContract);

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Code.Value == ProcessCompletionDiagnosticCodes.ArtifactPayloadSchemaInvalid);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_stages_managed_artifact_without_acceptance_when_completion_gate_fails()
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
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            [
                new AgentCapabilityAssignment(
                    Guid.NewGuid(),
                    "workspace-dotnet-build",
                    CapabilityKind.Tool,
                    CapabilityProofStatus.Verified,
                    DateTimeOffset.UtcNow,
                    "Build tool is assigned.")
            ]);
        var baseAssignment = CreateManagedArtifactAssignment(
            "feature-intake",
            agent.Id,
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ]);
        var assignment = baseAssignment with
        {
            LaunchVariables = WithLaunchVariables(
                baseAssignment,
                (ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts, "workspace_dotnet_build"))
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
            executionDetail: CreateExecutionRunDetail(agent.Id, executionRunId, responseText, []));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.product_required_tool_receipt_missing");
            Assert.Empty(result.ProducedArtifacts);
            var artifactPath = Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                assignment.RunId.Value.ToString("D"),
                "steps",
                "feature-intake.md");
            Assert.True(File.Exists(artifactPath));
            var content = await File.ReadAllTextAsync(artifactPath);
            Assert.Contains("Runtime Captured Structured Outcome", content, StringComparison.Ordinal);
            Assert.Contains("Completion gates have not accepted this output yet", content, StringComparison.Ordinal);
            Assert.DoesNotContain("Runtime Validated Structured Outcome", content, StringComparison.Ordinal);
            Assert.DoesNotContain("Runtime Accepted Completion Gates", content, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_preserves_evidence_backed_blocked_branch_without_materializing_completion()
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
            AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var baseAssignment = CreateManagedArtifactAssignment(
            "independent-validate-quality-repair",
            agent.Id,
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.LaunchRuntime,
                ProcessOperationContractNames.CaptureRuntimeProof,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ]);
        var assignment = baseAssignment with
        {
            Prompt = """
            Independently validate the repaired product.

            Available branch outcomes:
            - repair-validated: Repair validated - Independent proof is green.
            - bughunt-required: Bughunt required - Independent proof found a remaining product defect.
            """
        };
        var executionRunId = Guid.NewGuid();
        var primaryRef = BuildStepArtifactRef(assignment);
        var responseText = $$"""
            {
              "status": "Blocked",
              "reason": "The browser proof found a remaining visible application error, so specialist bughunt is required.",
              "branchOutcomeKey": "bughunt-required",
              "branchOutcomeTitle": "Bughunt required",
              "evidenceRefs": [
                "{{primaryRef}}"
              ],
              "nextActions": [
                "Route the captured failure evidence to the bughunt specialist."
              ],
              "humanReadableSummaryMarkdown": "Independent restore, build, tests, runtime launch, browser inspection, and cleanup completed. The browser still shows a product defect."
            }
            """;
        var toolReceipts = new[]
        {
            CreateToolReceipt("workspace_dotnet_restore", "restore product", "Succeeded (exit 0)", executionRunId: executionRunId),
            CreateToolReceipt("workspace_dotnet_build", "build product", "Succeeded (exit 0)", executionRunId: executionRunId),
            CreateToolReceipt("workspace_dotnet_test", "test product", "Succeeded (exit 0)", executionRunId: executionRunId),
            CreateToolReceipt("workspace_dotnet_run", "run product", "Succeeded (exit 0)", executionRunId: executionRunId),
            CreateToolReceipt("browser_navigate", "navigate to product", "Succeeded (exit 0)", executionRunId: executionRunId),
            CreateToolReceipt("browser_snapshot", "capture visible error", "Succeeded (exit 0)", executionRunId: executionRunId),
            CreateToolReceipt("browser_console_messages", "read console", "Succeeded (exit 0)", executionRunId: executionRunId),
            CreateToolReceipt("workspace_dotnet_stop", "stop product", "Succeeded (exit 0)", executionRunId: executionRunId)
        };
        var workspace = new ThrowingWorkspaceService(
            agent,
            executeException: null,
            executeResult: CreateExecutionRunResult(agent.Id, executionRunId, responseText),
            executionDetail: CreateExecutionRunDetail(agent.Id, executionRunId, responseText, toolReceipts));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    [],
                    BranchStepContract("repair-validated", "bughunt-required")));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Empty(result.ProducedArtifacts);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.agent_blocked");
            Assert.Contains(result.ManagerSignals, signal =>
                signal.Code.Value == ProcessBranchSignalCodes.Outcome("bughunt-required").Value);
            var artifactPath = Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                assignment.RunId.Value.ToString("D"),
                "steps",
                "independent-validate-quality-repair.md");
            Assert.False(File.Exists(artifactPath));
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_appends_staged_and_accepted_outcome_to_existing_managed_artifact()
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

            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
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
            Assert.Contains("Runtime Captured Structured Outcome", content, StringComparison.Ordinal);
            Assert.Contains("Runtime Accepted Completion Gates", content, StringComparison.Ordinal);
            Assert.Contains("Restore, build, and tests completed successfully", content, StringComparison.Ordinal);
            Assert.Contains("Restore, build, and test proof is green.", content, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public Task ExecuteAsync_replaying_same_completed_run_keeps_agent_written_managed_artifact_idempotent()
        => AssertAgentWrittenManagedArtifactReplayAsync(ManagedArtifactReplayMode.Sequential);

    [Fact]
    public Task ExecuteAsync_concurrent_replay_of_same_completed_run_keeps_agent_written_managed_artifact_idempotent()
        => AssertAgentWrittenManagedArtifactReplayAsync(ManagedArtifactReplayMode.Concurrent);

    private static async Task AssertAgentWrittenManagedArtifactReplayAsync(ManagedArtifactReplayMode replayMode)
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
              "reason": "Restore, build, and tests completed successfully.",
              "branchOutcomeKey": "",
              "branchOutcomeTitle": "",
              "evidenceRefs": [
                "{{primaryRef}}"
              ],
              "nextActions": [],
              "humanReadableSummaryMarkdown": "Current-run validation proof is green."
            }
            """;
        using var concurrentReplayBarrier = new Barrier(2);
        Action? synchronizeConcurrentReplay = replayMode == ManagedArtifactReplayMode.Concurrent
            ? () =>
            {
                if (!concurrentReplayBarrier.SignalAndWait(TimeSpan.FromSeconds(15)))
                {
                    throw new TimeoutException("Concurrent replay calls did not reach the execution barrier.");
                }
            }
            : null;
        var workspace = new ThrowingWorkspaceService(
            agent,
            executeException: null,
            beforeThrow: synchronizeConcurrentReplay,
            executeResult: CreateExecutionRunResult(agent.Id, executionRunId, responseText),
            executionDetail: CreateExecutionRunDetail(
                agent.Id,
                executionRunId,
                responseText,
                [
                    CreateToolReceipt(
                        "workspace_write_file",
                        primaryRef,
                        "Succeeded: Created validation evidence.",
                        executionRunId: executionRunId)
                ]));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        var descriptor = new ProcessArtifactSlotDescriptor(
            assignment.ProducedArtifactSlotIds[0],
            "validate-first-build:validation-evidence",
            assignment.StepKey,
            "validation-evidence",
            "Validation evidence",
            "ManagedMarkdown",
            primaryRef,
            ProcessArtifactMaterializationMode.AgentWritten);
        var stepContract = new ProcessStepExecutionContract(
            [],
            [new ExpectedProducedArtifactRef(descriptor.SlotId)],
            [],
            "sha256:agent-written-replay")
        {
            ArtifactDescriptors = [descriptor]
        };
        var request = CreateAdapterRequest(
            assignment,
            ProcessExecutionAdapterKind.Workflow,
            new ProcessExecutionAdapterOperationKey("execute"),
            Binding,
            [],
            [],
            stepContract);

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

                Restore, build, and tests completed successfully.
                """);

            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            ProcessExecutionAdapterResult[] results;
            string? contentAfterFirstExecution = null;
            if (replayMode == ManagedArtifactReplayMode.Sequential)
            {
                var firstResult = await adapter.ExecuteAsync(request);
                contentAfterFirstExecution = await File.ReadAllTextAsync(artifactPath);
                var secondResult = await adapter.ExecuteAsync(request);
                results = [firstResult, secondResult];
            }
            else
            {
                results = await Task.WhenAll(
                    Task.Run(async () => await adapter.ExecuteAsync(request)),
                    Task.Run(async () => await adapter.ExecuteAsync(request)));
            }

            var acceptedMarker = ProcessManagedArtifactFormatter.BuildManagedOutcomeArtifactLifecycleMarker(
                executionRunId,
                ProcessManagedArtifactFormatter.ManagedOutcomeArtifactLifecyclePhase.Accepted);
            Assert.All(results, result =>
            {
                Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
                Assert.Empty(result.Diagnostics);
            });
            Assert.Equal(2, workspace.ExecutionDetailRequestIds.Count);
            Assert.All(
                workspace.ExecutionDetailRequestIds,
                requestedExecutionRunId => Assert.Equal(executionRunId, requestedExecutionRunId));
            var producedArtifacts = results
                .Select(result => Assert.Single(
                    result.ProducedArtifacts,
                    artifact => artifact.SlotId == descriptor.SlotId))
                .ToArray();
            var content = await File.ReadAllTextAsync(artifactPath);
            if (contentAfterFirstExecution is not null)
            {
                Assert.Equal(contentAfterFirstExecution, content);
            }

            Assert.Equal(
                1,
                content.Split(
                    ProcessManagedArtifactService.ManagedOutcomeArtifactCapturedHeading,
                    StringSplitOptions.None).Length - 1);
            Assert.Equal(
                1,
                content.Split(
                    ProcessManagedArtifactService.ManagedOutcomeArtifactAcceptedHeading,
                    StringSplitOptions.None).Length - 1);
            Assert.Equal(
                1,
                content.Split(
                    ProcessManagedArtifactFormatter.BuildManagedOutcomeArtifactLifecycleMarker(
                        executionRunId,
                        ProcessManagedArtifactFormatter.ManagedOutcomeArtifactLifecyclePhase.Captured),
                    StringSplitOptions.None).Length - 1);
            Assert.Equal(
                1,
                content.Split(
                    acceptedMarker,
                    StringSplitOptions.None).Length - 1);
            var finalContentHash = ComputeContentHash(content);
            Assert.All(
                producedArtifacts,
                artifact => Assert.Equal(finalContentHash, artifact.ContentHash));
            Assert.Contains("# Validate first build", content, StringComparison.Ordinal);
            Assert.Contains("Restore, build, and tests completed successfully.", content, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_omits_ungrounded_supplemental_ref_before_appending_grounded_defect_outcome()
    {
        var agent = NewAgent(
            ".NET Solution Architect",
            "Solution architect",
            AgentWorkloadKind.Programming,
            ["dotnet", "solution-architect", "architecture"],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var baseAssignment = CreateManagedArtifactAssignment("qa-validation", agent.Id);
        var matrix = new ProcessAcceptanceCriteriaMatrix
        {
            Criteria =
            [
                new ProcessAcceptanceCriterion
                {
                    Id = "AC-001",
                    SourceNodeId = "custom:product",
                    Summary = "The required interaction works.",
                    VerificationMethods = ["browser-proof"],
                    RequiredForAcceptance = true
                }
            ]
        };
        var assignment = baseAssignment with
        {
            Prompt = """
            Validate the product.

            Available branch outcomes:
            - quality-accepted: Quality accepted - all required criteria passed.
            - repair-required: Repair required - grounded failed criteria require repair.
            """,
            LaunchVariables = WithLaunchVariables(
                baseAssignment,
                (ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix, ProcessAcceptanceCriteriaMatrixJson.Serialize(matrix)),
                (ProcessRuntimeLaunchVariables.AcceptanceCriteriaAcceptedBranchOutcomeKeys, "quality-accepted"))
        };
        var executionRunId = Guid.NewGuid();
        var primaryRef = BuildStepArtifactRef(assignment);
        var ungroundedSupplementalRef =
            $"artifacts/process-runs/{Guid.NewGuid():D}/steps/capture-ui-screenshots.md";
        var responseText = $$"""
            {
              "status": "Completed",
              "reason": "QA found a repairable acceptance defect.",
              "branchOutcomeKey": "repair-required",
              "branchOutcomeTitle": "Repair required",
              "evidenceRefs": [
                "{{primaryRef}}",
                "{{ungroundedSupplementalRef}}"
              ],
              "acceptanceCriteriaEvidence": [
                {
                  "criterionId": "AC-001",
                  "status": "Failed",
                  "summary": "The required interaction did not reach the expected state.",
                  "evidenceRefs": [
                    "{{primaryRef}}"
                  ]
                }
              ],
              "nextActions": [
                "Repair the failed criterion and rerun QA."
              ],
              "humanReadableSummaryMarkdown": "QA routed the grounded defect to repair."
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
                [
                    CreateToolReceipt(
                        "workspace_write_file",
                        primaryRef,
                        "Succeeded: Created QA evidence.",
                        executionRunId: executionRunId)
                ]));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var artifactPath = Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                assignment.RunId.Value.ToString("D"),
                "steps",
                "qa-validation.md");
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            await File.WriteAllTextAsync(
                artifactPath,
                """
                # QA validation

                The current-run product proof found a repairable failed criterion.
                """);

            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    [],
                    BranchStepContract("quality-accepted", "repair-required")));

            Assert.True(result.Outcome == StrategyOutcome.Succeeded, result.UserSafeSummary);
            Assert.Contains(result.ManagerSignals, signal =>
                signal.Code.Value == ProcessBranchSignalCodes.Outcome("repair-required").Value);
            Assert.DoesNotContain(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.ungrounded_managed_artifact_reference");
            var content = await File.ReadAllTextAsync(artifactPath);
            Assert.Contains(primaryRef, content, StringComparison.Ordinal);
            Assert.DoesNotContain(ungroundedSupplementalRef, content, StringComparison.Ordinal);
            Assert.Contains("Runtime Accepted Completion Gates", content, StringComparison.Ordinal);
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

            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
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
    public void Grounding_parser_accepts_prompt_grounded_native_path_with_closing_markdown_emphasis()
    {
        const string nativePath = @"C:\programovani\dotnet\output";
        var agent = NewAgent(
            ".NET QA Review Lead",
            "QA lead",
            AgentWorkloadKind.Programming,
            ["qa-lead", "dotnet", "review"],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var assignment = CreateManagedArtifactAssignment("test-contract", agent.Id) with
        {
            Prompt = $"Mandatory criterion: final app must be placed in {nativePath}."
        };
        var candidateRefs = ProcessOutcomeReferenceGroundingPolicy.EnumerateTextPathReferences(
                $"**AC-017: Final app must be placed in {nativePath}**")
            .ToArray();

        var candidate = Assert.Single(candidateRefs);
        Assert.Equal(nativePath, candidate);
        Assert.Empty(ProcessOutcomeReferenceGroundingPolicy.FindUngroundedPathReferences(
            assignment,
            candidateRefs,
            toolReceipts: null));
    }

    [Fact]
    public async Task ExecuteAsync_removes_non_citable_source_paths_from_structured_outcome_before_grounding()
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
        var assignment = CreateManagedArtifactAssignment("quality-repair", agent.Id);
        var executionRunId = Guid.NewGuid();
        var primaryRef = BuildStepArtifactRef(assignment);
        var sourceDocumentRef = @"managed-files\project-media\files\3324868f66e2478abb8f14f32a5db1e9\office365-category-email-summary.md";
        var escapedSourceDocumentRef = sourceDocumentRef.Replace(@"\", @"\\", StringComparison.Ordinal);
        var responseText = $$"""
            {
              "status": "Completed",
              "reason": "Repaired the reported product defect. Source context: {{escapedSourceDocumentRef}}",
              "branchOutcomeKey": "",
              "branchOutcomeTitle": "",
              "evidenceRefs": [
                "{{primaryRef}}"
              ],
              "nextActions": [
                "QA can recheck the repaired product."
              ],
              "humanReadableSummaryMarkdown": "Status: Completed\n\nThe product defect is repaired.\n\nSource citation: {{escapedSourceDocumentRef}}"
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
            var writeResult = workspaceFiles.WriteTextFile(
                primaryRef,
                "# Quality repair evidence",
                overwrite: true);
            Assert.True(writeResult.Succeeded, writeResult.Message);

            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.True(
                result.Outcome == StrategyOutcome.Succeeded,
                result.UserSafeSummary);
            Assert.DoesNotContain(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == "process.adapter.ungrounded_outcome_reference");
            Assert.DoesNotContain("managed-files", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_does_not_recover_branch_from_current_primary_artifact_when_finalizer_omits_it()
    {
        var agent = NewAgent(
            ".NET Solution Architect",
            "Solution architect",
            AgentWorkloadKind.Programming,
            ["dotnet", "solution-architect", "architecture"],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var baseAssignment = CreateManagedArtifactAssignment("feature-repair", agent.Id);
        var assignment = baseAssignment with
        {
            Prompt = """
            Apply the repair and record the evidence.

            Available branch outcomes:
            - feature-repair-applied: Feature repair applied - The repair has current-run evidence.
            """
        };
        var executionRunId = Guid.NewGuid();
        var primaryRef = BuildStepArtifactRef(assignment);
        var responseText = $$"""
            {
              "status": "Completed",
              "reason": "Applied the repair and revalidated it.",
              "branchOutcomeKey": "",
              "branchOutcomeTitle": "",
              "evidenceRefs": ["{{primaryRef}}"],
              "nextActions": [],
              "humanReadableSummaryMarkdown": "The finalizer omitted the branch key."
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
                [CreateToolReceipt(
                    "workspace_write_file",
                    primaryRef,
                    "Succeeded: Created file.",
                    executionRunId: executionRunId)]));
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var writeResult = workspaceFiles.WriteTextFile(
                primaryRef,
                """
                # feature-repair Process Step Outcome

                ## Status
                Completed

                ## Branch Outcome
                - BranchOutcomeKey: feature-repair-applied
                - BranchOutcomeTitle: Feature repair applied
                """,
                overwrite: true);
            Assert.True(writeResult.Succeeded, writeResult.Message);

            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    [],
                    BranchStepContract("feature-repair-applied")));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Empty(result.ProducedArtifacts);
            Assert.DoesNotContain(result.ManagerSignals, signal =>
                signal.Code.Value == ProcessBranchSignalCodes.Outcome("feature-repair-applied").Value);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Code.Value == ProcessCompletionDiagnosticCodes.RequiredBranchOutcomeMissing);
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

            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    [],
                    BranchStepContract("scope-clarified")));

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

            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    [],
                    BranchStepContract("scope-clarified")));

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

            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    [],
                    BranchStepContract("slice-accepted")));

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
    public async Task ExecuteAsync_accepts_child_process_ref_grounded_by_hash_verified_required_artifact_content()
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
        var requiredSlotId = ArtifactSlotId.New();
        var baseAssignment = CreateManagedArtifactAssignment(
            "slice-handoff",
            agent.Id,
            requiredArtifactSlotIds: [requiredSlotId]);
        var upstreamRef = $"artifacts/process-runs/{baseAssignment.RunId.Value:D}/steps/implement-code-change.md";
        const string childEvidenceRef = "artifacts/process-runs/3fde01c1-9e62-4448-bbab-ed4e6d7d93b1/steps/feature-handoff.md";
        var assignment = baseAssignment with
        {
            Prompt = "Read the required upstream implementation evidence before handoff."
        };
        var upstreamContent = $$"""
            # Implement code change

            ## Child evidence

            - `{{childEvidenceRef}}`
            """;
        var executionRunId = Guid.NewGuid();
        var primaryRef = BuildStepArtifactRef(assignment);
        var responseText = $$"""
            {
              "status": "Completed",
              "reason": "Handed off the validated implementation slice.",
              "branchOutcomeKey": "slice-accepted",
              "branchOutcomeTitle": "Slice accepted",
              "evidenceRefs": [
                "{{primaryRef}}"
              ],
              "nextActions": [],
              "humanReadableSummaryMarkdown": "The current slice was accepted from upstream managed evidence."
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
            var upstreamPath = Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                assignment.RunId.Value.ToString("D"),
                "steps",
                "implement-code-change.md");
            Directory.CreateDirectory(Path.GetDirectoryName(upstreamPath)!);
            await File.WriteAllTextAsync(
                upstreamPath,
                upstreamContent);

            var artifactPath = Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                assignment.RunId.Value.ToString("D"),
                "steps",
                "slice-handoff.md");
            await File.WriteAllTextAsync(
                artifactPath,
                $$"""
                # Slice handoff

                Status: Completed

                ## Evidence

                The handoff preserves bridged child process evidence at `{{childEvidenceRef}}`.
                """);
            var runtimeState = NewRuntimeState(
                assignment.RunId,
                assignment.RunId,
                ProcessRuntimeStatus.Active,
                assignment,
                ProcessRuntimeStepStatus.Running);
            var runtimeStep = Assert.Single(runtimeState.Steps);
            runtimeState = runtimeState with
            {
                PlanId = assignment.PlanId,
                Steps =
                [
                    runtimeStep with
                    {
                        RequiredArtifactSlots = new HashSet<ArtifactSlotId> { requiredSlotId },
                        ArtifactDescriptors =
                        [
                            new ProcessArtifactSlotDescriptor(
                                requiredSlotId,
                                "implementation-change-set",
                                "implement-code-change",
                                "implementation-change-set",
                                "Implementation change set",
                                "ManagedMarkdown",
                                upstreamRef,
                                ProcessArtifactMaterializationMode.AgentWritten)
                        ]
                    }
                ],
                ConnectedInputArtifacts =
                [
                    new ProcessRuntimeInputArtifactReceipt(
                        assignment.StepInstanceId,
                        requiredSlotId,
                        ProcessArtifactInputAvailability.Available,
                        ProcessStepInstanceId.New(),
                        ArtifactInstanceId.New(),
                        ComputeContentHash(upstreamContent),
                        "sha256:implementation-change-set-connection")
                ]
            };
            var stepContract = ProcessRuntimeArtifactContracts.BuildStepContract(
                runtimeState,
                Assert.Single(runtimeState.Steps)) with
            {
                ConfiguredBranchOutcomeIds = [new BranchOutcomeId("slice-accepted")]
            };

            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(runtimeState),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    [],
                    stepContract));

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
    public async Task ExecuteAsync_preserves_pure_producer_self_evidence_blocker_as_nonterminal()
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
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Empty(result.ProducedArtifacts);
            var artifactPath = Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                assignment.RunId.Value.ToString("D"),
                "steps",
                "feature-intake.md");
            Assert.False(File.Exists(artifactPath));
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task ExecuteAsync_preserves_pure_producer_missing_own_primary_artifact_blocker_as_nonterminal()
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
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(assignment.RunId, assignment.RunId, ProcessRuntimeStatus.Active)),
                workspaceFiles);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Empty(result.ProducedArtifacts);
            var artifactPath = Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                assignment.RunId.Value.ToString("D"),
                "steps",
                "feature-intake.md");
            Assert.False(File.Exists(artifactPath));
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
        Guid? currentExecutionRunId = null,
        ProcessStepExecutionContract? stepContract = null)
    {
        var externalTargets = TestExternalTargetPathRegistry.Create();
        (assignment, toolReceipts) = BindProductRootAlias(assignment, toolReceipts, externalTargets);
        var workspaceFiles = TestWorkspaceServices.CreateFileService(
            Path.GetTempPath(),
            externalTargetRegistry: externalTargets);
        var toolReceiptPolicies = CreateToolReceiptPolicyCatalog();
        var completionIssueResultFactory = new ProcessCompletionIssueResultFactory(
            workspaceFiles,
            new ProcessCompletionDefectEvidenceCatalog(
            [
                new BrowserConsoleDefectEvidenceContribution(),
                new BrowserObservedDefectEvidenceContribution()
            ]));
        var completionGateEvaluator = new ProcessCompletionGateFactory(
                toolReceiptPolicies,
                new ProcessToolReceiptEvidenceGate(workspaceFiles, []),
                [
                    new WorkspaceProductSourceInspectionCompletionGateContribution(),
                    new WorkspaceProductFilesystemCompletionGateContribution(
                        completionIssueResultFactory.ProductCompletionPathGate),
                    new BrowserRuntimeLifecycleCompletionGateContribution(),
                    new BrowserInteractiveAcceptanceCompletionGateContribution()
                ],
                completionIssueResultFactory,
                new ProcessOutcomeGroundingValidator(workspaceFiles))
            .CreateCompletionGateEvaluator();
        var resultConverter = new ProcessExecutionResultConverter(
            completionGateEvaluator,
            toolReceiptPolicies,
            completionIssueResultFactory);
        var effectiveExecutionRunId = currentExecutionRunId ?? toolReceipts?.FirstOrDefault()?.ExecutionRunId;
        var effectiveStepContract = stepContract ??
            (string.IsNullOrWhiteSpace(output.BranchOutcomeKey)
                ? ProcessStepExecutionContract.Empty
                : BranchStepContract(output.BranchOutcomeKey));
        return resultConverter.ToAdapterResult(
            assignment,
            output,
            ComputeContentHash("raw-output"),
            toolReceipts,
            effectiveExecutionRunId,
            stepContract: effectiveStepContract);
    }

    private static (
        ProcessRuntimeStepAssignment Assignment,
        IReadOnlyList<ToolExecutionReceiptRecord>? ToolReceipts) BindProductRootAlias(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        IExternalTargetPathRegistry externalTargets)
    {
        var physicalProductRoot = assignment.LaunchVariables.GetValueOrDefault("ProductRoot");
        var configuredAlias = assignment.LaunchVariables.GetValueOrDefault(
            ProcessRuntimeLaunchVariables.ProductRootAlias);
        if (string.IsNullOrWhiteSpace(physicalProductRoot) ||
            string.IsNullOrWhiteSpace(configuredAlias) ||
            !externalTargets.TryCreateAlias(physicalProductRoot, out var boundAlias))
        {
            return (assignment, toolReceipts);
        }

        assignment = assignment with
        {
            LaunchVariables = WithLaunchVariables(
                assignment,
                (ProcessRuntimeLaunchVariables.ProductRootAlias, boundAlias))
        };
        if (toolReceipts is null)
        {
            return (assignment, null);
        }

        toolReceipts = toolReceipts
            .Select(receipt => receipt with
            {
                RequestSummary = ReplaceAliasPrefix(
                    receipt.RequestSummary,
                    configuredAlias,
                    boundAlias)
            })
            .ToArray();
        return (assignment, toolReceipts);
    }

    private static string ReplaceAliasPrefix(string value, string configuredAlias, string boundAlias)
    {
        if (string.Equals(value, configuredAlias, StringComparison.OrdinalIgnoreCase))
        {
            return boundAlias;
        }

        var prefix = configuredAlias.TrimEnd('/', '\\') + "/";
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? boundAlias + "/" + value[prefix.Length..]
            : value;
    }

    private static string DescribeResult(ProcessExecutionAdapterResult result)
        => $"Outcome={result.Outcome}; Summary={result.UserSafeSummary}; Diagnostics=[{string.Join("; ", result.Diagnostics.Select(diagnostic => $"{diagnostic.Code.Value}: {diagnostic.SafeSummary}"))}]";

    private static ProcessStepExecutionContract BranchStepContract(params string[] branchOutcomeKeys)
        => ProcessStepExecutionContract.Empty with
        {
            ConfiguredBranchOutcomeIds = branchOutcomeKeys
                .Select(key => new BranchOutcomeId(key))
                .ToArray()
        };

    private static ProcessExecutionAdapterRequest CreateAdapterRequest(
        ProcessRuntimeStepAssignment assignment,
        ProcessExecutionAdapterKind kind,
        ProcessExecutionAdapterOperationKey operationKey,
        ProcessStrategyBindingSnapshot binding,
        IReadOnlyList<StrategyBindingInput> inputs,
        IReadOnlyList<ProcessExecutionContextFacet> contextFacets,
        ProcessStepExecutionContract? stepContract = null)
    {
        var effectiveStepContract = stepContract ?? ProcessStepExecutionContract.Empty;
        if (stepContract is null)
        {
            effectiveStepContract = effectiveStepContract with
            {
                RequiredRuntimeToolNames = ProcessSubprocessCompletionPolicy
                    .ResolvePreflightRequiredRuntimeToolNames(assignment, effectiveStepContract)
            };
        }

        return CreateAdapterRequest(
            assignment.RunId,
            assignment.StepInstanceId,
            kind,
            operationKey,
            binding,
            inputs,
            contextFacets,
            effectiveStepContract);
    }

    private static ProcessExecutionAdapterRequest CreateAdapterRequest(
        ProcessRunId runId,
        ProcessStepInstanceId? stepId,
        ProcessExecutionAdapterKind kind,
        ProcessExecutionAdapterOperationKey operationKey,
        ProcessStrategyBindingSnapshot binding,
        IReadOnlyList<StrategyBindingInput> inputs,
        IReadOnlyList<ProcessExecutionContextFacet> contextFacets,
        ProcessStepExecutionContract? stepContract = null)
    {
        return new ProcessExecutionAdapterRequest(
            runId,
            stepId,
            kind,
            operationKey,
            binding,
            inputs,
            contextFacets)
        {
            DispatchClaimIdentity = new ProcessDispatchClaimIdentity(Guid.NewGuid()),
            StepContract = stepContract ?? ProcessStepExecutionContract.Empty
        };
    }

    private static ProcessStepExecutionContract ResolvePromptStepContract(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepExecutionContract stepContract)
    {
        return stepContract with
        {
            RequiredRuntimeToolNames = ProcessSubprocessCompletionPolicy
                .ResolvePreflightRequiredRuntimeToolNames(assignment, stepContract)
        };
    }

    private static AgentProcessRoleReadinessRequest CreateRuntimeReadinessRequest(ProcessRuntimeStepAssignment assignment)
        => ProcessExecutionResultFactory.CreateRuntimeReadinessRequest(assignment);

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

    private static (ProcessRuntimeStepAssignment Assignment, AgentDefinition Agent) CreateRuntimeOwnedProductAssignment(
        string outputRoot,
        IReadOnlyList<string>? allowedOperations = null)
    {
        var agent = NewAgent(
            ".NET Application Developer",
            ".NET developer",
            AgentWorkloadKind.Programming,
            [
                "dotnet",
                "developer",
                "implementation"
            ],
            AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var baseAssignment = CreateProductMutationAssignment(outputRoot);
        return (
            baseAssignment with
            {
                StepKey = "verify-existing-dotnet-solution",
                ExecutorId = agent.Id.ToString("D"),
                ExecutorDisplayName = agent.Name,
                AllowedOperations = allowedOperations ??
                    [
                        ProcessOperationContractNames.MutateProductTarget,
                        ProcessOperationContractNames.WriteManagedProcessArtifacts
                    ],
                LaunchVariables = WithLaunchVariables(
                    baseAssignment,
                    (ProcessRuntimeLaunchVariables.ProcessStepRuntimeOwnedExecutorKey, FakeRuntimeOwnedStepExecutor.RuntimeOwnedExecutorKey))
            },
            agent);
    }

    private static IReadOnlyList<ToolExecutionReceiptRecord> CreateReadOnlyVerificationReceipts(
        Guid executionRunId,
        string outputRoot)
        =>
        [
            CreateToolReceipt(
                "workspace_stat_path",
                "external-target/product/Calculator.sln",
                "Succeeded: Product solution exists.",
                workingDirectory: outputRoot,
                executionRunId: executionRunId),
            CreateToolReceipt(
                "workspace_read_file",
                "external-target/product/Calculator.sln",
                "Succeeded: Read product solution.",
                workingDirectory: outputRoot,
                executionRunId: executionRunId)
        ];

    private static ProcessRuntimeOwnedStepExecutionResult CreateSuccessfulRuntimeOwnedStepResult(
        Guid executionRunId,
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts,
        ProcessRuntimeOwnedCompletionScope? completionScope = null)
        => new(
            true,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Runtime-owned product verification completed.",
                EvidenceRefs = ["artifacts/process-runs/runtime-owned-product-verification.json"],
                NextActions = [],
                HumanReadableSummaryMarkdown = "Runtime-owned product verification completed."
            },
            toolReceipts,
            executionRunId,
            "Runtime-owned product verification completed.",
            "runtime-owned-product-verification:completed",
            EffectiveCompletionScope: completionScope);

    private static async Task<(
        ProcessExecutionAdapterResult Result,
        bool AgentWasInvoked,
        int RuntimeExecutorCallCount)> ExecuteRuntimeOwnedStepAsync(
        ProcessRuntimeStepAssignment assignment,
        AgentDefinition agent,
        ProcessRuntimeOwnedStepExecutionResult runtimeResult)
    {
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("The agent must not run when the runtime-owned executor handles the step."));
        var runtimeExecutor = new FakeRuntimeOwnedStepExecutor(runtimeResult);
        var workspaceFiles = CreateWorkspaceFileService(out var workspaceRoot);
        try
        {
            var adapter = CreateAdapter(
                new FakeWorkspaceFactory(workspace),
                CreateReferenceDataProvider(workspace),
                new InMemoryAssignmentStore(assignment),
                new InMemoryRuntimeStateStore(NewRuntimeState(
                    assignment.RunId,
                    assignment.RunId,
                    ProcessRuntimeStatus.Active)),
                workspaceFiles,
                runtimeOwnedStepExecutors: [runtimeExecutor]);

            var result = await adapter.ExecuteAsync(
                CreateAdapterRequest(
                    assignment,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    []));
            return (result, workspace.ExecuteRunCalled, runtimeExecutor.CallCount);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    private static ProcessRuntimeStepAssignment CreateQaValidationAssignmentWithBranchAwareCompletionRules(
        string outputRoot,
        string scaffoldPath)
    {
        var scaffoldPathFromProductRoot = Path.GetRelativePath(outputRoot, scaffoldPath)
            .Replace('\\', '/');
        var baseAssignment = CreateManagedArtifactAssignment(
            "qa-validation",
            allowedOperations:
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.LaunchRuntime,
                ProcessOperationContractNames.CaptureRuntimeProof,
                ProcessOperationContractNames.WriteManagedProcessArtifacts
            ]);
        var receiptRules = JsonSerializer.Serialize(new Dictionary<string, object[]>(StringComparer.Ordinal)
        {
            ["qa-validation"] =
            [
                BranchAwareReceipt("workspace_dotnet_restore", "quality-accepted"),
                BranchAwareReceipt("workspace_dotnet_build", "quality-accepted"),
                BranchAwareReceipt("workspace_dotnet_test", "quality-accepted"),
                BranchAwareReceipt("workspace_dotnet_run", "quality-accepted"),
                BranchAwareReceipt("browser_navigate", "quality-accepted"),
                BranchAwareReceipt("browser_evaluate", "quality-accepted"),
                BranchAwareReceipt("browser_snapshot", "quality-accepted"),
                BranchAwareReceipt("browser_take_screenshot", "quality-accepted"),
                BranchAwareReceipt("browser_console_messages", "quality-accepted"),
                BranchAwareReceipt("workspace_dotnet_stop", "quality-accepted")
            ]
        });
        var contentChecks = JsonSerializer.Serialize(new Dictionary<string, object[]>(StringComparer.Ordinal)
        {
            ["qa-validation"] =
            [
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["pathCandidates"] = new[] { scaffoldPathFromProductRoot },
                    ["mustExist"] = false,
                    ["enforceBranchOutcomeKeys"] = new[] { "quality-accepted" },
                    ["evidenceBranchOutcomeKeys"] = new[] { "repair-required" },
                    ["forbiddenTextAny"] = new[] { "@page \"/counter\"" }
                }
            ]
        });
        var routes = JsonSerializer.Serialize(new Dictionary<string, object[]>(StringComparer.Ordinal)
        {
            ["qa-validation"] =
            [
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["issueCode"] = "process.adapter.product_required_file_content_missing",
                    ["sourceBranchOutcomeKeys"] = new[] { "quality-accepted" },
                    ["targetBranchOutcomeKey"] = "repair-required",
                    ["targetBranchOutcomeTitle"] = "Repair required",
                    ["requiresDefectEvidence"] = true
                },
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["issueCode"] = ProcessCompletionDiagnosticCodes.ProductSourceInspectionEvidenceMissing,
                    ["sourceBranchOutcomeKeys"] = new[] { "quality-accepted" },
                    ["targetBranchOutcomeKey"] = "repair-required",
                    ["targetBranchOutcomeTitle"] = "Repair required",
                    ["requiresDefectEvidence"] = true
                },
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["issueCode"] = ProcessCompletionDiagnosticCodes.UiInteractionEvidenceMissing,
                    ["sourceBranchOutcomeKeys"] = new[] { "quality-accepted" },
                    ["targetBranchOutcomeKey"] = "repair-required",
                    ["targetBranchOutcomeTitle"] = "Repair required",
                    ["requiresDefectEvidence"] = true
                },
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["issueCode"] = ProcessCompletionDiagnosticCodes.UiPostInteractionStateEvidenceMissing,
                    ["sourceBranchOutcomeKeys"] = new[] { "quality-accepted" },
                    ["targetBranchOutcomeKey"] = "repair-required",
                    ["targetBranchOutcomeTitle"] = "Repair required",
                    ["requiresDefectEvidence"] = true
                }
            ]
        });

        return baseAssignment with
        {
            Prompt = """
            Branch outcomes:
            - quality-accepted: Quality accepted - validation and product acceptance proof passed.
            - repair-required: Repair required - deterministic product defect evidence requires repair.
            """,
            LaunchVariables = WithLaunchVariables(
                baseAssignment,
                ("ProductRoot", outputRoot),
                (ProcessRuntimeLaunchVariables.ProductRootAlias, "external-target/product"),
                (ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep, receiptRules),
                (ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep, contentChecks),
                (ProcessRuntimeLaunchVariables.CompletionIssueRoutesByStep, routes)),
            CapabilityScope = new ProcessCapabilityScope
            {
                RequiredReceipts =
                [
                    CapabilityReceipt("workspace_dotnet_run", "quality-accepted"),
                    CapabilityReceipt("browser_evaluate", "quality-accepted"),
                    CapabilityReceipt("browser_take_screenshot", "quality-accepted"),
                    CapabilityReceipt("workspace_dotnet_stop", "quality-accepted")
                ]
            }
        };
    }

    private static ProcessRuntimeStepAssignment CreateQaValidationAssignmentWithAcceptanceMatrix(
        string outputRoot,
        string scaffoldPath)
    {
        var assignment = CreateQaValidationAssignmentWithBranchAwareCompletionRules(outputRoot, scaffoldPath);
        var matrix = new ProcessAcceptanceCriteriaMatrix
        {
            Criteria =
            [
                new ProcessAcceptanceCriterion
                {
                    Id = "AC-001",
                    SourceNodeId = "custom:tetris",
                    Summary = "support falling blocks with movement and rotation",
                    VerificationMethods = ["browser-proof"],
                    RequiredForAcceptance = true
                },
                new ProcessAcceptanceCriterion
                {
                    Id = "AC-002",
                    SourceNodeId = "custom:tetris",
                    Summary = "clear completed lines and update score",
                    VerificationMethods = ["unit-test"],
                    RequiredForAcceptance = true
                }
            ]
        };

        return assignment with
        {
            LaunchVariables = WithLaunchVariables(
                assignment,
                (ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix, ProcessAcceptanceCriteriaMatrixJson.Serialize(matrix)),
                (ProcessRuntimeLaunchVariables.AcceptanceCriteriaAcceptedBranchOutcomeKeys, "quality-accepted"))
        };
    }

    private static Dictionary<string, object> BranchAwareReceipt(
        string toolName,
        string branchOutcomeKey)
        => new(StringComparer.Ordinal)
        {
            ["toolName"] = toolName,
            ["purpose"] = "AcceptanceProof",
            ["applicableBranchOutcomeKeys"] = new[] { branchOutcomeKey }
        };

    private static ProcessRequiredToolReceipt CapabilityReceipt(
        string toolName,
        string branchOutcomeKey)
        => new()
        {
            Key = $"qa:{branchOutcomeKey}:{toolName}",
            Kind = ProcessRequiredToolReceiptKind.RuntimeToolName,
            ToolName = toolName,
            Purpose = ProcessRequiredToolReceiptPurpose.AcceptanceProof,
            ApplicableBranchOutcomeKeys = [branchOutcomeKey],
            Reason = $"Required for branch '{branchOutcomeKey}'."
        };

    private static IReadOnlyList<ToolExecutionReceiptRecord> CreateFullQaValidationReceipts(
        string primaryRef,
        Guid executionRunId,
        string hostUrl = "http://127.0.0.1:5173",
        string? startupReceipt = null,
        string? browserHostUrl = null,
        Guid? runtimeExecutionRunId = null,
        string stopExitSummary = "Succeeded (exit 0)")
    {
        startupReceipt ??= $"artifacts/process-runs/{Guid.NewGuid():D}/tool-runs/dotnet-run/startup.json";
        var runtimeRunId = runtimeExecutionRunId ?? executionRunId;
        browserHostUrl ??= hostUrl;
        var stepPathIndex = primaryRef.IndexOf("/steps/", StringComparison.Ordinal);
        var processRunArtifactRoot = primaryRef[..stepPathIndex];
        var postInteractionStateRef = $"{processRunArtifactRoot}/browser/post-interaction.json";
        return
        [
            CreateToolReceipt("workspace_dotnet_restore", "restore app", "Succeeded (exit 0)", executionRunId: executionRunId),
            CreateToolReceipt("workspace_dotnet_build", "build app", "Succeeded (exit 0)", executionRunId: executionRunId),
            CreateToolReceipt("workspace_dotnet_test", "test app", "Succeeded (exit 0)", executionRunId: executionRunId),
            CreateToolReceipt("workspace_read_file", "external-target/product/src/App/Pages/Home.razor", "Succeeded: Read product source.", executionRunId: executionRunId),
            CreateToolReceipt("workspace_dotnet_run", $"run app; startupReceipt={startupReceipt}; hostUrl={hostUrl}", "Succeeded (exit 0)", executionRunId: runtimeRunId),
            CreateToolReceipt("browser_navigate", $"open app {browserHostUrl}", "Succeeded (exit 0)", executionRunId: executionRunId),
            CreateToolReceipt("browser_snapshot", $"snapshot app {browserHostUrl}", "Succeeded (exit 0)", executionRunId: executionRunId),
            CreateToolReceipt("browser_press_key", "key=ArrowRight", "Succeeded (exit 0)", executionRunId: executionRunId),
            CreateToolReceipt("browser_evaluate", $"filename={postInteractionStateRef}; url={browserHostUrl}", "Succeeded (exit 0)", executionRunId: executionRunId),
            CreateToolReceipt("browser_take_screenshot", $"screenshot app {browserHostUrl}", "Succeeded (exit 0)", executionRunId: executionRunId),
            CreateToolReceipt("browser_console_messages", $"read console {browserHostUrl}", "Succeeded (exit 0)", executionRunId: executionRunId),
            CreateToolReceipt("workspace_dotnet_stop", $"stop app; startupReceipt={startupReceipt}; hostUrl={hostUrl}", stopExitSummary, executionRunId: executionRunId),
            CreateToolReceipt("workspace_write_file", primaryRef, "Succeeded: Wrote QA evidence.", executionRunId: executionRunId)
        ];
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

    private static IReadOnlyDictionary<string, string> CreateDotNetCreateProjectLaunchVariables(
        string productRoot,
        IReadOnlyList<string>? requiredReceipts = null)
    {
        var solutionFile = Path.Combine(productRoot, "Calculator.slnx");
        var appProjectFile = Path.Combine(productRoot, "src", "Calculator", "Calculator.csproj");
        var scriptRef = $"artifacts/process-runs/{Guid.NewGuid():D}/scripts/create-dotnet-project.wire-solution.ps1";
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OutputRoot"] = productRoot,
            ["ProductRoot"] = productRoot,
            ["ExternalTargetRoot"] = productRoot,
            ["DotNetAppTemplate"] = "blazorwasm",
            ["DotNetCreateProjectScriptRef"] = scriptRef,
            ["DotNetCreateProjectScript"] = "dotnet sln $SolutionFile add $AppProjectFile; dotnet sln $SolutionFile list",
            ["DotNetCreateProjectSideEffectManifest"] = JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["version"] = 1,
                ["mode"] = "ProductMutation",
                ["declaredReadPaths"] = new[] { solutionFile, appProjectFile },
                ["declaredWritePaths"] = new[] { solutionFile },
                ["allowShellDelegation"] = true
            }),
            ["DotNetCreateProjectExecutionPlan"] =
                JsonSerializer.Serialize(new
                {
                    PlanKey = "dotnet.create-project",
                    ScriptRef = scriptRef,
                    WorkspaceAlias = "external-target/calculator",
                    RequiresScaffold = true
                }),
            [ProcessRuntimeLaunchVariables.ProcessStepScriptHelperDescriptorJson] =
                ProcessRuntimeLaunchVariables.SerializeProcessStepScriptHelperDescriptor(
                    new ProcessRuntimeScriptHelperDescriptor(
                        "DotNetCreateProjectScript",
                        "DotNetCreateProjectScriptRef",
                        "DotNetCreateProjectSideEffectManifest",
                        "dotnet.create-project",
                        "DotNetSolutionCreate",
                        "DotNetCreateProjectExecutionPlan")),
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] = JsonSerializer.Serialize(
                (requiredReceipts ??
                [
                    "template=sln",
                    "template=blazorwasm",
                    "workspace_pwsh_run_script"
                ]).ToArray()),
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths] = JsonSerializer.Serialize(
                new[]
                {
                    solutionFile,
                    appProjectFile
                }),
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks] = JsonSerializer.Serialize(
                new object[]
                {
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["pathCandidates"] = new[] { solutionFile },
                        ["requiredTextAnyGroups"] = new[] { new[] { "src/Calculator/Calculator.csproj" } }
                    }
                })
        };
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
                [ProcessRuntimeLaunchVariables.ProcessStepSubprocessDefinitionKey] = "dotnet-development-slice",
                [ProcessRuntimeLaunchVariables.ProcessStepSubprocessContractJson] =
                    ProcessRuntimeLaunchVariables.SerializeProcessStepSubprocessContract(
                        new ProcessSubprocessContract
                        {
                            DefinitionKey = "dotnet-development-slice",
                            LaunchMode = ProcessSubprocessLaunchMode.RuntimeOwned,
                            ParentProducedArtifactExpectationKey = "implementation-change-set",
                            AcceptedChildOutputs =
                            [
                                new ProcessSubprocessChildOutputContract
                                {
                                    StepKey = "slice-handoff",
                                    ArtifactExpectationKey = "slice-handoff-packet",
                                    ArtifactTitle = "Implementation slice handoff packet"
                                },
                                new ProcessSubprocessChildOutputContract
                                {
                                    StepKey = "slice-handoff-after-repair",
                                    ArtifactExpectationKey = "slice-handoff-packet-after-repair",
                                    ArtifactTitle = "Implementation slice handoff packet after repair"
                                },
                                new ProcessSubprocessChildOutputContract
                                {
                                    StepKey = "slice-handoff-after-manager-repair",
                                    ArtifactExpectationKey = "slice-handoff-packet-after-manager-repair",
                                    ArtifactTitle = "Implementation slice handoff packet after manager-assisted repair"
                                }
                            ],
                            MaterializationMode = ProcessSubprocessMaterializationMode.RuntimeSynthesizedParentHandoff
                        })
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

    private static IWorkspaceFileService CreateWorkspaceFileService(
        out string workspaceRoot,
        IExternalTargetPathRegistry? externalTargetRegistry = null)
    {
        workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.ProcessAdapter.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        return TestWorkspaceServices.CreateFileService(
            workspaceRoot,
            externalTargetRegistry: externalTargetRegistry ?? TestExternalTargetPathRegistry.Create());
    }

    private static AgentFrameworkProcessExecutionAdapter CreateAdapter(
        ICanDoItAllAgentWorkspaceFactory workspaceFactory,
        IAgentReferenceDataProvider agentReferenceDataProvider,
        IProcessRuntimeStepAssignmentStore assignmentStore,
        IProcessRuntimeStateStore stateStore,
        IWorkspaceFileService workspaceFiles,
        IEnumerable<IProcessSubprocessLaunchCoordinator>? subprocessLaunchCoordinators = null,
        IProcessRuntimeToolPreflightService? runtimeToolPreflightService = null,
        IParentSubprocessArtifactBridge? parentSubprocessArtifactBridge = null,
        IEnumerable<IProcessRuntimeOwnedStepExecutor>? runtimeOwnedStepExecutors = null,
        IProcessWorkflowStepExecutor? workflowStepExecutor = null)
    {
        var toolReceiptPolicies = CreateToolReceiptPolicyCatalog();
        var completionIssueResultFactory = new ProcessCompletionIssueResultFactory(
            workspaceFiles,
            new ProcessCompletionDefectEvidenceCatalog(
            [
                new BrowserConsoleDefectEvidenceContribution(),
                new BrowserObservedDefectEvidenceContribution()
            ]));
        var completionGateEvaluator = new ProcessCompletionGateFactory(
                toolReceiptPolicies,
                new ProcessToolReceiptEvidenceGate(
                    workspaceFiles,
                    []),
                [
                    new WorkspaceProductSourceInspectionCompletionGateContribution(),
                    new WorkspaceProductFilesystemCompletionGateContribution(
                        completionIssueResultFactory.ProductCompletionPathGate),
                    new DotNetSolutionContextCompletionGateContribution(workspaceFiles),
                    new BrowserRuntimeLifecycleCompletionGateContribution(),
                    new BrowserInteractiveAcceptanceCompletionGateContribution()
                ],
                completionIssueResultFactory,
                new ProcessOutcomeGroundingValidator(workspaceFiles))
            .CreateCompletionGateEvaluator();
        var resultConverter = new ProcessExecutionResultConverter(
            completionGateEvaluator,
            toolReceiptPolicies,
            completionIssueResultFactory);
        var subprocessContractResolver = new ProcessSubprocessContractResolver();
        var managedArtifactService = new ProcessManagedArtifactService(workspaceFiles);
        var groundingValidator = new ProcessOutcomeGroundingValidator(workspaceFiles);
        var completionCoordinator = new ProcessStepCompletionCoordinator(
            completionIssueResultFactory,
            managedArtifactService,
            groundingValidator,
            completionGateEvaluator,
            resultConverter,
            NullLogger<ProcessStepCompletionCoordinator>.Instance);
        var subprocessArtifactBridge = parentSubprocessArtifactBridge ??
            new ParentSubprocessArtifactBridge(
                assignmentStore,
                stateStore,
                workspaceFiles,
                subprocessContractResolver);
        var subprocessCoordinator = new ProcessSubprocessCoordinator(
            subprocessLaunchCoordinators ?? [],
            subprocessArtifactBridge,
            completionCoordinator);
        var runtimeOwnedStepCoordinator = new ProcessRuntimeOwnedStepCoordinator(
            runtimeOwnedStepExecutors ?? [],
            completionCoordinator,
            toolReceiptPolicies);
        var effectiveRuntimeToolPreflightService = runtimeToolPreflightService ??
            new FakeRuntimeToolPreflightService(ProcessRuntimeToolPreflightResult.Satisfied);
        var executor = new AgentFrameworkProcessStepExecutor(
            workspaceFactory,
            agentReferenceDataProvider,
            assignmentStore,
            stateStore,
            workflowStepExecutor ?? new UnexpectedProcessWorkflowStepExecutor(),
            effectiveRuntimeToolPreflightService,
            runtimeOwnedStepCoordinator,
            subprocessCoordinator,
            completionCoordinator,
            subprocessContractResolver,
            new ProcessParentSubprocessArtifactContextHydrator(workspaceFiles),
            new ProcessExecutionMetadataComposer(
            [
                new BrowserExecutionMetadataContribution()
            ]));
        return new AgentFrameworkProcessExecutionAdapter(executor);
    }

    private sealed class UnexpectedProcessWorkflowStepExecutor : IProcessWorkflowStepExecutor
    {
        public ValueTask<ProcessExecutionAdapterResult> ExecuteAsync(
            ProcessRuntimeStepAssignment assignment,
            ProcessStepExecutionContract stepContract,
            CancellationToken cancellationToken = default,
            Func<CancellationToken, ValueTask<ProcessExecutionAdapterResult?>>? beforeLaunch = null)
            => throw new InvalidOperationException(
                $"Agent integration test unexpectedly dispatched workflow assignment '{assignment.StepKey}'.");
    }

    private sealed class RecordingProcessWorkflowStepExecutor(ProcessExecutionAdapterResult result) :
        IProcessWorkflowStepExecutor
    {
        public ProcessRuntimeStepAssignment? Assignment { get; private set; }

        public ProcessStepExecutionContract? StepContract { get; private set; }

        public int ExecutionCount { get; private set; }

        public async ValueTask<ProcessExecutionAdapterResult> ExecuteAsync(
            ProcessRuntimeStepAssignment assignment,
            ProcessStepExecutionContract stepContract,
            CancellationToken cancellationToken = default,
            Func<CancellationToken, ValueTask<ProcessExecutionAdapterResult?>>? beforeLaunch = null)
        {
            Assignment = assignment;
            StepContract = stepContract;
            if (beforeLaunch is not null &&
                await beforeLaunch(cancellationToken) is { } blocked)
            {
                return blocked;
            }

            ExecutionCount++;
            return result;
        }
    }

    private static ProcessToolReceiptPolicyCatalog CreateToolReceiptPolicyCatalog()
        => new(
        [
            new GenericWorkspaceToolReceiptPolicyContribution(),
            new BrowserInteractionToolReceiptPolicyContribution(),
            new DotNetToolReceiptPolicyContribution()
        ]);

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
                 0)
             {
                 ExecutionRunId = executionRunId
             });

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

    private static ExecutionRunDetail CreateFailedExecutionRunDetail(
        ProcessRuntimeStepAssignment assignment,
        Guid executionRunId,
        string responseText,
        AgentTransientDurableSignal signal = AgentTransientDurableSignal.None)
    {
        var agentId = Guid.Parse(assignment.ExecutorId);
        var now = DateTimeOffset.UtcNow;
        var metric = CreateExecutionRunResult(
            agentId,
            executionRunId,
            responseText,
            RunOutcome.Failed).Metric;
        var usageObservation = new ProviderUsageObservation(
            Guid.NewGuid(),
            now,
            "test-provider",
            ProviderKind.OpenAi,
            "test-model",
            ProviderTransportKind.Responses,
            ProviderUsageSourcePhases.LegacyAgentRunMetric,
            ProviderUsageObservationStatus.EstimatedFromMetric,
            10,
            0,
            0,
            0,
            10,
            0)
        {
            ExecutionRunId = executionRunId,
            AgentId = agentId,
            ProcessRunId = assignment.RunId.Value.ToString("D"),
            ProcessStepId = assignment.StepInstanceId.Value.ToString("D"),
            CorrelationId = assignment.RunId.Value.ToString("D")
        };
        var baseDetail = CreateExecutionRunDetail(agentId, executionRunId, responseText, []);
        var detail = baseDetail with
        {
            Run = baseDetail.Run with
            {
                SourceKind = ProcessMockAgentCatalog.ProcessSourceKind,
                SourceId = assignment.StepKey,
                CorrelationId = assignment.RunId.Value.ToString("D"),
                CausationId = assignment.StepInstanceId.Value.ToString("D"),
                RequestedBy = "process-runtime",
                RequestedByKind = "system",
                State = ExecutionState.Failed,
                Outcome = RunOutcome.Failed,
                CompletedAtUtc = now,
                ProcessRunId = assignment.RunId.Value.ToString("D"),
                ProcessStepId = assignment.StepInstanceId.Value.ToString("D")
            },
            ExecutionLog =
            [
                new ExecutionLogEntry(
                    Guid.NewGuid(),
                    agentId,
                    null,
                    now,
                    ExecutionState.Failed,
                    "Failed",
                    responseText)
                {
                    ExecutionRunId = executionRunId
                }
            ],
            Metrics = [metric],
            UsageObservations = [usageObservation]
        };

        return signal switch
        {
            AgentTransientDurableSignal.None => detail,
            AgentTransientDurableSignal.ImmediateMetricToolCall => detail,
            AgentTransientDurableSignal.RunNotFailed => detail with
            {
                Run = detail.Run with
                {
                    State = ExecutionState.Completed,
                    Outcome = RunOutcome.Succeeded
                }
            },
            AgentTransientDurableSignal.WrongProcessIdentity => detail with
            {
                Run = detail.Run with
                {
                    ProcessStepId = Guid.NewGuid().ToString("D")
                }
            },
            AgentTransientDurableSignal.MissingTerminalMetric => detail with
            {
                Metrics = []
            },
            AgentTransientDurableSignal.MissingTerminalLog => detail with
            {
                ExecutionLog = []
            },
            AgentTransientDurableSignal.MetricToolCall => detail with
            {
                Metrics = [metric with { ToolCalls = 1 }]
            },
            AgentTransientDurableSignal.UsageToolCall => detail with
            {
                UsageObservations = [usageObservation with { ToolCallCount = 1 }]
            },
            AgentTransientDurableSignal.ToolReceipt => detail with
            {
                ToolReceipts =
                [
                    CreateToolReceipt(
                        "workspace_pwsh_run_script",
                        "mutate product",
                        "Succeeded",
                        executionRunId: executionRunId)
                ]
            },
            AgentTransientDurableSignal.Artifact => detail with
            {
                Artifacts =
                [
                    new ExecutionArtifactRecord(
                        Guid.NewGuid(),
                        executionRunId,
                        "test",
                        "Test artifact",
                        "artifacts/test.md",
                        "text/markdown",
                        "test",
                        "Durable artifact",
                        now)
                ]
            },
            AgentTransientDurableSignal.Checkpoint => detail with
            {
                Checkpoints =
                [
                    new ExecutionWorkflowCheckpointRecord(
                        Guid.NewGuid(),
                        executionRunId,
                        "workflow-session",
                        "checkpoint",
                        "test",
                        ExecutionState.Failed,
                        [],
                        now,
                        null,
                        assignment.RunId.Value.ToString("D"),
                        ProcessMockAgentCatalog.ProcessSourceKind,
                        assignment.StepKey,
                        assignment.RunId.Value.ToString("D"),
                        assignment.StepInstanceId.Value.ToString("D"),
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty)
                ]
            },
            AgentTransientDurableSignal.Approval => detail with
            {
                Approvals =
                [
                    new ExecutionApprovalRecord(
                        "approval",
                        executionRunId,
                        "call",
                        "workspace_pwsh_run_script",
                        "workspace",
                        "Run a script",
                        "{}",
                        ExecutionApprovalStatus.Pending,
                        now,
                        null,
                        string.Empty,
                        string.Empty,
                        string.Empty)
                ]
            },
            AgentTransientDurableSignal.RunPendingApproval => detail with
            {
                Run = detail.Run with
                {
                    PendingApprovals =
                    [
                        new PendingToolApprovalRecord(
                            "approval",
                            "call",
                            "workspace_pwsh_run_script",
                            "workspace",
                            "Run a script",
                            "{}")
                    ]
                }
            },
            AgentTransientDurableSignal.StructuredOutput => detail with
            {
                Run = detail.Run with
                {
                    StructuredOutputRawOutput = "{}"
                }
            },
            AgentTransientDurableSignal.StructuredOutputValidation => detail with
            {
                Run = detail.Run with
                {
                    StructuredOutputValidationStatus = "Invalid",
                    StructuredOutputValidationErrorsJson = """["invalid"]"""
                }
            },
            AgentTransientDurableSignal.SerializedSessionState => detail with
            {
                Run = detail.Run with
                {
                    SerializedSessionStateJson = "{}"
                }
            },
            AgentTransientDurableSignal.RuntimeSessionKey => detail with
            {
                Run = detail.Run with
                {
                    RuntimeSessionKey = "runtime-session"
                }
            },
            AgentTransientDurableSignal.WaitingOnToolLog => detail with
            {
                ExecutionLog =
                [
                    .. detail.ExecutionLog,
                    new ExecutionLogEntry(
                        Guid.NewGuid(),
                        agentId,
                        null,
                        now,
                        ExecutionState.WaitingOnTool,
                        "Tool",
                        "Waiting on tool")
                    {
                        ExecutionRunId = executionRunId
                    }
                ]
            },
            AgentTransientDurableSignal.FailedThenRunningLog => detail with
            {
                Run = detail.Run with
                {
                    CompletedAtUtc = now.AddSeconds(2)
                },
                ExecutionLog =
                [
                    .. detail.ExecutionLog,
                    new ExecutionLogEntry(
                        Guid.NewGuid(),
                        agentId,
                        null,
                        now.AddSeconds(1),
                        ExecutionState.Running,
                        "Running",
                        "Execution resumed after a terminal failure.")
                    {
                        ExecutionRunId = executionRunId
                    }
                ]
            },
            AgentTransientDurableSignal.LogAfterRunCompletion => detail with
            {
                ExecutionLog =
                [
                    detail.ExecutionLog[0] with
                    {
                        CreatedAtUtc = now.AddSeconds(1)
                    }
                ]
            },
            _ => throw new ArgumentOutOfRangeException(nameof(signal), signal, null)
        };
    }

    public enum AgentTransientDurableSignal
    {
        None,
        RunNotFailed,
        WrongProcessIdentity,
        MissingTerminalMetric,
        MissingTerminalLog,
        ImmediateMetricToolCall,
        MetricToolCall,
        UsageToolCall,
        ToolReceipt,
        Artifact,
        Checkpoint,
        Approval,
        RunPendingApproval,
        StructuredOutput,
        StructuredOutputValidation,
        SerializedSessionState,
        RuntimeSessionKey,
        WaitingOnToolLog,
        FailedThenRunningLog,
        LogAfterRunCompletion
    }

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
        ProcessRuntimeStatus status,
        ProcessRuntimeStepAssignment? stepAssignment = null,
        ProcessRuntimeStepStatus stepStatus = ProcessRuntimeStepStatus.Completed,
        IReadOnlyList<StrategyResultReceipt>? appliedResults = null)
    {
        var completedResultKey = stepStatus == ProcessRuntimeStepStatus.Completed
            ? appliedResults?
                .Where(receipt =>
                    receipt.StepInstanceId == stepAssignment?.StepInstanceId &&
                    receipt.AppliedStepStatus == ProcessRuntimeStepStatus.Completed)
                .LastOrDefault()
                ?.IdempotencyKey
            : null;
        return new ProcessRuntimeStateSnapshot(
            rootRunId,
            runId,
            ProcessInstancePlanId.New(),
            "sha256:plan",
            status,
            stepAssignment is null
                ? []
                :
                [
                    new ProcessRuntimeStepState(
                        stepAssignment.StepInstanceId,
                        ProcessStepDefinitionId.New(),
                        stepStatus,
                        IsExecutable: true,
                        AttemptNumber: 1,
                        DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                        RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                        ActiveClaimToken: null,
                        CompletedResultKey: completedResultKey)
                    {
                        ProducedArtifactSlots = stepAssignment.ProducedArtifactSlotIds.ToHashSet()
                    }
                ],
            [],
            appliedResults ?? [],
            appliedResults?
                .SelectMany(receipt => receipt.ProducedArtifacts)
                .Select(artifact => artifact.SlotId)
                .ToHashSet() ?? new HashSet<ArtifactSlotId>(),
            DateTimeOffset.UtcNow);
    }

    private static void AssertPublicAndPersistedReceiptExclude(
        ProcessRuntimeStepAssignment assignment,
        ProcessExecutionAdapterResult result,
        params string[] forbiddenValues)
    {
        var appliedStepStatus = result.Outcome switch
        {
            StrategyOutcome.NeedsManager => ProcessRuntimeStepStatus.Blocked,
            StrategyOutcome.Canceled => ProcessRuntimeStepStatus.Cancelled,
            StrategyOutcome.Failed => ProcessRuntimeStepStatus.Failed,
            _ => throw new InvalidOperationException("The non-disclosure receipt assertion requires a non-success result.")
        };
        var receipt = new StrategyResultReceipt(
            assignment.StepInstanceId,
            Binding.StrategyId,
            StrategyResultIdempotencyKey.New(),
            result.Outcome,
            appliedStepStatus,
            result.ResultHash,
            result.Diagnostics
                .Select(diagnostic => new StrategyResultDiagnosticReceipt(
                    diagnostic.Code.Value,
                    diagnostic.Sensitivity,
                    diagnostic.EvidenceHash,
                    diagnostic.SafeSummary,
                    diagnostic.RestrictedEvidenceReference,
                    diagnostic.RetrySafety,
                    diagnostic.Idempotency)
                {
                    RelatedChildRunId = diagnostic.RelatedChildRunId,
                    ExecutionSafetyAttestation = diagnostic.ExecutionSafetyAttestation
                })
                .ToArray(),
            producedArtifacts: [])
        {
            UserSafeSummary = result.UserSafeSummary,
            ExecutionRunId = result.ExecutionRunId,
            HostCapabilityEvidence = result.HostCapabilityEvidence
        };
        var state = NewRuntimeState(
            assignment.RunId,
            assignment.RunId,
            ProcessRuntimeStatus.Failed,
            assignment,
            appliedStepStatus,
            [receipt]);
        var persistedReceipt = Assert.Single(ProcessPersistenceMappers.ToEntity(state).ResultReceipts);
        var publicJson = JsonSerializer.Serialize(result);
        var persistedJson = $"{persistedReceipt.UserSafeSummary}{Environment.NewLine}{persistedReceipt.DiagnosticsJson}";

        foreach (var forbiddenValue in forbiddenValues)
        {
            Assert.DoesNotContain(forbiddenValue, publicJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(forbiddenValue, persistedJson, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static StrategyResultReceipt CreateProducedArtifactReceipt(
        ProcessRuntimeStepAssignment assignment,
        ArtifactSlotId slotId,
        string content)
        => new(
            assignment.StepInstanceId,
            new StrategyId("strategy.adapter.workflow.execute"),
            StrategyResultIdempotencyKey.New(),
            StrategyOutcome.Succeeded,
            ProcessRuntimeStepStatus.Completed,
            "sha256:accepted-child-output",
            diagnostics: [],
            producedArtifacts:
            [
                new StrategyResultArtifactReceipt(
                    slotId,
                    ArtifactInstanceId.New(),
                    ComputeContentHash(content))
            ]);

    private static string ComputeContentHash(string value)
        => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static StrategyResultReceipt CreateBlockedChildDiagnosticReceipt(ProcessRuntimeStepAssignment assignment)
        => new(
            assignment.StepInstanceId,
            new StrategyId("strategy.adapter.workflow.execute"),
            StrategyResultIdempotencyKey.New(),
            StrategyOutcome.NeedsManager,
            ProcessRuntimeStepStatus.Blocked,
            "sha256:blocked-child",
            diagnostics:
            [
                new StrategyResultDiagnosticReceipt(
                    "process.adapter.product_required_file_content_missing",
                    StrategyDiagnosticSensitivity.Normal,
                    "sha256:child-diagnostic",
                    "Calculator.slnx does not contain src/Calculator/Calculator.csproj and the required workspace_pwsh_run_script receipt is missing.",
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.SafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            producedArtifacts: [],
            recoveryDecision: new ProcessRecoveryDecisionReceipt(
                ProcessFailureCategory.ProductCompletionGate,
                ProcessRecoveryDecisionKind.ManagerRequired,
                "process.adapter.product_required_file_content_missing",
                "process.current-step-safe-retry-budget-exhausted",
                "Child retry budget exhausted.")
            {
                RouteKind = ProcessRecoveryRouteKind.ManagerAction,
                ResponsibleStepInstanceId = assignment.StepInstanceId,
                DiagnosticFingerprint = "sha256:child-diagnostic",
                AutomaticRetryAttempt = 3,
                MaximumAutomaticRetryAttempts = 3,
                SameDiagnosticFingerprintAttempt = 1,
                MaximumSameDiagnosticFingerprintAttempts = 1
            });

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

    private sealed class FakeRuntimeToolPreflightService(
        ProcessRuntimeToolPreflightResult result,
        ProcessRuntimeToolPreflightResult? runtimeToolHostCapabilityResult = null,
        ProcessRuntimeToolPreflightResult? requiredHostCapabilityResult = null) : IProcessRuntimeToolPreflightService
    {
        public List<ProcessRuntimeToolPreflightRequest> Requests { get; } = [];

        public ValueTask<ProcessRuntimeToolPreflightResult> EvaluateAsync(
            ProcessRuntimeToolPreflightRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(result);
        }

        public ValueTask<ProcessRuntimeToolPreflightResult> EvaluateHostCapabilitiesAsync(
            IReadOnlyList<string> requiredRuntimeToolNames,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(
                runtimeToolHostCapabilityResult ?? ProcessRuntimeToolPreflightResult.Satisfied);

        public ValueTask<ProcessRuntimeToolPreflightResult> EvaluateRequiredHostCapabilitiesAsync(
            IReadOnlyCollection<ProcessHostCapabilityId> requiredHostCapabilities,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(
                requiredHostCapabilityResult ?? ProcessRuntimeToolPreflightResult.Satisfied);

        public ValueTask<ProcessRuntimeToolPreflightResult> EvaluateStepHostCapabilitiesAsync(
            IReadOnlyList<string> declaredRuntimeToolNames,
            IReadOnlyList<string> effectiveRuntimeToolNames,
            IReadOnlyCollection<ProcessHostCapabilityId> requiredHostCapabilities,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(
                runtimeToolHostCapabilityResult ??
                requiredHostCapabilityResult ??
                ProcessRuntimeToolPreflightResult.Satisfied);
    }

    private sealed class FakeRuntimeOwnedStepExecutor(ProcessRuntimeOwnedStepExecutionResult? result) : IProcessRuntimeOwnedStepExecutor
    {
        public const string RuntimeOwnedExecutorKey = "test.runtime-owned";

        public string ExecutorKey => RuntimeOwnedExecutorKey;

        public int CallCount { get; private set; }

        public ValueTask<ProcessRuntimeOwnedStepExecutionResult?> TryExecuteAsync(
            ProcessRuntimeStepAssignment assignment,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(result);
        }
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

        public ExecutionRunRequest? LastExecuteRunRequest { get; private set; }

        public List<Guid> ExecutionDetailRequestIds { get; } = [];

        public Task<IReadOnlyList<AgentDefinition>> ListAgentsAsync(
            bool includeTemplates = true,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AgentDefinition>>([agent]);

        public Task<ExecutionRunResult> ExecuteRunAsync(
            ExecutionRunRequest request,
            CancellationToken cancellationToken = default)
        {
            ExecuteRunCalled = true;
            LastExecuteRunRequest = request;
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
            AgentExecutionOperationId activityOperationId,
            bool approved,
            bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunResult> ContinueExecutionRunAsync(
            Guid executionRunId,
            AgentExecutionOperationId activityOperationId,
            IReadOnlyList<PendingToolApprovalDecision> decisions,
            bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentChatRunResult> SendMessageAsync(
            Guid agentId,
            Guid? chatSessionId,
            string prompt,
            AgentChatRunOptions options,
            CancellationToken cancellationToken = default,
            IReadOnlyList<string>? attachmentPaths = null)
            => throw Unused();

        public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
            Guid agentId,
            Guid chatSessionId,
            AgentExecutionOperationId activityOperationId,
            bool approved,
            bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
            Guid agentId,
            Guid chatSessionId,
            AgentExecutionOperationId activityOperationId,
            IReadOnlyList<PendingToolApprovalDecision> decisions,
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

        public Task<AgentExecutionReportPage> QueryExecutionReportAsync(
            AgentExecutionReportQuery query,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunDetail> GetExecutionRunDetailAsync(
            Guid executionRunId,
            CancellationToken cancellationToken = default)
        {
            lock (ExecutionDetailRequestIds)
            {
                ExecutionDetailRequestIds.Add(executionRunId);
            }

            return executionDetailById.TryGetValue(executionRunId, out var detail)
                ? Task.FromResult(detail)
                : throw Unused();
        }

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

    private sealed class ThrowingSubprocessLaunchCoordinator(Exception exception) : IProcessSubprocessLaunchCoordinator
    {
        public bool Called { get; private set; }

        public ValueTask<ProcessSubprocessLaunchCoordinatorResult?> TryLaunchAsync(
            ProcessSubprocessLaunchCoordinatorRequest request,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            throw exception;
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

    private sealed class ThrowingRuntimeStateStore(Exception exception) : IProcessRuntimeStateStore
    {
        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromException<ProcessRuntimeStateSnapshot?>(exception);
    }

    private sealed class NoMatchingChildSubprocessArtifactBridge : IParentSubprocessArtifactBridge
    {
        public ValueTask<ParentSubprocessArtifactBridgeResult> ResolveExistingAsync(
            ProcessRuntimeStepAssignment assignment,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(ParentSubprocessArtifactBridgeResult.NoMatchingChildRun);

        public ValueTask<ParentSubprocessArtifactBridgeResult> ResolveFromOutputAsync(
            ProcessRuntimeStepAssignment assignment,
            ProcessStepOutcomeResult output,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(ParentSubprocessArtifactBridgeResult.NoMatchingChildRun);
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
