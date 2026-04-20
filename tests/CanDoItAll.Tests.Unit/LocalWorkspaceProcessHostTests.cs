using System.Diagnostics;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Unit;

public sealed class LocalWorkspaceProcessHostTests
{
    [Fact]
    public async Task ExecuteAsync_returns_after_parent_exit_when_descendant_keeps_redirected_pipe_open()
    {
        var host = new LocalWorkspaceProcessHost();
        var childSleepSeconds = 30;
        var childPidFilePath = Path.Combine(Path.GetTempPath(), $"CanDoItAll.LocalWorkspaceProcessHostTests.{Guid.NewGuid():N}.pid");
        var command = string.Join(
            Environment.NewLine,
            [
                $"$childPidFilePath = '{childPidFilePath.Replace("'", "''")}'",
                "$childPwshPath = Join-Path $PSHOME 'pwsh.exe'",
                "if (-not (Test-Path -LiteralPath $childPwshPath)) {",
                "    $childPwshPath = 'pwsh'",
                "}",
                string.Empty,
                "$psi = [System.Diagnostics.ProcessStartInfo]::new()",
                "$psi.FileName = $childPwshPath",
                "$psi.UseShellExecute = $false",
                "$psi.RedirectStandardOutput = $false",
                "$psi.RedirectStandardError = $false",
                "$psi.CreateNoWindow = $true",
                "$null = $psi.ArgumentList.Add('-NoLogo')",
                "$null = $psi.ArgumentList.Add('-NoProfile')",
                "$null = $psi.ArgumentList.Add('-NonInteractive')",
                "$null = $psi.ArgumentList.Add('-Command')",
                $"$null = $psi.ArgumentList.Add('Write-Output ''child-start''; Start-Sleep -Seconds {childSleepSeconds}')",
                "$child = [System.Diagnostics.Process]::Start($psi)",
                "Set-Content -LiteralPath $childPidFilePath -Value $child.Id -NoNewline",
                "Write-Output 'parent-done'"
            ]);
        var request = new WorkspaceProcessExecutionRequest(
            ToolName: "workspace_pwsh_run_script",
            RecipeId: "pwsh_run_script",
            ExecutablePath: "pwsh",
            Arguments:
            [
                "-NoLogo",
                "-NoProfile",
                "-NonInteractive",
                "-Command",
                command
            ],
            WorkingDirectory: Path.GetTempPath(),
            EnvironmentVariables: new Dictionary<string, string?>(),
            TimeoutSeconds: 20,
            StdoutLimitCharacters: 4096,
            StderrLimitCharacters: 4096);

        Task<WorkspaceProcessExecutionResult>? executionTask = null;

        try
        {
            var stopwatch = Stopwatch.StartNew();
            executionTask = host.ExecuteAsync(request);
            var result = await executionTask.WaitAsync(TimeSpan.FromSeconds(6));
            stopwatch.Stop();

            Assert.True(result.Started);
            Assert.False(result.TimedOut);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("parent-done", result.Stdout, StringComparison.Ordinal);
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(6),
                $"Expected the host to return before the child released the inherited pipe. Elapsed: {stopwatch.Elapsed}.");
            Assert.True(
                result.StdoutTruncated || result.StderrTruncated,
                "Expected at least one redirected stream to be marked truncated after the bounded drain timeout.");
        }
        finally
        {
            TryKillProcessFromFile(childPidFilePath);

            if (executionTask is not null)
            {
                try
                {
                    await executionTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch
                {
                }
            }

            try
            {
                File.Delete(childPidFilePath);
            }
            catch
            {
            }
        }
    }

    private static void TryKillProcessFromFile(string childPidFilePath)
    {
        if (!File.Exists(childPidFilePath))
        {
            return;
        }

        try
        {
            var childPidText = File.ReadAllText(childPidFilePath).Trim();
            if (!int.TryParse(childPidText, out var childPid))
            {
                return;
            }

            var childProcess = Process.GetProcessById(childPid);
            childProcess.Kill(entireProcessTree: true);
            childProcess.WaitForExit(5000);
        }
        catch
        {
        }
    }
}
