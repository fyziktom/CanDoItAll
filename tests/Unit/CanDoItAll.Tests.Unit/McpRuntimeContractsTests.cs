using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Access;
using CanDoItAll.AgentFramework.Mcp;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using System.Text;
using System.Text.Json.Nodes;

namespace CanDoItAll.Tests.Unit;

public sealed class McpRuntimeContractsTests
{
    [Fact]
    public async Task SB04_INV_INTERNAL_001_internal_hosted_mcp_setup_uses_application_lifecycle_and_implementation_key()
    {
        var descriptor = McpDescriptorFactory.InternalHosted(
            CapabilityKey.Create("workspace-internal-mcp"),
            McpServerKey.Create("workspace-internal-mcp"),
            "Workspace Internal MCP",
            "Internal hosted workspace MCP.",
            ImplementationKey.Create("mcp.workspace.internal"),
            allowedTools: [McpToolName.Create("workspace_search")],
            approvalMode: McpApprovalMode.NeverRequire,
            timeout: TimeSpan.FromSeconds(5));
        var exposure = McpExposureDescriptorFactory.CreateServer(descriptor);
        var setup = new McpSetupTestService(new FakeMcpClientFactory(new FakeMcpServerScript(
            Tools:
            [
                new DiscoveredMcpTool(McpToolName.Create("workspace_search"), "Search workspace content.")
            ])));

        var result = await setup.TestAsync(descriptor, "SB04_INV_INTERNAL_001", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.CleanupCompleted);
        Assert.Equal(McpLifecycleOwner.Application, descriptor.LifecycleOwner);
        Assert.Equal(ImplementationKey.Create("mcp.workspace.internal"), exposure.ImplementationKey);
        Assert.Equal(McpServerDescriptorKind.InternalHosted, descriptor.DescriptorKind);
        Assert.Equal(CapabilitySideEffectKind.McpTool, descriptor.SideEffectProfile.Kind);
    }

    [Fact]
    public async Task SB04_INV_LOCAL_001_fake_local_mcp_setup_lists_allowed_tools_and_cleans_up()
    {
        var descriptor = McpDescriptorFactory.LocalStdio(
            CapabilityKey.Create("playwright-local-mcp"),
            McpServerKey.Create("playwright-local"),
            "Playwright Local MCP",
            "Local browser automation MCP.",
            command: "node",
            arguments: ["@playwright/mcp"],
            workingDirectory: ".",
            allowedWorkingDirectories: [],
            allowedTools: [McpToolName.Create("browser_snapshot")],
            environmentVariableBindings: new Dictionary<string, string>(),
            rawEnvironmentVariables: new Dictionary<string, string>(),
            approvalMode: McpApprovalMode.AlwaysRequire,
            timeout: TimeSpan.FromSeconds(5));
        var fakeFactory = new FakeMcpClientFactory(new FakeMcpServerScript(
            Tools:
            [
                new DiscoveredMcpTool(McpToolName.Create("browser_snapshot"), "Snapshot page state."),
                new DiscoveredMcpTool(McpToolName.Create("browser_click"), "Click a page element.")
            ]));
        var setup = new McpSetupTestService(fakeFactory);

        var result = await setup.TestAsync(descriptor, "SB04_INV_LOCAL_001", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.CleanupCompleted);
        Assert.Equal([McpToolName.Create("browser_snapshot")], result.AllowedTools.Select(tool => tool.Name));
        Assert.Equal(1, fakeFactory.CreatedClients);
        Assert.Equal(1, fakeFactory.LastClient!.StartCount);
        Assert.Equal(1, fakeFactory.LastClient.StopCount);
    }

