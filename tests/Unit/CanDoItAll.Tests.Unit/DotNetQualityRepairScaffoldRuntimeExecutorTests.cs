using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class DotNetQualityRepairScaffoldRuntimeExecutorTests
{
    [Fact]
    public async Task TryExecuteAsync_removes_only_fingerprint_matched_scaffold_and_validates_product()
    {
        using var workspace = new TestWorkspace();
        var productRoot = workspace.CreateProduct();
        WriteStockBlazorFiles(productRoot);
        var processHost = new FakeWorkspaceProcessHost(request =>
        {
            if (request.ToolName == "workspace_pwsh_run_script")
            {
                ApplyExpectedScaffoldRepair(productRoot);
            }
        });
        var workspaceFiles = new WorkspaceFileService(workspace.WorkspaceRoot);
        var executor = new DotNetQualityRepairScaffoldRuntimeExecutor(
            workspaceFiles,
            new WorkspaceCommandExecutionService(workspace.WorkspaceRoot, processHost),
            new DotNetScaffoldResidueInspector(workspaceFiles));

        var assignment = CreateAssignment(productRoot);
        WriteDiagnosis(workspaceFiles, assignment, DotNetQualityRepairScaffoldRuntimeExecutor.ScaffoldResidueDiagnosisMarker);

        var result = await executor.TryExecuteAsync(assignment);

        Assert.NotNull(result);
        Assert.True(result!.Succeeded, result.Summary);
        Assert.Equal("product-repair-applied", result.Output!.BranchOutcomeKey);
        Assert.False(File.Exists(Path.Combine(productRoot, "src", "BusinessApp", "Pages", "Counter.razor")));
        Assert.False(File.Exists(Path.Combine(productRoot, "src", "BusinessApp", "Pages", "Weather.razor")));
        Assert.DoesNotContain(
            "learn.microsoft.com/aspnet/core/",
            await File.ReadAllTextAsync(Path.Combine(productRoot, "src", "BusinessApp", "Layout", "MainLayout.razor")),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_pwsh_run_script");
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_dotnet_restore");
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_dotnet_build");
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_dotnet_test");
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_write_file" && receipt.RequestSummary.EndsWith("implement-quality-repair.md", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryExecuteAsync_does_not_claim_unrelated_quality_repair()
    {
        using var workspace = new TestWorkspace();
        var productRoot = workspace.CreateProduct();
        WriteCleanBlazorFiles(productRoot);
        var workspaceFiles = new WorkspaceFileService(workspace.WorkspaceRoot);
        var executor = new DotNetQualityRepairScaffoldRuntimeExecutor(
            workspaceFiles,
            new WorkspaceCommandExecutionService(workspace.WorkspaceRoot, new FakeWorkspaceProcessHost()),
            new DotNetScaffoldResidueInspector(workspaceFiles));

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot));

        Assert.Null(result);
    }

    [Fact]
    public async Task TryExecuteAsync_does_not_claim_stock_residue_when_diagnosis_selected_another_boundary()
    {
        using var workspace = new TestWorkspace();
        var productRoot = workspace.CreateProduct();
        WriteStockBlazorFiles(productRoot);
        var workspaceFiles = new WorkspaceFileService(workspace.WorkspaceRoot);
        var executor = new DotNetQualityRepairScaffoldRuntimeExecutor(
            workspaceFiles,
            new WorkspaceCommandExecutionService(workspace.WorkspaceRoot, new FakeWorkspaceProcessHost()),
            new DotNetScaffoldResidueInspector(workspaceFiles));
        var assignment = CreateAssignment(productRoot);
        WriteDiagnosis(
            workspaceFiles,
            assignment,
            "The owning boundary is gameplay scoring in TetrisGameState. Repair the soft-drop invariant.");

        var result = await executor.TryExecuteAsync(assignment);

        Assert.Null(result);
        Assert.True(File.Exists(Path.Combine(productRoot, "src", "BusinessApp", "Pages", "Counter.razor")));
    }

    [Theory]
    [InlineData(
        "<p role=\"status\">Redirect to the playable product surface from the home route.</p>",
        "<p>This starter sample is intentionally left without gameplay content.</p>")]
    [InlineData(
        "<h1>Counter removed</h1><p>This scaffold page is not part of the product slice.</p>",
        "<h1>Weather removed</h1><p>This scaffold page is not part of the product slice.</p>")]
    public async Task TryExecuteAsync_removes_exact_generated_starter_page_stubs(
        string counterContent,
        string weatherContent)
    {
        using var workspace = new TestWorkspace();
        var productRoot = workspace.CreateProduct();
        WriteStockBlazorFiles(productRoot);
        var pagesDirectory = Path.Combine(productRoot, "src", "BusinessApp", "Pages");
        File.WriteAllText(
            Path.Combine(pagesDirectory, "Counter.razor"),
            $"@page \"/counter\"\n{counterContent}");
        File.WriteAllText(
            Path.Combine(pagesDirectory, "Weather.razor"),
            $"@page \"/weather\"\n{weatherContent}");
        var processHost = new FakeWorkspaceProcessHost(request =>
        {
            if (request.ToolName == "workspace_pwsh_run_script")
            {
                ApplyExpectedScaffoldRepair(productRoot);
            }
        });
        var workspaceFiles = new WorkspaceFileService(workspace.WorkspaceRoot);
        var executor = new DotNetQualityRepairScaffoldRuntimeExecutor(
            workspaceFiles,
            new WorkspaceCommandExecutionService(workspace.WorkspaceRoot, processHost),
            new DotNetScaffoldResidueInspector(workspaceFiles));

        var assignment = CreateAssignment(productRoot);
        WriteDiagnosis(workspaceFiles, assignment, DotNetQualityRepairScaffoldRuntimeExecutor.ScaffoldResidueDiagnosisMarker);

        var result = await executor.TryExecuteAsync(assignment);

        Assert.NotNull(result);
        Assert.True(result!.Succeeded, result.Summary);
        Assert.False(File.Exists(Path.Combine(pagesDirectory, "Counter.razor")));
        Assert.False(File.Exists(Path.Combine(pagesDirectory, "Weather.razor")));
    }

    [Fact]
    public async Task TryExecuteAsync_preserves_functional_product_page_on_stock_route()
    {
        using var workspace = new TestWorkspace();
        var productRoot = workspace.CreateProduct();
        WriteCleanBlazorFiles(productRoot);
        var counterPage = Path.Combine(productRoot, "src", "BusinessApp", "Pages", "Counter.razor");
        File.WriteAllText(
            counterPage,
            "@page \"/counter\"\n<button @onclick=\"Increment\">Run sample</button>\n@code { private void Increment() { } }");
        var workspaceFiles = new WorkspaceFileService(workspace.WorkspaceRoot);
        var executor = new DotNetQualityRepairScaffoldRuntimeExecutor(
            workspaceFiles,
            new WorkspaceCommandExecutionService(workspace.WorkspaceRoot, new FakeWorkspaceProcessHost()),
            new DotNetScaffoldResidueInspector(workspaceFiles));

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot));

        Assert.Null(result);
        Assert.True(File.Exists(counterPage));
    }

    private static ProcessRuntimeStepAssignment CreateAssignment(string productRoot)
    {
        var runId = ProcessRunId.New();
        var productAlias = ToExternalTargetAlias(productRoot);
        var appDirectory = Path.Combine(productRoot, "src", "BusinessApp");
        var solutionFile = Path.Combine(productRoot, "BusinessApp.slnx");
        var testProjectFile = Path.Combine(productRoot, "tests", "BusinessApp.Tests", "BusinessApp.Tests.csproj");
        var scriptRef = $"artifacts/process-runs/{runId.Value:D}/scripts/remove-default-blazor-scaffold.ps1";
        var manifest = JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["version"] = 1,
            ["mode"] = "ProductMutation",
            ["declaredReadPaths"] = new[] { appDirectory },
            ["declaredWritePaths"] = new[] { appDirectory },
            ["allowShellDelegation"] = false
        });
        return new ProcessRuntimeStepAssignment(
            runId,
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "implement-quality-repair",
            "dotnet-repair-engineer",
            "dotnet-repair-engineer",
            ".NET repair engineer",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            ".NET Application Developer",
            "Apply a bounded quality repair.",
            "sha256:readiness",
            "Resolved from role fit.",
            [ArtifactSlotId.New()],
            [],
            [ProcessOperationContractNames.MutateProductTarget, ProcessOperationContractNames.RunValidation],
            ProcessOperationContractNames.ExternalProductTargetMutable,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProcessRuntimeLaunchVariables.ProcessDefinitionKey] = "dotnet-quality-repair",
                ["ProductRoot"] = productRoot,
                ["ProductRootAlias"] = productAlias,
                ["DotNetAppProjectDirectory"] = appDirectory,
                ["DotNetSolutionFileAlias"] = ToExternalTargetAlias(solutionFile),
                ["DotNetTestProjectFileAlias"] = ToExternalTargetAlias(testProjectFile),
                ["DotNetScaffoldRepairScriptRef"] = scriptRef,
                ["DotNetScaffoldRepairScript"] = "# deterministic test helper",
                ["DotNetScaffoldRepairSideEffectManifest"] = manifest
            },
            BranchGate: null,
            DateTimeOffset.UtcNow);
    }

    private static void WriteDiagnosis(
        IWorkspaceFileService workspaceFiles,
        ProcessRuntimeStepAssignment assignment,
        string content)
    {
        var result = workspaceFiles.WriteTextFile(
            $"artifacts/process-runs/{assignment.RunId.Value:D}/steps/diagnose-quality-failure.md",
            content,
            overwrite: true);
        Assert.True(result.Succeeded, result.Message);
    }

    private static void WriteStockBlazorFiles(string productRoot)
    {
        var appDirectory = Path.Combine(productRoot, "src", "BusinessApp");
        Directory.CreateDirectory(Path.Combine(appDirectory, "Layout"));
        Directory.CreateDirectory(Path.Combine(appDirectory, "Pages"));
        Directory.CreateDirectory(Path.Combine(appDirectory, "wwwroot", "css"));
        Directory.CreateDirectory(Path.Combine(productRoot, "tests", "BusinessApp.Tests"));
        File.WriteAllText(Path.Combine(productRoot, "BusinessApp.slnx"), "BusinessApp");
        File.WriteAllText(Path.Combine(productRoot, "tests", "BusinessApp.Tests", "BusinessApp.Tests.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(appDirectory, "Layout", "MainLayout.razor"), "<a href=\"https://learn.microsoft.com/aspnet/core/\">About</a>");
        File.WriteAllText(Path.Combine(appDirectory, "Layout", "NavMenu.razor"), "<a href=\"counter\">Counter</a><a href=\"weather\">Weather</a>");
        File.WriteAllText(Path.Combine(appDirectory, "Pages", "Counter.razor"), "<button>Click me</button> @code { int currentCount; }");
        File.WriteAllText(Path.Combine(appDirectory, "Pages", "Weather.razor"), "WeatherForecast sample-data/weather.json");
        File.WriteAllText(Path.Combine(appDirectory, "wwwroot", "css", "app.css"), "body { color: black; }");
    }

    private static void WriteCleanBlazorFiles(string productRoot)
    {
        WriteStockBlazorFiles(productRoot);
        ApplyExpectedScaffoldRepair(productRoot);
    }

    private static void ApplyExpectedScaffoldRepair(string productRoot)
    {
        var appDirectory = Path.Combine(productRoot, "src", "BusinessApp");
        File.WriteAllText(Path.Combine(appDirectory, "Layout", "MainLayout.razor"), "<main>@Body</main>");
        File.WriteAllText(Path.Combine(appDirectory, "Layout", "NavMenu.razor"), "<nav>BusinessApp</nav>");
        File.Delete(Path.Combine(appDirectory, "Pages", "Counter.razor"));
        File.Delete(Path.Combine(appDirectory, "Pages", "Weather.razor"));
        File.WriteAllText(Path.Combine(appDirectory, "wwwroot", "css", "app.css"), "#blazor-error-ui { display: none; }");
    }

    private static string ToExternalTargetAlias(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var rootPath = Path.GetPathRoot(fullPath)
            ?? throw new InvalidOperationException($"Path '{path}' has no root.");
        var trimmedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var relativePath = fullPath[rootPath.Length..]
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
        return string.IsNullOrWhiteSpace(relativePath)
            ? $"external-target/{char.ToUpperInvariant(trimmedRoot[0])}"
            : $"external-target/{char.ToUpperInvariant(trimmedRoot[0])}/{relativePath.Replace(Path.DirectorySeparatorChar, '/')}";
    }

    private sealed class TestWorkspace : IDisposable
    {
        private readonly List<string> roots = [];

        public TestWorkspace()
        {
            WorkspaceRoot = CreateRoot("workspace");
        }

        public string WorkspaceRoot { get; }

        public string CreateProduct() => CreateRoot("product");

        public void Dispose()
        {
            foreach (var root in roots)
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch
                {
                }
            }
        }

        private string CreateRoot(string name)
        {
            var root = Path.Combine(Path.GetTempPath(), $"CanDoItAll.DotNetQualityRepairScaffoldRuntimeExecutorTests.{name}.{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            roots.Add(root);
            return root;
        }
    }

    private sealed class FakeWorkspaceProcessHost(Action<WorkspaceProcessExecutionRequest>? onExecute = null) : IWorkspaceProcessHost
    {
        public ExecutionBoundaryDescriptor DescribeBoundary()
            => new("Test", "Workspace", "None", "None", "Fake", false, "Unit test host.");

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            onExecute?.Invoke(request);
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new WorkspaceProcessExecutionResult(
                true,
                0,
                "ok",
                string.Empty,
                false,
                false,
                now,
                now,
                false,
                DescribeBoundary(),
                string.Empty));
        }
    }
}
