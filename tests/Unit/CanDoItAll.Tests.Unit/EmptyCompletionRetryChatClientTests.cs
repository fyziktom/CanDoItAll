using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Unit;

public sealed class EmptyCompletionRetryChatClientTests
{
    private static readonly ChatMessage[] Messages =
    [
        new(ChatRole.User, "Complete the request.")
    ];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetResponse_EmptyThenValid_RetriesExactlyOnceWithTransientInstruction(bool streaming)
    {
        var validResponse = CreateResponse(new TextContent("completed"));
        var innerClient = streaming
            ? ScriptedChatClient.ForStreaming(
                [],
                [CreateUpdate(new TextContent("completed"))])
            : ScriptedChatClient.ForResponses(
                CreateResponse(),
                validResponse);
        using var client = CreateClient(innerClient);

        var result = await InvokeAsync(client, streaming);

        Assert.Equal(2, innerClient.InvocationCount);
        Assert.Equal("completed", result.Text);
        Assert.Single(innerClient.Invocations[0]);
        Assert.Same(Messages[0], innerClient.Invocations[0][0]);
        Assert.Equal(2, innerClient.Invocations[1].Count);
        Assert.Same(Messages[0], innerClient.Invocations[1][0]);
        var retryMessage = innerClient.Invocations[1][1];
        Assert.Equal(ChatRole.User, retryMessage.Role);
        Assert.Contains(
            "The previous assistant response was empty.",
            string.Concat(retryMessage.Contents.OfType<TextContent>().Select(content => content.Text)));
        if (!streaming)
        {
            Assert.Same(validResponse, result.Response);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetResponse_TwoUsageOnlyEmptyAttempts_StopsAfterSecondAndPreservesUsage(bool streaming)
    {
        var firstUsage = CreateUsage(inputTokens: 3, outputTokens: 0);
        var secondUsage = CreateUsage(inputTokens: 5, outputTokens: 0);
        var secondResponse = CreateResponse(usage: secondUsage);
        var innerClient = streaming
            ? ScriptedChatClient.ForStreaming(
                [CreateUsageUpdate(firstUsage)],
                [CreateUsageUpdate(secondUsage)])
            : ScriptedChatClient.ForResponses(
                CreateResponse(usage: firstUsage),
                secondResponse);
        using var client = CreateClient(innerClient);

        var result = await InvokeAsync(client, streaming);

        Assert.Equal(2, innerClient.InvocationCount);
        Assert.Equal(8, result.InputTokens);
        Assert.Equal(0, result.OutputTokens);
        Assert.Equal(string.Empty, result.Text);
        if (!streaming)
        {
            Assert.Same(secondResponse, result.Response);
        }
    }

    [Theory]
    [InlineData(false, CompletionSignal.Text)]
    [InlineData(true, CompletionSignal.Text)]
    [InlineData(false, CompletionSignal.FunctionCall)]
    [InlineData(true, CompletionSignal.FunctionCall)]
    [InlineData(false, CompletionSignal.ToolApprovalRequest)]
    [InlineData(true, CompletionSignal.ToolApprovalRequest)]
    [InlineData(false, CompletionSignal.ContinuationToken)]
    [InlineData(true, CompletionSignal.ContinuationToken)]
    [InlineData(false, CompletionSignal.UnknownActionableContent)]
    [InlineData(true, CompletionSignal.UnknownActionableContent)]
    public async Task GetResponse_ActionableSignal_DoesNotRetry(
        bool streaming,
        CompletionSignal signal)
    {
        var firstResponse = CreateResponse(signal);
        var innerClient = streaming
            ? ScriptedChatClient.ForStreaming(
                CreateUpdates(signal),
                [CreateUpdate(new TextContent("must not be reached"))])
            : ScriptedChatClient.ForResponses(
                firstResponse,
                CreateResponse(new TextContent("must not be reached")));
        using var client = CreateClient(innerClient);

        var result = await InvokeAsync(client, streaming);

        Assert.Equal(1, innerClient.InvocationCount);
        Assert.True(result.HasSignal(signal));
        if (!streaming)
        {
            Assert.Same(firstResponse, result.Response);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetResponse_ReasoningAndPositiveUsageThenValid_RetriesExactlyOnce(bool streaming)
    {
        var firstUsage = CreateUsage(inputTokens: 7, outputTokens: 937);
        var secondUsage = CreateUsage(inputTokens: 11, outputTokens: 4);
        var reasoning = new TextReasoningContent("I need to inspect the project structure first.");
        var firstResponse = CreateResponse(reasoning, firstUsage);
        firstResponse.FinishReason = ChatFinishReason.Stop;
        var firstUsageUpdate = CreateUsageUpdate(firstUsage);
        firstUsageUpdate.FinishReason = ChatFinishReason.Stop;
        var innerClient = streaming
            ? ScriptedChatClient.ForStreaming(
                [CreateUpdate(reasoning), firstUsageUpdate],
                [
                    CreateUpdate(new TextContent("completed")),
                    CreateUsageUpdate(secondUsage)
                ])
            : ScriptedChatClient.ForResponses(
                firstResponse,
                CreateResponse(new TextContent("completed"), secondUsage));
        using var client = CreateClient(innerClient);

        var result = await InvokeAsync(client, streaming);

        Assert.Equal(2, innerClient.InvocationCount);
        Assert.Equal("completed", result.Text);
        Assert.Equal(18, result.InputTokens);
        Assert.Equal(941, result.OutputTokens);
        Assert.Equal(959, result.TotalTokens);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetResponse_ReasoningRetryEndsWithLength_ReportsExhaustionWithoutThirdAttempt(
        bool streaming)
    {
        var firstResponse = CreateResponse(new TextReasoningContent("first attempt reasoning"));
        firstResponse.FinishReason = ChatFinishReason.Stop;
        var secondResponse = CreateResponse(new TextReasoningContent("second attempt reasoning"));
        secondResponse.FinishReason = ChatFinishReason.Length;
        var firstUpdate = CreateUpdate(new TextReasoningContent("first attempt reasoning"));
        firstUpdate.FinishReason = ChatFinishReason.Stop;
        var secondUpdate = CreateUpdate(new TextReasoningContent("second attempt reasoning"));
        secondUpdate.FinishReason = ChatFinishReason.Length;
        var innerClient = streaming
            ? ScriptedChatClient.ForStreaming([firstUpdate], [secondUpdate])
            : ScriptedChatClient.ForResponses(firstResponse, secondResponse);
        var logger = new RecordingLogger();
        using var client = CreateClient(innerClient, logger: logger);

        var result = await InvokeAsync(client, streaming);

        Assert.Equal(2, innerClient.InvocationCount);
        Assert.Equal(string.Empty, result.Text);
        Assert.Contains(
            logger.Messages,
            message => message.Contains("terminal runtime guard will reject", StringComparison.Ordinal));
        Assert.DoesNotContain(
            logger.Messages,
            message => message.Contains("recovered from", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task GetResponse_UnsafeFinishReason_DoesNotRetry(
        bool streaming,
        bool contentFiltered)
    {
        var finishReason = contentFiltered
            ? ChatFinishReason.ContentFilter
            : ChatFinishReason.Length;
        var firstResponse = CreateResponse(
            new TextReasoningContent("unfinished reasoning"),
            CreateUsage(inputTokens: 3, outputTokens: 8));
        firstResponse.FinishReason = finishReason;
        var firstUpdate = CreateUpdate(new TextReasoningContent("unfinished reasoning"));
        firstUpdate.FinishReason = finishReason;
        var innerClient = streaming
            ? ScriptedChatClient.ForStreaming(
                [firstUpdate],
                [CreateUpdate(new TextContent("must not be reached"))])
            : ScriptedChatClient.ForResponses(
                firstResponse,
                CreateResponse(new TextContent("must not be reached")));
        using var client = CreateClient(innerClient);

        var result = await InvokeAsync(client, streaming);

        Assert.Equal(1, innerClient.InvocationCount);
        if (streaming)
        {
            Assert.Equal(finishReason, Assert.Single(result.Updates).FinishReason);
        }
        else
        {
            Assert.Same(firstResponse, result.Response);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetResponse_UnknownProviderToolConfigured_DoesNotRetry(bool streaming)
    {
        var firstResponse = CreateResponse();
        var innerClient = streaming
            ? ScriptedChatClient.ForStreaming(
                [],
                [CreateUpdate(new TextContent("must not be reached"))])
            : ScriptedChatClient.ForResponses(
                firstResponse,
                CreateResponse(new TextContent("must not be reached")));
        var options = new ChatOptions
        {
            Tools = [new UnknownProviderTool()]
        };
        using var client = CreateClient(innerClient);

        var result = await InvokeAsync(client, streaming, options: options);

        Assert.Equal(1, innerClient.InvocationCount);
        Assert.Equal(string.Empty, result.Text);
        if (!streaming)
        {
            Assert.Same(firstResponse, result.Response);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetResponse_BackgroundResponsesEnabled_DoesNotRetry(bool streaming)
    {
        var firstResponse = CreateResponse();
        var innerClient = streaming
            ? ScriptedChatClient.ForStreaming(
                [],
                [CreateUpdate(new TextContent("must not be reached"))])
            : ScriptedChatClient.ForResponses(
                firstResponse,
                CreateResponse(new TextContent("must not be reached")));
        using var client = CreateClient(
            innerClient,
            allowBackgroundResponses: true);

        var result = await InvokeAsync(client, streaming);

        Assert.Equal(1, innerClient.InvocationCount);
        Assert.Equal(string.Empty, result.Text);
        if (!streaming)
        {
            Assert.Same(firstResponse, result.Response);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetResponse_LocalFunctionToolConfigured_RemainsEligibleForRetry(bool streaming)
    {
        var innerClient = streaming
            ? ScriptedChatClient.ForStreaming(
                [],
                [CreateUpdate(new TextContent("completed"))])
            : ScriptedChatClient.ForResponses(
                CreateResponse(),
                CreateResponse(new TextContent("completed")));
        var options = new ChatOptions
        {
            Tools =
            [
                AIFunctionFactory.Create(
                    () => "local result",
                    "local_tool",
                    "A local test function.")
            ]
        };
        using var client = CreateClient(innerClient);

        var result = await InvokeAsync(client, streaming, options: options);

        Assert.Equal(2, innerClient.InvocationCount);
        Assert.Equal("completed", result.Text);
    }

    [Fact]
    public async Task RunStreaming_AsAIAgent_PreservesRetryUsageAndInvokesLocalFunctionOnce()
    {
        var invocationCount = 0;
        var localFunction = AIFunctionFactory.Create(
            () =>
            {
                invocationCount++;
                return "local result";
            },
            "local_tool",
            "A local test function.");
        var innerClient = ScriptedChatClient.ForStreaming(
            [CreateUsageUpdate(CreateUsage(inputTokens: 3, outputTokens: 0))],
            [
                CreateUpdate(
                    new FunctionCallContent(
                        "call-001",
                        localFunction.Name,
                        new Dictionary<string, object?>())),
                CreateUsageUpdate(CreateUsage(inputTokens: 5, outputTokens: 1))
            ],
            [
                CreateUpdate(new TextContent("completed")),
                CreateUsageUpdate(CreateUsage(inputTokens: 7, outputTokens: 2))
            ]);
        using var retryClient = CreateClient(innerClient);
        var agent = retryClient.AsAIAgent(
            options: new ChatClientAgentOptions
            {
                ChatOptions = new ChatOptions
                {
                    Tools = [localFunction]
                }
            });
        var session = await agent.CreateSessionAsync();

        var response = await agent
            .RunStreamingAsync(Messages, session)
            .ToAgentResponseAsync();

        Assert.Equal("completed", response.Text);
        Assert.Equal(3, innerClient.InvocationCount);
        Assert.Equal(1, invocationCount);
        Assert.NotNull(response.Usage);
        Assert.Equal(15, response.Usage.InputTokenCount);
        Assert.Equal(3, response.Usage.OutputTokenCount);
        Assert.Equal(18, response.Usage.TotalTokenCount);
    }

    [Fact]
    public async Task RunStreaming_AsAIAgent_EmptyAfterLocalFunction_RecoversWithoutRepeatingTool()
    {
        var invocationCount = 0;
        var localFunction = AIFunctionFactory.Create(
            () =>
            {
                invocationCount++;
                return "local result";
            },
            "local_tool",
            "A local test function.");
        var innerClient = ScriptedChatClient.ForStreaming(
            [
                CreateUpdate(
                    new FunctionCallContent(
                        "call-001",
                        localFunction.Name,
                        new Dictionary<string, object?>())),
                CreateUsageUpdate(CreateUsage(inputTokens: 3, outputTokens: 1))
            ],
            [CreateUsageUpdate(CreateUsage(inputTokens: 5, outputTokens: 0))],
            [
                CreateUpdate(new TextContent("completed")),
                CreateUsageUpdate(CreateUsage(inputTokens: 7, outputTokens: 2))
            ]);
        using var retryClient = CreateClient(innerClient);
        var agent = retryClient.AsAIAgent(
            options: new ChatClientAgentOptions
            {
                ChatOptions = new ChatOptions
                {
                    Tools = [localFunction]
                }
            });
        var session = await agent.CreateSessionAsync();

        var response = await agent
            .RunStreamingAsync(Messages, session)
            .ToAgentResponseAsync();

        Assert.Equal("completed", response.Text);
        Assert.Equal(3, innerClient.InvocationCount);
        Assert.Equal(1, invocationCount);
        Assert.Equal(
            innerClient.Invocations[1].Count + 1,
            innerClient.Invocations[2].Count);
        var retryMessage = innerClient.Invocations[2][^1];
        Assert.Equal(ChatRole.User, retryMessage.Role);
        Assert.Contains(
            "The previous assistant response was empty.",
            string.Concat(retryMessage.Contents.OfType<TextContent>().Select(content => content.Text)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetResponse_Cancellation_DoesNotRetry(bool streaming)
    {
        var innerClient = ScriptedChatClient.ThrowCancellation(streaming);
        using var client = CreateClient(innerClient);
        using var cancellationSource = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => InvokeAsync(client, streaming, cancellationSource.Token));

        Assert.Equal(1, innerClient.InvocationCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetResponse_EmptyRetry_MergesFirstAttemptUsageIntoValidResult(bool streaming)
    {
        var firstUsage = CreateUsage(inputTokens: 7, outputTokens: 0);
        var secondUsage = CreateUsage(inputTokens: 11, outputTokens: 4);
        var innerClient = streaming
            ? ScriptedChatClient.ForStreaming(
                [CreateUsageUpdate(firstUsage)],
                [
                    CreateUpdate(new TextContent("completed")),
                    CreateUsageUpdate(secondUsage)
                ])
            : ScriptedChatClient.ForResponses(
                CreateResponse(usage: firstUsage),
                CreateResponse(new TextContent("completed"), secondUsage));
        using var client = CreateClient(innerClient);

        var result = await InvokeAsync(client, streaming);

        Assert.Equal(2, innerClient.InvocationCount);
        Assert.Equal("completed", result.Text);
        Assert.Equal(18, result.InputTokens);
        Assert.Equal(4, result.OutputTokens);
        Assert.Equal(22, result.TotalTokens);
    }

    private static async Task<InvocationResult> InvokeAsync(
        IChatClient client,
        bool streaming,
        CancellationToken cancellationToken = default,
        ChatOptions? options = null)
    {
        if (!streaming)
        {
            var response = await client.GetResponseAsync(
                Messages,
                options,
                cancellationToken: cancellationToken);
            return InvocationResult.FromResponse(response);
        }

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(
            Messages,
            options,
            cancellationToken: cancellationToken))
        {
            updates.Add(update);
        }

        return InvocationResult.FromUpdates(updates);
    }

    private static EmptyCompletionRetryChatClient CreateClient(
        IChatClient innerClient,
        bool allowBackgroundResponses = false,
        ILogger? logger = null)
    {
        return new EmptyCompletionRetryChatClient(
            innerClient,
            CreateProvider(),
            "test-model",
            allowBackgroundResponses,
            logger);
    }

    private static ProviderProfile CreateProvider()
    {
        return new ProviderProfile(
            Id: Guid.NewGuid(),
            Name: "Test provider",
            Kind: ProviderKind.OpenAi,
            BaseUrl: "https://provider.invalid/v1",
            ApiKeyEnvironmentVariable: "TEST_PROVIDER_API_KEY",
            DefaultModel: "test-model",
            Transport: ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "ok",
            LastCheckedAtUtc: null,
            SuggestedModels: []);
    }

    private static ChatResponse CreateResponse(
        AIContent? content = null,
        UsageDetails? usage = null)
    {
        return new ChatResponse(
            new ChatMessage(
                ChatRole.Assistant,
                content is null ? [] : [content]))
        {
            Usage = usage
        };
    }

    private static ChatResponse CreateResponse(CompletionSignal signal)
    {
        var response = signal switch
        {
            CompletionSignal.Text => CreateResponse(new TextContent("already completed")),
            CompletionSignal.FunctionCall => CreateResponse(CreateFunctionCall()),
            CompletionSignal.ToolApprovalRequest => CreateResponse(CreateApprovalRequest()),
            CompletionSignal.ContinuationToken => CreateResponse(),
            CompletionSignal.UnknownActionableContent => CreateResponse(new UnknownActionableContent()),
            _ => throw new ArgumentOutOfRangeException(nameof(signal), signal, null)
        };

        if (signal is CompletionSignal.ContinuationToken)
        {
            response.ContinuationToken = CreateContinuationToken();
        }

        return response;
    }

    private static IReadOnlyList<ChatResponseUpdate> CreateUpdates(CompletionSignal signal)
    {
        var update = signal switch
        {
            CompletionSignal.Text => CreateUpdate(new TextContent("already completed")),
            CompletionSignal.FunctionCall => CreateUpdate(CreateFunctionCall()),
            CompletionSignal.ToolApprovalRequest => CreateUpdate(CreateApprovalRequest()),
            CompletionSignal.ContinuationToken => CreateUpdate(),
            CompletionSignal.UnknownActionableContent => CreateUpdate(new UnknownActionableContent()),
            _ => throw new ArgumentOutOfRangeException(nameof(signal), signal, null)
        };

        if (signal is CompletionSignal.ContinuationToken)
        {
            update.ContinuationToken = CreateContinuationToken();
        }

        return [update];
    }

    private static ChatResponseUpdate CreateUpdate(AIContent? content = null)
    {
        return new ChatResponseUpdate(
            ChatRole.Assistant,
            content is null ? [] : [content]);
    }

    private static ChatResponseUpdate CreateUsageUpdate(UsageDetails usage)
    {
        return CreateUpdate(new UsageContent(usage));
    }

    private static UsageDetails CreateUsage(long inputTokens, long outputTokens)
    {
        return new UsageDetails
        {
            InputTokenCount = inputTokens,
            OutputTokenCount = outputTokens,
            TotalTokenCount = inputTokens + outputTokens
        };
    }

    private static FunctionCallContent CreateFunctionCall()
    {
        return new FunctionCallContent(
            "call-001",
            "workspace_write_file",
            new Dictionary<string, object?>());
    }

    private static ToolApprovalRequestContent CreateApprovalRequest()
    {
        return new ToolApprovalRequestContent(
            "approval-001",
            CreateFunctionCall());
    }

    private static ResponseContinuationToken CreateContinuationToken()
    {
#pragma warning disable MEAI001
        return ResponseContinuationToken.FromBytes(new byte[] { 1, 2, 3 });
#pragma warning restore MEAI001
    }

    public enum CompletionSignal
    {
        Text,
        FunctionCall,
        ToolApprovalRequest,
        ContinuationToken,
        UnknownActionableContent
    }

    private sealed class UnknownActionableContent : AIContent;

    private sealed class UnknownProviderTool : AITool;

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    private sealed record InvocationResult(
        ChatResponse? Response,
        IReadOnlyList<ChatResponseUpdate> Updates,
        string Text,
        long InputTokens,
        long OutputTokens,
        long TotalTokens)
    {
        public static InvocationResult FromResponse(ChatResponse response)
        {
            var usage = response.Usage;
            return new InvocationResult(
                response,
                [],
                response.Text,
                usage?.InputTokenCount ?? 0,
                usage?.OutputTokenCount ?? 0,
                usage?.TotalTokenCount ?? 0);
        }

        public static InvocationResult FromUpdates(IReadOnlyList<ChatResponseUpdate> updates)
        {
            var usages = updates
                .SelectMany(update => update.Contents)
                .OfType<UsageContent>()
                .Select(content => content.Details)
                .ToArray();
            return new InvocationResult(
                null,
                updates,
                string.Concat(updates.Select(update => update.Text)),
                usages.Sum(usage => usage.InputTokenCount ?? 0),
                usages.Sum(usage => usage.OutputTokenCount ?? 0),
                usages.Sum(usage => usage.TotalTokenCount ?? 0));
        }

        public bool HasSignal(CompletionSignal signal)
        {
            var contents = Response?.Messages
                .SelectMany(message => message.Contents)
                ?? Updates.SelectMany(update => update.Contents);
            return signal switch
            {
                CompletionSignal.Text => Text is "already completed",
                CompletionSignal.FunctionCall => contents.OfType<FunctionCallContent>().Any(),
                CompletionSignal.ToolApprovalRequest => contents.OfType<ToolApprovalRequestContent>().Any(),
                CompletionSignal.ContinuationToken => Response?.ContinuationToken is not null
                    || Updates.Any(update => update.ContinuationToken is not null),
                CompletionSignal.UnknownActionableContent => contents.OfType<UnknownActionableContent>().Any(),
                _ => false
            };
        }
    }

    private sealed class ScriptedChatClient : IChatClient
    {
        private readonly Queue<ChatResponse> responses;
        private readonly Queue<IReadOnlyList<ChatResponseUpdate>> streamingResponses;
        private readonly bool throwCancellation;

        private ScriptedChatClient(
            IEnumerable<ChatResponse>? responses = null,
            IEnumerable<IReadOnlyList<ChatResponseUpdate>>? streamingResponses = null,
            bool throwCancellation = false)
        {
            this.responses = new Queue<ChatResponse>(responses ?? []);
            this.streamingResponses = new Queue<IReadOnlyList<ChatResponseUpdate>>(
                streamingResponses ?? []);
            this.throwCancellation = throwCancellation;
        }

        public int InvocationCount { get; private set; }

        public List<IReadOnlyList<ChatMessage>> Invocations { get; } = [];

        public static ScriptedChatClient ForResponses(params ChatResponse[] responses)
        {
            return new ScriptedChatClient(responses: responses);
        }

        public static ScriptedChatClient ForStreaming(
            params IReadOnlyList<ChatResponseUpdate>[] responses)
        {
            return new ScriptedChatClient(streamingResponses: responses);
        }

        public static ScriptedChatClient ThrowCancellation(bool streaming)
        {
            return streaming
                ? new ScriptedChatClient(streamingResponses: [[]], throwCancellation: true)
                : new ScriptedChatClient(responses: [CreateResponse()], throwCancellation: true);
        }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            Invocations.Add(messages.ToArray());
            if (throwCancellation)
            {
                throw new OperationCanceledException(
                    "The scripted provider cancelled the request.",
                    innerException: null,
                    cancellationToken);
            }

            return Task.FromResult(responses.Dequeue());
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            Invocations.Add(messages.ToArray());
            if (throwCancellation)
            {
                throw new OperationCanceledException(
                    "The scripted provider cancelled the request.",
                    innerException: null,
                    cancellationToken);
            }

            foreach (var update in streamingResponses.Dequeue())
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return update;
            }

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
}
