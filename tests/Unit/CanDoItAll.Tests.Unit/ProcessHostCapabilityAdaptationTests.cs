using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Composition;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.Processes;

[Trait("Category", "ProcessCapabilityPortability")]
public sealed class ProcessHostCapabilityAdaptationTests
{
    [Fact]
    public void Capability_identifiers_are_canonical_typed_values()
    {
        var upperCase = new ProcessHostCapabilityId("HOST.RUNTIME.PYTHON");

        Assert.Equal(ProcessHostCapabilityIds.PythonRuntime, upperCase);
        Assert.Equal("host.runtime.python", upperCase.Value);
        Assert.Throws<ArgumentException>(() => new ProcessHostCapabilityId("host/runtime/python"));
    }

    [Fact]
    public void Capability_evidence_policy_rejects_unbounded_duplicate_and_default_envelopes()
    {
        var fact = Available(
            ProcessHostCapabilityIds.DirectExecution,
            ProcessHostExecutionPort.ManagedProcessHost);
        var oversized = new ProcessHostCapabilityEvaluationEvidence(
            new ProcessHostProfileId("linux"),
            Enumerable.Repeat(fact, 10_000).ToArray());
        var duplicate = new ProcessHostCapabilityEvaluationEvidence(
            new ProcessHostProfileId("linux"),
            [fact, fact]);
        var defaultProfile = new ProcessHostCapabilityEvaluationEvidence(default, []);
        var nullFacts = new ProcessHostCapabilityEvaluationEvidence(
            new ProcessHostProfileId("linux"),
            null!);

        Assert.False(ProcessHostCapabilityEvidencePolicy.TryMerge(null, oversized, out _));
        Assert.False(ProcessHostCapabilityEvidencePolicy.TryMerge(null, duplicate, out _));
        Assert.False(ProcessHostCapabilityEvidencePolicy.TryMerge(null, defaultProfile, out _));
        Assert.False(ProcessHostCapabilityEvidencePolicy.TryMerge(null, nullFacts, out _));
        Assert.Empty(ProcessHostCapabilityEvidencePolicy.CreateUnstableEvidence(oversized, duplicate).Capabilities);
    }

    [Fact]
    public async Task Snapshot_provider_rejects_overbound_source_ownership_before_probe()
    {
        var source = new RecordingOverboundProcessHostCapabilitySource();
        var provider = new ProcessHostCapabilitySnapshotProvider(
            [source],
            [new StaticProcessHostProfileSource(new("linux"))]);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await provider.GetAsync());

