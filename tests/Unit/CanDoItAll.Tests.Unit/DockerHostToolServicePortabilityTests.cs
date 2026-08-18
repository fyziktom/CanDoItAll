using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Plugins;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Plugins.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.Infrastructure;

[Trait("Category", "PluginPortability")]
public sealed class DockerHostToolServicePortabilityTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        nameof(DockerHostToolServicePortabilityTests),
        Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string?> environment;
    private readonly LocalHostPlatform platform = LocalHostPlatformExtensions.CaptureCurrent();

    public DockerHostToolServicePortabilityTests()
    {
        Directory.CreateDirectory(root);
        string executablePath = Path.Combine(
            root,
            OperatingSystem.IsWindows() ? "docker.exe" : "docker");
        File.WriteAllText(executablePath, string.Empty);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                executablePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        environment = new Dictionary<string, string?>(platform.EnvironmentNameComparer())
        {
            ["PATH"] = root,
            ["PATHEXT"] = ".EXE;.CMD",
            ["HOME"] = root,
            ["UNRELATED_SECRET"] = "must-not-be-inherited"
        };
    }

    [Fact]
    public async Task Execute_uses_injected_host_and_redacts_docker_endpoint_and_config()
    {
        string dockerConfig = Directory.CreateDirectory(Path.Combine(root, "docker config")).FullName;
        const string dockerHost = "tcp://example.test:2376";
        environment["DOCKER_CONFIG"] = dockerConfig;
        environment["DOCKER_HOST"] = dockerHost;
        var host = new RecordingProcessHost(_ => Result(
            exitCode: 1,
            stdout: $"endpoint={dockerHost}",
            stderr: $"password=clear-text config={dockerConfig}"));
        DockerHostToolService service = CreateService(host);

        PluginHostToolExecutionResult result = await service.ExecuteAsync(
            DockerPluginConstants.PluginId,
            PluginHostToolRecipeIds.DockerListContainers,
            new Dictionary<string, string>(),
            timeoutSeconds: 30,
            maxOutputCharacters: 4096);

        WorkspaceProcessExecutionRequest request = Assert.Single(host.Requests);
        Assert.Equal("docker", request.ToolName);
        Assert.Equal(root, request.WorkingDirectory);
        Assert.True(request.EnvironmentVariables.ContainsKey("DOCKER_CONFIG"));
        Assert.True(request.EnvironmentVariables.ContainsKey("DOCKER_HOST"));
        Assert.False(request.EnvironmentVariables.ContainsKey("UNRELATED_SECRET"));
        Assert.DoesNotContain(dockerHost, result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(dockerHost, result.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain(dockerConfig, result.Stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("clear-text", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Probe_rejects_credential_bearing_or_component_ambiguous_endpoints_before_execution()
    {
        string[] invalidEndpoints =
        [
            "tcp://operator:super-secret@example.test:2376",
            "tcp://example.test:2376?context=unsafe",
            "ssh://example.test/#fragment"
        ];

        foreach (string endpoint in invalidEndpoints)
        {
            environment["DOCKER_HOST"] = endpoint;
            var host = new RecordingProcessHost(_ => throw new InvalidOperationException("must not execute"));

            DockerHostCapabilitySnapshot snapshot = await CreateService(host).ProbeAsync();

            Assert.Equal(DockerHostDependencyState.InvalidConfiguration, snapshot.Context);
            Assert.Empty(host.Requests);
            Assert.DoesNotContain(endpoint, snapshot.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Execute_redacts_host_equivalent_endpoint_and_path_spellings()
    {
        string dockerConfigRoot = Directory.CreateDirectory(Path.Combine(root, "DockerConfig")).FullName;
        string dockerConfig = dockerConfigRoot + Path.DirectorySeparatorChar;
        string dockerHost = OperatingSystem.IsWindows()
            ? "npipe:////./pipe/Docker_Engine"
            : "tcp://EXAMPLE.test:2376";
        environment["DOCKER_CONFIG"] = dockerConfig;
        environment["DOCKER_HOST"] = dockerHost;
        string equivalentConfig = OperatingSystem.IsWindows()
            ? dockerConfigRoot.ToUpperInvariant().Replace('\\', '/')
            : dockerConfigRoot;
        string equivalentHost = dockerHost.ToLowerInvariant();
        var host = new RecordingProcessHost(_ => Result(
            exitCode: 1,
            stderr: $"config={equivalentConfig} endpoint={equivalentHost}"));

        PluginHostToolExecutionResult result = await CreateService(host).ExecuteAsync(
            DockerPluginConstants.PluginId,
            PluginHostToolRecipeIds.DockerListContainers,
            new Dictionary<string, string>(),
            timeoutSeconds: 30,
            maxOutputCharacters: 4096);

        Assert.DoesNotContain(equivalentConfig, result.Stderr, StringComparison.Ordinal);
        Assert.DoesNotContain(equivalentHost, result.Stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unix_socket_endpoint_rejects_link_traversal_before_execution()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string socketRoot = Directory.CreateDirectory(Path.Combine(root, "socket-root")).FullName;
        string linkedRoot = Path.Combine(root, "linked-socket-root");
        Directory.CreateSymbolicLink(linkedRoot, socketRoot);
        environment["DOCKER_HOST"] = $"unix://{Path.Combine(linkedRoot, "docker.sock").Replace('\\', '/')}";
        var host = new RecordingProcessHost(_ => throw new InvalidOperationException("must not execute"));

        DockerHostCapabilitySnapshot snapshot = await CreateService(host).ProbeAsync();

        Assert.Equal(DockerHostDependencyState.InvalidConfiguration, snapshot.Context);
        Assert.Empty(host.Requests);
    }

    [Fact]
    public async Task Start_container_stops_when_container_inventory_is_indeterminate()
    {
        var host = new RecordingProcessHost(_ => Result(exitCode: 1, stderr: "daemon query failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteStartAsync(CreateService(host)));

        WorkspaceProcessExecutionRequest request = Assert.Single(host.Requests);
        Assert.Equal(["container", "ls", "--all", "--filter", "name=^/test-container$", "--format", "{{.Names}}"], request.Arguments);
    }

    [Theory]
    [InlineData("")]
    [InlineData("yes")]
    [InlineData(" true")]
    [InlineData("1")]
    public async Task Start_container_rejects_malformed_boolean_before_preflight(string value)
    {
        var host = new RecordingProcessHost(_ => throw new InvalidOperationException("must not execute"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ExecuteStartAsync(CreateService(host), pullIfMissing: value));

        Assert.Contains("'true' or 'false'", exception.Message, StringComparison.Ordinal);
        Assert.Empty(host.Requests);
    }

    [Fact]
    public async Task Start_container_rejects_excessive_port_mappings_before_preflight()
    {
        var host = new RecordingProcessHost(_ => throw new InvalidOperationException("must not execute"));
        string mappings = string.Join(',', Enumerable.Range(1, 17).Select(port => $"{port}:{port}"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ExecuteStartAsync(CreateService(host), portMappings: mappings));

        Assert.Contains("16-item limit", exception.Message, StringComparison.Ordinal);
        Assert.Empty(host.Requests);
    }

    [Theory]
    [InlineData("environmentVariables", 33)]
    [InlineData("labels", 33)]
    [InlineData("mounts", 17)]
    public async Task Start_container_bounds_reserved_structured_arguments_before_rejecting_them(
        string argumentName,
        int count)
    {
        var host = new RecordingProcessHost(_ => throw new InvalidOperationException("must not execute"));
        var arguments = CreateStartArguments();
        arguments[argumentName] = string.Join(',', Enumerable.Repeat("x", count));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(host).ExecuteAsync(
                DockerPluginConstants.PluginId,
                PluginHostToolRecipeIds.DockerStartContainer,
                arguments,
                timeoutSeconds: 60,
                maxOutputCharacters: 4096));

        Assert.Contains("item limit", exception.Message, StringComparison.Ordinal);
        Assert.Empty(host.Requests);
    }

    [Fact]
    public async Task Recipe_rejects_excessive_total_argument_bytes_before_execution()
    {
        var host = new RecordingProcessHost(_ => throw new InvalidOperationException("must not execute"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ExecuteLogsAsync(
                CreateService(host),
                new Dictionary<string, string>
                {
                    ["containerName"] = "test-container",
                    ["since"] = new string('x', 16 * 1024)
                }));

        Assert.Contains("byte argument limit", exception.Message, StringComparison.Ordinal);
        Assert.Empty(host.Requests);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1001")]
    [InlineData("12.5")]
    [InlineData(" 12")]
    public async Task Read_logs_rejects_malformed_or_out_of_range_tail(string tail)
    {
        var host = new RecordingProcessHost(_ => throw new InvalidOperationException("must not execute"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ExecuteLogsAsync(
                CreateService(host),
                new Dictionary<string, string>
                {
                    ["containerName"] = "test-container",
                    ["tail"] = tail
                }));

        Assert.Contains("integer from 1 through 1000", exception.Message, StringComparison.Ordinal);
        Assert.Empty(host.Requests);
    }

    [Theory]
    [InlineData("1h30m")]
    [InlineData("250ms")]
    [InlineData("2026-08-12T13:45:00Z")]
    [InlineData("2026-08-12T13:45:00.1234567-04:00")]
    public async Task Read_logs_accepts_bounded_duration_or_rfc3339_since(string since)
    {
        var host = new RecordingProcessHost(_ => Result(exitCode: 0));

        PluginHostToolExecutionResult result = await ExecuteLogsAsync(
            CreateService(host),
            new Dictionary<string, string>
            {
                ["containerName"] = "test-container",
                ["since"] = since
            });

        Assert.True(result.Succeeded);
        Assert.Equal(["logs", "--tail", "120", "--since", since, "test-container"], Assert.Single(host.Requests).Arguments);
    }

    [Theory]
    [InlineData("--follow")]
    [InlineData("31d")]
    [InlineData("721h")]
    [InlineData("2026-08-12 13:45:00Z")]
    [InlineData("2026-13-12T13:45:00Z")]
    public async Task Read_logs_rejects_invalid_or_option_like_since(string since)
    {
        var host = new RecordingProcessHost(_ => throw new InvalidOperationException("must not execute"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ExecuteLogsAsync(
                CreateService(host),
                new Dictionary<string, string>
                {
                    ["containerName"] = "test-container",
                    ["since"] = since
                }));

        Assert.Contains("'since' value is invalid", exception.Message, StringComparison.Ordinal);
        Assert.Empty(host.Requests);
    }

    [Fact]
    public async Task Start_container_stops_when_running_state_is_indeterminate()
    {
        var results = new Queue<WorkspaceProcessExecutionResult>(
        [
            Result(exitCode: 0, stdout: "test-container"),
            Result(exitCode: 1, stderr: "state unavailable")
        ]);
        var host = new RecordingProcessHost(_ => results.Dequeue());

        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteStartAsync(CreateService(host)));

        Assert.Equal(2, host.Requests.Count);
        Assert.DoesNotContain(host.Requests, request => request.Arguments.Contains("start"));
    }

    [Fact]
    public async Task Start_container_stops_when_image_inventory_is_indeterminate()
    {
        var results = new Queue<WorkspaceProcessExecutionResult>(
        [
            Result(exitCode: 0),
            Result(exitCode: 1, stderr: "image inventory unavailable")
        ]);
        var host = new RecordingProcessHost(_ => results.Dequeue());

        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteStartAsync(CreateService(host)));

        Assert.Equal(2, host.Requests.Count);
        Assert.DoesNotContain(host.Requests, request => request.Arguments.Contains("pull"));
        Assert.DoesNotContain(host.Requests, request => request.Arguments.Contains("run"));
    }

    [Fact]
    public async Task Start_container_mutates_only_after_authoritative_absence_queries()
    {
        var results = new Queue<WorkspaceProcessExecutionResult>(
        [
            Result(exitCode: 0),
            Result(exitCode: 0),
            Result(exitCode: 0, stdout: "pulled"),
            Result(exitCode: 0, stdout: "container-id")
        ]);
        var host = new RecordingProcessHost(_ => results.Dequeue());

        PluginHostToolExecutionResult result = await ExecuteStartAsync(CreateService(host));

        Assert.True(result.Succeeded);
        Assert.Equal(4, host.Requests.Count);
        Assert.Equal("container", host.Requests[0].Arguments[0]);
        Assert.Equal(["image", "ls", "--filter", "reference=alpine:latest", "--format", "{{.ID}}"], host.Requests[1].Arguments);
        Assert.Equal(["pull", "alpine:latest"], host.Requests[2].Arguments);
        Assert.Equal("run", host.Requests[3].Arguments[0]);
    }

    [Theory]
    [InlineData("true", "inspect")]
    [InlineData("false", "start")]
    public async Task Start_container_uses_authoritative_running_state_for_an_existing_container(
        string runningState,
        string expectedCommand)
    {
        var results = new Queue<WorkspaceProcessExecutionResult>(
        [
            Result(exitCode: 0, stdout: "test-container"),
            Result(exitCode: 0, stdout: runningState),
            Result(exitCode: 0, stdout: "container-id")
        ]);
        var host = new RecordingProcessHost(_ => results.Dequeue());

        PluginHostToolExecutionResult result = await ExecuteStartAsync(CreateService(host));

        Assert.True(result.Succeeded);
        Assert.Equal(3, host.Requests.Count);
        Assert.Equal(expectedCommand, host.Requests[2].Arguments[0]);
        Assert.DoesNotContain(host.Requests, request => request.Arguments.Contains("pull"));
        Assert.DoesNotContain(host.Requests, request => request.Arguments.Contains("run"));
    }

    [Fact]
    public async Task Start_container_runs_without_pull_when_image_inventory_authoritatively_finds_the_image()
    {
        var results = new Queue<WorkspaceProcessExecutionResult>(
        [
            Result(exitCode: 0),
            Result(exitCode: 0, stdout: "sha256:image"),
            Result(exitCode: 0, stdout: "container-id")
        ]);
        var host = new RecordingProcessHost(_ => results.Dequeue());

        PluginHostToolExecutionResult result = await ExecuteStartAsync(CreateService(host));

        Assert.True(result.Succeeded);
        Assert.Equal(3, host.Requests.Count);
        Assert.Equal("run", host.Requests[2].Arguments[0]);
        Assert.DoesNotContain(host.Requests, request => request.Arguments.Contains("pull"));
    }

    [Fact]
    public async Task Start_container_fails_without_mutation_when_image_is_absent_and_pull_is_disabled()
    {
        var results = new Queue<WorkspaceProcessExecutionResult>(
        [
            Result(exitCode: 0),
            Result(exitCode: 0)
        ]);
        var host = new RecordingProcessHost(_ => results.Dequeue());
        DockerHostToolService service = CreateService(host);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(
            DockerPluginConstants.PluginId,
            PluginHostToolRecipeIds.DockerStartContainer,
            new Dictionary<string, string>
            {
                ["image"] = "alpine:latest",
                ["containerName"] = "test-container",
                ["pullIfMissing"] = bool.FalseString,
                ["portMappings"] = string.Empty
            },
            timeoutSeconds: 60,
            maxOutputCharacters: 4096));

        Assert.Equal(2, host.Requests.Count);
        Assert.DoesNotContain(host.Requests, request => request.Arguments.Contains("pull"));
        Assert.DoesNotContain(host.Requests, request => request.Arguments.Contains("run"));
    }

    [Fact]
    public async Task Probe_reports_context_permission_separately_from_executable_and_daemon()
    {
        var host = new RecordingProcessHost(_ => Result(exitCode: 1, stderr: "permission denied"));
        DockerHostToolService service = CreateService(host);

        DockerHostCapabilitySnapshot snapshot = await service.ProbeAsync();

        Assert.Equal(DockerHostDependencyState.Available, snapshot.Executable);
        Assert.Equal(DockerHostDependencyState.PermissionDenied, snapshot.Context);
        Assert.Equal(DockerHostDependencyState.Unavailable, snapshot.Daemon);
        Assert.Equal(["context", "show"], Assert.Single(host.Requests).Arguments);
        Assert.DoesNotContain("permission denied", snapshot.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Probe_reports_a_missing_executable_without_starting_a_process()
    {
        environment["PATH"] = Path.Combine(root, "missing");
        var host = new RecordingProcessHost(_ => throw new InvalidOperationException("must not execute"));
        DockerHostToolService service = CreateService(host);

        DockerHostCapabilitySnapshot snapshot = await service.ProbeAsync();

        Assert.Equal(DockerHostDependencyState.Missing, snapshot.Executable);
        Assert.Equal(DockerHostDependencyState.Unavailable, snapshot.Context);
        Assert.Equal(DockerHostDependencyState.Unavailable, snapshot.Daemon);
        Assert.Empty(host.Requests);
    }

    [Fact]
    public async Task Probe_reports_a_context_timeout_without_probing_the_daemon()
    {
        var host = new RecordingProcessHost(_ => Result(exitCode: -1, timedOut: true));
        DockerHostToolService service = CreateService(host);

        DockerHostCapabilitySnapshot snapshot = await service.ProbeAsync();

        Assert.Equal(DockerHostDependencyState.Available, snapshot.Executable);
        Assert.Equal(DockerHostDependencyState.TimedOut, snapshot.Context);
        Assert.Equal(DockerHostDependencyState.Unavailable, snapshot.Daemon);
        Assert.Single(host.Requests);
    }

    [Fact]
    public async Task Probe_reports_remote_endpoint_and_daemon_unavailability_after_context_success()
    {
        environment["DOCKER_HOST"] = "ssh://example.test";
        var results = new Queue<WorkspaceProcessExecutionResult>(
        [
            Result(exitCode: 0, stdout: "remote"),
            Result(exitCode: 1, stderr: "cannot connect")
        ]);
        var host = new RecordingProcessHost(_ => results.Dequeue());
        DockerHostToolService service = CreateService(host);

        DockerHostCapabilitySnapshot snapshot = await service.ProbeAsync();

        Assert.Equal(DockerEndpointKind.Remote, snapshot.EndpointKind);
        Assert.Equal(DockerHostDependencyState.Available, snapshot.Context);
        Assert.Equal(DockerHostDependencyState.Unavailable, snapshot.Daemon);
        Assert.Equal(2, host.Requests.Count);
    }

    [Theory]
    [InlineData(DockerHostDependencyState.Missing, DockerHostDependencyState.Unavailable, DockerHostDependencyState.Unavailable, DockerEndpointKind.Default, false, "docker-executable-missing")]
    [InlineData(DockerHostDependencyState.Available, DockerHostDependencyState.InvalidConfiguration, DockerHostDependencyState.Unavailable, DockerEndpointKind.Default, false, "docker-context-invalidconfiguration")]
    [InlineData(DockerHostDependencyState.Available, DockerHostDependencyState.PermissionDenied, DockerHostDependencyState.Unavailable, DockerEndpointKind.LocalSocket, false, "docker-context-permissiondenied")]
    [InlineData(DockerHostDependencyState.Available, DockerHostDependencyState.TimedOut, DockerHostDependencyState.Unavailable, DockerEndpointKind.Remote, false, "docker-context-timedout")]
    [InlineData(DockerHostDependencyState.Available, DockerHostDependencyState.Available, DockerHostDependencyState.Unavailable, DockerEndpointKind.Default, false, "docker-daemon-unavailable")]
    [InlineData(DockerHostDependencyState.Available, DockerHostDependencyState.Available, DockerHostDependencyState.Available, DockerEndpointKind.Remote, true, "")]
    [InlineData(DockerHostDependencyState.Available, DockerHostDependencyState.Available, DockerHostDependencyState.Available, DockerEndpointKind.Default, true, "")]
    public async Task Runtime_availability_catalog_projects_typed_docker_host_state(
        DockerHostDependencyState executable,
        DockerHostDependencyState context,
        DockerHostDependencyState daemon,
        DockerEndpointKind endpoint,
        bool expectedRunnable,
        string expectedReason)
    {
        var snapshot = new DockerHostCapabilitySnapshot(
            executable,
            context,
            daemon,
            endpoint,
            expectedRunnable ? "Docker host is ready." : "Docker host is not ready.");
        var executor = new DockerListContainersWorkflowExecutor(
            new AllowingGrantEvaluator(),
            new RejectingHostToolService(),
            new StaticCapabilitySnapshotProvider(snapshot));
        var catalog = WorkflowExecutorCatalog.FromDescriptors([executor.Descriptor]);
        var runtimeCatalog = new WorkflowExecutorRuntimeAvailabilityCatalog(catalog, [executor]);

        WorkflowExecutorDescriptor descriptor = Assert.Single(await runtimeCatalog.ListExecutorsAsync());

        Assert.Equal(expectedRunnable, descriptor.Availability.IsRunnable);
        Assert.Equal(expectedReason, descriptor.Availability.ReasonCode);
        Assert.DoesNotContain("docker.sock", descriptor.Availability.Message, StringComparison.Ordinal);
        if (expectedRunnable)
        {
            Assert.Contains(endpoint.ToString(), descriptor.Availability.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Probe_rejects_foreign_docker_config_before_process_execution()
    {
        environment["DOCKER_CONFIG"] = OperatingSystem.IsWindows()
            ? "/var/lib/docker-config"
            : @"C:\Users\foreign\.docker";
        var host = new RecordingProcessHost(_ => throw new InvalidOperationException("must not execute"));
        DockerHostToolService service = CreateService(host);

        DockerHostCapabilitySnapshot snapshot = await service.ProbeAsync();

        Assert.Equal(DockerHostDependencyState.Available, snapshot.Executable);
        Assert.Equal(DockerHostDependencyState.InvalidConfiguration, snapshot.Context);
        Assert.Equal(DockerHostDependencyState.Unavailable, snapshot.Daemon);
        Assert.Empty(host.Requests);
    }

    [Fact]
    public async Task Probe_propagates_caller_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var host = new RecordingProcessHost((_, token) =>
        {
            token.ThrowIfCancellationRequested();
            return Result(exitCode: 0);
        });
        DockerHostToolService service = CreateService(host);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ProbeAsync(cancellation.Token));
    }

    [Fact]
    public void Plugin_composition_registers_one_shared_scoped_host_service_and_probe()
    {
        var services = new ServiceCollection();

        services.AddCanDoItAllDockerPlugin(
            registerBundledDescriptor: false,
            registerWorkflowExecutors: false);

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(DockerHostToolService));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IPluginHostToolService));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IDockerHostCapabilityProbe));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IDockerHostCapabilitySnapshotProvider));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(WorkspaceExecutableLocator));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(WorkspaceCommandEnvironmentPolicy));
        Assert.All(
            services.Where(descriptor => descriptor.ServiceType is
                { } serviceType &&
                (serviceType == typeof(DockerHostToolService) ||
                 serviceType == typeof(IPluginHostToolService) ||
                 serviceType == typeof(IDockerHostCapabilityProbe) ||
                 serviceType == typeof(IDockerHostCapabilitySnapshotProvider))),
            descriptor => Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private DockerHostToolService CreateService(IWorkspaceProcessHost processHost)
    {
        var locator = new WorkspaceExecutableLocator(
            platform,
            name => environment.TryGetValue(name, out string? value) ? value : null);
        var environmentPolicy = new WorkspaceCommandEnvironmentPolicy(platform, environment);
        return new DockerHostToolService(
            new StaticWorkspacePathResolver(root),
            processHost,
            locator,
            environmentPolicy,
            new PhysicalFileSystemPathPolicyFactory(),
            NullLogger<DockerHostToolService>.Instance);
    }

    private static Task<PluginHostToolExecutionResult> ExecuteStartAsync(
        DockerHostToolService service,
        string pullIfMissing = "True",
        string portMappings = "")
        => service.ExecuteAsync(
            DockerPluginConstants.PluginId,
            PluginHostToolRecipeIds.DockerStartContainer,
            CreateStartArguments(pullIfMissing, portMappings),
            timeoutSeconds: 60,
            maxOutputCharacters: 4096);

    private static Dictionary<string, string> CreateStartArguments(
        string pullIfMissing = "True",
        string portMappings = "")
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["image"] = "alpine:latest",
            ["containerName"] = "test-container",
            ["pullIfMissing"] = pullIfMissing,
            ["portMappings"] = portMappings
        };

    private static Task<PluginHostToolExecutionResult> ExecuteLogsAsync(
        DockerHostToolService service,
        IReadOnlyDictionary<string, string> arguments)
        => service.ExecuteAsync(
            DockerPluginConstants.PluginId,
            PluginHostToolRecipeIds.DockerReadLogs,
            arguments,
            timeoutSeconds: 30,
            maxOutputCharacters: 4096);

    private static WorkspaceProcessExecutionResult Result(
        int exitCode,
        string stdout = "",
        string stderr = "",
        bool timedOut = false)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new WorkspaceProcessExecutionResult(
            Started: true,
            ExitCode: exitCode,
            Stdout: stdout,
            Stderr: stderr,
            StdoutTruncated: false,
            StderrTruncated: false,
            StartedAtUtc: now,
            CompletedAtUtc: now,
            TimedOut: timedOut,
            Boundary: ExecutionBoundaryDescriptor.Unknown,
            FailureMessage: exitCode == 0 ? string.Empty : "Docker command failed.");
    }

    private sealed class RecordingProcessHost : IWorkspaceProcessHost
    {
        private readonly Func<WorkspaceProcessExecutionRequest, CancellationToken, WorkspaceProcessExecutionResult> execute;

        public RecordingProcessHost(Func<WorkspaceProcessExecutionRequest, WorkspaceProcessExecutionResult> execute)
            : this((request, _) => execute(request))
        {
        }

        public RecordingProcessHost(
            Func<WorkspaceProcessExecutionRequest, CancellationToken, WorkspaceProcessExecutionResult> execute)
        {
            this.execute = execute;
        }

        public List<WorkspaceProcessExecutionRequest> Requests { get; } = [];

        public ExecutionBoundaryDescriptor DescribeBoundary() => ExecutionBoundaryDescriptor.Unknown;

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(execute(request, cancellationToken));
        }
    }

    private sealed class StaticWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => workspaceRoot;

        public string ResolveExportsRoot() => workspaceRoot;

        public string ResolveEvidenceRoot() => workspaceRoot;

        public string ResolveManagerArtifactsRoot() => workspaceRoot;
    }

    private sealed class StaticCapabilitySnapshotProvider(DockerHostCapabilitySnapshot snapshot)
        : IDockerHostCapabilitySnapshotProvider
    {
        public Task<DockerHostCapabilitySnapshot> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(snapshot);
    }

    private sealed class AllowingGrantEvaluator : IPluginWorkflowExecutorGrantEvaluator
    {
        public PluginGrantDecision Evaluate(
            PluginId pluginId,
            PluginCapabilityKind capability,
            PluginHostToolRecipeId? recipeId = null)
            => PluginGrantDecision.Allow(pluginId, capability, recipeId);
    }

    private sealed class RejectingHostToolService : IPluginHostToolService
    {
        public Task<PluginHostToolExecutionResult> ExecuteAsync(
            PluginId pluginId,
            PluginHostToolRecipeId recipeId,
            IReadOnlyDictionary<string, string> arguments,
            int timeoutSeconds,
            int maxOutputCharacters,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Runtime availability must not execute a recipe.");
    }
}
