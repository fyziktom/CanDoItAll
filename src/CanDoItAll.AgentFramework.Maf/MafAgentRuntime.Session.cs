using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    private static async ValueTask<AgentSession> RestoreOrCreateSessionAsync(
        AIAgent runtimeAgent,
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        CancellationToken cancellationToken,
        bool isApprovalContinuation = false)
    {
        if (ShouldRestoreSerializedSession(agent, provider, session, isApprovalContinuation))
        {
            using var document = JsonDocument.Parse(session.Compatibility!.SerializedSessionStateJson!);
            return await runtimeAgent.DeserializeSessionAsync(document.RootElement.Clone(), cancellationToken: cancellationToken);
        }

        return await runtimeAgent.CreateSessionAsync(cancellationToken);
    }

    private static IEnumerable<ChatMessage> CreatePromptInputMessages(
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        string prompt)
    {
        if (ShouldRestoreSerializedSession(agent, provider, session))
        {
            return
            [
                new ChatMessage(ChatRole.User, prompt.Trim())
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
                new ChatMessage(ChatRole.User, prompt.Trim())
            ];
        }

        return transcriptMessages;
    }

    private static IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        IEnumerable<ChatMessage> inputMessages,
        ChatClientAgentRunOptions runOptions,
        CancellationToken cancellationToken)
    {
        var materializedMessages = inputMessages as IReadOnlyCollection<ChatMessage> ?? inputMessages.ToList();
        return materializedMessages.Count switch
        {
            0 => runtimeAgent.RunStreamingAsync(runtimeSession, runOptions, cancellationToken),
            1 => runtimeAgent.RunStreamingAsync(materializedMessages.First(), runtimeSession, runOptions, cancellationToken),
            _ => runtimeAgent.RunStreamingAsync(materializedMessages, runtimeSession, runOptions, cancellationToken)
        };
    }

    private static ChatClientAgentRunOptions CreateRunOptions(
        AgentDefinition agent,
        ProviderProfile provider,
        bool hasApprovalTools,
        ResponseContinuationToken? continuationToken)
    {
        return new ChatClientAgentRunOptions(new ChatOptions
        {
            Temperature = (float)agent.Temperature,
            AllowMultipleToolCalls = !hasApprovalTools
        })
        {
            AllowBackgroundResponses = agent.EnableBackgroundResponses && SupportsBackgroundResponses(provider),
            ContinuationToken = continuationToken
        };
    }

    private static bool ShouldRestoreSerializedSession(
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
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

    private static string ResolveSessionMessage(
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session)
    {
        if (ShouldRestoreSerializedSession(agent, provider, session))
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

    private static ChatRole MapRole(ChatMessageRole role)
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

    private static bool SupportsBackgroundResponses(ProviderProfile provider)
    {
        return provider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi
            && provider.Transport == ProviderTransportKind.Responses
            && provider.SupportsBackgroundResponses;
    }
}
