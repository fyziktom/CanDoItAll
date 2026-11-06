using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed partial class ProjectStructureRuntimeAdapterTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_runtime_stop_checks_original_admission_after_waiting_without_stopping_a_successor(bool denyWhileWaiting) {
        var host = new RecordingLongRunningProcessHost();
        await using var registry = CreateRegistry(host);
        var adapter = new ProjectStructureRuntimeExecutionAdapter(registry, new StubExecutableResolver("/tools/dotnet"),
            NullLogger<ProjectStructureRuntimeExecutionAdapter>.Instance, TimeSpan.Zero);
        var accepted = await adapter.LaunchAsync(CreatePlan(), "Original stoppable runtime", CancellationToken.None);
        Assert.True(accepted.IsSuccess);
        var identity = Assert.IsType<WorkspaceOwnedProcessIdentity>(accepted.Identity);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker = registry.StartSessionAsync("Other owned runtime", new WorkspaceProcessSessionRequest("test", "gate holder",
            "/tools/dotnet", [], "/workspace", new Dictionary<string, string?>(), 1024, 1024), CancellationToken.None,
            authorizeLaunch: async token => {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
            });
        var allowed = true;
        var checkedAfterGate = false;
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var pending = adapter.StopAsync("Original stoppable runtime", identity, CancellationToken.None, token => {
                token.ThrowIfCancellationRequested();
                Assert.Equal(2, host.Sessions.Count);
                Assert.False(host.Session.Terminated);
                checkedAfterGate = true;
                if (!allowed) {
                    throw new UnauthorizedAccessException("Original Stop access retired while waiting.");
                }
                return ValueTask.CompletedTask;
            });
            Assert.False(pending.IsCompleted);
            allowed = !denyWhileWaiting;
            release.TrySetResult();
            Assert.True((await blocker).IsSuccess);
            if (denyWhileWaiting) {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => pending);
                Assert.Equal(identity, registry.GetIdentity("Original stoppable runtime"));
            } else {
                var stopped = await pending;
                Assert.True(stopped.IsSuccess);
                Assert.Equal(identity, stopped.Identity);
            }
            Assert.True(checkedAfterGate);
            Assert.Equal(!denyWhileWaiting, host.Session.Terminated);
            Assert.False(host.Sessions[1].Terminated);
        } finally {
            release.TrySetResult();
            await blocker;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_runtime_registry_checks_original_admission_after_its_launch_gate(bool denyWhileWaiting) {
        var host = new RecordingLongRunningProcessHost();
        await using var registry = CreateRegistry(host);
        var adapter = new ProjectStructureRuntimeExecutionAdapter(registry, new StubExecutableResolver("/tools/dotnet"),
            NullLogger<ProjectStructureRuntimeExecutionAdapter>.Instance, TimeSpan.Zero);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocker = registry.StartSessionAsync("Owned gate holder", new WorkspaceProcessSessionRequest("test", "gate holder",
            "/tools/dotnet", [], "/workspace", new Dictionary<string, string?>(), 1024, 1024), CancellationToken.None,
            authorizeLaunch: async token => {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
            });
        var allowed = true;
        var checkedAfterGate = false;
        Task<ProjectStructureRuntimeLaunchResult>? pending = null;
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            pending = adapter.LaunchAsync(CreatePlan() with { AuthorizeLaunch = token => {
                token.ThrowIfCancellationRequested();
                Assert.Single(host.Sessions);
                checkedAfterGate = true;
                if (!allowed) {
                    throw new UnauthorizedAccessException("Original runtime access retired while waiting for native gate.");
                }
                return ValueTask.CompletedTask;
            } }, "Original gated runtime", CancellationToken.None);
            Assert.False(pending.IsCompleted);
            allowed = !denyWhileWaiting;
            release.TrySetResult();
            Assert.True((await blocker).IsSuccess);
            if (denyWhileWaiting) {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => pending);
                Assert.False(registry.IsRunning("Original gated runtime"));
            } else {
                var accepted = await pending;
                Assert.True(accepted.IsSuccess, accepted.Message);
                Assert.Equal(host.Sessions[1].Identity, accepted.Identity);
                Assert.False(host.Sessions[1].Terminated);
            }
            Assert.True(checkedAfterGate);
            Assert.Equal(denyWhileWaiting ? 1 : 2, host.Sessions.Count);
        } finally {
            release.TrySetResult();
            await blocker;
        }
    }
}
