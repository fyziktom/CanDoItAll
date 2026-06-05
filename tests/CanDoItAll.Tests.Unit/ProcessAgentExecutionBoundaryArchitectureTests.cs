using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessAgentExecutionBoundaryArchitectureTests
{
    [Fact]
    public void Process_core_and_driver_pack_projects_are_not_introduced_prematurely()
    {
        var root = FindRepositoryRoot();
        var projectFiles = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .ToArray();

        Assert.DoesNotContain(projectFiles, name =>
            string.Equals(name, "CanDoItAll.Processes.Core", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "CanDoItAll.Modules.Processes.Core", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("ProcessDriver", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("DriverPack", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Execution_boundary_design_stays_on_staging_facade_cutline()
    {
        var design = ReadRepositoryFile(
            "codex",
            "bundles",
            "process-agent-execution-boundary-foundation-v1",
            "architecture",
            "02-execution-boundary-staging.md");

        Assert.Contains("IProcessAutomationExecutionClient", design, StringComparison.Ordinal);
        Assert.Contains("SB06 Movement Cutline", design, StringComparison.Ordinal);
        Assert.Contains("The first stage may still return selected AgentFramework types", design, StringComparison.Ordinal);
        Assert.Contains("not a final `Processes.Core` contract", design, StringComparison.Ordinal);
        Assert.Contains("manager chat, observation services, recovery worker, UI run-detail loaders", design, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_execution_boundary_inventory_records_direct_dispatcher_calls_before_movement()
    {
        var inventory = ReadRepositoryFile(
            "codex",
            "bundles",
            "process-agent-execution-boundary-foundation-v1",
            "inventories",
            "02-agentframework-usage-in-processes.md");

        Assert.Contains("ProcessRunAutomationDispatchService.Execution.cs", inventory, StringComparison.Ordinal);
        Assert.Contains("ExecuteRunAsync", inventory, StringComparison.Ordinal);
        Assert.Contains("GetExecutionRunDetailAsync", inventory, StringComparison.Ordinal);
        Assert.Contains("ListExecutionRunsAsync", inventory, StringComparison.Ordinal);
        Assert.Contains("Process automation execution client facade where execution-path related", inventory, StringComparison.Ordinal);
        Assert.Contains("Out of dispatcher-boundary scope for this bundle", inventory, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispatcher_execution_path_uses_process_owned_execution_client_after_migration()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var dispatcherSource = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(dispatchDirectory, "ProcessRunAutomationDispatchService*.cs")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("workspaceService.", dispatcherSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IAgentFrameworkWorkspaceService workspaceService", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("IProcessAutomationExecutionClient executionClient", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("executionClient.ExecuteRunAsync", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("executionClient.GetExecutionRunDetailAsync", dispatcherSource, StringComparison.Ordinal);
        Assert.Contains("executionClient.ListExecutionRunsAsync", dispatcherSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_contracts_project_is_solution_registered_and_stays_neutral()
    {
        var root = FindRepositoryRoot();
        var solution = ReadRepositoryFile("CanDoItAll.slnx");
        var projectPath = Path.Combine(
            root,
            "src",
            "CanDoItAll.Processes.Contracts",
            "CanDoItAll.Processes.Contracts.csproj");
        var project = File.ReadAllText(projectPath);
        var source = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(Path.GetDirectoryName(projectPath)!, "*.cs", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.Contains("src/CanDoItAll.Processes.Contracts/CanDoItAll.Processes.Contracts.csproj", solution, StringComparison.Ordinal);
        Assert.DoesNotContain("<ProjectReference", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<PackageReference", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.AspNetCore.Components", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.AgentFramework", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ComponentBase", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_contracts_project_contains_only_minimal_execution_boundary_snapshots()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Processes.Contracts",
            "Automation",
            "ProcessAutomationExecutionContracts.cs");

        Assert.Contains("public sealed record ProcessAutomationExecutionRequest", source, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessAutomationInvocationSource", source, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessAutomationInvocationPolicy", source, StringComparison.Ordinal);
        Assert.Contains("public enum ProcessAutomationFinalizerMode", source, StringComparison.Ordinal);
        Assert.Contains("public enum ProcessAutomationStructuredOutputKind", source, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessAutomationExecutionRunQuery", source, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessAutomationExecutionRunResult", source, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessAutomationExecutionRunDetail", source, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessAutomationExecutionRunRecord", source, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessAutomationToolExecutionReceipt", source, StringComparison.Ordinal);
        Assert.Contains("public sealed record ProcessAutomationProviderUsageObservation", source, StringComparison.Ordinal);
        Assert.Contains("public sealed class ProcessAutomationExecutionFailedException", source, StringComparison.Ordinal);
        Assert.Contains("public enum ProcessAutomationExecutionState", source, StringComparison.Ordinal);
        Assert.Contains("public enum ProcessAutomationRunOutcome", source, StringComparison.Ordinal);
        Assert.Contains("public enum ProcessAutomationProviderUsageStatus", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessRun ", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessStepRun", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ViewModel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionRunRequest", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispatcher_partials_excluding_execution_client_do_not_use_agent_framework_execution_snapshots()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var source = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(dispatchDirectory, "*.cs")
                .Where(path => !string.Equals(
                    Path.GetFileName(path),
                    "ProcessAutomationExecutionClient.cs",
                    StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var forbiddenTokens = new[]
        {
            "ExecutionRunResult",
            "ExecutionRunDetail",
            "ExecutionRunRecord",
            "ExecutionRunQuery",
            "AgentChatRunFailedException",
            "AgentRunFailedException",
            "AgentStructuredOutputContracts"
        };

        foreach (var forbiddenToken in forbiddenTokens)
        {
            Assert.False(
                Regex.IsMatch(source, $@"\b{Regex.Escape(forbiddenToken)}\b", RegexOptions.CultureInvariant),
                forbiddenToken);
        }
    }

    [Fact]
    public void Receipt_observation_helper_uses_process_snapshots_without_agent_framework_references()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessAutomationReceiptObservationHelper.cs");

        Assert.Contains("ProcessAutomationExecutionRunDetail", source, StringComparison.Ordinal);
        Assert.Contains("ProcessAutomationToolExecutionReceipt", source, StringComparison.Ordinal);
        Assert.Contains("ResolveSuccessfulToolNames", source, StringComparison.Ordinal);
        Assert.Contains("ResolveReceiptFamilies", source, StringComparison.Ordinal);
        Assert.Contains("ResolveProviderMetadata", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.AgentFramework", source, StringComparison.Ordinal);
        Assert.False(Regex.IsMatch(source, @"\bExecutionRunDetail\b", RegexOptions.CultureInvariant));
        Assert.False(Regex.IsMatch(source, @"\bToolExecutionReceiptRecord\b", RegexOptions.CultureInvariant));
    }

    [Fact]
    public void Bundle_proof_paths_do_not_contain_mobile_or_small_screen_artifacts()
    {
        var proofRoot = Path.Combine(
            FindRepositoryRoot(),
            "codex",
            "bundles",
            "process-dispatch-execution-snapshot-boundary-v1",
            "proof");
        var forbiddenPathTokens = new[]
        {
            "mobile",
            "small-screen",
            "small_screen",
            "medium-screen",
            "medium_screen",
            "phone",
            "tablet"
        };
        IEnumerable<string> proofArtifactPaths = Directory.Exists(proofRoot)
            ? Directory.EnumerateFiles(proofRoot, "*", SearchOption.AllDirectories)
            : [];

        Assert.DoesNotContain(proofArtifactPaths, path =>
        {
            var relativePath = Path.GetRelativePath(proofRoot, path);
            return forbiddenPathTokens.Any(token =>
                relativePath.Contains(token, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Artifact_boundary_bundle_proof_paths_do_not_contain_mobile_or_small_screen_artifacts()
    {
        var proofRoot = Path.Combine(
            FindRepositoryRoot(),
            "codex",
            "bundles",
            "process-dispatch-artifact-boundary-foundation-v1",
            "proof");
        var forbiddenPathTokens = new[]
        {
            "mobile",
            "small-screen",
            "small_screen",
            "medium-screen",
            "medium_screen",
            "phone",
            "tablet"
        };
        IEnumerable<string> proofArtifactPaths = Directory.Exists(proofRoot)
            ? Directory.EnumerateFiles(proofRoot, "*", SearchOption.AllDirectories)
            : [];

        Assert.DoesNotContain(proofArtifactPaths, path =>
        {
            var relativePath = Path.GetRelativePath(proofRoot, path);
            return forbiddenPathTokens.Any(token =>
                relativePath.Contains(token, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Artifact_boundary_helpers_stay_inside_processes_module_without_core_project()
    {
        var root = FindRepositoryRoot();
        var processesDispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");

        Assert.True(File.Exists(Path.Combine(processesDispatchDirectory, "ProcessArtifactExpectationMatcher.cs")));
        Assert.True(File.Exists(Path.Combine(processesDispatchDirectory, "ProcessArtifactProjectionLineageBuilder.cs")));
        Assert.True(File.Exists(Path.Combine(processesDispatchDirectory, "ProcessArtifactProjectionPlanner.cs")));
        Assert.True(File.Exists(Path.Combine(processesDispatchDirectory, "ProcessArtifactEvidenceValidationRules.cs")));
        Assert.False(Directory.Exists(Path.Combine(root, "src", "CanDoItAll.Processes.Core")));
        Assert.False(Directory.Exists(Path.Combine(root, "src", "CanDoItAll.Modules.Processes.Core")));
    }

    [Fact]
    public void Artifact_validation_snapshot_boundary_is_process_module_local_without_driver_contracts()
    {
        var root = FindRepositoryRoot();
        var processesDispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var snapshotPath = Path.Combine(processesDispatchDirectory, "ProcessArtifactValidationSnapshot.cs");
        var builderPath = Path.Combine(processesDispatchDirectory, "ProcessArtifactValidationSnapshotBuilder.cs");
        var helperSource = string.Join(
            Environment.NewLine,
            File.ReadAllText(snapshotPath),
            File.ReadAllText(builderPath));

        Assert.True(File.Exists(snapshotPath));
        Assert.True(File.Exists(builderPath));
        Assert.Contains("internal sealed record ProcessArtifactValidationSnapshot", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessArtifactValidationExpectation", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", helperSource, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(root, "src", "CanDoItAll.Processes.Core")));
    }

    [Fact]
    public void Tool_validation_boundary_helpers_are_module_local_without_core_or_driver_contracts()
    {
        var root = FindRepositoryRoot();
        var processesDispatchDirectory = Path.Combine(
            root,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var helperFiles = new[]
        {
            "ProcessToolReceiptFacts.cs",
            "ProcessRequiredToolValidationRules.cs",
            "ProcessCriticalToolFailureRules.cs",
            "ProcessCompletionBlockerRules.cs",
            "ProcessCompletionDecisionRules.cs",
            "ProcessRecoveryRetryDecisionRules.cs"
        };
        var helperSource = string.Join(
            Environment.NewLine,
            helperFiles.Select(file =>
            {
                var path = Path.Combine(processesDispatchDirectory, file);
                Assert.True(File.Exists(path), file);
                return File.ReadAllText(path);
            }));

        Assert.Contains("internal sealed record ProcessToolReceiptFact", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessRequiredToolDecision", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessCriticalToolFailureStackContext", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessCompletionBlockerSummary", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessCompletionDecisionInput", helperSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessRecoveryRetryFacts", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync", helperSource, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(root, "src", "CanDoItAll.Processes.Core")));
    }

    [Fact]
    public void Tool_validation_dispatcher_delegates_to_local_fact_and_rule_boundaries()
    {
        var toolValidationSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ToolValidation.cs");
        var artifactValidationSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactValidation.cs");
        var recoveryPacketsSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.RecoveryPackets.cs");
        var receiptObservationSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessAutomationReceiptObservationHelper.cs");

        Assert.Contains("ProcessCriticalToolFailureRules.ResolveUnresolvedCriticalToolFailures", toolValidationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRequiredToolValidationRules.ResolveMissingRequiredTools", toolValidationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessCompletionBlockerRules.CreateSummary", toolValidationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessCompletionDecisionRules.TryResolveRunStateDecision", toolValidationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessToolReceiptFacts.IsCriticalWorkspaceProcessReceipt", artifactValidationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessToolReceiptFacts.ResolveSuccessfulReceipts", receiptObservationSource, StringComparison.Ordinal);
        Assert.Contains("ProcessRecoveryRetryDecisionRules.CreateFacts", recoveryPacketsSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Tool_validation_boundary_bundle_proof_paths_do_not_contain_mobile_or_small_screen_artifacts()
    {
        var proofRoot = Path.Combine(
            FindRepositoryRoot(),
            "codex",
            "bundles",
            "process-dispatch-tool-validation-recovery-boundary-v1",
            "proof");
        var forbiddenPathTokens = new[]
        {
            "mobile",
            "small-screen",
            "small_screen",
            "medium-screen",
            "medium_screen",
            "phone",
            "tablet",
            "android",
            "iphone",
            "responsive"
        };
        IEnumerable<string> proofArtifactPaths = Directory.Exists(proofRoot)
            ? Directory.EnumerateFiles(proofRoot, "*", SearchOption.AllDirectories)
            : [];

        Assert.DoesNotContain(proofArtifactPaths, path =>
        {
            var relativePath = Path.GetRelativePath(proofRoot, path);
            return forbiddenPathTokens.Any(token =>
                relativePath.Contains(token, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Artifact_validation_gate_a_records_live_inventory_and_blocks_driver_or_viewport_drift()
    {
        var root = FindRepositoryRoot();
        var inventory = ReadRepositoryFile(
            "codex",
            "bundles",
            "process-dispatch-artifact-validation-rule-boundary-v1",
            "inventories",
            "02-artifact-validation-method-inventory-seed.md");
        var driverReadiness = ReadRepositoryFile(
            "codex",
            "bundles",
            "process-dispatch-artifact-validation-rule-boundary-v1",
            "inventories",
            "04-driver-readiness-map.md");
        var proofRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-dispatch-artifact-validation-rule-boundary-v1",
            "proof");
        var forbiddenPathTokens = new[]
        {
            "mobile",
            "small-screen",
            "small_screen",
            "medium-screen",
            "medium_screen",
            "phone",
            "tablet"
        };
        IEnumerable<string> proofArtifactPaths = Directory.Exists(proofRoot)
            ? Directory.EnumerateFiles(proofRoot, "*", SearchOption.AllDirectories)
            : [];

        Assert.Contains("Status: refreshed from live source in SB02.", inventory, StringComparison.Ordinal);
        Assert.Contains("Current line count: 3931", inventory, StringComparison.Ordinal);
        Assert.Contains("Method declaration rows found: 188", inventory, StringComparison.Ordinal);
        Assert.Contains("Side-effect indicator rows found: 57", inventory, StringComparison.Ordinal);
        Assert.Contains("File.Copy", inventory, StringComparison.Ordinal);
        Assert.Contains("Do not implement driver APIs", driverReadiness, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", driverReadiness, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", driverReadiness, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", driverReadiness, StringComparison.Ordinal);
        Assert.DoesNotContain(proofArtifactPaths, path =>
        {
            var relativePath = Path.GetRelativePath(proofRoot, path);
            return forbiddenPathTokens.Any(token =>
                relativePath.Contains(token, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Step_completion_finalizer_gate_a_SB04_INV_001_records_live_inventory_and_blocks_core_driver_or_viewport_drift()
    {
        var root = FindRepositoryRoot();
        var inventory = ReadRepositoryFile(
            "codex",
            "bundles",
            "process-dispatch-step-completion-finalizer-boundary-v1",
            "inventories",
            "02-finalizer-method-classification-template.md");
        var targetSolution = ReadRepositoryFile(
            "codex",
            "bundles",
            "process-dispatch-step-completion-finalizer-boundary-v1",
            "architecture",
            "01-target-solution.md");
        var mafProject = ReadRepositoryFile(
            "src",
            "CanDoItAll.AgentFramework.Maf",
            "CanDoItAll.AgentFramework.Maf.csproj");
        var sourceText = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
                .Where(path => Path.GetExtension(path) is ".cs" or ".csproj")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var proofRoot = Path.Combine(
            root,
            "codex",
            "bundles",
            "process-dispatch-step-completion-finalizer-boundary-v1",
            "proof");
        var forbiddenPathTokens = new[]
        {
            "mobile",
            "small-screen",
            "small_screen",
            "medium-screen",
            "medium_screen",
            "phone",
            "tablet",
            "android",
            "iphone",
            "responsive"
        };
        IEnumerable<string> proofArtifactPaths = Directory.Exists(proofRoot)
            ? Directory.EnumerateFiles(proofRoot, "*", SearchOption.AllDirectories)
            : [];

        Assert.Contains("2091 lines", ReadRepositoryFile(
            "codex",
            "bundles",
            "process-dispatch-step-completion-finalizer-boundary-v1",
            "inventories",
            "01-source-impact-inventory.md"), StringComparison.Ordinal);
        Assert.Contains("StorageBackedProcessArtifactContentReader", inventory, StringComparison.Ordinal);
        Assert.Contains("PersistRuntimeInvariantAuditAsync", inventory, StringComparison.Ordinal);
        Assert.Contains("ProcessStepTransitionRequest", inventory, StringComparison.Ordinal);
        Assert.Contains("ProcessRunAutomationDispatchService.StepCompletionFinalizer.Types.cs", targetSolution, StringComparison.Ordinal);
        Assert.Contains("No Process Core extraction", targetSolution, StringComparison.Ordinal);
        Assert.Contains("No public type promotion", targetSolution, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.Processes", mafProject, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.Projects", mafProject, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.Workbench", mafProject, StringComparison.Ordinal);
        Assert.DoesNotContain("interface IProcessDriverPack", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("class ProcessDriverPack", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain(proofArtifactPaths, path =>
        {
            var relativePath = Path.GetRelativePath(proofRoot, path);
            return forbiddenPathTokens.Any(token =>
                relativePath.Contains(token, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Step_completion_finalizer_gate_a_SB04_INV_002_preserves_nested_type_surface_before_movement()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var source = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(dispatchDirectory, "ProcessRunAutomationDispatchService.StepCompletionFinalizer*.cs")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.Contains("internal enum ProcessStepCompletionExecutorKind", source, StringComparison.Ordinal);
        Assert.Contains("internal enum ProcessArtifactExpectationMode", source, StringComparison.Ordinal);
        Assert.Contains("internal enum ProcessArtifactValidationStatus", source, StringComparison.Ordinal);
        Assert.Contains("internal enum ProcessArtifactFailureOwnership", source, StringComparison.Ordinal);
        Assert.Contains("internal enum ProcessArtifactProducerKind", source, StringComparison.Ordinal);
        Assert.Contains("internal interface IProcessArtifactContentReader", source, StringComparison.Ordinal);
        Assert.Contains("internal sealed class WorkspaceProcessArtifactContentReader", source, StringComparison.Ordinal);
        Assert.Contains("internal sealed class StorageBackedProcessArtifactContentReader", source, StringComparison.Ordinal);
        Assert.Contains("ProcessStepCompletionFinalizerContext", source, StringComparison.Ordinal);
        Assert.Contains("ProcessStepCompletionFinalizerResult", source, StringComparison.Ordinal);
        Assert.Contains("ProcessStepTransitionArtifactValidationContext", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Step_completion_finalizer_gate_b_SB08_INV_001_extracts_types_and_readers_without_surface_or_stub_drift()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var mainSource = File.ReadAllText(Path.Combine(
            dispatchDirectory,
            "ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs"));
        var typeSource = File.ReadAllText(Path.Combine(
            dispatchDirectory,
            "ProcessRunAutomationDispatchService.StepCompletionFinalizer.Types.cs"));
        var readerSource = File.ReadAllText(Path.Combine(
            dispatchDirectory,
            "ProcessRunAutomationDispatchService.StepCompletionFinalizer.ArtifactContentReaders.cs"));
        var extractedSource = string.Join(Environment.NewLine, typeSource, readerSource);

        Assert.True(mainSource.Split(Environment.NewLine).Length < 2091);
        Assert.DoesNotContain("internal enum ProcessStepCompletionExecutorKind", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("internal interface IProcessArtifactContentReader", mainSource, StringComparison.Ordinal);
        Assert.Contains("internal enum ProcessStepCompletionExecutorKind", typeSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed record ProcessArtifactExpectationValidationResult", typeSource, StringComparison.Ordinal);
        Assert.Contains("private sealed record ProcessStepCompletionFinalizerContext", typeSource, StringComparison.Ordinal);
        Assert.Contains("internal interface IProcessArtifactContentReader", readerSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class WorkspaceProcessArtifactContentReader", readerSource, StringComparison.Ordinal);
        Assert.Contains("internal sealed class StorageBackedProcessArtifactContentReader", readerSource, StringComparison.Ordinal);
        Assert.Contains("WorkspaceScopeDescriptor.NormalizeRelativePath", readerSource, StringComparison.Ordinal);
        Assert.Contains("StorageJson.TryParseReference", readerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public enum ProcessStepCompletionExecutorKind", extractedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO", extractedSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NotImplementedException", extractedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IProcessDriverPack", extractedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Processes.Core", extractedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Step_completion_finalizer_gate_c_SB12_INV_001_extracts_validation_invariant_and_transition_helpers_with_field_parity()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var mainSource = File.ReadAllText(Path.Combine(
            dispatchDirectory,
            "ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs"));
        var validationSource = File.ReadAllText(Path.Combine(
            dispatchDirectory,
            "ProcessRunAutomationDispatchService.StepCompletionFinalizer.ValidationOrchestration.cs"));
        var invariantSource = File.ReadAllText(Path.Combine(
            dispatchDirectory,
            "ProcessRunAutomationDispatchService.StepCompletionFinalizer.RuntimeInvariantAudit.cs"));
        var transitionSource = File.ReadAllText(Path.Combine(
            dispatchDirectory,
            "ProcessRunAutomationDispatchService.StepCompletionFinalizer.TransitionRequestBuilder.cs"));
        var helperSource = string.Join(Environment.NewLine, validationSource, invariantSource, transitionSource);

        Assert.Contains("ValidateRequiredCompletionArtifactsAsync", validationSource, StringComparison.Ordinal);
        Assert.Contains("StorageBackedProcessArtifactContentReader", validationSource, StringComparison.Ordinal);
        Assert.Contains("ResolveWorkflowRunIdForStep", validationSource, StringComparison.Ordinal);
        Assert.Contains("ResolveSubprocessRunIdForStep", validationSource, StringComparison.Ordinal);
        Assert.Contains("PersistRuntimeInvariantAuditAsync", invariantSource, StringComparison.Ordinal);
        Assert.Contains("RuntimeInvariantViolationRecorded", invariantSource, StringComparison.Ordinal);
        Assert.Contains("ProcessConformanceSeverity.High or ProcessConformanceSeverity.Critical", mainSource, StringComparison.Ordinal);
        Assert.Contains("BuildFinalizedStepTransitionRequest", transitionSource, StringComparison.Ordinal);
        Assert.Contains("BuildStepTransitionArtifactValidationContext", transitionSource, StringComparison.Ordinal);
        Assert.Contains("BuildFinalizedStepTransitionRequest(candidate, finalizerResult)", mainSource, StringComparison.Ordinal);

        var requiredTransitionFields = new[]
        {
            "ArtifactValidationExecutorKind",
            "ArtifactValidationExecutionRunId",
            "ArtifactValidationWorkflowRunId",
            "ArtifactValidationSubprocessRunId",
            "ArtifactValidationRecoveryExecutionRunId",
            "ArtifactValidationRecoveredForExecutionRunId"
        };

        foreach (var field in requiredTransitionFields)
        {
            Assert.Contains(field, transitionSource, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("private async Task<IReadOnlyList<ProcessArtifactExpectationValidationResult>> ValidateRequiredCompletionArtifactsAsync", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private async Task<IReadOnlyList<RuntimeInvariantViolation>> PersistRuntimeInvariantAuditAsync", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private static void ApplyArtifactValidationContext", helperSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO", helperSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NotImplementedException", helperSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Artifact_validation_matcher_core_uses_validation_snapshot_expectations()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactValidation.cs");
        var snapshotSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactValidationSnapshot.cs");

        Assert.Contains("ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectations", source, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<ProcessArtifactValidationExpectation> expectedArtifacts", source, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactValidationExpectation expectedArtifact", source, StringComparison.Ordinal);
        Assert.Contains("ToProjectionExpectation()", snapshotSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private static ProcessArtifactProjectionExpectation ToProjectionExpectation", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Artifact_validation_path_rules_are_local_and_do_not_own_file_or_storage_effects()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactPathValidationRules.cs");

        Assert.Contains("internal static class ProcessArtifactPathValidationRules", source, StringComparison.Ordinal);
        Assert.Contains("NormalizeManagedPathReference", source, StringComparison.Ordinal);
        Assert.Contains("IsShallowSharedManagedArtifactPath", source, StringComparison.Ordinal);
        Assert.Contains("TryExtractExpectedArtifactRelativePath", source, StringComparison.Ordinal);
        Assert.Contains("ExpectedArtifactExplicitlyTargetsPath", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatchArtifactExpectation", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Artifact_validation_text_match_rules_are_local_and_do_not_own_orchestration_effects()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactTextMatchRules.cs");

        Assert.Contains("internal static class ProcessArtifactTextMatchRules", source, StringComparison.Ordinal);
        Assert.Contains("HasExpectedArtifactContentSignals", source, StringComparison.Ordinal);
        Assert.Contains("MatchesExpectedArtifactByTitleTokens", source, StringComparison.Ordinal);
        Assert.Contains("TokenizeArtifactComparisonText", source, StringComparison.Ordinal);
        Assert.Contains("SharesNarrativeArtifactPurpose", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatchArtifactExpectation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Artifact_validation_provider_native_visual_rules_are_local_and_do_not_own_orchestration_effects()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactProviderNativeVisualValidationRules.cs");
        var dispatcherSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactValidation.cs");

        Assert.Contains("internal static class ProcessArtifactProviderNativeVisualValidationRules", source, StringComparison.Ordinal);
        Assert.Contains("ScoreProviderNativeVisualArtifactExpectation", source, StringComparison.Ordinal);
        Assert.Contains("ResolveProviderNativeBrowserToolName", source, StringComparison.Ordinal);
        Assert.Contains("IsProviderNativeBrowserArtifactPath", source, StringComparison.Ordinal);
        Assert.Contains("ContainsScreenshotArtifactSignal", source, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProviderNativeVisualValidationRules.ScoreProviderNativeVisualArtifactExpectation", dispatcherSource, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatchArtifactExpectation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Artifact_validation_quality_rules_are_local_and_do_not_own_orchestration_effects()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactQualityValidationRules.cs");
        var dispatcherSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactValidation.cs");

        Assert.Contains("internal static class ProcessArtifactQualityValidationRules", source, StringComparison.Ordinal);
        Assert.Contains("ResolveInvalidQualityValidationProofSummary", source, StringComparison.Ordinal);
        Assert.Contains("ContainsZeroTestRunEvidence", source, StringComparison.Ordinal);
        Assert.Contains("ContainsBuildWarningEvidence", source, StringComparison.Ordinal);
        Assert.Contains("IsPlaceholderCriticalToolRequestSummary", source, StringComparison.Ordinal);
        Assert.Contains("ContainsConcreteBrowserProofSignal", source, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactQualityValidationRules.ResolveInvalidQualityValidationProofSummary", dispatcherSource, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatchArtifactExpectation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Artifact_validation_project_structure_requirement_rules_are_local_and_preserve_mandatory_source_lines()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactProjectStructureRequirementValidationRules.cs");
        var dispatcherSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactValidation.cs");
        var rootSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.cs");

        Assert.Contains("internal static class ProcessArtifactProjectStructureRequirementValidationRules", source, StringComparison.Ordinal);
        Assert.Contains("ResolveDowngradedProjectStructureRequirementSummary", source, StringComparison.Ordinal);
        Assert.Contains("ResolveGroundedProjectStructureRequirementLines", source, StringComparison.Ordinal);
        Assert.Contains("IsNonMandatoryProjectStructureSourceLine", source, StringComparison.Ordinal);
        Assert.Contains("ContainsRequirementWeakeningPhrase", source, StringComparison.Ordinal);
        Assert.Contains("TokenizeProjectStructureRequirementText", source, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectStructureRequirementValidationRules.ResolveDowngradedProjectStructureRequirementSummary", dispatcherSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectStructureRequirementNoiseTokens", rootSource, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatchArtifactExpectation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessDriver", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DriverPack", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Execution_artifact_projection_path_uses_projection_planner_before_recording_artifact()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactProjection.cs");

        var methodIndex = source.IndexOf("private async Task ProjectExecutionArtifactsAsync", StringComparison.Ordinal);
        var plannerIndex = source.IndexOf("ProcessArtifactProjectionPlanner.PlanExecutionArtifact", methodIndex, StringComparison.Ordinal);
        var recordIndex = source.IndexOf("RecordArtifactAsync", plannerIndex, StringComparison.Ordinal);

        Assert.True(methodIndex >= 0);
        Assert.True(plannerIndex > methodIndex);
        Assert.True(recordIndex > plannerIndex);
    }

    [Fact]
    public void Artifact_projection_helpers_do_not_reference_dispatcher_nested_expectations()
    {
        var helperFiles = new[]
        {
            "ProcessArtifactExpectationMatcher.cs",
            "ProcessArtifactProjectionPlanner.cs",
            "ProcessArtifactProjectionSourceAdapters.cs"
        };

        foreach (var helperFile in helperFiles)
        {
            var source = ReadRepositoryFile(
                "src",
                "CanDoItAll.Modules.Processes",
                "Automation",
                "Dispatch",
                helperFile);

            Assert.DoesNotContain("ProcessRunAutomationDispatchService.DispatchArtifactExpectation", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DispatchArtifactExpectation", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Artifact_projection_source_adapters_are_local_and_used_by_migrated_source_paths()
    {
        var dispatchDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch");
        var adapterPath = Path.Combine(dispatchDirectory, "ProcessArtifactProjectionSourceAdapters.cs");
        var projectionSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactProjection.cs");

        Assert.True(File.Exists(adapterPath));
        Assert.Contains("ProcessMockArtifactProjectionSourceAdapter.Plan", projectionSource, StringComparison.Ordinal);
        Assert.Contains("WorkspaceWrittenArtifactProjectionSourceAdapter.Plan", projectionSource, StringComparison.Ordinal);
        Assert.Contains("ExistingManagedArtifactProjectionSourceAdapter.Plan", projectionSource, StringComparison.Ordinal);
        Assert.Contains("ResponseTextArtifactProjectionSourceAdapter.Plan", projectionSource, StringComparison.Ordinal);
        Assert.Contains("ProviderNativeBrowserArtifactProjectionSourceAdapter.PlanExpectedOutput", projectionSource, StringComparison.Ordinal);
        Assert.Contains("ProviderNativeBrowserArtifactProjectionSourceAdapter.PlanDiscoveredOutput", projectionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Artifact_projection_write_coordinator_is_created_once_by_artifact_projection_flow()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactProjection.cs");
        var methodIndex = source.IndexOf("private async Task ProjectExecutionArtifactsAsync", StringComparison.Ordinal);
        var coordinatorIndex = source.IndexOf("new ProcessArtifactProjectionWriteCoordinator", StringComparison.Ordinal);
        var totalCoordinatorCreations = Regex.Matches(source, "new ProcessArtifactProjectionWriteCoordinator").Count;

        Assert.True(methodIndex >= 0);
        Assert.True(coordinatorIndex > methodIndex);
        Assert.Equal(1, totalCoordinatorCreations);
    }

    [Fact]
    public void Process_mock_projection_SB05_INV_001_uses_write_coordinator_without_direct_storage_record_block()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactProjection.cs");
        var methodStart = source.IndexOf("private async Task ProjectProcessMockArtifactsAsync", StringComparison.Ordinal);
        var nextMethodStart = source.IndexOf("private async Task ProjectWorkspaceWrittenArtifactsAsync", StringComparison.Ordinal);

        Assert.True(methodStart >= 0);
        Assert.True(nextMethodStart > methodStart);

        var processMockSection = source[methodStart..nextMethodStart];

        Assert.Contains("writeCoordinator.WriteAsync", processMockSection, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionWriteRequest", processMockSection, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService.PlaceAsync", processMockSection, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync(", processMockSection, StringComparison.Ordinal);
        Assert.Contains("throw new InvalidOperationException", processMockSection, StringComparison.Ordinal);
    }

    [Fact]
    public void Workspace_written_projection_SB06_INV_001_uses_write_coordinator_without_direct_storage_record_block()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactProjection.cs");
        var methodStart = source.IndexOf("private async Task ProjectWorkspaceWrittenArtifactsAsync", StringComparison.Ordinal);
        var nextMethodStart = source.IndexOf("private static IReadOnlyList<string> ResolveSuccessfulWorkspaceFileMutationReceiptPaths", StringComparison.Ordinal);

        Assert.True(methodStart >= 0);
        Assert.True(nextMethodStart > methodStart);

        var workspaceWrittenSection = source[methodStart..nextMethodStart];

        Assert.Contains("ProcessArtifactProjectionWriteCoordinator writeCoordinator", workspaceWrittenSection, StringComparison.Ordinal);
        Assert.Contains("writeCoordinator.WriteAsync", workspaceWrittenSection, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionWriteRequest", workspaceWrittenSection, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService.PlaceAsync", workspaceWrittenSection, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync(", workspaceWrittenSection, StringComparison.Ordinal);
        Assert.Contains("logger.LogWarning", workspaceWrittenSection, StringComparison.Ordinal);
        Assert.DoesNotContain("throw new InvalidOperationException", workspaceWrittenSection, StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_managed_projection_SB07_INV_001_uses_write_coordinator_without_direct_storage_record_block()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactProjection.cs");
        var methodStart = source.IndexOf("private async Task ProjectExistingManagedArtifactFilesAsync", StringComparison.Ordinal);
        var nextMethodStart = source.IndexOf("private static IReadOnlyList<string> ResolveExpectedManagedArtifactRelativePaths", StringComparison.Ordinal);

        Assert.True(methodStart >= 0);
        Assert.True(nextMethodStart > methodStart);

        var existingManagedSection = source[methodStart..nextMethodStart];

        Assert.Contains("ExistingManagedArtifactFileMatches", existingManagedSection, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionWriteCoordinator writeCoordinator", existingManagedSection, StringComparison.Ordinal);
        Assert.Contains("writeCoordinator.WriteAsync", existingManagedSection, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionWriteRequest", existingManagedSection, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService.PlaceAsync", existingManagedSection, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync(", existingManagedSection, StringComparison.Ordinal);
        Assert.Contains("logger.LogWarning", existingManagedSection, StringComparison.Ordinal);
        Assert.DoesNotContain("throw new InvalidOperationException", existingManagedSection, StringComparison.Ordinal);
    }

    [Fact]
    public void Response_text_projection_SB09_INV_001_uses_write_coordinator_without_moving_file_creation_or_short_circuit()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactProjection.cs");
        var responseMethodStart = source.IndexOf("private async Task ProjectResponseTextArtifactsAsync", StringComparison.Ordinal);
        var helperMethodStart = source.IndexOf("private async Task<bool> TryRecordExistingManagedArtifactForResponseProjectionAsync", StringComparison.Ordinal);
        var nextMethodStart = source.IndexOf("private async Task ProjectProviderNativeBrowserArtifactsAsync", StringComparison.Ordinal);

        Assert.True(responseMethodStart >= 0);
        Assert.True(helperMethodStart > responseMethodStart);
        Assert.True(nextMethodStart > helperMethodStart);

        var responseSection = source[responseMethodStart..helperMethodStart];
        var existingManagedHelperSection = source[helperMethodStart..nextMethodStart];

        Assert.Contains("ProcessArtifactProjectionWriteCoordinator writeCoordinator", responseSection, StringComparison.Ordinal);
        Assert.Contains("IsWithinWorkspace", responseSection, StringComparison.Ordinal);
        Assert.Contains("File.WriteAllTextAsync", responseSection, StringComparison.Ordinal);
        Assert.Contains("persistedResponseText", responseSection, StringComparison.Ordinal);
        Assert.Contains("Environment.NewLine", responseSection, StringComparison.Ordinal);
        Assert.Contains("Encoding.UTF8.GetBytes(persistedResponseText)", responseSection, StringComparison.Ordinal);
        Assert.Contains("TryRecordExistingManagedArtifactForResponseProjectionAsync", responseSection, StringComparison.Ordinal);
        Assert.Contains("writeCoordinator.WriteAsync", responseSection, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionWriteRequest", responseSection, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService.PlaceAsync", responseSection, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync(", responseSection, StringComparison.Ordinal);

        Assert.Contains("ExistingManagedArtifactFileMatches", existingManagedHelperSection, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionWriteCoordinator writeCoordinator", existingManagedHelperSection, StringComparison.Ordinal);
        Assert.Contains("writeCoordinator.WriteAsync", existingManagedHelperSection, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionWriteRequest", existingManagedHelperSection, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService.PlaceAsync", existingManagedHelperSection, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync(", existingManagedHelperSection, StringComparison.Ordinal);
        Assert.Contains("logger.LogWarning", existingManagedHelperSection, StringComparison.Ordinal);
        Assert.Contains("return false", existingManagedHelperSection, StringComparison.Ordinal);
    }

    [Fact]
    public void Provider_native_browser_projection_SB10_INV_001_uses_write_coordinator_for_expected_and_discovered_modes()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactProjection.cs");
        var expectedMethodStart = source.IndexOf("private async Task ProjectProviderNativeBrowserArtifactsAsync", StringComparison.Ordinal);
        var discoveredMethodStart = source.IndexOf("private async Task ProjectProviderNativeBrowserOutputArtifactsAsync", StringComparison.Ordinal);
        var nextMethodStart = source.IndexOf("private static string ResolveProviderNativeBrowserProjectedRelativePath", StringComparison.Ordinal);

        Assert.True(expectedMethodStart >= 0);
        Assert.True(discoveredMethodStart > expectedMethodStart);
        Assert.True(nextMethodStart > discoveredMethodStart);

        var expectedSection = source[expectedMethodStart..discoveredMethodStart];
        var discoveredSection = source[discoveredMethodStart..nextMethodStart];

        Assert.Contains("ProcessArtifactProjectionWriteCoordinator writeCoordinator", expectedSection, StringComparison.Ordinal);
        Assert.Contains("ProjectProviderNativeBrowserOutputArtifactsAsync", expectedSection, StringComparison.Ordinal);
        Assert.Contains("writeCoordinator", expectedSection, StringComparison.Ordinal);
        Assert.Contains("ResolveProviderNativeBrowserToolName", expectedSection, StringComparison.Ordinal);
        Assert.Contains("PlanExpectedOutput", expectedSection, StringComparison.Ordinal);
        Assert.Contains("IsWithinWorkspace", expectedSection, StringComparison.Ordinal);
        Assert.Contains("File.Copy", expectedSection, StringComparison.Ordinal);
        Assert.Contains("writeCoordinator.WriteAsync", expectedSection, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionWriteRequest", expectedSection, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService.PlaceAsync", expectedSection, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync(", expectedSection, StringComparison.Ordinal);

        Assert.Contains("ProcessArtifactProjectionWriteCoordinator writeCoordinator", discoveredSection, StringComparison.Ordinal);
        Assert.Contains("IsProviderNativeBrowserArtifactPath", discoveredSection, StringComparison.Ordinal);
        Assert.Contains("ResolveArtifactExpectation", discoveredSection, StringComparison.Ordinal);
        Assert.Contains("recordExpectation", discoveredSection, StringComparison.Ordinal);
        Assert.Contains("PlanDiscoveredOutput", discoveredSection, StringComparison.Ordinal);
        Assert.Contains("File.Copy", discoveredSection, StringComparison.Ordinal);
        Assert.Contains("writeCoordinator.WriteAsync", discoveredSection, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionWriteRequest", discoveredSection, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService.PlaceAsync", discoveredSection, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync(", discoveredSection, StringComparison.Ordinal);
    }

    [Fact]
    public void Completed_decision_projection_SB11_INV_001_uses_record_only_coordinator_without_storage_placement()
    {
        var source = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.ArtifactProjection.cs");
        var coordinatorSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessArtifactProjectionWriteCoordinator.cs");
        var methodStart = source.IndexOf("private async Task EnsureDecisionArtifactsForCompletedStepAsync", StringComparison.Ordinal);
        var nextMethodStart = source.IndexOf("private static bool HasProjectedArtifactExpectationExternalReference", StringComparison.Ordinal);
        var recordOnlyCoordinatorStart = coordinatorSource.IndexOf("internal sealed class ProcessArtifactProjectionRecordOnlyCoordinator", StringComparison.Ordinal);

        Assert.True(methodStart >= 0);
        Assert.True(nextMethodStart > methodStart);
        Assert.True(recordOnlyCoordinatorStart >= 0);

        var decisionSection = source[methodStart..nextMethodStart];
        var recordOnlyCoordinatorSection = coordinatorSource[recordOnlyCoordinatorStart..];

        Assert.Contains("ProcessArtifactProjectionRecordOnlyCoordinator recordOnlyCoordinator", decisionSection, StringComparison.Ordinal);
        Assert.Contains("ProcessArtifactProjectionRecordOnlyRequest", decisionSection, StringComparison.Ordinal);
        Assert.Contains("recordOnlyCoordinator.RecordAsync", decisionSection, StringComparison.Ordinal);
        Assert.Contains("BuildCompletedDecisionArtifactExternalReferenceKey", decisionSection, StringComparison.Ordinal);
        Assert.Contains("ResolveCompletedDecisionArtifactTrustStatus", decisionSection, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService.PlaceAsync", decisionSection, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessArtifactProjectionWriteCoordinator", decisionSection, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordArtifactAsync(", decisionSection, StringComparison.Ordinal);

        Assert.Contains("ProcessArtifactProjectionRecordOnlyRequest", recordOnlyCoordinatorSection, StringComparison.Ordinal);
        Assert.Contains("recordArtifactAsync", recordOnlyCoordinatorSection, StringComparison.Ordinal);
        Assert.DoesNotContain("storagePlacementService.PlaceAsync", recordOnlyCoordinatorSection, StringComparison.Ordinal);
        Assert.DoesNotContain("StoragePlacementRequest", recordOnlyCoordinatorSection, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        return File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. pathParts]));
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), Path.GetDirectoryName(sourceFilePath) ?? string.Empty })
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                continue;
            }

            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
