using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Mcp.DotNetWatch.Backend;

namespace CanDoItAll.Mcp.DotNetWatch.IntegrationTests;

public sealed class BundleImprovementIntegrationTests : IAsyncLifetime
{
    private static string RepoRoot => ValidationHarness.RepoRoot;
    private static string WebProjectDirectory => Path.Combine(RepoRoot, "src", "CanDoItAll.Web");
    private static string DotNetWatchUnitTestProjectPath => Path.Combine(RepoRoot, "tests", "CanDoItAll.Mcp.DotNetWatch.Tests", "CanDoItAll.Mcp.DotNetWatch.Tests.csproj");

    public Task InitializeAsync() => ValidationHarness.StopBackendIfPresentAsync();

    public Task DisposeAsync() => ValidationHarness.StopBackendIfPresentAsync();

    [Fact]
    public async Task WorkspaceInfo_RepairsBridge_AfterBackendProcessDies()
    {
        await using var harness = await ValidationHarness.CreateAsync();

        var initial = await harness.CallToolAsync<ToolEnvelope<WorkspaceInfoData>>("candoitall_workspace_info");
        Assert.True(initial.Ok, initial.Error?.Message);

        var firstRegistration = await WaitForBackendRegistrationAsync();
        Assert.NotNull(firstRegistration);

        using (var process = Process.GetProcessById(firstRegistration!.ProcessId))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        var repaired = await harness.CallToolAsync<ToolEnvelope<WorkspaceInfoData>>("candoitall_workspace_info");

        Assert.True(repaired.Ok, repaired.Error?.Message);
        Assert.NotNull(repaired.Data);
        Assert.NotNull(repaired.Data!.Bridge);
        Assert.Equal("Repaired", repaired.Data.Bridge!.Health);

        var secondRegistration = await WaitForBackendRegistrationAsync();
        Assert.NotNull(secondRegistration);
        Assert.NotEqual(firstRegistration.ProcessId, secondRegistration!.ProcessId);
    }

    [Fact]
    public async Task BackendRoute_Deduplicates_NonIdempotent_AppStart_ByRequestId()
    {
        await using var harness = await ValidationHarness.CreateAsync();

        var workspace = await harness.CallToolAsync<ToolEnvelope<WorkspaceInfoData>>("candoitall_workspace_info");
        Assert.True(workspace.Ok, workspace.Error?.Message);

        var registration = await WaitForBackendRegistrationAsync();
        Assert.NotNull(registration);

        var request = new AppStartRequest(
            LogicalAppId: null,
            ProjectPath: null,
            Mode: null,
            LaunchType: AppLaunchType.Project,
            PreferredLane: null,
            EntryPath: null,
            ConfigurationName: null,
            Framework: null,
            LaunchProfile: null,
            WorkingDirectory: null,
            Arguments: null,
            EnvironmentOverlay: null,
            Urls: null,
            ReuseIfCompatible: true,
            ConflictPolicy: AppStartConflictPolicy.Fail,
            WaitFor: AppWaitCondition.None);

        var first = await CallBackendToolAsync<AppStartData>(registration!, "app-start", request, requestId: "req_bundle_start");
        var second = await CallBackendToolAsync<AppStartData>(registration, "app-start", request, requestId: "req_bundle_start");

        Assert.True(first.Ok, first.Error?.Message);
        Assert.True(second.Ok, second.Error?.Message);
        Assert.Equal(first.Data!.SessionId, second.Data!.SessionId);
        Assert.Equal(first.CorrelationId, second.CorrelationId);

        await StopSessionAsync(harness, first.Data.SessionId);
    }

    [Fact]
    public async Task HealthyWatchStatus_EmitsCompactGuidance_WhileLogsAndEventsRemainClean()
    {
        await using var harness = await ValidationHarness.CreateAsync();

        var start = await harness.CallToolAsync<ToolEnvelope<AppStartData>>(
            "candoitall_app_start",
            new Dictionary<string, object?>
            {
                ["mode"] = nameof(AppRunMode.WatchRun)
            });
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
            var waitGuidance = ReadGuidance(wait);
            Assert.NotNull(waitGuidance);
            Assert.True(JsonSerializer.Serialize(waitGuidance, JsonOptions).Length <= 180);

            var status = await harness.CallToolAsync<ToolEnvelope<AppStatusData>>(
                "candoitall_app_status",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = start.Data.SessionId
                });

            Assert.True(status.Ok, status.Error?.Message);
            var statusGuidance = ReadGuidance(status);
            Assert.NotNull(statusGuidance);
            Assert.Contains(statusGuidance!.Mode, new[] { "watch-small-step", "watch-validate-now" });

