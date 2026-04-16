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

    private sealed class FakeWorkspaceProcessHost : IWorkspaceProcessHost
    {
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
