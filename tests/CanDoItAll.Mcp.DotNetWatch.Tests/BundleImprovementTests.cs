using System.Text.Json;
using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Mcp.DotNetWatch.Backend;
using CanDoItAll.Mcp.DotNetWatch.Configuration;
using CanDoItAll.Mcp.DotNetWatch.Guidance;
using CanDoItAll.Mcp.DotNetWatch.Runtime;
using CanDoItAll.Mcp.DotNetWatch.Runtime.Atomic;
using CanDoItAll.Mcp.DotNetWatch.Runtime.Coordination;
using CanDoItAll.Mcp.DotNetWatch.Runtime.LaunchSpecs;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.DotNetWatch.Tests;

public sealed class BundleImprovementTests
{
    [Fact]
    public void WorkflowGuidancePolicy_Emits_HealthyWatchGuidance_WithinBudget()
    {
        using var workspace = CreateWorkspace();
        var configuration = new RuntimeConfiguration(Options.Create(CreateOptions(workspace.RootPath)));
        var policy = new WorkflowGuidancePolicy(configuration);
        var status = CreateHealthyWatchStatus();

        var guidance = policy.ForApp(status);

        Assert.NotNull(guidance);
        Assert.Equal("watch-small-step", guidance!.Mode);
        Assert.Equal("edit-1-nearby-file", guidance.Next);
        Assert.True(JsonSerializer.Serialize(guidance).Length <= configuration.WorkflowGuidanceMaxSerializedCharacters);
    }

    [Fact]
    public void WorkflowGuidancePolicy_Suppresses_WhenBudgetWouldBeExceeded()
    {
        using var workspace = CreateWorkspace();
        var options = CreateOptions(workspace.RootPath);
        options.WorkflowGuidance.MaxSerializedCharacters = 24;
        var configuration = new RuntimeConfiguration(Options.Create(options));
        var policy = new WorkflowGuidancePolicy(configuration);

        var guidance = policy.ForApp(CreateHealthyWatchStatus());

        Assert.Null(guidance);
    }

    [Fact]
    public async Task BackendRequestReplayStore_Deduplicates_RepeatedRequestIds()
    {
        var store = new BackendRequestReplayStore();
        var invocationCount = 0;
        var request = new AppStartRequest(
            LogicalAppId: "web",
            ProjectPath: @"C:\repo\src\CanDoItAll.Web\CanDoItAll.Web.csproj",
            Mode: AppRunMode.WatchRun,
            LaunchType: AppLaunchType.Project,
            PreferredLane: RuntimeLaneKind.SourceWatch,
            EntryPath: null,
            ConfigurationName: "Debug",
            Framework: null,
            LaunchProfile: "https",
            WorkingDirectory: @"C:\repo\src\CanDoItAll.Web",
            Arguments: [],
            EnvironmentOverlay: null,
            Urls: ["https://localhost:7271"],
            ReuseIfCompatible: true,
            ConflictPolicy: AppStartConflictPolicy.Fail,
            WaitFor: AppWaitCondition.None);

        async Task<ToolEnvelope<AppStartData>> Callback(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref invocationCount);
            await Task.Delay(50, cancellationToken);

            return ToolEnvelope<AppStartData>.Success(
                "candoitall_app_start",
                "corr_test",
                new AppStartData(
                    SessionId: "app_deduped",
                    CorrelationId: "corr_test",
                    Reused: false,
                    Mode: AppRunMode.WatchRun,
                    State: AppLifecycleState.Starting,
                    SessionVersion: 1,
                    ProjectPath: request.ProjectPath!,
                    ObservedUrls: ["https://localhost:7271"],
                    InitialCursor: 10,
                    LastKnownPid: 5000,
                    Watch: null));
        }

        var firstTask = store.ExecuteJsonAsync("app-start", "req_same", request, Callback, CancellationToken.None);
        var secondTask = store.ExecuteJsonAsync("app-start", "req_same", request, Callback, CancellationToken.None);
        var first = await firstTask;
        var second = await secondTask;