            var logs = await harness.CallToolAsync<ToolEnvelope<AppLogsData>>(
                "candoitall_app_logs",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = start.Data.SessionId
                });

            Assert.True(logs.Ok, logs.Error?.Message);
            Assert.Null(logs.WorkflowGuidance);

            var events = await harness.CallToolAsync<ToolEnvelope<AppEventsData>>(
                "candoitall_app_events",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = start.Data.SessionId
                });

            Assert.True(events.Ok, events.Error?.Message);
            Assert.Null(events.WorkflowGuidance);
        }
        finally
        {
            await StopSessionAsync(harness, start.Data!.SessionId);
        }
    }

    [Fact]
    [Trait("Category", "Quarantined")]
    public async Task FailedOperation_EmitsFocusedGuidance_WhileOperationLogsRemainGuidanceFree()
    {
        await using var harness = await ValidationHarness.CreateAsync();
        var brokenFilePath = Path.Combine(RepoRoot, "tests", "CanDoItAll.Mcp.DotNetWatch.Tests", "BundleBrokenBuildFixture.cs");

        try
        {
            await File.WriteAllTextAsync(brokenFilePath, "namespace Broken; public sealed class BundleBrokenBuildFixture { this is not valid C# }");

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

            var status = await harness.CallToolAsync<ToolEnvelope<OperationStatusData>>(
                "candoitall_operation_status",
                new Dictionary<string, object?>
                {
                    ["operationId"] = build.Data.OperationId
                });

            Assert.True(status.Ok, status.Error?.Message);
            var statusGuidance = ReadGuidance(status);
            Assert.NotNull(statusGuidance);
            Assert.Equal("fix-current-failure", statusGuidance!.Mode);

            var logs = await harness.CallToolAsync<ToolEnvelope<OperationLogsData>>(
                "candoitall_operation_logs",
                new Dictionary<string, object?>
                {
                    ["operationId"] = build.Data.OperationId
                });

            Assert.True(logs.Ok, logs.Error?.Message);
            Assert.Null(logs.WorkflowGuidance);
            Assert.Contains(logs.Data!.Entries, entry => entry.Text.Contains("error CS", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (File.Exists(brokenFilePath))
            {
                File.Delete(brokenFilePath);
            }
        }
    }

    [Fact]
    public async Task AtomicUpdate_Commits_EmitsEvents_AndRollbackRestoresPreviousRuntime()
    {
        await using var harness = await ValidationHarness.CreateAsync();

        var start = await harness.CallToolAsync<ToolEnvelope<AppStartData>>(
            "candoitall_app_start",
            new Dictionary<string, object?>
            {
                ["mode"] = nameof(AppRunMode.RunOnce)
            });
        Assert.True(start.Ok, start.Error?.Message);

        try
        {
            await WaitForHealthyAsync(harness, start.Data!.SessionId);
            var baseline = await harness.CallToolAsync<ToolEnvelope<AppStatusData>>(
                "candoitall_app_status",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = start.Data.SessionId
                });

            Assert.True(baseline.Ok, baseline.Error?.Message);
            Assert.NotNull(baseline.Data);

            var atomic = await harness.CallToolAsync<ToolEnvelope<AtomicUpdateData>>(
                "candoitall_app_update_atomic",
                new Dictionary<string, object?>
                {
                    ["logicalAppId"] = start.Data.LogicalAppId,
                    ["configurationName"] = "Debug",
                    ["timeoutMs"] = 300000,
                    ["keepPreviousRuntimeWarm"] = true,
                    ["allowRollback"] = true,
                    ["activateOnSuccess"] = true
                });

            Assert.True(atomic.Ok, atomic.Error?.Message);
            Assert.NotNull(atomic.Data);
            Assert.True(atomic.Data!.Committed);
            Assert.True(atomic.Data.RollbackAvailable);

            var originalStatus = await harness.CallToolAsync<ToolEnvelope<AppStatusData>>(
                "candoitall_app_status",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = start.Data.SessionId
                });

            Assert.True(originalStatus.Ok, originalStatus.Error?.Message);
            Assert.Contains(originalStatus.Data!.State, new[] { AppLifecycleState.Healthy, AppLifecycleState.Running, AppLifecycleState.Restarting });

            var candidateStatus = await harness.CallToolAsync<ToolEnvelope<AppStatusData>>(
                "candoitall_app_status",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = atomic.Data.CandidateSessionId
                });

            Assert.True(candidateStatus.Ok, candidateStatus.Error?.Message);
            Assert.Equal(RuntimeLaneKind.PublishedActive, candidateStatus.Data!.LaneKind);
            Assert.True(candidateStatus.Data.RollbackAvailable);
            Assert.Equal("PublishedBundle", candidateStatus.Data.Revision!.Kind);

            var events = await harness.CallToolAsync<ToolEnvelope<AppEventsData>>(
                "candoitall_app_events",
                new Dictionary<string, object?>
                {
                    ["logicalAppId"] = atomic.Data.LogicalAppId
                });

            Assert.True(events.Ok, events.Error?.Message);
            Assert.Null(events.WorkflowGuidance);
            Assert.Contains(events.Data!.Entries, entry => string.Equals(entry.EventType, "candidate-prepared", StringComparison.Ordinal));
            Assert.Contains(events.Data.Entries, entry => string.Equals(entry.EventType, "candidate-healthy", StringComparison.Ordinal));
            Assert.Contains(events.Data.Entries, entry => string.Equals(entry.EventType, "transaction-committed", StringComparison.Ordinal));

            var rollback = await harness.CallToolAsync<ToolEnvelope<AtomicRollbackData>>(
                "candoitall_app_rollback",
                new Dictionary<string, object?>
                {
                    ["logicalAppId"] = atomic.Data.LogicalAppId,
                    ["transactionId"] = atomic.Data.TransactionId
                });

            Assert.True(rollback.Ok, rollback.Error?.Message);
            Assert.NotNull(rollback.Data);
            Assert.Equal(start.Data.SessionId, rollback.Data!.RestoredSessionId);

            var restoredStatus = await harness.CallToolAsync<ToolEnvelope<AppStatusData>>(
                "candoitall_app_status",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = start.Data.SessionId
                });

            Assert.True(restoredStatus.Ok, restoredStatus.Error?.Message);
            Assert.Contains(restoredStatus.Data!.State, new[] { AppLifecycleState.Healthy, AppLifecycleState.Running, AppLifecycleState.Starting, AppLifecycleState.Restarting });
            Assert.Equal(rollback.Data.RestoredRevision.Value, restoredStatus.Data!.Revision!.Value);

            var stoppedCandidate = await harness.CallToolAsync<ToolEnvelope<AppStatusData>>(
                "candoitall_app_status",
                new Dictionary<string, object?>
                {
                    ["sessionId"] = atomic.Data.CandidateSessionId
                });

            Assert.True(stoppedCandidate.Ok, stoppedCandidate.Error?.Message);
            Assert.Contains(stoppedCandidate.Data!.State, new[] { AppLifecycleState.Stopped, AppLifecycleState.ExitedUnexpectedly });

            var rollbackEvents = await harness.CallToolAsync<ToolEnvelope<AppEventsData>>(
                "candoitall_app_events",
                new Dictionary<string, object?>
                {
                    ["logicalAppId"] = atomic.Data.LogicalAppId
                });

            Assert.Contains(rollbackEvents.Data!.Entries, entry => string.Equals(entry.EventType, "rollback-committed", StringComparison.Ordinal));
        }
        finally
        {
            await StopSessionAsync(harness, start.Data!.SessionId);
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

        if (!stop.Ok && string.Equals(stop.Error?.Code, "SessionNotFound", StringComparison.Ordinal))
        {
            return;
        }

        Assert.True(stop.Ok, stop.Error?.Message);
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
            return await JsonSerializer.DeserializeAsync<BackendRegistrationRecord>(stream, JsonOptions);
        }
        catch
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

    private static async Task<ToolEnvelope<T>> CallBackendToolAsync<T>(BackendRegistrationRecord registration, string route, object request, string? requestId = null)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(registration.BaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromMinutes(5)
        };

        client.DefaultRequestHeaders.Add(BackendAuth.HeaderName, registration.AuthToken);
        if (!string.IsNullOrWhiteSpace(requestId))
        {
            client.DefaultRequestHeaders.Add("X-CanDoItAll-RequestId", requestId);
        }

        using var response = await client.PostAsJsonAsync($"/api/tools/{route}", request, JsonOptions);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ToolEnvelope<T>>(JsonOptions);
        Assert.NotNull(envelope);
        return envelope!;
    }

    private static WorkflowGuidanceData? ReadGuidance<T>(ToolEnvelope<T> envelope)
    {
        if (envelope.WorkflowGuidance is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(envelope.WorkflowGuidance, JsonOptions);
        return JsonSerializer.Deserialize<WorkflowGuidanceData>(json, JsonOptions);
    }

    private static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