    [Fact]
    public async Task SB04_INV_LOCAL_002_missing_allowed_tools_fails_before_start()
    {
        var descriptor = McpDescriptorFactory.LocalStdio(
            CapabilityKey.Create("playwright-local-mcp"),
            McpServerKey.Create("playwright-local"),
            "Playwright Local MCP",
            "Local browser automation MCP.",
            command: "node",
            arguments: ["@playwright/mcp"],
            workingDirectory: ".",
            allowedWorkingDirectories: [],
            allowedTools: [],
            environmentVariableBindings: new Dictionary<string, string>(),
            rawEnvironmentVariables: new Dictionary<string, string>(),
            approvalMode: McpApprovalMode.NeverRequire,
            timeout: TimeSpan.FromSeconds(5));
        var fakeFactory = new FakeMcpClientFactory(new FakeMcpServerScript(Tools: []));
        var setup = new McpSetupTestService(fakeFactory);

        var result = await setup.TestAsync(descriptor, "SB04_INV_LOCAL_002", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, fakeFactory.CreatedClients);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CapabilityDiagnosticCategory.TemplateValidation, diagnostic.Category);
        Assert.Equal("$.allowedTools", diagnostic.FieldPath);
    }

    [Fact]
    public async Task SB04_INV_LOCAL_003_disallowed_command_is_rejected_before_start()
    {
        var descriptor = McpDescriptorFactory.LocalStdio(
            CapabilityKey.Create("unsafe-local-mcp"),
            McpServerKey.Create("unsafe-local-mcp"),
            "Unsafe Local MCP",
            "Unsafe local MCP.",
            command: "cmd.exe",
            arguments: ["/c", "unsafe"],
            workingDirectory: ".",
            allowedWorkingDirectories: [],
            allowedTools: [McpToolName.Create("unsafe_tool")],
            environmentVariableBindings: new Dictionary<string, string>(),
            rawEnvironmentVariables: new Dictionary<string, string>(),
            approvalMode: McpApprovalMode.AlwaysRequire,
            timeout: TimeSpan.FromSeconds(5));
        var fakeFactory = new FakeMcpClientFactory(new FakeMcpServerScript(Tools: []));
        var setup = new McpSetupTestService(fakeFactory);

        var result = await setup.TestAsync(descriptor, "SB04_INV_LOCAL_003", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, fakeFactory.CreatedClients);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CapabilityDiagnosticCategory.CommandPolicy, diagnostic.Category);
        Assert.Contains("approved", diagnostic.RepairHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SB04_INV_SECRET_001_raw_environment_and_headers_are_rejected()
    {
        var localDescriptor = McpDescriptorFactory.LocalStdio(
            CapabilityKey.Create("raw-env-local-mcp"),
            McpServerKey.Create("raw-env-local-mcp"),
            "Raw Env MCP",
            "Raw environment variable test.",
            command: "node",
            arguments: [],
            workingDirectory: ".",
            allowedWorkingDirectories: [],
            allowedTools: [McpToolName.Create("env_tool")],
            environmentVariableBindings: new Dictionary<string, string>(),
            rawEnvironmentVariables: new Dictionary<string, string> { ["API_KEY"] = "raw-secret-value" },
            approvalMode: McpApprovalMode.AlwaysRequire,
            timeout: TimeSpan.FromSeconds(5));
        var remoteDescriptor = McpDescriptorFactory.RemoteHttp(
            CapabilityKey.Create("raw-header-remote-mcp"),
            McpServerKey.Create("raw-header-remote-mcp"),
            "Raw Header MCP",
            "Raw header test.",
            new Uri("https://example.test/mcp"),
            allowedTools: [McpToolName.Create("remote_tool")],
            headerBindings: new Dictionary<string, string>(),
            rawHeaders: new Dictionary<string, string> { ["Authorization"] = "Bearer raw-secret-value" },
            approvalMode: McpApprovalMode.NeverRequire,
            timeout: TimeSpan.FromSeconds(5));
        var setup = new McpSetupTestService(new FakeMcpClientFactory(new FakeMcpServerScript(Tools: [])));

        var local = await setup.TestAsync(localDescriptor, "SB04_INV_SECRET_001_LOCAL", CancellationToken.None);
        var remote = await setup.TestAsync(remoteDescriptor, "SB04_INV_SECRET_001_REMOTE", CancellationToken.None);

        Assert.False(local.IsSuccess);
        Assert.False(remote.IsSuccess);
        Assert.All(local.Diagnostics.Concat(remote.Diagnostics), diagnostic =>
        {
            Assert.Equal(CapabilityDiagnosticCategory.SecretBinding, diagnostic.Category);
            Assert.DoesNotContain("raw-secret-value", diagnostic.MaskedDetail, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task SB04_INV_SETUP_001_list_tools_failure_returns_typed_diagnostic_and_cleanup_proof()
    {
        var descriptor = BrowserDescriptor([McpToolName.Create("browser_snapshot")]);
        var fakeFactory = new FakeMcpClientFactory(new FakeMcpServerScript(
            Tools: [],
            ListToolsException: new McpSetupException(
                CapabilityDiagnosticCategory.McpListTools,
                "$.listTools",
                "tools/list failed with protocol error token=raw-secret-value",
                "Fix the MCP server list-tools handler.")));
        var setup = new McpSetupTestService(fakeFactory);

        var result = await setup.TestAsync(descriptor, "SB04_INV_SETUP_001", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.CleanupCompleted);
        Assert.Equal(1, fakeFactory.LastClient!.StopCount);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CapabilityDiagnosticCategory.McpListTools, diagnostic.Category);
        Assert.DoesNotContain("raw-secret-value", diagnostic.MaskedDetail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SB04_INV_CLEANUP_001_cleanup_failure_returns_cleanup_diagnostic_without_hiding_original_failure()
    {
        var descriptor = BrowserDescriptor([McpToolName.Create("browser_snapshot")]);
        var fakeFactory = new FakeMcpClientFactory(new FakeMcpServerScript(
            Tools: [],
            ListToolsException: new McpSetupException(
                CapabilityDiagnosticCategory.McpListTools,
                "$.listTools",
                "tools/list failed",
                "Fix the MCP server list-tools handler."),
            StopException: new InvalidOperationException("shutdown failed token=raw-secret-value")));
        var setup = new McpSetupTestService(fakeFactory);

        var result = await setup.TestAsync(descriptor, "SB04_INV_CLEANUP_001", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.CleanupCompleted);
        Assert.Equal(1, fakeFactory.LastClient!.StopCount);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Category == CapabilityDiagnosticCategory.McpListTools);
        var cleanup = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Category == CapabilityDiagnosticCategory.ResourceCleanup);
        Assert.DoesNotContain("raw-secret-value", cleanup.MaskedDetail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SB04_INV_SETUP_002_allowed_tools_mismatch_reports_missing_tool()
    {
        var descriptor = BrowserDescriptor([McpToolName.Create("browser_missing")]);
        var setup = new McpSetupTestService(new FakeMcpClientFactory(new FakeMcpServerScript(
            Tools: [new DiscoveredMcpTool(McpToolName.Create("browser_snapshot"), "Snapshot page state.")])));

        var result = await setup.TestAsync(descriptor, "SB04_INV_SETUP_002", CancellationToken.None);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CapabilityDiagnosticCategory.McpListTools, diagnostic.Category);
        Assert.Equal("$.allowedTools", diagnostic.FieldPath);
        Assert.Contains("browser_missing", diagnostic.MaskedDetail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SB04_INV_SETUP_003_startup_timeout_returns_timeout_diagnostic()
    {
        var descriptor = BrowserDescriptor([McpToolName.Create("browser_snapshot")]);
        var setup = new McpSetupTestService(new FakeMcpClientFactory(new FakeMcpServerScript(
            Tools: [],
            StartException: new TimeoutException("startup expired token=raw-secret-value"))));

        var result = await setup.TestAsync(descriptor, "SB04_INV_SETUP_003", CancellationToken.None);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CapabilityDiagnosticCategory.Timeout, diagnostic.Category);
        Assert.Equal(TimeSpan.FromSeconds(5), diagnostic.Timeout);
        Assert.DoesNotContain("raw-secret-value", diagnostic.MaskedDetail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SB04_INV_SETUP_004_process_start_and_handshake_failures_return_typed_diagnostics()
    {
        var descriptor = BrowserDescriptor([McpToolName.Create("browser_snapshot")]);
        var processStartSetup = new McpSetupTestService(new FakeMcpClientFactory(new FakeMcpServerScript(
            Tools: [],
            StartException: new McpSetupException(
                CapabilityDiagnosticCategory.ProcessStart,
                "$.command",
                "process start failed token=raw-secret-value",
                "Fix the local MCP command."))));
        var handshakeSetup = new McpSetupTestService(new FakeMcpClientFactory(new FakeMcpServerScript(
            Tools: [],
            StartException: new McpSetupException(
                CapabilityDiagnosticCategory.McpHandshake,
                "$.handshake",
                "handshake failed token=raw-secret-value",
                "Fix the MCP initialize handshake."))));

        var processStart = await processStartSetup.TestAsync(descriptor, "SB04_INV_SETUP_004_PROCESS", CancellationToken.None);
        var handshake = await handshakeSetup.TestAsync(descriptor, "SB04_INV_SETUP_004_HANDSHAKE", CancellationToken.None);

        var processDiagnostic = Assert.Single(processStart.Diagnostics);
        var handshakeDiagnostic = Assert.Single(handshake.Diagnostics);
        Assert.False(processStart.IsSuccess);
        Assert.False(handshake.IsSuccess);
        Assert.True(processStart.CleanupCompleted);
        Assert.True(handshake.CleanupCompleted);
        Assert.Equal(CapabilityDiagnosticCategory.ProcessStart, processDiagnostic.Category);
        Assert.Equal(CapabilityDiagnosticCategory.McpHandshake, handshakeDiagnostic.Category);
        Assert.DoesNotContain("raw-secret-value", processDiagnostic.MaskedDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-secret-value", handshakeDiagnostic.MaskedDetail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SB04_INV_SETUP_005_cancellation_returns_cancellation_diagnostic_and_cleanup_proof()
    {
        var descriptor = BrowserDescriptor([McpToolName.Create("browser_snapshot")]);
        var fakeFactory = new FakeMcpClientFactory(new FakeMcpServerScript(
            Tools: [],
            StartException: new OperationCanceledException()));
        var setup = new McpSetupTestService(fakeFactory);

        var result = await setup.TestAsync(descriptor, "SB04_INV_SETUP_005", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.CleanupCompleted);
        Assert.Equal(1, fakeFactory.LastClient!.StopCount);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CapabilityDiagnosticCategory.Cancellation, diagnostic.Category);
    }

    [Fact]
    public async Task SB04_INV_RUNTIME_001_fake_mcp_client_can_start_list_call_and_stop()
    {
        var descriptor = BrowserDescriptor([McpToolName.Create("browser_snapshot")]);
        var factory = new FakeMcpClientFactory(new FakeMcpServerScript(
            Tools: [new DiscoveredMcpTool(McpToolName.Create("browser_snapshot"), "Snapshot page state.")],
            ToolResults: new Dictionary<McpToolName, string>
            {
                [McpToolName.Create("browser_snapshot")] = """{"ok":true}"""
            }));
        var client = await factory.CreateAsync(descriptor, "SB04_INV_RUNTIME_001", CancellationToken.None);

        await client.StartAsync(CancellationToken.None);
        var tools = await client.ListToolsAsync(CancellationToken.None);
        var result = await client.CallToolAsync(McpToolName.Create("browser_snapshot"), """{"url":"https://example.test"}""", CancellationToken.None);
        await client.StopAsync(CancellationToken.None);

        Assert.Single(tools);
        Assert.Contains("\"ok\":true", result, StringComparison.Ordinal);
        Assert.Equal(1, factory.LastClient!.StartCount);
        Assert.Equal(1, factory.LastClient.ListToolsCount);
        Assert.Equal(1, factory.LastClient.CallCount);
        Assert.Equal(1, factory.LastClient.StopCount);
    }

    [Fact]
    public async Task Local_stdio_factory_creates_local_clients_and_rejects_unsupported_transports()
    {
        var factory = new LocalStdioMcpClientFactory();
        var local = BrowserDescriptor([McpToolName.Create("browser_snapshot")]);
        var remote = McpDescriptorFactory.RemoteHttp(
            CapabilityKey.Create("remote-browser-mcp"),
            McpServerKey.Create("remote-browser-mcp"),
            "Remote Browser MCP",
            "Remote MCP.",
            new Uri("https://example.test/mcp"),
            allowedTools: [McpToolName.Create("browser_snapshot")],
            headerBindings: new Dictionary<string, string>(),
            rawHeaders: new Dictionary<string, string>(),
            approvalMode: McpApprovalMode.NeverRequire,
            timeout: TimeSpan.FromSeconds(3));

        var client = await factory.CreateAsync(local, "LOCAL_STDIO_FACTORY_LOCAL", CancellationToken.None);
        var exception = await Assert.ThrowsAsync<McpSetupException>(
            () => factory.CreateAsync(remote, "LOCAL_STDIO_FACTORY_REMOTE", CancellationToken.None));

        Assert.IsType<LocalStdioMcpRuntimeClient>(client);
        Assert.Equal(CapabilityDiagnosticCategory.ImplementationMissing, exception.Category);
        await client.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Local_stdio_json_rpc_framing_round_trips_content_length_messages()
    {
        var payload = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 42,
            ["result"] = new JsonObject
            {
                ["ok"] = true
            }
        };
        await using var stream = new MemoryStream();

        await McpJsonRpcFraming.WriteMessageAsync(stream, payload, CancellationToken.None);
        stream.Position = 0;
        var message = await McpJsonRpcFraming.ReadMessageAsync(stream, CancellationToken.None);

        Assert.Contains("\"id\":42", message, StringComparison.Ordinal);
        Assert.Contains("\"ok\":true", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Local_stdio_json_rpc_framing_round_trips_newline_delimited_json_messages()
    {
        var payload = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 43,
            ["result"] = new JsonObject
            {
                ["ok"] = true
            }
        };
        await using var stream = new MemoryStream();

        await McpJsonRpcFraming.WriteMessageAsync(
            stream,
            payload,
            McpStdioMessageFraming.NewlineDelimitedJson,
            CancellationToken.None);
        stream.Position = 0;
        var wireMessage = Encoding.UTF8.GetString(stream.ToArray());
        var message = await McpJsonRpcFraming.ReadMessageAsync(
            stream,
            McpStdioMessageFraming.NewlineDelimitedJson,
            CancellationToken.None);

        Assert.DoesNotContain("Content-Length", wireMessage, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("\n", wireMessage, StringComparison.Ordinal);
        Assert.Contains("\"id\":43", message, StringComparison.Ordinal);
        Assert.Contains("\"ok\":true", message, StringComparison.Ordinal);
    }

    [Fact]
    public void SB04_INV_POLICY_001_server_and_child_tool_descriptors_participate_in_access_policy()
    {
        var server = McpExposureDescriptorFactory.CreateServer(BrowserDescriptor([McpToolName.Create("browser_snapshot")]));
        var tool = McpExposureDescriptorFactory.CreateTool(
            BrowserDescriptor([McpToolName.Create("browser_snapshot")]),
            new DiscoveredMcpTool(McpToolName.Create("browser_snapshot"), "Snapshot page state."));
        var evaluator = new CapabilityAccessPolicyEvaluator();
        var policy = new CapabilityAccessPolicy(
        [
            new CapabilityAccessRule(
                CapabilityRuleId.Create("deny-server"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByMcpServerKey(McpServerKey.Create("playwright-local")),
                "No browser MCP server."),
            new CapabilityAccessRule(
                CapabilityRuleId.Create("deny-tool"),
                CapabilityAccessEffect.Deny,
                CapabilityAccessScope.ProcessStep,
                CapabilitySelector.ByMcpToolName(McpServerKey.Create("playwright-local"), McpToolName.Create("browser_snapshot")),
                "No browser snapshot tool.")
        ]);

        var result = evaluator.Evaluate(new CapabilityAccessEvaluationContext(
            [server, tool],
            [],
            [policy],
            "SB04_INV_POLICY_001"));

        Assert.Empty(result.AllowedCapabilities);
        Assert.Contains(result.Diagnostics, item => item.Identity == server.Identity);
        Assert.Contains(result.Diagnostics, item => item.Identity == tool.Identity);
    }

    [Fact]
    public async Task SB04_INV_REMOTE_001_remote_http_status_failure_maps_typed_diagnostic()
    {
        var descriptor = McpDescriptorFactory.RemoteHttp(
            CapabilityKey.Create("remote-docs-mcp"),
            McpServerKey.Create("remote-docs-mcp"),
            "Remote Docs MCP",
            "Remote docs MCP.",
            new Uri("https://example.test/mcp"),
            allowedTools: [McpToolName.Create("docs_search")],
            headerBindings: new Dictionary<string, string> { ["Authorization"] = "secret:docs-token" },
            rawHeaders: new Dictionary<string, string>(),
            approvalMode: McpApprovalMode.NeverRequire,
            timeout: TimeSpan.FromSeconds(3));
        var setup = new McpSetupTestService(new FakeMcpClientFactory(new FakeMcpServerScript(
            Tools: [],
            StartException: new McpSetupException(
                CapabilityDiagnosticCategory.HttpStatus,
                "$.endpoint",
                "HTTP 503 from example.test with Authorization=Bearer raw-secret-value",
                "Repair the remote endpoint or credentials.",
                httpStatusCode: 503))));

        var result = await setup.TestAsync(descriptor, "SB04_INV_REMOTE_001", CancellationToken.None);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(CapabilityDiagnosticCategory.HttpStatus, diagnostic.Category);
        Assert.Equal(503, diagnostic.HttpStatusCode);
        Assert.DoesNotContain("raw-secret-value", diagnostic.MaskedDetail, StringComparison.Ordinal);
    }

    private static LocalStdioMcpServerDescriptor BrowserDescriptor(IReadOnlyList<McpToolName> allowedTools)
    {
        return McpDescriptorFactory.LocalStdio(
            CapabilityKey.Create("playwright-local-mcp"),
            McpServerKey.Create("playwright-local"),
            "Playwright Local MCP",
            "Local browser automation MCP.",
            command: "node",
            arguments: ["@playwright/mcp"],
            workingDirectory: ".",
            allowedWorkingDirectories: [],
            allowedTools: allowedTools,
            environmentVariableBindings: new Dictionary<string, string>(),
            rawEnvironmentVariables: new Dictionary<string, string>(),
            approvalMode: McpApprovalMode.AlwaysRequire,
            timeout: TimeSpan.FromSeconds(5));
    }
}
