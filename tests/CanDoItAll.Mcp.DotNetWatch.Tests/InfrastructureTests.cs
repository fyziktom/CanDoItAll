using System.Diagnostics;
using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Mcp.Core.Observability;
using CanDoItAll.Mcp.DotNetWatch.Configuration;
using CanDoItAll.Mcp.DotNetWatch.Diagnostics;
using CanDoItAll.Mcp.DotNetWatch.Operations;
using CanDoItAll.Mcp.DotNetWatch.Runtime;
using CanDoItAll.Mcp.DotNetWatch.Security;
using CanDoItAll.Mcp.LocalRuntime.Processes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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

        var session = new AppSession("app_test", template, "corr_test", new RingLogBuffer(128));

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
            affectedSessionId: null,
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
