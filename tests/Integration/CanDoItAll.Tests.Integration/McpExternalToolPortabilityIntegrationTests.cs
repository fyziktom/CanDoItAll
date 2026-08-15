using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Mcp;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Tools;
using CanDoItAll.AgentFramework.Tools.Abstractions;
using CanDoItAll.Infrastructure;
using CanDoItAll.McpTestHost;
using CanDoItAll.Modules.AgentFramework;
using System.Diagnostics;

namespace CanDoItAll.Tests.Integration.AgentFramework;

[Collection("B04 environment")]
[Trait("Category", "UnixRuntimePortability")]
public sealed class McpExternalToolPortabilityIntegrationTests
{
    private const string SecretSentinel = "B04_RUNTIME_SECRET_SENTINEL_7f2b18";
    private const string SecretSourceName = "CANDOITALL_B04_TEST_SECRET_SOURCE";

    [Fact]
    [Trait("Category", "McpPortability")]
    public async Task Local_stdio_MCP_runs_through_owned_duplex_host_and_cleans_up()
    {
        using var workspace = new TemporaryDirectory();
        var factory = CreateMcpFactory(workspace.Path);
        var descriptor = CreateMcpDescriptor(workspace.Path, arguments: ["", " value ", " "]);
        var previousSecret = Environment.GetEnvironmentVariable(SecretSourceName);
        Environment.SetEnvironmentVariable(SecretSourceName, SecretSentinel);
        try
        {
            var client = await factory.CreateAsync(
                descriptor,
                "mcp-portability",
                CancellationToken.None);
            try
            {
                await client.StartAsync(CancellationToken.None);
                var tools = await client.ListToolsAsync(CancellationToken.None);
                var result = await client.CallToolAsync(
                    McpToolName.Create("echo"),
                    "{\"value\":\"portable\"}",
                    CancellationToken.None);

                Assert.Equal("echo", Assert.Single(tools).Name.Value);
                Assert.Contains("\"ok\":true", result, StringComparison.Ordinal);
                Assert.Contains("\"secretPresent\":true", result, StringComparison.Ordinal);
                Assert.Contains("\"arguments\":[\"\",\" value \",\" \"]", result, StringComparison.Ordinal);
                Assert.DoesNotContain(SecretSentinel, result, StringComparison.Ordinal);

                var argumentsException = await Assert.ThrowsAsync<McpSetupException>(() =>
                    client.CallToolAsync(
                        McpToolName.Create("echo"),
                        "[]",
                        CancellationToken.None));
                Assert.Equal(CapabilityDiagnosticCategory.JsonParse, argumentsException.Category);
            }
            finally
            {
                await client.StopAsync(CancellationToken.None);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(SecretSourceName, previousSecret);
        }
    }

    [Theory]
    [InlineData("--invalid-list", CapabilityDiagnosticCategory.McpListTools)]
    [InlineData("--scalar-list", CapabilityDiagnosticCategory.McpListTools)]
    [InlineData("--array-list", CapabilityDiagnosticCategory.McpListTools)]
    [InlineData("--malformed-tool", CapabilityDiagnosticCategory.McpListTools)]
    [InlineData("--missing-jsonrpc-list", CapabilityDiagnosticCategory.McpListTools)]
    [InlineData("--hang-list", CapabilityDiagnosticCategory.McpListTools)]
    [InlineData("--missing-initialize-result", CapabilityDiagnosticCategory.McpHandshake)]
    [InlineData("--unsupported-protocol", CapabilityDiagnosticCategory.McpHandshake)]
    [Trait("Category", "McpPortability")]
    public async Task Local_stdio_MCP_bounds_failure_and_withholds_secret_output(
        string mode,
        CapabilityDiagnosticCategory expectedCategory)
    {
        using var workspace = new TemporaryDirectory();
        var descriptor = CreateMcpDescriptor(
            workspace.Path,
            [mode],
            timeout: TimeSpan.FromMilliseconds(400));
        var setup = new McpSetupTestService(CreateMcpFactory(workspace.Path));

        var previousSecret = Environment.GetEnvironmentVariable(SecretSourceName);
        Environment.SetEnvironmentVariable(SecretSourceName, SecretSentinel);
        try
        {
            var result = await setup.TestAsync(
                descriptor,
                "mcp-portability-failure",
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.True(result.CleanupCompleted);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Category == expectedCategory ||
                mode == "--hang-list" && diagnostic.Category == CapabilityDiagnosticCategory.Timeout);
            Assert.DoesNotContain(
                SecretSentinel,
                string.Join(" ", result.Diagnostics.Select(diagnostic => diagnostic.MaskedDetail)),
                StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(SecretSourceName, previousSecret);
        }
    }

    [Theory]
    [InlineData("--missing-call-result", "$.tools.call")]
    [InlineData("--scalar-call-result", "$.tools.call.result")]
    [Trait("Category", "McpPortability")]
    public async Task Local_stdio_MCP_rejects_an_invalid_call_response(
        string mode,
        string expectedFieldPath)
    {
        using var workspace = new TemporaryDirectory();
        var factory = CreateMcpFactory(workspace.Path);
        var descriptor = CreateMcpDescriptor(
            workspace.Path,
            [mode]);
        var client = await factory.CreateAsync(
            descriptor,
            "mcp-portability-invalid-call",
            CancellationToken.None);
        var previousSecret = Environment.GetEnvironmentVariable(SecretSourceName);
        Environment.SetEnvironmentVariable(SecretSourceName, SecretSentinel);
        try
        {
            await client.StartAsync(CancellationToken.None);
            await client.ListToolsAsync(CancellationToken.None);

            var exception = await Assert.ThrowsAsync<McpSetupException>(() =>
                client.CallToolAsync(
                    McpToolName.Create("echo"),
                    "{}",
                    CancellationToken.None));

            Assert.Equal(CapabilityDiagnosticCategory.RuntimeAdapter, exception.Category);
            Assert.Equal(expectedFieldPath, exception.FieldPath);
        }
        finally
        {
            await client.StopAsync(CancellationToken.None);
            Environment.SetEnvironmentVariable(SecretSourceName, previousSecret);
        }
    }

    [Fact]
    [Trait("Category", "McpPortability")]
    public async Task Local_stdio_MCP_caller_cancellation_cleans_up_the_actual_child()
    {
        using var workspace = new TemporaryDirectory();
        var pidFile = Path.Combine(workspace.Path, "mcp.pid");
        var readyFile = Path.Combine(workspace.Path, "mcp.ready");
        var descriptor = CreateMcpDescriptor(
            workspace.Path,
            ["--hang-list", "--pid-file", pidFile, "--ready-file", readyFile],
            timeout: TimeSpan.FromSeconds(30));
        var setup = new McpSetupTestService(CreateMcpFactory(workspace.Path));
        using var cancellation = new CancellationTokenSource();
        var previousSecret = Environment.GetEnvironmentVariable(SecretSourceName);
        Environment.SetEnvironmentVariable(SecretSourceName, SecretSentinel);
        try
        {
            var setupTask = setup.TestAsync(
                descriptor,
                "mcp-portability-cancellation",
                cancellation.Token);
            var readinessTask = WaitForProcessReadyAsync(pidFile, readyFile);
            if (await Task.WhenAny(setupTask, readinessTask) == setupTask)
            {
                var earlyResult = await setupTask;
                Assert.Fail(
                    "MCP setup completed before its deterministic child signaled readiness: " +
                    string.Join(" | ", earlyResult.Diagnostics.Select(diagnostic =>
                        $"{diagnostic.Category}:{diagnostic.FieldPath}:{diagnostic.MaskedDetail}")));
            }

            var processId = await readinessTask;
            cancellation.Cancel();
            var result = await setupTask;

            Assert.False(result.IsSuccess);
            Assert.True(result.CleanupCompleted);
            Assert.Contains(result.Diagnostics, diagnostic =>
                diagnostic.Category == CapabilityDiagnosticCategory.Cancellation);
            await AssertProcessExitedAsync(processId);
        }
        finally
        {
            Environment.SetEnvironmentVariable(SecretSourceName, previousSecret);
        }
    }

    [Fact]
    [Trait("Category", "McpPortability")]
    public async Task Local_stdio_MCP_answers_peer_ping_before_list_and_call_responses()
    {
        using var workspace = new TemporaryDirectory();
        var client = await CreateMcpFactory(workspace.Path).CreateAsync(
            CreateMcpDescriptor(
                workspace.Path,
                ["--ping-before-response"],
                bindSecret: false),
            "mcp-peer-ping",
            CancellationToken.None);
        try
        {
            await client.StartAsync(CancellationToken.None);
            var tools = await client.ListToolsAsync(CancellationToken.None);
            var result = await client.CallToolAsync(
                McpToolName.Create("echo"),
                "{}",
                CancellationToken.None);

            Assert.Equal("echo", Assert.Single(tools).Name.Value);
            Assert.Contains("\"ok\":true", result, StringComparison.Ordinal);
        }
        finally
        {
            await client.StopAsync(CancellationToken.None);
        }
    }

    [Theory]
    [InlineData("--unsupported-peer-request")]
    [InlineData("--notification-before-list")]
    [InlineData("--exit-after-list")]
    [Trait("Category", "McpPortability")]
    public async Task Local_stdio_MCP_handles_bounded_peer_control_before_the_list_response(
        string mode)
    {
        using var workspace = new TemporaryDirectory();
        var client = await CreateMcpFactory(workspace.Path).CreateAsync(
            CreateMcpDescriptor(workspace.Path, [mode], bindSecret: false),
            "mcp-peer-control",
            CancellationToken.None);
        try
        {
            await client.StartAsync(CancellationToken.None);
            var tools = await client.ListToolsAsync(CancellationToken.None);

            Assert.Equal("echo", Assert.Single(tools).Name.Value);
        }
        finally
        {
            await client.StopAsync(CancellationToken.None);
        }
    }

    [Theory]
    [InlineData("--excessive-unmatched", McpTransportFailureKind.ExcessiveUnmatchedMessages)]
    [InlineData("--overlong-list", McpTransportFailureKind.MessageTooLarge)]
    [InlineData("--deep-list", McpTransportFailureKind.InvalidJson)]
    [InlineData("--duplicate-peer-id", McpTransportFailureKind.DuplicateMessageId)]
    [InlineData("--invalid-peer-id", McpTransportFailureKind.InvalidMessageId)]
    [InlineData("--duplicate-id-property", McpTransportFailureKind.DuplicateMessageId)]
    [Trait("Category", "McpPortability")]
    public async Task Local_stdio_MCP_rejects_bounded_or_invalid_peer_traffic(
        string mode,
        McpTransportFailureKind expectedFailure)
    {
        using var workspace = new TemporaryDirectory();
        var client = await CreateMcpFactory(workspace.Path).CreateAsync(
            CreateMcpDescriptor(
                workspace.Path,
                [mode],
                timeout: TimeSpan.FromSeconds(20),
                bindSecret: false),
            "mcp-peer-adversarial",
            CancellationToken.None);
        try
        {
            await client.StartAsync(CancellationToken.None);
            var exception = await Assert.ThrowsAsync<McpSetupException>(() =>
                client.ListToolsAsync(CancellationToken.None));

            Assert.Equal(expectedFailure, exception.TransportFailure);
        }
        finally
        {
            await client.StopAsync(CancellationToken.None);
        }
    }

    [Theory]
    [InlineData("--exit-before-list")]
    [InlineData("--stderr-exit-before-list")]
    [Trait("Category", "McpPortability")]
    public async Task Local_stdio_MCP_reports_redacted_typed_transport_exit(string mode)
    {
        using var workspace = new TemporaryDirectory();
        var client = await CreateMcpFactory(workspace.Path).CreateAsync(
            CreateMcpDescriptor(workspace.Path, [mode], bindSecret: false),
            "mcp-peer-exit",
            CancellationToken.None);
        try
        {
            await client.StartAsync(CancellationToken.None);
            var exception = await Assert.ThrowsAsync<McpSetupException>(() =>
                client.ListToolsAsync(CancellationToken.None));

            Assert.Contains(
                exception.TransportFailure,
                new McpTransportFailureKind?[]
                {
                    McpTransportFailureKind.EndOfStream,
                    McpTransportFailureKind.ProcessExited
                });
            Assert.DoesNotContain(
                "stdio-secret-that-must-not-leak",
                exception.Detail,
                StringComparison.Ordinal);
        }
        finally
        {
            await client.StopAsync(CancellationToken.None);
        }
    }

    [Theory]
    [InlineData("--external-json", true)]
    [InlineData("--external-invalid", false)]
    [InlineData("--external-fail", false)]
    [Trait("Category", "ExternalToolPortability")]
    public async Task External_JSON_tool_reuses_canonical_host_and_never_discloses_output_secret(
        string mode,
        bool expectedSuccess)
    {
        using var workspace = new TemporaryDirectory();
        var runner = new WorkspaceExternalProcessRunner(
            new LocalWorkspaceProcessHost(),
            new WorkspacePathResolutionService(
                workspace.Path,
                new PhysicalFileSystemPathPolicyFactory()));
        var invoker = new ExternalProcessToolInvoker(runner);
        var descriptor = ToolDescriptorFactory.ExternalProcess(
            CapabilityKey.Create("portable-json-tool"),
            RuntimeToolName.Create("portable_json_tool"),
            ImplementationKey.Create("portable.json-tool"),
            executablePath: "dotnet",
            arguments: [typeof(McpTestHostMarker).Assembly.Location, mode, "", " value ", " "],
            workingDirectory: workspace.Path,
            timeout: TimeSpan.FromSeconds(5),
            maxOutputBytes: 4096,
            allowedExecutableNames: ["dotnet"],
            requiredOutputProperties: ["ok"]);
        var result = await invoker.InvokeAsync(
                descriptor,
                ToolInvocationRequest.Create(
                    descriptor.Identity,
                    descriptor.ImplementationKey,
                    $"{{\"input\":\"{SecretSentinel}\"}}",
                    "external-portability"),
                CancellationToken.None);

        Assert.Equal(expectedSuccess, result.IsSuccess);
        if (expectedSuccess)
        {
            Assert.Equal(
                [mode, "", " value ", " "],
                result.Output.GetProperty("arguments")
                    .EnumerateArray()
                    .Select(item => item.GetString() ?? throw new InvalidOperationException("The test host returned a non-string argument."))
                    .ToArray());
        }
        Assert.DoesNotContain(
            SecretSentinel,
            string.Join(" ", result.Diagnostics.Select(diagnostic => diagnostic.MaskedDetail)),
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "ExternalToolPortability")]
    public async Task External_JSON_tool_caller_cancellation_cleans_up_the_actual_child()
    {
        using var workspace = new TemporaryDirectory();
        var pidFile = Path.Combine(workspace.Path, "external.pid");
        var readyFile = Path.Combine(workspace.Path, "external.ready");
        var runner = new WorkspaceExternalProcessRunner(
            new LocalWorkspaceProcessHost(),
            new WorkspacePathResolutionService(
                workspace.Path,
                new PhysicalFileSystemPathPolicyFactory()));
        var invoker = new ExternalProcessToolInvoker(runner);
        var descriptor = ToolDescriptorFactory.ExternalProcess(
            CapabilityKey.Create("portable-json-tool"),
            RuntimeToolName.Create("portable_json_tool"),
            ImplementationKey.Create("portable.json-tool"),
            executablePath: "dotnet",
            arguments:
            [
                typeof(McpTestHostMarker).Assembly.Location,
                "--external-hang",
                "--pid-file",
                pidFile,
                "--ready-file",
                readyFile
            ],
            workingDirectory: workspace.Path,
            timeout: TimeSpan.FromSeconds(30),
            maxOutputBytes: 4096,
            allowedExecutableNames: ["dotnet"],
            requiredOutputProperties: ["ok"]);
        using var cancellation = new CancellationTokenSource();

        var invocationTask = invoker.InvokeAsync(
            descriptor,
            ToolInvocationRequest.Create(
                descriptor.Identity,
                descriptor.ImplementationKey,
                "{}",
                "external-portability-cancellation"),
            cancellation.Token);
        var processId = await WaitForProcessReadyAsync(pidFile, readyFile);
        cancellation.Cancel();
        var result = await invocationTask;

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Category == CapabilityDiagnosticCategory.Cancellation);
        await AssertProcessExitedAsync(processId);
    }

    private static async Task<int> WaitForProcessReadyAsync(
        string pidFile,
        string readyFile)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!File.Exists(pidFile) || !File.Exists(readyFile))
        {
            await Task.Delay(20, timeout.Token);
        }

