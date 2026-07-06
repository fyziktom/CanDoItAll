using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Access;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class MafRuntimeArchitectureServicesTests
{
    [Fact]
    public void MafRuntimeArchitectureServices_registers_runtime_collaborators()
    {
        var services = new ServiceCollection();

        services.AddMafRuntimeArchitectureServices();

        using var provider = services.BuildServiceProvider();
        Assert.IsType<MafRuntimeDependencyResolver>(provider.GetRequiredService<IMafRuntimeDependencyResolver>());
        Assert.IsType<MafProviderCredentialService>(provider.GetRequiredService<IMafProviderCredentialService>());
        Assert.IsType<MafProviderAgentFactory>(provider.GetRequiredService<IMafProviderAgentFactory>());
        Assert.IsType<MafProviderStreamingRunner>(provider.GetRequiredService<IMafProviderStreamingRunner>());
        Assert.IsType<RuntimeToolProviderComposer>(provider.GetRequiredService<IRuntimeToolProviderComposer>());
        Assert.IsType<RuntimeToolProviderAccessFilter>(provider.GetRequiredService<IRuntimeToolProviderAccessFilter>());
        Assert.IsType<NoOpMafRuntimeCompositionMetrics>(provider.GetRequiredService<IMafRuntimeCompositionMetrics>());
    }

    [Fact]
    public void MafRuntimeDependencyResolver_prefers_registered_provider_dependencies()
    {
        var gateway = new TestMafProviderRuntimeGateway();
        var streamingGate = new TestMafProviderStreamingDispatchGate();
        var services = new ServiceCollection();
        services.AddSingleton<IMafProviderRuntimeGateway>(gateway);
        services.AddSingleton<IMafProviderStreamingDispatchGate>(streamingGate);
        using var provider = services.BuildServiceProvider();
        var resolver = new MafRuntimeDependencyResolver();

        var dependencies = resolver.ResolveProviderDependencies(provider);

        Assert.Same(gateway, dependencies.ProviderRuntimeGateway);
        Assert.Same(streamingGate, dependencies.ProviderStreamingDispatchGate);
    }

    [Fact]
    public void RuntimeToolProviderComposer_orders_registrations_deterministically()
    {
        var composer = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter());
        var late = new TestRuntimeToolProvider(
            20,
            CreateDescriptor("tests.late"),
            "late_runtime_tool");
        var early = new TestRuntimeToolProvider(
            10,
            CreateDescriptor("tests.early"),
            "early_runtime_tool");

        var registrations = composer.ComposeRegistrations([late, early]);

        Assert.Equal(["tests.early", "tests.late"], registrations.Select(item => item.Descriptor.ProviderKey));
    }

    [Fact]
    public void RuntimeToolProviderComposer_rejects_duplicate_provider_keys()
    {
        var composer = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            composer.ComposeRegistrations(
            [
                new TestRuntimeToolProvider(10, CreateDescriptor("tests.duplicate"), "first_tool"),
                new TestRuntimeToolProvider(20, CreateDescriptor("tests.duplicate"), "second_tool")
            ]));

        Assert.Contains("Runtime tool provider key(s) must be unique", exception.Message, StringComparison.Ordinal);
        Assert.Contains("tests.duplicate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeToolProviderComposer_attaches_tools_metadata_and_approval_wrappers()
    {
        var composer = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter());
        var provider = new TestRuntimeToolProvider(
            10,
            CreateDescriptor("tests.process"),
            "processes_runs_list",
            "processes_run_start");
        var registrations = composer.ComposeRegistrations([provider]);
        var state = new RuntimeCapabilityState();
        var accessPlan = CreateAllowAllAccessPlan();

        var result = await composer.AttachAsync(
            new RuntimeToolProviderAttachmentRequest(
                state,
                accessPlan,
                registrations,
                CreateContext(),
                SuppressApprovalRequirements: false),
            CancellationToken.None);

        Assert.Equal(2, result.AttachedToolCount);
        Assert.Equal("tests.process", Assert.Single(state.RuntimeToolProviderDescriptors).ProviderKey);
        Assert.Equal(["processes_runs_list", "processes_run_start"], state.Tools.Select(tool => tool.Name));
        Assert.IsNotType<ApprovalRequiredAIFunction>(Assert.Single(state.Tools, tool => tool.Name == "processes_runs_list"));
        Assert.IsType<ApprovalRequiredAIFunction>(Assert.Single(state.Tools, tool => tool.Name == "processes_run_start"));
        Assert.Contains(state.RuntimeToolMetadata, metadata =>
            metadata.ToolName == "processes_run_start" &&
            metadata.OperationKind == AgentRuntimeToolOperationKind.Mutation);
        Assert.Contains("Attached 2 tool(s) from 1 registered runtime tool provider(s).", result.ProgressMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void MafProviderCredentialService_resolves_configuration_credentials()
    {
        var key = $"UNIT_TEST_OPENAI_KEY_{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [key] = "unit-test-secret"
            })
            .Build());
        services.AddMafRuntimeArchitectureServices();
        using var provider = services.BuildServiceProvider();
        var credentialService = provider.GetRequiredService<IMafProviderCredentialService>();

        var resolution = credentialService.Resolve(CreateProviderProfile(apiKeyEnvironmentVariable: key));

        Assert.True(resolution.IsResolved);
        Assert.Equal("unit-test-secret", resolution.ApiKey);
        Assert.Contains(key, resolution.ResolutionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolInvocationTraceRecorder_records_completion_state()
    {
        var recorder = new ToolInvocationTraceRecorder();

        var sequence = recorder.Start(
            "workspace_write_file",
            ToolInvocationClassification.Mutation,
            "signature",
            new AgentRuntimeToolOwnership("provider.key", "Provider", "workspace_write_file"));
        recorder.Complete(sequence, succeeded: false, "denied");

        var trace = Assert.Single(recorder.Snapshot());
        Assert.Equal("workspace_write_file", trace.ToolName);
        Assert.Equal(ToolInvocationClassification.Mutation, trace.Classification);
        Assert.False(trace.Succeeded);
        Assert.Equal("denied", trace.FailureMessage);
        Assert.Equal("provider.key", trace.RuntimeToolProviderKey);
    }

    [Fact]
    public void InMemoryMafRuntimeCompositionMetrics_records_measurements()
    {
        var metrics = new InMemoryMafRuntimeCompositionMetrics();
        var measurement = new MafRuntimeCompositionMeasurement(
            "capability.test",
            TimeSpan.FromMilliseconds(5),
            "unit-test");

        metrics.Record(measurement);

        Assert.Equal([measurement], metrics.Snapshot());
    }

    private static RuntimeCapabilityAccessPlan CreateAllowAllAccessPlan()
        => new(
            new EffectiveCapabilitySet([], []),
            AllowedCatalogCapabilities: [],
            Policies: [],
            new CapabilityAccessPolicyEvaluator(),
            InitialAllowedCapabilities: [],
            InitialDiagnostics: [],
            CatalogCapabilitiesByIdentity: new Dictionary<CapabilityIdentity, CapabilityCatalogItem>(),
            DescriptorsByKey: new Dictionary<string, CapabilityExposureDescriptor>(StringComparer.OrdinalIgnoreCase),
            CorrelationId: "unit-test");

    private static AgentRuntimeToolProviderContext CreateContext()
        => new(
            CreateAgent(),
            CreateProviderProfile(),
            Capabilities: [],
            SuppressApprovalRequirements: false,
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            RuntimeSessionKey: string.Empty,
            AgentRuntimeContextIntent.Empty,
            Tags: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private static AgentRuntimeToolProviderDescriptor CreateDescriptor(string providerKey)
        => new(
            providerKey,
            $"Test provider {providerKey}",
            "Test runtime provider.",
            ["tests"],
            [AgentRuntimeToolProviderPurpose.InteractiveChat]);

    private static AgentDefinition CreateAgent()
        => new(
            Id: Guid.NewGuid(),
            Name: "Runtime Architecture Agent",
            RoleTitle: "Tester",
            Summary: "Tests runtime architecture services.",
            Instructions: "Use supplied tools.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: Guid.NewGuid(),
            Model: string.Empty,
            Workload: AgentWorkloadKind.Programming,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default with
            {
                CanUseTools = true,
                CanAskOtherAgents = false,
                RequiresApprovalForExternalCalls = false
            },
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow);

    private static ProviderProfile CreateProviderProfile(
        string apiKeyEnvironmentVariable = "OPENAI_API_KEY")
        => new(
            Guid.NewGuid(),
            "Unit Provider",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            apiKeyEnvironmentVariable,
            "gpt-4.1",
            ProviderTransportKind.ChatCompletions,
            true,
            true,
            true,
            false,
            true,
            "{}",
            string.Empty,
            "Not checked",
            null,
            []);

    private sealed class TestRuntimeToolProvider : IAgentRuntimeToolProvider
    {
        private readonly IReadOnlyList<string> toolNames;

        public TestRuntimeToolProvider(
            int order,
            AgentRuntimeToolProviderDescriptor descriptor,
            params string[] toolNames)
        {
            Order = order;
            Descriptor = descriptor;
            this.toolNames = toolNames;
        }

        public int Order { get; }

        public AgentRuntimeToolProviderDescriptor Descriptor { get; }

        public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(
            AgentRuntimeToolProviderContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IReadOnlyList<AITool>>(toolNames
                .Select(toolName => AIFunctionFactory.Create(() => "ok", toolName, "Test runtime provider tool."))
                .ToList());
        }
    }

    private sealed class TestMafProviderRuntimeGateway : IMafProviderRuntimeGateway
    {
        public Task<ProviderHealthResult> TestProviderAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            string model,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProviderTestChatResult> RunProviderImageChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            string model,
            IReadOnlyList<ProviderChatAttachment> attachments,
            string modelParameterConfigurationJson = "",
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
            ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TestMafProviderStreamingDispatchGate : IMafProviderStreamingDispatchGate
    {
        public ValueTask<IAsyncDisposable> EnterAsync(
            ProviderProfile provider,
            string model,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IAsyncDisposable>(new NoOpAsyncDisposable());
    }

    private sealed class NoOpAsyncDisposable : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
