using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Mcp.Core.Observability;
using CanDoItAll.Mcp.DotNetWatch;
using CanDoItAll.Mcp.DotNetWatch.Backend;
using CanDoItAll.Mcp.LocalRuntime.Persistence;

namespace CanDoItAll.Mcp.DotNetWatch.IntegrationTests;

public sealed class ValidationMatrixTests : IAsyncLifetime
{
    private static string RepoRoot => ValidationHarness.RepoRoot;
    private static string WebProjectDirectory => Path.Combine(RepoRoot, "src", "CanDoItAll.Web");
    private static string DotNetWatchUnitTestProjectPath => Path.Combine(RepoRoot, "tests", "CanDoItAll.Mcp.DotNetWatch.Tests", "CanDoItAll.Mcp.DotNetWatch.Tests.csproj");
    private static string RegistryPath => Path.Combine(RepoRoot, ".mcp-state", "process-registry.json");

    public Task InitializeAsync() => ValidationHarness.StopBackendIfPresentAsync();

    public Task DisposeAsync() => ValidationHarness.StopBackendIfPresentAsync();

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
    public async Task Backend_PersistsLiveSession_AcrossStdioServerReinstance()
    {
        using var firstProxy = await StartProxyProcessAsync();
        var firstRegistration = await WaitForBackendRegistrationAsync();
        Assert.NotNull(firstRegistration);
        Assert.True(IsProcessAlive(firstRegistration!.ProcessId), "The backend registration should point to a live process.");

        var start = await CallBackendToolAsync<AppStartData>(firstRegistration, "app-start", new { });
        Assert.True(start.Ok, start.Error?.Message);
        var sessionId = start.Data!.SessionId;

        var healthy = await CallBackendToolAsync<AppWaitData>(
            firstRegistration,
            "app-wait",
            new
            {
                sessionId,
                condition = nameof(AppWaitCondition.Healthy),
                timeoutMs = 180000
            });

        Assert.True(healthy.Ok, healthy.Error?.Message);
        Assert.True(healthy.Data!.Satisfied, healthy.Data.DiagnosticHint);

        firstProxy.Kill(entireProcessTree: true);
        firstProxy.WaitForExit();
        await Task.Delay(TimeSpan.FromSeconds(3));

        var registrationAfterKill = await WaitForBackendRegistrationAsync();
        Assert.NotNull(registrationAfterKill);
        Assert.Equal(firstRegistration.BackendId, registrationAfterKill!.BackendId);
        Assert.Equal(firstRegistration.ProcessId, registrationAfterKill.ProcessId);
        Assert.True(IsProcessAlive(firstRegistration.ProcessId), $"The backend process should survive proxy termination. BackendPid={firstRegistration.ProcessId}");

        using var secondProxy = await StartProxyProcessAsync();
        var secondRegistration = await WaitForBackendRegistrationAsync();
        Assert.NotNull(secondRegistration);
        Assert.Equal(firstRegistration.BackendId, secondRegistration!.BackendId);
        Assert.Equal(firstRegistration.ProcessId, secondRegistration.ProcessId);

        var workspace = await CallBackendToolAsync<WorkspaceInfoData>(
            secondRegistration,
            "workspace-info",
            new
            {
                includeHistory = true,
                includeConfigSnapshot = false
            });

        Assert.True(workspace.Ok, workspace.Error?.Message);
        Assert.Contains(workspace.Data!.ActiveAppSessions, session => string.Equals(session.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));

        var status = await CallBackendToolAsync<AppStatusData>(
            secondRegistration,
            "app-status",
            new
            {
                sessionId
            });

        Assert.True(status.Ok, status.Error?.Message);
        Assert.NotNull(status.Data);
        Assert.Equal(sessionId, status.Data!.SessionId);
        Assert.Contains(status.Data.State, new[] { AppLifecycleState.Healthy, AppLifecycleState.Starting, AppLifecycleState.Running, AppLifecycleState.Restarting });

        var stop = await CallBackendToolAsync<AppStopData>(
            secondRegistration,
            "app-stop",
            new
            {
                sessionId,
                force = true
            });

        Assert.True(stop.Ok, stop.Error?.Message);
        secondProxy.Kill(entireProcessTree: true);
        secondProxy.WaitForExit();
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
            Assert.NotNull(quiet.Data.Watch);
            Assert.False(quiet.Data.Watch!.PendingChange);

            await WaitForHealthyAsync(harness, start.Data.SessionId);

            await Task.Delay(TimeSpan.FromSeconds(3));
            var trailingLogs = await harness.CallToolAsync<ToolEnvelope<AppLogsData>>(
                "candoitall_app_logs",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = start.Data.SessionId,
                    ["cursor"] = quiet.Data.FinalCursor
                });