        Assert.False(source.WasProbed);
    }

    [Fact]
    public async Task Snapshot_provider_rejects_oversized_probe_result_before_enumeration()
    {
        var provider = new ProcessHostCapabilitySnapshotProvider(
            [new OversizedResultProcessHostCapabilitySource()],
            [new StaticProcessHostProfileSource(new("linux"))]);

        var snapshot = await provider.GetAsync();

        var fact = Assert.Single(snapshot.Capabilities);
        Assert.Equal(ProcessHostCapabilityReason.InvalidConfiguration, fact.Reason);
        Assert.False(fact.IsAvailable);
    }

    [Fact]
    public async Task Snapshot_provider_rejects_duplicate_host_authority_reports()
    {
        var fact = Available(
            ProcessHostCapabilityIds.DirectExecution,
            ProcessHostExecutionPort.ManagedProcessHost);
        var provider = new ProcessHostCapabilitySnapshotProvider(
        [
            new StaticProcessHostCapabilitySource([fact], "test-source-a"),
            new StaticProcessHostCapabilitySource([fact], "test-source-b")
        ],
        [new StaticProcessHostProfileSource(new("linux"))]);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.GetAsync());

        Assert.Contains(ProcessHostCapabilityIds.DirectExecution.Value, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Managed_runtime_source_reports_current_profile_and_bounded_execution_ports()
    {
        var source = new ManagedRuntimeProcessHostCapabilitySource(
            [new StubWorkspaceProcessHost()],
            [],
            [new WorkspaceExecutableLocator()]);
        var provider = new ProcessHostCapabilitySnapshotProvider(
            [source],
            [new StaticProcessHostProfileSource(ExpectedCurrentProfile())]);

        var snapshot = await provider.GetAsync();

        Assert.Equal(ExpectedCurrentProfile(), snapshot.ProfileId);
        Assert.Equal(snapshot.Capabilities.Count, snapshot.Capabilities.Select(fact => fact.Id).Distinct().Count());
        Assert.Contains(snapshot.Capabilities, fact =>
            fact.Id == ProcessHostCapabilityIds.DirectExecution &&
            fact.IsAvailable &&
            fact.ExecutionPort == ProcessHostExecutionPort.ManagedProcessHost);
        Assert.Contains(snapshot.Capabilities, fact =>
            fact.Id == ProcessHostCapabilityIds.DotNetRuntime && fact.IsAvailable);
        Assert.Contains(snapshot.Capabilities, fact =>
            fact.Id == ProcessHostCapabilityIds.LocalStdioMcp &&
            fact.Availability == ProcessHostCapabilityAvailability.Unavailable &&
            fact.Reason == ProcessHostCapabilityReason.NotRegistered);
        var serialized = JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain(Environment.CurrentDirectory, serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ProcessHostCapabilityAvailability.Available, ProcessHostCapabilityReason.NotRegistered, ProcessHostExecutionPort.ManagedProcessHost)]
    [InlineData(ProcessHostCapabilityAvailability.Available, ProcessHostCapabilityReason.Ready, ProcessHostExecutionPort.None)]
    [InlineData(ProcessHostCapabilityAvailability.Unavailable, ProcessHostCapabilityReason.Ready, ProcessHostExecutionPort.None)]
    [InlineData(ProcessHostCapabilityAvailability.Unavailable, ProcessHostCapabilityReason.DependencyMissing, ProcessHostExecutionPort.ManagedProcessHost)]
    public async Task Snapshot_provider_rejects_contradictory_capability_facts(
        ProcessHostCapabilityAvailability availability,
        ProcessHostCapabilityReason reason,
        ProcessHostExecutionPort executionPort)
    {
        var provider = new ProcessHostCapabilitySnapshotProvider(
            [new StaticProcessHostCapabilitySource(
            [
                new ProcessHostCapabilityFact(
                    ProcessHostCapabilityIds.DirectExecution,
                    availability,
                    reason,
                    executionPort)
            ])],
            [new StaticProcessHostProfileSource(new("linux"))]);

        var snapshot = await provider.GetAsync();

        var fact = Assert.Single(snapshot.Capabilities);
        Assert.False(fact.IsAvailable);
        Assert.Equal(ProcessHostCapabilityReason.InvalidConfiguration, fact.Reason);
        Assert.Equal(ProcessHostExecutionPort.None, fact.ExecutionPort);
    }

    [Fact]
    public async Task Snapshot_provider_contains_throwing_source_and_profile_without_disclosing_exception_text()
    {
        const string sentinel = "secret: C:\\private\\host-probe";
        var provider = new ProcessHostCapabilitySnapshotProvider(
            [new ThrowingProcessHostCapabilitySource(sentinel)],
            [new ThrowingProcessHostProfileSource(sentinel)]);

        var snapshot = await provider.GetAsync();

        Assert.Equal(ProcessHostCapabilitySnapshot.Unknown.ProfileId, snapshot.ProfileId);
        var fact = Assert.Single(snapshot.Capabilities);
        Assert.Equal(ProcessHostCapabilityIds.Docker, fact.Id);
        Assert.Equal(ProcessHostCapabilityReason.Unavailable, fact.Reason);
        Assert.DoesNotContain(sentinel, JsonSerializer.Serialize(snapshot), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Snapshot_provider_contains_internal_probe_cancellation_but_preserves_caller_cancellation()
    {
        var provider = new ProcessHostCapabilitySnapshotProvider(
            [new InternallyCanceledProcessHostCapabilitySource()],
            [new InternallyCanceledProcessHostProfileSource()]);

        var snapshot = await provider.GetAsync();

        Assert.Equal(ProcessHostCapabilitySnapshot.Unknown.ProfileId, snapshot.ProfileId);
        var fact = Assert.Single(snapshot.Capabilities);
        Assert.Equal(ProcessHostCapabilityIds.Docker, fact.Id);
        Assert.Equal(ProcessHostCapabilityReason.TimedOut, fact.Reason);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await provider.GetAsync(cancellation.Token));
    }

    [Fact]
    public async Task Snapshot_provider_rejects_wrong_builtin_execution_port_as_invalid_configuration()
    {
        var provider = new ProcessHostCapabilitySnapshotProvider(
            [new StaticProcessHostCapabilitySource(
            [
                new ProcessHostCapabilityFact(
                    ProcessHostCapabilityIds.Docker,
                    ProcessHostCapabilityAvailability.Available,
                    ProcessHostCapabilityReason.Ready,
                    ProcessHostExecutionPort.DesktopLauncher)
            ])],
            [new StaticProcessHostProfileSource(new("linux"))]);

        var snapshot = await provider.GetAsync();

        var fact = Assert.Single(snapshot.Capabilities);
        Assert.Equal(ProcessHostCapabilityReason.InvalidConfiguration, fact.Reason);
        Assert.False(fact.IsAvailable);
    }

    [Fact]
    public async Task Standard_catalog_and_runtime_snapshot_share_the_canonical_adapter_source()
    {
        var executionDriver = new StubProcessStepExecutionDriver();
        var snapshotProvider = new ProcessHostCapabilitySnapshotProvider(
            [new StandardProcessAdapterHostCapabilitySource(
            [
                new StandardProcessAdapterCompositionRegistration(
                    AgentFrameworkProcessExecutionAdapter.DriverDescriptor)
            ])],
            [new StaticProcessHostProfileSource(new("linux"))]);
        var provider = new StandardProcessLaunchDriverCatalogProvider(
            executionDriver,
            snapshotProvider);

        var catalog = await provider.LoadAsync();
        var runtimeSnapshot = await snapshotProvider.GetAsync();
        var adapter = Assert.Single(
            catalog.HostCapabilities.Capabilities,
            fact => fact.Id == ProcessHostCapabilityIds.ManagedProcessAdapter);
        var match = catalog.DriverCatalog.Match(new ProcessCapabilityRequest(
            catalog.RequiredCapabilityTags,
            new HashSet<CapabilityTag>(),
            new HashSet<CapabilityTag>())
        {
            HostCapabilities = catalog.HostCapabilities
        });

        Assert.True(adapter.IsAvailable);
        Assert.Equal(ProcessHostExecutionPort.ManagedProcessAdapter, adapter.ExecutionPort);
        Assert.Equal(
            adapter,
            Assert.Single(
                runtimeSnapshot.Capabilities,
                fact => fact.Id == ProcessHostCapabilityIds.ManagedProcessAdapter));
        Assert.True(match.Succeeded, string.Join(", ", match.Diagnostics));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Standard_adapter_source_fails_closed_without_exactly_one_composition_registration(
        int registrationCount)
    {
        var source = new StandardProcessAdapterHostCapabilitySource(
            Enumerable.Range(0, registrationCount)
                .Select(_ => new StandardProcessAdapterCompositionRegistration(
                    AgentFrameworkProcessExecutionAdapter.DriverDescriptor)));

        var fact = Assert.Single(await source.ProbeAsync());

        Assert.Equal(ProcessHostCapabilityIds.ManagedProcessAdapter, fact.Id);
        Assert.False(fact.IsAvailable);
        Assert.Equal(ProcessHostCapabilityReason.InvalidConfiguration, fact.Reason);
        Assert.Equal(ProcessHostExecutionPort.None, fact.ExecutionPort);
    }

    [Fact]
    public async Task Standard_runtime_resolver_rejects_same_strategy_with_stale_driver_factory_or_schema_binding()
    {
        var driver = new StubProcessStepExecutionDriver();
        var resolver = new StandardProcessRuntimeStrategyFactoryResolver(driver);
        var binding = CreateStandardBinding(driver);

        Assert.NotNull(await resolver.ResolveAsync(binding));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await resolver.ResolveAsync(binding with { DriverId = StandardProcessAdapterDriverIds.Foundation }));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await resolver.ResolveAsync(binding with { FactoryVersion = "builder/0.9" }));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await resolver.ResolveAsync(binding with { MinRuntimeSchema = "runtime/0.9" }));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await resolver.ResolveAsync(binding with { MaxRuntimeSchema = "runtime/9.x" }));
    }

    [Fact]
    public async Task Application_adapter_projects_desktop_and_terminal_facts_without_host_details()
    {
        const string sentinel = "secret: application-host-detail";
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddRuntimeHostPlatformComposition(
            configuration,
            new TestHostEnvironment(Environment.CurrentDirectory, "CanDoItAll.B06.Tests"));
        services.AddSingleton<IHostCapabilitySnapshotProvider>(
            new StaticApplicationHostCapabilitySnapshotProvider(
                new HostCapabilitySnapshot(
                    RuntimeHostProfileKind.WindowsInteractive,
                    RuntimeHostOperatingSystem.Windows,
                    IsInteractive: true,
                    IsReady: true,
                    DateTimeOffset.UtcNow,
                    [],
                    [
                        ApplicationFact(
                            HostCapabilityId.DesktopFileOpen,
                            HostCapabilityAvailability.Available,
                            HostCapabilityReasonCode.Ready,
                            sentinel),
                        ApplicationFact(
                            HostCapabilityId.InteractiveTerminal,
                            HostCapabilityAvailability.Unsupported,
                            HostCapabilityReasonCode.DisabledByProfile,
                            sentinel)
                    ])));
        await using var serviceProvider = services.BuildServiceProvider();
        var source = Assert.Single(serviceProvider.GetServices<IProcessHostCapabilitySource>());

        var facts = await source.ProbeAsync();

        Assert.Contains(facts, fact =>
            fact.Id == ProcessHostCapabilityIds.DesktopOpen &&
            fact.IsAvailable &&
            fact.ExecutionPort == ProcessHostExecutionPort.DesktopLauncher);
        Assert.Contains(facts, fact =>
            fact.Id == ProcessHostCapabilityIds.InteractiveTerminal &&
            fact.Availability == ProcessHostCapabilityAvailability.Unsupported &&
            fact.ExecutionPort == ProcessHostExecutionPort.None);
        Assert.DoesNotContain(sentinel, JsonSerializer.Serialize(facts), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Application_adapter_maps_internal_probe_cancellation_to_timed_out_facts()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddRuntimeHostPlatformComposition(
            configuration,
            new TestHostEnvironment(Environment.CurrentDirectory, "CanDoItAll.B06.Tests"));
        services.AddSingleton<IHostCapabilitySnapshotProvider>(
            new ThrowingApplicationHostCapabilitySnapshotProvider(new OperationCanceledException()));
        await using var serviceProvider = services.BuildServiceProvider();
        var source = Assert.Single(serviceProvider.GetServices<IProcessHostCapabilitySource>());

        var facts = await source.ProbeAsync();

        Assert.Equal(2, facts.Count);
        Assert.All(facts, fact =>
        {
            Assert.Equal(ProcessHostCapabilityAvailability.Unavailable, fact.Availability);
            Assert.Equal(ProcessHostCapabilityReason.TimedOut, fact.Reason);
        });
    }

    [Fact]
    public async Task Docker_adapter_projects_typed_availability_without_probe_message()
    {
        const string sentinel = "secret: docker-probe-detail";
        var services = new ServiceCollection();
        services.AddCanDoItAllDockerPlugin(
            registerBundledDescriptor: false,
            registerWorkflowExecutors: false);
        services.AddScoped<IDockerHostCapabilitySnapshotProvider>(_ =>
            new StaticDockerHostCapabilitySnapshotProvider(
                new DockerHostCapabilitySnapshot(
                    DockerHostDependencyState.Available,
                    DockerHostDependencyState.Available,
                    DockerHostDependencyState.PermissionDenied,
                    DockerEndpointKind.LocalSocket,
                    sentinel)));
        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var source = Assert.Single(scope.ServiceProvider.GetServices<IProcessHostCapabilitySource>());

        var fact = Assert.Single(await source.ProbeAsync());

        Assert.Equal(ProcessHostCapabilityIds.Docker, fact.Id);
        Assert.Equal(ProcessHostCapabilityAvailability.Unavailable, fact.Availability);
        Assert.Equal(ProcessHostCapabilityReason.PermissionDenied, fact.Reason);
        Assert.Equal(ProcessHostExecutionPort.None, fact.ExecutionPort);
        Assert.DoesNotContain(sentinel, JsonSerializer.Serialize(fact), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Docker_adapter_maps_internal_probe_cancellation_to_timed_out_fact()
    {
        var services = new ServiceCollection();
        services.AddCanDoItAllDockerPlugin(
            registerBundledDescriptor: false,
            registerWorkflowExecutors: false);
        services.AddScoped<IDockerHostCapabilitySnapshotProvider>(_ =>
            new ThrowingDockerHostCapabilitySnapshotProvider(new OperationCanceledException()));
        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var source = Assert.Single(scope.ServiceProvider.GetServices<IProcessHostCapabilitySource>());

        var fact = Assert.Single(await source.ProbeAsync());

        Assert.Equal(ProcessHostCapabilityAvailability.Unavailable, fact.Availability);
        Assert.Equal(ProcessHostCapabilityReason.TimedOut, fact.Reason);
    }

    private static ProcessHostProfileId ExpectedCurrentProfile()
        => new(
            OperatingSystem.IsWindows()
                ? "windows"
                : OperatingSystem.IsLinux()
                    ? "linux"
                    : OperatingSystem.IsMacOS()
                        ? "macos"
                        : "unknown");

    private static ProcessHostCapabilityFact Available(
        ProcessHostCapabilityId id,
        ProcessHostExecutionPort executionPort)
        => new(
            id,
            ProcessHostCapabilityAvailability.Available,
            ProcessHostCapabilityReason.Ready,
            executionPort);

    private static HostCapabilityDescriptor ApplicationFact(
        HostCapabilityId id,
        HostCapabilityAvailability availability,
        HostCapabilityReasonCode reason,
        string sentinel)
        => new(
            id,
            HostCapabilityCriticality.Optional,
            availability,
            reason,
            sentinel,
            HostCapabilitySupportLevel.Stable,
            HostCapabilityImplementationRegistration.Registered,
            sentinel,
            sentinel,
            HostCapabilityExecutionBoundary.OperatingSystem,
            RuntimeHostProfileKind.WindowsInteractive,
            DateTimeOffset.UtcNow);

    private sealed class StaticProcessHostCapabilitySource(
        IReadOnlyList<ProcessHostCapabilityFact> facts,
        string sourceId = "test-source") : IProcessHostCapabilitySource
    {
        public ProcessHostCapabilitySourceId SourceId { get; } = new(sourceId);

        public IReadOnlySet<ProcessHostCapabilityId> DeclaredCapabilities { get; } =
            facts.Select(fact => fact.Id).ToHashSet();

        public ValueTask<IReadOnlyList<ProcessHostCapabilityFact>> ProbeAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(facts);
        }
    }

    private sealed class ThrowingProcessHostCapabilitySource(string message) : IProcessHostCapabilitySource
    {
        public ProcessHostCapabilitySourceId SourceId { get; } = new("throwing-source");

        public IReadOnlySet<ProcessHostCapabilityId> DeclaredCapabilities { get; } =
            new HashSet<ProcessHostCapabilityId> { ProcessHostCapabilityIds.Docker };

        public ValueTask<IReadOnlyList<ProcessHostCapabilityFact>> ProbeAsync(
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(message);
    }

    private sealed class ThrowingProcessHostProfileSource(string message) : IProcessHostProfileSource
    {
        public ValueTask<ProcessHostProfileId> GetProfileIdAsync(
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(message);
    }

    private sealed class InternallyCanceledProcessHostCapabilitySource : IProcessHostCapabilitySource
    {
        public ProcessHostCapabilitySourceId SourceId { get; } = new("internally-canceled-source");

        public IReadOnlySet<ProcessHostCapabilityId> DeclaredCapabilities { get; } =
            new HashSet<ProcessHostCapabilityId> { ProcessHostCapabilityIds.Docker };

        public ValueTask<IReadOnlyList<ProcessHostCapabilityFact>> ProbeAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException("Internal probe timeout.");
        }
    }

    private sealed class RecordingOverboundProcessHostCapabilitySource : IProcessHostCapabilitySource
    {
        public bool WasProbed { get; private set; }

        public ProcessHostCapabilitySourceId SourceId { get; } = new("overbound-source");

        public IReadOnlySet<ProcessHostCapabilityId> DeclaredCapabilities { get; } = Enumerable
            .Range(0, ProcessHostCapabilitySnapshot.MaximumCapabilities + 1)
            .Select(index => new ProcessHostCapabilityId($"host.test.capability-{index}"))
            .ToHashSet();

        public ValueTask<IReadOnlyList<ProcessHostCapabilityFact>> ProbeAsync(
            CancellationToken cancellationToken = default)
        {
            WasProbed = true;
            return ValueTask.FromResult<IReadOnlyList<ProcessHostCapabilityFact>>([]);
        }
    }

    private sealed class OversizedResultProcessHostCapabilitySource : IProcessHostCapabilitySource
    {
        public ProcessHostCapabilitySourceId SourceId { get; } = new("oversized-result-source");

        public IReadOnlySet<ProcessHostCapabilityId> DeclaredCapabilities { get; } =
            new HashSet<ProcessHostCapabilityId> { ProcessHostCapabilityIds.Docker };

        public ValueTask<IReadOnlyList<ProcessHostCapabilityFact>> ProbeAsync(
            CancellationToken cancellationToken = default)
        {
            var fact = Available(
                ProcessHostCapabilityIds.Docker,
                ProcessHostExecutionPort.DockerHostTool);
            return ValueTask.FromResult<IReadOnlyList<ProcessHostCapabilityFact>>(
                Enumerable.Repeat(fact, 10_000).ToArray());
        }
    }

    private sealed class InternallyCanceledProcessHostProfileSource : IProcessHostProfileSource
    {
        public ValueTask<ProcessHostProfileId> GetProfileIdAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException("Internal profile timeout.");
        }
    }

    private sealed class StaticProcessHostCapabilitySnapshotProvider(
        ProcessHostCapabilitySnapshot snapshot) : IProcessHostCapabilitySnapshotProvider
    {
        public ValueTask<ProcessHostCapabilitySnapshot> GetAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(snapshot);
        }
    }

    private sealed class StaticProcessHostProfileSource(
        ProcessHostProfileId profileId) : IProcessHostProfileSource
    {
        public ValueTask<ProcessHostProfileId> GetProfileIdAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(profileId);
        }
    }

    private sealed class StubProcessStepExecutionDriver : IProcessStepExecutionDriver
    {
        public ProcessStepExecutionDriverDescriptor Descriptor { get; } = new(
            StandardProcessAdapterDriverIds.Workflow,
            StandardProcessAdapterDescriptors.WorkflowAdapter,
            StandardProcessAdapterDescriptors.WorkflowAdapter.Strategy);

        public ValueTask<ProcessExecutionAdapterResult> ExecuteStepAsync(
            ProcessExecutionAdapterRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Catalog construction must not execute a process step.");
    }

    private static ProcessStrategyBindingSnapshot CreateStandardBinding(
        IProcessStepExecutionDriver executionDriver)
    {
        var descriptor = executionDriver.Descriptor;
        return new ProcessStrategyBindingSnapshot(
            descriptor.DriverId,
            descriptor.Strategy.StrategyId,
            descriptor.Strategy.StrategyVersion,
            ProcessStrategyBindingVersions.ForDriver(
                StandardProcessAdapterDriverPackageFactory.DriverVersion),
            StandardProcessAdapterDriverPackageFactory.MinimumRuntimeSchema,
            StandardProcessAdapterDriverPackageFactory.MaximumRuntimeSchema,
            "sha256:binding",
            [])
        {
            HostProfileId = new ProcessHostProfileId("linux"),
            HostCapabilities =
            [
                Available(
                    ProcessHostCapabilityIds.ManagedProcessAdapter,
                    ProcessHostExecutionPort.ManagedProcessAdapter)
            ]
        };
    }

    private sealed class StubWorkspaceProcessHost : IWorkspaceProcessHost
    {
        public ExecutionBoundaryDescriptor DescribeBoundary() => ExecutionBoundaryDescriptor.Unknown;

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The capability probe must not execute a child process.");
    }

    private sealed class StaticApplicationHostCapabilitySnapshotProvider(
        HostCapabilitySnapshot snapshot) : IHostCapabilitySnapshotProvider
    {
        public HostCapabilitySnapshot GetSnapshot() => snapshot;
    }

    private sealed class ThrowingApplicationHostCapabilitySnapshotProvider(
        Exception exception) : IHostCapabilitySnapshotProvider
    {
        public HostCapabilitySnapshot GetSnapshot() => throw exception;
    }

    private sealed class StaticDockerHostCapabilitySnapshotProvider(
        DockerHostCapabilitySnapshot snapshot) : IDockerHostCapabilitySnapshotProvider
    {
        public Task<DockerHostCapabilitySnapshot> GetAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(snapshot);
        }
    }

    private sealed class ThrowingDockerHostCapabilitySnapshotProvider(
        Exception exception) : IDockerHostCapabilitySnapshotProvider
    {
        public Task<DockerHostCapabilitySnapshot> GetAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromException<DockerHostCapabilitySnapshot>(exception);
        }
    }
}
