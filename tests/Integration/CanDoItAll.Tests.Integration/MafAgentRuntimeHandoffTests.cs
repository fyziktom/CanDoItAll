using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Integration;

public sealed class MafAgentRuntimeHandoffTests
{
    [Fact]
    public async Task Handoff_workflow_transfers_from_entry_agent_to_target_agent()
    {
        var entryAgentId = Guid.NewGuid();
        var qaAgentId = Guid.NewGuid();
        var entryAgent = new ScriptedHandoffAgent(entryAgentId, "Implementer", handoffWhenAvailable: true);
        var qaAgent = new ScriptedHandoffAgent(qaAgentId, "QA", handoffWhenAvailable: false);
        var settings = CreateSettings(entryAgentId, qaAgentId, returnToPrevious: false);

        var build = MafHandoffWorkflowFactory.Build(
            settings,
            new Dictionary<Guid, AIAgent>
            {
                [entryAgentId] = entryAgent,
                [qaAgentId] = qaAgent
            },
            entryAgentId,
            "handoff-transfer-test",
            InProcessExecution.Lockstep);

        var session = await build.Agent.CreateSessionAsync();
        var response = await build.Agent.RunAsync("Deliver the feature with QA artifacts.", session);

        Assert.Equal(1, entryAgent.InvocationCount);
        Assert.Equal(1, qaAgent.InvocationCount);
        Assert.Equal(5, response.Messages.Count);
        Assert.IsType<FunctionCallContent>(Assert.Single(response.Messages[2].Contents));
        Assert.IsType<FunctionResultContent>(Assert.Single(response.Messages[3].Contents));
        Assert.Equal(
            "QA processed: Implementer processed: Deliver the feature with QA artifacts.",
            response.Messages[^1].Text);
    }

    [Fact]
    public async Task Handoff_streaming_keeps_activity_but_projects_the_last_terminal_assistant_update()
    {
        var entryAgentId = Guid.NewGuid();
        var qaAgentId = Guid.NewGuid();
        var entryAgent = new ScriptedHandoffAgent(entryAgentId, "Implementer", handoffWhenAvailable: true);
        var qaAgent = new ScriptedHandoffAgent(qaAgentId, "QA", handoffWhenAvailable: false);
        var settings = CreateSettings(entryAgentId, qaAgentId, returnToPrevious: false);
        var build = MafHandoffWorkflowFactory.Build(
            settings,
            new Dictionary<Guid, AIAgent>
            {
                [entryAgentId] = entryAgent,
                [qaAgentId] = qaAgent
            },
            entryAgentId,
            "handoff-streaming-test",
            InProcessExecution.Lockstep);
        var session = await build.Agent.CreateSessionAsync();
        var updates = new List<AgentResponseUpdate>();

        await foreach (var update in build.Agent.RunStreamingAsync(
                           "Deliver the feature with QA artifacts.",
                           session))
        {
            updates.Add(update);
        }

        var terminalUpdates = updates
            .Where(build.IsTerminalResponseUpdate)
            .ToList();
        var terminalUpdate = terminalUpdates[^1];
        var activityResponse = updates
            .Select(MafAgentResponseSnapshotter.SnapshotUpdate)
            .ToAgentResponse();
        var projectedResponse = MafRuntimeResponseAssembler.ProjectTerminalResponse(
            activityResponse,
            MafAgentResponseSnapshotter.SnapshotUpdate(terminalUpdate));

        Assert.Contains(
            updates,
            update => update.Text.StartsWith("Implementer processed:", StringComparison.Ordinal));
        Assert.True(terminalUpdates.Count > 1);
        var terminalOutput = Assert.IsType<WorkflowOutputEvent>(terminalUpdate.RawRepresentation);
        Assert.IsAssignableFrom<IReadOnlyList<ChatMessage>>(terminalOutput.Data);
        Assert.False(terminalOutput.IsIntermediate());
        Assert.All(
            updates.Where(update =>
                update.RawRepresentation is AgentResponseUpdateEvent or AgentResponseEvent),
            update => Assert.False(build.IsTerminalResponseUpdate(update)));
        Assert.Equal(
            "QA processed: Implementer processed: Deliver the feature with QA artifacts.",
            projectedResponse.Text);
        Assert.Contains("Implementer processed:", activityResponse.Text, StringComparison.Ordinal);
        Assert.Equal(1, entryAgent.InvocationCount);
        Assert.Equal(1, qaAgent.InvocationCount);
    }

    [Fact]
    public async Task Handoff_workflow_return_to_previous_routes_followup_to_last_specialist()
    {
        var entryAgentId = Guid.NewGuid();
        var qaAgentId = Guid.NewGuid();
        var entryAgent = new ScriptedHandoffAgent(entryAgentId, "Implementer", handoffWhenAvailable: true);
        var qaAgent = new ScriptedHandoffAgent(qaAgentId, "QA", handoffWhenAvailable: false);
        var settings = CreateSettings(entryAgentId, qaAgentId, returnToPrevious: true);

        var build = MafHandoffWorkflowFactory.Build(
            settings,
            new Dictionary<Guid, AIAgent>
            {
                [entryAgentId] = entryAgent,
                [qaAgentId] = qaAgent
            },
            entryAgentId,
            "handoff-return-test",
            InProcessExecution.Lockstep);

        var session = await build.Agent.CreateSessionAsync();
        await build.Agent.RunAsync("Deliver the feature with QA artifacts.", session);
        await build.Agent.RunAsync("Add one more QA check.", session);

        Assert.Equal(1, entryAgent.InvocationCount);
        Assert.Equal(2, qaAgent.InvocationCount);
    }

