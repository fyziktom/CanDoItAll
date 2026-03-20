using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Mcp.DotNetWatch;
using CanDoItAll.Mcp.DotNetWatch.Persistence;
using ModelContextProtocol.Client;

namespace CanDoItAll.Mcp.DotNetWatch.IntegrationTests;

public sealed class McpServerIntegrationTests
{
    private static string SharedKernelProjectPath => Path.Combine(McpServerHarness.RepoRoot, "src", "CanDoItAll.SharedKernel", "CanDoItAll.SharedKernel.csproj");

    private static string McpUnitTestProjectPath => Path.Combine(McpServerHarness.RepoRoot, "tests", "CanDoItAll.Mcp.DotNetWatch.Tests", "CanDoItAll.Mcp.DotNetWatch.Tests.csproj");

    private static string RegistryPath => Path.Combine(McpServerHarness.RepoRoot, ".mcp-state", "process-registry.json");

    [Fact]
    public async Task WorkspaceInfo_UsesCurrentRepositoryConfiguration()
    {
        await using var harness = await McpServerHarness.CreateAsync();

        var envelope = await harness.CallToolAsync<ToolEnvelope<WorkspaceInfoData>>("candoitall_workspace_info");

        Assert.True(envelope.Ok, envelope.Error?.Message);
        Assert.NotNull(envelope.Data);
        Assert.Equal(McpServerHarness.RepoRoot, envelope.Data!.WorkspaceRoot);
        Assert.Equal(Path.Combine(McpServerHarness.RepoRoot, "src", "CanDoItAll.Web", "CanDoItAll.Web.csproj"), envelope.Data.DefaultApp.ProjectPath);
        Assert.Contains(Path.Combine(McpServerHarness.RepoRoot, "tests", "CanDoItAll.Tests.Unit", "CanDoItAll.Tests.Unit.csproj"), envelope.Data.TestProjects);
    }

    [Fact]
    public async Task AppStart_WaitHealthy_Stop_WorksAgainstCurrentRepo()
    {
        await using var harness = await McpServerHarness.CreateAsync();

        var start = await harness.CallToolAsync<ToolEnvelope<AppStartData>>("candoitall_app_start");
        Assert.True(start.Ok, start.Error?.Message);
        Assert.NotNull(start.Data);

        var sessionId = start.Data!.SessionId;
        try
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
            Assert.NotNull(wait.Data);
            Assert.True(wait.Data!.Satisfied, wait.Data.DiagnosticHint);

            var status = await harness.CallToolAsync<ToolEnvelope<AppStatusData>>(
                "candoitall_app_status",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = sessionId
                });

