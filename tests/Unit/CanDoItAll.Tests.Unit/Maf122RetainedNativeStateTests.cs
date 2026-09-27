using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class Maf122RetainedNativeStateTests {
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task Genuine_120_pending_state_restores_native_authority_once(bool serviceManaged, bool streaming, bool approve) {
        var fixture = await ReadFixtureAsync(serviceManaged, streaming, "pending");
        var effects = new List<string>();
        using var client = new CompletionClient(serviceManaged);
        var agent = CreateAgent(client, effects);
        var session = await agent.DeserializeSessionAsync(fixture.State);
        var approval = Assert.Single(fixture.Approvals);
        var request = new ToolApprovalRequestContent(approval.RequestId,
            new FunctionCallContent(approval.CallId, approval.Name, approval.Arguments));
        var messages = new[] { new ChatMessage(ChatRole.User, [request.CreateResponse(approve)]) };

        await RunAsync(agent, session, messages, streaming);

        Assert.Equal(approve ? 1 : 0, effects.Count);
        Assert.All(effects, path => Assert.Equal("approved.txt", path));
        var settled = await agent.SerializeSessionAsync(session);
        var restarted = await agent.DeserializeSessionAsync(settled);
        await RunAsync(agent, restarted, messages, streaming);
        Assert.Equal(approve ? 1 : 0, effects.Count);
    }

    [Theory]
    [InlineData(false, false, "ordinary")]
    [InlineData(false, true, "ordinary")]
    [InlineData(true, false, "ordinary")]
    [InlineData(true, true, "ordinary")]
    [InlineData(false, false, "settled")]
    [InlineData(false, true, "settled")]
    [InlineData(true, false, "settled")]
    [InlineData(true, true, "settled")]
    public async Task Genuine_120_ordinary_and_settled_state_remains_readable_without_reexecuting_effects(
        bool serviceManaged, bool streaming, string stage) {
        var fixture = await ReadFixtureAsync(serviceManaged, streaming, stage);
        var effects = new List<string>();
        using var client = new CompletionClient(serviceManaged);
        var agent = CreateAgent(client, effects);
        var session = await agent.DeserializeSessionAsync(fixture.State);

        var response = await RunAsync(agent, session, [new ChatMessage(ChatRole.User, "Continue the conversation.")], streaming);

        Assert.Equal("fixture completed", response.Text);
        Assert.Empty(effects);
        Assert.Equal(serviceManaged ? "fixture-provider-conversation" : null, client.ConversationId);
        if (!serviceManaged) {
            Assert.Contains(client.Messages, message => message.Text == "ordinary");
        }
        if (stage == "settled") {
            var pending = Assert.Single((await ReadFixtureAsync(serviceManaged, streaming, "pending")).Approvals);
            var request = new ToolApprovalRequestContent(pending.RequestId,
                new FunctionCallContent(pending.CallId, pending.Name, pending.Arguments));
            await RunAsync(agent, session, [new ChatMessage(ChatRole.User, [request.CreateResponse(true)])], streaming);
            Assert.Empty(effects);
        }
    }

    private static ChatClientAgent CreateAgent(IChatClient client, List<string> effects) => new(client,
        new ChatClientAgentOptions {
            ChatOptions = new ChatOptions {
                Tools = [new ApprovalRequiredAIFunction(AIFunctionFactory.Create((string path) => {
                    effects.Add(path);
                    return "written";
                }, "write_artifact", "Write the approved fixture artifact."))]
            },
            DisableApprovalResponseBinding = false,
            DisableApprovalNotRequiredFunctionBypassing = true
        });

    private static Task<AgentResponse> RunAsync(AIAgent agent, AgentSession session, IReadOnlyList<ChatMessage> messages, bool streaming) =>
        streaming ? agent.RunStreamingAsync(messages, session).ToAgentResponseAsync() : agent.RunAsync(messages, session);

    private static async Task<NativeFixture> ReadFixtureAsync(bool serviceManaged, bool streaming, string stage) {
        var file = $"{(serviceManaged ? "service" : "local")}-{(streaming ? "streaming" : "blocking")}-{stage}.json";
        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Maf120", "Native", file));
        var fixture = JsonSerializer.Deserialize<NativeFixture>(json, JsonSerializerOptions.Web)!;
        Assert.Equal("1.20.0.0", fixture.MafVersion);
        Assert.Equal(new Version(1, 22, 0, 0), typeof(AIAgent).Assembly.GetName().Version);
        return fixture;
    }

    private sealed record NativeFixture(string MafVersion, JsonElement State, ApprovalFixture[] Approvals);
    private sealed record ApprovalFixture(string RequestId, string CallId, string Name, Dictionary<string, object?> Arguments);

    private sealed class CompletionClient(bool serviceManaged) : IChatClient {
        public IReadOnlyList<ChatMessage> Messages { get; private set; } = [];
        public string? ConversationId { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Messages = messages.ToArray();
            ConversationId = options?.ConversationId;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "fixture completed")) {
                ConversationId = serviceManaged ? "fixture-provider-conversation" : null
            });
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
