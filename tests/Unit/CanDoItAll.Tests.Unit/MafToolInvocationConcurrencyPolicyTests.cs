using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Maf;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafToolInvocationConcurrencyPolicyTests
{
    private static readonly ChatMessage[] Messages =
    [
        new(ChatRole.User, "Run tools A, B, and C.")
    ];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Production_policy_invokes_multiple_calls_serially_and_in_order(
        bool useStreaming)
    {
        var probe = new InvocationProbe(expectOverlap: false);
        var (response, client) = await RunAsync(
            probe,
            allowConcurrentInvocation: false,
            useStreaming);

        Assert.Equal("completed", response.Text);
        Assert.Equal(2, client.InvocationCount);
        Assert.Equal(1, probe.MaximumActiveInvocations);
        Assert.Equal(
            ["A:start", "A:end", "B:start", "B:end", "C:start", "C:end"],
            probe.Events);
        Assert.Equal(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["A"] = 1,
                ["B"] = 1,
                ["C"] = 1
            },
            probe.InvocationCounts);
        Assert.Equal(["call-a", "call-b", "call-c"], client.FunctionResultCallIds);
    }

    [Fact]
    public async Task Overlap_probe_detects_concurrent_invocation_when_explicitly_opted_in()
    {
        var probe = new InvocationProbe(expectOverlap: true);

        var (response, _) = await RunAsync(
            probe,
            allowConcurrentInvocation: true,
            useStreaming: false);

        Assert.Equal("completed", response.Text);
        Assert.True(probe.MaximumActiveInvocations > 1);
        Assert.Equal(3, probe.InvocationCounts.Values.Sum());
    }

    private static async Task<(AgentResponse Response, MultiToolScriptChatClient Client)> RunAsync(
        InvocationProbe probe,
        bool allowConcurrentInvocation,
        bool useStreaming)
    {
        var tools = new[]
        {
            CreateTool("A", "tool_a", probe),
            CreateTool("B", "tool_b", probe),
            CreateTool("C", "tool_c", probe)
        };
        var chatOptions = new ChatOptions
        {
            AllowMultipleToolCalls = true,
            Tools = [.. tools]
        };
        var agentOptions = MafChatClientAgentOptionsFactory.Create(chatOptions);
        agentOptions.AllowConcurrentInvocation = allowConcurrentInvocation;
        var client = new MultiToolScriptChatClient(tools.Select(tool => tool.Name).ToArray());
        var agent = new ChatClientAgent(client, agentOptions);
        var session = await agent.CreateSessionAsync();

        var response = useStreaming
            ? await agent.RunStreamingAsync(Messages, session).ToAgentResponseAsync()
            : await agent.RunAsync(Messages, session);

        return (response, client);
    }

    private static AIFunction CreateTool(
        string invocationName,
        string toolName,
        InvocationProbe probe)
    {
        return AIFunctionFactory.Create(
            new Func<CancellationToken, Task<string>>(
                cancellationToken => probe.InvokeAsync(invocationName, cancellationToken)),
            toolName,
            $"Test tool {invocationName}.");
    }

    private sealed class InvocationProbe(bool expectOverlap)
    {
        private readonly ConcurrentQueue<string> events = new();
        private readonly ConcurrentDictionary<string, int> invocationCounts =
            new(StringComparer.Ordinal);
        private readonly TaskCompletionSource overlapObserved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int activeInvocations;
        private int maximumActiveInvocations;

        public IReadOnlyList<string> Events => [.. events];

        public IReadOnlyDictionary<string, int> InvocationCounts => invocationCounts;

        public int MaximumActiveInvocations => Volatile.Read(ref maximumActiveInvocations);

        public async Task<string> InvokeAsync(
            string invocationName,
            CancellationToken cancellationToken)
        {
            invocationCounts.AddOrUpdate(invocationName, 1, static (_, count) => count + 1);
            events.Enqueue($"{invocationName}:start");
            var active = Interlocked.Increment(ref activeInvocations);
            UpdateMaximum(active);

            try
            {
                if (expectOverlap)
                {
                    if (active > 1)
                    {
                        overlapObserved.TrySetResult();
                    }

                    await overlapObserved.Task.WaitAsync(
                        TimeSpan.FromSeconds(5),
                        cancellationToken);
                }
                else
                {
                    await Task.Yield();
                }

                return invocationName;
            }
            finally
            {
                events.Enqueue($"{invocationName}:end");
                Interlocked.Decrement(ref activeInvocations);
            }
        }

        private void UpdateMaximum(int active)
        {
            var observed = Volatile.Read(ref maximumActiveInvocations);
            while (observed < active)
            {
                var original = Interlocked.CompareExchange(
                    ref maximumActiveInvocations,
                    active,
                    observed);
                if (original == observed)
                {
                    return;
                }

                observed = original;
            }
        }
    }

    private sealed class MultiToolScriptChatClient(IReadOnlyList<string> toolNames) : IChatClient
    {
        private readonly List<string> functionResultCallIds = [];

        public IReadOnlyList<string> FunctionResultCallIds => functionResultCallIds;

        public int InvocationCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InvocationCount++;

            var results = messages
                .SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>()
                .ToList();
            if (results.Count > 0)
            {
                functionResultCallIds.AddRange(results.Select(result => result.CallId));
                return Task.FromResult(
                    new ChatResponse(new ChatMessage(ChatRole.Assistant, "completed")));
            }

            var calls = toolNames
                .Select((toolName, index) => new FunctionCallContent(
                    $"call-{(char)('a' + index)}",
                    toolName,
                    new Dictionary<string, object?>()))
                .Cast<AIContent>()
                .ToList();

            return Task.FromResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, calls)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var update in response.ToChatResponseUpdates())
            {
                yield return update;
            }
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