            Assert.True(status.Ok, status.Error?.Message);
            Assert.NotNull(status.Data);
            Assert.Equal(AppLifecycleState.Healthy, status.Data!.State);
        }
        finally
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
    }

    [Fact]
    public async Task AppStart_RunOnce_WaitHealthy_Stop_WorksAgainstCurrentRepo()
    {
        await using var harness = await McpServerHarness.CreateAsync();

        var start = await harness.CallToolAsync<ToolEnvelope<AppStartData>>(
            "candoitall_app_start",
            new Dictionary<string, object?>
            {
                ["mode"] = nameof(AppRunMode.RunOnce)
            });
        Assert.True(start.Ok, start.Error?.Message);
        Assert.NotNull(start.Data);

        var sessionId = start.Data!.SessionId;
        try
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
            Assert.NotNull(wait.Data);
            Assert.True(wait.Data!.Satisfied, wait.Data.DiagnosticHint);

            var status = await harness.CallToolAsync<ToolEnvelope<AppStatusData>>(
                "candoitall_app_status",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = sessionId
                });

            Assert.True(status.Ok, status.Error?.Message);
            Assert.NotNull(status.Data);
            Assert.Equal(AppRunMode.RunOnce, status.Data!.Mode);
            Assert.Equal(AppLifecycleState.Healthy, status.Data.State);
        }
        finally
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
    }

    [Fact]
    public async Task AppLogs_ReturnsIncrementalEntries_ForCurrentSession()
    {
        await using var harness = await McpServerHarness.CreateAsync();

        var start = await harness.CallToolAsync<ToolEnvelope<AppStartData>>("candoitall_app_start");
        Assert.True(start.Ok, start.Error?.Message);
        Assert.NotNull(start.Data);

        var sessionId = start.Data!.SessionId;
        try
        {
            var healthy = await harness.CallToolAsync<ToolEnvelope<AppWaitData>>(
                "candoitall_app_wait",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = sessionId,
                    ["condition"] = nameof(AppWaitCondition.Healthy),
                    ["timeoutMs"] = 180000
                });

            Assert.True(healthy.Ok, healthy.Error?.Message);
            Assert.True(healthy.Data!.Satisfied, healthy.Data.DiagnosticHint);

            var logs = await harness.CallToolAsync<ToolEnvelope<AppLogsData>>(
                "candoitall_app_logs",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = sessionId,
                    ["cursor"] = start.Data.InitialCursor
                });

            Assert.True(logs.Ok, logs.Error?.Message);
            Assert.NotNull(logs.Data);
            Assert.NotEmpty(logs.Data!.Entries);
            Assert.True(logs.Data.NextCursor > start.Data.InitialCursor);
            Assert.Contains(logs.Data.Entries, entry => entry.Sequence > start.Data.InitialCursor);
        }
        finally
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
    }

    [Fact]
    public async Task SolutionBuild_StopAndResume_WorksAgainstCurrentRepo()
    {
        await using var harness = await McpServerHarness.CreateAsync();

        var start = await harness.CallToolAsync<ToolEnvelope<AppStartData>>("candoitall_app_start");
        Assert.True(start.Ok, start.Error?.Message);
        Assert.NotNull(start.Data);

        var originalSessionId = start.Data!.SessionId;
        string? resumedSessionId = null;

        try
        {
            var healthy = await harness.CallToolAsync<ToolEnvelope<AppWaitData>>(
                "candoitall_app_wait",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = originalSessionId,
                    ["condition"] = nameof(AppWaitCondition.Healthy),
                    ["timeoutMs"] = 180000
                });

            Assert.True(healthy.Ok, healthy.Error?.Message);
            Assert.True(healthy.Data!.Satisfied, healthy.Data.DiagnosticHint);

            var build = await harness.CallToolAsync<ToolEnvelope<OperationStartData>>(
                "candoitall_solution_build",
                new Dictionary<string, object?>
                {
                    ["targetPath"] = SharedKernelProjectPath,
                    ["timeoutMs"] = 300000
                });

            Assert.True(build.Ok, build.Error?.Message);
            Assert.NotNull(build.Data);

            var wait = await harness.CallToolAsync<ToolEnvelope<OperationWaitData>>(
                "candoitall_operation_wait",
                new Dictionary<string, object?>
                {
                    ["operationId"] = build.Data!.OperationId,
                    ["timeoutMs"] = 300000
                });

            Assert.True(wait.Ok, wait.Error?.Message);
            Assert.NotNull(wait.Data);
            Assert.True(wait.Data!.Completed);
            Assert.False(wait.Data.TimedOut);
            Assert.Equal(OperationState.Completed, wait.Data.State);

            resumedSessionId = wait.Data.ResumeOutcome.SessionId;
            Assert.True(wait.Data.ResumeOutcome.Attempted);
            Assert.True(wait.Data.ResumeOutcome.Success);
            Assert.False(string.IsNullOrWhiteSpace(resumedSessionId));

            var resumedHealthy = await harness.CallToolAsync<ToolEnvelope<AppWaitData>>(
                "candoitall_app_wait",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = resumedSessionId!,
                    ["condition"] = nameof(AppWaitCondition.Healthy),
                    ["timeoutMs"] = 180000
                });

            Assert.True(resumedHealthy.Ok, resumedHealthy.Error?.Message);
            Assert.True(resumedHealthy.Data!.Satisfied, resumedHealthy.Data.DiagnosticHint);
        }
        finally
        {
            var sessionToStop = resumedSessionId ?? originalSessionId;
            var stop = await harness.CallToolAsync<ToolEnvelope<AppStopData>>(
                "candoitall_app_stop",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = sessionToStop,
                    ["force"] = true
                });

            Assert.True(stop.Ok, stop.Error?.Message);
        }
    }

    [Fact]
    public async Task AppStatus_ReportsUnexpectedExit_WhenManagedProcessDies()
    {
        await using var harness = await McpServerHarness.CreateAsync();

        var start = await harness.CallToolAsync<ToolEnvelope<AppStartData>>(
            "candoitall_app_start",
            new Dictionary<string, object?>
            {
                ["mode"] = nameof(AppRunMode.RunOnce)
            });
        Assert.True(start.Ok, start.Error?.Message);
        Assert.NotNull(start.Data);

        var sessionId = start.Data!.SessionId;
        try
        {
            var healthy = await harness.CallToolAsync<ToolEnvelope<AppWaitData>>(
                "candoitall_app_wait",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = sessionId,
                    ["condition"] = nameof(AppWaitCondition.Healthy),
                    ["timeoutMs"] = 180000
                });

            Assert.True(healthy.Ok, healthy.Error?.Message);
            Assert.True(healthy.Data!.Satisfied, healthy.Data.DiagnosticHint);

            var status = await harness.CallToolAsync<ToolEnvelope<AppStatusData>>(
                "candoitall_app_status",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = sessionId
                });

            Assert.True(status.Ok, status.Error?.Message);
            Assert.NotNull(status.Data);
            Assert.NotNull(status.Data!.LastKnownPid);

            using (var process = Process.GetProcessById(status.Data.LastKnownPid.Value))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            var stopped = await harness.CallToolAsync<ToolEnvelope<AppWaitData>>(
                "candoitall_app_wait",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = sessionId,
                    ["condition"] = nameof(AppWaitCondition.Stopped),
                    ["timeoutMs"] = 30000
                });

            Assert.True(stopped.Ok, stopped.Error?.Message);
            Assert.True(stopped.Data!.Satisfied, stopped.Data.DiagnosticHint);
            Assert.Equal(AppLifecycleState.ExitedUnexpectedly, stopped.Data.ObservedState);

            var exitedStatus = await harness.CallToolAsync<ToolEnvelope<AppStatusData>>(
                "candoitall_app_status",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = sessionId
                });

            Assert.True(exitedStatus.Ok, exitedStatus.Error?.Message);
            Assert.Equal(AppLifecycleState.ExitedUnexpectedly, exitedStatus.Data!.State);
        }
        finally
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
    }

    [Fact]
    public async Task TestsRun_StopAndResume_WorksAgainstCurrentRepo()
    {
        await using var harness = await McpServerHarness.CreateAsync();

        var start = await harness.CallToolAsync<ToolEnvelope<AppStartData>>("candoitall_app_start");
        Assert.True(start.Ok, start.Error?.Message);
        Assert.NotNull(start.Data);

        var originalSessionId = start.Data!.SessionId;
        string? resumedSessionId = null;

        try
        {
            var healthy = await harness.CallToolAsync<ToolEnvelope<AppWaitData>>(
                "candoitall_app_wait",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = originalSessionId,
                    ["condition"] = nameof(AppWaitCondition.Healthy),
                    ["timeoutMs"] = 180000
                });

            Assert.True(healthy.Ok, healthy.Error?.Message);
            Assert.True(healthy.Data!.Satisfied, healthy.Data.DiagnosticHint);

            var run = await harness.CallToolAsync<ToolEnvelope<OperationStartData>>(
                "candoitall_tests_run",
                new Dictionary<string, object?>
                {
                    ["targetPath"] = McpUnitTestProjectPath,
                    ["timeoutMs"] = 300000
                });

            Assert.True(run.Ok, run.Error?.Message);
            Assert.NotNull(run.Data);

            var wait = await harness.CallToolAsync<ToolEnvelope<OperationWaitData>>(
                "candoitall_operation_wait",
                new Dictionary<string, object?>
                {
                    ["operationId"] = run.Data!.OperationId,
                    ["timeoutMs"] = 300000
                });

            Assert.True(wait.Ok, wait.Error?.Message);
            Assert.NotNull(wait.Data);
            Assert.True(wait.Data!.Completed);
            Assert.False(wait.Data.TimedOut);
            Assert.Equal(OperationState.Completed, wait.Data.State);
            Assert.True(wait.Data.ResumeOutcome.Attempted);
            Assert.True(wait.Data.ResumeOutcome.Success);

            resumedSessionId = wait.Data.ResumeOutcome.SessionId;
            Assert.False(string.IsNullOrWhiteSpace(resumedSessionId));

            var resumedHealthy = await harness.CallToolAsync<ToolEnvelope<AppWaitData>>(
                "candoitall_app_wait",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = resumedSessionId!,
                    ["condition"] = nameof(AppWaitCondition.Healthy),
                    ["timeoutMs"] = 180000
                });

            Assert.True(resumedHealthy.Ok, resumedHealthy.Error?.Message);
            Assert.True(resumedHealthy.Data!.Satisfied, resumedHealthy.Data.DiagnosticHint);

            var status = await harness.CallToolAsync<ToolEnvelope<OperationStatusData>>(
                "candoitall_operation_status",
                new Dictionary<string, object?>
                {
                    ["operationId"] = run.Data.OperationId
                });

            Assert.True(status.Ok, status.Error?.Message);
            Assert.NotNull(status.Data);
            Assert.NotNull(status.Data!.TestSummary);
            Assert.True(status.Data.TestSummary!.Passed > 0);
            Assert.Equal(0, status.Data.TestSummary.Failed);
        }
        finally
        {
            var sessionToStop = resumedSessionId ?? originalSessionId;
            var stop = await harness.CallToolAsync<ToolEnvelope<AppStopData>>(
                "candoitall_app_stop",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = sessionToStop,
                    ["force"] = true
                });

            Assert.True(stop.Ok, stop.Error?.Message);
        }
    }

    [Fact]
    public async Task AppStart_Rejects_PathOutsideWorkspace()
    {
        await using var harness = await McpServerHarness.CreateAsync();

        var outsidePath = Path.Combine(Path.GetTempPath(), $"outside-{Guid.NewGuid():N}.csproj");
        var result = await harness.CallToolAsync<ToolEnvelope<AppStartData>>(
            "candoitall_app_start",
            new Dictionary<string, object?>
            {
                ["projectPath"] = outsidePath
            });

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
        Assert.Equal("PathOutsideWorkspace", result.Error!.Code);
    }

    [Fact]
    public async Task StartupCleanup_Kills_RegisteredStaleProcess()
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
                    DateTimeOffset.UtcNow,
                    "powershell",
                    ["-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 300"],
                    McpServerHarness.RepoRoot,
                    McpServerHarness.RepoRoot,
                    "AppSession",
                    "stale_test",
                    "stale_server")
            ]);

            await using var harness = await McpServerHarness.CreateAsync();

            var cleanup = await harness.CallToolAsync<ToolEnvelope<CleanupStaleProcessesData>>(
                "candoitall_cleanup_stale_processes",
                new Dictionary<string, object?>
                {
                    ["dryRun"] = false
                });

            Assert.True(cleanup.Ok, cleanup.Error?.Message);
            Assert.NotNull(cleanup.Data);
            Assert.Equal(0, cleanup.Data!.Checked);
            Assert.Empty(cleanup.Data.Killed);

            Assert.True(process.WaitForExit(10000), "The stale managed process should be terminated during server startup cleanup.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

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

    private sealed class McpServerHarness : IAsyncDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        static McpServerHarness()
        {
            JsonOptions.Converters.Add(new JsonStringEnumConverter());
        }

        private readonly McpClient _client;

        private McpServerHarness(McpClient client)
        {
            _client = client;
        }

        public static string RepoRoot { get; } = ResolveRepoRoot();

        public static string ServerAssemblyPath { get; } = Path.Combine(
            RepoRoot,
            "src",
            "CanDoItAll.Mcp.DotNetWatch",
            "bin",
            "Debug",
            "net10.0",
            "CanDoItAll.Mcp.DotNetWatch.dll");

        public static async Task<McpServerHarness> CreateAsync()
        {
            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "CanDoItAll.Mcp.DotNetWatch.IntegrationTests",
                Command = "dotnet",
                Arguments =
                [
                    ServerAssemblyPath,
                    "--settings",
                    Path.Combine(RepoRoot, "CanDoItAll.Mcp.DotNetWatch.settings.json")
                ],
                WorkingDirectory = RepoRoot,
                ShutdownTimeout = TimeSpan.FromSeconds(15)
            });

            var client = await McpClient.CreateAsync(transport);
            return new McpServerHarness(client);
        }

        public async Task<T> CallToolAsync<T>(string toolName, IReadOnlyDictionary<string, object?>? arguments = null)
        {
            var result = await _client.CallToolAsync(toolName, arguments ?? new Dictionary<string, object?>());
            Assert.True(result.IsError is not true, Serialize(result));
            var payload = result.StructuredContent is null
                ? Serialize(result.Content)
                : Serialize(result.StructuredContent);
            var value = JsonSerializer.Deserialize<T>(payload, JsonOptions);
            Assert.NotNull(value);
            return value!;
        }

        public async ValueTask DisposeAsync()
        {
            if (_client is IAsyncDisposable asyncClient)
            {
                await asyncClient.DisposeAsync();
            }
        }

        private static string Serialize(object? value)
        {
            return JsonSerializer.Serialize(value, JsonOptions);
        }

        private static string ResolveRepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not locate the repo root from the test output directory.");
        }
    }
}
