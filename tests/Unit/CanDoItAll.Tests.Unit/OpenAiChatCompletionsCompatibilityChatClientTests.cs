using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class OpenAiChatCompletionsCompatibilityChatClientTests
{
    private static readonly AIChatMessage[] Messages =
    [
        new(ChatRole.User, "Complete the request.")
    ];

    [Theory]
    [InlineData(OpenAiModelIds.Gpt56Terra, false)]
    [InlineData(OpenAiModelIds.Gpt56Terra, true)]
    [InlineData(OpenAiModelIds.Gpt56Luna, false)]
    [InlineData(OpenAiModelIds.Gpt56Luna, true)]
    [InlineData(OpenAiModelIds.Gpt54Mini, false)]
    [InlineData(OpenAiModelIds.Gpt54Mini, true)]
    [InlineData("gpt-5.4-mini-2026-08-01", false)]
    [InlineData("gpt-5.4-mini-2026-08-01", true)]
    [InlineData("gpt-5.6-luna-2026-08-01", false)]
    [InlineData("gpt-5.6-luna-2026-08-01", true)]
    [InlineData("gpt-5.6-terra-2026-08-01", false)]
    [InlineData("gpt-5.6-terra-2026-08-01", true)]
    public async Task Affected_models_force_none_without_mutating_caller_options(
        string model,
        bool streaming)
    {
        var function = AIFunctionFactory.Create(
            () => "ok",
            "test_function",
            "A local test function.");
        var options = new ChatOptions
        {
            Temperature = 0.25f,
            Reasoning = new ReasoningOptions
            {
                Effort = ReasoningEffort.Medium
            },
            Tools = [function]
        };
        using var innerClient = new RecordingChatClient();
        using var client = CreateClient(innerClient, model);

        await InvokeAsync(client, streaming, options);

        var observedOptions = Assert.IsType<ChatOptions>(innerClient.ObservedOptions);
        Assert.NotSame(options, observedOptions);
        Assert.Equal(0.25f, observedOptions.Temperature);
        Assert.Equal(ReasoningEffort.Medium, options.Reasoning!.Effort);
        Assert.Same(function, Assert.Single(options.Tools!));
        Assert.Equal(ReasoningEffort.None, observedOptions.Reasoning!.Effort);
        Assert.Same(function, Assert.Single(observedOptions.Tools!));
    }

    [Fact]
    public async Task Already_explicit_none_preserves_caller_options()
    {
        var function = AIFunctionFactory.Create(
            () => "ok",
            "already_none_test_function",
            "An already-none test function.");
        var options = new ChatOptions
        {
            Reasoning = new ReasoningOptions
            {
                Effort = ReasoningEffort.None
            },
            Tools = [function]
        };
        using var innerClient = new RecordingChatClient();
        using var client = CreateClient(innerClient, OpenAiModelIds.Gpt56Luna);

        await client.GetResponseAsync(Messages, options);

        Assert.Same(options, innerClient.ObservedOptions);
        Assert.Equal(ReasoningEffort.None, options.Reasoning.Effort);
        Assert.Same(function, Assert.Single(options.Tools));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Terra_function_tools_add_explicit_none_when_reasoning_is_omitted(bool streaming)
    {
        var options = new ChatOptions
        {
            Tools =
            [
                AIFunctionFactory.Create(
                    () => "ok",
                    "test_function",
                    "A local test function.")
            ]
        };
        using var innerClient = new RecordingChatClient();
        using var client = CreateClient(innerClient, OpenAiModelIds.Gpt56Terra);

        await InvokeAsync(client, streaming, options);

        var observedOptions = Assert.IsType<ChatOptions>(innerClient.ObservedOptions);
        Assert.Null(options.Reasoning);
        Assert.Equal(ReasoningEffort.None, observedOptions.Reasoning!.Effort);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Approval_required_function_is_treated_as_a_function_tool(bool streaming)
    {
        var function = AIFunctionFactory.Create(
            () => "ok",
            "approval_test_function",
            "A local approval test function.");
        var options = new ChatOptions
        {
            Tools = [new ApprovalRequiredAIFunction(function)]
        };
        using var innerClient = new RecordingChatClient();
        using var client = CreateClient(innerClient, OpenAiModelIds.Gpt56Terra);

        await InvokeAsync(client, streaming, options);

        var observedOptions = Assert.IsType<ChatOptions>(innerClient.ObservedOptions);
        Assert.Equal(ReasoningEffort.None, observedOptions.Reasoning!.Effort);
        Assert.IsType<ApprovalRequiredAIFunction>(Assert.Single(observedOptions.Tools!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Declaration_only_function_is_treated_as_a_function_tool(bool streaming)
    {
        var declaration = AIFunctionFactory.Create(
                () => "ok",
                "declaration_test_function",
                "A declaration-only test function.")
            .AsDeclarationOnly();
        var options = new ChatOptions
        {
            Tools = [declaration]
        };
        using var innerClient = new RecordingChatClient();
        using var client = CreateClient(innerClient, OpenAiModelIds.Gpt56Terra);

        await InvokeAsync(client, streaming, options);

        var observedOptions = Assert.IsType<ChatOptions>(innerClient.ObservedOptions);
        Assert.Equal(ReasoningEffort.None, observedOptions.Reasoning!.Effort);
        Assert.Same(declaration, Assert.Single(observedOptions.Tools!));
    }

    [Fact]
    public async Task Non_function_tool_does_not_disable_reasoning()
    {
        var options = new ChatOptions
        {
            Reasoning = new ReasoningOptions
            {
                Effort = ReasoningEffort.High
            },
            Tools = [new UnknownProviderTool()]
        };
        using var innerClient = new RecordingChatClient();
        using var client = CreateClient(innerClient, OpenAiModelIds.Gpt56Terra);

        await client.GetResponseAsync(Messages, options);

        Assert.Same(options, innerClient.ObservedOptions);
        Assert.Equal(ReasoningEffort.High, innerClient.ObservedOptions!.Reasoning!.Effort);
    }

    [Fact]
    public async Task AsAIAgent_normalizes_tools_after_agent_option_composition()
    {
        var function = AIFunctionFactory.Create(
            () => "ok",
            "composed_test_function",
            "A composed local test function.");
        using var innerClient = new RecordingChatClient();
        using var client = CreateClient(innerClient, OpenAiModelIds.Gpt56Terra);
        var agent = client.AsAIAgent(
            options: new ChatClientAgentOptions
            {
                ChatOptions = new ChatOptions
                {
                    Tools = [function]
                }
            });
        var session = await agent.CreateSessionAsync();

        await agent.RunAsync(Messages, session);

        var observedOptions = Assert.IsType<ChatOptions>(innerClient.ObservedOptions);
        Assert.Equal(ReasoningEffort.None, observedOptions.Reasoning!.Effort);
        Assert.Same(function, Assert.Single(observedOptions.Tools!));
    }

    [Theory]
    [InlineData(OpenAiModelIds.Gpt56Sol, OpenAiModelIds.Gpt56Terra, true)]
    [InlineData(OpenAiModelIds.Gpt56Terra, OpenAiModelIds.Gpt56Sol, false)]
    public async Task Request_model_override_controls_compatibility(
        string clientModel,
        string requestModel,
        bool expectNone)
    {
        var options = new ChatOptions
        {
            ModelId = requestModel,
            Reasoning = new ReasoningOptions
            {
                Effort = ReasoningEffort.High
            },
            Tools =
            [
                AIFunctionFactory.Create(
                    () => "ok",
                    "model_override_test_function",
                    "A model override test function.")
            ]
        };
        using var innerClient = new RecordingChatClient();
        using var client = CreateClient(innerClient, clientModel);

        await client.GetResponseAsync(Messages, options);

        var observedEffort = innerClient.ObservedOptions!.Reasoning!.Effort;
        Assert.Equal(
            expectNone ? ReasoningEffort.None : ReasoningEffort.High,
            observedEffort);
    }

    [Fact]
#pragma warning disable OPENAI001
    public async Task Terra_function_tools_replace_transport_native_max_with_none()
    {
        var options = new ChatOptions
        {
            RawRepresentationFactory = _ => new ChatCompletionOptions
            {
                ReasoningEffortLevel = new ChatReasoningEffortLevel("max")
            },
            Tools =
            [
                AIFunctionFactory.Create(
                    () => "ok",
                    "test_function",
                    "A local test function.")
            ]
        };
        using var innerClient = new RecordingChatClient();
        using var client = CreateClient(innerClient, OpenAiModelIds.Gpt56Terra);

        await client.GetResponseAsync(Messages, options);

        var observedOptions = Assert.IsType<ChatOptions>(innerClient.ObservedOptions);
        var rawOptions = Assert.IsType<ChatCompletionOptions>(observedOptions.RawRepresentationFactory!(null!));
        Assert.Equal(ReasoningEffort.None, observedOptions.Reasoning!.Effort);
        Assert.Equal("none", rawOptions.ReasoningEffortLevel.ToString());
        var originalRawOptions = Assert.IsType<ChatCompletionOptions>(options.RawRepresentationFactory!(null!));
        Assert.Equal("max", originalRawOptions.ReasoningEffortLevel.ToString());
    }
#pragma warning restore OPENAI001

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
#pragma warning disable OPENAI001
    public async Task Native_raw_function_tools_force_none_when_generic_tools_are_absent(bool streaming)
    {
        var options = new ChatOptions
        {
            Reasoning = new ReasoningOptions
            {
                Effort = ReasoningEffort.Medium
            },
            RawRepresentationFactory = _ => CreateNativeToolOptions()
        };
        using var innerClient = new RecordingChatClient();
        using var client = CreateClient(innerClient, OpenAiModelIds.Gpt56Terra);

        await InvokeAsync(client, streaming, options);

        var observedOptions = Assert.IsType<ChatOptions>(innerClient.ObservedOptions);
        var rawOptions = Assert.IsType<ChatCompletionOptions>(observedOptions.RawRepresentationFactory!(null!));
        Assert.Equal(ReasoningEffort.Medium, options.Reasoning!.Effort);
        Assert.Equal(ReasoningEffort.None, observedOptions.Reasoning!.Effort);
        Assert.Single(rawOptions.Tools);
        Assert.Equal("none", rawOptions.ReasoningEffortLevel.ToString());

        static ChatCompletionOptions CreateNativeToolOptions()
        {
            var rawOptions = new ChatCompletionOptions
            {
                ReasoningEffortLevel = new ChatReasoningEffortLevel("high")
            };
            rawOptions.Tools.Add(ChatTool.CreateFunctionTool(
                "native_test_function",
                "A native raw test function.",
                BinaryData.FromString("{\"type\":\"object\"}")));
            return rawOptions;
        }
    }
#pragma warning restore OPENAI001

    [Fact]
    public async Task Unexpected_raw_representation_preserves_provider_fallback_behavior()
    {
        var rawRepresentation = new object();
        var options = new ChatOptions
        {
            Reasoning = new ReasoningOptions
            {
                Effort = ReasoningEffort.High
            },
            RawRepresentationFactory = _ => rawRepresentation,
            Tools =
            [
                AIFunctionFactory.Create(
                    () => "ok",
                    "unexpected_raw_test_function",
                    "An unexpected raw representation test function.")
            ]
        };
        using var innerClient = new RecordingChatClient();
        using var client = CreateClient(innerClient, OpenAiModelIds.Gpt56Terra);

        await client.GetResponseAsync(Messages, options);

        var observedOptions = Assert.IsType<ChatOptions>(innerClient.ObservedOptions);
        Assert.Equal(ReasoningEffort.None, observedOptions.Reasoning!.Effort);
        Assert.Same(rawRepresentation, observedOptions.RawRepresentationFactory!(null!));
    }

    [Theory]
    [InlineData(OpenAiModelIds.Gpt56Sol, true)]
    [InlineData("gpt-5.6-sol-2026-08-01", true)]
    [InlineData(OpenAiModelIds.Gpt56, true)]
    [InlineData("gpt-5.6-2026-08-01", true)]
    [InlineData(OpenAiModelIds.Gpt56Terra, false)]
    public async Task Unproven_shapes_preserve_reasoning(string model, bool useFunctionTool)
    {
        var options = new ChatOptions
        {
            Reasoning = new ReasoningOptions
            {
                Effort = ReasoningEffort.High
            },
            Tools = useFunctionTool
                ?
                [
                    AIFunctionFactory.Create(
                        () => "ok",
                        "test_function",
                        "A local test function.")
                ]
                : []
        };
        using var innerClient = new RecordingChatClient();
        using var client = CreateClient(innerClient, model);

        await client.GetResponseAsync(Messages, options);

        Assert.Same(options, innerClient.ObservedOptions);
        Assert.Equal(ReasoningEffort.High, innerClient.ObservedOptions!.Reasoning!.Effort);
    }

    private static OpenAiChatCompletionsCompatibilityChatClient CreateClient(
        IChatClient innerClient,
        string model)
    {
        return new OpenAiChatCompletionsCompatibilityChatClient(
            innerClient,
            CreateProvider(),
            model,
            logger: null);
    }

    private static async Task InvokeAsync(
        IChatClient client,
        bool streaming,
        ChatOptions options)
    {
        if (!streaming)
        {
            await client.GetResponseAsync(Messages, options);
            return;
        }

        await foreach (var _ in client.GetStreamingResponseAsync(Messages, options))
        {
        }
    }

    private static ProviderProfile CreateProvider()
    {
        return new ProviderProfile(
            Id: Guid.NewGuid(),
            Name: "OpenAI Chat Completions test",
            Kind: ProviderKind.OpenAi,
            BaseUrl: "https://api.openai.com/v1",
            ApiKeyEnvironmentVariable: "TEST_OPENAI_API_KEY",
            DefaultModel: OpenAiModelIds.Gpt56Terra,
            Transport: ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "ok",
            LastCheckedAtUtc: null,
            SuggestedModels: []);
    }

    private sealed class RecordingChatClient : IChatClient
    {
        public ChatOptions? ObservedOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<AIChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ObservedOptions = options;
            return Task.FromResult(new ChatResponse(
                new AIChatMessage(ChatRole.Assistant, "completed")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<AIChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ObservedOptions = options;
            yield return new ChatResponseUpdate(
                role: ChatRole.Assistant,
                contents: [new TextContent("completed")]);
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return serviceKey is null && serviceType.IsInstanceOfType(this)
                ? this
                : null;
        }

        public void Dispose()
        {
        }
    }

    private sealed class UnknownProviderTool : AITool;
}
