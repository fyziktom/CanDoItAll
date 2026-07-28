using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafRuntimeSessionBuilder
{
    private const string LocalHistoryConversationId = "_agent_local_chat_history";

    public static async ValueTask<AgentSession> RestoreOrCreateSessionAsync(
        AIAgent runtimeAgent,
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        AgentRuntimeExecutionOptions runtimeOptions,
        CancellationToken cancellationToken,
        bool isApprovalContinuation = false)
    {
        if (!isApprovalContinuation && runtimeOptions.ContextIntent?.IsGovernedProcessStep == true)
        {
            return await runtimeAgent.CreateSessionAsync(cancellationToken);
        }

        if (ShouldRestoreSerializedSession(agent, provider, session, runtimeOptions, isApprovalContinuation))
        {
            using var document = JsonDocument.Parse(session.Compatibility!.SerializedSessionStateJson!);
            return await runtimeAgent.DeserializeSessionAsync(document.RootElement.Clone(), cancellationToken: cancellationToken);
        }

        if (isApprovalContinuation && (session.Compatibility?.PendingApprovals.Count ?? 0) > 0)
        {
            throw new InvalidOperationException(
                "Cannot continue pending tool approvals because serialized Microsoft Agent Framework session state is unavailable or incompatible with the current provider/history mode.");
        }

        return await runtimeAgent.CreateSessionAsync(cancellationToken);
    }

    public static IEnumerable<ChatMessage> CreatePromptInputMessages(
        AgentDefinition agent,
        ProviderProfile provider,
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

        if (ShouldRestoreSerializedSession(agent, provider, session, runtimeOptions))
        {
            return
            [
                CreateUserInputMessage(prompt, inputAttachments)
            ];
        }

        var transcriptMessages = session.Messages
            .OrderBy(item => item.CreatedAtUtc)
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
        chatOptions.AllowMultipleToolCalls = !hasApprovalTools;
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
        ChatSessionRecord session,
        AgentRuntimeExecutionOptions runtimeOptions)
    {
        if (runtimeOptions.ContextIntent?.IsGovernedProcessStep == true)
        {
            return "Creating an isolated Microsoft Agent Framework session for this governed process step.";
        }

        if (ShouldRestoreSerializedSession(agent, provider, session, runtimeOptions))
        {
            return "Restoring the serialized Microsoft Agent Framework session for this conversation.";
        }

        if (ShouldReplayTranscriptAfterApproval(session))
        {
            return "This conversation contains prior approval turns, so the sandbox transcript will be replayed into a fresh session for the next prompt.";
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

    private static bool ShouldRestoreSerializedSession(
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        AgentRuntimeExecutionOptions runtimeOptions,
        bool isApprovalContinuation = false)
    {
        var compatibility = session.Compatibility;
        if (compatibility is null || string.IsNullOrWhiteSpace(compatibility.SerializedSessionStateJson))
        {
            return false;
        }

        if (!isApprovalContinuation && ShouldReplayTranscriptAfterApproval(session))
        {
            return false;
        }

        var containsProviderConversationId = SerializedSessionContainsProviderConversationId(compatibility.SerializedSessionStateJson);

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

    private static bool ShouldReplayTranscriptAfterApproval(ChatSessionRecord session)
    {
        return false;
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
