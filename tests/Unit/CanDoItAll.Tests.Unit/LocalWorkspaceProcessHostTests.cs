using System.Diagnostics;
using System.Text;
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
        var command = OperatingSystem.IsWindows()
            ? BuildWindowsDetachedChildCommand(childPidFilePath, childSleepSeconds)
            : BuildPortableDetachedChildCommand(childPidFilePath, childSleepSeconds);
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
            var result = await executionTask.WaitAsync(TimeSpan.FromSeconds(12));
            stopwatch.Stop();

            Assert.True(result.Started);
            Assert.False(result.TimedOut);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("parent-done", result.Stdout, StringComparison.Ordinal);
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(12),
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

    private static string BuildWindowsDetachedChildCommand(string childPidFilePath, int childSleepSeconds)
    {
        var escapedChildPidFilePath = childPidFilePath.Replace("'", "''");
        var childCommand = $"Set-Content -LiteralPath '{escapedChildPidFilePath}' -Value $PID -NoNewline; Write-Output 'child-start'; Start-Sleep -Seconds {childSleepSeconds}";
        var encodedChildCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(childCommand));

        return string.Join(
            Environment.NewLine,
            [
                "$childPwshPath = Join-Path $PSHOME 'pwsh.exe'",
                "if (-not (Test-Path -LiteralPath $childPwshPath)) {",
                "    $childPwshPath = 'pwsh'",
                "}",
                "$cmdPath = $env:ComSpec",
                "if ([string]::IsNullOrWhiteSpace($cmdPath)) {",
                "    $cmdPath = 'cmd.exe'",
                "}",
                "$cmdStartInfo = [System.Diagnostics.ProcessStartInfo]::new()",
                "$cmdStartInfo.FileName = $cmdPath",
                "$cmdStartInfo.UseShellExecute = $false",
                "$cmdStartInfo.RedirectStandardOutput = $false",
                "$cmdStartInfo.RedirectStandardError = $false",
                "$cmdStartInfo.CreateNoWindow = $true",
                "$null = $cmdStartInfo.ArgumentList.Add('/c')",
                "$null = $cmdStartInfo.ArgumentList.Add('start')",
                "$null = $cmdStartInfo.ArgumentList.Add('\"\"')",
                "$null = $cmdStartInfo.ArgumentList.Add('/b')",
                "$null = $cmdStartInfo.ArgumentList.Add($childPwshPath)",
                "$null = $cmdStartInfo.ArgumentList.Add('-NoLogo')",
                "$null = $cmdStartInfo.ArgumentList.Add('-NoProfile')",
                "$null = $cmdStartInfo.ArgumentList.Add('-NonInteractive')",
                "$null = $cmdStartInfo.ArgumentList.Add('-EncodedCommand')",
                $"$null = $cmdStartInfo.ArgumentList.Add('{encodedChildCommand}')",
                "$cmdProcess = [System.Diagnostics.Process]::Start($cmdStartInfo)",
                "if ($cmdProcess -ne $null) {",
                "    $cmdProcess.WaitForExit(5000) | Out-Null",
                "}",
                "Write-Output 'parent-done'"
            ]);
    }

    private static string BuildPortableDetachedChildCommand(string childPidFilePath, int childSleepSeconds)
    {
        return string.Join(
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
    }
}
