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
                    "artifacts/showcases/blazor-ssr-calculator/evidence/ui/qa-validation/calculator-proof.png",
                    "artifacts/showcases/blazor-ssr-calculator/evidence/ui/qa-validation/calculator-page.yml",
                    "artifacts/showcases/blazor-ssr-calculator/evidence/ui/qa-validation/calculator-console.log",
                    "artifacts/showcases/blazor-ssr-calculator/evidence/ui/qa-validation/import-summary.json"
                ]);

            Assert.True(result.Succeeded);
            Assert.True(result.Receipt.MutatesWorkspace);
            Assert.Contains(
                result.Receipt.TargetPaths,
                item => string.Equals(item, "artifacts/showcases/blazor-ssr-calculator/evidence/ui/qa-validation/calculator-proof.png", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                result.Receipt.ArtifactReferences,
                item => string.Equals(item.RelativePath, "artifacts/showcases/blazor-ssr-calculator/evidence/ui/qa-validation/import-summary.json", StringComparison.OrdinalIgnoreCase));
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
        var deliveryDirectory = Path.Combine(workspaceRoot, "deliveries", "blazor-ssr-basic-units-converter");
        Directory.CreateDirectory(deliveryDirectory);
        await File.WriteAllTextAsync(Path.Combine(deliveryDirectory, "BasicUnitsConverter.sln"), string.Empty);
        var processHost = new FakeWorkspaceProcessHost();
        var service = new WorkspaceCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.DotnetBuild("deliveries/blazor-ssr-basic-units-converter/BasicUnitsConverter.sln");

            Assert.True(result.Succeeded);
            Assert.NotNull(processHost.LastRequest);
            Assert.EndsWith(
                $"{Path.DirectorySeparatorChar}dotnet.exe",
                processHost.LastRequest!.ExecutablePath,
                StringComparison.OrdinalIgnoreCase);
            Assert.True(processHost.LastRequest.WorkingDirectory.Length < workspaceRoot.Length);
            Assert.Matches("^[A-Z]:\\\\$", processHost.LastRequest.WorkingDirectory);
            Assert.Equal("deliveries/blazor-ssr-basic-units-converter/BasicUnitsConverter.sln".Replace('/', Path.DirectorySeparatorChar), processHost.LastRequest.Arguments[1]);
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
        var scriptPath = Path.Combine(scriptDirectory, "Launch-UnitsConverterApp.ps1");
        await File.WriteAllTextAsync(scriptPath, "Write-Output 'ok'");
        var processHost = new FakeWorkspaceProcessHost();
        var service = new WorkspaceCommandExecutionService(workspaceRoot, processHost);

        try
        {
            var result = await service.PowerShellRunScript("scripts/Launch-UnitsConverterApp.ps1");

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
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new WorkspaceProcessExecutionResult(
                Started: true,
                ExitCode: 0,
                Stdout: "ok",
                Stderr: string.Empty,
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
