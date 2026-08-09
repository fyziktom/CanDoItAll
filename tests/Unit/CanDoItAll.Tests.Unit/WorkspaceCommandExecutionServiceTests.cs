using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceCommandExecutionServiceTests
{
    [Fact]
    public async Task GitWorkspaceTools_build_standard_read_command_plans()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var status = await service.GitStatus(includeBranch: true);
            Assert.True(status.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal("workspace_git_status", processHost.LastRequest!.ToolName);
            Assert.Equal(["status", "--short", "--branch"], processHost.LastRequest.Arguments);

            var diff = await service.GitDiff(nameOnly: true);
            Assert.True(diff.Succeeded);
            Assert.Equal("workspace_git_diff", processHost.LastRequest.ToolName);
            Assert.Equal(["diff", "--name-only"], processHost.LastRequest.Arguments);

            var log = await service.GitLog(count: 5);
            Assert.True(log.Succeeded);
            Assert.Equal("workspace_git_log", processHost.LastRequest.ToolName);
            Assert.Equal(["log", "-5", "--oneline"], processHost.LastRequest.Arguments);

            var show = await service.GitShow("HEAD");
            Assert.True(show.Succeeded);
            Assert.Equal("workspace_git_show", processHost.LastRequest.ToolName);
            Assert.Equal(["show", "--stat", "HEAD"], processHost.LastRequest.Arguments);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task GitWorkspaceTools_build_standard_mutation_command_plans()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "src"));
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "src", "Feature.cs"), "namespace Sample;");
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var add = await service.GitAdd(["src/Feature.cs"]);
            Assert.True(add.Succeeded);
            Assert.True(add.Receipt.MutatesWorkspace);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal("workspace_git_add", processHost.LastRequest!.ToolName);
            Assert.Equal(["add", "--", "src/Feature.cs"], processHost.LastRequest.Arguments);

            var unstage = await service.GitUnstage(["src/Feature.cs"]);
            Assert.True(unstage.Succeeded);
            Assert.Equal("workspace_git_unstage", processHost.LastRequest.ToolName);
            Assert.Equal(["restore", "--staged", "--", "src/Feature.cs"], processHost.LastRequest.Arguments);

            var commit = await service.GitCommit("Implement git wrapper tools");
            Assert.True(commit.Succeeded);
            Assert.Equal("workspace_git_commit", processHost.LastRequest.ToolName);
            Assert.Equal(["commit", "-m", "Implement git wrapper tools"], processHost.LastRequest.Arguments);

            var branch = await service.GitBranchCreate("codex/git-tools");
            Assert.True(branch.Succeeded);
            Assert.Equal("workspace_git_branch_create", processHost.LastRequest.ToolName);
            Assert.Equal(["branch", "codex/git-tools"], processHost.LastRequest.Arguments);

            var switchBranch = await service.GitSwitch("codex/git-tools");
            Assert.True(switchBranch.Succeeded);
            Assert.Equal("workspace_git_switch", processHost.LastRequest.ToolName);
            Assert.Equal(["switch", "codex/git-tools"], processHost.LastRequest.Arguments);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task GitWorkspaceTools_reject_unsafe_inputs_before_process_execution()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var show = await service.GitShow("--stat");
            var add = await service.GitAdd([".git/config"]);

            Assert.False(show.Succeeded);
            Assert.Contains("Git revision is invalid", show.Message, StringComparison.Ordinal);
            Assert.False(add.Succeeded);
            Assert.Contains("authorized repository-relative path", add.Message, StringComparison.Ordinal);
            Assert.Null(processHost.LastRequest);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task Command_input_path_failure_returns_actionable_message_without_physical_path()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        var privatePath = Path.Combine(
            Path.GetPathRoot(workspaceRoot)!,
            "private",
            "command-provider-secret.py");
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.PythonRunFile(privatePath);

            Assert.False(result.Succeeded);
            Assert.Contains("allowed workspace scope", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(privatePath, result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(processHost.LastRequest);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task Unexpected_process_host_failure_preserves_exception_and_is_not_model_mappable()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        var sentinel = new IOException(
            @"Provider I/O failed while reading C:\private\command-provider-secret.txt");
        var processHost = new FakeWorkspaceProcessHost(onExecute: _ => throw sentinel);
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var exception = await Assert.ThrowsAsync<IOException>(() => service.GitStatus());

            Assert.Same(sentinel, exception);
            Assert.False(MafAgentToolFailureMapper.TryMap(exception, out _));
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task PowerShellRunScript_registers_declared_output_paths_as_artifacts()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "scripts"));
        var scriptPath = Path.Combine(workspaceRoot, "scripts", "Import-PlaywrightEvidence.ps1");
        await File.WriteAllTextAsync(scriptPath, "Write-Output 'ok'");

        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, new FakeWorkspaceProcessHost());

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
    public async Task PowerShellRunScript_persists_declared_product_mutation_mode_in_audit_receipt()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var scriptDirectory = Path.Combine(workspaceRoot, "scripts");
        Directory.CreateDirectory(scriptDirectory);
        await File.WriteAllTextAsync(Path.Combine(scriptDirectory, "Apply-Update.ps1"), "Write-Output 'ok'");
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, new FakeWorkspaceProcessHost());
        var run = CreateProcessStepExecutionRun("{}");

        try
        {
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                var result = await service.PowerShellRunScript(
                    "scripts/Apply-Update.ps1",
                    sideEffectManifest: JsonSerializer.Serialize(new GovernedScriptSideEffectManifest
                    {
                        Mode = GovernedScriptSideEffectMode.ProductMutation
                    }));

                Assert.True(result.Succeeded);
            }

            var auditRoot = WorkspaceExecutionAuditTrailWriter.GetRunAuditRoot(
                workspaceRoot,
                WorkspaceScopeDescriptor.Sandbox,
                run.Id);
            var receiptPath = Assert.Single(Directory.EnumerateFiles(
                Path.Combine(auditRoot, "receipts"),
                "*.json",
                SearchOption.TopDirectoryOnly));
            var receipt = JsonSerializer.Deserialize<ToolExecutionReceiptRecord>(
                await File.ReadAllTextAsync(receiptPath),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            Assert.NotNull(receipt);
            Assert.Equal(ToolExecutionSideEffectMode.ProductMutation, receipt.DeclaredSideEffectMode);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
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
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

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
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

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
    public async Task PowerShellRunScript_rewrites_external_target_alias_arguments_to_native_paths()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var productRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.ProductTarget.{Guid.NewGuid():N}", "calculator-output");
        var scriptDirectory = Path.Combine(workspaceRoot, "scripts");
        var appProjectDirectory = Path.Combine(productRoot, "src", "Calculator");
        Directory.CreateDirectory(scriptDirectory);
        Directory.CreateDirectory(appProjectDirectory);
        var solutionPath = Path.Combine(productRoot, "Calculator.slnx");
        var appProjectPath = Path.Combine(appProjectDirectory, "Calculator.csproj");
        await File.WriteAllTextAsync(Path.Combine(scriptDirectory, "Add-SolutionMember.ps1"), "Write-Output 'ok'");
        await File.WriteAllTextAsync(solutionPath, string.Empty);
        await File.WriteAllTextAsync(appProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);
        var productAlias = ToExternalTargetAlias(productRoot);

        try
        {
            var result = await service.PowerShellRunScript(
                "scripts/Add-SolutionMember.ps1",
                arguments:
                [
                    $"{productAlias}/Calculator.slnx",
                    $"{productAlias}/src/Calculator/Calculator.csproj"
                ],
                workingDirectory: productAlias);

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Contains(solutionPath, processHost.LastRequest!.Arguments, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(appProjectPath, processHost.LastRequest.Arguments, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(processHost.LastRequest.Arguments, argument => argument.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
            TryDeleteDirectory(Path.GetDirectoryName(productRoot) ?? productRoot);
        }
    }

    [Fact]
    public async Task PowerShellRunScript_rewrites_named_managed_path_argument_before_process_execution()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var scriptDirectory = Path.Combine(workspaceRoot, "scripts");
        var inputDirectory = Path.Combine(workspaceRoot, "inputs");
        Directory.CreateDirectory(scriptDirectory);
        Directory.CreateDirectory(inputDirectory);
        await File.WriteAllTextAsync(Path.Combine(scriptDirectory, "Read-Input.ps1"), "Write-Output 'ok'");
        await File.WriteAllTextAsync(Path.Combine(inputDirectory, "request.json"), "{}");
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.PowerShellRunScript(
                "scripts/Read-Input.ps1",
                arguments:
                [
                    "--input=inputs/request.json",
                    "--mode=validate",
                    "https://example.test/status"
                ]);

            Assert.True(result.Succeeded, result.Message);
            Assert.NotNull(processHost.LastRequest);
            Assert.Contains(
                $"--input={Path.Combine(inputDirectory, "request.json")}",
                processHost.LastRequest!.Arguments,
                StringComparer.OrdinalIgnoreCase);
            Assert.Contains("--mode=validate", processHost.LastRequest.Arguments, StringComparer.Ordinal);
            Assert.Contains("https://example.test/status", processHost.LastRequest.Arguments, StringComparer.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task PowerShellRunScript_denies_native_external_argument_before_process_execution()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var externalRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.ScriptArgumentExternal.{Guid.NewGuid():N}");
        var scriptDirectory = Path.Combine(workspaceRoot, "scripts");
        Directory.CreateDirectory(scriptDirectory);
        Directory.CreateDirectory(externalRoot);
        await File.WriteAllTextAsync(Path.Combine(scriptDirectory, "Read-Input.ps1"), "Write-Output 'ok'");
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.PowerShellRunScript(
                "scripts/Read-Input.ps1",
                arguments: [Path.Combine(externalRoot, "secret.txt")]);

            Assert.False(result.Succeeded);
            Assert.Contains("outside the allowed workspace scope", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(processHost.LastRequest);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
            TryDeleteDirectory(externalRoot);
        }
    }

    [Fact]
    public async Task PythonRunFile_denies_named_parent_traversal_argument_before_process_execution()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var scriptDirectory = Path.Combine(workspaceRoot, "scripts");
        Directory.CreateDirectory(scriptDirectory);
        await File.WriteAllTextAsync(Path.Combine(scriptDirectory, "read_input.py"), "print('ok')");
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.PythonRunFile(
                "scripts/read_input.py",
                arguments: ["--input=../secret.txt"]);

            Assert.False(result.Succeeded);
            Assert.Contains("parent traversal", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(processHost.LastRequest);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task PowerShellRunScript_denies_external_target_dot_segment_argument_before_process_execution()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var scriptDirectory = Path.Combine(workspaceRoot, "scripts");
        Directory.CreateDirectory(scriptDirectory);
        await File.WriteAllTextAsync(Path.Combine(scriptDirectory, "Read-Input.ps1"), "Write-Output 'ok'");
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.PowerShellRunScript(
                "scripts/Read-Input.ps1",
                arguments: ["--input=external-target/C/repositories/allowed/../secret.txt"]);

            Assert.False(result.Succeeded);
            Assert.Contains("parent traversal", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(processHost.LastRequest);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task PowerShellRunScript_denies_colon_attached_parent_traversal_before_process_execution()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var scriptDirectory = Path.Combine(workspaceRoot, "scripts");
        Directory.CreateDirectory(scriptDirectory);
        await File.WriteAllTextAsync(Path.Combine(scriptDirectory, "Read-Input.ps1"), "Write-Output 'ok'");
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.PowerShellRunScript(
                "scripts/Read-Input.ps1",
                arguments: [@"-Path:..\secret.txt"]);

            Assert.False(result.Succeeded);
            Assert.Contains("parent traversal", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(processHost.LastRequest);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task PythonRunFile_denies_attached_short_native_external_argument_before_process_execution()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var externalRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.ScriptArgumentExternal.{Guid.NewGuid():N}");
        var scriptDirectory = Path.Combine(workspaceRoot, "scripts");
        Directory.CreateDirectory(scriptDirectory);
        Directory.CreateDirectory(externalRoot);
        await File.WriteAllTextAsync(Path.Combine(scriptDirectory, "read_input.py"), "print('ok')");
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.PythonRunFile(
                "scripts/read_input.py",
                arguments: [$"-i{Path.Combine(externalRoot, "secret.txt")}"]);

            Assert.False(result.Succeeded);
            Assert.Contains("outside the allowed workspace scope", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(processHost.LastRequest);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
            TryDeleteDirectory(externalRoot);
        }
    }

    [Fact]
    public async Task PowerShellRunScript_preserves_non_path_slash_literals()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var scriptDirectory = Path.Combine(workspaceRoot, "scripts");
        Directory.CreateDirectory(scriptDirectory);
        await File.WriteAllTextAsync(Path.Combine(scriptDirectory, "Validate.ps1"), "Write-Output 'ok'");
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);
        string[] arguments =
        [
            "--content-type=application/json",
            "--payload={\"route\":\"api/v1/items\"}",
            "--regex=^api/v[0-9]+$",
            "--route=api/v1/items",
            "-Endpoint:https://example.test/api/v1/items"
        ];

        try
        {
            var result = await service.PowerShellRunScript(
                "scripts/Validate.ps1",
                arguments: arguments);

            Assert.True(result.Succeeded, result.Message);
            Assert.NotNull(processHost.LastRequest);
            Assert.All(arguments, argument => Assert.Contains(argument, processHost.LastRequest!.Arguments));
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task PowerShellRunScript_preserves_external_working_directory_when_script_path_is_shortened()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var workspaceRoot = CreateDeepWorkspaceRoot();
        var productRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.ProductTarget.{Guid.NewGuid():N}",
            "product");
        var scriptDirectory = Path.Combine(workspaceRoot, "scripts");
        Directory.CreateDirectory(scriptDirectory);
        Directory.CreateDirectory(productRoot);
        await File.WriteAllTextAsync(
            Path.Combine(scriptDirectory, "Report-Location.ps1"),
            "Write-Output (Get-Location).Path");
        var service = TestWorkspaceServices.CreateCommandExecutionService(
            workspaceRoot,
            new LocalWorkspaceProcessHost());
        var productAlias = ToExternalTargetAlias(productRoot);

        try
        {
            var result = await service.PowerShellRunScript(
                "scripts/Report-Location.ps1",
                workingDirectory: productAlias);

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(
                productRoot.TrimEnd(Path.DirectorySeparatorChar),
                result.StdoutPreview.Trim().TrimEnd(Path.DirectorySeparatorChar),
                ignoreCase: true);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
            TryDeleteDirectory(Path.GetDirectoryName(productRoot) ?? productRoot);
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
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

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
    public async Task PowerShellRunScript_fails_post_execution_audit_when_nonmutating_step_changes_product_root()
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
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);
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

    [Theory]
    [InlineData(0, 1)]
    [InlineData(7, 7)]
    public async Task PowerShellRunScript_persists_process_evidence_when_post_execution_audit_is_inconclusive(
        int processExitCode,
        int expectedExitCode)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var productRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.ProductTarget.{Guid.NewGuid():N}",
            "product");
        var scriptDirectory = Path.Combine(workspaceRoot, "scripts");
        var productFile = Path.Combine(productRoot, "Program.cs");
        Directory.CreateDirectory(scriptDirectory);
        Directory.CreateDirectory(productRoot);
        await File.WriteAllTextAsync(
            Path.Combine(scriptDirectory, "Inspect.ps1"),
            "Write-Output 'inspected'");
        await File.WriteAllTextAsync(productFile, "Console.WriteLine(\"before\");");
        FileStream? lockedProductFile = null;
        var processHost = new FakeWorkspaceProcessHost(
            exitCode: processExitCode,
            stdout: "captured stdout",
            stderr: processExitCode == 0 ? string.Empty : "primary process failure",
            onExecute: _ => lockedProductFile = new FileStream(
                productFile,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None));
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);
        var productAlias = ToExternalTargetAlias(productRoot);
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
                    sideEffectManifest: JsonSerializer.Serialize(
                        new GovernedScriptSideEffectManifest
                        {
                            Mode = GovernedScriptSideEffectMode.NoMutation
                        }));

                Assert.False(result.Succeeded);
                Assert.Equal(expectedExitCode, result.ExitCode);
                Assert.Single(processHost.Requests);
                Assert.Equal("captured stdout", result.StdoutPreview);
                Assert.Equal(
                    processExitCode == 0 ? string.Empty : "primary process failure",
                    result.StderrPreview);
                Assert.Contains(
                    "could not complete the post-execution inspection",
                    result.Message,
                    StringComparison.OrdinalIgnoreCase);
                if (processExitCode != 0)
                {
                    Assert.Contains(
                        $"exit code {processExitCode}",
                        result.Message,
                        StringComparison.OrdinalIgnoreCase);
                }

                Assert.Equal("Failed", result.Receipt.Outcome);
                Assert.False(string.IsNullOrWhiteSpace(result.Receipt.ReceiptRelativePath));
            }
        }
        finally
        {
            lockedProductFile?.Dispose();
            TryDeleteDirectory(workspaceRoot);
            TryDeleteDirectory(Path.GetDirectoryName(productRoot) ?? productRoot);
        }
    }

    [Fact]
    public async Task PowerShellRunScript_does_not_launch_when_the_pre_execution_product_audit_is_inaccessible()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var productRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.ProductTarget.{Guid.NewGuid():N}",
            "product");
        var scriptDirectory = Path.Combine(workspaceRoot, "scripts");
        var productFile = Path.Combine(productRoot, "Program.cs");
        Directory.CreateDirectory(scriptDirectory);
        Directory.CreateDirectory(productRoot);
        await File.WriteAllTextAsync(
            Path.Combine(scriptDirectory, "Inspect.ps1"),
            "Write-Output 'inspected'");
        await File.WriteAllTextAsync(productFile, "Console.WriteLine(\"before\");");
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);
        var productAlias = ToExternalTargetAlias(productRoot);
        var run = CreateProcessStepExecutionRun(
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                [ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey] = new[] { productAlias },
                [ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey] = false
            }));

        try
        {
            await using var lockedProductFile = new FileStream(
                productFile,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                var result = await service.PowerShellRunScript(
                    "scripts/Inspect.ps1",
                    sideEffectManifest: JsonSerializer.Serialize(
                        new GovernedScriptSideEffectManifest
                        {
                            Mode = GovernedScriptSideEffectMode.NoMutation
                        }));

                Assert.False(result.Succeeded);
                Assert.Equal("Denied", result.Receipt.Outcome);
                Assert.Contains(productAlias, result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Empty(processHost.Requests);
            }
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
            TryDeleteDirectory(Path.GetDirectoryName(productRoot) ?? productRoot);
        }
    }

    [Fact]
    public async Task PowerShellRunScript_does_not_launch_when_the_pre_execution_product_audit_exceeds_its_byte_budget()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var productRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.ProductTarget.{Guid.NewGuid():N}",
            "product");
        var scriptDirectory = Path.Combine(workspaceRoot, "scripts");
        var productFile = Path.Combine(productRoot, "large.bin");
        Directory.CreateDirectory(scriptDirectory);
        Directory.CreateDirectory(productRoot);
        await File.WriteAllTextAsync(
            Path.Combine(scriptDirectory, "Inspect.ps1"),
            "Write-Output 'inspected'");
        await using (var stream = new FileStream(productFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.SetLength((100L * 1024 * 1024) + 1);
        }

        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);
        var productAlias = ToExternalTargetAlias(productRoot);
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
                    sideEffectManifest: JsonSerializer.Serialize(
                        new GovernedScriptSideEffectManifest
                        {
                            Mode = GovernedScriptSideEffectMode.NoMutation
                        }));

                Assert.False(result.Succeeded);
                Assert.Equal("Denied", result.Receipt.Outcome);
                Assert.Contains(productAlias, result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(
                    "bounded pre-execution mutation audit",
                    result.Message,
                    StringComparison.OrdinalIgnoreCase);
                Assert.Empty(processHost.Requests);
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
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

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
            Assert.Contains("$workspaceRoot = ", script, StringComparison.Ordinal);
            Assert.Contains("$env:ASPNETCORE_ENVIRONMENT = 'Development'", script, StringComparison.Ordinal);
            Assert.Contains("$env:DOTNET_ENVIRONMENT = 'Development'", script, StringComparison.Ordinal);
            Assert.Contains("aspNetCoreEnvironment = $env:ASPNETCORE_ENVIRONMENT", script, StringComparison.Ordinal);
            Assert.Contains("hostUrl = $probeUrl", script, StringComparison.Ordinal);
            Assert.Contains("workspaceRoot = $workspaceRoot", script, StringComparison.Ordinal);
            Assert.Contains("databaseProfileId = $env:CANDOITALL_DATABASE_PROFILE_ID", script, StringComparison.Ordinal);
            Assert.Contains("databaseProfileFingerprint = $env:CANDOITALL_DATABASE_PROFILE_FINGERPRINT", script, StringComparison.Ordinal);
            Assert.Contains("$cleanupReceipt = Join-Path (Split-Path -Parent $startupReceipt) 'cleanup.json'", script, StringComparison.Ordinal);
            Assert.Contains("cleanupReceiptPath = $cleanupReceipt", script, StringComparison.Ordinal);
            Assert.Contains("stopTool = 'workspace_dotnet_stop'", script, StringComparison.Ordinal);
            Assert.Contains("stopToolStartupReceiptPath = $startupReceipt", script, StringComparison.Ordinal);
            Assert.Contains("Resolve-StaticWebAssetsAliasMappings", script, StringComparison.Ordinal);
            Assert.Contains("Mount-StaticWebAssetsAliasMappings", script, StringComparison.Ordinal);
            Assert.Contains("Dismount-StaticWebAssetsAliasMappings", script, StringComparison.Ordinal);
            Assert.Contains("staticWebAssetsAliasMappings = @($staticWebAssetsAliasMappings)", script, StringComparison.Ordinal);
            Assert.Contains("if ($noBuild) { $staticWebAssetsAliasMappings = Mount-StaticWebAssetsAliasMappings -Mappings @(Resolve-StaticWebAssetsAliasMappings $projectPath $workspaceRoot $configuration) }", script, StringComparison.Ordinal);
            Assert.Contains("if ($null -eq $Mappings -or $Mappings.Count -eq 0) { return @() }", script, StringComparison.Ordinal);
            Assert.Contains("Stop-AppProcessTree $processTreeIds", script, StringComparison.Ordinal);
            Assert.Contains("Dismount-StaticWebAssetsAliasMappings $staticWebAssetsAliasMappings", script, StringComparison.Ordinal);
            Assert.Contains("Process tree was stopped after smoke validation", script, StringComparison.Ordinal);
            Assert.Contains("cleanupAttempted = $CleanupAttempted", script, StringComparison.Ordinal);
            Assert.Contains("cleanupProcessIds = @($CleanupProcessIds)", script, StringComparison.Ordinal);
            Assert.DoesNotContain("stopCommand", script, StringComparison.Ordinal);
            Assert.Contains("Write-StartupReceipt $true \"Application started and $probeUrl returned success. Process tree was stopped after smoke validation.\" ($processTreeIds.Count -gt 0) $processTreeIds", script, StringComparison.Ordinal);
            Assert.Contains("Write-StartupReceipt $false $message ($processTreeIds.Count -gt 0) $processTreeIds", script, StringComparison.Ordinal);
            Assert.DoesNotContain("workflow", script, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("converter", script, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetRun_http_smoke_with_port_zero_probes_actual_listening_url_from_log()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workspaceRoot, "apps", "SampleWeb");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "SampleWeb.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);
        var run = CreateProcessStepExecutionRun("{}");

        try
        {
            WorkspaceCommandExecutionResult result;
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                result = await service.DotnetRun(
                    "apps/SampleWeb/SampleWeb.csproj",
                    url: "http://127.0.0.1:0/health",
                    startupTimeoutSeconds: 5,
                    timeoutSeconds: 20,
                    keepAlive: true);
            }

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);

            var script = await ReadGeneratedDotnetRunScriptAsync(processHost);
            Assert.Contains("$listenUrl = 'http://127.0.0.1:0'", script, StringComparison.Ordinal);
            Assert.Contains("$probeUrl = 'http://127.0.0.1:0/health'", script, StringComparison.Ordinal);
            Assert.Contains("function Resolve-ListeningUrlFromLog", script, StringComparison.Ordinal);
            Assert.Contains("Now listening on:", script, StringComparison.Ordinal);
            Assert.Contains("Resolve-EffectiveProbeUrl $probeUrl $listenUrl $stdoutLog", script, StringComparison.Ordinal);
            Assert.Contains("Waiting for dotnet run to report a concrete listening URL.", script, StringComparison.Ordinal);
            Assert.Contains("$builder.Path = $requested.AbsolutePath", script, StringComparison.Ordinal);
            Assert.Contains("if ([string]::IsNullOrWhiteSpace($probeUrl) -and -not (Test-DynamicPortUrl $listenUrl))", script, StringComparison.Ordinal);
            Assert.DoesNotContain("Timed out after $startupTimeoutSeconds second(s) waiting for $probeUrl", script[..script.IndexOf("function Resolve-EffectiveProbeUrl", StringComparison.Ordinal)], StringComparison.Ordinal);
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
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);
        var run = CreateProcessStepExecutionRun("{}");

        try
        {
            WorkspaceCommandExecutionResult result;
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                result = await service.DotnetRun(
                    "apps/SampleWeb/SampleWeb.csproj",
                    url: "http://127.0.0.1:5125/",
                    startupTimeoutSeconds: 5,
                    timeoutSeconds: 20,
                    keepAlive: true);
            }

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            var script = await ReadGeneratedDotnetRunScriptAsync(processHost);
            Assert.Contains("$keepAlive = $true", script, StringComparison.Ordinal);
            Assert.Contains("The process tree is still running for follow-up browser proof", script, StringComparison.Ordinal);
            Assert.Contains("call workspace_dotnet_stop with startup.json when proof is complete", script, StringComparison.Ordinal);
            Assert.DoesNotContain("stopCommand", script, StringComparison.Ordinal);
            Assert.Contains("cleanupReceiptPath = $cleanupReceipt", script, StringComparison.Ordinal);
            Assert.Contains("stopTool = 'workspace_dotnet_stop'", script, StringComparison.Ordinal);
            Assert.Contains("stopToolStartupReceiptPath = $startupReceipt", script, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetStop_uses_startup_receipt_and_records_cleanup_targets()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var receiptDirectory = Path.Combine(workspaceRoot, "artifacts", "process-runs", "dotnet-run", "20260616-183000000");
        Directory.CreateDirectory(receiptDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(receiptDirectory, "startup.json"),
            """
            {
              "succeeded": true,
              "appProcessId": 12345,
              "appProcessTreeIds": [12346, 12345],
              "staticWebAssetsAliasMappings": [
                { "drive": "Q:", "mounted": true }
              ]
            }
            """);
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);
        var run = CreateProcessStepExecutionRun("{}");

        try
        {
            WorkspaceCommandExecutionResult result;
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                result = await service.DotnetStop(
                    "artifacts/process-runs/dotnet-run/20260616-183000000/startup.json",
                    timeoutSeconds: 10);
            }

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal("workspace_dotnet_stop", processHost.LastRequest!.ToolName);
            Assert.Equal("dotnet_stop", processHost.LastRequest.RecipeId);
            Assert.Equal(receiptDirectory, processHost.LastRequest.WorkingDirectory);
            Assert.Contains("-File", processHost.LastRequest.Arguments);
            Assert.DoesNotContain("-EncodedCommand", processHost.LastRequest.Arguments);
            Assert.Contains(
                result.Receipt.TargetPaths,
                item => string.Equals(item, "artifacts/process-runs/dotnet-run/20260616-183000000/startup.json", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                result.Receipt.TargetPaths,
                item => string.Equals(item, "artifacts/process-runs/dotnet-run/20260616-183000000/cleanup.json", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                result.Receipt.TargetPaths,
                item => string.Equals(item, "artifacts/process-runs/dotnet-run/20260616-183000000/stop.ps1", StringComparison.OrdinalIgnoreCase));

            var script = await ReadGeneratedPowerShellScriptAsync(processHost);
            Assert.Contains("Resolve-StartupProcessIds", script, StringComparison.Ordinal);
            Assert.Contains("Stop-AppProcessTree", script, StringComparison.Ordinal);
            Assert.Contains("Dismount-StaticWebAssetsAliasMappings", script, StringComparison.Ordinal);
            Assert.Contains("cleanupReceiptPath = $cleanupReceipt", script, StringComparison.Ordinal);
            Assert.Contains("cleanupSucceeded", script, StringComparison.Ordinal);
            Assert.Contains("if (-not $succeeded) { exit 1 }", script, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Theory]
    [InlineData("artifacts/process-runs/dotnet-run/20260624-193219617/startup.json")]
    [InlineData("artifacts/scopes/organization/e5df9ad633dbc6974a0678a74976013c/process-runs/dotnet-run/20260624-193219617/startup.json")]
    public async Task DotnetStop_in_organization_scope_accepts_scoped_and_unscoped_startup_receipts(string startupReceiptPath)
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var workspaceScope = WorkspaceScopeDescriptor.Organization("e5df9ad633dbc6974a0678a74976013c");
        var receiptRelativeDirectory = workspaceScope.CombineArtifactPath("process-runs", "dotnet-run", "20260624-193219617");
        var receiptDirectory = Path.Combine(workspaceRoot, receiptRelativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(receiptDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(receiptDirectory, "startup.json"),
            """
            {
              "succeeded": true,
              "appProcessId": 12345,
              "appProcessTreeIds": [12346, 12345]
            }
            """);
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost, workspaceScope);
        var run = CreateProcessStepExecutionRun("{}");

        try
        {
            WorkspaceCommandExecutionResult result;
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                result = await service.DotnetStop(startupReceiptPath, timeoutSeconds: 10);
            }

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal("workspace_dotnet_stop", processHost.LastRequest!.ToolName);
            Assert.Equal(receiptDirectory, processHost.LastRequest.WorkingDirectory);
            Assert.Contains(
                result.Receipt.TargetPaths,
                item => string.Equals(item, $"{receiptRelativeDirectory}/startup.json", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                result.Receipt.TargetPaths,
                item => string.Equals(item, $"{receiptRelativeDirectory}/cleanup.json", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                result.Receipt.TargetPaths,
                item => string.Equals(item, $"{receiptRelativeDirectory}/stop.ps1", StringComparison.OrdinalIgnoreCase));
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
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

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
            Assert.Contains("stopTool = 'workspace_dotnet_stop'", script, StringComparison.Ordinal);
            Assert.Contains("stopToolStartupReceiptPath = $startupReceipt", script, StringComparison.Ordinal);
            Assert.DoesNotContain("stopCommand", script, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetRun_kept_alive_execution_run_requires_active_audit_context()
    {
        var workspaceRoot = CreateWorkspaceWithWebProject();
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetRun(
                "apps/SampleWeb/SampleWeb.csproj",
                url: "http://127.0.0.1:5127/",
                keepAlive: true);

            Assert.False(result.Succeeded);
            Assert.Contains("active execution-run audit context", result.Message, StringComparison.Ordinal);
            Assert.Empty(processHost.Requests);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Workspace_process_identity_rejects_missing_or_ambiguous_startup_receipt_targets()
    {
        var workspaceRoot = CreateWorkspaceWithWebProject();
        var store = new WorkspaceExecutionRunProcessLeaseStore(
            workspaceRoot,
            WorkspaceScopeDescriptor.Sandbox);

        try
        {
            var missing = Assert.Throws<InvalidOperationException>(() =>
                store.ResolveSingleStartupReceiptPath(
                    ["artifacts/process-runs/dotnet-run/one/run.ps1"],
                    "Test launch"));
            var ambiguous = Assert.Throws<InvalidOperationException>(() =>
                store.ResolveSingleStartupReceiptPath(
                    [
                        "artifacts/process-runs/dotnet-run/one/startup.json",
                        "artifacts/process-runs/dotnet-run/two/startup.json"
                    ],
                    "Test launch"));

            Assert.Contains("did not prove", missing.Message, StringComparison.Ordinal);
            Assert.Contains("proved multiple", ambiguous.Message, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Workspace_process_identity_keeps_case_distinct_logical_receipts_distinct()
    {
        var workspaceRoot = CreateWorkspaceWithWebProject();
        var store = new WorkspaceExecutionRunProcessLeaseStore(
            workspaceRoot,
            WorkspaceScopeDescriptor.Sandbox);
        var executionRunId = Guid.NewGuid();
        const string upperPath = "artifacts/process-runs/Foo/startup.json";
        const string lowerPath = "artifacts/process-runs/foo/startup.json";

        try
        {
            Assert.NotEqual(
                store.GetLeaseFilePath(executionRunId, upperPath),
                store.GetLeaseFilePath(executionRunId, lowerPath));
            Assert.Throws<InvalidOperationException>(() =>
                store.ResolveSingleStartupReceiptPath(
                    [upperPath, lowerPath],
                    "Case-distinct test launch"));
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetRun_registers_normalized_durable_lease_for_cross_instance_cleanup()
    {
        var workspaceRoot = CreateWorkspaceWithWebProject();
        var run = CreateProcessStepExecutionRun("{}");
        var launchHost = new FakeWorkspaceProcessHost(onExecute: WriteStartupReceiptForRunRequest);
        var launchService = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, launchHost);

        try
        {
            WorkspaceCommandExecutionResult launchResult;
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                launchResult = await launchService.DotnetRun(
                    "apps/SampleWeb/SampleWeb.csproj",
                    url: "http://127.0.0.1:5128/",
                    keepAlive: true);
            }

            Assert.True(launchResult.Succeeded);
            var startupReceiptPath = Assert.Single(
                launchResult.Receipt.TargetPaths,
                path => path.EndsWith("/startup.json", StringComparison.OrdinalIgnoreCase));
            var store = new WorkspaceExecutionRunProcessLeaseStore(
                workspaceRoot,
                WorkspaceScopeDescriptor.Sandbox);
            store.Register(
                run.Id,
                Path.Combine(
                    workspaceRoot,
                    startupReceiptPath.Replace('/', Path.DirectorySeparatorChar)));
            var registered = store.Load(run.Id);
            var lease = Assert.Single(registered.Leases);
            Assert.Empty(registered.Failures);
            Assert.Equal(
                WorkspaceExecutionRunProcessLeasePhase.Active,
                lease.Phase);
            Assert.NotNull(lease.ActivatedAtUtc);
            Assert.Equal(
                WorkspaceScopeDescriptor.NormalizeRelativePath(startupReceiptPath),
                lease.StartupReceiptPath,
                ignoreCase: true);

            var cleanupHost = new FakeWorkspaceProcessHost();
            var cleanupService = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, cleanupHost);
            var cleanup = await GetCleanupExecutor(cleanupService).CleanupAsync(run.Id);

            Assert.Equal(run.Id, cleanup.ExecutionRunId);
            Assert.Equal([lease.StartupReceiptPath], cleanup.CleanedStartupReceiptPaths);
            Assert.Empty(cleanup.Failures);
            Assert.Single(cleanupHost.Requests);
            Assert.Equal("workspace_dotnet_stop", cleanupHost.Requests[0].ToolName);
            Assert.Empty(store.Load(run.Id).Leases);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetRun_concurrent_kept_alive_launches_use_collision_safe_startup_receipt_identities()
    {
        var workspaceRoot = CreateWorkspaceWithWebProject();
        var runs = new[]
        {
            CreateProcessStepExecutionRun("{}"),
            CreateProcessStepExecutionRun("{}")
        };
        var launchService = TestWorkspaceServices.CreateCommandExecutionService(
            workspaceRoot,
            new FakeWorkspaceProcessHost(onExecute: WriteStartupReceiptForRunRequest));

        try
        {
            var launchResults = await Task.WhenAll(runs.Select(async run =>
            {
                using var auditScope = WorkspaceExecutionAuditContext.BeginScope(run);
                return await launchService.DotnetRun(
                    "apps/SampleWeb/SampleWeb.csproj",
                    url: "http://127.0.0.1:0/",
                    keepAlive: true);
            }));

            Assert.All(launchResults, result => Assert.True(result.Succeeded, result.Message));
            var startupReceiptPaths = launchResults
                .Select(result => Assert.Single(
                    result.Receipt.TargetPaths,
                    path => path.EndsWith("/startup.json", StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            Assert.Equal(
                startupReceiptPaths.Length,
                startupReceiptPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(startupReceiptPaths, startupReceiptPath =>
            {
                var directoryName = Path.GetFileName(
                    Path.GetDirectoryName(startupReceiptPath.Replace('/', Path.DirectorySeparatorChar)))
                    ?? throw new InvalidOperationException("Startup receipt path must have a parent directory.");
                var identitySeparatorIndex = directoryName.LastIndexOf('-');

                Assert.True(identitySeparatorIndex > 0);
                Assert.True(Guid.TryParseExact(
                    directoryName[(identitySeparatorIndex + 1)..],
                    "N",
                    out _));
            });

            var leaseStore = new WorkspaceExecutionRunProcessLeaseStore(
                workspaceRoot,
                WorkspaceScopeDescriptor.Sandbox);
            Assert.All(runs, run => Assert.Single(leaseStore.Load(run.Id).Leases));
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetRun_registers_only_successful_kept_alive_execution_run_processes()
    {
        var workspaceRoot = CreateWorkspaceWithWebProject();
        var run = CreateProcessStepExecutionRun("{}");
        var store = new WorkspaceExecutionRunProcessLeaseStore(
            workspaceRoot,
            WorkspaceScopeDescriptor.Sandbox);

        try
        {
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                var nonKeptAlive = await TestWorkspaceServices.CreateCommandExecutionService(
                        workspaceRoot,
                        new FakeWorkspaceProcessHost())
                    .DotnetRun(
                        "apps/SampleWeb/SampleWeb.csproj",
                        url: "http://127.0.0.1:5129/",
                        keepAlive: false);
                var failed = await TestWorkspaceServices.CreateCommandExecutionService(
                        workspaceRoot,
                        new FakeWorkspaceProcessHost(exitCode: 1))
                    .DotnetRun(
                        "apps/SampleWeb/SampleWeb.csproj",
                        url: "http://127.0.0.1:5130/",
                        keepAlive: true);
                var processRunLifetime = await TestWorkspaceServices.CreateCommandExecutionService(
                        workspaceRoot,
                        new FakeWorkspaceProcessHost())
                    .DotnetRun(
                        "apps/SampleWeb/SampleWeb.csproj",
                        url: "http://127.0.0.1:5131/",
                        keepAlive: true,
                        lifetimeScope: WorkspaceProcessLifetimeScope.ProcessRun);

                Assert.True(nonKeptAlive.Succeeded);
                Assert.False(failed.Succeeded);
                Assert.True(processRunLifetime.Succeeded);
            }

            Assert.Empty(store.Load(run.Id).Leases);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetRun_pending_lease_registration_failure_prevents_launch()
    {
        var workspaceRoot = CreateWorkspaceWithWebProject();
        var run = CreateProcessStepExecutionRun("{}");
        var leaseDirectory = Path.Combine(
            WorkspaceExecutionAuditTrailWriter.GetRunAuditRoot(
                workspaceRoot,
                WorkspaceScopeDescriptor.Sandbox,
                run.Id),
            "process-leases");
        Directory.CreateDirectory(Path.GetDirectoryName(leaseDirectory)!);
        await File.WriteAllTextAsync(leaseDirectory, "blocks lease directory creation");
        var processHost = new FakeWorkspaceProcessHost(onExecute: WriteStartupReceiptForRunRequest);
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            InvalidOperationException exception;
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    service.DotnetRun(
                        "apps/SampleWeb/SampleWeb.csproj",
                        url: "http://127.0.0.1:5132/",
                        keepAlive: true));
            }

            Assert.Contains("pending ExecutionRun lease could not be persisted", exception.Message, StringComparison.Ordinal);
            Assert.NotNull(exception.InnerException);
            Assert.False(MafAgentToolFailureMapper.TryMap(exception, out _));
            Assert.Empty(processHost.Requests);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetRun_host_termination_retains_pending_lease_for_recovery()
    {
        var workspaceRoot = CreateWorkspaceWithWebProject();
        var run = CreateProcessStepExecutionRun("{}");
        var launchHost = new FakeWorkspaceProcessHost(onExecute: request =>
        {
            WriteStartupReceiptForRunRequest(request);
            throw new InvalidOperationException("synthetic host termination");
        });
        var launchService = TestWorkspaceServices.CreateCommandExecutionService(
            workspaceRoot,
            launchHost);
        var store = new WorkspaceExecutionRunProcessLeaseStore(
            workspaceRoot,
            WorkspaceScopeDescriptor.Sandbox);

        try
        {
            InvalidOperationException exception;
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    launchService.DotnetRun(
                        "apps/SampleWeb/SampleWeb.csproj",
                        url: "http://127.0.0.1:5132/",
                        startupTimeoutSeconds: 1,
                        keepAlive: true));
            }

            Assert.Contains("pending lease was retained", exception.Message, StringComparison.Ordinal);
            Assert.Equal("synthetic host termination", exception.InnerException?.Message);
            Assert.False(MafAgentToolFailureMapper.TryMap(exception, out _));
            var pendingLease = Assert.Single(store.Load(run.Id).Leases);
            Assert.Equal(
                WorkspaceExecutionRunProcessLeasePhase.Pending,
                pendingLease.Phase);
            Assert.Null(pendingLease.ActivatedAtUtc);
            Assert.True(
                pendingLease.StartupReceiptDeadlineUtc >= pendingLease.RegisteredAtUtc);

            var recoveryHost = new FakeWorkspaceProcessHost();
            var recoveryService = TestWorkspaceServices.CreateCommandExecutionService(
                workspaceRoot,
                recoveryHost);
            var recovery = await GetCleanupExecutor(recoveryService)
                .CleanupAsync(run.Id);

            Assert.Equal(
                [pendingLease.StartupReceiptPath],
                recovery.CleanedStartupReceiptPaths);
            Assert.Empty(recovery.Failures);
            Assert.Single(recoveryHost.Requests);
            Assert.Empty(store.Load(run.Id).Leases);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetStop_removes_only_the_active_execution_run_lease_using_result_identity()
    {
        var workspaceRoot = CreateWorkspaceWithWebProject();
        var workspaceScope = WorkspaceScopeDescriptor.Organization("lease-owner");
        var run = CreateProcessStepExecutionRun("{}");
        var canonicalStartupReceiptPath = workspaceScope.CombineArtifactPath(
            "process-runs",
            "dotnet-run",
            "owner-proof",
            "startup.json");
        var startupReceiptFullPath = Path.Combine(
            workspaceRoot,
            canonicalStartupReceiptPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(startupReceiptFullPath)!);
        await File.WriteAllTextAsync(startupReceiptFullPath, """{"succeeded":true,"appProcessTreeIds":[12345]}""");
        var store = new WorkspaceExecutionRunProcessLeaseStore(workspaceRoot, workspaceScope);
        store.Register(run.Id, canonicalStartupReceiptPath);
        var otherRunId = Guid.NewGuid();
        store.Register(otherRunId, canonicalStartupReceiptPath);
        var service = TestWorkspaceServices.CreateCommandExecutionService(
            workspaceRoot,
            new FakeWorkspaceProcessHost(),
            workspaceScope);

        try
        {
            WorkspaceCommandExecutionResult result;
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                result = await service.DotnetStop(
                    "artifacts/process-runs/dotnet-run/owner-proof/startup.json");
            }

            Assert.True(result.Succeeded);
            Assert.Empty(store.Load(run.Id).Leases);
            Assert.Single(store.Load(otherRunId).Leases);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task CleanupAsync_attempts_every_lease_and_retains_failed_lease()
    {
        var workspaceRoot = CreateWorkspaceWithWebProject();
        var run = CreateProcessStepExecutionRun("{}");
        var firstPath = "artifacts/process-runs/dotnet-run/first/startup.json";
        var secondPath = "artifacts/process-runs/dotnet-run/second/startup.json";
        await WriteStartupReceiptAsync(workspaceRoot, firstPath);
        await WriteStartupReceiptAsync(workspaceRoot, secondPath);
        var store = new WorkspaceExecutionRunProcessLeaseStore(
            workspaceRoot,
            WorkspaceScopeDescriptor.Sandbox);
        store.Register(run.Id, firstPath);
        store.Register(run.Id, secondPath);
        var processHost = new FakeWorkspaceProcessHost(onExecute: request =>
        {
            if (request.ToolName == "workspace_dotnet_stop" &&
                request.WorkingDirectory.EndsWith(
                    $"{Path.DirectorySeparatorChar}first",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("synthetic stop failure");
            }
        });
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await GetCleanupExecutor(service).CleanupAsync(run.Id);

            Assert.Equal([secondPath], result.CleanedStartupReceiptPaths);
            var failure = Assert.Single(result.Failures);
            Assert.Equal(firstPath, failure.StartupReceiptPath, ignoreCase: true);
            Assert.Equal(
                WorkspaceCommandFailureBoundary.CleanupAttemptFailureMessage,
                failure.Message);
            Assert.DoesNotContain("synthetic stop failure", failure.Message, StringComparison.Ordinal);
            Assert.Equal(2, processHost.Requests.Count);
            var retained = store.Load(run.Id);
            Assert.Equal(firstPath, Assert.Single(retained.Leases).StartupReceiptPath, ignoreCase: true);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task CleanupAsync_retains_pending_lease_when_startup_receipt_is_unavailable()
    {
        var workspaceRoot = CreateWorkspaceWithWebProject();
        var run = CreateProcessStepExecutionRun("{}");
        var startupReceiptPath = "artifacts/process-runs/dotnet-run/pending/startup.json";
        var store = new WorkspaceExecutionRunProcessLeaseStore(
            workspaceRoot,
            WorkspaceScopeDescriptor.Sandbox);
        var registeredAtUtc = DateTimeOffset.UtcNow.AddSeconds(-2);
        store.RegisterPending(
            run.Id,
            startupReceiptPath,
            registeredAtUtc,
            registeredAtUtc.AddSeconds(1));
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await GetCleanupExecutor(service).CleanupAsync(run.Id);

            Assert.Empty(result.CleanedStartupReceiptPaths);
            var failure = Assert.Single(result.Failures);
            Assert.Equal(startupReceiptPath, failure.StartupReceiptPath, ignoreCase: true);
            Assert.Contains("did not produce its startup receipt", failure.Message, StringComparison.Ordinal);
            Assert.Empty(processHost.Requests);
            var retainedLease = Assert.Single(store.Load(run.Id).Leases);
            Assert.Equal(
                WorkspaceExecutionRunProcessLeasePhase.Pending,
                retainedLease.Phase);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task CleanupAsync_coordinates_concurrent_service_instances_per_lease()
    {
        var workspaceRoot = CreateWorkspaceWithWebProject();
        var run = CreateProcessStepExecutionRun("{}");
        var startupReceiptPath = "artifacts/process-runs/dotnet-run/concurrent/startup.json";
        await WriteStartupReceiptAsync(workspaceRoot, startupReceiptPath);
        var store = new WorkspaceExecutionRunProcessLeaseStore(
            workspaceRoot,
            WorkspaceScopeDescriptor.Sandbox);
        store.Register(run.Id, startupReceiptPath);
        var stopEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executionCount = 0;

        async Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref executionCount);
            stopEntered.TrySetResult();
            await releaseStop.Task.WaitAsync(cancellationToken);
            return CreateSuccessfulProcessExecutionResult();
        }

        var firstService = TestWorkspaceServices.CreateCommandExecutionService(
            workspaceRoot,
            new FakeWorkspaceProcessHost(executeAsync: ExecuteAsync));
        var secondService = TestWorkspaceServices.CreateCommandExecutionService(
            workspaceRoot,
            new FakeWorkspaceProcessHost(executeAsync: ExecuteAsync));

        try
        {
            var firstCleanup = GetCleanupExecutor(firstService).CleanupAsync(run.Id);
            await stopEntered.Task;
            var secondCleanup = GetCleanupExecutor(secondService).CleanupAsync(run.Id);
            releaseStop.TrySetResult();
            var results = await Task.WhenAll(firstCleanup, secondCleanup);

            Assert.Equal(1, executionCount);
            Assert.All(
                results,
                result =>
                {
                    Assert.Equal([startupReceiptPath], result.CleanedStartupReceiptPaths);
                    Assert.Empty(result.Failures);
                });
            Assert.Empty(store.Load(run.Id).Leases);
        }
        finally
        {
            releaseStop.TrySetResult();
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task CleanupAsync_rejects_misnamed_or_mismatched_durable_lease_identity()
    {
        var workspaceRoot = CreateWorkspaceWithWebProject();
        var run = CreateProcessStepExecutionRun("{}");
        var startupReceiptPath = "artifacts/process-runs/dotnet-run/corrupt/startup.json";
        await WriteStartupReceiptAsync(workspaceRoot, startupReceiptPath);
        var store = new WorkspaceExecutionRunProcessLeaseStore(
            workspaceRoot,
            WorkspaceScopeDescriptor.Sandbox);
        store.Register(run.Id, startupReceiptPath);
        var leaseFile = store.GetLeaseFilePath(run.Id, startupReceiptPath);
        var misnamedLeaseFile = Path.Combine(Path.GetDirectoryName(leaseFile)!, "misnamed.json");
        File.Move(leaseFile, misnamedLeaseFile);
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await GetCleanupExecutor(service).CleanupAsync(run.Id);

            Assert.Empty(result.CleanedStartupReceiptPaths);
            var failure = Assert.Single(result.Failures);
            Assert.Equal(startupReceiptPath, failure.StartupReceiptPath, ignoreCase: true);
            Assert.Contains("filename does not match", failure.Message, StringComparison.Ordinal);
            Assert.Empty(processHost.Requests);
            Assert.True(File.Exists(misnamedLeaseFile));
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Theory]
    [InlineData(ExecutionState.Running)]
    [InlineData(ExecutionState.WaitingOnTool)]
    public async Task Authorized_cleanup_rejects_nonterminal_execution_run(
        ExecutionState executionState)
    {
        var workspaceRoot = CreateWorkspaceWithWebProject();
        var run = CreateProcessStepExecutionRun("{}") with
        {
            State = executionState
        };
        var startupReceiptPath = "artifacts/process-runs/dotnet-run/authorization/startup.json";
        await WriteStartupReceiptAsync(workspaceRoot, startupReceiptPath);
        var store = new WorkspaceExecutionRunProcessLeaseStore(
            workspaceRoot,
            WorkspaceScopeDescriptor.Sandbox);
        store.Register(run.Id, startupReceiptPath);
        var processHost = new FakeWorkspaceProcessHost();
        var cleaner = new WorkspaceExecutionRunProcessLeaseCleaner(
            new FakeExecutionRunStore(run),
            new WorkspaceExecutionScope(
                workspaceRoot,
                WorkspaceScopeDescriptor.Sandbox),
            new TestWorkspaceExecutionRunProcessLeaseCleanupScopeFactory(
                () => processHost));

        try
        {
            var result = await cleaner.CleanupAsync(run.Id);

            Assert.Empty(result.CleanedStartupReceiptPaths);
            var failure = Assert.Single(result.Failures);
            Assert.Contains("not a persisted terminal state", failure.Message, StringComparison.Ordinal);
            Assert.Empty(processHost.Requests);
            Assert.Single(store.Load(run.Id).Leases);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task Authorized_cleanup_rejects_nonexistent_execution_run()
    {
        var workspaceRoot = CreateWorkspaceWithWebProject();
        var executionRunId = Guid.NewGuid();
        var startupReceiptPath = "artifacts/process-runs/dotnet-run/nonexistent/startup.json";
        await WriteStartupReceiptAsync(workspaceRoot, startupReceiptPath);
        var store = new WorkspaceExecutionRunProcessLeaseStore(
            workspaceRoot,
            WorkspaceScopeDescriptor.Sandbox);
        store.Register(executionRunId, startupReceiptPath);
        var processHost = new FakeWorkspaceProcessHost();
        var cleaner = new WorkspaceExecutionRunProcessLeaseCleaner(
            new FakeExecutionRunStore(),
            new WorkspaceExecutionScope(
                workspaceRoot,
                WorkspaceScopeDescriptor.Sandbox),
            new TestWorkspaceExecutionRunProcessLeaseCleanupScopeFactory(
                () => processHost));

        try
        {
            var result = await cleaner.CleanupAsync(executionRunId);

            Assert.Empty(result.CleanedStartupReceiptPaths);
            var failure = Assert.Single(result.Failures);
            Assert.Contains("does not exist", failure.Message, StringComparison.Ordinal);
            Assert.Empty(processHost.Requests);
            Assert.Single(store.Load(executionRunId).Leases);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Theory]
    [InlineData(ExecutionState.Completed, WorkspaceScopeKind.Sandbox)]
    [InlineData(ExecutionState.Failed, WorkspaceScopeKind.Sandbox)]
    [InlineData(ExecutionState.Completed, WorkspaceScopeKind.Organization)]
    [InlineData(ExecutionState.Failed, WorkspaceScopeKind.Organization)]
    public async Task Authorized_cleanup_delegates_for_persisted_terminal_execution_run(
        ExecutionState executionState,
        WorkspaceScopeKind workspaceScopeKind)
    {
        var workspaceRoot = CreateWorkspaceWithWebProject();
        var workspaceScope = workspaceScopeKind == WorkspaceScopeKind.Organization
            ? WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("N"))
            : WorkspaceScopeDescriptor.Sandbox;
        var run = CreateProcessStepExecutionRun("{}") with
        {
            State = executionState
        };
        var startupReceiptPath = workspaceScope.CombineArtifactPath(
            "process-runs",
            "dotnet-run",
            "terminal",
            "startup.json");
        await WriteStartupReceiptAsync(workspaceRoot, startupReceiptPath);
        var store = new WorkspaceExecutionRunProcessLeaseStore(
            workspaceRoot,
            workspaceScope);
        store.Register(run.Id, startupReceiptPath);
        var processHost = new FakeWorkspaceProcessHost();
        var cleaner = new WorkspaceExecutionRunProcessLeaseCleaner(
            new FakeExecutionRunStore(run),
            new WorkspaceExecutionScope(
                workspaceRoot,
                workspaceScope),
            new TestWorkspaceExecutionRunProcessLeaseCleanupScopeFactory(
                () => processHost));

        try
        {
            var result = await cleaner.CleanupAsync(run.Id);

            Assert.Equal([startupReceiptPath], result.CleanedStartupReceiptPaths);
            Assert.Empty(result.Failures);
            Assert.Single(processHost.Requests);
            Assert.Empty(store.Load(run.Id).Leases);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void Workspace_command_contract_does_not_expose_authorized_process_cleanup()
    {
        var workspaceRoot = CreateWorkspaceWithWebProject();

        try
        {
            IWorkspaceCommandExecutionService commandService =
                TestWorkspaceServices.CreateCommandExecutionService(
                    workspaceRoot,
                    new FakeWorkspaceProcessHost());

            Assert.False(
                commandService is IWorkspaceExecutionRunProcessLeaseCleaner);
            Assert.IsAssignableFrom<IWorkspaceExecutionRunProcessLeaseCleanupExecutor>(
                commandService);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetRun_keeps_storage_scope_distinct_from_project_authorization_scope()
    {
        var workspaceRoot = CreateWorkspaceWithWebProject();
        var storageScope = WorkspaceScopeDescriptor.Organization("enterprise");
        var metadata = ExecutionInvocationMetadata.ApplyContextWorkspaceScope(
            "{}",
            WorkspaceScopeDescriptor.Project("calculator"));
        var run = CreateProcessStepExecutionRun(metadata);
        var service = TestWorkspaceServices.CreateCommandExecutionService(
            workspaceRoot,
            new FakeWorkspaceProcessHost(onExecute: WriteStartupReceiptForRunRequest),
            storageScope);

        try
        {
            WorkspaceCommandExecutionResult result;
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                result = await service.DotnetRun(
                    "apps/SampleWeb/SampleWeb.csproj",
                    url: "http://127.0.0.1:5133/",
                    keepAlive: true);
            }

            Assert.True(result.Succeeded);
            var store = new WorkspaceExecutionRunProcessLeaseStore(
                workspaceRoot,
                storageScope);
            Assert.Single(store.Load(run.Id).Leases);
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
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetTest("tests/SampleWeb.Tests");

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal("workspace_dotnet_test", processHost.LastRequest!.ToolName);
            Assert.Equal("test", processHost.LastRequest.Arguments[0]);
            Assert.Equal("tests/SampleWeb.Tests/SampleWeb.Tests.csproj".Replace('/', Path.DirectorySeparatorChar), processHost.LastRequest.Arguments[1]);
            Assert.Equal(300, processHost.LastRequest.TimeoutSeconds);
            Assert.Contains("tests/SampleWeb.Tests/SampleWeb.Tests.csproj", result.Receipt.TargetPaths, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetRestore_disables_build_servers_to_avoid_stale_msbuild_pipes()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workspaceRoot, "apps", "SampleWeb");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "SampleWeb.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetRestore("apps/SampleWeb");

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal("workspace_dotnet_restore", processHost.LastRequest!.ToolName);
            Assert.Equal("restore", processHost.LastRequest.Arguments[0]);
            Assert.Contains("--disable-build-servers", processHost.LastRequest.Arguments);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetBuild_disables_build_servers_to_avoid_stale_msbuild_pipes()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workspaceRoot, "apps", "SampleWeb");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "SampleWeb.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetBuild("apps/SampleWeb", noRestore: true);

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal("workspace_dotnet_build", processHost.LastRequest!.ToolName);
            Assert.Equal("build", processHost.LastRequest.Arguments[0]);
            Assert.Contains("--no-restore", processHost.LastRequest.Arguments);
            Assert.Contains("--disable-build-servers", processHost.LastRequest.Arguments);
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
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

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
    public async Task Process_scoped_command_receipts_use_current_run_artifact_namespace()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var workspaceScope = WorkspaceScopeDescriptor.Organization("org-001");
        var scriptDirectory = Path.Combine(workspaceRoot, "scripts");
        Directory.CreateDirectory(scriptDirectory);
        await File.WriteAllTextAsync(Path.Combine(scriptDirectory, "Fail.ps1"), "Write-Error 'failed'");
        var processHost = new FakeWorkspaceProcessHost(exitCode: 1, stdout: "command stdout", stderr: "command stderr");
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost, workspaceScope);
        var processRunId = Guid.NewGuid().ToString("D");
        var run = CreateProcessStepExecutionRun("{}", processRunId);

        try
        {
            using (WorkspaceExecutionAuditContext.BeginScope(run))
            {
                var result = await service.PowerShellRunScript("scripts/Fail.ps1");

                Assert.False(result.Succeeded);
                var stdoutRef = Assert.Single(
                    result.ArtifactReferences,
                    item => item.DisplayName.Contains("stdout", StringComparison.OrdinalIgnoreCase)).RelativePath;
                var normalizedStdoutRef = stdoutRef.Replace('\\', '/');
                Assert.Contains($"/process-runs/{processRunId}/tool-runs/", normalizedStdoutRef, StringComparison.OrdinalIgnoreCase);
                Assert.True(WorkspaceProcessRunArtifactPath.TryResolveRunId(stdoutRef, out var referencedRunId, out var artifactSuffix));
                Assert.Equal(processRunId, referencedRunId, ignoreCase: true);
                Assert.EndsWith("stdout.txt", artifactSuffix, StringComparison.OrdinalIgnoreCase);
                Assert.True(File.Exists(Path.Combine(workspaceRoot, stdoutRef.Replace('/', Path.DirectorySeparatorChar))));
            }
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
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

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
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, new FakeWorkspaceProcessHost());

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
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

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
    public async Task DotnetRun_foreground_request_honors_explicit_no_http_wait_for_blazor_webassembly_project()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workspaceRoot, "apps", "TetrisGame");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "TetrisGame.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\" />");
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

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
            Assert.DoesNotContain("-File", processHost.LastRequest.Arguments);
            Assert.DoesNotContain(result.Receipt.TargetPaths, item => item.EndsWith("startup.json", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(result.Receipt.TargetPaths, item => item.EndsWith("run.ps1", StringComparison.OrdinalIgnoreCase));
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
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

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
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

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
    public async Task DotnetNew_accepts_blazor_webassembly_pwa_template_option()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "apps"));
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetNew("blazorwasm --pwa", "TetrisGame", "apps");

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal("workspace_dotnet_new", processHost.LastRequest!.ToolName);
            Assert.Equal(["new", "blazorwasm", "--pwa", "-n", "TetrisGame"], processHost.LastRequest.Arguments);
            Assert.Equal(Path.Combine(workspaceRoot, "apps"), processHost.LastRequest.WorkingDirectory);
            Assert.Contains("apps/TetrisGame", result.Receipt.TargetPaths, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetNew_rejects_a_template_option_not_supported_by_the_selected_template()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "apps"));
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetNew("console --pwa", "ConsoleApp", "apps");

            Assert.False(result.Succeeded);
            Assert.Contains("requested template option is not approved", result.Message, StringComparison.Ordinal);
            Assert.Null(processHost.LastRequest);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetNew_accepts_target_framework_argument_with_a_valid_value()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "apps"));
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetNew(
                "blazorwasm",
                "FrameworkScopedApp",
                "apps",
                targetFramework: "net8.0");

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal(
                ["new", "blazorwasm", "--framework", "net8.0", "-n", "FrameworkScopedApp"],
                processHost.LastRequest!.Arguments);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetNew_rejects_target_framework_argument_without_a_valid_value()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "apps"));
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetNew(
                "blazorwasm",
                "FrameworkScopedApp",
                "apps",
                targetFramework: "invalid");

            Assert.False(result.Succeeded);
            Assert.Contains("targetFramework must be a supported value", result.Message, StringComparison.Ordinal);
            Assert.Null(processHost.LastRequest);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetNew_rejects_an_inline_target_framework_value()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "apps"));
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetNew("blazorwasm --framework net8.0", "FrameworkScopedApp", "apps");

            Assert.False(result.Succeeded);
            Assert.Contains("positional template argument is not approved", result.Message, StringComparison.Ordinal);
            Assert.Null(processHost.LastRequest);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public async Task DotnetNew_rejects_unapproved_inline_template_option()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "apps"));
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetNew("blazorwasm --install-source", "TetrisGame", "apps");

            Assert.False(result.Succeeded);
            Assert.Contains("requested template option is not approved", result.Message, StringComparison.Ordinal);
            Assert.Null(processHost.LastRequest);
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
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

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
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

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

    [Fact]
    public async Task DotnetNew_solution_template_allows_product_root_with_existing_solution_file()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var productRoot = Path.Combine(workspaceRoot, "calculator-output");
        Directory.CreateDirectory(productRoot);
        await File.WriteAllTextAsync(Path.Combine(productRoot, "Calculator.slnx"), string.Empty);
        var processHost = new FakeWorkspaceProcessHost();
        var service = TestWorkspaceServices.CreateCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetNew("sln", "Calculator", "calculator-output");

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.Equal("workspace_dotnet_new", processHost.LastRequest!.ToolName);
            Assert.Equal(["new", "sln", "-n", "Calculator"], processHost.LastRequest.Arguments);
            Assert.Equal(productRoot, processHost.LastRequest.WorkingDirectory);
        }
        finally
        {
            TryDeleteDirectory(workspaceRoot);
        }
    }

    private static string CreateWorkspaceWithWebProject()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.WorkspaceCommandExecutionServiceTests.{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(workspaceRoot, "apps", "SampleWeb");
        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(
            Path.Combine(projectDirectory, "SampleWeb.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        return workspaceRoot;
    }

    private static void WriteStartupReceiptForRunRequest(
        WorkspaceProcessExecutionRequest request)
    {
        if (request.ToolName != "workspace_dotnet_run")
        {
            return;
        }

        var fileIndex = request.Arguments.ToList().IndexOf("-File");
        if (fileIndex < 0 ||
            fileIndex + 1 >= request.Arguments.Count)
        {
            return;
        }

        var scriptDirectory = Path.GetDirectoryName(request.Arguments[fileIndex + 1])
            ?? throw new InvalidOperationException("Generated workspace_dotnet_run script has no directory.");
        File.WriteAllText(
            Path.Combine(scriptDirectory, "startup.json"),
            """{"succeeded":true,"appProcessTreeIds":[12345]}""");
    }

    private static async Task WriteStartupReceiptAsync(
        string workspaceRoot,
        string startupReceiptPath)
    {
        var fullPath = Path.Combine(
            workspaceRoot,
            startupReceiptPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(
            fullPath,
            """{"succeeded":true,"appProcessTreeIds":[12345]}""");
    }

    private static WorkspaceProcessExecutionResult CreateSuccessfulProcessExecutionResult()
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkspaceProcessExecutionResult(
            Started: true,
            ExitCode: 0,
            Stdout: "ok",
            Stderr: string.Empty,
            StdoutTruncated: false,
            StderrTruncated: false,
            StartedAtUtc: now,
            CompletedAtUtc: now,
            TimedOut: false,
            Boundary: new ExecutionBoundaryDescriptor(
                Mode: "Test",
                FilesystemScope: "Workspace",
                NetworkScope: "None",
                CredentialScope: "None",
                HostLabel: "Fake",
                IsEnforcedByHost: false,
                Notes: "Unit test host."),
            FailureMessage: string.Empty);
    }

    private static IWorkspaceExecutionRunProcessLeaseCleanupExecutor GetCleanupExecutor(
        WorkspaceCommandExecutionService service)
        => Assert.IsAssignableFrom<IWorkspaceExecutionRunProcessLeaseCleanupExecutor>(
            service);

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

    private static ExecutionRunRecord CreateProcessStepExecutionRun(string metadataJson, string? processRunId = null)
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
            ProcessRunId: processRunId ?? "process-run-001",
            ProcessStepId: "step-001");
    }

    private static async Task<string> ReadGeneratedDotnetRunScriptAsync(FakeWorkspaceProcessHost processHost)
        => await ReadGeneratedPowerShellScriptAsync(processHost);

    private static async Task<string> ReadGeneratedPowerShellScriptAsync(FakeWorkspaceProcessHost processHost)
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
        private readonly Func<WorkspaceProcessExecutionRequest, CancellationToken, Task<WorkspaceProcessExecutionResult>>? executeAsync;
        private readonly List<WorkspaceProcessExecutionRequest> requests = [];
        private readonly object synchronization = new();

        public FakeWorkspaceProcessHost(
            int exitCode = 0,
            string stdout = "ok",
            string stderr = "",
            Action<WorkspaceProcessExecutionRequest>? onExecute = null,
            Func<WorkspaceProcessExecutionRequest, CancellationToken, Task<WorkspaceProcessExecutionResult>>? executeAsync = null)
        {
            this.exitCode = exitCode;
            this.stdout = stdout;
            this.stderr = stderr;
            this.onExecute = onExecute;
            this.executeAsync = executeAsync;
        }

        public WorkspaceProcessExecutionRequest? LastRequest { get; private set; }

        public IReadOnlyList<WorkspaceProcessExecutionRequest> Requests
        {
            get
            {
                lock (synchronization)
                {
                    return requests.ToArray();
                }
            }
        }

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
            lock (synchronization)
            {
                LastRequest = request;
                requests.Add(request);
            }

            onExecute?.Invoke(request);
            if (executeAsync is not null)
            {
                return executeAsync(request, cancellationToken);
            }

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

    private sealed class FakeExecutionRunStore(
        params ExecutionRunRecord[] executionRuns)
        : ISandboxWorkspaceExecutionRunStore
    {
        private readonly Dictionary<Guid, ExecutionRunDetail> details =
            executionRuns.ToDictionary(
                run => run.Id,
                run => new ExecutionRunDetail(run, null, [], []));

        public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ExecutionRunRecord>>(
                details.Values
                    .Select(detail => detail.Run)
                    .ToArray());

        public Task<ExecutionRunRecord?> GetExecutionRunAsync(
            Guid executionRunId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                details.TryGetValue(executionRunId, out var detail)
                    ? detail.Run
                    : null);

        public Task<ExecutionRunDetail?> GetExecutionRunDetailAsync(
            Guid executionRunId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                details.GetValueOrDefault(executionRunId));

        public Task<ExecutionRunDetail> SaveExecutionRunDetailAsync(
            ExecutionRunDetail detail,
            CancellationToken cancellationToken = default)
        {
            details[detail.Run.Id] = detail;
            return Task.FromResult(detail);
        }
    }
}
