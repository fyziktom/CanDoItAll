using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Mcp.DotNetWatch;

namespace CanDoItAll.Mcp.DotNetWatch.IntegrationTests;

public sealed class BootstrapValidationTests : IAsyncLifetime
{
    public Task InitializeAsync() => ValidationHarness.StopBackendIfPresentAsync();

    public Task DisposeAsync() => ValidationHarness.StopBackendIfPresentAsync();

    [Fact]
    public async Task RepositoryMcpConfig_UsesWrapperLauncher()
    {
        var mcpConfigPath = Path.Combine(ValidationHarness.RepoRoot, ".vscode", "mcp.json");
        Assert.True(File.Exists(mcpConfigPath));

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(mcpConfigPath));
        var server = document.RootElement
            .GetProperty("servers")
            .GetProperty("candoitall_dotnetwatch");

        Assert.Equal("stdio", server.GetProperty("type").GetString());
        Assert.Equal("powershell", server.GetProperty("command").GetString(), ignoreCase: true);

        var args = server.GetProperty("args")
            .EnumerateArray()
            .Select(static value => value.GetString() ?? string.Empty)
            .ToArray();

        Assert.Contains("-File", args, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(
            @"${workspaceFolder}\tools\CanDoItAll.Mcp.DotNetWatch\Start-CanDoItAllDotNetWatchMcp.ps1",
            args,
            StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            args,
            static value => value.Contains(@".artifacts\mcp-server-shadow\bin\CanDoItAll.Mcp.DotNetWatch\debug\CanDoItAll.Mcp.DotNetWatch.dll", StringComparison.OrdinalIgnoreCase));
    }

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
                        ["timeoutMs"] = 300000
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

    [Fact]
    public async Task WrapperLaunch_CanServe_WorkspaceInfo_AndWriteBootstrapLog()
    {
        await using var harness = await ValidationHarness.CreateViaWrapperAsync();

        var workspace = await harness.CallToolAsync<ToolEnvelope<WorkspaceInfoData>>("candoitall_workspace_info");

        Assert.True(workspace.Ok, workspace.Error?.Message);
        Assert.NotNull(workspace.Data);
        Assert.Equal(ValidationHarness.RepoRoot, workspace.Data!.WorkspaceRoot.AbsolutePath);

        var bootstrapLogPath = Path.Combine(ValidationHarness.RepoRoot, ".mcp-state", "logs", "mcp-dotnetwatch-bootstrap.log");
        Assert.True(File.Exists(bootstrapLogPath));
        var bootstrapLog = await File.ReadAllTextAsync(bootstrapLogPath);
        Assert.Contains("wrapper start", bootstrapLog, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WrapperShadowCleanup_RetainsOnlyCurrentAndPreviousBuildRoots()
    {
        var tempDirectory = Path.Combine(ValidationHarness.RepoRoot, ".mcp-state", "wrapper-retention-tests", Guid.NewGuid().ToString("N"));
        var shadowArtifactsPath = Path.Combine(tempDirectory, "shadow");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            await RunWrapperAsync(shadowArtifactsPath, forceRebuild: true);
            await RunWrapperAsync(shadowArtifactsPath, forceRebuild: true);
            await RunWrapperAsync(shadowArtifactsPath, forceRebuild: true);
            await Task.Delay(TimeSpan.FromSeconds(10));
            await RunWrapperAsync(shadowArtifactsPath, forceRebuild: false);

            var buildsRoot = Path.Combine(shadowArtifactsPath, "builds");
            var buildDirectories = await WaitForRetainedBuildRootsAsync(buildsRoot, maximumCount: 2, timeout: TimeSpan.FromSeconds(10));

            Assert.True(File.Exists(Path.Combine(shadowArtifactsPath, "current.json")));
            Assert.True(File.Exists(Path.Combine(shadowArtifactsPath, "previous.json")));
            Assert.True(buildDirectories.Length <= 2, $"Expected wrapper cleanup to retain at most two successful build roots, but found {buildDirectories.Length}:{Environment.NewLine}{string.Join(Environment.NewLine, buildDirectories)}");
        }
        finally
        {
            await TryDeleteDirectoryAsync(tempDirectory);
        }
    }

    private static async Task RunWrapperAsync(string shadowArtifactsPath, bool forceRebuild)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("powershell")
            {
                WorkingDirectory = ValidationHarness.RepoRoot,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };

        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        process.StartInfo.ArgumentList.Add("Bypass");
        process.StartInfo.ArgumentList.Add("-File");
        process.StartInfo.ArgumentList.Add(ValidationHarness.WrapperScriptPath);
        process.StartInfo.ArgumentList.Add("-ShadowArtifactsPath");
        process.StartInfo.ArgumentList.Add(shadowArtifactsPath);
        if (forceRebuild)
        {
            process.StartInfo.ArgumentList.Add("-ForceRebuild");
        }

        Assert.True(process.Start());
        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(3));

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        Assert.Equal(0, process.ExitCode);
        Assert.True(File.Exists(Path.Combine(shadowArtifactsPath, "current.json")), $"Wrapper run did not produce a current shadow manifest. Stdout={stdout} Stderr={stderr}");
    }

    private static async Task TryDeleteDirectoryAsync(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        for (var attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (UnauthorizedAccessException) when (attempt < 5)
            {
                await Task.Delay(1000);
            }
            catch (IOException) when (attempt < 5)
            {
                await Task.Delay(1000);
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
            catch (IOException)
            {
                return;
            }
        }
    }

    private static async Task<string[]> WaitForRetainedBuildRootsAsync(string buildsRoot, int maximumCount, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        string[] buildDirectories = [];

        do
        {
            buildDirectories = Directory.Exists(buildsRoot)
                ? Directory.GetDirectories(buildsRoot, "*", SearchOption.TopDirectoryOnly)
                : [];

            if (buildDirectories.Length <= maximumCount)
            {
                return buildDirectories;
            }

            await Task.Delay(500);
        }
        while (DateTimeOffset.UtcNow < deadline);

        return buildDirectories;
    }
}
