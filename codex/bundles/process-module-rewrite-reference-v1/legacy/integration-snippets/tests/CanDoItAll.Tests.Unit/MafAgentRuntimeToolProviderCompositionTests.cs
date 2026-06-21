using System.Reflection;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class MafAgentRuntimeToolProviderCompositionTests
{
    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_zero_registered_providers_does_not_attach_process_tools()
    {
        var runtime = new MafAgentRuntime(Path.GetTempPath(), new ServiceCollection().BuildServiceProvider());
        var progressMessages = new List<string>();

        var state = await InvokeCreateCapabilityStateAsync(runtime, CreateToolEnabledAgent(), CreateProviderProfile(), progressMessages);

        var tools = ReadTools(state);
        Assert.Empty(tools);
        Assert.DoesNotContain(tools, tool =>
            tool.Name.StartsWith("processes_", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(progressMessages, message =>
            message.Contains("registered runtime tool provider", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(progressMessages, message =>
            message.Contains("process-module tools", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_invokes_fake_providers_in_deterministic_order()
    {
        var lateProvider = new TestRuntimeToolProvider(20, "late_runtime_tool");
        var earlyProvider = new TestRuntimeToolProvider(10, "early_runtime_tool");
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(lateProvider);
        services.AddSingleton<IAgentRuntimeToolProvider>(earlyProvider);
        var runtime = new MafAgentRuntime(Path.GetTempPath(), services.BuildServiceProvider());
        var agent = CreateToolEnabledAgent();
        var provider = CreateProviderProfile();
        var progressMessages = new List<string>();

        var state = await InvokeCreateCapabilityStateAsync(runtime, agent, provider, progressMessages);

        var toolNames = ReadTools(state)
            .Select(tool => tool.Name)
            .ToList();
        Assert.Equal(["early_runtime_tool", "late_runtime_tool"], toolNames);
        Assert.Single(earlyProvider.Contexts);
        Assert.Single(lateProvider.Contexts);
        Assert.Equal(agent.Id, earlyProvider.Contexts[0].Agent.Id);
        Assert.Equal(provider.Id, earlyProvider.Contexts[0].Provider.Id);
        Assert.Equal(AgentRuntimeToolProviderPurpose.InteractiveChat, earlyProvider.Contexts[0].Purpose);
        Assert.False(earlyProvider.Contexts[0].SuppressApprovalRequirements);
        var descriptors = ReadProviderDescriptors(state);
        Assert.Equal(2, descriptors.Count);
        Assert.All(descriptors, descriptor =>
            Assert.StartsWith("legacy:", descriptor.ProviderKey, StringComparison.Ordinal));
        Assert.Contains(progressMessages, message =>
            message.Contains("Attached 2 tool(s) from 2 registered runtime tool provider(s).", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_records_provider_descriptor_metadata()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(
            10,
            CreateDescriptor("tests.provider-a"),
            "provider_descriptor_tool"));
        var runtime = new MafAgentRuntime(Path.GetTempPath(), services.BuildServiceProvider());
        var progressMessages = new List<string>();

        var state = await InvokeCreateCapabilityStateAsync(runtime, CreateToolEnabledAgent(), CreateProviderProfile(), progressMessages);

        var descriptor = Assert.Single(ReadProviderDescriptors(state));
        Assert.Equal("tests.provider-a", descriptor.ProviderKey);
        Assert.Equal("Test provider tests.provider-a", descriptor.DisplayName);
        Assert.Contains("tests", descriptor.DomainTags);
        Assert.Contains(AgentRuntimeToolProviderPurpose.InteractiveChat, descriptor.SupportedPurposes);
        Assert.Contains(progressMessages, message =>
            message.Contains("tests.provider-a", StringComparison.Ordinal) &&
            message.Contains("Test provider tests.provider-a", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_rejects_duplicate_provider_keys()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(
            10,
            CreateDescriptor("tests.duplicate-provider"),
            "first_provider_tool"));
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(
            20,
            CreateDescriptor("tests.duplicate-provider"),
            "second_provider_tool"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            var runtime = new MafAgentRuntime(Path.GetTempPath(), services.BuildServiceProvider());
            await InvokeCreateCapabilityStateAsync(runtime, CreateToolEnabledAgent(), CreateProviderProfile(), []);
        });

        Assert.Contains("Runtime tool provider key(s) must be unique", exception.Message, StringComparison.Ordinal);
        Assert.Contains("tests.duplicate-provider", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AgentRuntimeToolProviderDescriptor_rejects_null_or_empty_provider_key(string? providerKey)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentRuntimeToolProviderDescriptor(providerKey!, "Display", "Description"));

        Assert.Equal("providerKey", exception.ParamName);
    }

    [Fact]
    public void AgentRuntimeToolProviderDescriptor_rejects_empty_display_name()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new AgentRuntimeToolProviderDescriptor("tests.provider", " ", "Description"));

        Assert.Equal("displayName", exception.ParamName);
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_infers_tool_operation_metadata_from_policy_catalog()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(
            10,
            CreateDescriptor("tests.process-provider"),
            "processes_runs_list",
            "processes_run_start",
            "unclassified_provider_tool"));
        var runtime = new MafAgentRuntime(Path.GetTempPath(), services.BuildServiceProvider());

        var state = await InvokeCreateCapabilityStateAsync(runtime, CreateToolEnabledAgent(), CreateProviderProfile(), []);

        var metadata = ReadToolMetadata(state);
        var readMetadata = Assert.Single(metadata, item => item.ToolName == "processes_runs_list");
        Assert.Equal("tests.process-provider", readMetadata.ProviderKey);
        Assert.Equal(AgentRuntimeToolOperationKind.Read, readMetadata.OperationKind);
        Assert.False(readMetadata.RequiresApprovalByDefault);
        Assert.Contains("tests", readMetadata.OwnershipTags);

        var mutationMetadata = Assert.Single(metadata, item => item.ToolName == "processes_run_start");
        Assert.Equal(AgentRuntimeToolOperationKind.Mutation, mutationMetadata.OperationKind);
        Assert.True(mutationMetadata.RequiresApprovalByDefault);

        var unknownMetadata = Assert.Single(metadata, item => item.ToolName == "unclassified_provider_tool");
        Assert.Equal(AgentRuntimeToolOperationKind.Unknown, unknownMetadata.OperationKind);
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_rejects_tool_metadata_for_unknown_tool_name()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(
            10,
            CreateDescriptor("tests.metadata-provider"),
            ["metadata_known_tool"],
            [
                new AgentRuntimeToolMetadata(
                    "tests.metadata-provider",
                    "metadata_unknown_tool",
                    AgentRuntimeToolOperationKind.Read,
                    requiresApprovalByDefault: false)
            ]));
        var runtime = new MafAgentRuntime(Path.GetTempPath(), services.BuildServiceProvider());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await InvokeCreateCapabilityStateAsync(runtime, CreateToolEnabledAgent(), CreateProviderProfile(), []));

        Assert.Contains("declared metadata for unknown tool name(s)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("metadata_unknown_tool", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_rejects_duplicate_provider_tool_names()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(10, "duplicate_runtime_tool"));
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(20, "duplicate_runtime_tool"));
        var runtime = new MafAgentRuntime(Path.GetTempPath(), services.BuildServiceProvider());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await InvokeCreateCapabilityStateAsync(runtime, CreateToolEnabledAgent(), CreateProviderProfile(), []));

        Assert.Contains("Runtime tool provider", exception.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate_runtime_tool", exception.Message, StringComparison.Ordinal);
        Assert.Contains("already registered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_wraps_policy_mutation_tools_from_providers()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(new TestRuntimeToolProvider(
            10,
            "processes_runs_list",
            "processes_run_start"));
        var runtime = new MafAgentRuntime(Path.GetTempPath(), services.BuildServiceProvider());

        var state = await InvokeCreateCapabilityStateAsync(runtime, CreateToolEnabledAgent(), CreateProviderProfile(), []);

        var tools = ReadTools(state);
        Assert.IsNotType<ApprovalRequiredAIFunction>(Assert.Single(tools, tool => tool.Name == "processes_runs_list"));
        Assert.IsType<ApprovalRequiredAIFunction>(Assert.Single(tools, tool => tool.Name == "processes_run_start"));
    }

    [Fact]
    public async Task MafAgentRuntimeToolProviderComposition_reports_provider_failures_with_provider_type()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRuntimeToolProvider>(new ThrowingRuntimeToolProvider());
        var runtime = new MafAgentRuntime(Path.GetTempPath(), services.BuildServiceProvider());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await InvokeCreateCapabilityStateAsync(runtime, CreateToolEnabledAgent(), CreateProviderProfile(), []));

        Assert.Contains(nameof(ThrowingRuntimeToolProvider), exception.Message, StringComparison.Ordinal);
        Assert.Contains("failed to create tools", exception.Message, StringComparison.Ordinal);
        Assert.Contains("provider failed intentionally", exception.Message, StringComparison.Ordinal);
    }

    private static IReadOnlyList<AITool> ReadTools(object state)
        => Assert.IsAssignableFrom<IEnumerable<AITool>>(
                state.GetType().GetProperty("Tools", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state))
            .ToList();

    private static IReadOnlyList<AgentRuntimeToolProviderDescriptor> ReadProviderDescriptors(object state)
        => Assert.IsAssignableFrom<IEnumerable<AgentRuntimeToolProviderDescriptor>>(
                state.GetType().GetProperty("RuntimeToolProviderDescriptors", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state))
            .ToList();

    private static IReadOnlyList<AgentRuntimeToolMetadata> ReadToolMetadata(object state)
        => Assert.IsAssignableFrom<IEnumerable<AgentRuntimeToolMetadata>>(
                state.GetType().GetProperty("RuntimeToolMetadata", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state))
            .ToList();

    private static AgentRuntimeToolProviderDescriptor CreateDescriptor(string providerKey)
        => new(
            providerKey,
            $"Test provider {providerKey}",
            "Test runtime provider.",
            ["tests"],
            [AgentRuntimeToolProviderPurpose.InteractiveChat]);

    private static async Task<object> InvokeCreateCapabilityStateAsync(
        MafAgentRuntime runtime,
        AgentDefinition agent,
        ProviderProfile provider,
        List<string> progressMessages,
        bool suppressApprovalRequirements = false)
    {
        var method = typeof(MafAgentRuntime).GetMethod(
                         "CreateCapabilityStateAsync",
                         BindingFlags.NonPublic | BindingFlags.Instance)
                     ?? throw new InvalidOperationException("CreateCapabilityStateAsync method was not found.");
        var invocation = method.Invoke(
            runtime,
            [
                agent,
                provider,
                Array.Empty<CapabilityCatalogItem>(),
                Array.Empty<AgentMemoryRecord>(),
                (Func<ExecutionState, string, string, Task>)((_, _, message) =>
                {
                    progressMessages.Add(message);
                    return Task.CompletedTask;
                }),
                CancellationToken.None,
                suppressApprovalRequirements
            ]);
        var task = Assert.IsAssignableFrom<Task>(invocation);
        await task;

        return task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance)?.GetValue(task)
               ?? throw new InvalidOperationException("CreateCapabilityStateAsync did not produce a result.");
    }

    private static AgentDefinition CreateToolEnabledAgent()
        => new(
            Id: Guid.NewGuid(),
            Name: "Tool Provider Agent",
            RoleTitle: "Tester",
            Summary: "Tests runtime tool providers.",
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

    private static ProviderProfile CreateProviderProfile()
        => new(
            Guid.NewGuid(),
            "Unit Provider",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
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
        private readonly IReadOnlyList<AgentRuntimeToolMetadata> metadata;

        public TestRuntimeToolProvider(int order, params string[] toolNames)
            : this(order, null, toolNames)
        {
        }

        public TestRuntimeToolProvider(
            int order,
            AgentRuntimeToolProviderDescriptor? descriptor,
            params string[] toolNames)
            : this(order, descriptor, toolNames, [])
        {
        }

        public TestRuntimeToolProvider(
            int order,
            AgentRuntimeToolProviderDescriptor? descriptor,
            IReadOnlyList<string> toolNames,
            IReadOnlyList<AgentRuntimeToolMetadata> metadata)
        {
            Order = order;
            Descriptor = descriptor;
            this.toolNames = toolNames;
            this.metadata = metadata;
        }

        public int Order { get; }

        public AgentRuntimeToolProviderDescriptor? Descriptor { get; }

        public List<AgentRuntimeToolProviderContext> Contexts { get; } = [];

        public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(
            AgentRuntimeToolProviderContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Contexts.Add(context);
            return ValueTask.FromResult<IReadOnlyList<AITool>>(toolNames
                .Select(toolName => AIFunctionFactory.Create(() => "ok", toolName, "Test runtime provider tool."))
                .ToList());
        }

        public IReadOnlyList<AgentRuntimeToolMetadata> GetToolMetadata(
            AgentRuntimeToolProviderContext context)
            => metadata;
    }

    private sealed class ThrowingRuntimeToolProvider : IAgentRuntimeToolProvider
    {
        public int Order => 0;

        public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(
            AgentRuntimeToolProviderContext context,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("provider failed intentionally");
    }
}
