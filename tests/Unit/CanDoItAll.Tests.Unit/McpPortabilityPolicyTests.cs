using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Mcp;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using System.Text.Json;

namespace CanDoItAll.Tests.Unit;

[Collection("MCP portability environment")]
public sealed class McpPortabilityPolicyTests
{
    [Fact]
    public void Capability_editor_round_trip_preserves_exact_mcp_and_process_runtime_data()
    {
        var mcpEditor = new CapabilityEditorModel
        {
            Kind = CanDoItAll.AgentFramework.Models.CapabilityKind.McpServer,
            Key = "exact-mcp",
            Name = "Exact MCP",
            EndpointOrPath = "node",
            ConfigurationJson = JsonSerializer.Serialize(new
            {
                transport = "stdio",
                command = "node",
                arguments = new[] { "", " value ", "--yes" },
                workingDirectory = " /tmp/working root ",
                allowedWorkingDirectories = new[] { "/tmp/Foo", "/tmp/foo", " /tmp/working root " },
                allowedTools = new[] { "SafeTool", "safetool" },
                approvalMode = "AlwaysRequire"
            })
        };

        var mcpState = CapabilityConfigurationEditorSupport.ReadMcp(mcpEditor);
        Assert.Empty(CapabilityConfigurationEditorSupport.WriteMcp(mcpEditor, mcpState));

        using (var document = JsonDocument.Parse(mcpEditor.ConfigurationJson))
        {
            var root = document.RootElement;
            Assert.Equal(["", " value ", "--yes"], ReadStrings(root, "arguments"));
            Assert.Equal(" /tmp/working root ", root.GetProperty("workingDirectory").GetString());
            Assert.Equal(
                ["/tmp/Foo", "/tmp/foo", " /tmp/working root "],
                ReadStrings(root, "allowedWorkingDirectories"));
            Assert.Equal(["SafeTool", "safetool"], ReadStrings(root, "allowedTools"));
        }

        var toolEditor = new CapabilityEditorModel
        {
            Kind = CanDoItAll.AgentFramework.Models.CapabilityKind.Tool,
            Key = "exact-process",
            Name = "Exact process",
            EndpointOrPath = "dotnet",
            ConfigurationJson = JsonSerializer.Serialize(new
            {
                toolKind = "externalProcess",
                runtimeToolName = "exact_process",
                implementationKey = "external.exact-process",
                externalProcess = new
                {
                    command = "dotnet",
                    arguments = new[] { "", " value ", " " },
                    workingDirectory = " /tmp/tool root ",
                    allowedExecutableNames = new[] { "Tool", "tool" },
                    requiredOutputProperties = new[] { "id", "ID", " id " }
                }
            })
        };

        var toolState = CapabilityConfigurationEditorSupport.ReadTool(toolEditor);
        Assert.Empty(CapabilityConfigurationEditorSupport.WriteTool(toolEditor, toolState));

        using var toolDocument = JsonDocument.Parse(toolEditor.ConfigurationJson);
        var process = toolDocument.RootElement.GetProperty("externalProcess");
        Assert.Equal(["", " value ", " "], ReadStrings(process, "arguments"));
        Assert.Equal(" /tmp/tool root ", process.GetProperty("workingDirectory").GetString());
        Assert.Equal(["Tool", "tool"], ReadStrings(process, "allowedExecutableNames"));
        Assert.Equal(["id", "ID", " id "], ReadStrings(process, "requiredOutputProperties"));

        static string[] ReadStrings(JsonElement owner, string propertyName)
            => owner.GetProperty(propertyName)
                .EnumerateArray()
                .Select(item => item.GetString() ?? throw new InvalidOperationException("Expected a JSON string."))
                .ToArray();
    }

    [Fact]
    public void Capability_editor_environment_bindings_follow_host_case_semantics_and_reject_collisions()
    {
        var editor = new CapabilityEditorModel
        {
            Kind = CanDoItAll.AgentFramework.Models.CapabilityKind.McpServer,
            Key = "environment-case-semantics",
            Name = "Environment case semantics",
            EndpointOrPath = "node",
            ConfigurationJson = JsonSerializer.Serialize(new
            {
                transport = "stdio",
                command = "node",
                arguments = new[] { "server.js" },
                workingDirectory = ".",
                allowedTools = new[] { "browser_snapshot" },
                approvalMode = "AlwaysRequire"
            })
        };
        var state = CapabilityConfigurationEditorSupport.ReadMcp(editor);
        state.EnvironmentVariableBindingsText = string.Join(
            Environment.NewLine,
            "MCP_CASE_TARGET=MCP_SOURCE_ONE",
            "mcp_case_target=MCP_SOURCE_TWO");

        var errors = CapabilityConfigurationEditorSupport.WriteMcp(editor, state);

        if (OperatingSystem.IsWindows())
        {
            Assert.Contains(errors, error => error.Contains("ambiguous", StringComparison.OrdinalIgnoreCase));
            return;
        }

        Assert.Empty(errors);
        using var document = JsonDocument.Parse(editor.ConfigurationJson);
        var bindings = document.RootElement.GetProperty("environmentVariableBindings");
        Assert.Equal(2, bindings.EnumerateObject().Count());
        Assert.True(bindings.TryGetProperty("MCP_CASE_TARGET", out _));
        Assert.True(bindings.TryGetProperty("mcp_case_target", out _));
    }

