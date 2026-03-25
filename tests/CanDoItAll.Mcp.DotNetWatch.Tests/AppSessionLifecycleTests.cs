using CanDoItAll.Mcp.Core.Observability;
using CanDoItAll.Mcp.DotNetWatch.Health;
using CanDoItAll.Mcp.DotNetWatch.Runtime;

namespace CanDoItAll.Mcp.DotNetWatch.Tests;

public sealed class AppSessionLifecycleTests
{
    [Fact]
    public void NoteLog_RealRestartMessages_InvalidateHealthyState_AndTrackNextGeneration()
    {
        var session = CreateWatchSession();
        session.MarkHealthy(CreateHealthySnapshot(watchIteration: 1, runtimePid: 4100));

        session.NoteLog(CreateLogEntry(10, "dotnet watch : File added: .\\McpRestartProbe.cs"));
        session.NoteLog(CreateLogEntry(11, "dotnet watch : Restart is needed to apply the changes."));
        session.NoteLog(CreateLogEntry(12, "dotnet watch : [CanDoItAll.Web (net10.0)] Exited"));

        var status = session.ToStatusData();

        Assert.Equal(AppLifecycleState.Restarting, status.State);
        Assert.Equal(2, status.SessionVersion);
        Assert.NotNull(status.Watch);
        Assert.True(status.Watch!.PendingChange);
        Assert.Equal(WatchProcessingState.ChildExited, status.Watch.State);
        Assert.Equal(HotReloadOutcome.RestartRequired, status.Watch.LastHotReloadOutcome);
        Assert.Equal(2, status.Watch.ExpectedWatchIteration);
        Assert.Equal(1, status.Watch.ConfirmedWatchIteration);
        Assert.Null(status.Watch.RuntimePid);
        Assert.Equal("Pending", status.Health!.Status);
    }

    [Fact]
    public void ConfirmsCurrentGeneration_RequiresReplacementIteration_WhenRestartWasRequested()
    {
        var session = CreateWatchSession();
        session.MarkHealthy(CreateHealthySnapshot(watchIteration: 3, runtimePid: 5100));
        session.NoteLog(CreateLogEntry(20, "dotnet watch : File deleted: .\\McpRestartProbe.cs"));
        session.NoteLog(CreateLogEntry(21, "dotnet watch : Restart is needed to apply the changes."));
        session.NoteLog(CreateLogEntry(22, "dotnet watch : [CanDoItAll.Web (net10.0)] Exited"));

        var staleRuntimeSnapshot = CreateHealthySnapshot(watchIteration: 3, runtimePid: 5100);
        var replacementRuntimeSnapshot = CreateHealthySnapshot(watchIteration: 4, runtimePid: 6200);

        Assert.False(session.ConfirmsCurrentGeneration(staleRuntimeSnapshot));
        Assert.True(session.ConfirmsCurrentGeneration(replacementRuntimeSnapshot));
    }

    [Fact]
    public void MarkHealthy_AfterHotReloadSuccess_ClearsPendingChange_WithoutInventingARestart()
    {
        var session = CreateWatchSession();
        session.MarkHealthy(CreateHealthySnapshot(watchIteration: 7, runtimePid: 7100));

        session.NoteLog(CreateLogEntry(30, "dotnet watch : File updated: .\\Components\\Pages\\Home.razor"));
        session.NoteLog(CreateLogEntry(31, "dotnet watch : [CanDoItAll.Web (net10.0)] Hot reload succeeded."));
        session.MarkHealthy(CreateHealthySnapshot(watchIteration: 7, runtimePid: 7100));

        var status = session.ToStatusData();

        Assert.Equal(AppLifecycleState.Healthy, status.State);
        Assert.NotNull(status.Watch);
        Assert.False(status.Watch!.PendingChange);
        Assert.Equal(WatchProcessingState.WaitingForChanges, status.Watch.State);
        Assert.Equal(HotReloadOutcome.Succeeded, status.Watch.LastHotReloadOutcome);
        Assert.Equal(7, status.Watch.ExpectedWatchIteration);
        Assert.Equal(7, status.Watch.ConfirmedWatchIteration);
        Assert.Equal(7100, status.Watch.RuntimePid);
        Assert.True(status.Health!.IsReady);
        Assert.Equal(7, status.Health.WatchIteration);
        Assert.Equal(7100, status.Health.RuntimePid);
    }

    [Fact]
    public void ConfirmsCurrentGeneration_Rejects_RuntimeOwnedByDifferentSession()
    {
        var session = CreateWatchSession();

        var foreignSnapshot = CreateHealthySnapshot(watchIteration: 1, runtimePid: 4100) with
        {
            OwnerId = "app_other"
        };

        Assert.False(session.ConfirmsCurrentGeneration(foreignSnapshot));
    }

    private static AppSession CreateWatchSession()
    {
        var template = new AppStartTemplate(
            @"C:\repo\src\CanDoItAll.Web\CanDoItAll.Web.csproj",
            @"C:\repo\src\CanDoItAll.Web",
            AppRunMode.WatchRun,
            "Debug",
            null,
            "https",
            [],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development"
            },
            ["https://localhost:7271", "http://localhost:5032"]);

        return new AppSession("app_test", template, "corr_test", new RingLogBuffer(128), healthEnabled: true);
    }

    private static HealthSnapshot CreateHealthySnapshot(int watchIteration, int runtimePid)
    {
        return new HealthSnapshot(
            "Healthy",
            true,
            DateTimeOffset.UtcNow,
            null,
            "https://localhost:7271/_dev/runtime",
            "Ready",
            watchIteration,
            runtimePid,
            ["https://localhost:7271", "http://localhost:5032"])
        {
            OwnerKind = "app",
            OwnerId = "app_test",
            ServerInstanceId = "srv_test"
        };
    }

    private static LogEntry CreateLogEntry(long sequence, string text)
    {
        return new LogEntry(
            sequence,
            DateTimeOffset.UtcNow,
            "ProcessStdOut",
            "stdout",
            1,
            "corr_test",
            text);
    }
}
