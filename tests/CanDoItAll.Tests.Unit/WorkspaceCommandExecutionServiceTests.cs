using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceCommandExecutionServiceTests
{
    [Fact]
    public async Task PowerShellRunScript_registers_declared_output_paths_as_artifacts()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "scripts"));
        var scriptPath = Path.Combine(workspaceRoot, "scripts", "Import-PlaywrightEvidence.ps1");
        await File.WriteAllTextAsync(scriptPath, "Write-Output 'ok'");

        var service = new WorkspaceCommandExecutionService(workspaceRoot, new FakeWorkspaceProcessHost());

        try
        {
            var result = await service.PowerShellRunScript(
                "scripts/Import-PlaywrightEvidence.ps1",
                arguments: ["-StepKey", "qa-validation"],
                outputPaths:
                [
                    "artifacts/showcases/generated-web-app/evidence/ui/qa-validation/primary-proof.png",
                    "artifacts/showcases/generated-web-app/evidence/ui/qa-validation/page.yml",
                    "artifacts/showcases/generated-web-app/evidence/ui/qa-validation/console.log",
                    "artifacts/showcases/generated-web-app/evidence/ui/qa-validation/import-summary.json"
                ]);

            Assert.True(result.Succeeded);
            Assert.True(result.Receipt.MutatesWorkspace);
            Assert.Contains(
                result.Receipt.TargetPaths,
                item => string.Equals(item, "artifacts/showcases/generated-web-app/evidence/ui/qa-validation/primary-proof.png", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                result.Receipt.ArtifactReferences,
                item => string.Equals(item.RelativePath, "artifacts/showcases/generated-web-app/evidence/ui/qa-validation/import-summary.json", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(
                result.Receipt.TargetPaths,
                item => string.Equals(item, "scripts/Import-PlaywrightEvidence.ps1", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task DotnetBuild_shortens_windows_workspace_root_when_path_budget_is_unsafe()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var workspaceRoot = CreateDeepWorkspaceRoot();
        var deliveryDirectory = Path.Combine(workspaceRoot, "deliveries", "blazor-ssr-sample-web");
        Directory.CreateDirectory(deliveryDirectory);
        await File.WriteAllTextAsync(Path.Combine(deliveryDirectory, "SampleWeb.sln"), string.Empty);
        var processHost = new FakeWorkspaceProcessHost();
        var service = new WorkspaceCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetBuild("deliveries/blazor-ssr-sample-web/SampleWeb.sln");

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.EndsWith(
                $"{Path.DirectorySeparatorChar}dotnet.exe",
                processHost.LastRequest!.ExecutablePath,
                StringComparison.OrdinalIgnoreCase);
            Assert.True(processHost.LastRequest.WorkingDirectory.Length < workspaceRoot.Length);
            Assert.Matches("^[A-Z]:\\\\$", processHost.LastRequest.WorkingDirectory);
            Assert.Equal("deliveries/blazor-ssr-sample-web/SampleWeb.sln".Replace('/', Path.DirectorySeparatorChar), processHost.LastRequest.Arguments[1]);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task PowerShellRunScript_rewrites_script_argument_under_windows_workspace_alias()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var workspaceRoot = CreateDeepWorkspaceRoot();
        var scriptDirectory = Path.Combine(workspaceRoot, "scripts");
        Directory.CreateDirectory(scriptDirectory);
        var scriptPath = Path.Combine(scriptDirectory, "Launch-WebApp.ps1");
        await File.WriteAllTextAsync(scriptPath, "Write-Output 'ok'");
        var processHost = new FakeWorkspaceProcessHost();
        var service = new WorkspaceCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.PowerShellRunScript("scripts/Launch-WebApp.ps1");

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.True(processHost.LastRequest!.WorkingDirectory.Length < scriptDirectory.Length);
            Assert.StartsWith(processHost.LastRequest.WorkingDirectory[..2], processHost.LastRequest.Arguments[4], StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(workspaceRoot, processHost.LastRequest.Arguments[4], StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task PowerShellRunScript_denies_foreground_static_server_script()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var scriptDirectory = Path.Combine(workspaceRoot, "scripts");
        Directory.CreateDirectory(scriptDirectory);
        var scriptPath = Path.Combine(scriptDirectory, "Serve-Static.ps1");
        await File.WriteAllTextAsync(
            scriptPath,
            """
            $listener = [System.Net.HttpListener]::new()
            $listener.Prefixes.Add('http://localhost:8080/')
            $listener.Start()
            while ($true) {
                $context = $listener.GetContext()
                $context.Response.Close()
            }
            """);
        var processHost = new FakeWorkspaceProcessHost();
        var service = new WorkspaceCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.PowerShellRunScript("scripts/Serve-Static.ps1");

            Assert.False(result.Succeeded);
            Assert.Contains("must not run a foreground long-running browser host", result.Message, StringComparison.Ordinal);
            Assert.Null(processHost.LastRequest);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task PowerShellRunScript_SB06_INV_001_fails_post_execution_audit_when_nonmutating_step_changes_product_root()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var productRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.ProductTarget.{Guid.NewGuid():N}", "product");
        var scriptDirectory = Path.Combine(workspaceRoot, "scripts");
        Directory.CreateDirectory(scriptDirectory);
        Directory.CreateDirectory(productRoot);
        await File.WriteAllTextAsync(
            Path.Combine(scriptDirectory, "Inspect.ps1"),
            "Write-Output 'inspected'");
        await File.WriteAllTextAsync(
            Path.Combine(productRoot, "Program.cs"),
            "Console.WriteLine(\"before\");");
        var productAlias = ToExternalTargetAlias(productRoot);
        var processHost = new FakeWorkspaceProcessHost(
            onExecute: _ => File.WriteAllText(
                Path.Combine(productRoot, "Program.cs"),
                "Console.WriteLine(\"after\");"));
        var service = new WorkspaceCommandExecutionService(workspaceRoot, processHost);
        var run = CreateProcessStepExecutionRun(
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                [ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey] = new[] { productAlias },
                [ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey] = false
            }));

        try
        {
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                var result = await service.PowerShellRunScript(
                    "scripts/Inspect.ps1",
                    sideEffectManifest: JsonSerializer.Serialize(new GovernedScriptSideEffectManifest
                    {
                        Mode = GovernedScriptSideEffectMode.NoMutation
                    }));

                Assert.False(result.Succeeded);
                Assert.Contains("Post-execution product target audit", result.Message, StringComparison.Ordinal);
                Assert.Contains(productAlias, result.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
            TryDeleteDirectory(Path.GetDirectoryName(productRoot) ?? productRoot);
        }
    }

    [Fact]
    public async Task DotnetRun_http_smoke_uses_project_directory_and_returns_launch_evidence_targets()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workspaceRoot, "apps", "SampleWeb");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "SampleWeb.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        var processHost = new FakeWorkspaceProcessHost();
        var service = new WorkspaceCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetRun(
                "apps/SampleWeb/SampleWeb.csproj",
                url: "http://127.0.0.1:5123/",
                startupTimeoutSeconds: 5,
                timeoutSeconds: 20);

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal("workspace_dotnet_run", processHost.LastRequest!.ToolName);
            Assert.Equal(projectDirectory, processHost.LastRequest.WorkingDirectory);
            Assert.DoesNotContain("-EncodedCommand", processHost.LastRequest.Arguments);
            Assert.Contains("-File", processHost.LastRequest.Arguments);
            Assert.Contains(result.Receipt.TargetPaths, item => item.EndsWith("startup.json", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Receipt.TargetPaths, item => item.EndsWith("run.ps1", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Receipt.TargetPaths, item => string.Equals(item, "apps/SampleWeb/SampleWeb.csproj", StringComparison.OrdinalIgnoreCase));

            var argumentLength = string.Join(" ", processHost.LastRequest.Arguments).Length;
            Assert.True(argumentLength < 8191);
            var script = await ReadGeneratedDotnetRunScriptAsync(processHost);
            Assert.Contains("http://127.0.0.1:5123", script, StringComparison.Ordinal);
            Assert.Contains("'--urls'", script, StringComparison.Ordinal);
            Assert.Contains("$keepAlive = $false", script, StringComparison.Ordinal);
            Assert.Contains("$env:ASPNETCORE_ENVIRONMENT = 'Development'", script, StringComparison.Ordinal);
            Assert.Contains("$env:DOTNET_ENVIRONMENT = 'Development'", script, StringComparison.Ordinal);
            Assert.Contains("aspNetCoreEnvironment = $env:ASPNETCORE_ENVIRONMENT", script, StringComparison.Ordinal);
            Assert.Contains("Stop-AppProcessTree $processTreeIds", script, StringComparison.Ordinal);
            Assert.Contains("Process tree was stopped after smoke validation", script, StringComparison.Ordinal);
            Assert.DoesNotContain("workflow", script, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("converter", script, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetRun_http_smoke_can_keep_process_alive_for_browser_follow_up()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workspaceRoot, "apps", "SampleWeb");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "SampleWeb.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        var processHost = new FakeWorkspaceProcessHost();
        var service = new WorkspaceCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetRun(
                "apps/SampleWeb/SampleWeb.csproj",
                url: "http://127.0.0.1:5125/",
                startupTimeoutSeconds: 5,
                timeoutSeconds: 20,
                keepAlive: true);

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            var script = await ReadGeneratedDotnetRunScriptAsync(processHost);
            Assert.Contains("$keepAlive = $true", script, StringComparison.Ordinal);
            Assert.Contains("The process tree is still running for follow-up browser proof", script, StringComparison.Ordinal);
            Assert.Contains("stopCommand", script, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetRun_http_smoke_can_mark_process_run_lifetime_for_downstream_capture()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workspaceRoot, "apps", "SampleWeb");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "SampleWeb.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        var processHost = new FakeWorkspaceProcessHost();
        var service = new WorkspaceCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetRun(
                "apps/SampleWeb/SampleWeb.csproj",
                url: "http://127.0.0.1:5126/",
                startupTimeoutSeconds: 5,
                timeoutSeconds: 20,
                keepAlive: true,
                lifetimeScope: WorkspaceProcessLifetimeScope.ProcessRun);

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            var script = await ReadGeneratedDotnetRunScriptAsync(processHost);
            Assert.Contains("$keepAlive = $true", script, StringComparison.Ordinal);
            Assert.Contains("$lifetimeScope = 'ProcessRun'", script, StringComparison.Ordinal);
            Assert.Contains("lifetimeScope = $lifetimeScope", script, StringComparison.Ordinal);
            Assert.Contains("stopCommand", script, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetTest_accepts_project_directory_when_target_is_unambiguous()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var testProjectDirectory = Path.Combine(workspaceRoot, "tests", "SampleWeb.Tests");
        Directory.CreateDirectory(testProjectDirectory);
        var testProjectPath = Path.Combine(testProjectDirectory, "SampleWeb.Tests.csproj");
        await File.WriteAllTextAsync(testProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var processHost = new FakeWorkspaceProcessHost();
        var service = new WorkspaceCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetTest("tests/SampleWeb.Tests");

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal("workspace_dotnet_test", processHost.LastRequest!.ToolName);
            Assert.Equal("test", processHost.LastRequest.Arguments[0]);
            Assert.Equal("tests/SampleWeb.Tests/SampleWeb.Tests.csproj".Replace('/', Path.DirectorySeparatorChar), processHost.LastRequest.Arguments[1]);
            Assert.Contains("tests/SampleWeb.Tests/SampleWeb.Tests.csproj", result.Receipt.TargetPaths, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetTest_failure_result_points_agent_to_captured_diagnostics()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var testProjectDirectory = Path.Combine(workspaceRoot, "tests", "SampleWeb.Tests");
        Directory.CreateDirectory(testProjectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(testProjectDirectory, "SampleWeb.Tests.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var processHost = new FakeWorkspaceProcessHost(exitCode: 1, stdout: "CS0246 missing reference", stderr: "Build failed");
        var service = new WorkspaceCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetTest("tests/SampleWeb.Tests");

            Assert.False(result.Succeeded);
            Assert.Contains("Inspect captured diagnostics before editing or retrying", result.Message, StringComparison.Ordinal);
            Assert.Contains("stdout.txt", result.Message, StringComparison.Ordinal);
            Assert.Contains("stderr.txt", result.Message, StringComparison.Ordinal);
            Assert.Contains("Captured diagnostics preview", result.Message, StringComparison.Ordinal);
            Assert.Contains("Build failed", result.Message, StringComparison.Ordinal);
            Assert.Contains("CS0246 missing reference", result.Message, StringComparison.Ordinal);
            Assert.Contains("dotnet_test stdout", result.DiagnosticArtifactSummary, StringComparison.Ordinal);
            Assert.Contains(
                result.ArtifactReferences,
                item => item.RelativePath.EndsWith("stdout.txt", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                result.ArtifactReferences,
                item => item.RelativePath.EndsWith("stderr.txt", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetRun_accepts_project_directory_when_target_is_unambiguous()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workspaceRoot, "apps", "SampleWeb");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "SampleWeb.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        var processHost = new FakeWorkspaceProcessHost();
        var service = new WorkspaceCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetRun(
                "apps/SampleWeb",
                url: "http://127.0.0.1:5124/",
                startupTimeoutSeconds: 5,
                timeoutSeconds: 20);

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal(projectDirectory, processHost.LastRequest!.WorkingDirectory);
            Assert.Contains(result.Receipt.TargetPaths, item => string.Equals(item, "apps/SampleWeb/SampleWeb.csproj", StringComparison.OrdinalIgnoreCase));

            var script = await ReadGeneratedDotnetRunScriptAsync(processHost);
            Assert.Contains(Path.Combine(projectDirectory, "SampleWeb.csproj"), script, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetBuild_rejects_ambiguous_project_directory()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var appDirectory = Path.Combine(workspaceRoot, "apps", "Ambiguous");
        Directory.CreateDirectory(appDirectory);
        await File.WriteAllTextAsync(Path.Combine(appDirectory, "First.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        await File.WriteAllTextAsync(Path.Combine(appDirectory, "Second.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var service = new WorkspaceCommandExecutionService(workspaceRoot, new FakeWorkspaceProcessHost());

        try
        {
            var result = await service.DotnetBuild("apps/Ambiguous");

            Assert.False(result.Succeeded);
            Assert.Contains("contains multiple .NET project files", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("dotnet_build", result.RecipeId);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetRun_foreground_uses_dotnet_run_for_runnable_project()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workspaceRoot, "apps", "WorkerApp");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "WorkerApp.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var processHost = new FakeWorkspaceProcessHost();
        var service = new WorkspaceCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetRun(
                "apps/WorkerApp/WorkerApp.csproj",
                waitForHttp: false,
                noBuild: false);

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal("workspace_dotnet_run", processHost.LastRequest!.ToolName);
            Assert.Equal("run", processHost.LastRequest.Arguments[0]);
            Assert.Equal("--project", processHost.LastRequest.Arguments[1]);
            Assert.EndsWith("WorkerApp.csproj", processHost.LastRequest.Arguments[2], StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("--no-build", processHost.LastRequest.Arguments);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetRun_foreground_request_uses_http_smoke_for_blazor_webassembly_project()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workspaceRoot, "apps", "TetrisGame");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "TetrisGame.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\" />");
        var processHost = new FakeWorkspaceProcessHost();
        var service = new WorkspaceCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetRun(
                "apps/TetrisGame/TetrisGame.csproj",
                waitForHttp: false,
                noBuild: false);

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal("workspace_dotnet_run", processHost.LastRequest!.ToolName);
            Assert.DoesNotContain("-EncodedCommand", processHost.LastRequest.Arguments);
            Assert.Contains("-File", processHost.LastRequest.Arguments);
            Assert.Contains(result.Receipt.TargetPaths, item => item.EndsWith("startup.json", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Receipt.TargetPaths, item => item.EndsWith("run.ps1", StringComparison.OrdinalIgnoreCase));

            var script = await ReadGeneratedDotnetRunScriptAsync(processHost);
            Assert.Contains("'--urls'", script, StringComparison.Ordinal);
            Assert.Contains("$noBuild = $false", script, StringComparison.Ordinal);
            Assert.Contains("Process tree was stopped after smoke validation", script, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetNew_accepts_razor_pages_webapp_template()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "apps"));
        var processHost = new FakeWorkspaceProcessHost();
        var service = new WorkspaceCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetNew("webapp", "TrailheadSnackBox.Web", "apps");

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal("workspace_dotnet_new", processHost.LastRequest!.ToolName);
            Assert.Equal(["new", "webapp", "-n", "TrailheadSnackBox.Web"], processHost.LastRequest.Arguments);
            Assert.Equal(Path.Combine(workspaceRoot, "apps"), processHost.LastRequest.WorkingDirectory);
            Assert.Contains("apps/TrailheadSnackBox.Web", result.Receipt.TargetPaths, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetNew_accepts_blazor_webassembly_template()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "apps"));
        var processHost = new FakeWorkspaceProcessHost();
        var service = new WorkspaceCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetNew("blazorwasm", "TetrisGame", "apps");

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal("workspace_dotnet_new", processHost.LastRequest!.ToolName);
            Assert.Equal(["new", "blazorwasm", "-n", "TetrisGame"], processHost.LastRequest.Arguments);
            Assert.Equal(Path.Combine(workspaceRoot, "apps"), processHost.LastRequest.WorkingDirectory);
            Assert.Contains("apps/TetrisGame", result.Receipt.TargetPaths, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetNew_accepts_empty_blazor_webassembly_template()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "apps"));
        var processHost = new FakeWorkspaceProcessHost();
        var service = new WorkspaceCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetNew("blazorwasm-empty", "TetrisGame", "apps");

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal("workspace_dotnet_new", processHost.LastRequest!.ToolName);
            Assert.Equal(["new", "blazorwasm-empty", "-n", "TetrisGame"], processHost.LastRequest.Arguments);
            Assert.Equal(Path.Combine(workspaceRoot, "apps"), processHost.LastRequest.WorkingDirectory);
            Assert.Contains("apps/TetrisGame", result.Receipt.TargetPaths, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetNew_accepts_solution_template()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "apps"));
        var processHost = new FakeWorkspaceProcessHost();
        var service = new WorkspaceCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetNew("sln", "SampleSuite", "apps");

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal("workspace_dotnet_new", processHost.LastRequest!.ToolName);
            Assert.Equal(["new", "sln", "-n", "SampleSuite"], processHost.LastRequest.Arguments);
            Assert.Equal(Path.Combine(workspaceRoot, "apps"), processHost.LastRequest.WorkingDirectory);
            Assert.Contains("apps/SampleSuite.slnx", result.Receipt.TargetPaths, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("apps/SampleSuite.sln", result.Receipt.TargetPaths, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("apps/SampleSuite", result.Receipt.TargetPaths, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    private static string CreateDeepWorkspaceRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "CanDoItAll.WorkspaceCommandExecutionServiceTests", Guid.NewGuid().ToString("N"), "workspace");
        while (root.Length < 140)
        {
            root = Path.Combine(root, "nested-segment");
        }

        Directory.CreateDirectory(root);
        return root;
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
            throw new InvalidOperationException($"Path '{path}' cannot be represented as an external-target alias.");
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

    private static ExecutionRunRecord CreateProcessStepExecutionRun(string metadataJson)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Process step",
            SourceKind: "process-step",
            SourceId: "step-001",
            CorrelationId: Guid.NewGuid().ToString("N"),
            CausationId: string.Empty,
            RequestedBy: "unit-test",
            RequestedByKind: "system",
            MetadataJson: metadataJson,
            InputSummary: string.Empty,
            ResultSummary: string.Empty,
            ProviderName: "test",
            Model: "test",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [],
            ProcessRunId: "process-run-001",
            ProcessStepId: "step-001");
    }

    private static async Task<string> ReadGeneratedDotnetRunScriptAsync(FakeWorkspaceProcessHost processHost)
    {
        Assert.NotNull(processHost.LastRequest);
        var fileIndex = processHost.LastRequest!.Arguments.ToList().IndexOf("-File");
        Assert.True(fileIndex >= 0);
        Assert.True(fileIndex + 1 < processHost.LastRequest.Arguments.Count);
        var scriptPath = processHost.LastRequest.Arguments[fileIndex + 1];
        Assert.True(File.Exists(scriptPath));
        return await File.ReadAllTextAsync(scriptPath);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private sealed class FakeWorkspaceProcessHost : IWorkspaceProcessHost
    {
        private readonly int exitCode;
        private readonly string stdout;
        private readonly string stderr;
        private readonly Action<WorkspaceProcessExecutionRequest>? onExecute;

        public FakeWorkspaceProcessHost(
            int exitCode = 0,
            string stdout = "ok",
            string stderr = "",
            Action<WorkspaceProcessExecutionRequest>? onExecute = null)
        {
            this.exitCode = exitCode;
            this.stdout = stdout;
            this.stderr = stderr;
            this.onExecute = onExecute;
        }

        public WorkspaceProcessExecutionRequest? LastRequest { get; private set; }

        public ExecutionBoundaryDescriptor DescribeBoundary()
        {
            return new ExecutionBoundaryDescriptor(
                Mode: "Test",
                FilesystemScope: "Workspace",
                NetworkScope: "None",
                CredentialScope: "None",
                HostLabel: "Fake",
                IsEnforcedByHost: false,
                Notes: "Unit test host.");
        }

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(WorkspaceProcessExecutionRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            onExecute?.Invoke(request);
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new WorkspaceProcessExecutionResult(
                Started: true,
                ExitCode: exitCode,
                Stdout: stdout,
                Stderr: stderr,
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
