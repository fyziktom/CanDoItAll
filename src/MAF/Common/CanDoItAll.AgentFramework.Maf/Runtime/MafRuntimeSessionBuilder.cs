using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafRuntimeSessionBuilder
{
    private const string LocalHistoryConversationId = "_agent_local_chat_history";
    private const string IncompatibleApprovalContinuationMessage =
        "Cannot continue pending tool approvals because serialized Microsoft Agent Framework session state is unavailable or incompatible with the current provider/history mode.";

    private static readonly IAgentRuntimeStateAdapter StateAdapter = new MafRuntimeStateAdapter();
    private static readonly IRuntimeStateCompatibilityPolicy CompatibilityPolicy = new MafRuntimeStateCompatibilityPolicy();

    public static async ValueTask<AgentSession> RestoreOrCreateSessionAsync(
        AIAgent runtimeAgent,
        AgentDefinition agent,
        ProviderProfile provider,
        string model,
        ChatSessionRecord session,
        AgentRuntimeExecutionOptions runtimeOptions,
        CancellationToken cancellationToken,
        bool isApprovalContinuation = false,
        Func<ExecutionState, string, string, Task>? progressCallback = null)
    {
        if (!isApprovalContinuation && runtimeOptions.ContextIntent?.IsGovernedProcessStep == true)
        {
            return await runtimeAgent.CreateSessionAsync(cancellationToken);
        }

        var pendingApprovalCount = session.Compatibility?.PendingApprovals.Count ?? 0;
        var evaluation = ResolveRestoreEvaluation(agent, provider, model, session, runtimeOptions, isApprovalContinuation);
        if (evaluation.Decision is { } evaluatedDecision && progressCallback is not null)
        {
            await progressCallback(
                ExecutionState.Preparing,
                "Session compatibility",
                DescribeCompatibilityDecision(evaluatedDecision));
        }

        if (evaluation.ShouldRestore)
        {
            return await DeserializeSerializedSessionAsync(runtimeAgent, evaluation.RestorePayloadJson!, cancellationToken);
        }

        if (evaluation.FailClosedReason is { } failClosedReason)
        {
            return await FailClosedOrCreateSessionAsync(
                runtimeAgent,
                isApprovalContinuation,
                pendingApprovalCount,
                failClosedReason,
                cancellationToken);
        }

        if (isApprovalContinuation && pendingApprovalCount > 0)
        {
            throw new InvalidOperationException(IncompatibleApprovalContinuationMessage);
        }

        return await runtimeAgent.CreateSessionAsync(cancellationToken);
    }

    /// <summary>
    /// One evaluation shared by session creation, prompt-message composition,
    /// and progress messaging: parse the persisted state, decide envelope
    /// compatibility, unwrap the adapter payload, and apply the restore
    /// eligibility rules to the INNER payload. The provider-conversation-id
    /// probe always inspects the adapter payload, never the envelope wrapper,
    /// so a wrapped provider-managed conversation cannot slip past the
    /// transient-context and history-mode gates.
    /// </summary>
    internal sealed record RestoreEvaluation(
        bool ShouldRestore,
        RuntimeStateCompatibilityDecision? Decision,
        string? RestorePayloadJson,
        string? FailClosedReason);

    internal static RestoreEvaluation ResolveRestoreEvaluation(
        AgentDefinition agent,
        ProviderProfile provider,
        string model,
        ChatSessionRecord session,
        AgentRuntimeExecutionOptions runtimeOptions,
        bool isApprovalContinuation = false)
    {
        var compatibility = session.Compatibility;
        if (compatibility is null || string.IsNullOrWhiteSpace(compatibility.SerializedSessionStateJson))
        {
            return new RestoreEvaluation(false, null, null, null);
        }

        var rawStateJson = compatibility.SerializedSessionStateJson!;
        var decision = EvaluateStoredRuntimeState(rawStateJson, provider, model, runtimeOptions, out var envelope);
        string? payloadJson;
        string? failClosedReason = null;
        switch (decision.Outcome)
        {
            case RuntimeStateCompatibilityOutcome.CompatibleRestore:
            {
                var restoreResult = StateAdapter.TryRestore(envelope!);
                if (restoreResult.Succeeded)
                {
                    payloadJson = restoreResult.PayloadJson;
                }
                else
                {
                    // Defensive: the policy judged this envelope compatible but
                    // the adapter itself could not unwrap it. Fail exactly like
                    // an Incompatible decision — never trust an envelope the
                    // owning adapter refuses.
                    payloadJson = null;
                    failClosedReason = restoreResult.FailureReason;
                }

                break;
            }

            case RuntimeStateCompatibilityOutcome.RegisteredMigration:
                // Legacy unversioned state: the raw stored JSON IS the adapter
                // payload; it was captured before envelopes existed.
                payloadJson = rawStateJson;
                break;

            case RuntimeStateCompatibilityOutcome.SafeCanonicalReplay:
                payloadJson = null;
                break;

            default:
                payloadJson = null;
                failClosedReason = decision.Reason;
                break;
        }

        if (payloadJson is null)
        {
            return new RestoreEvaluation(false, decision, null, failClosedReason);
        }

        var containsProviderConversationId = SerializedSessionContainsProviderConversationId(payloadJson);
        var shouldRestore = ResolveRestoreEligibility(
            agent,
            provider,
            runtimeOptions,
            isApprovalContinuation,
            containsProviderConversationId);
        return shouldRestore
            ? new RestoreEvaluation(true, decision, payloadJson, null)
            : new RestoreEvaluation(false, decision, null, failClosedReason);
    }

    /// <summary>
    /// The restore eligibility rules, applied to the inner adapter payload's
    /// provider-conversation flag: a transient-context ordinary send never
    /// reattaches to a provider-managed conversation; framework-managed
    /// history restores local-history state and service-managed conversations
    /// only where the provider supports them.
    /// </summary>
    private static bool ResolveRestoreEligibility(
        AgentDefinition agent,
        ProviderProfile provider,
        AgentRuntimeExecutionOptions runtimeOptions,
        bool isApprovalContinuation,
        bool containsProviderConversationId)
    {
        if (runtimeOptions.TransientContext is not null && !isApprovalContinuation)
        {
            return !containsProviderConversationId;
        }

        if (ShouldUseFrameworkManagedHistory(agent, provider))
        {
            return !containsProviderConversationId || SupportsServiceManagedConversations(provider);
        }

        if (SupportsServiceManagedConversations(provider))
        {
            return true;
        }

        return !containsProviderConversationId;
    }

    private static async ValueTask<AgentSession> DeserializeSerializedSessionAsync(
        AIAgent runtimeAgent,
        string serializedSessionStateJson,
        CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(serializedSessionStateJson);
        return await runtimeAgent.DeserializeSessionAsync(document.RootElement.Clone(), cancellationToken: cancellationToken);
    }

    private static async ValueTask<AgentSession> FailClosedOrCreateSessionAsync(
        AIAgent runtimeAgent,
        bool isApprovalContinuation,
        int pendingApprovalCount,
        string reason,
        CancellationToken cancellationToken)
    {
        if (isApprovalContinuation && pendingApprovalCount > 0)
        {
            throw new InvalidOperationException($"{IncompatibleApprovalContinuationMessage} Reason: {reason}");
        }

        return await runtimeAgent.CreateSessionAsync(cancellationToken);
    }

    /// <summary>
    /// Parses the persisted text as a runtime-state envelope (or recognizes it as legacy
    /// unversioned state) and asks <see cref="MafRuntimeStateCompatibilityPolicy"/> for an
    /// explicit restore/migrate/replay/fail decision. Never guesses: unparseable text is
    /// Incompatible, not a silent replay.
    /// </summary>
    internal static RuntimeStateCompatibilityDecision EvaluateStoredRuntimeState(
        string rawStateJson,
        ProviderProfile provider,
        string model,
        AgentRuntimeExecutionOptions runtimeOptions,
        out RuntimeStateEnvelope? envelope)
    {
        var isEnvelope = RuntimeStateEnvelope.TryParse(rawStateJson, out envelope);
        var hasStoredText = !string.IsNullOrWhiteSpace(rawStateJson);
        var isWellFormedJson = isEnvelope || (hasStoredText && IsWellFormedJson(rawStateJson));
        var request = new RuntimeStateCompatibilityRequest(
            Envelope: envelope,
            IsLegacyUnversionedState: !isEnvelope && isWellFormedJson,
            HasUnparseableStoredState: !isEnvelope && !isWellFormedJson && hasStoredText,
            CurrentProviderProfileId: provider.Id,
            CurrentProviderTransport: provider.Transport,
            CurrentModel: model,
            CurrentToolsetFingerprint: runtimeOptions.ToolsetFingerprint,
            CurrentContextPolicyFingerprint: runtimeOptions.ModelContextDigest,
            CurrentHistoryMode: runtimeOptions.HistoryMode)
        {
            CurrentAuthorityPolicyFingerprint = runtimeOptions.AuthorityPolicyFingerprint,
            CurrentCapabilityPolicyFingerprint = runtimeOptions.CapabilityPolicyFingerprint,
            CurrentLegacyToolsetNameFingerprint = runtimeOptions.LegacyToolsetNameFingerprint,
            CurrentAdapterPackageVersion = MafRuntimeStateAdapter.AdapterPackageVersion
        };
        return CompatibilityPolicy.Evaluate(request);
    }

    private static bool IsWellFormedJson(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string DescribeCompatibilityDecision(RuntimeStateCompatibilityDecision decision)
        => string.IsNullOrWhiteSpace(decision.MigrationId)
            ? $"Runtime-state compatibility decision: {decision.Outcome}. {decision.Reason}"
            : $"Runtime-state compatibility decision: {decision.Outcome} (migration '{decision.MigrationId}'). {decision.Reason}";

    public static IEnumerable<ChatMessage> CreatePromptInputMessages(
        AgentDefinition agent,
        ProviderProfile provider,
        string model,
        ChatSessionRecord session,
        string prompt,
        AgentRuntimeExecutionOptions runtimeOptions)
    {
        var inputAttachments = runtimeOptions.InputAttachments ?? [];
        if (runtimeOptions.ContextIntent?.IsGovernedProcessStep == true)
        {
            return
            [
                CreateUserInputMessage(prompt, inputAttachments)
            ];
        }

        var canonicalToolEvidence = SelectCanonicalToolEvidence(
            session,
            agent,
            runtimeOptions);
        if (ResolveRestoreEvaluation(agent, provider, model, session, runtimeOptions).ShouldRestore)
        {
            return canonicalToolEvidence
                .Select(message => new ChatMessage(ChatRole.System, message.Content))
                .Append(CreateUserInputMessage(prompt, inputAttachments))
                .ToArray();
        }

        var selectedEvidenceIds = canonicalToolEvidence
            .Select(message => message.Id)
            .ToHashSet();
        var transcriptMessages = session.Messages
            .OrderBy(item => item.CreatedAtUtc)
            .Where(message =>
                !IsCanonicalToolEvidence(message) ||
                selectedEvidenceIds.Contains(message.Id))
            .Select(message => new ChatMessage(MapRole(message.Role), message.Content))
            .ToList();
        if (transcriptMessages.Count == 0)
        {
            return
            [
                CreateUserInputMessage(prompt, inputAttachments)
            ];
        }

        var lastUserMessageIndex = transcriptMessages.FindLastIndex(message => message.Role == ChatRole.User);
        if (lastUserMessageIndex >= 0)
        {
            transcriptMessages[lastUserMessageIndex] = CreateUserInputMessage(prompt, inputAttachments);
        }
        else
        {
            transcriptMessages.Add(CreateUserInputMessage(prompt, inputAttachments));
        }

        return transcriptMessages;
    }

    private static IReadOnlyList<ChatMessageRecord> SelectCanonicalToolEvidence(
        ChatSessionRecord session,
        AgentDefinition agent,
        AgentRuntimeExecutionOptions runtimeOptions)
    {
        const int maximumMessages = 4;
        const int maximumCharacters = 4_096;
        var governance = runtimeOptions.Governance;
        var contextIntent = runtimeOptions.ContextIntent;
        if (session.AgentId != agent.Id ||
            governance is null ||
            contextIntent is null ||
            governance.AgentId != agent.Id ||
            !governance.ReadAllowed ||
            !governance.MutationAllowed)
        {
            return [];
        }

        var selected = new List<ChatMessageRecord>(maximumMessages);
        var characters = 0;
        foreach (var message in session.Messages
                     .Where(message => IsCanonicalToolEvidence(
                         message,
                         session,
                         agent,
                         governance,
                         contextIntent))
                     .OrderByDescending(message => message.CreatedAtUtc)
                     .ThenByDescending(message => message.Id))
        {
            if (selected.Count >= maximumMessages ||
                characters + message.Content.Length > maximumCharacters)
            {
                break;
            }

            selected.Add(message);
            characters += message.Content.Length;
        }

        selected.Reverse();
        return selected;
    }

    private static bool IsCanonicalToolEvidence(
        ChatMessageRecord message,
        ChatSessionRecord session,
        AgentDefinition agent,
        AgentExecutionGovernanceSnapshot governance,
        AgentRuntimeContextIntent contextIntent)
    {
        var ownership = message.ToolEvidenceOwnership;
        return message.Role == ChatMessageRole.System &&
               message.Content.StartsWith(
                   AgentToolEvidenceMessage.Prefix,
                   StringComparison.Ordinal) &&
               ownership is not null &&
               ownership.ChatSessionId == session.Id &&
               ownership.AgentId == agent.Id &&
               ownership.DatabaseProfileId == governance.DatabaseProfileId &&
               ownership.DatabaseProfileGeneration == governance.DatabaseProfileGeneration.Value &&
               ownership.WorkspaceScope == governance.WorkspaceScope &&
               string.Equals(
                   ownership.SourceKind,
                   contextIntent.SourceKind,
                   StringComparison.Ordinal) &&
               string.Equals(
                   ownership.SourceId,
                   contextIntent.SourceId,
                   StringComparison.Ordinal);
    }

    private static bool IsCanonicalToolEvidence(ChatMessageRecord message)
        => message.Role == ChatMessageRole.System &&
           message.Content.StartsWith(
               AgentToolEvidenceMessage.Prefix,
               StringComparison.Ordinal);
    public static ChatMessage CreateUserInputMessage(
        string prompt,
        IReadOnlyList<AgentRuntimeInputAttachment> attachments)
    {
        var normalizedPrompt = prompt.Trim();
        if (attachments.Count == 0)
        {
            return new ChatMessage(ChatRole.User, normalizedPrompt);
        }

        var contents = new List<AIContent>();
        if (!string.IsNullOrWhiteSpace(normalizedPrompt))
        {
            contents.Add(new TextContent(normalizedPrompt));
        }

        contents.AddRange(attachments.Select(attachment => new DataContent(attachment.Bytes.AsMemory(), attachment.ContentType)
        {
            Name = string.IsNullOrWhiteSpace(attachment.Name) ? Path.GetFileName(attachment.SourcePath) : attachment.Name
        }));
        return new ChatMessage(ChatRole.User, contents);
    }

    public static ChatClientAgentRunOptions CreateRunOptions(
        AgentDefinition agent,
        ProviderProfile provider,
        string model,
        bool hasApprovalTools,
        ResponseContinuationToken? continuationToken,
        bool forceOmitTemperature,
        AgentRuntimeExecutionOptions runtimeOptions)
    {
        var chatOptions = MafModelParametersBuilder.CreateModelCompatibleChatOptions(
            provider,
            model,
            (float)agent.Temperature,
            forceOmitTemperature,
            agent.ConfigurationJson);
        chatOptions = ProviderHistoryChatContext.WithContext(chatOptions, runtimeOptions.History);
        chatOptions.AllowMultipleToolCalls = hasApprovalTools ? false : null;
        ApplyResponseFormat(chatOptions, runtimeOptions);

        return new ChatClientAgentRunOptions(chatOptions)
        {
            AllowBackgroundResponses = agent.EnableBackgroundResponses && SupportsBackgroundResponses(provider),
            ContinuationToken = continuationToken
        };
    }

    public static void ApplyStructuredResponseFormat(
        ChatOptions chatOptions,
        AgentStructuredOutputContract? structuredOutput)
    {
        ArgumentNullException.ThrowIfNull(chatOptions);
        if (structuredOutput is null)
        {
            return;
        }

        chatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema(
            structuredOutput.OutputType,
            AgentOutputJson.SerializerOptions,
            string.IsNullOrWhiteSpace(structuredOutput.SchemaName) ? null : structuredOutput.SchemaName,
            string.IsNullOrWhiteSpace(structuredOutput.SchemaDescription) ? null : structuredOutput.SchemaDescription);
    }

    public static void ApplyResponseFormat(
        ChatOptions chatOptions,
        AgentRuntimeExecutionOptions runtimeOptions)
    {
        ArgumentNullException.ThrowIfNull(chatOptions);
        ArgumentNullException.ThrowIfNull(runtimeOptions);

        if (ShouldApplyStructuredOutputResponseFormat(runtimeOptions))
        {
            ApplyStructuredResponseFormat(chatOptions, runtimeOptions.StructuredOutput);
            return;
        }

        if (runtimeOptions.StructuredOutput is not null)
        {
            return;
        }

        if (!runtimeOptions.RequireJsonResponseFormat)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(runtimeOptions.ResponseFormatJsonSchema))
        {
            chatOptions.ResponseFormat = ChatResponseFormat.Json;
            return;
        }

        using var document = JsonDocument.Parse(runtimeOptions.ResponseFormatJsonSchema);
        chatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema(
            document.RootElement.Clone(),
            string.IsNullOrWhiteSpace(runtimeOptions.ResponseFormatSchemaName) ? null : runtimeOptions.ResponseFormatSchemaName,
            string.IsNullOrWhiteSpace(runtimeOptions.ResponseFormatSchemaDescription) ? null : runtimeOptions.ResponseFormatSchemaDescription);
    }

    public static bool ShouldApplyStructuredOutputResponseFormat(AgentRuntimeExecutionOptions runtimeOptions)
    {
        ArgumentNullException.ThrowIfNull(runtimeOptions);

        return runtimeOptions.StructuredOutput is not null &&
               runtimeOptions.FinalizerMode != AgentFinalizerMode.Required;
    }

    public static string ResolveSessionMessage(
        AgentDefinition agent,
        ProviderProfile provider,
        string model,
        ChatSessionRecord session,
        AgentRuntimeExecutionOptions runtimeOptions)
    {
        if (runtimeOptions.ContextIntent?.IsGovernedProcessStep == true)
        {
            return "Creating an isolated Microsoft Agent Framework session for this governed process step.";
        }

        if (ResolveRestoreEvaluation(agent, provider, model, session, runtimeOptions).ShouldRestore)
        {
            return "Restoring the serialized Microsoft Agent Framework session for this conversation.";
        }

        if (!string.IsNullOrWhiteSpace(session.Compatibility?.SerializedSessionStateJson))
        {
            return "Serialized Microsoft Agent Framework session state is incompatible with the current history mode, so the sandbox transcript will be replayed into a fresh session.";
        }

        if (!string.IsNullOrWhiteSpace(session.Compatibility?.RuntimeSessionKey))
        {
            return "Serialized Microsoft Agent Framework session state is unavailable, so the sandbox transcript will be replayed into a fresh session.";
        }

        return "Creating a new Microsoft Agent Framework session and hydrating it from the sandbox transcript.";
    }

    public static bool ShouldUseFrameworkManagedHistory(
        AgentDefinition agent,
        ProviderProfile provider,
        AgentRuntimeExecutionOptions runtimeOptions)
    {
        ArgumentNullException.ThrowIfNull(runtimeOptions);

        return runtimeOptions.ContextIntent?.IsGovernedProcessStep == true ||
               runtimeOptions.TransientContext is not null ||
               ShouldUseFrameworkManagedHistory(agent, provider);
    }

    public static bool SupportsBackgroundResponses(ProviderProfile provider)
    {
        return provider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi
            && provider.Transport == ProviderTransportKind.Responses
            && provider.SupportsBackgroundResponses;
    }

    private static bool SerializedSessionContainsProviderConversationId(string serializedSessionStateJson)
    {
        try
        {
            using var document = JsonDocument.Parse(serializedSessionStateJson);
            return document.RootElement.TryGetProperty("conversationId", out var conversationIdProperty)
                && !string.IsNullOrWhiteSpace(conversationIdProperty.GetString())
                && !string.Equals(conversationIdProperty.GetString(), LocalHistoryConversationId, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return true;
        }
    }

    public static ChatRole MapRole(ChatMessageRole role)
    {
        return role switch
        {
            ChatMessageRole.Assistant => ChatRole.Assistant,
            ChatMessageRole.System => ChatRole.System,
            _ => ChatRole.User
        };
    }

    private static bool ShouldUseFrameworkManagedHistory(AgentDefinition agent, ProviderProfile provider)
    {
        return agent.ChatHistoryMode switch
        {
            AgentChatHistoryMode.FrameworkManaged => true,
            AgentChatHistoryMode.ProviderManaged => false,
            _ => provider.PreferFrameworkManagedChatHistory || !SupportsServiceManagedConversations(provider)
        };
    }

    private static bool SupportsServiceManagedConversations(ProviderProfile provider)
    {
        return provider.Kind switch
        {
            ProviderKind.OpenAi or ProviderKind.AzureOpenAi => provider.Transport == ProviderTransportKind.Responses && !provider.PreferFrameworkManagedChatHistory,
            _ => false
        };
    }
}