    [Fact]
    public async Task Handoff_depth_guard_fails_when_workflow_exceeds_configured_depth()
    {
        var guardedAgent = new HandoffDepthGuardAgent(
            new BurstHandoffAgent(),
            maxHandoffDepth: 1,
            correlationId: "handoff-depth-test");

        var session = await guardedAgent.CreateSessionAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => guardedAgent.RunAsync("Trigger repeated handoffs.", session));
        Assert.Contains("maxHandoffDepth 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("handoff-depth-test", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handoff_depth_guard_rejects_a_handoff_without_a_stable_call_id()
    {
        var guardedAgent = new HandoffDepthGuardAgent(
            new ToolCallBurstAgent(string.Empty),
            maxHandoffDepth: 1,
            correlationId: "handoff-missing-id-test");
        var session = await guardedAgent.CreateSessionAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => guardedAgent.RunAsync("Trigger an invalid handoff.", session));

        Assert.Contains("without a stable call identifier", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handoff_depth_guard_counts_a_repeated_call_id_once()
    {
        var guardedAgent = new HandoffDepthGuardAgent(
            new ToolCallBurstAgent("handoff-repeat", "handoff-repeat"),
            maxHandoffDepth: 1,
            correlationId: "handoff-repeat-test");
        var session = await guardedAgent.CreateSessionAsync();

        var response = await guardedAgent.RunAsync("Trigger a fragmented handoff.", session);

        Assert.Equal(2, response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().Count());
    }

    private static AgentHandoffSettings CreateSettings(
        Guid entryAgentId,
        Guid qaAgentId,
        bool returnToPrevious)
    {
        return new AgentHandoffSettings
        {
            Enabled = true,
            EntryAgentId = entryAgentId,
            ReturnToPrevious = returnToPrevious,
            MaxHandoffDepth = 4,
            EmitAgentResponseUpdateEvents = true,
            Routes =
            [
                new AgentHandoffRouteSettings
                {
                    SourceAgentId = entryAgentId,
                    TargetAgentId = qaAgentId,
                    Reason = "QA validates implementation outputs and required artifacts."
                }
            ]
        };
    }

    private sealed class ScriptedHandoffAgent(
        Guid agentId,
        string name,
        bool handoffWhenAvailable) : AIAgent
    {
        protected override string? IdCore => agentId.ToString("D");

        public override string? Name => name;

        public override string? Description => $"{name} deterministic test agent.";

        public int InvocationCount { get; private set; }

        protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<AgentSession>(new ScriptedAgentSession());
        }

        protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
            AgentSession session,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(JsonSerializer.SerializeToElement(new { agentId }, jsonSerializerOptions));
        }

        protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
            JsonElement serializedState,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<AgentSession>(new ScriptedAgentSession());
        }

        protected override Task<AgentResponse> RunCoreAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return RunCoreStreamingAsync(messages, session, options, cancellationToken)
                .ToAgentResponseAsync(cancellationToken);
        }

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            InvocationCount++;
            yield return new AgentResponseUpdate(
                ChatRole.Assistant,
                [new TextContent($"{name} processed: {ResolveUserText(messages)}")])
            {
                AuthorName = name,
                MessageId = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTimeOffset.UtcNow
            };

            var handoffToolName = ResolveHandoffToolName(options);
            if (handoffWhenAvailable && handoffToolName is not null)
            {
                yield return new AgentResponseUpdate(
                    ChatRole.Assistant,
                    [new FunctionCallContent(Guid.NewGuid().ToString("N"), handoffToolName)])
                {
                    AuthorName = name,
                    MessageId = Guid.NewGuid().ToString("N"),
                    CreatedAt = DateTimeOffset.UtcNow
                };
            }
        }
    }

    private sealed class BurstHandoffAgent : AIAgent
    {
        protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<AgentSession>(new ScriptedAgentSession());
        }

        protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
            AgentSession session,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(JsonSerializer.SerializeToElement(new { ok = true }, jsonSerializerOptions));
        }

        protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
            JsonElement serializedState,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<AgentSession>(new ScriptedAgentSession());
        }

        protected override Task<AgentResponse> RunCoreAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return RunCoreStreamingAsync(messages, session, options, cancellationToken)
                .ToAgentResponseAsync(cancellationToken);
        }

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new AgentResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("handoff-1", "handoff_to_1")]);
            yield return new AgentResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("handoff-2", "handoff_to_2")]);
        }
    }

    private sealed class ToolCallBurstAgent(params string[] callIds) : AIAgent
    {
        protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<AgentSession>(new ScriptedAgentSession());
        }

        protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
            AgentSession session,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(JsonSerializer.SerializeToElement(new { ok = true }, jsonSerializerOptions));
        }

        protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
            JsonElement serializedState,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<AgentSession>(new ScriptedAgentSession());
        }

        protected override Task<AgentResponse> RunCoreAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return RunCoreStreamingAsync(messages, session, options, cancellationToken)
                .ToAgentResponseAsync(cancellationToken);
        }

        protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            foreach (var callId in callIds)
            {
                yield return new AgentResponseUpdate(
                    ChatRole.Assistant,
                    [new FunctionCallContent(callId, "handoff_to_target")]);
            }
        }
    }

    private sealed class ScriptedAgentSession : AgentSession;

    private static string ResolveUserText(IEnumerable<ChatMessage> messages)
    {
        return messages.LastOrDefault(message => message.Role == ChatRole.User)?.Text ?? string.Empty;
    }

    private static string? ResolveHandoffToolName(AgentRunOptions? options)
    {
        return (options as ChatClientAgentRunOptions)?
            .ChatOptions?
            .Tools?
            .Select(tool => tool.Name)
            .FirstOrDefault(name => name is not null && name.StartsWith("handoff_to_", StringComparison.Ordinal));
    }
}
