using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceExecutionService
{
    public async Task<IReadOnlyList<ChatSessionRecord>> ListChatSessionsAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        if (store is ISandboxWorkspaceChatQueryStore chatQueryStore)
        {
            var summaries = await chatQueryStore.ListChatSessionSummariesAsync(agentId, cancellationToken);
            var sessions = new List<ChatSessionRecord>(summaries.Count);
            foreach (var summary in summaries)
            {
                if (await chatQueryStore.GetChatSessionAsync(summary.Id, cancellationToken) is { } session &&
                    session.AgentId == agentId)
                {
                    sessions.Add(session);
                }
            }

            return sessions
                .OrderByDescending(item => item.UpdatedAtUtc)
                .ToList();
        }

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
            if (store is ISandboxWorkspaceChatQueryStore chatQueryStore)
            {
                var catalog = await store.LoadCatalogAsync(cancellationToken);
                EnsureAgentExists(catalog, agentId);
                return EnsureAgentOwnsSession(
                    await chatQueryStore.GetChatSessionAsync(chatSessionId.Value, cancellationToken),
                    agentId,
                    chatSessionId.Value);
            }

            var document = await store.LoadAsync(cancellationToken);
            EnsureAgentExists(document.ToCatalog(), agentId);
            return EnsureAgentOwnsSession(document.ToExecutionState(), agentId, chatSessionId.Value);
        }

        if (store is ISandboxWorkspaceChatSessionStore chatSessionStore)
        {
            var catalog = await store.LoadCatalogAsync(cancellationToken);
            EnsureAgentExists(catalog, agentId);
            var now = DateTimeOffset.UtcNow;
            return await chatSessionStore.CreateChatSessionAsync(
                new ChatSessionRecord(
                    Id: Guid.NewGuid(),
                    AgentId: agentId,
                    Title: "New exploration thread",
                    CreatedAtUtc: now,
                    UpdatedAtUtc: now,
                    RuntimeSessionKey: string.Empty,
                    SerializedSessionStateJson: null,
                    Messages: [],
                    PendingApprovals: []),
                cancellationToken);
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

    public async Task<ChatSessionRecord> RenameChatSessionAsync(
        Guid agentId,
        Guid chatSessionId,
        string title,
        CancellationToken cancellationToken = default)
    {
        var normalizedTitle = NormalizeChatSessionTitle(title);
        if (store is ISandboxWorkspaceChatQueryStore chatQueryStore &&
            store is ISandboxWorkspaceChatSessionStore chatSessionStore)
        {
            var catalog = await store.LoadCatalogAsync(cancellationToken);
            EnsureAgentExists(catalog, agentId);
            var session = EnsureAgentOwnsSession(
                await chatQueryStore.GetChatSessionAsync(chatSessionId, cancellationToken),
                agentId,
                chatSessionId);

            return await chatSessionStore.UpdateChatSessionAsync(
                session with
                {
                    Title = normalizedTitle,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                },
                cancellationToken);
        }

        ChatSessionRecord? renamedSession = null;
        await store.UpdateWorkspaceAsync(document =>
        {
            var catalog = document.ToCatalog();
            var executionState = document.ToExecutionState();
            EnsureAgentExists(catalog, agentId);

            var session = EnsureAgentOwnsSession(executionState, agentId, chatSessionId);
            renamedSession = session with
            {
                Title = normalizedTitle,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            return SandboxWorkspaceDocument.Combine(
                catalog,
                executionState with
                {
                    ChatSessions = ReplaceChatSession(executionState.ChatSessions, renamedSession)
                });
        }, cancellationToken);

        return renamedSession ?? throw new InvalidOperationException("Chat session could not be renamed.");
    }

    public async Task<AgentChatRunResult> SendMessageAsync(
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? attachmentPaths = null,
        AgentChatRunOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new InvalidOperationException("Prompt is required.");
        }

        if (store is ISandboxWorkspaceExecutionRunStore &&
            store is ISandboxWorkspaceChatQueryStore chatQueryStore)
        {
            var splitCatalog = await store.LoadCatalogAsync(cancellationToken);
            var splitAgent = EnsureAgentExists(splitCatalog, agentId);
            var splitProvider = await ResolveProviderForAgentAsync(splitAgent, splitCatalog, cancellationToken);
            var splitSession = chatSessionId.HasValue
                ? EnsureAgentOwnsSession(
                    await chatQueryStore.GetChatSessionAsync(chatSessionId.Value, cancellationToken),
                    agentId,
                    chatSessionId.Value)
                : null;

            return await SendMessageCoreAsync(
                splitAgent,
                splitProvider,
                splitCatalog,
                SandboxWorkspaceExecutionState.Empty,
                splitSession,
                prompt,
                attachmentPaths,
                options,
                cancellationToken);
        }

        var document = await store.LoadAsync(cancellationToken);
        var fallbackCatalog = document.ToCatalog();
        var fallbackExecutionState = document.ToExecutionState();
        var fallbackAgent = EnsureAgentExists(fallbackCatalog, agentId);
        var fallbackProvider = await ResolveProviderForAgentAsync(fallbackAgent, fallbackCatalog, cancellationToken);
        var fallbackSession = chatSessionId.HasValue
            ? EnsureAgentOwnsSession(fallbackExecutionState, agentId, chatSessionId.Value)
            : null;

        return await SendMessageCoreAsync(
            fallbackAgent,
            fallbackProvider,
            fallbackCatalog,
            fallbackExecutionState,
            fallbackSession,
            prompt,
            attachmentPaths,
            options,
            cancellationToken);
    }

    private async Task<AgentChatRunResult> SendMessageCoreAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        SandboxWorkspaceCatalog catalog,
        SandboxWorkspaceExecutionState executionState,
        ChatSessionRecord? session,
        string prompt,
        IReadOnlyList<string>? attachmentPaths,
        AgentChatRunOptions? options,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteRunCoreAsync(
            agent,
            provider,
            catalog,
            executionState,
            session,
            new ExecutionRunRequest(
                AgentId: agent.Id,
                Prompt: prompt.Trim(),
                ChatSessionId: session?.Id,
                Context: new ExecutionInvocationContext(
                    SourceKind: "chat-session",
                    SourceId: session?.Id.ToString("N") ?? string.Empty,
                    CorrelationId: string.Empty,
                    CausationId: string.Empty,
                    RequestedBy: "sandbox-chat",
                    RequestedByKind: "interactive",
                    MetadataJson: BuildChatMetadataJson(options)),
                AutoApprovePendingToolCalls: false,
                InputAttachmentPaths: attachmentPaths),
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

    private static string BuildChatMetadataJson(AgentChatRunOptions? options)
    {
        var metadataJson = "{}";
        if (options?.RuntimeToolProvidersEnabled == false)
        {
            metadataJson = ExecutionInvocationMetadata.ApplyRuntimeToolProvidersEnabled(metadataJson, enabled: false);
        }

        if (options?.WorkspaceToolsEnabled == false)
        {
            metadataJson = ExecutionInvocationMetadata.ApplyWorkspaceToolsEnabled(metadataJson, enabled: false);
        }

        return metadataJson;
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

    private async Task<(ChatSessionRecord Session, AgentRuntimeResponse Response, int TotalInputTokens, int TotalCachedInputTokens, int TotalOutputTokens, int TotalToolCalls)> ContinueAutoApprovedRunAsync(
        ExecutionRunRecord run,
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        Guid? chatSessionId,
        IReadOnlyList<CapabilityCatalogItem> attachedCapabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        AgentRuntimeResponse initialResponse,
        Func<ExecutionState, string, string, Task> progressCallback,
        AgentStructuredOutputContract? structuredOutput,
        AgentRuntimeHandoffExecutionOptions? handoffOptions,
        CancellationToken cancellationToken)
    {
        var currentSession = session;
        var currentResponse = initialResponse;
        var totalInputTokens = initialResponse.InputTokens;
        var totalCachedInputTokens = initialResponse.CachedInputTokens;
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
                suppressApprovalRequirements: true,
                structuredOutput: structuredOutput,
                executionOptions: CreateRuntimeExecutionOptions(run, structuredOutput, handoffOptions));

            totalInputTokens += currentResponse.InputTokens;
            totalCachedInputTokens += currentResponse.CachedInputTokens;
            totalOutputTokens += currentResponse.OutputTokens;
            totalToolCalls += currentResponse.ToolCalls;
        }

        return (currentSession, currentResponse, totalInputTokens, totalCachedInputTokens, totalOutputTokens, totalToolCalls);
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
            .Where(item => !AgentCapabilityRequirementEvaluator.IsRetiredCapability(item))
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

    private static IReadOnlyList<AgentMemoryRecord> ResolveAgentMemoryForRun(
        SandboxWorkspaceCatalog catalog,
        Guid agentId,
        ExecutionRunRecord run)
    {
        return IsGovernedMachineCriticalRun(run)
            ? []
            : ResolveAgentMemory(catalog, agentId);
    }
}
