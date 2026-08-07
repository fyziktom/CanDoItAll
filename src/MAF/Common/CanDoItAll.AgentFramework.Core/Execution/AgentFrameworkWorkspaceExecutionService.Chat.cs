using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

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

    public Task<AgentChatRunResult> SendMessageWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        AgentChatRunOptions options,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? attachmentPaths = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateActivityOperation(
            operation,
            agentId,
            chatSessionId,
            options.InitialActivityOperationId);
        return ExecuteWithinActivityBoundaryAsync(
            operation,
            () => SendMessageCoreEntryAsync(
                operation,
                agentId,
                chatSessionId,
                prompt,
                options,
                cancellationToken,
                attachmentPaths));
    }

    private async Task<AgentChatRunResult> SendMessageCoreEntryAsync(
        IAgentExecutionActivityOperationLease activityOperation,
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        AgentChatRunOptions options,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? attachmentPaths)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new InvalidOperationException("Prompt is required.");
        }

        ArgumentNullException.ThrowIfNull(options);
        EnsureRequiredActivityOperationId(
            options.InitialActivityOperationId,
            nameof(options));
        var defaultContext = new ExecutionInvocationContext(
            SourceKind: "chat-session",
            SourceId: chatSessionId?.ToString("N") ?? string.Empty,
            CorrelationId: string.Empty,
            CausationId: string.Empty,
            RequestedBy: "sandbox-chat",
            RequestedByKind: "interactive",
            MetadataJson: "{}");
        var requestedContext = options.Context ?? defaultContext;
        var invocationContext = requestedContext with
        {
            MetadataJson = BuildChatMetadataJson(options, requestedContext.MetadataJson)
        };
        var request = new ExecutionRunRequest(
            AgentId: agentId,
            Prompt: prompt.Trim(),
            InitialActivityOperationId: options.InitialActivityOperationId,
            ChatSessionId: chatSessionId,
            Context: invocationContext,
            AutoApprovePendingToolCalls: false,
            InputAttachmentPaths: attachmentPaths)
        {
            TransientContext = options.TransientContext
        };
        var result = await ExecuteRunEntryAsync(
            activityOperation,
            request,
            cancellationToken,
            persistTranscript: true);

        return new AgentChatRunResult(
            ChatSessionId: result.ChatSessionId ?? throw new InvalidOperationException("Chat-backed execution runs must return a chat session id."),
            AssistantMessage: result.AssistantMessage ?? throw new InvalidOperationException("Chat-backed execution runs must produce an assistant message."),
            Metric: result.Metric)
        {
            ExecutionRunId = result.ExecutionRunId,
            State = result.State,
            ContextCompletionNotification = result.ContextCompletionNotification
        };
    }

    private static string BuildChatMetadataJson(AgentChatRunOptions? options, string? baseMetadataJson)
    {
        var metadataJson = string.IsNullOrWhiteSpace(baseMetadataJson) ? "{}" : baseMetadataJson;
        if (options?.RuntimeToolProvidersEnabled == false)
        {
            metadataJson = ExecutionInvocationMetadata.ApplyRuntimeToolProvidersEnabled(metadataJson, enabled: false);
        }

        if (options?.WorkspaceToolsEnabled == false)
        {
            metadataJson = ExecutionInvocationMetadata.ApplyWorkspaceToolsEnabled(metadataJson, enabled: false);
        }

        if (options?.ToolCapabilitiesEnabled == false)
        {
            metadataJson = ExecutionInvocationMetadata.ApplyToolCapabilitiesEnabled(metadataJson, enabled: false);
        }

        return metadataJson;
    }

    public Task<AgentChatRunResult> RespondToPendingApprovalsWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        Guid agentId,
        Guid chatSessionId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        ValidateActivityOperation(
            operation,
            agentId,
            chatSessionId,
            operation.StreamId.OperationId);
        return ExecuteWithinActivityBoundaryAsync(
            operation,
            () => RespondToPendingApprovalsCoreAsync(
                operation,
                agentId,
                chatSessionId,
                operation.StreamId.OperationId,
                decisions,
                autoApprovePendingToolCalls,
                cancellationToken));
    }

    private async Task<AgentChatRunResult> RespondToPendingApprovalsCoreAsync(
        IAgentExecutionActivityOperationLease activityOperation,
        Guid agentId,
        Guid chatSessionId,
        AgentExecutionOperationId activityOperationId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls,
        CancellationToken cancellationToken)
    {
        var executionRunId = await ResolveOrCreatePendingExecutionRunIdAsync(agentId, chatSessionId, cancellationToken);
        var result = await ContinueExecutionRunEntryAsync(
            activityOperation,
            executionRunId,
            activityOperationId,
            decisions,
            autoApprovePendingToolCalls,
            cancellationToken);

        return ToAgentChatRunResult(result, chatSessionId);
    }

    private static AgentChatRunResult ToAgentChatRunResult(ExecutionRunResult result, Guid chatSessionId)
    {
        return new AgentChatRunResult(
            ChatSessionId: result.ChatSessionId ?? chatSessionId,
            AssistantMessage: result.AssistantMessage ?? throw new InvalidOperationException("Chat-backed approval continuations must produce an assistant message."),
            Metric: result.Metric)
        {
            ExecutionRunId = result.ExecutionRunId,
            State = result.State,
            ContextCompletionNotification = result.ContextCompletionNotification
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
        AgentExecutionOperationId activityOperationId,
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
        Action<AgentRuntimeResponse> responseObserved,
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

            await progressCallback(
                ExecutionState.Preparing,
                "Auto approval continuation",
                "Preparing the automatically approved tool continuation.");
            var continuationResponse = await continuationRuntime.ContinueAsync(
                new AgentRuntimeContinuationRequest(
                    agent,
                    provider,
                    currentSession,
                    attachedCapabilities,
                    memory,
                    Decisions: currentResponse.PendingApprovals
                        .Select(item => new AgentRuntimeApprovalDecision(item.ApprovalId, Approved: true))
                        .ToArray(),
                    string.IsNullOrWhiteSpace(ChatSessionRuntimeCompatibilityAdapter.RuntimeSessionKeyOrEmpty(currentSession))
                        ? null
                        : ChatSessionRuntimeCompatibilityAdapter.RuntimeSessionKeyOrEmpty(currentSession),
                    progressCallback,
                    SuppressApprovalRequirements: true,
                    StructuredOutput: structuredOutput,
                    ExecutionOptions: CreateRuntimeExecutionOptionsCore(
                        run,
                        activityOperationId,
                        structuredOutput,
                        handoffOptions,
                        inputAttachments: null,
                        jsonSchemaOutput: null)),
                cancellationToken);

            currentResponse = continuationResponse with
            {
                ToolInvocationTraces = AggregateToolInvocationTraces(
                    currentResponse.ToolInvocationTraces,
                    continuationResponse.ToolInvocationTraces)
            };
            responseObserved(currentResponse);

            totalInputTokens += currentResponse.InputTokens;
            totalCachedInputTokens += currentResponse.CachedInputTokens;
            totalOutputTokens += currentResponse.OutputTokens;
            totalToolCalls += currentResponse.ToolCalls;
        }

        return (currentSession, currentResponse, totalInputTokens, totalCachedInputTokens, totalOutputTokens, totalToolCalls);
    }

    internal static IReadOnlyList<AgentToolInvocationTrace> AggregateToolInvocationTraces(
        IReadOnlyList<AgentToolInvocationTrace> accumulatedTraces,
        IReadOnlyList<AgentToolInvocationTrace> continuationTraces)
    {
        ArgumentNullException.ThrowIfNull(accumulatedTraces);
        ArgumentNullException.ThrowIfNull(continuationTraces);

        if (accumulatedTraces.Count == 0 && continuationTraces.Count == 0)
        {
            return [];
        }

        var aggregatedTraces = new List<AgentToolInvocationTrace>(
            accumulatedTraces.Count + continuationTraces.Count);
        AppendRebased(accumulatedTraces);
        AppendRebased(continuationTraces);
        return aggregatedTraces;

        void AppendRebased(IReadOnlyList<AgentToolInvocationTrace> traces)
        {
            foreach (var trace in traces
                         .OrderBy(item => item.Sequence)
                         .ThenBy(item => item.StartedAtUtc))
            {
                aggregatedTraces.Add(trace with
                {
                    Sequence = aggregatedTraces.Count + 1
                });
            }
        }
    }

    private static bool ShouldAutoApprovePendingToolCalls(AgentDefinition agent, ChatSessionRecord session)
        => agent.Permissions.AutoApproveExternalCallsByDefault || ChatSessionRuntimeCompatibilityAdapter.AutoApprovePendingToolCalls(session);

    private void ValidateActivityOperation(
        IAgentExecutionActivityOperationLease operation,
        Guid agentId,
        Guid? chatSessionId,
        AgentExecutionOperationId operationId)
    {
        ValidateActivityOperationScope(operation);
        if (agentId == Guid.Empty ||
            operation.AgentId != agentId)
        {
            throw new InvalidOperationException(
                "The activity operation belongs to a different agent.");
        }

        if (operationId.Value == Guid.Empty ||
            operation.StreamId.OperationId != operationId)
        {
            throw new InvalidOperationException(
                "The activity operation id does not match the chat request.");
        }

        if (chatSessionId.HasValue &&
            operation.ChatSessionId != chatSessionId)
        {
            throw new InvalidOperationException(
                "The activity operation belongs to a different chat session.");
        }
    }

    private void ValidateActivityOperationScope(
        IAgentExecutionActivityOperationLease operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.StreamId.DatabaseProfileId !=
            activityWorkspaceIdentity.DatabaseProfileId)
        {
            throw new InvalidOperationException(
                "The activity operation belongs to a different database profile.");
        }

        if (operation.StreamId.WorkspaceScope !=
            activityWorkspaceIdentity.WorkspaceScope)
        {
            throw new InvalidOperationException(
                "The activity operation belongs to a different workspace scope.");
        }
    }

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

    private IReadOnlyList<AgentMemoryRecord> ResolveAgentMemoryForRun(
        SandboxWorkspaceCatalog catalog,
        Guid agentId,
        ExecutionRunRecord run)
    {
        return IsGovernedMachineCriticalRun(run)
            ? []
            : ResolveAgentMemory(catalog, agentId);
    }
}
