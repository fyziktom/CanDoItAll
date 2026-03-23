using System.Diagnostics;
using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Mcp.Core.Observability;
using CanDoItAll.Mcp.DotNetWatch.Backend;
using CanDoItAll.Mcp.DotNetWatch.Configuration;
using CanDoItAll.Mcp.DotNetWatch.Diagnostics;
using CanDoItAll.Mcp.DotNetWatch.Manager;
using CanDoItAll.Mcp.DotNetWatch.Operations;
using CanDoItAll.Mcp.DotNetWatch.Runtime;
using CanDoItAll.Mcp.DotNetWatch.Security;
using CanDoItAll.Mcp.LocalRuntime.Processes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

namespace CanDoItAll.Mcp.DotNetWatch.Tests;

public sealed class InfrastructureTests
{
    [Fact]
    public void Validator_Accepts_WellFormedOptions()
    {
        using var workspace = CreateWorkspace();
        var options = CreateOptions(workspace.RootPath);
        var validator = new McpServerOptionsValidator();

        var result = validator.Validate(name: null, options);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Failures ?? []));
    }

    [Fact]
    public void Validator_Rejects_InvalidSolutionPath()
    {
        using var workspace = CreateWorkspace();
        var options = CreateOptions(workspace.RootPath);
        options.Server.SolutionPath = "missing.slnx";

        var result = new McpServerOptionsValidator().Validate(name: null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, failure => failure.Contains("Solution path", StringComparison.Ordinal));
    }

    [Fact]
    public void PathGuard_Rejects_PathOutsideWorkspace()
    {
        using var workspace = CreateWorkspace();
        var configuration = new RuntimeConfiguration(Options.Create(CreateOptions(workspace.RootPath)));
        var pathGuard = new PathGuard(configuration);

        var exception = Assert.Throws<ToolInvocationException>(() => pathGuard.ResolveInsideWorkspace(Path.Combine(workspace.RootPath, "..", "outside.txt")));

        Assert.Equal("PathOutsideWorkspace", exception.Code);
    }

    [Fact]
    public void EnvironmentOverlayFilter_Rejects_DisallowedKeys()
    {
        using var workspace = CreateWorkspace();
        var configuration = new RuntimeConfiguration(Options.Create(CreateOptions(workspace.RootPath)));
        var filter = new EnvironmentOverlayFilter(configuration);

        var exception = Assert.Throws<ToolInvocationException>(() => filter.Merge(
            new Dictionary<string, string>(),
            new Dictionary<string, string?> { ["UNSAFE_KEY"] = "x" },
            includePollingWatcher: false));

        Assert.Equal("SecurityViolation", exception.Code);
    }

    [Fact]
    public void LogRedactor_Masks_KnownSecretPatterns()
    {
        using var workspace = CreateWorkspace();
        var configuration = new RuntimeConfiguration(Options.Create(CreateOptions(workspace.RootPath)));
        var redactor = new SecretRedactor(configuration.CreateSecretRedactionOptions());

        var redacted = redactor.Redact("password=supersecret apiKey:abc123");

        Assert.DoesNotContain("supersecret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", redacted, StringComparison.Ordinal);
        Assert.Contains("***redacted***", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void FileLogStore_Rotates_WhenMaxSizeIsExceeded()
    {
        using var workspace = CreateWorkspace();
        var options = CreateOptions(workspace.RootPath);
        options.Logs.MaxFileSizeMb = 1;
        var configuration = new RuntimeConfiguration(Options.Create(options));
        var store = new FileLogStore(configuration.CreateFileLogStoreOptions());
        var path = Path.Combine(configuration.LogFolder, "app-rotation.ndjson");

        File.WriteAllText(path, new string('x', (int)configuration.MaxLogFileSizeBytes));

        store.Append("app", "rotation", new LogEntry(1, DateTimeOffset.UtcNow, "System", null, 1, "corr_test", "rotated"));

        Assert.True(File.Exists($"{path}.1"));
        Assert.Contains("rotated", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void AppSession_Compatibility_RequiresMatchingTemplate()
    {
        var template = new AppStartTemplate(
            @"C:\repo\src\CanDoItAll.Web\CanDoItAll.Web.csproj",
            @"C:\repo\src\CanDoItAll.Web",
            AppRunMode.WatchRun,
            "Debug",
            null,
            "https",
            ["--flag"],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development"
            },
            ["https://localhost:7271"]);

        var session = new AppSession("app_test", template, "corr_test", new RingLogBuffer(128), healthEnabled: true);

        Assert.True(session.IsCompatible(template));

        var differentTemplate = template with
        {
            Arguments = ["--different"]
        };

        Assert.False(session.IsCompatible(differentTemplate));
    }

    [Fact]
    public void StartFailureDiagnoser_Classifies_PortInUse()
    {
        var buffer = new RingLogBuffer(32);
        var correlationId = "corr_test";
        buffer.Append("ProcessStdErr", "stderr", sessionVersion: 1, correlationId, "Failed to bind to address https://127.0.0.1:7271: address already in use");

        var operation = new OperationRecord(
            "op_test",
            OperationType.Build,
            correlationId,
            @"C:\repo\CanDoItAll.slnx",
            framework: null,
            configuration: "Debug",
            WhenAppRunningPolicy.StopAndResume,
            affectedSessionIds: [],
            runner: null,
            buffer,
            TimeSpan.FromMinutes(5));

        var result = new StartFailureDiagnoser().Diagnose(session: null, operation, maxLogEntries: 10);

        Assert.Equal(DiagnosticCategory.PortInUse, result.Category);
        Assert.Contains(result.RecommendedActions, action => action.Contains("cleanup_stale_processes", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WorkspaceExecutionLock_FailsFast_WithActionableHolder()
    {
        var executionLock = new WorkspaceExecutionLock();
        await using var lease = await executionLock.AcquireMutationAsync("build:op_123", CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ToolInvocationException>(() => executionLock.AcquireMutationAsync("app-start", CancellationToken.None));

        Assert.Equal("OperationInProgress", exception.Code);
        Assert.Contains("build:op_123", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnixProcessTreeTerminator_KillsDescendants_FromDeepestToRoot()
    {
        using var process = Process.GetCurrentProcess();
        var pid = process.Id;
        var runner = new RecordingCommandRunner(command =>
        {
            if (command == $"pgrep -P {pid}")
            {
                return "20\n30\n";
            }

            if (command == "pgrep -P 20")
            {
                return "40\n";
            }

            return string.Empty;
        });
        var terminator = new UnixProcessTreeTerminator(runner, NullLogger<UnixProcessTreeTerminator>.Instance);

        var result = await terminator.KillTreeAsync(process, CancellationToken.None);

        Assert.Equal(new[] { pid, 20, 40, 30 }, result);
        Assert.Equal(
            new[]
            {
                $"pgrep -P {pid}",
                "pgrep -P 20",
                "pgrep -P 40",
                "pgrep -P 30",
                "kill -KILL 30",
                "kill -KILL 40",
                "kill -KILL 20",
                $"kill -KILL {pid}"
            },
            runner.Commands);
    }

    [Fact]
    public async Task WindowsProcessTreeTerminator_UsesTaskkillForce()
    {
        using var process = Process.GetCurrentProcess();
        var pid = process.Id;
        var runner = new RecordingCommandRunner(command =>
            command.StartsWith("powershell -NoProfile -NonInteractive -Command", StringComparison.Ordinal)
                ? $"[{pid},{pid + 1}]"
                : string.Empty);
        var terminator = new WindowsProcessTreeTerminator(runner, NullLogger<WindowsProcessTreeTerminator>.Instance);

        var result = await terminator.KillTreeAsync(process, CancellationToken.None);

        Assert.Equal(new[] { pid, pid + 1 }, result);
        Assert.Contains($"taskkill /PID {pid} /T /F", runner.Commands);
    }

    [Fact]
    public void AgentLogReducer_SuppressesWarningsAndRestoreNoise_ButKeepsOutcomeLines()
    {
        var reducer = new AgentLogReducer();
        var result = reducer.Reduce(
            [
                CreateLogEntry(1, "  Determining projects to restore..."),
                CreateLogEntry(2, @"C:\repo\App.csproj : warning NU1510: PackageReference Microsoft.Extensions.Hosting will not be pruned."),
                CreateLogEntry(3, @"C:\repo\Program.cs(42,11): warning CS8602: Dereference of a possibly null reference."),
                CreateLogEntry(4, "Build succeeded."),
                CreateLogEntry(5, "    2 Warning(s)"),
                CreateLogEntry(6, "    0 Error(s)")
            ],
            startCursor: 0,
            limit: 50,
            scenario: LogReductionScenario.Operation,
            view: LogViewMode.AgentOptimized);

        Assert.Collection(
            result.Entries,
            entry => Assert.Equal("Build succeeded.", entry.Text),
            entry => Assert.Equal("    2 Warning(s)", entry.Text),
            entry => Assert.Equal("    0 Error(s)", entry.Text));
        Assert.Equal(3, result.FilterSummary.SuppressedEntryCount);
        Assert.Contains(result.FilterSummary.Notes, note => note.Contains("compiler/NuGet warning lines", StringComparison.Ordinal));
        Assert.Contains(result.FilterSummary.Notes, note => note.Contains("restore/build progress lines", StringComparison.Ordinal));
    }

    [Fact]
    public void AgentLogReducer_SuppressesFrameworkHttpNoise_ButKeepsAppWarnings()
    {
        var reducer = new AgentLogReducer();
        var result = reducer.Reduce(
            [
                CreateLogEntry(1, "info: System.Net.Http.HttpClient.Default.LogicalHandler[100]"),
                CreateLogEntry(2, "      Start processing HTTP request GET https://example.test/api"),
                CreateLogEntry(3, "warn: PVEInvoicing[0]"),
                CreateLogEntry(4, "      Mail provider returned 404 for a simulated lookup.")
            ],
            startCursor: 0,
            limit: 50,
            scenario: LogReductionScenario.App,
            view: LogViewMode.AgentOptimized);

        Assert.Collection(
            result.Entries,
            entry => Assert.Equal("warn: PVEInvoicing[0]", entry.Text),
            entry => Assert.Equal("      Mail provider returned 404 for a simulated lookup.", entry.Text));
        Assert.Equal(2, result.FilterSummary.SuppressedEntryCount);
        Assert.Contains(result.FilterSummary.Notes, note => note.Contains("framework HTTP trace lines", StringComparison.Ordinal));
    }

    [Fact]
    public void AgentLogReducer_SuppressesEntityFrameworkAndDebugTraceNoise()
    {
        var reducer = new AgentLogReducer();
        var result = reducer.Reduce(
            [
                CreateLogEntry(1, "dbug: Microsoft.Extensions.Hosting.Internal.Host[1]"),
                CreateLogEntry(2, "      Hosting debug trace."),
                CreateLogEntry(3, "info: Microsoft.EntityFrameworkCore.Update[30100]"),
                CreateLogEntry(4, "      Saved 2 entities to in-memory store."),
                CreateLogEntry(5, "warn: PVEInvoicing[0]"),
                CreateLogEntry(6, "      Mail provider returned 404 for a simulated lookup.")
            ],
            startCursor: 0,
            limit: 50,
            scenario: LogReductionScenario.App,
            view: LogViewMode.AgentOptimized);

        Assert.Collection(
            result.Entries,
            entry => Assert.Equal("warn: PVEInvoicing[0]", entry.Text),
            entry => Assert.Equal("      Mail provider returned 404 for a simulated lookup.", entry.Text));
        Assert.Equal(4, result.FilterSummary.SuppressedEntryCount);
        Assert.Contains(result.FilterSummary.Notes, note => note.Contains("Entity Framework", StringComparison.Ordinal));
        Assert.Contains(result.FilterSummary.Notes, note => note.Contains("debug/trace log lines", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildManagedApplicationArguments_AddsConfiguredUrls_WithoutDuplicatingExplicitOverride()
    {
        var template = new AppStartTemplate(
            @"C:\repo\App.csproj",
            @"C:\repo",
            AppRunMode.WatchRun,
            "Debug",
            Framework: null,
            LaunchProfile: "https",
            Arguments: ["--flag"],
            EnvironmentOverlay: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Urls: ["https://localhost:7367", "http://localhost:5239"]);

        var arguments = AppRuntimeManager.BuildManagedApplicationArguments(template, ["--CanDoItAllMcpOwnerKind=app"]);

        Assert.Equal("--CanDoItAllMcpOwnerKind=app", arguments[0]);
        Assert.Contains("--urls", arguments);
        Assert.Contains("https://localhost:7367;http://localhost:5239", arguments);
        Assert.Contains("--flag", arguments);

        var explicitUrlsTemplate = template with
        {
            Arguments = ["--urls", "https://localhost:9001"]
        };

        var explicitArguments = AppRuntimeManager.BuildManagedApplicationArguments(explicitUrlsTemplate, []);
        Assert.Equal(2, explicitArguments.Count);
        Assert.Equal("--urls", explicitArguments[0]);
        Assert.Equal("https://localhost:9001", explicitArguments[1]);
    }

    [Fact]
    public void BuildManagedArtifactsRoot_UsesStableTemplateCache()
    {
        var template = new AppStartTemplate(
            @"C:\repo\App.csproj",
            @"C:\repo",
            AppRunMode.WatchRun,
            "Debug",
            Framework: "net10.0",
            LaunchProfile: "https",
            Arguments: [],
            EnvironmentOverlay: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Urls: []);

        var artifactsRoot = AppRuntimeManager.BuildManagedArtifactsRoot(@"C:\repo", template);

        Assert.StartsWith(@"C:\repo\.mcp-state\artifacts\app-projects\", artifactsRoot, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("app-", artifactsRoot, StringComparison.OrdinalIgnoreCase);

        var sameTemplateRoot = AppRuntimeManager.BuildManagedArtifactsRoot(@"C:\repo", template);
        Assert.Equal(artifactsRoot, sameTemplateRoot);

        var differentConfigurationRoot = AppRuntimeManager.BuildManagedArtifactsRoot(
            @"C:\repo",
            template with
            {
                Configuration = "Release"
            });

        Assert.NotEqual(artifactsRoot, differentConfigurationRoot);
    }

    [Fact]
    public void BuildManagedProcessArguments_UsesArtifactsCache_ForWatchRun()
    {
        var template = new AppStartTemplate(
            @"C:\repo\App.csproj",
            @"C:\repo",
            AppRunMode.WatchRun,
            "Debug",
            Framework: "net10.0",
            LaunchProfile: "https",
            Arguments: [],
            EnvironmentOverlay: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Urls: []);

        var arguments = AppRuntimeManager.BuildManagedProcessArguments(
            template,
            ["--CanDoItAllMcpOwnerKind=app", "--urls", "https://localhost:7367"],
            @"C:\repo\.mcp-state\artifacts\app-projects\app-12345678");

        Assert.Equal("watch", arguments[0]);
        Assert.Contains("--non-interactive", arguments);
        Assert.Contains("--artifacts-path", arguments);
        Assert.Contains(@"C:\repo\.mcp-state\artifacts\app-projects\app-12345678", arguments);
        Assert.Contains("--property:UseAppHost=false", arguments);
        Assert.Contains("--framework", arguments);
        Assert.Contains("net10.0", arguments);
        Assert.Contains("--launch-profile", arguments);
        Assert.Contains("https", arguments);
        Assert.Equal("--", arguments[^4]);
        Assert.Equal("--CanDoItAllMcpOwnerKind=app", arguments[^3]);
        Assert.Equal("--urls", arguments[^2]);
        Assert.Equal("https://localhost:7367", arguments[^1]);
    }

    [Fact]
    public void BuildManagedProcessArguments_UsesArtifactsCache_ForRunOnce()
    {
        var template = new AppStartTemplate(
            @"C:\repo\App.csproj",
            @"C:\repo",
            AppRunMode.RunOnce,
            "Release",
            Framework: null,
            LaunchProfile: null,
            Arguments: [],
            EnvironmentOverlay: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Urls: []);

        var arguments = AppRuntimeManager.BuildManagedProcessArguments(
            template,
            ["--CanDoItAllMcpOwnerKind=app"],
            @"C:\repo\.mcp-state\artifacts\app-projects\app-87654321");

        Assert.Equal("run", arguments[0]);
        Assert.Contains("--artifacts-path", arguments);
        Assert.Contains(@"C:\repo\.mcp-state\artifacts\app-projects\app-87654321", arguments);
        Assert.Contains("--property:UseAppHost=false", arguments);
        Assert.DoesNotContain("--non-interactive", arguments);
    }

    [Fact]
    public void BackendDashboardPage_RendersAggregateBackends_WithStringEnums_AndForceRebuildControl()
    {
        var identity = new BackendIdentitySnapshot("CanDoItAll.Mcp.DotNetWatch", @"C:\repo\one", @"C:\repo\one\settings.json", "hash-1", "bin-1");
        var otherIdentity = identity with { WorkspaceRoot = @"C:\repo\two", SettingsPath = @"C:\repo\two\settings.json", SettingsHash = "hash-2" };
        var session = new AppStatusData(
            "app_1",
            "corr_1",
            AppLifecycleState.Restarting,
            AppRunMode.WatchRun,
            @"C:\repo\one\App.csproj",
            3,
            1234,
            ["https://localhost:7411"],
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            42,
            new HealthData("Pending", null, DateTimeOffset.UtcNow, "https://localhost:7411/_dev/runtime", "Restarting", false, null, null),
            ["Restart requested."],
            new WatchStatusData(WatchProcessingState.Building, "Building", true, 1234, null, 2, 1, HotReloadOutcome.RestartRequired, 42, DateTimeOffset.UtcNow));

        var status = new BackendManagerStatusResponse(
            identity,
            "backend_1",
            1001,
            DateTimeOffset.UtcNow,
            "http://127.0.0.1:5001",
            "http://127.0.0.1:5001/?token=test",
            [session],
            [],
            [],
            [
                new ManagedBackendStatusData(identity, "backend_1", 1001, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "http://127.0.0.1:5001", "http://127.0.0.1:5001/?token=test", true, true, null, [session], [], [], DateTimeOffset.UtcNow),
                new ManagedBackendStatusData(otherIdentity, "backend_2", 1002, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "http://127.0.0.1:5002", "http://127.0.0.1:5002/?token=test", false, true, null, [], [], [], DateTimeOffset.UtcNow)
            ],
            2,
            1,
            0,
            DateTimeOffset.UtcNow);

        var html = BackendDashboardPage.Render(status);

        Assert.Contains(@"""state"":""Restarting""", html, StringComparison.Ordinal);
        Assert.Contains(@"""mode"":""WatchRun""", html, StringComparison.Ordinal);
        Assert.Contains(@"C:\\repo\\two", html, StringComparison.Ordinal);
        Assert.Contains("Force Rebuild", html, StringComparison.Ordinal);
        Assert.Contains("Backend PID", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BackendManagerService_RemoteAction_IsReportedAsProxied()
    {
        using var workspace = CreateWorkspace();
        var configuration = new RuntimeConfiguration(Options.Create(CreateOptions(workspace.RootPath)));
        var catalog = new GlobalBackendCatalogStore(configuration, NullLogger<GlobalBackendCatalogStore>.Instance);
        var identity = new BackendIdentitySnapshot("CanDoItAll.Mcp.DotNetWatch", @"C:\repo\remote", @"C:\repo\remote\settings.json", "hash-remote", "bin-remote");
        var remoteRegistration = new BackendRegistrationRecord(
            "backend_remote",
            Environment.ProcessId,
            Process.GetCurrentProcess().StartTime.ToUniversalTime(),
            DateTimeOffset.UtcNow,
            "http://127.0.0.1:5015",
            "http://127.0.0.1:5015/?token=test",
            "test-token",
            identity);

        await catalog.UpsertAsync(remoteRegistration, CancellationToken.None);

        var httpClientFactory = new StaticHttpClientFactory(new HttpClient(new StaticHttpMessageHandler(_ =>
        {
            var payload = new BackendManagerActionResponse(
                Success: true,
                BackendId: "backend_remote",
                Action: BackendManagerActionKind.RebuildSession,
                Message: "Remote rebuild requested.",
                SessionId: "app_remote",
                OperationId: null,
                Proxied: false,
                TimestampUtc: DateTimeOffset.UtcNow);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(payload)
            };
        })));

        var service = new BackendManagerService(
            identityProvider: null!,
            coordinator: null!,
            catalogStore: catalog,
            httpClientFactory,
            NullLogger<BackendManagerService>.Instance);

        var result = await service.ExecuteManagerActionAsync(
            new BackendManagerActionRequest("backend_remote", BackendManagerActionKind.RebuildSession, SessionId: "app_remote"),
            currentRegistration: new BackendRegistrationRecord(
                "backend_current",
                Environment.ProcessId,
                Process.GetCurrentProcess().StartTime.ToUniversalTime(),
                DateTimeOffset.UtcNow,
                "http://127.0.0.1:5001",
                "http://127.0.0.1:5001/?token=current",
                "current-token",
                identity with { WorkspaceRoot = workspace.RootPath, SettingsPath = Path.Combine(workspace.RootPath, "settings.json") }),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Proxied);
        Assert.Equal("app_remote", result.SessionId);
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
                DefaultTargetPath = Path.Combine("src", "CanDoItAll.Web", "CanDoItAll.Web.csproj")
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

    private sealed class RecordingCommandRunner(Func<string, string> captureOutputFactory) : IProcessCommandRunner
    {
        public List<string> Commands { get; } = [];

        public Task<int> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            Commands.Add($"{fileName} {string.Join(' ', arguments)}");
            return Task.FromResult(0);
        }

        public Task<string> RunCaptureAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            var key = $"{fileName} {string.Join(' ', arguments)}";
            Commands.Add(key);
            return Task.FromResult(captureOutputFactory(key));
        }
    }

    private sealed class StaticHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StaticHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        public TemporaryWorkspace()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "CanDoItAll.Mcp.DotNetWatch.Tests", Guid.NewGuid().ToString("N"));
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
