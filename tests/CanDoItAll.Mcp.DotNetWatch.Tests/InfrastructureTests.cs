using CanDoItAll.Mcp.DotNetWatch.Configuration;
using CanDoItAll.Mcp.DotNetWatch.Diagnostics;
using CanDoItAll.Mcp.DotNetWatch.Logging;
using CanDoItAll.Mcp.DotNetWatch.Operations;
using CanDoItAll.Mcp.DotNetWatch.Runtime;
using CanDoItAll.Mcp.DotNetWatch.Security;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.DotNetWatch.Tests;

public sealed class InfrastructureTests
{
    [Fact]
    public void Validator_Accepts_WellFormedOptions()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("CanDoItAll.slnx", "<Solution />");
        workspace.WriteFile(Path.Combine("src", "CanDoItAll.Web", "CanDoItAll.Web.csproj"), "<Project />");

        var options = CreateOptions(workspace.RootPath);
        var validator = new McpServerOptionsValidator();

        var result = validator.Validate(name: null, options);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Failures ?? []));
    }

    [Fact]
    public void PathGuard_Rejects_PathOutsideWorkspace()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("CanDoItAll.slnx", "<Solution />");
        workspace.WriteFile(Path.Combine("src", "CanDoItAll.Web", "CanDoItAll.Web.csproj"), "<Project />");

        var configuration = new RuntimeConfiguration(Options.Create(CreateOptions(workspace.RootPath)));
        var pathGuard = new PathGuard(configuration);

        var exception = Assert.Throws<ToolInvocationException>(() => pathGuard.ResolveInsideWorkspace(Path.Combine(workspace.RootPath, "..", "outside.txt")));

        Assert.Equal("PathOutsideWorkspace", exception.Code);
    }

    [Fact]
    public void EnvironmentOverlayFilter_Rejects_DisallowedKeys()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("CanDoItAll.slnx", "<Solution />");
        workspace.WriteFile(Path.Combine("src", "CanDoItAll.Web", "CanDoItAll.Web.csproj"), "<Project />");

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
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("CanDoItAll.slnx", "<Solution />");
        workspace.WriteFile(Path.Combine("src", "CanDoItAll.Web", "CanDoItAll.Web.csproj"), "<Project />");

        var configuration = new RuntimeConfiguration(Options.Create(CreateOptions(workspace.RootPath)));
        var redactor = new LogRedactor(configuration);

        var redacted = redactor.Redact("password=supersecret apiKey:abc123");

        Assert.DoesNotContain("supersecret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", redacted, StringComparison.Ordinal);
        Assert.Contains("***redacted***", redacted, StringComparison.Ordinal);
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

        var session = new AppSession("app_test", template, new RingLogBuffer(128));

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

        var diagnoser = new StartFailureDiagnoser();

        var result = diagnoser.Diagnose(session: null, operation, maxLogEntries: 10);

        Assert.Equal(DiagnosticCategory.PortInUse, result.Category);
        Assert.Contains(result.RecommendedActions, action => action.Contains("cleanup_stale_processes", StringComparison.Ordinal));
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
