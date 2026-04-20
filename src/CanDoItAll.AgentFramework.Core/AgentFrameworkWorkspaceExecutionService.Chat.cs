using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceExecutionService
{
    public async Task<IReadOnlyList<ChatSessionRecord>> ListChatSessionsAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        var executionState = await store.LoadExecutionAsync(cancellationToken);
        return executionState.ChatSessions
            .Where(item => item.AgentId == agentId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();
    }

    public async Task<ChatSessionRecord> GetOrCreateChatSessionAsync(
        Guid agentId,
        Guid? chatSessionId = null,
        CancellationToken cancellationToken = default)
    {
        if (chatSessionId.HasValue)
        {
            var document = await store.LoadAsync(cancellationToken);
            EnsureAgentExists(document.ToCatalog(), agentId);
            return EnsureAgentOwnsSession(document.ToExecutionState(), agentId, chatSessionId.Value);
        }

        ChatSessionRecord? createdSession = null;
        await store.UpdateWorkspaceAsync(document =>
        {
            var catalog = document.ToCatalog();
            var executionState = document.ToExecutionState();
            EnsureAgentExists(catalog, agentId);

            createdSession = new ChatSessionRecord(
                Id: Guid.NewGuid(),
                AgentId: agentId,
                Title: "New exploration thread",
                CreatedAtUtc: DateTimeOffset.UtcNow,
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                RuntimeSessionKey: string.Empty,
                SerializedSessionStateJson: null,
                Messages: [],
                PendingApprovals: []);

            return SandboxWorkspaceDocument.Combine(
                catalog,
                executionState with
                {
                    ChatSessions = ReplaceChatSession(executionState.ChatSessions, createdSession)
                });
        }, cancellationToken);

        return createdSession ?? throw new InvalidOperationException("Chat session could not be created.");
    }

    public async Task<AgentChatRunResult> SendMessageAsync(
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new InvalidOperationException("Prompt is required.");
        }

        var document = await store.LoadAsync(cancellationToken);
        var catalog = document.ToCatalog();
        var executionState = document.ToExecutionState();
        var agent = EnsureAgentExists(catalog, agentId);
        var provider = await ResolveProviderForAgentAsync(agent, catalog, cancellationToken);
        var session = chatSessionId.HasValue
            ? EnsureAgentOwnsSession(executionState, agentId, chatSessionId.Value)
            : null;

        var result = await ExecuteRunCoreAsync(
            agent,
            provider,
            catalog,
            executionState,
            session,
            new ExecutionRunRequest(
                AgentId: agentId,
                Prompt: prompt.Trim(),
                ChatSessionId: session?.Id,
                Context: new ExecutionInvocationContext(
                    SourceKind: "chat-session",
                    SourceId: session?.Id.ToString("N") ?? string.Empty,
                    CorrelationId: string.Empty,
                    CausationId: string.Empty,
                    RequestedBy: "sandbox-chat",
                    RequestedByKind: "interactive",
                    MetadataJson: "{}"),
                AutoApprovePendingToolCalls: false),
            persistTranscript: true,
            cancellationToken);

        return new AgentChatRunResult(
            ChatSessionId: result.ChatSessionId ?? throw new InvalidOperationException("Chat-backed execution runs must return a chat session id."),
            AssistantMessage: result.AssistantMessage ?? throw new InvalidOperationException("Chat-backed execution runs must produce an assistant message."),
            Metric: result.Metric)
        {
            ExecutionRunId = result.ExecutionRunId
        };
    }

    public async Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
        Guid agentId,
        Guid chatSessionId,
        bool approved,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default)
    {
        var document = await store.LoadAsync(cancellationToken);
        EnsureAgentExists(document.ToCatalog(), agentId);
        EnsureAgentOwnsSession(document.ToExecutionState(), agentId, chatSessionId);

        var executionRunId = await ResolveOrCreatePendingExecutionRunIdAsync(agentId, chatSessionId, cancellationToken);
        var result = await ContinueExecutionRunAsync(executionRunId, approved, autoApprovePendingToolCalls, cancellationToken);

        return new AgentChatRunResult(
            ChatSessionId: result.ChatSessionId ?? chatSessionId,
            AssistantMessage: result.AssistantMessage ?? throw new InvalidOperationException("Chat-backed approval continuations must produce an assistant message."),
            Metric: result.Metric)
        {
            ExecutionRunId = result.ExecutionRunId
        };
    }

    public async Task<IReadOnlyList<ExecutionLogEntry>> ListExecutionLogAsync(
        Guid agentId,
        Guid? chatSessionId = null,
        CancellationToken cancellationToken = default)
    {
        var executionState = await store.LoadExecutionAsync(cancellationToken);
        return executionState.ExecutionLog
            .Where(item => item.AgentId == agentId && (!chatSessionId.HasValue || item.ChatSessionId == chatSessionId.Value))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<AgentRunMetric>> ListMetricsAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        var executionState = await store.LoadExecutionAsync(cancellationToken);
        return executionState.Metrics
            .Where(item => item.AgentId == agentId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    private async Task<(ChatSessionRecord Session, AgentRuntimeResponse Response, int TotalInputTokens, int TotalOutputTokens, int TotalToolCalls)> ContinueAutoApprovedRunAsync(
        ExecutionRunRecord run,
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        Guid? chatSessionId,
        IReadOnlyList<CapabilityCatalogItem> attachedCapabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        AgentRuntimeResponse initialResponse,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken)
    {
        var currentSession = session;
        var currentResponse = initialResponse;
        var totalInputTokens = initialResponse.InputTokens;
        var totalOutputTokens = initialResponse.OutputTokens;
        var totalToolCalls = initialResponse.ToolCalls;

        while (currentResponse.PendingApprovals.Count > 0 && ShouldAutoApprovePendingToolCalls(agent, currentSession))
        {
            await AppendExecutionLogAsync(
                run.Id,
                agent.Id,
                chatSessionId,
                ExecutionState.WaitingOnTool,
                "Approval policy",
                $"{ResolveApprovalPolicySource(agent, currentSession)} auto-approved {currentResponse.PendingApprovals.Count} pending tool request(s).",
                cancellationToken);

            currentSession = currentSession with
            {
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Compatibility = ChatSessionRuntimeCompatibilityAdapter.CreateCompatibility(
                    currentResponse.RuntimeSessionKey,
                    currentResponse.SerializedSessionStateJson,
                    currentResponse.PendingApprovals,
                    ChatSessionRuntimeCompatibilityAdapter.AutoApprovePendingToolCalls(currentSession))
            };

            currentResponse = await runtime.RespondToPendingApprovalsAsync(
                agent,
                provider,
                currentSession,
                attachedCapabilities,
                memory,
                approved: true,
                string.IsNullOrWhiteSpace(ChatSessionRuntimeCompatibilityAdapter.RuntimeSessionKeyOrEmpty(currentSession))
                    ? null
                    : ChatSessionRuntimeCompatibilityAdapter.RuntimeSessionKeyOrEmpty(currentSession),
                progressCallback,
                cancellationToken,
                suppressApprovalRequirements: true);

            totalInputTokens += currentResponse.InputTokens;
            totalOutputTokens += currentResponse.OutputTokens;
            totalToolCalls += currentResponse.ToolCalls;
        }

        return (currentSession, currentResponse, totalInputTokens, totalOutputTokens, totalToolCalls);
    }

    private static bool ShouldAutoApprovePendingToolCalls(AgentDefinition agent, ChatSessionRecord session)
        => agent.Permissions.AutoApproveExternalCallsByDefault || ChatSessionRuntimeCompatibilityAdapter.AutoApprovePendingToolCalls(session);

    private static string ResolveApprovalPolicySource(AgentDefinition agent, ChatSessionRecord session)
    {
        if (ChatSessionRuntimeCompatibilityAdapter.AutoApprovePendingToolCalls(session))
        {
            return "Run auto-approve";
        }

        return agent.Permissions.AutoApproveExternalCallsByDefault
            ? "Agent default auto-approve"
            : "Manual approval";
    }

    private static IReadOnlyList<CapabilityCatalogItem> ResolveAttachedCapabilities(
        SandboxWorkspaceCatalog catalog,
        AgentDefinition agent)
    {
        var attachedCapabilityIds = agent.Capabilities.Select(item => item.CapabilityId).ToHashSet();
        return catalog.Capabilities
            .Where(item => attachedCapabilityIds.Contains(item.Id))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<AgentMemoryRecord> ResolveAgentMemory(
        SandboxWorkspaceCatalog catalog,
        Guid agentId)
    {
        return catalog.Memory
            .Where(item => item.AgentId == agentId)
            .OrderByDescending(item => item.Importance)
            .ThenByDescending(item => item.CreatedAtUtc)
            .ToList();
    }
}