        return int.Parse(await File.ReadAllTextAsync(pidFile, timeout.Token));
    }

    private static async Task AssertProcessExitedAsync(int processId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    return;
                }
            }
            catch (ArgumentException)
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.Fail($"Process {processId} remained alive after cancellation cleanup.");
    }

    private static LocalStdioMcpClientFactory CreateMcpFactory(string workspaceRoot)
        => new(
            new LocalWorkspaceProcessHost(),
            new WorkspacePathResolutionService(
                workspaceRoot,
                new PhysicalFileSystemPathPolicyFactory()));

    private static LocalStdioMcpServerDescriptor CreateMcpDescriptor(
        string workspaceRoot,
        IReadOnlyList<string> arguments,
        TimeSpan? timeout = null,
        bool bindSecret = true)
    {
        return McpDescriptorFactory.LocalStdio(
            CapabilityKey.Create("portable-local-mcp"),
            McpServerKey.Create("portable-local-mcp"),
            "Portable Local MCP",
            "Deterministic local MCP portability test.",
            command: "dotnet",
            arguments: [typeof(McpTestHostMarker).Assembly.Location, ..arguments],
            workingDirectory: workspaceRoot,
            allowedWorkingDirectories: [],
            allowedTools: [McpToolName.Create("echo")],
            environmentVariableBindings: bindSecret
                ? new Dictionary<string, string>
                {
                    ["MCP_TEST_SECRET"] = SecretSourceName
                }
                : new Dictionary<string, string>(),
            rawEnvironmentVariables: new Dictionary<string, string>(),
            approvalMode: McpApprovalMode.AlwaysRequire,
            timeout: timeout ?? TimeSpan.FromSeconds(5),
            messageFraming: McpStdioMessageFraming.NewlineDelimitedJson);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"CanDoItAll.B04.{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }
}

[CollectionDefinition("B04 environment", DisableParallelization = true)]
public sealed class B04EnvironmentCollection;
