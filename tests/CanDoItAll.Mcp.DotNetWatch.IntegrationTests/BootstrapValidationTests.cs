using System.Diagnostics;

namespace CanDoItAll.Mcp.DotNetWatch.IntegrationTests;

public sealed class BootstrapValidationTests
{
    [Fact]
    public async Task InvalidSolutionPath_FailsFast_WithActionableError()
    {
        var tempDirectory = Path.Combine(ValidationHarness.RepoRoot, ".mcp-state", "bootstrap-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var settingsPath = Path.Combine(tempDirectory, "invalid-settings.json");
        var stderrPath = Path.Combine(tempDirectory, "stderr.log");
        var stdoutPath = Path.Combine(tempDirectory, "stdout.log");

        await File.WriteAllTextAsync(
            settingsPath,
            """
            {
              "Server": {
                "Name": "CanDoItAll.Mcp.DotNetWatch",
                "WorkspaceRoot": ".",
                "SolutionPath": "missing.slnx"
              },
              "DefaultApp": {
                "ProjectPath": "src/CanDoItAll.Web/CanDoItAll.Web.csproj",
                "WorkingDirectory": "src/CanDoItAll.Web",
                "Mode": "WatchRun",
                "Configuration": "Debug",
                "Framework": null,
                "LaunchProfile": "https",
                "Arguments": [],
                "Urls": [ "https://localhost:7271", "http://localhost:5032" ],
                "EnvironmentOverlay": {
                  "ASPNETCORE_ENVIRONMENT": "Development",
                  "DOTNET_ENVIRONMENT": "Development"
                }
              },
              "Health": {
                "Enabled": true,
                "Urls": [ "https://localhost:7271/_dev/runtime", "http://localhost:5032/_dev/runtime" ],
                "TimeoutMs": 5000,
                "PollIntervalMs": 500,
                "StableSuccessCount": 1,
                "AcceptInsecureLocalhostHttps": true,
                "AllowedHosts": [ "localhost", "127.0.0.1", "::1" ]
              },
              "Build": {
                "DefaultTargetPath": "CanDoItAll.slnx",
                "DefaultWhenAppRunning": "StopAndResume",
                "DefaultTimeoutMs": 1800000,
                "ExtraArguments": []
              },
              "Tests": {
                "DefaultTargetPath": "tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj",
                "DefaultWhenAppRunning": "StopAndResume",
                "DefaultTimeoutMs": 1800000,
                "RunnerPreference": "Auto",
                "DefaultFilter": null,
                "Projects": [ "tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj" ]
              },
              "Logs": {
                "BufferCapacity": 5000,
                "PersistToFile": true,
                "Folder": ".mcp-state/logs",
                "MaxFileSizeMb": 50,
                "RedactionEnabled": true,
                "IncludeSystemEvents": true
              },
              "Process": {
                "GracefulStopTimeoutMs": 1000,
                "ForceKillAfterMs": 5000,
                "CleanupStaleManagedProcessesOnStartup": true,
                "RegistryPath": ".mcp-state/process-registry.json",
                "UsePollingFileWatcher": false
              },
              "Waits": {
                "DefaultAppWaitTimeoutMs": 120000,
                "DefaultOperationWaitTimeoutMs": 1800000,
                "DefaultPollIntervalMs": 500,
                "DefaultQuietPeriodMs": 2000
              },
              "Security": {
                "AllowedProjectRoots": [ "src", "tests", "tools" ],
                "AllowExternalHealthHosts": false,
                "AllowedEnvironmentKeys": [
                  "ASPNETCORE_ENVIRONMENT",
                  "ASPNETCORE_URLS",
                  "DOTNET_ENVIRONMENT",
                  "DOTNET_USE_POLLING_FILE_WATCHER",
                  "DetailedErrors"
                ]
              }
            }
            """);

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet")
                {
                    WorkingDirectory = ValidationHarness.RepoRoot,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                }
            };

            process.StartInfo.ArgumentList.Add(ValidationHarness.ServerAssemblyPath);
            process.StartInfo.ArgumentList.Add("--settings");
            process.StartInfo.ArgumentList.Add(settingsPath);

            Assert.True(process.Start());
            var stderrTask = process.StandardError.ReadToEndAsync();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var exited = process.WaitForExit(5000);
            if (!exited && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            var stderr = await stderrTask;
            var stdout = await stdoutTask;

            await File.WriteAllTextAsync(stderrPath, stderr);
            await File.WriteAllTextAsync(stdoutPath, stdout);

            Assert.True(exited || stderr.Contains("OptionsValidationException", StringComparison.OrdinalIgnoreCase), stderr);
            Assert.Contains("Solution path", stderr, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("missing.slnx", stderr, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("missing.slnx", stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Stdout_RemainsProtocolOnly_DuringHandshake_AndAppLifecycle()
    {
        var tempDirectory = Path.Combine(ValidationHarness.RepoRoot, ".mcp-state", "stdout-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var stdoutPath = Path.Combine(tempDirectory, "stdout.log");
        var stderrPath = Path.Combine(tempDirectory, "stderr.log");

        await using (var harness = await ValidationHarness.CreateCapturedAsync(stdoutPath, stderrPath))
        {
            var workspace = await harness.CallToolAsync<ToolEnvelope<WorkspaceInfoData>>("candoitall_workspace_info");
            Assert.True(workspace.Ok, workspace.Error?.Message);

            var start = await harness.CallToolAsync<ToolEnvelope<AppStartData>>("candoitall_app_start");
            Assert.True(start.Ok, start.Error?.Message);

            try
            {
                var wait = await harness.CallToolAsync<ToolEnvelope<AppWaitData>>(
                    "candoitall_app_wait",
                    new Dictionary<string, object?>
                    {
                        ["sessionId"] = start.Data!.SessionId,
                        ["condition"] = nameof(AppWaitCondition.Healthy),
                        ["timeoutMs"] = 180000
                    });

                Assert.True(wait.Ok, wait.Error?.Message);
                Assert.True(wait.Data!.Satisfied, wait.Data.DiagnosticHint);
            }
            finally
            {
                var stop = await harness.CallToolAsync<ToolEnvelope<AppStopData>>(
                    "candoitall_app_stop",
                    new Dictionary<string, object?>
                    {
                        ["sessionId"] = start.Data!.SessionId,
                        ["force"] = true
                    });

                Assert.True(stop.Ok, stop.Error?.Message);
            }
        }

        var stdout = await File.ReadAllTextAsync(stdoutPath);
        var stderr = await File.ReadAllTextAsync(stderrPath);

        Assert.NotEmpty(stdout);
        Assert.DoesNotContain("info:", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dotnet watch :", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Started process", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Server (stream)", stderr, StringComparison.OrdinalIgnoreCase);
    }
}
