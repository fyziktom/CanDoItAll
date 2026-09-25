using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Maf;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class Maf122DynamicToolIsolationTests {
    private const string ToolName = "write_project_note";
    private const string FinalizerName = "submit_process_step_outcome";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Overlapping_sessions_preserve_per_run_schema_authority_middleware_and_finalizer(bool streaming) {
        using var client = new OverlappingClient();
        var authority = new AsyncLocal<string?>();
        var receipts = new ConcurrentQueue<string>();
        var middleware = new ConcurrentQueue<string>();
        var finalizers = new ConcurrentQueue<string>();
        var agent = new ChatClientAgent(client, MafChatClientAgentOptionsFactory.Create(new ChatOptions())).AsBuilder()
            .Use(async (_, context, next, token) => {
                Assert.Equal($"{authority.Value}:{context.CallContent.Name}", context.CallContent.CallId);
                middleware.Enqueue($"{authority.Value}:{context.CallContent.Name}");
                return await next(context, token);
            }).Build();
        var firstTool = AIFunctionFactory.Create((string title) => {
            Assert.Equal("first", authority.Value);
            Assert.Equal("first-note", title);
            receipts.Enqueue("first");
            return "first-receipt";
        }, ToolName);
        var secondTool = AIFunctionFactory.Create((int number) => {
            Assert.Equal("second", authority.Value);
            Assert.Equal(42, number);
            receipts.Enqueue("second");
            return "second-receipt";
        }, ToolName);

        await Task.WhenAll(RunAsync("first", firstTool), RunAsync("second", secondTool));

        Assert.Equal(2, client.OverlappingRuns);
        Assert.Equal(["first", "second"], receipts.Order().ToArray());
        Assert.Equal(["first", "second"], finalizers.Order().ToArray());
        Assert.Equal(4, middleware.Count);
        Assert.Contains($"first:{ToolName}", middleware);
        Assert.Contains($"second:{ToolName}", middleware);
        Assert.Contains($"first:{FinalizerName}", middleware);
        Assert.Contains($"second:{FinalizerName}", middleware);

        async Task RunAsync(string project, AIFunction tool) {
            authority.Value = project;
            var finalizer = AIFunctionFactory.Create((string receipt) => {
                Assert.Equal(project, authority.Value);
                Assert.Equal($"{project}-receipt", receipt);
                Assert.Contains(project, receipts);
                finalizers.Enqueue(project);
                return "accepted";
            }, FinalizerName);
            var options = new ChatClientAgentRunOptions(new ChatOptions { Tools = [tool, finalizer] });
            var session = await agent.CreateSessionAsync();
            var messages = new[] { new ChatMessage(ChatRole.User, project) };
            var response = streaming
                ? await agent.RunStreamingAsync(messages, session, options).ToAgentResponseAsync()
                : await agent.RunAsync(messages, session, options);
            Assert.Equal($"{project}-completed", response.Text);
        }
    }

    private sealed class OverlappingClient : IChatClient {
        private readonly TaskCompletionSource bothEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int entered;
        public int OverlappingRuns => entered;

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            var input = messages.ToArray();
            var project = input.First(message => message.Role == ChatRole.User).Text;
            var tools = options!.Tools!;
            var work = Assert.IsAssignableFrom<AIFunctionDeclaration>(Assert.Single(tools, tool => tool.Name == ToolName));
            var properties = work.JsonSchema.GetProperty("properties");
            Assert.True(properties.TryGetProperty(project == "first" ? "title" : "number", out _));
            Assert.False(properties.TryGetProperty(project == "first" ? "number" : "title", out _));
            var results = input.SelectMany(message => message.Contents).OfType<FunctionResultContent>().ToArray();
            AIContent content;
            if (results.Length == 0) {
                if (Interlocked.Increment(ref entered) == 2) {
                    bothEntered.TrySetResult();
                }
                await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
                content = new FunctionCallContent($"{project}:{ToolName}", ToolName, project == "first"
                    ? new Dictionary<string, object?> { ["title"] = "first-note" }
                    : new Dictionary<string, object?> { ["number"] = 42 });
            } else if (results.Length == 1) {
                content = new FunctionCallContent($"{project}:{FinalizerName}", FinalizerName,
                    new Dictionary<string, object?> { ["receipt"] = $"{project}-receipt" });
            } else {
                content = new TextContent($"{project}-completed");
            }
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, [content]));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var update in response.ToChatResponseUpdates()) {
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose() { }
    }
}