        Assert.Equal(1, invocationCount);
        Assert.Equal(first, second);
        Assert.Contains("app_deduped", first, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkspaceExecutionLock_Allows_NonConflictingScopedLeases_AndRejects_Conflicts()
    {
        var executionLock = new WorkspaceExecutionLock();

        await using var bridgeLease = await executionLock.AcquireMutationAsync("bridge-repair", ["bridge", "backend-registration"], CancellationToken.None);
        await using var slotLease = await executionLock.AcquireMutationAsync("slot-prepare", ["logical-app:web", "slot:web:slot-a"], CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ToolInvocationException>(() =>
            executionLock.AcquireMutationAsync("slot-commit", ["logical-app:web"], CancellationToken.None));

        Assert.Equal("ResourceConflict", exception.Code);
        Assert.Contains("slot-prepare", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResourceScopePlanner_MarksSolutionBuildAndTestProjectAsConflictingWorkspaceSegments()
    {
        using var workspace = CreateWorkspace();
        var configuration = new RuntimeConfiguration(Options.Create(CreateOptions(workspace.RootPath)));
        var planner = new ResourceScopePlanner(configuration);

        var solutionPlan = planner.ForOperation(configuration.SolutionPath, []);
        var testProjectPlan = planner.ForOperation(
            Path.Combine(configuration.WorkspaceRoot, "tests", "CanDoItAll.Mcp.DotNetWatch.Tests", "CanDoItAll.Mcp.DotNetWatch.Tests.csproj"),
            []);

        Assert.Contains("source-tree:solution", solutionPlan.ResourceKeys);
        Assert.Contains("workspace-segment:tests", solutionPlan.ResourceKeys);
        Assert.Contains("workspace-segment:tests", testProjectPlan.ResourceKeys);
    }

    [Fact]
    public void RuntimeSlotRegistry_Persists_SlotManifest_Transaction_AndLogicalApp()
    {
        using var workspace = CreateWorkspace();
        var configuration = new RuntimeConfiguration(Options.Create(CreateOptions(workspace.RootPath)));
        var registry = new RuntimeSlotRegistry(configuration);
        var state = registry.GetState("web");
        var revision = registry.CreatePublishedRevision("web", "slot-a", configuration.RuntimeSlotRoot);
        var manifest = new SlotManifest(
            SlotId: "slot-a",
            LogicalAppId: "web",
            PublishHash: revision.Value,
            EntryPath: Path.Combine(configuration.RuntimeSlotRoot, "slot-a", "payload", "CanDoItAll.Web.dll"),
            WorkingDirectory: Path.Combine(configuration.RuntimeSlotRoot, "slot-a", "payload"),
            HealthUrls: ["http://127.0.0.1:5500/_dev/runtime"],
            CreatedUtc: DateTimeOffset.UtcNow)
        {
            ProjectPath = Path.Combine(configuration.WorkspaceRoot, "src", "CanDoItAll.Web", "CanDoItAll.Web.csproj")
        };
        var transaction = new AtomicTransactionRecord(
            TransactionId: "txn_1",
            LogicalAppId: "web",
            SourceSignature: "source",
            TargetSlotId: "slot-a",
            PreviousActiveSessionId: "app_old",
            PreviousActiveRevision: null,
            CandidateSessionId: "app_candidate",
            CandidateRevision: revision,
            State: AtomicTransactionState.Committed,
            CreatedUtc: DateTimeOffset.UtcNow)
        {
            CommittedUtc = DateTimeOffset.UtcNow
        };
        var logicalApp = new LogicalAppRecord(
            LogicalAppId: "web",
            ActiveSessionId: "app_candidate",
            ActiveRevision: revision,
            PreviousSessionId: "app_old",
            PreviousRevision: null,
            CurrentSlotId: "slot-a",
            LastCommittedTransactionId: "txn_1",
            RollbackAvailable: true);

        registry.SaveSlotManifest(state, manifest);
        registry.SaveTransaction(state, transaction);
        registry.SaveLogicalApp(state, logicalApp);

        var snapshot = registry.GetSnapshot("web");
        var artifactsPath = registry.GetSlotArtifactsPath(state, "slot-a");

        Assert.Equal("app_candidate", snapshot.App.ActiveSessionId);
        Assert.NotNull(snapshot.ActiveSlot);
        Assert.Equal("slot-a", snapshot.ActiveSlot!.SlotId);
        Assert.NotNull(snapshot.ActiveTransaction);
        Assert.Equal(AtomicTransactionState.Committed, snapshot.ActiveTransaction!.State);
        Assert.EndsWith(Path.Combine("slot-a", "artifacts"), artifactsPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildManagedProcessStartArguments_Supports_PublishedDll_And_ExecutableLaunches()
    {
        var published = new PublishedDllLaunchSpec(
            LogicalAppId: "web",
            LaneKind: RuntimeLaneKind.PublishedActive,
            ProjectPath: @"C:\repo\src\CanDoItAll.Web\CanDoItAll.Web.csproj",
            EntryPath: @"C:\repo\publish\CanDoItAll.Web.dll",
            WorkingDirectory: @"C:\repo\publish",
            Configuration: "Release",
            Framework: "net10.0",
            Arguments: ["--urls", "http://127.0.0.1:5500"],
            EnvironmentOverlay: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Urls: ["http://127.0.0.1:5500"],
            HealthUrls: [new Uri("http://127.0.0.1:5500/_dev/runtime", UriKind.Absolute)],
            SlotId: null);
        var executable = new ExecutableLaunchSpec(
            LogicalAppId: "worker",
            LaneKind: RuntimeLaneKind.ExternalExecutable,
            EntryPath: @"C:\repo\tools\worker.exe",
            WorkingDirectory: @"C:\repo\tools",
            ProjectPath: null,
            Configuration: "Release",
            Arguments: ["--listen", "127.0.0.1:6600"],
            EnvironmentOverlay: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Urls: [],
            HealthUrls: []);

        var publishedStart = AppRuntimeManager.BuildManagedProcessStartArguments(published, ["--flag"], @"C:\repo\.mcp-state\artifacts\unused");
        var executableStart = AppRuntimeManager.BuildManagedProcessStartArguments(executable, ["--extra"], @"C:\repo\.mcp-state\artifacts\unused");

        Assert.Equal("dotnet", publishedStart.Command);
        Assert.Equal(@"C:\repo\publish\CanDoItAll.Web.dll", publishedStart.Arguments[0]);
        Assert.Contains("--flag", publishedStart.Arguments);
        Assert.Equal(@"C:\repo\tools\worker.exe", executableStart.Command);
        Assert.Contains("--extra", executableStart.Arguments);
    }

    private static AppStatusData CreateHealthyWatchStatus()
    {
        return new AppStatusData(
            SessionId: "app_watch",
            CorrelationId: "corr_watch",
            State: AppLifecycleState.Healthy,
            Mode: AppRunMode.WatchRun,
            ProjectPath: @"C:\repo\src\CanDoItAll.Web\CanDoItAll.Web.csproj",
            SessionVersion: 3,
            LastKnownPid: 4321,
            ObservedUrls: ["https://localhost:7271"],
            LastExitCode: null,
            LastStartUtc: DateTimeOffset.UtcNow,
            LastRestartUtc: null,
            LastStopUtc: null,
            LastCursor: 15,
            Health: new HealthData("Healthy", DateTimeOffset.UtcNow, null, "https://localhost:7271/_dev/runtime", "Ready", true, 3, 4321),
            RecentEvents: ["Healthy."],
            Watch: new WatchStatusData(WatchProcessingState.WaitingForChanges, "Watching", false, 4100, 4321, 3, 3, HotReloadOutcome.Succeeded, 3, DateTimeOffset.UtcNow))
        {
            LogicalAppId = "web",
            LaneKind = RuntimeLaneKind.SourceWatch,
            Revision = new RuntimeRevisionData("WatchIteration", "web:3", DateTimeOffset.UtcNow, true)
        };
    }

    private static McpServerOptions CreateOptions(string workspaceRoot)
    {
        return new McpServerOptions
        {
            Server = new ServerOptions
            {
                WorkspaceRoot = workspaceRoot,
                SolutionPath = "CanDoItAll.slnx"
            },
            DefaultApp = new DefaultAppOptions
            {
                ProjectPath = Path.Combine("src", "CanDoItAll.Web", "CanDoItAll.Web.csproj"),
                WorkingDirectory = Path.Combine("src", "CanDoItAll.Web")
            },
            Health = new HealthOptions
            {
                Enabled = true,
                Urls = ["https://localhost:7271/_dev/runtime"]
            },
            Build = new BuildOptions
            {
                DefaultTargetPath = "CanDoItAll.slnx"
            },
            Tests = new TestOptions
            {
                DefaultTargetPath = Path.Combine("tests", "CanDoItAll.Mcp.DotNetWatch.Tests", "CanDoItAll.Mcp.DotNetWatch.Tests.csproj")
            },
            Logs = new LogOptions
            {
                Folder = ".mcp-state/logs"
            },
            Process = new ProcessOptions
            {
                RegistryPath = ".mcp-state/process-registry.json"
            },
            Security = new SecurityOptions
            {
                AllowedProjectRoots = ["src", "tests", "tools"]
            }
        };
    }

    private static TemporaryWorkspace CreateWorkspace()
    {
        var workspace = new TemporaryWorkspace();
        workspace.WriteFile("CanDoItAll.slnx", "<Solution />");
        workspace.WriteFile(Path.Combine("src", "CanDoItAll.Web", "CanDoItAll.Web.csproj"), "<Project />");
        return workspace;
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        public TemporaryWorkspace()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "CanDoItAll.Mcp.DotNetWatch.BundleTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }

        public void WriteFile(string relativePath, string content)
        {
            var fullPath = Path.Combine(RootPath, relativePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, content);
        }
    }
}
