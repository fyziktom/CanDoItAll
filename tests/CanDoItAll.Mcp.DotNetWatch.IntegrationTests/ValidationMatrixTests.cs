using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.Mcp.DotNetWatch;
using CanDoItAll.Mcp.DotNetWatch.Persistence;

namespace CanDoItAll.Mcp.DotNetWatch.IntegrationTests;

public sealed class ValidationMatrixTests
{
    private static string RepoRoot => ValidationHarness.RepoRoot;
    private static string WebProjectDirectory => Path.Combine(RepoRoot, "src", "CanDoItAll.Web");
    private static string DotNetWatchUnitTestProjectPath => Path.Combine(RepoRoot, "tests", "CanDoItAll.Mcp.DotNetWatch.Tests", "CanDoItAll.Mcp.DotNetWatch.Tests.csproj");
    private static string RegistryPath => Path.Combine(RepoRoot, ".mcp-state", "process-registry.json");

    [Fact]
    public async Task AppStart_ReusesCompatibleSession()
    {
        await using var harness = await ValidationHarness.CreateAsync();

        var first = await harness.CallToolAsync<ToolEnvelope<AppStartData>>("candoitall_app_start");
        Assert.True(first.Ok, first.Error?.Message);

        try
        {
            var second = await harness.CallToolAsync<ToolEnvelope<AppStartData>>("candoitall_app_start");

            Assert.True(second.Ok, second.Error?.Message);
            Assert.True(second.Data!.Reused);
            Assert.Equal(first.Data!.SessionId, second.Data.SessionId);
        }
        finally
        {
            await StopSessionAsync(harness, first.Data!.SessionId);
        }
    }

    [Fact]
    public async Task AppStart_ReturnsConflict_ForIncompatibleSession()
    {
        await using var harness = await ValidationHarness.CreateAsync();

        var first = await harness.CallToolAsync<ToolEnvelope<AppStartData>>("candoitall_app_start");
        Assert.True(first.Ok, first.Error?.Message);

        try
        {
            var second = await harness.CallToolAsync<ToolEnvelope<AppStartData>>(
                "candoitall_app_start",
                new Dictionary<string, object?>
                {
                    ["mode"] = nameof(AppRunMode.RunOnce),
                    ["conflictPolicy"] = nameof(AppStartConflictPolicy.Fail)
                });

            Assert.False(second.Ok);
            Assert.Equal("RunningSessionConflict", second.Error!.Code);
        }
        finally
        {
            await StopSessionAsync(harness, first.Data!.SessionId);
        }
    }

    [Fact]
    public async Task QuietWait_Completes_AfterWatchRestart()
    {
        await using var harness = await ValidationHarness.CreateAsync();
        var tempFilePath = Path.Combine(WebProjectDirectory, "McpQuietWaitFixture.cs");

        var start = await harness.CallToolAsync<ToolEnvelope<AppStartData>>("candoitall_app_start");
        Assert.True(start.Ok, start.Error?.Message);

        try
        {
            await WaitForHealthyAsync(harness, start.Data!.SessionId);

            var baseline = await harness.CallToolAsync<ToolEnvelope<AppStatusData>>(
                "candoitall_app_status",
                new Dictionary<string, object?> { ["sessionId"] = start.Data.SessionId });

            await File.WriteAllTextAsync(
                tempFilePath,
                """
                namespace CanDoItAll.Web;
                internal static class McpQuietWaitFixture
                {
                    public const string Value = "QuietWait";
                }
                """);

            var quiet = await harness.CallToolAsync<ToolEnvelope<AppWaitData>>(
                "candoitall_app_wait",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = start.Data.SessionId,
                    ["condition"] = nameof(AppWaitCondition.QuietSinceCursor),
                    ["cursor"] = baseline.Data!.LastCursor,
                    ["quietPeriodMs"] = 2000,
                    ["timeoutMs"] = 180000
                });

            Assert.True(quiet.Ok, quiet.Error?.Message);
            Assert.True(quiet.Data!.Satisfied, quiet.Data.DiagnosticHint);
            Assert.True(quiet.Data.FinalCursor > baseline.Data.LastCursor);

            await WaitForHealthyAsync(harness, start.Data.SessionId);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }

