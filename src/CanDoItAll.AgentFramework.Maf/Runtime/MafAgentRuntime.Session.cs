using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
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

        if (isApprovalContinuation && (session.Compatibility?.PendingApprovals.Count ?? 0) > 0)
        {
            throw new InvalidOperationException(
                "Cannot continue pending tool approvals because serialized Microsoft Agent Framework session state is unavailable or incompatible with the current provider/history mode.");
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

    private static AgentResponseUpdate SnapshotUpdate(
        AgentResponseUpdate update)
    {
        return new AgentResponseUpdate(update.Role, update.Contents.Select(SnapshotContent).ToList())
        {
            AdditionalProperties = SnapshotAdditionalProperties(update.AdditionalProperties),
            AuthorName = update.AuthorName,
            ContinuationToken = update.ContinuationToken,
            CreatedAt = update.CreatedAt,
            FinishReason = update.FinishReason,
            MessageId = update.MessageId,
            RawRepresentation = null,
            ResponseId = update.ResponseId
        };
    }

    private static AIContent SnapshotContent(AIContent content)
    {
        return content switch
        {
            ToolApprovalRequestContent approval => new ToolApprovalRequestContent(
                approval.RequestId,
                SnapshotToolCall(approval.ToolCall)),
            FunctionCallContent functionCall => new FunctionCallContent(
                functionCall.CallId,
                functionCall.Name,
                SnapshotArguments(functionCall.Arguments)),
            McpServerToolCallContent mcpToolCall => new McpServerToolCallContent(
                mcpToolCall.CallId,
                mcpToolCall.Name,
                mcpToolCall.ServerName)
            {
                Arguments = SnapshotArguments(mcpToolCall.Arguments)
            },
            ToolCallContent toolCall => SnapshotToolCall(toolCall),
            TextContent textContent => new TextContent(textContent.Text),
            _ => content
        };
    }

    private static ToolCallContent SnapshotToolCall(ToolCallContent toolCall)
    {
        return toolCall switch
        {
            FunctionCallContent functionCall => new FunctionCallContent(
                functionCall.CallId,
                functionCall.Name,
                SnapshotArguments(functionCall.Arguments)),
            McpServerToolCallContent mcpToolCall => new McpServerToolCallContent(
                mcpToolCall.CallId,
                mcpToolCall.Name,
                mcpToolCall.ServerName)
            {
                Arguments = SnapshotArguments(mcpToolCall.Arguments)
            },
            _ => new FunctionCallContent(
                toolCall.CallId ?? Guid.NewGuid().ToString("N"),
                ResolveOpaqueToolCallName(toolCall),
                SnapshotNamedValues(ResolveOpaqueToolCallArguments(toolCall)))
        };
    }

    private static IDictionary<string, object?>? SnapshotArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null)
        {
            return null;
        }

        return arguments.ToDictionary(
            pair => pair.Key,
            pair => SnapshotArgumentValue(pair.Value),
            StringComparer.Ordinal);
    }

    private static object? SnapshotArgumentValue(object? value)
    {
        return value switch
        {
            null => null,
            JsonElement jsonElement => jsonElement.Clone(),
            IDictionary<string, object?> dictionary => SnapshotArguments(dictionary),
            IReadOnlyDictionary<string, object?> readOnlyDictionary => readOnlyDictionary.ToDictionary(
                pair => pair.Key,
                pair => SnapshotArgumentValue(pair.Value),
                StringComparer.Ordinal),
            IEnumerable<object?> values when value is not string => values
                .Select(SnapshotArgumentValue)
                .ToList(),
            _ => value
        };
    }

    private static IDictionary<string, object?>? SnapshotNamedValues(object? value)
    {
        return value switch
        {
            null => null,
            IDictionary<string, object?> dictionary => SnapshotArguments(dictionary),
            IReadOnlyDictionary<string, object?> readOnlyDictionary => readOnlyDictionary.ToDictionary(
                pair => pair.Key,
                pair => SnapshotArgumentValue(pair.Value),
                StringComparer.Ordinal),
            IEnumerable<KeyValuePair<string, object?>> pairs => pairs.ToDictionary(
                pair => pair.Key,
                pair => SnapshotArgumentValue(pair.Value),
                StringComparer.Ordinal),
            JsonElement { ValueKind: JsonValueKind.Object } jsonObject => jsonObject
                .EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => SnapshotArgumentValue(property.Value.Clone()),
                    StringComparer.Ordinal),
            _ => null
        };
    }

    private static AdditionalPropertiesDictionary? SnapshotAdditionalProperties(AdditionalPropertiesDictionary? properties)
    {
        var snapshot = SnapshotNamedValues(properties);
        if (snapshot is null)
        {
            return null;
        }

        var clone = new AdditionalPropertiesDictionary();
        foreach (var pair in snapshot)
        {
            clone[pair.Key] = pair.Value;
        }

        return clone;
    }

    private static string ResolveOpaqueToolCallName(ToolCallContent toolCall)
    {
        var toolType = toolCall.GetType();
        return toolType.GetProperty("Name")?.GetValue(toolCall) as string
            ?? toolType.GetProperty("ToolName")?.GetValue(toolCall) as string
            ?? toolType.Name;
    }

    private static object? ResolveOpaqueToolCallArguments(ToolCallContent toolCall)
    {
        var toolType = toolCall.GetType();
        return toolType.GetProperty("Arguments")?.GetValue(toolCall)
            ?? toolType.GetProperty("Input")?.GetValue(toolCall)
            ?? toolType.GetProperty("Parameters")?.GetValue(toolCall);
    }

    private static ChatClientAgentRunOptions CreateRunOptions(
        AgentDefinition agent,
        ProviderProfile provider,
        string model,
        bool hasApprovalTools,
        ResponseContinuationToken? continuationToken,
        bool forceOmitTemperature,
        AgentStructuredOutputContract? structuredOutput)
    {
        var chatOptions = CreateModelCompatibleChatOptions(
            provider,
            model,
            (float)agent.Temperature,
            forceOmitTemperature);
        chatOptions.AllowMultipleToolCalls = !hasApprovalTools;
        ApplyStructuredResponseFormat(chatOptions, structuredOutput);

        return new ChatClientAgentRunOptions(chatOptions)
        {
            AllowBackgroundResponses = agent.EnableBackgroundResponses && SupportsBackgroundResponses(provider),
            ContinuationToken = continuationToken
        };
    }

    internal static void ApplyStructuredResponseFormat(
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
