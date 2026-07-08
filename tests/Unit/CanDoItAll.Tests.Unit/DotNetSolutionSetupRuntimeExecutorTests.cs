using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class DotNetSolutionSetupRuntimeExecutorTests
{
    [Fact]
    public async Task TryExecuteAsync_scaffolds_missing_solution_and_project_before_helper_readback()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        var processHost = new FakeWorkspaceProcessHost(request =>
        {
            if (request.ToolName == "workspace_dotnet_new")
            {
                CreateDotNetNewOutput(request);
            }
            else if (request.ToolName == "workspace_pwsh_run_script")
            {
                WriteSolutionMembership(productRoot);
            }
        });
        var executor = new DotNetSolutionSetupRuntimeExecutor(
            new WorkspaceFileService(workspace.WorkspaceRoot),
            new WorkspaceCommandExecutionService(workspace.WorkspaceRoot, processHost));

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot));

        Assert.NotNull(result);
        Assert.True(result!.Succeeded, result.Summary);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, result.Output!.Status);
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_dotnet_new" && receipt.RequestSummary.StartsWith("new sln ", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_dotnet_new" && receipt.RequestSummary.StartsWith("new blazorwasm ", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_pwsh_run_script" && receipt.ExitSummary.Contains("Succeeded", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, processHost.Requests.Count);
        Assert.Contains("src/Calculator/Calculator.csproj", await File.ReadAllTextAsync(Path.Combine(productRoot, "Calculator.slnx")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryExecuteAsync_repairs_existing_project_empty_solution_without_regenerating_project()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        Directory.CreateDirectory(Path.Combine(productRoot, "src", "Calculator"));
        await File.WriteAllTextAsync(Path.Combine(productRoot, "Calculator.slnx"), string.Empty);
        await File.WriteAllTextAsync(
            Path.Combine(productRoot, "src", "Calculator", "Calculator.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\" />");
        var processHost = new FakeWorkspaceProcessHost(request =>
        {
            if (request.ToolName == "workspace_pwsh_run_script")
            {
                WriteSolutionMembership(productRoot);
            }
        });
        var executor = new DotNetSolutionSetupRuntimeExecutor(
            new WorkspaceFileService(workspace.WorkspaceRoot),
            new WorkspaceCommandExecutionService(workspace.WorkspaceRoot, processHost));

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot));

        Assert.NotNull(result);
        Assert.True(result!.Succeeded, result.Summary);
        Assert.Single(processHost.Requests);
        Assert.Equal("workspace_pwsh_run_script", processHost.Requests[0].ToolName);
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_dotnet_new" && receipt.RiskClass == "RuntimeOwned:IdempotentSkip");
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_write_file" && receipt.RequestSummary.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_stat_path" && receipt.ExitSummary.Contains("Succeeded", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("src/Calculator/Calculator.csproj", await File.ReadAllTextAsync(Path.Combine(productRoot, "Calculator.slnx")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryExecuteAsync_returns_failure_when_helper_does_not_satisfy_readback()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        Directory.CreateDirectory(Path.Combine(productRoot, "src", "Calculator"));
        await File.WriteAllTextAsync(Path.Combine(productRoot, "Calculator.slnx"), string.Empty);
        await File.WriteAllTextAsync(
            Path.Combine(productRoot, "src", "Calculator", "Calculator.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\" />");
        var executor = new DotNetSolutionSetupRuntimeExecutor(
            new WorkspaceFileService(workspace.WorkspaceRoot),
            new WorkspaceCommandExecutionService(workspace.WorkspaceRoot, new FakeWorkspaceProcessHost()));

        var result = await executor.TryExecuteAsync(CreateAssignment(productRoot));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Contains("readback", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_pwsh_run_script" && receipt.ExitSummary.Contains("Succeeded", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryExecuteAsync_runs_add_test_project_helper_and_verifies_solution_and_project_reference()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        WriteExistingAppProject(productRoot);
        await File.WriteAllTextAsync(
            Path.Combine(productRoot, "Calculator.slnx"),
            "src/Calculator/Calculator.csproj");
        var processHost = new FakeWorkspaceProcessHost(request =>
        {
            if (request.ToolName == "workspace_pwsh_run_script")
            {
                WriteTestProjectMembership(productRoot);
            }
        });
        var executor = new DotNetSolutionSetupRuntimeExecutor(
            new WorkspaceFileService(workspace.WorkspaceRoot),
            new WorkspaceCommandExecutionService(workspace.WorkspaceRoot, processHost));

        var result = await executor.TryExecuteAsync(CreateAssignment(
            productRoot,
            "add-test-project",
            CreateAddTestProjectLaunchVariables(productRoot)));

        Assert.NotNull(result);
        Assert.True(result!.Succeeded, result.Summary);
        Assert.Equal(ProcessStepOutcomeStatus.Completed, result.Output!.Status);
        Assert.Single(processHost.Requests);
        Assert.Equal("workspace_pwsh_run_script", processHost.Requests[0].ToolName);
        Assert.DoesNotContain(result.ToolReceipts, receipt => receipt.ToolName == "workspace_dotnet_new");
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_pwsh_run_script" && receipt.ExitSummary.Contains("Succeeded", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("tests/Calculator.Tests/Calculator.Tests.csproj", await File.ReadAllTextAsync(Path.Combine(productRoot, "Calculator.slnx")), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("../../src/Calculator/Calculator.csproj", await File.ReadAllTextAsync(Path.Combine(productRoot, "tests", "Calculator.Tests", "Calculator.Tests.csproj")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryExecuteAsync_returns_failure_when_helper_script_fails()
    {
        using var workspace = new RuntimeExecutorWorkspace();
        var productRoot = workspace.CreateProductRoot();
        WriteExistingAppProject(productRoot);
        await File.WriteAllTextAsync(
            Path.Combine(productRoot, "Calculator.slnx"),
            "src/Calculator/Calculator.csproj");
        var processHost = new FakeWorkspaceProcessHost
        {
            ExitCode = 1,
            FailureMessage = "script failed",
            Stderr = "membership command failed"
        };
        var executor = new DotNetSolutionSetupRuntimeExecutor(
            new WorkspaceFileService(workspace.WorkspaceRoot),
            new WorkspaceCommandExecutionService(workspace.WorkspaceRoot, processHost));

        var result = await executor.TryExecuteAsync(CreateAssignment(
            productRoot,
            "add-test-project",
            CreateAddTestProjectLaunchVariables(productRoot)));

        Assert.NotNull(result);
        Assert.False(result!.Succeeded);
        Assert.Contains("helper failed", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.ToolReceipts, receipt => receipt.ToolName == "workspace_pwsh_run_script" && receipt.ExitSummary.Contains("Failed (exit 1)", StringComparison.OrdinalIgnoreCase));
    }

    private static ProcessRuntimeStepAssignment CreateAssignment(
        string productRoot,
        string stepKey = "create-dotnet-project",
        IReadOnlyDictionary<string, string>? launchVariables = null)
    {
        return new ProcessRuntimeStepAssignment(
            ProcessRunId.New(),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            stepKey,
            "dotnet-developer",
            "dotnet-developer",
            ".NET developer",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            ".NET Application Developer",
            "Runtime-owned .NET setup.",
            "sha256:readiness",
            "Resolved from role fit.",
            [ArtifactSlotId.New()],
            [],
            [ProcessOperationContractNames.MutateProductTarget],
            ProcessOperationContractNames.ExternalProductTargetMutable,
            launchVariables ?? CreateLaunchVariables(productRoot),
            BranchGate: null,
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyDictionary<string, string> CreateLaunchVariables(string productRoot)
    {
        var solutionFile = Path.Combine(productRoot, "Calculator.slnx");
        var appProjectFile = Path.Combine(productRoot, "src", "Calculator", "Calculator.csproj");
        var scriptRef = $"artifacts/process-runs/{Guid.NewGuid():D}/scripts/create-dotnet-project.wire-solution.ps1";
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OutputRoot"] = productRoot,
            ["ProductRoot"] = productRoot,
            ["ExternalTargetRoot"] = productRoot,
            ["DotNetSolutionFile"] = solutionFile,
            ["DotNetAppProjectFile"] = appProjectFile,
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
                $"Invoke workspace_dotnet_new for template 'sln'. Invoke workspace_dotnet_new for template 'blazorwasm'. Invoke workspace_pwsh_run_script with path '{scriptRef}', workingDirectory '{ToExternalTargetAlias(productRoot)}', sideEffectManifest from DotNetCreateProjectSideEffectManifest. Read back the solution file.",
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep] = JsonSerializer.Serialize(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["create-dotnet-project"] =
                    [
                        "template=sln",
                        "template=blazorwasm",
                        "workspace_pwsh_run_script"
                    ]
                }),
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredPathsByStep] = JsonSerializer.Serialize(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["create-dotnet-project"] =
                    [
                        solutionFile,
                        appProjectFile
                    ]
                }),
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep] = JsonSerializer.Serialize(
                new Dictionary<string, object[]>(StringComparer.Ordinal)
                {
                    ["create-dotnet-project"] =
                    [
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["pathCandidates"] = new[] { solutionFile },
                            ["requiredTextAnyGroups"] = new[] { new[] { "src/Calculator/Calculator.csproj" } }
                        }
                    ]
                })
        };
    }

    private static IReadOnlyDictionary<string, string> CreateAddTestProjectLaunchVariables(string productRoot)
    {
        var solutionFile = Path.Combine(productRoot, "Calculator.slnx");
        var appProjectFile = Path.Combine(productRoot, "src", "Calculator", "Calculator.csproj");
        var testProjectFile = Path.Combine(productRoot, "tests", "Calculator.Tests", "Calculator.Tests.csproj");
        var scriptRef = $"artifacts/process-runs/{Guid.NewGuid():D}/scripts/add-test-project.wire-solution.ps1";
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OutputRoot"] = productRoot,
            ["ProductRoot"] = productRoot,
            ["ExternalTargetRoot"] = productRoot,
            ["DotNetSolutionFile"] = solutionFile,
            ["DotNetAppProjectFile"] = appProjectFile,
            ["DotNetTestProjectFile"] = testProjectFile,
            ["DotNetTestTemplate"] = "mstest",
            ["DotNetTestProjectName"] = "Calculator.Tests",
            ["DotNetAddTestProjectScriptRef"] = scriptRef,
            ["DotNetAddTestProjectScript"] = "dotnet new mstest -n Calculator.Tests -o tests/Calculator.Tests; dotnet sln $SolutionFile add $TestProjectFile; dotnet add $TestProjectFile reference $AppProjectFile; dotnet sln $SolutionFile list",
            ["DotNetAddTestProjectSideEffectManifest"] = JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["version"] = 1,
                ["mode"] = "ProductMutation",
                ["declaredReadPaths"] = new[] { solutionFile, appProjectFile, testProjectFile },
                ["declaredWritePaths"] = new[] { solutionFile, testProjectFile },
                ["allowShellDelegation"] = true
            }),
            ["DotNetAddTestProjectExecutionPlan"] =
                $"Write DotNetAddTestProjectScript to {scriptRef}. Invoke workspace_pwsh_run_script with path '{scriptRef}', workingDirectory '{ToExternalTargetAlias(productRoot)}', sideEffectManifest from DotNetAddTestProjectSideEffectManifest. Read back the solution and test project files.",
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep] = JsonSerializer.Serialize(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["add-test-project"] = ["workspace_pwsh_run_script"]
                }),
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredPathsByStep] = JsonSerializer.Serialize(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["add-test-project"] =
                    [
                        solutionFile,
                        appProjectFile,
                        testProjectFile
                    ]
                }),
            [ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep] = JsonSerializer.Serialize(
                new Dictionary<string, object[]>(StringComparer.Ordinal)
                {
                    ["add-test-project"] =
                    [
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["pathCandidates"] = new[] { solutionFile },
                            ["requiredTextAnyGroups"] = new[] { new[] { "src/Calculator/Calculator.csproj" } }
                        },
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["pathCandidates"] = new[] { solutionFile },
                            ["requiredTextAnyGroups"] = new[] { new[] { "tests/Calculator.Tests/Calculator.Tests.csproj" } }
                        },
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["pathCandidates"] = new[] { testProjectFile },
                            ["requiredTextAnyGroups"] = new[] { new[] { "../../src/Calculator/Calculator.csproj" } }
                        }
                    ]
                })
        };
    }

    private static void CreateDotNetNewOutput(WorkspaceProcessExecutionRequest request)
    {
        var template = request.Arguments[1];
        var nameIndex = request.Arguments.ToList().IndexOf("-n");
        var name = request.Arguments[nameIndex + 1];
        if (string.Equals(template, "sln", StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllText(Path.Combine(request.WorkingDirectory, $"{name}.slnx"), string.Empty);
            return;
        }

        var projectDirectory = Path.Combine(request.WorkingDirectory, name);
        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(Path.Combine(projectDirectory, $"{name}.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\" />");
    }

    private static void WriteSolutionMembership(string productRoot)
    {
        File.WriteAllText(
            Path.Combine(productRoot, "Calculator.slnx"),
            "src/Calculator/Calculator.csproj");
    }

    private static void WriteExistingAppProject(string productRoot)
    {
        Directory.CreateDirectory(Path.Combine(productRoot, "src", "Calculator"));
        File.WriteAllText(
            Path.Combine(productRoot, "src", "Calculator", "Calculator.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\" />");
    }

    private static void WriteTestProjectMembership(string productRoot)
    {
        Directory.CreateDirectory(Path.Combine(productRoot, "tests", "Calculator.Tests"));
        File.WriteAllText(
            Path.Combine(productRoot, "Calculator.slnx"),
            """
            src/Calculator/Calculator.csproj
            tests/Calculator.Tests/Calculator.Tests.csproj
            """);
        File.WriteAllText(
            Path.Combine(productRoot, "tests", "Calculator.Tests", "Calculator.Tests.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="../../src/Calculator/Calculator.csproj" />
              </ItemGroup>
            </Project>
            """);
    }

    private static string ToExternalTargetAlias(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var rootPath = Path.GetPathRoot(fullPath)
            ?? throw new InvalidOperationException($"Path '{path}' has no root.");
        var trimmedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmedRoot.Length != 2 ||
            trimmedRoot[1] != ':' ||
            !char.IsLetter(trimmedRoot[0]))
        {
            return fullPath;
        }

        var relativePath = fullPath.Length <= rootPath.Length
            ? string.Empty
            : fullPath[rootPath.Length..]
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);
        return string.IsNullOrWhiteSpace(relativePath)
            ? $"external-target/{char.ToUpperInvariant(trimmedRoot[0])}"
            : $"external-target/{char.ToUpperInvariant(trimmedRoot[0])}/{relativePath.Replace(Path.DirectorySeparatorChar, '/')}";
    }

    private sealed class RuntimeExecutorWorkspace : IDisposable
    {
        private readonly List<string> roots = [];

        public RuntimeExecutorWorkspace()
        {
            WorkspaceRoot = CreateRoot("Workspace");
        }

        public string WorkspaceRoot { get; }

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
            var root = Path.Combine(Path.GetTempPath(), $"CanDoItAll.DotNetSolutionSetupRuntimeExecutorTests.{name}.{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            roots.Add(root);
            return root;
        }
    }

    private sealed class FakeWorkspaceProcessHost(Action<WorkspaceProcessExecutionRequest>? onExecute = null) : IWorkspaceProcessHost
    {
        public List<WorkspaceProcessExecutionRequest> Requests { get; } = [];

        public int ExitCode { get; init; }

        public string Stdout { get; init; } = "ok";

        public string Stderr { get; init; } = string.Empty;

        public bool TimedOut { get; init; }

        public string FailureMessage { get; init; } = string.Empty;

        public ExecutionBoundaryDescriptor DescribeBoundary()
        {
            return new ExecutionBoundaryDescriptor(
                "Test",
                "Workspace",
                "None",
                "None",
                "Fake",
                IsEnforcedByHost: false,
                "Unit test host.");
        }

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
                Stdout: Stdout,
                Stderr: Stderr,
                StdoutTruncated: false,
                StderrTruncated: false,
                StartedAtUtc: now,
                CompletedAtUtc: now,
                TimedOut: TimedOut,
                Boundary: DescribeBoundary(),
                FailureMessage: FailureMessage));
        }
    }
}