    [Theory]
    [InlineData("missing-approval", CapabilityDiagnosticCategory.TemplateValidation, "$.approvalMode")]
    [InlineData("invalid-tool", CapabilityDiagnosticCategory.TemplateValidation, "$.allowedTools")]
    [InlineData("secret-argument", CapabilityDiagnosticCategory.SecretBinding, "$.arguments")]
    public void Mcp_setup_descriptor_rejects_runtime_invalid_configuration_before_client_creation(
        string scenario,
        CapabilityDiagnosticCategory expectedCategory,
        string expectedFieldPath)
    {
        var approvalMode = scenario == "missing-approval" ? null : "AlwaysRequire";
        var allowedTools = scenario == "invalid-tool"
            ? new[] { "browser_snapshot", "invalid tool name" }
            : new[] { "browser_snapshot" };
        var arguments = scenario == "secret-argument"
            ? new[] { "server.js", "--password", "literal-secret" }
            : new[] { "server.js" };
        var editor = new CapabilityEditorModel
        {
            Kind = CanDoItAll.AgentFramework.Models.CapabilityKind.McpServer,
            Key = "setup-parity",
            Name = "Setup parity",
            EndpointOrPath = "node",
            ConfigurationJson = JsonSerializer.Serialize(new
            {
                transport = "stdio",
                serverName = "setup-parity",
                command = "node",
                arguments,
                workingDirectory = ".",
                allowedTools,
                approvalMode
            })
        };

        _ = AgentCapabilitySetupFlowService.BuildMcpDescriptor(
            editor,
            "setup-parity-test",
            out var diagnostics);

        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Category == expectedCategory &&
            diagnostic.FieldPath == expectedFieldPath);
    }

    [Fact]
    public async Task Playwright_resolver_rejects_unpinned_package_before_any_process_or_cache_probe()
    {
        var host = new RecordingLongRunningProcessHost();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PlaywrightMcpLaunchResolver.TryResolveAsync(
                Path.GetTempPath(),
                OperatingSystem.IsWindows() ? "npx.cmd" : "npx",
                ["--yes", "@playwright/mcp@latest"],
                host,
                CancellationToken.None));

        Assert.Contains("exact package version", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(host.SessionRequest);
        Assert.Null(host.ExecutionRequest);
    }

    [Fact]
    public async Task Playwright_resolver_rejects_missing_npm_before_creating_managed_install_directories()
    {
        using var workspace = new TemporaryDirectory();
        using var runtime = new TemporaryDirectory();
        var previousPath = Environment.GetEnvironmentVariable("PATH");
        var previousPathExtensions = Environment.GetEnvironmentVariable("PATHEXT");
        CreateFakeNodeOnlyRuntime(runtime.Path);
        Environment.SetEnvironmentVariable("PATH", runtime.Path);
        if (OperatingSystem.IsWindows())
        {
            Environment.SetEnvironmentVariable("PATHEXT", ".EXE;.CMD");
        }

        try
        {
            var host = new RecordingLongRunningProcessHost();

            await Assert.ThrowsAsync<WorkspaceExecutableResolutionException>(() =>
                PlaywrightMcpLaunchResolver.TryResolveAsync(
                    workspace.Path,
                    OperatingSystem.IsWindows() ? "npx.cmd" : "npx",
                    ["@playwright/mcp@0.0.78"],
                    host,
                    CancellationToken.None));

            Assert.False(Directory.Exists(Path.Combine(workspace.Path, ".agent-tools")));
            Assert.Null(host.ExecutionRequest);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
            Environment.SetEnvironmentVariable("PATHEXT", previousPathExtensions);
        }
    }

    [Fact]
    public void Playwright_provider_credential_classification_requires_npx_exact_package_and_vision()
    {
        var npx = OperatingSystem.IsWindows() ? "npx.cmd" : "npx";

        Assert.True(PlaywrightMcpLaunchResolver.IsPinnedVisionLaunch(
            npx,
            ["--yes", "@playwright/mcp@0.0.78", "--caps", "vision"]));
        Assert.False(PlaywrightMcpLaunchResolver.IsPinnedVisionLaunch(
            "node",
            ["@playwright/mcp@0.0.78", "--caps", "vision"]));
        Assert.False(PlaywrightMcpLaunchResolver.IsPinnedVisionLaunch(
            npx,
            ["@playwright/mcp-collector", "--caps", "vision"]));
    }

    [Fact]
    public void Provider_credential_environment_policy_preserves_significant_secret_bytes()
    {
        var environment = new Dictionary<string, string?>();

        LocalMcpCredentialEnvironmentPolicy.Add(
            environment,
            "OPENAI_API_KEY",
            "credential-with-trailing-space ");

        Assert.Equal("credential-with-trailing-space ", environment["OPENAI_API_KEY"]);
    }

    [Fact]
    public void Provider_credential_environment_policy_rejects_existing_targets()
    {
        var environment = new Dictionary<string, string?>(
            new WorkspaceCommandEnvironmentPolicy().EnvironmentNameComparer)
        {
            ["OPENAI_API_KEY"] = "explicit-binding"
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            LocalMcpCredentialEnvironmentPolicy.Add(
                environment,
                "OPENAI_API_KEY",
                "provider-credential"));

        Assert.Contains("cannot overwrite", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("explicit-binding", environment["OPENAI_API_KEY"]);
    }

    [Fact]
    public async Task Playwright_resolver_publishes_versioned_hash_evidence_and_reuses_only_a_valid_install()
    {
        using var workspace = new TemporaryDirectory();
        using var runtime = new TemporaryDirectory();
        var previousPath = Environment.GetEnvironmentVariable("PATH");
        var previousPathExtensions = Environment.GetEnvironmentVariable("PATHEXT");
        CreateFakeNodeRuntime(runtime.Path);
        Environment.SetEnvironmentVariable("PATH", runtime.Path);
        if (OperatingSystem.IsWindows())
        {
            Environment.SetEnvironmentVariable("PATHEXT", ".EXE;.CMD");
        }

        try
        {
            var installer = new InstallingProcessHost();
            var resolution = await PlaywrightMcpLaunchResolver.TryResolveAsync(
                workspace.Path,
                OperatingSystem.IsWindows() ? "npx.cmd" : "npx",
                ["--yes", "@playwright/mcp@0.0.78", "--yes", " value ", "", "--headless"],
                installer,
                CancellationToken.None);

            Assert.NotNull(resolution);
            Assert.Equal(1, installer.ExecutionCount);
            var cliPath = Assert.Single(
                resolution.Arguments,
                argument => argument.EndsWith("cli.js", StringComparison.Ordinal));
            Assert.Contains(
                Path.Combine("playwright-mcp", "0.0.78"),
                cliPath,
                StringComparison.Ordinal);
            Assert.Equal(
                ["--yes", " value ", "", "--headless"],
                resolution.Arguments.Skip(1));
            var versionRoot = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(cliPath)!,
                "..",
                "..",
                ".."));
            Assert.True(File.Exists(Path.Combine(versionRoot, ".candoitall-install.json")));
            var runtimeDependencyPath = Path.Combine(
                versionRoot,
                "node_modules",
                "@playwright",
                "mcp",
                "runtime.js");

            var reuseHost = new InstallingProcessHost();
            var reused = await PlaywrightMcpLaunchResolver.TryResolveAsync(
                workspace.Path,
                OperatingSystem.IsWindows() ? "npx.cmd" : "npx",
                ["@playwright/mcp@0.0.78"],
                reuseHost,
                CancellationToken.None);

            Assert.NotNull(reused);
            Assert.Equal(0, reuseHost.ExecutionCount);

            await File.AppendAllTextAsync(runtimeDependencyPath, "tampered");
            var integrityException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                PlaywrightMcpLaunchResolver.TryResolveAsync(
                    workspace.Path,
                    OperatingSystem.IsWindows() ? "npx.cmd" : "npx",
                    ["@playwright/mcp@0.0.78"],
                    reuseHost,
                    CancellationToken.None));
            Assert.Contains("integrity", integrityException.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, reuseHost.ExecutionCount);

            await File.WriteAllTextAsync(
                runtimeDependencyPath,
                "// deterministic transitive runtime fixture");
            Assert.NotNull(await PlaywrightMcpLaunchResolver.TryResolveAsync(
                workspace.Path,
                OperatingSystem.IsWindows() ? "npx.cmd" : "npx",
                ["@playwright/mcp@0.0.78"],
                reuseHost,
                CancellationToken.None));
            var nodePath = Path.Combine(
                runtime.Path,
                OperatingSystem.IsWindows() ? "node.exe" : "node");
            if (!OperatingSystem.IsWindows())
            {
                var dependencyMode = File.GetUnixFileMode(runtimeDependencyPath);
                File.SetUnixFileMode(
                    runtimeDependencyPath,
                    dependencyMode ^ UnixFileMode.GroupWrite);
                var dependencyModeException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    PlaywrightMcpLaunchResolver.TryResolveAsync(
                        workspace.Path,
                        "npx",
                        ["@playwright/mcp@0.0.78"],
                        reuseHost,
                        CancellationToken.None));
                Assert.Contains("integrity", dependencyModeException.Message, StringComparison.OrdinalIgnoreCase);
                File.SetUnixFileMode(runtimeDependencyPath, dependencyMode);

                var nodeMode = File.GetUnixFileMode(nodePath);
                File.SetUnixFileMode(nodePath, nodeMode ^ UnixFileMode.GroupWrite);
                var nodeModeException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    PlaywrightMcpLaunchResolver.TryResolveAsync(
                        workspace.Path,
                        "npx",
                        ["@playwright/mcp@0.0.78"],
                        reuseHost,
                        CancellationToken.None));
                Assert.Contains("integrity", nodeModeException.Message, StringComparison.OrdinalIgnoreCase);
                File.SetUnixFileMode(nodePath, nodeMode);
            }

            await File.AppendAllTextAsync(nodePath, "tampered-runtime");
            var runtimeIntegrityException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                PlaywrightMcpLaunchResolver.TryResolveAsync(
                    workspace.Path,
                    OperatingSystem.IsWindows() ? "npx.cmd" : "npx",
                    ["@playwright/mcp@0.0.78"],
                    reuseHost,
                    CancellationToken.None));
            Assert.Contains(
                "integrity",
                runtimeIntegrityException.Message,
                StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, reuseHost.ExecutionCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
            Environment.SetEnvironmentVariable("PATHEXT", previousPathExtensions);
        }
    }

    [Fact]
    public async Task Playwright_resolver_rejects_a_linked_managed_root_before_installing()
    {
        using var workspace = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        using var runtime = new TemporaryDirectory();
        var previousPath = Environment.GetEnvironmentVariable("PATH");
        var previousPathExtensions = Environment.GetEnvironmentVariable("PATHEXT");
        CreateFakeNodeRuntime(runtime.Path);
        Environment.SetEnvironmentVariable("PATH", runtime.Path);
        if (OperatingSystem.IsWindows())
        {
            Environment.SetEnvironmentVariable("PATHEXT", ".EXE;.CMD");
        }

        var linkedManagedRoot = Path.Combine(workspace.Path, ".agent-tools");
        Directory.CreateSymbolicLink(linkedManagedRoot, outside.Path);
        try
        {
            var installer = new InstallingProcessHost();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                PlaywrightMcpLaunchResolver.TryResolveAsync(
                    workspace.Path,
                    OperatingSystem.IsWindows() ? "npx.cmd" : "npx",
                    ["@playwright/mcp@0.0.78"],
                    installer,
                    CancellationToken.None));

            Assert.Contains("links or reparse points", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, installer.ExecutionCount);
            Assert.Empty(Directory.EnumerateFileSystemEntries(outside.Path));
        }
        finally
        {
            if (Directory.Exists(linkedManagedRoot))
            {
                Directory.Delete(linkedManagedRoot);
            }

            Environment.SetEnvironmentVariable("PATH", previousPath);
            Environment.SetEnvironmentVariable("PATHEXT", previousPathExtensions);
        }
    }

    [Fact]
    public void Local_MCP_command_policy_uses_host_correct_case_and_suffix_semantics()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.True(LocalMcpCommandPolicy.IsAllowed("NODE.EXE"));
            Assert.True(LocalMcpCommandPolicy.IsAllowed("npx.cmd"));
            return;
        }

        Assert.True(LocalMcpCommandPolicy.IsAllowed("node"));
        Assert.False(LocalMcpCommandPolicy.IsAllowed("NODE"));
        Assert.False(LocalMcpCommandPolicy.IsAllowed("node.exe"));
    }

    [Fact]
    public async Task Local_MCP_launcher_uses_duplex_owned_session_and_ephemeral_binding()
    {
        const string sourceName = "CANDOITALL_B04_UNIT_SECRET";
        const string secret = "B04_UNIT_SECRET_SENTINEL_31f6";
        var previous = Environment.GetEnvironmentVariable(sourceName);
        Environment.SetEnvironmentVariable(sourceName, secret);
        try
        {
            var workingDirectory = Path.GetTempPath();
            var descriptor = McpDescriptorFactory.LocalStdio(
                CapabilityKey.Create("portable-mcp"),
                McpServerKey.Create("portable-mcp"),
                "Portable MCP",
                "Portable MCP.",
                command: "dotnet",
                arguments: [],
                workingDirectory: workingDirectory,
                allowedWorkingDirectories: [],
                allowedTools: [McpToolName.Create("echo")],
                environmentVariableBindings: new Dictionary<string, string>
                {
                    ["MCP_TEST_SECRET"] = sourceName
                },
                rawEnvironmentVariables: new Dictionary<string, string>(),
                approvalMode: McpApprovalMode.AlwaysRequire,
                timeout: TimeSpan.FromSeconds(5));
            var host = new RecordingLongRunningProcessHost();

            await using var session = await LocalStdioMcpProcessLauncher.StartAsync(
                descriptor,
                "mcp-portability-unit",
                host,
                new FixedPathResolver(workingDirectory),
                CancellationToken.None);

            Assert.Same(host.Session, session);
            Assert.NotNull(host.SessionRequest);
            Assert.Equal(WorkspaceProcessStandardIoMode.Duplex, host.SessionRequest.StandardIoMode);
            Assert.Equal(
                secret,
                host.SessionRequest.EnvironmentVariables["MCP_TEST_SECRET"]);
            Assert.DoesNotContain(
                host.SessionRequest.EnvironmentVariables.Keys,
                name => name.Contains("TOKEN", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable(sourceName, previous);
        }
    }

    [Fact]
    public async Task Local_MCP_launcher_accepts_only_an_explicitly_allowed_external_working_directory()
    {
        using var workspace = new TemporaryDirectory();
        using var externalRoot = new TemporaryDirectory();
        using var unlistedRoot = new TemporaryDirectory();
        var externalWorkingDirectory = Path.Combine(
            externalRoot.Path,
            OperatingSystem.IsWindows() ? "folder" : " folder ");
        Directory.CreateDirectory(externalWorkingDirectory);
        var registry = new ExternalTargetPathRegistry();
        var resolver = new WorkspacePathResolutionService(
            workspace.Path,
            new PhysicalFileSystemPathPolicyFactory(),
            externalTargetRegistry: registry);
        var allowedHost = new RecordingLongRunningProcessHost();

        await using var session = await LocalStdioMcpProcessLauncher.StartAsync(
            CreateLocalDescriptor(
                "dotnet",
                ["", " value ", " "],
                externalWorkingDirectory,
                [externalRoot.Path]),
            "mcp-external-working-directory",
            allowedHost,
            resolver,
            CancellationToken.None);

        Assert.Equal(externalWorkingDirectory, allowedHost.SessionRequest!.WorkingDirectory);
        Assert.Equal(["", " value ", " "], allowedHost.SessionRequest.Arguments);

        var deniedHost = new RecordingLongRunningProcessHost();
        var exception = await Assert.ThrowsAsync<McpSetupException>(() =>
            LocalStdioMcpProcessLauncher.StartAsync(
                CreateLocalDescriptor(
                    "dotnet",
                    [],
                    externalWorkingDirectory,
                    [unlistedRoot.Path]),
                "mcp-unlisted-external-working-directory",
                deniedHost,
                resolver,
                CancellationToken.None));

        Assert.Equal(CapabilityDiagnosticCategory.WorkingDirectory, exception.Category);
        Assert.Null(deniedHost.SessionRequest);
    }

    [Fact]
    public async Task Local_MCP_launcher_reports_working_directory_failure_separately()
    {
        var descriptor = CreateLocalDescriptor("node", []);

        var exception = await Assert.ThrowsAsync<McpSetupException>(() =>
            LocalStdioMcpProcessLauncher.StartAsync(
                descriptor,
                "mcp-working-directory",
                new RecordingLongRunningProcessHost(),
                new ThrowingPathResolver(),
                CancellationToken.None));

        Assert.Equal(CapabilityDiagnosticCategory.WorkingDirectory, exception.Category);
        Assert.Equal("$.workingDirectory", exception.FieldPath);
    }

    [Fact]
    public async Task Local_MCP_launcher_reports_permission_failure_separately()
    {
        var descriptor = CreateLocalDescriptor("node", []);

        var exception = await Assert.ThrowsAsync<McpSetupException>(() =>
            LocalStdioMcpProcessLauncher.StartAsync(
                descriptor,
                "mcp-permission-denied",
                new RecordingLongRunningProcessHost(),
                new PermissionDeniedPathResolver(),
                CancellationToken.None));

        Assert.Equal(CapabilityDiagnosticCategory.PermissionDenied, exception.Category);
        Assert.Equal("$.workingDirectory", exception.FieldPath);
    }

    [Fact]
    public async Task Local_MCP_launcher_reports_missing_runtime_separately()
    {
        var previousPath = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", string.Empty);
        try
        {
            var host = new RecordingLongRunningProcessHost();
            var exception = await Assert.ThrowsAsync<McpSetupException>(() =>
                LocalStdioMcpProcessLauncher.StartAsync(
                    CreateLocalDescriptor("node", []),
                    "mcp-runtime-missing",
                    host,
                    new FixedPathResolver(Path.GetTempPath()),
                    CancellationToken.None));

            Assert.Equal(CapabilityDiagnosticCategory.RuntimeDependency, exception.Category);
            Assert.Equal("$.command", exception.FieldPath);
            Assert.Null(host.SessionRequest);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
        }
    }

    [Fact]
    public async Task Local_MCP_launcher_reports_package_setup_failure_separately()
    {
        var host = new RecordingLongRunningProcessHost();

        var exception = await Assert.ThrowsAsync<McpSetupException>(() =>
            LocalStdioMcpProcessLauncher.StartAsync(
                CreateLocalDescriptor(
                    OperatingSystem.IsWindows() ? "npx.cmd" : "npx",
                    ["@playwright/mcp@latest"]),
                "mcp-package-invalid",
                host,
                new FixedPathResolver(Path.GetTempPath()),
                CancellationToken.None));

        Assert.Equal(CapabilityDiagnosticCategory.PackageSetup, exception.Category);
        Assert.Equal("$.arguments", exception.FieldPath);
        Assert.Null(host.ExecutionRequest);
        Assert.Null(host.SessionRequest);
    }

    [Fact]
    public async Task Local_MCP_launcher_reports_unsupported_shell_neutral_runtime_separately()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new TemporaryDirectory();
        using var runtime = new TemporaryDirectory();
        var previousPath = Environment.GetEnvironmentVariable("PATH");
        var previousPathExtensions = Environment.GetEnvironmentVariable("PATHEXT");
        File.WriteAllText(Path.Combine(runtime.Path, "node.exe"), string.Empty);
        File.WriteAllText(Path.Combine(runtime.Path, "npm.cmd"), string.Empty);
        Environment.SetEnvironmentVariable("PATH", runtime.Path);
        Environment.SetEnvironmentVariable("PATHEXT", ".EXE;.CMD");
        try
        {
            var exception = await Assert.ThrowsAsync<McpSetupException>(() =>
                LocalStdioMcpProcessLauncher.StartAsync(
                    CreateLocalDescriptor("npx.cmd", ["@playwright/mcp@0.0.78"]),
                    "mcp-platform-unsupported",
                    new RecordingLongRunningProcessHost(),
                    new FixedPathResolver(workspace.Path),
                    CancellationToken.None));

            Assert.Equal(CapabilityDiagnosticCategory.UnsupportedPlatform, exception.Category);
            Assert.Equal("$.command", exception.FieldPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
            Environment.SetEnvironmentVariable("PATHEXT", previousPathExtensions);
        }
    }

    [Fact]
    public async Task Local_MCP_shutdown_reports_unconfirmed_process_tree_cleanup()
    {
        var descriptor = CreateLocalDescriptor("dotnet", []);
        var host = new ResidualProcessHost();
        var session = new LocalStdioMcpProcessSession(
            descriptor,
            "mcp-residual-cleanup",
            host,
            new FixedPathResolver(Path.GetTempPath()));
        await session.StartAsync(CancellationToken.None);

        var exception = await Assert.ThrowsAsync<McpSetupException>(() =>
            session.StopAsync(CancellationToken.None));

        Assert.Equal(CapabilityDiagnosticCategory.ResourceCleanup, exception.Category);
        Assert.Equal("$.cleanup", exception.FieldPath);
        Assert.Equal(1, host.Session.TerminateCount);
        Assert.Equal(1, host.Session.DisposeCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Local_MCP_rejects_malformed_binding_identifiers_as_typed_secret_failures(
        bool invalidTarget)
    {
        var invalidName = invalidTarget
            ? "INVALID=TARGET"
            : "INVALID" + '\0' + "SOURCE";
        var descriptor = McpDescriptorFactory.LocalStdio(
            CapabilityKey.Create("invalid-binding-mcp"),
            McpServerKey.Create("invalid-binding-mcp"),
            "Invalid Binding MCP",
            "Invalid Binding MCP.",
            "dotnet",
            [],
            ".",
            [],
            [McpToolName.Create("echo")],
            new Dictionary<string, string>
            {
                [invalidTarget ? invalidName : "VALID_TARGET"] =
                    invalidTarget ? "VALID_SOURCE" : invalidName
            },
            new Dictionary<string, string>(),
            McpApprovalMode.AlwaysRequire,
            TimeSpan.FromSeconds(5));

        var validation = Assert.IsType<McpSetupTestResult>(
            McpSetupValidator.ValidateDescriptor(descriptor, "invalid-binding"));
        var diagnostic = Assert.Single(validation.Diagnostics);
        var bindingException = Assert.Throws<McpSetupException>(() =>
            LocalStdioMcpEnvironmentBinder.Build(descriptor));

        Assert.Equal(CapabilityDiagnosticCategory.SecretBinding, diagnostic.Category);
        Assert.Equal("$.environmentVariableBindings", diagnostic.FieldPath);
        Assert.Equal(CapabilityDiagnosticCategory.SecretBinding, bindingException.Category);
        Assert.Equal("$.environmentVariableBindings", bindingException.FieldPath);
    }

    [Fact]
    public void Local_MCP_preserves_case_distinct_binding_targets_for_Unix()
    {
        var descriptor = McpDescriptorFactory.LocalStdio(
            CapabilityKey.Create("case-binding-mcp"),
            McpServerKey.Create("case-binding-mcp"),
            "Case Binding MCP",
            "Case Binding MCP.",
            "dotnet",
            [],
            ".",
            [],
            [McpToolName.Create("echo")],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["MCP_CASE_TARGET"] = "MCP_CASE_SOURCE_UPPER",
                ["mcp_case_target"] = "MCP_CASE_SOURCE_LOWER"
            },
            new Dictionary<string, string>(),
            McpApprovalMode.AlwaysRequire,
            TimeSpan.FromSeconds(5));

        var validation = McpSetupValidator.ValidateDescriptor(
            descriptor,
            "case-binding");

        Assert.Equal(2, descriptor.EnvironmentVariableBindings.Count);
        if (OperatingSystem.IsWindows())
        {
            var diagnostic = Assert.Single(Assert.IsType<McpSetupTestResult>(validation).Diagnostics);
            Assert.Equal(CapabilityDiagnosticCategory.SecretBinding, diagnostic.Category);
            return;
        }

        Assert.Null(validation);
    }

    [Fact]
    public void Local_MCP_raw_environment_is_case_distinct_on_Unix_and_ambiguous_on_Windows()
    {
        var descriptor = McpDescriptorFactory.LocalStdio(
            CapabilityKey.Create("case-raw-environment-mcp"),
            McpServerKey.Create("case-raw-environment-mcp"),
            "Case raw environment MCP",
            "Case raw environment MCP.",
            "dotnet",
            [],
            ".",
            [],
            [McpToolName.Create("echo")],
            new Dictionary<string, string>(),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["MCP_CASE_VALUE"] = "upper",
                ["mcp_case_value"] = "lower"
            },
            McpApprovalMode.AlwaysRequire,
            TimeSpan.FromSeconds(5));

        if (OperatingSystem.IsWindows())
        {
            var exception = Assert.Throws<McpSetupException>(() =>
                LocalStdioMcpEnvironmentBinder.Build(descriptor));
            Assert.Equal(CapabilityDiagnosticCategory.SecretBinding, exception.Category);
            return;
        }

        var environment = LocalStdioMcpEnvironmentBinder.Build(descriptor);
        Assert.Equal("upper", environment["MCP_CASE_VALUE"]);
        Assert.Equal("lower", environment["mcp_case_value"]);
    }

    [Fact]
    public void MCP_runtime_has_no_second_Process_owner_or_global_npx_cache_authority()
    {
        var root = FindRepositoryRoot();
        var mcpRuntimeRoot = Path.Combine(
            root,
            "src",
            "MAF",
            "Mcp",
            "CanDoItAll.AgentFramework.Mcp");
        var runtimeSource = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(mcpRuntimeRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        var playwrightSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Core",
            "Mcp",
            "PlaywrightMcpLaunchResolver.cs"));
        var processHostSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Core",
            "Workspace",
            "Process",
            "LocalWorkspaceProcessHost.cs"));

        Assert.DoesNotContain("System.Diagnostics", runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessStartInfo", runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("IWorkspaceLongRunningProcessHost", runtimeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_npx", playwrightSource, StringComparison.Ordinal);
        Assert.DoesNotContain("npm_config_cache", playwrightSource, StringComparison.Ordinal);
        Assert.Contains("Directory.Move(stagingRoot, versionRoot)", playwrightSource, StringComparison.Ordinal);
        Assert.Contains("CliSha256", playwrightSource, StringComparison.Ordinal);
        Assert.Contains("NodeExecutableSha256", playwrightSource, StringComparison.Ordinal);
        Assert.Contains("NodeExecutableMode", playwrightSource, StringComparison.Ordinal);
        Assert.Contains("ContentTreeSha256", playwrightSource, StringComparison.Ordinal);
        Assert.Contains("FileAttributes.ReparsePoint", playwrightSource, StringComparison.Ordinal);
        Assert.Contains("startInfo.Environment.Clear()", processHostSource, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static LocalStdioMcpServerDescriptor CreateLocalDescriptor(
        string command,
        IReadOnlyList<string> arguments,
        string workingDirectory = ".",
        IReadOnlyList<string>? allowedWorkingDirectories = null)
        => McpDescriptorFactory.LocalStdio(
            CapabilityKey.Create("portable-mcp"),
            McpServerKey.Create("portable-mcp"),
            "Portable MCP",
            "Portable MCP.",
            command,
            arguments,
            workingDirectory,
            allowedWorkingDirectories ?? [],
            [McpToolName.Create("echo")],
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            McpApprovalMode.AlwaysRequire,
            TimeSpan.FromSeconds(5));

    private static void CreateFakeNodeRuntime(string runtimeRoot)
    {
        var nodePath = CreateFakeNodeOnlyRuntime(runtimeRoot);
        var npmPath = Path.Combine(runtimeRoot, OperatingSystem.IsWindows() ? "npm.cmd" : "npm");
        File.WriteAllText(npmPath, OperatingSystem.IsWindows() ? string.Empty : "#!/bin/sh\nexit 0\n");
        if (OperatingSystem.IsWindows())
        {
            var npmCliPath = Path.Combine(runtimeRoot, "node_modules", "npm", "bin", "npm-cli.js");
            Directory.CreateDirectory(Path.GetDirectoryName(npmCliPath)!);
            File.WriteAllText(npmCliPath, "// deterministic npm fixture");
            return;
        }

        File.SetUnixFileMode(
            nodePath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.SetUnixFileMode(
            npmPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static string CreateFakeNodeOnlyRuntime(string runtimeRoot)
    {
        var nodePath = Path.Combine(runtimeRoot, OperatingSystem.IsWindows() ? "node.exe" : "node");
        File.WriteAllText(nodePath, OperatingSystem.IsWindows() ? string.Empty : "#!/bin/sh\nexit 0\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                nodePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return nodePath;
    }

    private sealed class FixedPathResolver(string workingDirectory) : IWorkspacePathResolutionService
    {
        public WorkspaceResolvedPath ResolveFilePath(string path, bool allowMissing)
            => Resolve(path);

        public WorkspaceResolvedPath ResolveDirectoryPath(string path, bool allowMissing)
            => Resolve(path);

        private WorkspaceResolvedPath Resolve(string path)
            => new(workingDirectory, ".", IsWorkspacePath: true);
    }

    private sealed class ThrowingPathResolver : IWorkspacePathResolutionService
    {
        public WorkspaceResolvedPath ResolveFilePath(string path, bool allowMissing)
            => throw new DirectoryNotFoundException();

        public WorkspaceResolvedPath ResolveDirectoryPath(string path, bool allowMissing)
            => throw new DirectoryNotFoundException();
    }

    private sealed class PermissionDeniedPathResolver : IWorkspacePathResolutionService
    {
        public WorkspaceResolvedPath ResolveFilePath(string path, bool allowMissing)
            => throw new UnauthorizedAccessException();

        public WorkspaceResolvedPath ResolveDirectoryPath(string path, bool allowMissing)
            => throw new UnauthorizedAccessException();
    }

    private sealed class RecordingLongRunningProcessHost : IWorkspaceLongRunningProcessHost
    {
        public RecordingDuplexSession Session { get; } = new();

        public WorkspaceProcessExecutionRequest? ExecutionRequest { get; private set; }

        public WorkspaceProcessSessionRequest? SessionRequest { get; private set; }

        public ExecutionBoundaryDescriptor DescribeBoundary()
            => new("Test", "Test", "Test", "Test", "Test", false, "Test");

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            ExecutionRequest = request;
            throw new NotSupportedException();
        }

        public Task<IWorkspaceProcessSession> StartSessionAsync(
            WorkspaceProcessSessionRequest request,
            CancellationToken cancellationToken = default)
        {
            SessionRequest = request;
            return Task.FromResult<IWorkspaceProcessSession>(Session);
        }

        public Task<WorkspaceProcessTerminationResult> TerminateOwnedProcessAsync(
            WorkspaceOwnedProcessIdentity identity,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class InstallingProcessHost : IWorkspaceProcessHost
    {
        public int ExecutionCount { get; private set; }

        public ExecutionBoundaryDescriptor DescribeBoundary()
            => new("Test", "Test", "Test", "Test", "Test", false, "Test");

        public async Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            var prefixIndex = Array.FindIndex(
                request.Arguments.ToArray(),
                argument => string.Equals(argument, "--prefix", StringComparison.Ordinal));
            Assert.True(prefixIndex >= 0 && prefixIndex + 1 < request.Arguments.Count);
            var versionRoot = request.Arguments[prefixIndex + 1];
            var packageRoot = Path.Combine(versionRoot, "node_modules", "@playwright", "mcp");
            Directory.CreateDirectory(packageRoot);
            await File.WriteAllTextAsync(
                Path.Combine(packageRoot, "cli.js"),
                "// deterministic Playwright MCP fixture",
                cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(packageRoot, "package.json"),
                "{\"version\":\"0.0.78\"}",
                cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(packageRoot, "runtime.js"),
                "// deterministic transitive runtime fixture",
                cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(versionRoot, "package-lock.json"),
                "{\"lockfileVersion\":3,\"packages\":{}}",
                cancellationToken);
            var now = DateTimeOffset.UtcNow;
            return new WorkspaceProcessExecutionResult(
                true,
                0,
                string.Empty,
                string.Empty,
                false,
                false,
                now,
                now,
                false,
                DescribeBoundary(),
                string.Empty);
        }
    }

    private sealed class ResidualProcessHost : IWorkspaceLongRunningProcessHost
    {
        public ResidualProcessSession Session { get; } = new();

        public ExecutionBoundaryDescriptor DescribeBoundary()
            => new("Test", "Test", "Test", "Test", "Test", false, "Test");

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IWorkspaceProcessSession> StartSessionAsync(
            WorkspaceProcessSessionRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IWorkspaceProcessSession>(Session);

        public Task<WorkspaceProcessTerminationResult> TerminateOwnedProcessAsync(
            WorkspaceOwnedProcessIdentity identity,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class ResidualProcessSession : IWorkspaceDuplexProcessSession
    {
        public int TerminateCount { get; private set; }

        public int DisposeCount { get; private set; }

        public WorkspaceOwnedProcessIdentity Identity { get; } = new(
            2,
            DateTimeOffset.UnixEpoch,
            new string('1', 64),
            new WorkspaceOwnedProcessBoundary(
                WorkspaceOwnedProcessBoundaryKind.UnixProcessGroup,
                2,
                Guid.Empty));

        public bool HasExited => false;

        public Stream StandardInput { get; } = new MemoryStream();

        public Stream StandardOutput { get; } = new MemoryStream();

        public WorkspaceProcessOutputSnapshot CaptureOutput()
            => new(string.Empty, string.Empty, false, false);

        public void CompleteStandardInput()
        {
        }

        public WorkspaceOwnedProcessIdentity Detach() => Identity;

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }

        public Task<WorkspaceProcessExecutionResult> TerminateAsync(
            WorkspaceProcessTerminationReason reason,
            string failureMessage,
            CancellationToken cancellationToken = default)
        {
            TerminateCount++;
            return Task.FromResult(Result(residualProcessPossible: true));
        }

        public Task<WorkspaceProcessExecutionResult> WaitForExitAsync(
            CancellationToken cancellationToken = default)
            => Task.FromException<WorkspaceProcessExecutionResult>(new OperationCanceledException());

        private static WorkspaceProcessExecutionResult Result(bool residualProcessPossible)
        {
            var now = DateTimeOffset.UtcNow;
            return new WorkspaceProcessExecutionResult(
                true,
                -1,
                string.Empty,
                string.Empty,
                false,
                false,
                now,
                now,
                false,
                new ExecutionBoundaryDescriptor("Test", "Test", "Test", "Test", "Test", false, "Test"),
                string.Empty,
                WorkspaceProcessTerminationReason.CallerCanceled,
                residualProcessPossible);
        }
    }

    private sealed class RecordingDuplexSession : IWorkspaceDuplexProcessSession
    {
        public WorkspaceOwnedProcessIdentity Identity { get; } = new(
            1,
            DateTimeOffset.UnixEpoch,
            new string('0', 64),
            new WorkspaceOwnedProcessBoundary(
                WorkspaceOwnedProcessBoundaryKind.UnixProcessGroup,
                1,
                Guid.Empty));

        public bool HasExited => false;

        public Stream StandardInput { get; } = new MemoryStream();

        public Stream StandardOutput { get; } = new MemoryStream();

        public WorkspaceProcessOutputSnapshot CaptureOutput()
            => new(string.Empty, string.Empty, false, false);

        public void CompleteStandardInput()
        {
        }

        public WorkspaceOwnedProcessIdentity Detach() => Identity;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<WorkspaceProcessExecutionResult> TerminateAsync(
            WorkspaceProcessTerminationReason reason,
            string failureMessage,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkspaceProcessExecutionResult> WaitForExitAsync(
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"CanDoItAll.McpPortabilityPolicyTests.{Guid.NewGuid():N}");
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

[CollectionDefinition("MCP portability environment", DisableParallelization = true)]
public sealed class McpPortabilityEnvironmentCollection;
