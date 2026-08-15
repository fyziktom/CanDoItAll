using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureRuntimeAdapterTests
{
    [Fact]
    public async Task Direct_adapter_registers_exact_typed_plan_with_the_Workbench_lifecycle_owner()
    {
        var host = new RecordingLongRunningProcessHost();
        await using var registry = CreateRegistry(host);
        var adapter = new ProjectStructureRuntimeExecutionAdapter(
            registry,
            new StubExecutableResolver("/tools/dotnet"),
            NullLogger<ProjectStructureRuntimeExecutionAdapter>.Instance);
        var plan = CreatePlan(
            ["run", "--project", "/workspace/App.csproj"],
            new Dictionary<string, string?> { ["ASPNETCORE_URLS"] = "http://127.0.0.1:5032" });

        var result = await adapter.LaunchAsync(plan, "node-1", CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.NotNull(host.Request);
        Assert.Equal("/tools/dotnet", host.Request!.ExecutablePath);
        Assert.Equal(plan.Arguments, host.Request.Arguments);
        Assert.Equal(plan.EnvironmentVariables, host.Request.EnvironmentVariables);
        Assert.Equal(plan.WorkingDirectory, host.Request.WorkingDirectory);
        Assert.False(host.Session.Detached);
        Assert.True(registry.IsRunning("node-1"));

        var stopped = await registry.StopSessionAsync("node-1", CancellationToken.None);

        Assert.True(stopped.IsSuccess, stopped.Message);
        Assert.True(host.Session.Terminated);
    }

    [Fact]
    public async Task Direct_adapter_reports_cancellation_without_shell_fallback()
    {
        var host = new RecordingLongRunningProcessHost { CancelStart = true };
        await using var registry = CreateRegistry(host);
        var adapter = new ProjectStructureRuntimeExecutionAdapter(
            registry,
            new StubExecutableResolver("/tools/dotnet"),
            NullLogger<ProjectStructureRuntimeExecutionAdapter>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await adapter.LaunchAsync(CreatePlan(), "node-1", cancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.Contains("canceled", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(host.Request);
    }

    [Fact]
    [Trait("Category", "UnixPortabilityCore")]
    public async Task Direct_adapter_starts_a_real_cross_platform_executable_without_a_shell()
    {
        await using var registry = CreateRegistry(new LocalWorkspaceProcessHost());
        var adapter = new ProjectStructureRuntimeExecutionAdapter(
            registry,
            new ProjectStructureExecutableResolver(new WorkspaceExecutableLocator()),
            NullLogger<ProjectStructureRuntimeExecutionAdapter>.Instance);
        var plan = CreatePlan(
            arguments: ["--version"],
            workingDirectory: AppContext.BaseDirectory);

        var result = await adapter.LaunchAsync(plan, "actual-host-node", CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Contains("Started .NET runtime directly as process", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Direct_adapter_distinguishes_missing_dependency_and_terminal_only_plan()
    {
        var registry = CreateRegistry(new RecordingLongRunningProcessHost());
        var adapter = new ProjectStructureRuntimeExecutionAdapter(
            registry,
            new StubExecutableResolver(null),
            NullLogger<ProjectStructureRuntimeExecutionAdapter>.Instance);

        var missing = adapter.Probe(CreatePlan());
        var terminalOnly = adapter.Probe(CreatePlan(terminalOnly: true));

        Assert.Equal(ProjectStructureRuntimeCapabilityStatus.DependencyMissing, missing.Status);
        Assert.Equal(ProjectStructureRuntimeCapabilityStatus.PolicyDenied, terminalOnly.Status);
    }

    [Fact]
    public async Task A_new_scoped_adapter_can_stop_the_session_owned_by_the_host_lifetime_registry()
    {
        var host = new RecordingLongRunningProcessHost();
        await using var registry = CreateRegistry(host);
        var firstAdapter = new ProjectStructureRuntimeExecutionAdapter(
            registry,
            new StubExecutableResolver("/tools/dotnet"),
            NullLogger<ProjectStructureRuntimeExecutionAdapter>.Instance);
        var recoveredAdapter = new ProjectStructureRuntimeExecutionAdapter(
            registry,
            new StubExecutableResolver("/tools/dotnet"),
            NullLogger<ProjectStructureRuntimeExecutionAdapter>.Instance);
        var started = await firstAdapter.LaunchAsync(CreatePlan(), "node-1", CancellationToken.None);

        var stopped = await recoveredAdapter.StopAsync("node-1", CancellationToken.None);

        Assert.True(started.IsSuccess, started.Message);
        Assert.True(stopped.IsSuccess, stopped.Message);
        Assert.True(host.Session.Terminated);
        Assert.False(registry.IsRunning("node-1"));
    }

    [Fact]
    public async Task Host_shutdown_terminates_every_registered_Workbench_runtime_session()
    {
        var host = new RecordingLongRunningProcessHost();
        await using var registry = CreateRegistry(host);
        var adapter = new ProjectStructureRuntimeExecutionAdapter(
            registry,
            new StubExecutableResolver("/tools/dotnet"),
            NullLogger<ProjectStructureRuntimeExecutionAdapter>.Instance);
        var started = await adapter.LaunchAsync(CreatePlan(), "node-1", CancellationToken.None);

        await registry.StopAsync(CancellationToken.None);

        Assert.True(started.IsSuccess, started.Message);
        Assert.True(host.Session.Terminated);
        Assert.False(registry.IsRunning("node-1"));
    }

    [Fact]
    public async Task Host_shutdown_with_an_already_canceled_token_still_terminates_every_owned_session()
    {
        var host = new RecordingLongRunningProcessHost();
        await using var registry = CreateRegistry(host);
        var adapter = new ProjectStructureRuntimeExecutionAdapter(
            registry,
            new StubExecutableResolver("/tools/dotnet"),
            NullLogger<ProjectStructureRuntimeExecutionAdapter>.Instance);
        Assert.True((await adapter.LaunchAsync(CreatePlan(), "node-1", CancellationToken.None)).IsSuccess);
        Assert.True((await adapter.LaunchAsync(CreatePlan(), "node-2", CancellationToken.None)).IsSuccess);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await registry.StopAsync(cancellation.Token);

        Assert.All(host.Sessions, session => Assert.True(session.Terminated));
        Assert.All(host.Sessions, session => Assert.True(session.Disposed));
        Assert.False(registry.IsRunning("node-1"));
        Assert.False(registry.IsRunning("node-2"));
    }

    [Fact]
    public async Task Host_shutdown_continues_after_each_session_reports_termination_cancellation()
    {
        var host = new RecordingLongRunningProcessHost { CancelEveryTermination = true };
        await using var registry = CreateRegistry(host);
        var adapter = new ProjectStructureRuntimeExecutionAdapter(
            registry,
            new StubExecutableResolver("/tools/dotnet"),
            NullLogger<ProjectStructureRuntimeExecutionAdapter>.Instance);
        Assert.True((await adapter.LaunchAsync(CreatePlan(), "node-1", CancellationToken.None)).IsSuccess);
        Assert.True((await adapter.LaunchAsync(CreatePlan(), "node-2", CancellationToken.None)).IsSuccess);

        await registry.StopAsync(CancellationToken.None);

        Assert.All(host.Sessions, session => Assert.Equal(1, session.TerminationAttempts));
        Assert.All(host.Sessions, session => Assert.True(session.Disposed));
        Assert.False(registry.IsRunning("node-1"));
        Assert.False(registry.IsRunning("node-2"));
    }

    [Theory]
    [InlineData(ProjectStructureRuntimeHostPlatform.Linux)]
    [InlineData(ProjectStructureRuntimeHostPlatform.MacOS)]
    public void Elevation_is_explicitly_unsupported_on_non_Windows_hosts(
        ProjectStructureRuntimeHostPlatform platform)
    {
        var adapter = new ProjectStructureRuntimeElevationAdapter(
            new ProjectStructureRuntimeHostContext(platform),
            new StubExecutableResolver("/tools/dotnet"),
            NullLogger<ProjectStructureRuntimeElevationAdapter>.Instance);

        var capability = adapter.Probe(CreatePlan());

        Assert.Equal(ProjectStructureRuntimeCapabilityStatus.Unsupported, capability.Status);
        Assert.Contains("no sudo, pkexec, or AppleScript fallback", capability.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProjectStructureRuntimeHostPlatform.Linux)]
    [InlineData(ProjectStructureRuntimeHostPlatform.MacOS)]
    public void Terminal_presentation_is_headless_until_explicitly_configured(
        ProjectStructureRuntimeHostPlatform platform)
    {
        var presenter = new ProjectStructureTerminalPresenter(
            new ProjectStructureRuntimeHostContext(platform),
            ProjectStructureRuntimePresentationOptions.Default,
            new StubExecutableResolver("/tools/dotnet"),
            NullLogger<ProjectStructureTerminalPresenter>.Instance);

        var capability = presenter.Probe(CreatePlan());

        Assert.Equal(ProjectStructureRuntimeCapabilityStatus.Headless, capability.Status);
    }

    [Fact]
    public void Configured_Linux_terminal_is_reported_available_when_both_dependencies_resolve()
    {
        var presenter = new ProjectStructureTerminalPresenter(
            new ProjectStructureRuntimeHostContext(ProjectStructureRuntimeHostPlatform.Linux),
            new ProjectStructureRuntimePresentationOptions
            {
                LinuxTerminalExecutable = "x-terminal-emulator",
                LinuxTerminalArgumentPrefix = ["-e"]
            },
            new StubExecutableResolver("/usr/bin/resolved"),
            NullLogger<ProjectStructureTerminalPresenter>.Instance);

        var capability = presenter.Probe(CreatePlan());

        Assert.Equal(ProjectStructureRuntimeCapabilityStatus.Available, capability.Status);
    }

    [Fact]
    public void Workbench_composition_binds_explicit_terminal_presentation_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ProjectStructureRuntimePresentationOptions.SectionName}:LinuxTerminalExecutable"] = "x-terminal-emulator",
                [$"{ProjectStructureRuntimePresentationOptions.SectionName}:LinuxTerminalArgumentPrefix:0"] = "-e"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddWorkbenchModule(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<ProjectStructureRuntimePresentationOptions>();
        Assert.Equal("x-terminal-emulator", options.LinuxTerminalExecutable);
        Assert.Equal(["-e"], options.LinuxTerminalArgumentPrefix);
    }

    private static ProjectStructureRuntimeLaunchPlan CreatePlan(
        IReadOnlyList<string>? arguments = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        bool terminalOnly = false,
        string? workingDirectory = null)
        => new(
            ProjectStructureRuntimePlanKind.DotNet,
            ["dotnet"],
            arguments ?? ["run"],
            environment ?? new Dictionary<string, string?>(),
            workingDirectory ?? "/workspace",
            "dotnet run",
            ".NET runtime",
            [],
            RequiresApproval: false,
            TerminalOnly: terminalOnly);

    private static ProjectStructureRuntimeSessionRegistry CreateRegistry(
        IWorkspaceLongRunningProcessHost host)
        => new(
            host,
            NullLogger<ProjectStructureRuntimeSessionRegistry>.Instance);

    private sealed class StubExecutableResolver(string? executablePath) : IProjectStructureExecutableResolver
    {
        public ProjectStructureExecutableResolution Resolve(
            IReadOnlyList<string> candidates,
            string workingDirectory)
            => executablePath is null
                ? new(null, "The required executable dependency is missing.")
                : new(executablePath, "Executable dependency is available.");
    }

    private sealed class RecordingLongRunningProcessHost : IWorkspaceLongRunningProcessHost
    {
        private readonly RecordingProcessSession firstSession = new();

        public WorkspaceProcessSessionRequest? Request { get; private set; }

        public RecordingProcessSession Session => firstSession;

        public List<RecordingProcessSession> Sessions { get; } = [];

        public bool CancelStart { get; init; }

        public bool CancelEveryTermination { get; init; }

        public ExecutionBoundaryDescriptor DescribeBoundary() => ExecutionBoundaryDescriptor.Unknown;

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Direct runtime launch must use the long-running session seam.");

        public Task<IWorkspaceProcessSession> StartSessionAsync(
            WorkspaceProcessSessionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (CancelStart)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            Request = request;
            var session = Sessions.Count == 0 ? firstSession : new RecordingProcessSession();
            session.CancelTermination = CancelEveryTermination;
            Sessions.Add(session);
            return Task.FromResult<IWorkspaceProcessSession>(session);
        }

        public Task<WorkspaceProcessTerminationResult> TerminateOwnedProcessAsync(
            WorkspaceOwnedProcessIdentity identity,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Detached runtime ownership is not terminated during launch.");
    }

    private sealed class RecordingProcessSession : IWorkspaceProcessSession
    {
        private readonly TaskCompletionSource<WorkspaceProcessExecutionResult> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Detached { get; private set; }

        public bool Terminated { get; private set; }

        public bool Disposed { get; private set; }

        public bool CancelTermination { get; set; }

        public int TerminationAttempts { get; private set; }

        public WorkspaceOwnedProcessIdentity Identity { get; } = new(
            1234,
            DateTimeOffset.Parse("2026-08-10T00:00:00Z"),
            new string('a', 64),
            new WorkspaceOwnedProcessBoundary(
                WorkspaceOwnedProcessBoundaryKind.UnixProcessGroup,
                1234,
                Guid.Empty));

        public bool HasExited => false;

        public WorkspaceProcessOutputSnapshot CaptureOutput() => new(string.Empty, string.Empty, false, false);

        public Task<WorkspaceProcessExecutionResult> WaitForExitAsync(CancellationToken cancellationToken = default)
            => completion.Task.WaitAsync(cancellationToken);

        public Task<WorkspaceProcessExecutionResult> TerminateAsync(
            WorkspaceProcessTerminationReason reason,
            string failureMessage,
            CancellationToken cancellationToken = default)
        {
            TerminationAttempts++;
            if (CancelTermination)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            Terminated = true;
            var result = CreateExecutionResult(reason, failureMessage);
            completion.TrySetResult(result);
            return Task.FromResult(result);
        }

        public WorkspaceOwnedProcessIdentity Detach()
        {
            Detached = true;
            return Identity;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            if (!completion.Task.IsCompleted)
            {
                completion.TrySetResult(CreateExecutionResult(
                    WorkspaceProcessTerminationReason.CallerCanceled,
                    "Disposed."));
            }

            return ValueTask.CompletedTask;
        }

        private static WorkspaceProcessExecutionResult CreateExecutionResult(
            WorkspaceProcessTerminationReason reason,
            string failureMessage)
            => new(
                Started: true,
                ExitCode: 0,
                Stdout: string.Empty,
                Stderr: string.Empty,
                StdoutTruncated: false,
                StderrTruncated: false,
                StartedAtUtc: DateTimeOffset.Parse("2026-08-10T00:00:00Z"),
                CompletedAtUtc: DateTimeOffset.Parse("2026-08-10T00:00:01Z"),
                TimedOut: false,
                Boundary: ExecutionBoundaryDescriptor.Unknown,
                FailureMessage: failureMessage,
                TerminationReason: reason,
                ResidualProcessPossible: false);
    }
}
