using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceManagedScriptPlanExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_records_workspace_receipts_and_validates_rooted_readback()
    {
        using var workspace = new TestWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var markerPath = Path.Combine(productRoot, "proof.txt");
        var processHost = new FakeWorkspaceProcessHost(request =>
        {
            if (request.ToolName == "workspace_pwsh_run_script")
            {
                File.WriteAllText(markerPath, "completed");
            }
        });
        var workspaceFiles = new WorkspaceFileService(workspace.Root);
        var workspaceCommands = new WorkspaceCommandExecutionService(workspace.Root, processHost);
        var executor = new WorkspaceManagedScriptPlanExecutor(workspaceFiles, workspaceCommands);

        var result = await executor.ExecuteAsync(CreateRequest(productRoot, markerPath));

        Assert.True(result.Succeeded, result.Summary);
        Assert.Single(processHost.Requests);
        Assert.Equal("workspace_pwsh_run_script", processHost.Requests[0].ToolName);
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_write_file");
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_stat_path");
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_pwsh_run_script");
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_read_file");
    }

    [Fact]
    public async Task ExecuteAsync_rejects_readback_outside_the_declared_product_root_before_running_the_script()
    {
        using var workspace = new TestWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var outsidePath = Path.Combine(workspace.Root, "outside.txt");
        var processHost = new FakeWorkspaceProcessHost();
        var workspaceFiles = new WorkspaceFileService(workspace.Root);
        var workspaceCommands = new WorkspaceCommandExecutionService(workspace.Root, processHost);
        var executor = new WorkspaceManagedScriptPlanExecutor(workspaceFiles, workspaceCommands);

        var result = await executor.ExecuteAsync(CreateRequest(productRoot, outsidePath));

        Assert.False(result.Succeeded);
        Assert.Contains("escapes ProductRoot", result.Summary, StringComparison.Ordinal);
        Assert.Empty(result.ToolReceipts);
        Assert.Empty(processHost.Requests);
    }

    [Fact]
    public async Task ExecuteAsync_returns_the_script_failure_and_receipt_without_attempting_readback()
    {
        using var workspace = new TestWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var markerPath = Path.Combine(productRoot, "proof.txt");
        var processHost = new FakeWorkspaceProcessHost
        {
            ExitCode = 1,
            Stderr = "script failed"
        };
        var workspaceFiles = new WorkspaceFileService(workspace.Root);
        var workspaceCommands = new WorkspaceCommandExecutionService(workspace.Root, processHost);
        var executor = new WorkspaceManagedScriptPlanExecutor(workspaceFiles, workspaceCommands);

        var result = await executor.ExecuteAsync(CreateRequest(productRoot, markerPath));

        Assert.False(result.Succeeded);
        Assert.Contains("helper failed", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Single(processHost.Requests);
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_pwsh_run_script");
        Assert.DoesNotContain(result.ToolReceipts, receipt => receipt.ToolName == "workspace_read_file");
    }

    [Fact]
    public async Task ExecuteAsync_rejects_a_missing_or_mismatched_readback_after_the_script_runs()
    {
        using var workspace = new TestWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var markerPath = Path.Combine(productRoot, "proof.txt");
        await File.WriteAllTextAsync(markerPath, "unexpected content");
        var processHost = new FakeWorkspaceProcessHost();
        var workspaceFiles = new WorkspaceFileService(workspace.Root);
        var workspaceCommands = new WorkspaceCommandExecutionService(workspace.Root, processHost);
        var executor = new WorkspaceManagedScriptPlanExecutor(workspaceFiles, workspaceCommands);

        var result = await executor.ExecuteAsync(CreateRequest(productRoot, markerPath));

        Assert.False(result.Succeeded);
        Assert.Contains("content was not found", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Single(processHost.Requests);
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_read_file");
    }

    [Fact]
    public async Task ExecuteAsync_requires_a_match_from_every_required_text_group()
    {
        using var workspace = new TestWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var markerPath = Path.Combine(productRoot, "proof.txt");
        await File.WriteAllTextAsync(markerPath, "first condition");
        var processHost = new FakeWorkspaceProcessHost();
        var workspaceFiles = new WorkspaceFileService(workspace.Root);
        var workspaceCommands = new WorkspaceCommandExecutionService(workspace.Root, processHost);
        var executor = new WorkspaceManagedScriptPlanExecutor(workspaceFiles, workspaceCommands);

        var result = await executor.ExecuteAsync(
            CreateRequest(
                productRoot,
                markerPath,
                [
                    ["first"],
                    ["second"]
                ]));

        Assert.False(result.Succeeded);
        Assert.Contains("content was not found", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Single(processHost.Requests);
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_read_file");
    }

    private static WorkspaceManagedScriptPlanExecutionRequest CreateRequest(
        string productRoot,
        string readbackPath,
        IReadOnlyList<IReadOnlyList<string>>? requiredTextAnyGroups = null)
    {
        var executionRunId = Guid.NewGuid();
        return new WorkspaceManagedScriptPlanExecutionRequest(
            executionRunId,
            $"artifacts/process-runs/{executionRunId:D}/scripts/managed-script.ps1",
            "Write-Output 'managed script'",
            JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["version"] = 1,
                ["mode"] = "ProductMutation",
                ["declaredReadPaths"] = new[] { readbackPath },
                ["declaredWritePaths"] = new[] { readbackPath },
                ["allowShellDelegation"] = true
            }),
            AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(productRoot)!,
            $"artifacts/process-runs/{executionRunId:D}/tool-runs/managed-script.json",
            productRoot,
            [new WorkspaceManagedScriptReadbackCheck(
                [readbackPath],
                requiredTextAnyGroups ?? [["completed"]],
                MustExist: true)],
            "test:managed-script");
    }

    private sealed class TestWorkspace : IDisposable
    {
        private readonly List<string> roots = [];

        public TestWorkspace()
        {
            Root = CreateRoot("Workspace");
        }

        public string Root { get; }

        public string CreateProductRoot()
            => CreateRoot("Product");

        public void Dispose()
        {
            foreach (var root in roots)
            {
                try
                {
                    if (Directory.Exists(root))
                    {
                        Directory.Delete(root, recursive: true);
                    }
                }
                catch
                {
                }
            }
        }

        private string CreateRoot(string name)
        {
            var root = Path.Combine(Path.GetTempPath(), $"cdi.{name}.{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            roots.Add(root);
            return root;
        }
    }

    private sealed class FakeWorkspaceProcessHost(Action<WorkspaceProcessExecutionRequest>? onExecute = null) : IWorkspaceProcessHost
    {
        public List<WorkspaceProcessExecutionRequest> Requests { get; } = [];

        public int ExitCode { get; init; }

        public string Stderr { get; init; } = string.Empty;

        public ExecutionBoundaryDescriptor DescribeBoundary()
            => new(
                "Test",
                "Workspace",
                "None",
                "None",
                "Fake",
                IsEnforcedByHost: false,
                "Unit test host.");

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            onExecute?.Invoke(request);
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new WorkspaceProcessExecutionResult(
                Started: true,
                ExitCode: ExitCode,
                Stdout: "ok",
                Stderr: Stderr,
                StdoutTruncated: false,
                StderrTruncated: false,
                StartedAtUtc: now,
                CompletedAtUtc: now,
                TimedOut: false,
                Boundary: DescribeBoundary(),
                FailureMessage: string.Empty));
        }
    }
}
