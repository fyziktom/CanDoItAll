using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafProcessToolIterationTests {
    private const string WorkToolName = "perform_work";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Governed_process_keeps_tools_after_the_framework_default_limit(bool streaming) {
        using var client = new WorkSequenceClient(requiredCalls: 45);
        var calls = 0;
        var tool = AIFunctionFactory.Create(() => ++calls, WorkToolName);
        var agent = new ChatClientAgent(client, MafChatClientAgentOptionsFactory.Create(new ChatOptions { Tools = [tool] }));

        var response = await RunAsync(agent, CreateRunOptions(governed: true), streaming);

        Assert.Equal("completed", response.Text);
        Assert.Equal(45, calls);
        Assert.Equal(46, client.RequestCount);
        Assert.False(client.ToolsRemoved);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Governed_process_still_has_a_finite_iteration_limit(bool streaming) {
        using var client = new WorkSequenceClient(requiredCalls: int.MaxValue);
        var calls = 0;
        var tool = AIFunctionFactory.Create(() => ++calls, WorkToolName);
        var agent = new ChatClientAgent(client, MafChatClientAgentOptionsFactory.Create(new ChatOptions { Tools = [tool] }));

        await RunAsync(agent, CreateRunOptions(governed: true), streaming);

        Assert.True(client.ToolsRemoved);
        Assert.InRange(calls, 255, 256);
        Assert.InRange(client.RequestCount, 256, 257);
    }

    [Fact]
    public async Task Ordinary_agent_keeps_the_framework_iteration_limit() {
        using var client = new WorkSequenceClient(requiredCalls: 45);
        var calls = 0;
        var tool = AIFunctionFactory.Create(() => ++calls, WorkToolName);
        var agent = new ChatClientAgent(client, MafChatClientAgentOptionsFactory.Create(new ChatOptions { Tools = [tool] }));

        await RunAsync(agent, CreateRunOptions(governed: false), streaming: true);

        Assert.True(client.ToolsRemoved);
        Assert.InRange(calls, 39, 40);
    }

    [Fact]
    public async Task Governed_process_keeps_required_tool_approval() {
        using var client = new WorkSequenceClient(requiredCalls: 1);
        var calls = 0;
        var tool = new ApprovalRequiredAIFunction(AIFunctionFactory.Create(() => ++calls, WorkToolName));
        var agent = new ChatClientAgent(client, MafChatClientAgentOptionsFactory.Create(new ChatOptions { Tools = [tool] }));

        var response = await RunAsync(agent, CreateRunOptions(governed: true), streaming: true);

        Assert.Equal(0, calls);
        Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>());
    }

    [Theory]
    [InlineData(true, AgentFinalizerMode.Required, true)]
    [InlineData(false, AgentFinalizerMode.Required, false)]
    [InlineData(true, AgentFinalizerMode.Disabled, false)]
    public void Tool_iteration_policy_uses_execution_intent_instead_of_output_schema(
        bool governed, AgentFinalizerMode finalizerMode, bool hasIterationPolicy) {
        var options = CreateRunOptions(governed, finalizerMode);

        Assert.Equal(hasIterationPolicy, options.ChatClientFactory is not null);
    }

    private static async Task<AgentResponse> RunAsync(ChatClientAgent agent, ChatClientAgentRunOptions options, bool streaming) {
        var session = await agent.CreateSessionAsync();
        ChatMessage[] messages = [new(ChatRole.User, "Complete the work using the supplied tool.")];
        return streaming
            ? await agent.RunStreamingAsync(messages, session, options).ToAgentResponseAsync()
            : await agent.RunAsync(messages, session, options);
    }

    private static ChatClientAgentRunOptions CreateRunOptions(bool governed, AgentFinalizerMode? finalizerMode = null) {
        var providerId = Guid.NewGuid();
        var definition = new AgentDefinition(
            Id: Guid.NewGuid(), Name: "Iteration test", RoleTitle: "Tester", Summary: "Tool iteration test",
            Instructions: "Use tools", Status: AgentLifecycleStatus.Active, ProviderProfileId: providerId,
            Model: "gpt-4.1", Workload: AgentWorkloadKind.Programming,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged, Temperature: 0,
            RequirePerServiceCallChatHistoryPersistence: false, EnableBackgroundResponses: false,
            ConfigurationJson: "{}", IsTemplate: false, TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default, Capabilities: [], Tags: [],
            CreatedAtUtc: DateTimeOffset.UtcNow, UpdatedAtUtc: DateTimeOffset.UtcNow);
        var provider = new ProviderProfile(providerId, "Test provider", ProviderKind.OpenAi,
            "https://api.openai.com/v1", string.Empty, "gpt-4.1", ProviderTransportKind.ChatCompletions,
            true, true, true, false, true, "{}", string.Empty, "Not checked", null, []);
        var execution = new AgentRuntimeExecutionOptions(
            StructuredOutput: AgentStructuredOutputContracts.ProcessStepOutcomeResult,
            FinalizerMode: finalizerMode ?? (governed ? AgentFinalizerMode.Required : AgentFinalizerMode.Disabled),
            RequireStructuredOutputValidation: true, MaxStructuredOutputRepairAttempts: 1,
            ContextIntent: AgentRuntimeContextIntent.Empty with { IsGovernedProcessStep = governed });
        return MafRuntimeSessionBuilder.CreateRunOptions(definition, provider, definition.Model,
            hasApprovalTools: false, continuationToken: null, forceOmitTemperature: false, execution);
    }

    private sealed class WorkSequenceClient(int requiredCalls) : IChatClient {
        public int RequestCount { get; private set; }
        public bool ToolsRemoved { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCount++;
            if (options?.Tools?.Any(tool => tool.Name == WorkToolName) != true) {
                ToolsRemoved = true;
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "tools unavailable")));
            }
            if (RequestCount > requiredCalls) {
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "completed")));
            }
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent($"work-{RequestCount}", WorkToolName, new Dictionary<string, object?>())])));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var update in response.ToChatResponseUpdates()) {
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) {
            return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
        }

        public void Dispose() {
        }
    }
}