            Assert.True(trailingLogs.Ok, trailingLogs.Error?.Message);
            Assert.Empty(trailingLogs.Data!.Entries);
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
    [Trait("Category", "Quarantined")]
    public async Task HealthyWait_DoesNotReuse_StaleState_DuringRestartRequiredChange()
    {
        await using var harness = await ValidationHarness.CreateAsync();
        var tempFilePath = Path.Combine(WebProjectDirectory, "McpRestartRequiredFixture.cs");

        var start = await harness.CallToolAsync<ToolEnvelope<AppStartData>>("candoitall_app_start");
        Assert.True(start.Ok, start.Error?.Message);

        try
        {
            await WaitForHealthyAsync(harness, start.Data!.SessionId);
            var baseline = await harness.CallToolAsync<ToolEnvelope<AppStatusData>>(
                "candoitall_app_status",
                new Dictionary<string, object?> { ["sessionId"] = start.Data.SessionId });
            var baselineCursor = baseline.Data!.LastCursor;

            await File.WriteAllTextAsync(
                tempFilePath,
                """
                namespace CanDoItAll.Web;
                internal static class McpRestartRequiredFixture
                {
                    public const string Value = "RestartRequired";
                }
                """);

            await WaitForWatchSettledAsync(harness, start.Data.SessionId, baselineCursor);
            var afterAdd = await harness.CallToolAsync<ToolEnvelope<AppStatusData>>(
                "candoitall_app_status",
                new Dictionary<string, object?> { ["sessionId"] = start.Data.SessionId });

            Assert.True(afterAdd.Ok, afterAdd.Error?.Message);
            Assert.NotNull(afterAdd.Data);
            Assert.True(
                afterAdd.Data!.SessionVersion > baseline.Data.SessionVersion,
                $"Expected the add-file generation to advance beyond baseline. Baseline={baseline.Data.SessionVersion}, AfterAdd={afterAdd.Data.SessionVersion}.");
            File.Delete(tempFilePath);

            var restart = await harness.CallToolAsync<ToolEnvelope<AppWaitData>>(
                "candoitall_app_wait",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = start.Data.SessionId,
                    ["condition"] = nameof(AppWaitCondition.RestartCompleted),
                    ["timeoutMs"] = 180000
                });

            Assert.True(restart.Ok, restart.Error?.Message);
            Assert.True(restart.Data!.Satisfied, restart.Data.DiagnosticHint);

            var healthy = await harness.CallToolAsync<ToolEnvelope<AppWaitData>>(
                "candoitall_app_wait",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = start.Data.SessionId,
                    ["condition"] = nameof(AppWaitCondition.Healthy),
                    ["timeoutMs"] = 180000
                });

            Assert.True(healthy.Ok, healthy.Error?.Message);
            Assert.True(healthy.Data!.Satisfied, healthy.Data.DiagnosticHint);
            Assert.NotNull(healthy.Data.Watch);
            Assert.False(healthy.Data.Watch!.PendingChange);
            Assert.NotNull(healthy.Data.Watch.RuntimePid);

            var current = await harness.CallToolAsync<ToolEnvelope<AppStatusData>>(
                "candoitall_app_status",
                new Dictionary<string, object?> { ["sessionId"] = start.Data.SessionId });

            Assert.True(current.Ok, current.Error?.Message);
            Assert.NotNull(current.Data);
            Assert.False(current.Data!.Watch!.PendingChange);
            Assert.True(
                current.Data.SessionVersion > afterAdd.Data.SessionVersion,
                $"Healthy wait should only complete after the replacement runtime generation advances. Baseline={baseline.Data.SessionVersion}, AfterAdd={afterAdd.Data.SessionVersion}, Final={current.Data.SessionVersion}.");
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
    [Trait("Category", "Quarantined")]
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