            await StopSessionAsync(harness, start.Data!.SessionId);
        }
    }

    [Fact]
    public async Task BuildFailure_PreservesResumeOutcome_AndDiagnostics()
    {
        await using var harness = await ValidationHarness.CreateAsync();
        var brokenFilePath = Path.Combine(RepoRoot, "tests", "CanDoItAll.Mcp.DotNetWatch.Tests", "BrokenBuildFixture.cs");

        var start = await harness.CallToolAsync<ToolEnvelope<AppStartData>>("candoitall_app_start");
        Assert.True(start.Ok, start.Error?.Message);

        try
        {
            await WaitForHealthyAsync(harness, start.Data!.SessionId);
            await File.WriteAllTextAsync(brokenFilePath, "namespace Broken; public sealed class BrokenBuildFixture { this is not valid C# }");

            var build = await harness.CallToolAsync<ToolEnvelope<OperationStartData>>(
                "candoitall_solution_build",
                new Dictionary<string, object?>
                {
                    ["targetPath"] = DotNetWatchUnitTestProjectPath,
                    ["timeoutMs"] = 300000
                });

            Assert.True(build.Ok, build.Error?.Message);

            var wait = await harness.CallToolAsync<ToolEnvelope<OperationWaitData>>(
                "candoitall_operation_wait",
                new Dictionary<string, object?>
                {
                    ["operationId"] = build.Data!.OperationId,
                    ["timeoutMs"] = 300000
                });

            Assert.True(wait.Ok, wait.Error?.Message);
            Assert.Equal(OperationState.Failed, wait.Data!.State);
            Assert.True(wait.Data.ResumeOutcome.Attempted);
            Assert.True(wait.Data.ResumeOutcome.Success);

            var logs = await harness.CallToolAsync<ToolEnvelope<OperationLogsData>>(
                "candoitall_operation_logs",
                new Dictionary<string, object?>
                {
                    ["operationId"] = build.Data.OperationId
                });

            Assert.Contains(logs.Data!.Entries, entry => entry.Text.Contains("error CS", StringComparison.OrdinalIgnoreCase));

            await WaitForHealthyAsync(harness, wait.Data.ResumeOutcome.SessionId!);
        }
        finally
        {
            if (File.Exists(brokenFilePath))
            {
                File.Delete(brokenFilePath);
            }

            await StopSessionAsync(harness, start.Data!.SessionId);
        }
    }

    [Fact]
    public async Task TestsRun_UsesDotnetTest_AndPublishesArtifacts()
    {
        await using var harness = await ValidationHarness.CreateAsync();

        var run = await harness.CallToolAsync<ToolEnvelope<OperationStartData>>(
            "candoitall_tests_run",
            new Dictionary<string, object?>
            {
                ["targetPath"] = DotNetWatchUnitTestProjectPath,
                ["collectCoverage"] = true,
                ["environmentOverlay"] = new Dictionary<string, object?> { ["DetailedErrors"] = "true" },
                ["timeoutMs"] = 300000
            });

        Assert.True(run.Ok, run.Error?.Message);
        Assert.Equal("VSTest", run.Data!.Runner);

        var wait = await harness.CallToolAsync<ToolEnvelope<OperationWaitData>>(
            "candoitall_operation_wait",
            new Dictionary<string, object?>
            {
                ["operationId"] = run.Data.OperationId,
                ["timeoutMs"] = 300000
            });

        Assert.True(wait.Ok, wait.Error?.Message);
        Assert.Equal(OperationState.Completed, wait.Data!.State);

        var status = await harness.CallToolAsync<ToolEnvelope<OperationStatusData>>(
            "candoitall_operation_status",
            new Dictionary<string, object?> { ["operationId"] = run.Data.OperationId });

        Assert.True(status.Ok, status.Error?.Message);
        Assert.NotEmpty(status.Data!.Artifacts);

        var logs = await harness.CallToolAsync<ToolEnvelope<OperationLogsData>>(
            "candoitall_operation_logs",
            new Dictionary<string, object?> { ["operationId"] = run.Data.OperationId });

        var startupLine = Assert.Single(logs.Data!.Entries, entry => entry.Source == "System" && entry.Text.Contains("Started process", StringComparison.Ordinal));
        Assert.Contains("dotnet test", startupLine.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("watch test", startupLine.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartupCleanup_Skips_UnownedProcess()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RegistryPath)!);
        var backupRegistryContents = File.Exists(RegistryPath) ? await File.ReadAllTextAsync(RegistryPath) : null;

        using var process = StartSleepingProcess();
        try
        {
            await WriteRegistryAsync(
            [
                new ManagedProcessRecord(
                    process.Id,
                    process.StartTime.ToUniversalTime(),
                    "powershell",
                    ["-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 300"],
                    RepoRoot,
                    RepoRoot,
                    "AppSession",
                    "unsafe_test",
                    "stale_server")
            ]);

            await using var harness = await ValidationHarness.CreateAsync();
            await Task.Delay(TimeSpan.FromSeconds(3));

            Assert.False(process.HasExited);

            var cleanup = await harness.CallToolAsync<ToolEnvelope<CleanupStaleProcessesData>>("candoitall_cleanup_stale_processes");
            Assert.True(cleanup.Ok, cleanup.Error?.Message);
            Assert.Equal(0, cleanup.Data!.Checked);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            await RestoreRegistryAsync(backupRegistryContents);
        }
    }

    [Fact]
    public async Task AppLogs_AndStatus_ShareCorrelationId()
    {
        await using var harness = await ValidationHarness.CreateAsync();

        var start = await harness.CallToolAsync<ToolEnvelope<AppStartData>>("candoitall_app_start");
        Assert.True(start.Ok, start.Error?.Message);

        try
        {
            await WaitForHealthyAsync(harness, start.Data!.SessionId);
            var status = await harness.CallToolAsync<ToolEnvelope<AppStatusData>>(
                "candoitall_app_status",
                new Dictionary<string, object?> { ["sessionId"] = start.Data.SessionId });
            var logs = await harness.CallToolAsync<ToolEnvelope<AppLogsData>>(
                "candoitall_app_logs",
                new Dictionary<string, object?> { ["sessionId"] = start.Data.SessionId });

            Assert.Equal(start.Data.CorrelationId, status.Data!.CorrelationId);
            Assert.Contains(logs.Data!.Entries, entry => string.Equals(entry.CorrelationId, status.Data.CorrelationId, StringComparison.Ordinal));
        }
        finally
        {
            await StopSessionAsync(harness, start.Data!.SessionId);
        }
    }

    [Fact]
    public async Task BusyWorkspace_ReturnsActionableError()
    {
        await using var harness = await ValidationHarness.CreateAsync();

        var build = await harness.CallToolAsync<ToolEnvelope<OperationStartData>>(
            "candoitall_solution_build",
            new Dictionary<string, object?>
            {
                ["timeoutMs"] = 300000
            });

        Assert.True(build.Ok, build.Error?.Message);

        try
        {
            var tests = await harness.CallToolAsync<ToolEnvelope<OperationStartData>>(
                "candoitall_tests_run",
                new Dictionary<string, object?>
                {
                    ["targetPath"] = DotNetWatchUnitTestProjectPath,
                    ["timeoutMs"] = 300000
                });

            Assert.False(tests.Ok);
            Assert.Equal("OperationInProgress", tests.Error!.Code);
            Assert.Contains("Build", tests.Error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await harness.CallToolAsync<ToolEnvelope<OperationWaitData>>(
                "candoitall_operation_wait",
                new Dictionary<string, object?>
                {
                    ["operationId"] = build.Data!.OperationId,
                    ["timeoutMs"] = 300000
                });
        }
    }

    private static async Task WaitForHealthyAsync(ValidationHarness harness, string sessionId)
    {
        var wait = await harness.CallToolAsync<ToolEnvelope<AppWaitData>>(
            "candoitall_app_wait",
            new Dictionary<string, object?>
            {
                ["sessionId"] = sessionId,
                ["condition"] = nameof(AppWaitCondition.Healthy),
                ["timeoutMs"] = 180000
            });

        Assert.True(wait.Ok, wait.Error?.Message);
        Assert.True(wait.Data!.Satisfied, wait.Data.DiagnosticHint);
    }

    private static async Task StopSessionAsync(ValidationHarness harness, string sessionId)
    {
        var stop = await harness.CallToolAsync<ToolEnvelope<AppStopData>>(
            "candoitall_app_stop",
            new Dictionary<string, object?>
            {
                ["sessionId"] = sessionId,
                ["force"] = true
            });

        Assert.True(stop.Ok, stop.Error?.Message);
    }

    private static Process StartSleepingProcess()
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo("powershell")
            {
                Arguments = "-NoProfile -NonInteractive -Command \"Start-Sleep -Seconds 300\"",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        Assert.True(process.Start());
        return process;
    }

    private static Task WriteRegistryAsync(IReadOnlyList<ManagedProcessRecord> records)
    {
        return File.WriteAllTextAsync(RegistryPath, JsonSerializer.Serialize(records, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        }));
    }

    private static async Task RestoreRegistryAsync(string? backupRegistryContents)
    {
        if (backupRegistryContents is null)
        {
            if (File.Exists(RegistryPath))
            {
                File.Delete(RegistryPath);
            }
        }
        else
        {
            await File.WriteAllTextAsync(RegistryPath, backupRegistryContents);
        }
    }
}
