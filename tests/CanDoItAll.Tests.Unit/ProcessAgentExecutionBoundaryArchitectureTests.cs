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