            await WaitForHealthyAsync(harness, GetResumeSessionId(wait.Data.ResumeOutcome)!);
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
            Assert.Contains(tests.Error!.Code, new[] { "ResourceConflict", "OperationInProgress" });
            Assert.Contains("Build", tests.Error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await WaitForOperationCompletionAsync(harness, build.Data!.OperationId, TimeSpan.FromMinutes(5));
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

    private static async Task WaitForWatchSettledAsync(ValidationHarness harness, string sessionId, long? cursor = null)
    {
        var wait = await harness.CallToolAsync<ToolEnvelope<AppWaitData>>(
            "candoitall_app_wait",
            new Dictionary<string, object?>
            {
                ["sessionId"] = sessionId,
                ["condition"] = nameof(AppWaitCondition.WatchSettled),
                ["cursor"] = cursor,
                ["timeoutMs"] = 180000
            });

        Assert.True(wait.Ok, wait.Error?.Message);
        Assert.True(wait.Data!.Satisfied, wait.Data.DiagnosticHint);
        Assert.NotNull(wait.Data.Watch);
        Assert.False(wait.Data.Watch!.PendingChange);
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

    private static async Task WaitForOperationCompletionAsync(ValidationHarness harness, string operationId, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow <= deadline)
        {
            var status = await harness.CallToolAsync<ToolEnvelope<OperationStatusData>>(
                "candoitall_operation_status",
                new Dictionary<string, object?>
                {
                    ["operationId"] = operationId
                });

            if (!status.Ok && string.Equals(status.Error?.Code, "OperationNotFound", StringComparison.Ordinal))
            {
                return;
            }

            Assert.True(status.Ok, status.Error?.Message);
            if (status.Data!.State is OperationState.Completed or OperationState.Failed or OperationState.TimedOut or OperationState.Cancelled)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new TimeoutException($"Timed out waiting for operation '{operationId}' to complete.");
    }

    private static string? GetResumeSessionId(ResumeOutcomeData outcome)
    {
        return outcome.SessionIds.FirstOrDefault() ?? outcome.SessionId;
    }

    private static async Task<BackendRegistrationRecord?> ReadBackendRegistrationAsync()
    {
        if (!File.Exists(ValidationHarness.BackendRegistrationPath))
        {
            return null;
        }

        try
        {
            await using var stream = File.Open(ValidationHarness.BackendRegistrationPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return await JsonSerializer.DeserializeAsync<BackendRegistrationRecord>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<BackendRegistrationRecord?> WaitForBackendRegistrationAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow <= deadline)
        {
            var registration = await ReadBackendRegistrationAsync();
            if (registration is not null)
            {
                return registration;
            }

            await Task.Delay(250);
        }

        return null;
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<Process> StartProxyProcessAsync()
    {
        var shadowServerAssemblyPath = await ValidationHarness.GetCurrentShadowServerAssemblyPathAsync();
        var proxy = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = RepoRoot,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        proxy.StartInfo.ArgumentList.Add(shadowServerAssemblyPath);
        proxy.StartInfo.ArgumentList.Add("--settings");
        proxy.StartInfo.ArgumentList.Add(Path.Combine(RepoRoot, "CanDoItAll.Mcp.DotNetWatch.settings.json"));

        proxy.OutputDataReceived += static (_, _) => { };
        proxy.ErrorDataReceived += static (_, _) => { };

        Assert.True(proxy.Start());
        proxy.BeginOutputReadLine();
        proxy.BeginErrorReadLine();
        return proxy;
    }

    private static async Task<ToolEnvelope<T>> CallBackendToolAsync<T>(BackendRegistrationRecord registration, string route, object request)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(registration.BaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromMinutes(5)
        };

        client.DefaultRequestHeaders.Add("X-CanDoItAll-Backend-Token", registration.AuthToken);
        using var response = await client.PostAsJsonAsync($"/api/tools/{route}", request, BackendJsonOptions);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ToolEnvelope<T>>(BackendJsonOptions);
        Assert.NotNull(envelope);
        return envelope!;
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

    private static JsonSerializerOptions BackendJsonOptions { get; } = CreateBackendJsonOptions();

    private static JsonSerializerOptions CreateBackendJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
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
