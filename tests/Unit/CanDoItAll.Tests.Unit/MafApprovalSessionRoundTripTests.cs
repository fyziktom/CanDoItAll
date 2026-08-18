using System.Text.Json;
using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafApprovalSessionRoundTripTests
{
    private const string ApprovalStateKey = "_pendingApprovalRequests";
    private const string CallId = "call-001";
    private const string ConversationId = "provider-conversation-001";
    private const string HostedMcpApprovalId = "hosted-mcp-approval-001";
    private const string HostedMcpCallId = "hosted-mcp-call-001";
    private const string HostedMcpConversationId = "provider-hosted-mcp-conversation-001";
    private const string HostedMcpServerName = "workspace-mcp";
    private const string HostedMcpToolName = "hosted_workspace_write_file";
    private const string UnknownApprovalId = "approval-unknown";
    private const string ToolName = "workspace_write_file";
    private const string SafePath = "artifacts/approved.txt";
    private const string SecondSafePath = "artifacts/approved-second.txt";
    private const string TamperedPath = "artifacts/tampered.txt";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scrubbed_session_preserves_native_approval_binding_across_restart(
        bool useStreaming)
    {
        var probe = new InvocationProbe();
        var initialAgent = CreateAgent(new ApprovalScriptChatClient(), probe);
        var initialSession = await initialAgent.CreateSessionAsync();

        var initialResponse = await RunAsync(
            initialAgent,
            [new ChatMessage(ChatRole.User, "Request the governed write.")],
            initialSession,
            useStreaming);

        var approval = Assert.Single(
            initialResponse.Messages
                .SelectMany(message => message.Contents)
                .OfType<ToolApprovalRequestContent>());

        Assert.False(string.IsNullOrWhiteSpace(approval.RequestId));
        Assert.Equal(CallId, approval.ToolCall.CallId);
        Assert.Equal(0, probe.InvocationCount);

        var scrubbed = await SerializeAndScrubSessionAsync(
            initialAgent,
            initialSession,
            approval.RequestId);

        var persistedApproval = new MafApprovalContinuationDriver()
            .MapPendingApproval(approval);
        var tamperedApproval = persistedApproval with
        {
            ArgumentsJson = $$"""{"path":"{{TamperedPath}}"}"""
        };
        var timestamp = new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);
        var persistedSession = new ChatSessionRecord(
            Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            AgentId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Title: "Approval binding fixture",
            CreatedAtUtc: timestamp,
            UpdatedAtUtc: timestamp,
            Messages: [],
            Compatibility: new ChatSessionRuntimeCompatibilityRecord(
                runtimeSessionKey: ConversationId,
                serializedSessionStateJson: scrubbed,
                pendingApprovals: [tamperedApproval]));
        var continuationMessages = new MafApprovalContinuationDriver()
            .CreateApprovalInputMessages(
                persistedSession,
                [new AgentRuntimeApprovalDecision(tamperedApproval.ApprovalId, Approved: true)])
            .ToList();

        using var scrubbedDocument = JsonDocument.Parse(scrubbed);
        var continuationAgent = CreateAgent(
            new ApprovalScriptChatClient(),
            probe);
        var restoredSession = await continuationAgent.DeserializeSessionAsync(
            scrubbedDocument.RootElement.Clone());

        var completion = await RunAsync(
            continuationAgent,
            continuationMessages,
            restoredSession,
            useStreaming);

        Assert.Equal("completed", completion.Text);
        Assert.Equal(1, probe.InvocationCount);
        Assert.Equal(SafePath, probe.LastPath);

        var completedState = await continuationAgent.SerializeSessionAsync(
            restoredSession);

        Assert.DoesNotContain(
            ApprovalStateKey,
            completedState.GetRawText(),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scrubbed_session_preserves_hosted_mcp_approval_binding_across_restart(
        bool useStreaming)
    {
        var initialClient = new ApprovalScriptChatClient(
            CreateHostedMcpApprovalRequest());
        var initialAgent = CreateHostedMcpAgent(initialClient);
        var initialSession = await initialAgent.CreateSessionAsync();

        var initialResponse = await RunAsync(
            initialAgent,
            [new ChatMessage(ChatRole.User, "Request the hosted MCP write.")],
            initialSession,
            useStreaming);

        var approval = Assert.Single(GetApprovalRequests(initialResponse));

        Assert.Equal(HostedMcpApprovalId, approval.RequestId);
        AssertHostedMcpCall(approval.ToolCall, SafePath);

        var scrubbed = await SerializeAndScrubSessionAsync(
            initialAgent,
            initialSession,
            approval.RequestId);
        var persistedApproval = new MafApprovalContinuationDriver()
            .MapPendingApproval(approval);
        var tamperedApproval = persistedApproval with
        {
            ArgumentsJson = $$"""{"path":"{{TamperedPath}}"}"""
        };
        var timestamp = new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);
        var persistedSession = new ChatSessionRecord(
            Id: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            AgentId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Title: "Hosted MCP approval binding fixture",
            CreatedAtUtc: timestamp,
            UpdatedAtUtc: timestamp,
            Messages: [],
            Compatibility: new ChatSessionRuntimeCompatibilityRecord(
                runtimeSessionKey: HostedMcpConversationId,
                serializedSessionStateJson: scrubbed,
                pendingApprovals: [tamperedApproval]));
        var continuationMessages = new MafApprovalContinuationDriver()
            .CreateApprovalInputMessages(
                persistedSession,
                [new AgentRuntimeApprovalDecision(tamperedApproval.ApprovalId, Approved: true)])
            .ToList();

        using var scrubbedDocument = JsonDocument.Parse(scrubbed);
        var continuationClient = new ApprovalScriptChatClient(
            CreateHostedMcpApprovalRequest());
        var continuationAgent = CreateHostedMcpAgent(continuationClient);
        var restoredSession = await continuationAgent.DeserializeSessionAsync(
            scrubbedDocument.RootElement.Clone());

        var completion = await RunAsync(
            continuationAgent,
            continuationMessages,
            restoredSession,
            useStreaming);

        Assert.Equal("completed", completion.Text);
        Assert.Equal(1, continuationClient.CompletionCount);
        var capturedResponse = Assert.IsType<ToolApprovalResponseContent>(
            continuationClient.CapturedApprovalResponse);
        Assert.True(capturedResponse.Approved);
        Assert.Equal(HostedMcpApprovalId, capturedResponse.RequestId);
        AssertHostedMcpCall(capturedResponse.ToolCall, SafePath);

        var completedState = await continuationAgent.SerializeSessionAsync(
            restoredSession);

        Assert.DoesNotContain(
            ApprovalStateKey,
            completedState.GetRawText(),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Replayed_approval_response_does_not_invoke_tool_again(
        bool useStreaming)
    {
        var probe = new InvocationProbe();
        var agent = CreateAgent(new ApprovalScriptChatClient(), probe);
        var session = await agent.CreateSessionAsync();
        var approval = await RequestApprovalAsync(
            agent,
            session,
            useStreaming);
        var approvalMessages =
            new[] { new ChatMessage(ChatRole.User, [approval.CreateResponse(approved: true)]) };

        var completion = await RunAsync(
            agent,
            approvalMessages,
            session,
            useStreaming);

        Assert.Equal("completed", completion.Text);
        Assert.Equal(1, probe.InvocationCount);
        Assert.Equal(SafePath, probe.LastPath);

        var replay = await RunAsync(
            agent,
            approvalMessages,
            session,
            useStreaming);

        Assert.Single(GetApprovalRequests(replay));
        Assert.Equal(1, probe.InvocationCount);
        Assert.Equal(SafePath, probe.LastPath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Consecutive_approval_requests_survive_serialized_session_reentry(
        bool useStreaming)
    {
        var probe = new InvocationProbe();
        var initialAgent = CreateAgent(
            new ConsecutiveApprovalScriptChatClient(),
            probe,
            includeCompaction: true);
        var initialSession = await initialAgent.CreateSessionAsync();
        var firstApproval = await RequestApprovalAsync(
            initialAgent,
            initialSession,
            useStreaming);
        var firstSerializedSession = await initialAgent.SerializeSessionAsync(initialSession);

        var secondAgent = CreateAgent(
            new ConsecutiveApprovalScriptChatClient(),
            probe,
            includeCompaction: true);
        var secondSession = await secondAgent.DeserializeSessionAsync(firstSerializedSession);
        var firstContinuationMessages = CreateRehydratedApprovalMessages(
            firstApproval,
            firstSerializedSession);
        var secondResponse = await RunAsync(
            secondAgent,
            firstContinuationMessages,
            secondSession,
            useStreaming);
        var secondApproval = Assert.Single(GetApprovalRequests(secondResponse));

        Assert.Equal(1, probe.InvocationCount);
        Assert.Equal(SafePath, probe.LastPath);
        Assert.NotEqual(firstApproval.RequestId, secondApproval.RequestId);

        var secondSerializedSession = await secondAgent.SerializeSessionAsync(secondSession);
        Assert.Contains(
            secondApproval.RequestId,
            secondSerializedSession.GetRawText(),
            StringComparison.Ordinal);

        var finalAgent = CreateAgent(
            new ConsecutiveApprovalScriptChatClient(),
            probe,
            includeCompaction: true);
        var finalSession = await finalAgent.DeserializeSessionAsync(secondSerializedSession);
        var secondContinuationMessages = CreateRehydratedApprovalMessages(
            secondApproval,
            secondSerializedSession);
        var completion = await RunAsync(
            finalAgent,
            secondContinuationMessages,
            finalSession,
            useStreaming);

        Assert.Equal("completed", completion.Text);
        Assert.Equal(2, probe.InvocationCount);
        Assert.Equal(SecondSafePath, probe.LastPath);
    }

    private static IReadOnlyList<ChatMessage> CreateRehydratedApprovalMessages(
        ToolApprovalRequestContent approval,
        JsonElement serializedSession)
    {
        var pending = new MafApprovalContinuationDriver().MapPendingApproval(approval);
        var timestamp = new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);
        var persistedSession = new ChatSessionRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Title: "Approval binding fixture",
            CreatedAtUtc: timestamp,
            UpdatedAtUtc: timestamp,
            Messages: [],
            Compatibility: new ChatSessionRuntimeCompatibilityRecord(
                runtimeSessionKey: ConversationId,
                serializedSessionStateJson: serializedSession.GetRawText(),
                pendingApprovals: [pending]));

        return new MafApprovalContinuationDriver()
            .CreateApprovalInputMessages(
                persistedSession,
                [new AgentRuntimeApprovalDecision(pending.ApprovalId, Approved: true)])
            .ToList();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Approval_response_from_another_session_does_not_invoke_tool(
        bool useStreaming)
    {
        var probe = new InvocationProbe();
        var agent = CreateAgent(new ApprovalScriptChatClient(), probe);
        var sourceSession = await agent.CreateSessionAsync();
        var approval = await RequestApprovalAsync(
            agent,
            sourceSession,
            useStreaming);
        var targetSession = await agent.CreateSessionAsync();

        var response = await RunAsync(
            agent,
            [new ChatMessage(ChatRole.User, [approval.CreateResponse(approved: true)])],
            targetSession,
            useStreaming);

        Assert.Single(GetApprovalRequests(response));
        Assert.Equal(0, probe.InvocationCount);
        Assert.Equal(string.Empty, probe.LastPath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unknown_approval_response_does_not_invoke_tool(
        bool useStreaming)
    {
        var probe = new InvocationProbe();
        var agent = CreateAgent(new ApprovalScriptChatClient(), probe);
        var session = await agent.CreateSessionAsync();
        var unknownApproval = new ToolApprovalRequestContent(
            UnknownApprovalId,
            new FunctionCallContent(
                CallId,
                ToolName,
                new Dictionary<string, object?>
                {
                    ["path"] = SafePath
                }));

        var response = await RunAsync(
            agent,
            [new ChatMessage(ChatRole.User, [unknownApproval.CreateResponse(approved: true)])],
            session,
            useStreaming);

        Assert.Single(GetApprovalRequests(response));
        Assert.Equal(0, probe.InvocationCount);
        Assert.Equal(string.Empty, probe.LastPath);
    }

    private static ChatClientAgent CreateAgent(
        IChatClient chatClient,
        InvocationProbe probe,
        bool includeCompaction = false)
    {
        var function = AIFunctionFactory.Create(
            new Func<string, string>(probe.Write),
            ToolName,
            "Writes one governed artifact.");

        var options = new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions
            {
                Tools =
                [
                    new ApprovalRequiredAIFunction(function)
                ]
            },
            UseProvidedChatClientAsIs = false,
            DisableApprovalNotRequiredFunctionBypassing = true,
            DisableApprovalResponseBinding = false
        };
        if (includeCompaction)
        {
#pragma warning disable MAAI001
            options.AIContextProviders =
            [
                new CompactionProvider(
                    new PipelineCompactionStrategy(
                        new ToolResultCompactionStrategy(CompactionTriggers.MessagesExceed(40)),
                        new SlidingWindowCompactionStrategy(CompactionTriggers.TurnsExceed(32)),
                        new TruncationCompactionStrategy(CompactionTriggers.TokensExceed(64000))))
            ];
#pragma warning restore MAAI001
            options.ChatHistoryProvider = new InMemoryChatHistoryProvider();
            options.RequirePerServiceCallChatHistoryPersistence = true;
        }

        return new ChatClientAgent(
            chatClient,
            options);
    }

    private static ChatClientAgent CreateHostedMcpAgent(
        IChatClient chatClient)
    {
        return new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                UseProvidedChatClientAsIs = false,
                DisableApprovalNotRequiredFunctionBypassing = true,
                DisableApprovalResponseBinding = false
            });
    }

    private static ToolApprovalRequestContent CreateHostedMcpApprovalRequest()
    {
        return new ToolApprovalRequestContent(
            HostedMcpApprovalId,
            new McpServerToolCallContent(
                HostedMcpCallId,
                HostedMcpToolName,
                HostedMcpServerName)
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["path"] = SafePath
                }
            });
    }

    private static void AssertHostedMcpCall(
        ToolCallContent toolCall,
        string expectedPath)
    {
        var hostedMcpCall = Assert.IsType<McpServerToolCallContent>(toolCall);

        Assert.Equal(HostedMcpCallId, hostedMcpCall.CallId);
        Assert.Equal(HostedMcpToolName, hostedMcpCall.Name);
        Assert.Equal(HostedMcpServerName, hostedMcpCall.ServerName);
        Assert.NotNull(hostedMcpCall.Arguments);

        var arguments = JsonSerializer.SerializeToElement(
            hostedMcpCall.Arguments);

        Assert.Equal(
            expectedPath,
            arguments.GetProperty("path").GetString());
    }

    private static async Task<string> SerializeAndScrubSessionAsync(
        AIAgent agent,
        AgentSession session,
        string approvalRequestId)
    {
        session.StateBag.SetValue(
            "requestScopedAttachmentFixture",
            new List<ChatMessage>
            {
                new(
                    ChatRole.User,
                    [
                        new DataContent(new byte[] { 1, 2, 3 }, "image/png")
                        {
                            Name = "proof.png"
                        }
                    ])
            });

        var serialized = await agent.SerializeSessionAsync(session);
        var beforeScrub = serialized.GetRawText();

        Assert.Contains(ApprovalStateKey, beforeScrub, StringComparison.Ordinal);
        Assert.Contains(approvalRequestId, beforeScrub, StringComparison.Ordinal);
        Assert.Contains("\"$type\":\"data\"", beforeScrub, StringComparison.OrdinalIgnoreCase);

        var scrubbed = RequestScopedSessionContentScrubber
            .RemoveRequestScopedDataContent(beforeScrub);

        Assert.NotNull(scrubbed);
        Assert.Contains(ApprovalStateKey, scrubbed, StringComparison.Ordinal);
        Assert.Contains(approvalRequestId, scrubbed, StringComparison.Ordinal);
        Assert.DoesNotContain("\"$type\":\"data\"", scrubbed, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("proof.png", scrubbed, StringComparison.Ordinal);

        return scrubbed;
    }

    private static async Task<ToolApprovalRequestContent> RequestApprovalAsync(
        AIAgent agent,
        AgentSession session,
        bool useStreaming)
    {
        var response = await RunAsync(
            agent,
            [new ChatMessage(ChatRole.User, "Request the governed write.")],
            session,
            useStreaming);

        return Assert.Single(GetApprovalRequests(response));
    }

    private static IEnumerable<ToolApprovalRequestContent> GetApprovalRequests(
        AgentResponse response)
    {
        return response.Messages
            .SelectMany(message => message.Contents)
            .OfType<ToolApprovalRequestContent>();
    }

    private static async Task<AgentResponse> RunAsync(
        AIAgent agent,
        IReadOnlyList<ChatMessage> messages,
        AgentSession session,
        bool useStreaming)
    {
        if (!useStreaming)
        {
            return await agent.RunAsync(messages, session);
        }

        return await agent
            .RunStreamingAsync(messages, session)
            .ToAgentResponseAsync();
    }

    private sealed class InvocationProbe
    {
        public int InvocationCount { get; private set; }

        public string LastPath { get; private set; } = string.Empty;

        public string Write(string path)
        {
            InvocationCount++;
            LastPath = path;
            return "written";
        }
    }

    private sealed class ApprovalScriptChatClient : IChatClient
    {
        private readonly ToolApprovalRequestContent? hostedMcpApprovalRequest;

        public ApprovalScriptChatClient(
            ToolApprovalRequestContent? hostedMcpApprovalRequest = null)
        {
            this.hostedMcpApprovalRequest = hostedMcpApprovalRequest;
        }

        public ToolApprovalResponseContent? CapturedApprovalResponse { get; private set; }

        public int CompletionCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var contents = messages
                .SelectMany(message => message.Contents)
                .ToList();

            var approvalResponse = contents
                .OfType<ToolApprovalResponseContent>()
                .SingleOrDefault();

            if (approvalResponse is not null)
            {
                CapturedApprovalResponse = approvalResponse;
                return Task.FromResult(CreateCompletionResponse());
            }

            if (contents.OfType<FunctionResultContent>().Any())
            {
                return Task.FromResult(CreateCompletionResponse());
            }

            AIContent nextContent = hostedMcpApprovalRequest is not null
                ? hostedMcpApprovalRequest
                : new FunctionCallContent(
                    CallId,
                    ToolName,
                    new Dictionary<string, object?>
                    {
                        ["path"] = SafePath
                    });

            return Task.FromResult(
                new ChatResponse(
                    new ChatMessage(
                        ChatRole.Assistant,
                        [nextContent]))
                {
                    ConversationId = ResolveConversationId()
                });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(
                messages,
                options,
                cancellationToken);
            foreach (var update in response.ToChatResponseUpdates())
            {
                yield return update;
            }
        }

        public object? GetService(
            Type serviceType,
            object? serviceKey = null)
        {
            return serviceKey is null && serviceType.IsInstanceOfType(this)
                ? this
                : null;
        }

        public void Dispose()
        {
        }

        private ChatResponse CreateCompletionResponse()
        {
            CompletionCount++;
            return new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "completed"))
            {
                ConversationId = ResolveConversationId()
            };
        }

        private string ResolveConversationId()
        {
            return hostedMcpApprovalRequest is null
                ? ConversationId
                : HostedMcpConversationId;
        }
    }

    private sealed class ConsecutiveApprovalScriptChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var functionResults = messages
                .SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>()
                .ToList();
            if (functionResults.Any(result => string.Equals(result.CallId, "call-002", StringComparison.Ordinal)))
            {
                return Task.FromResult(CreateResponse(new TextContent("completed")));
            }

            var nextCall = functionResults.Any(result => string.Equals(result.CallId, CallId, StringComparison.Ordinal))
                ? new FunctionCallContent(
                    "call-002",
                    ToolName,
                    new Dictionary<string, object?>
                    {
                        ["path"] = SecondSafePath
                    })
                : new FunctionCallContent(
                    CallId,
                    ToolName,
                    new Dictionary<string, object?>
                    {
                        ["path"] = SafePath
                    });

            return Task.FromResult(CreateResponse(nextCall));
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

        private static ChatResponse CreateResponse(AIContent content)
        {
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, [content]));
        }
    }
}
