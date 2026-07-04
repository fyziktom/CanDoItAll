using System.Runtime.CompilerServices;
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
        AgentRuntimeExecutionOptions runtimeOptions,
        CancellationToken cancellationToken,
        bool isApprovalContinuation = false)
    {
        if (!isApprovalContinuation && runtimeOptions.ContextIntent?.IsGovernedProcessStep == true)
        {
            return await runtimeAgent.CreateSessionAsync(cancellationToken);
        }

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

        if (ShouldRestoreSerializedSession(agent, provider, session))
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

    internal static ChatMessage CreateUserInputMessage(
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

        contents.AddRange(attachments.Select(attachment => new DataContent(attachment.Bytes, attachment.ContentType)
        {
            Name = string.IsNullOrWhiteSpace(attachment.Name) ? Path.GetFileName(attachment.SourcePath) : attachment.Name
        }));
        return new ChatMessage(ChatRole.User, contents);
    }

    private static string ResolveText(ChatMessage message)
        => string.Join(
            Environment.NewLine,
            message.Contents
                .OfType<TextContent>()
                .Select(content => content.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text)));

    private async IAsyncEnumerable<AgentResponseUpdate> RunProviderStreamingAsync(
        ProviderProfile provider,
        string model,
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        IEnumerable<ChatMessage> inputMessages,
        ChatClientAgentRunOptions runOptions,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var timeoutCancellation = new CancellationTokenSource(ResolveProviderNetworkTimeout(provider));
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellation.Token);
        var providerCancellationToken = linkedCancellation.Token;
        await using var dispatchLease = await providerStreamingDispatchGate.EnterAsync(
            provider,
            model,
            providerCancellationToken).ConfigureAwait(false);

        var updates = RunStreamingCoreAsync(
            runtimeAgent,
            runtimeSession,
            inputMessages,
            runOptions,
            providerCancellationToken);
        await using var enumerator = updates.GetAsyncEnumerator(providerCancellationToken);
        while (await MoveNextProviderStreamingUpdateAsync(
                   enumerator,
                   provider,
                   model,
                   timeoutCancellation,
                   cancellationToken).ConfigureAwait(false))
        {
            yield return enumerator.Current;
        }
    }

    private static async Task<bool> MoveNextProviderStreamingUpdateAsync(
        IAsyncEnumerator<AgentResponseUpdate> enumerator,
        ProviderProfile provider,
        string model,
        CancellationTokenSource timeoutCancellation,
        CancellationToken callerCancellationToken)
    {
        try
        {
            return await enumerator.MoveNextAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
            when (timeoutCancellation.IsCancellationRequested &&
                  !callerCancellationToken.IsCancellationRequested)
        {
            throw CreateProviderStreamingTimeoutException(provider, model, exception);
        }
    }

    private static TimeoutException CreateProviderStreamingTimeoutException(
        ProviderProfile provider,
        string model,
        Exception innerException)
    {
        var timeoutSeconds = Math.Round(ResolveProviderNetworkTimeout(provider).TotalSeconds);
        return new TimeoutException(
            $"Provider '{provider.Name}' streaming chat for model '{model}' exceeded the configured timeout of {timeoutSeconds:N0} second(s).",
            innerException);
    }

    private static IAsyncEnumerable<AgentResponseUpdate> RunStreamingCoreAsync(
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
            DataContent dataContent => new DataContent(dataContent.Data, dataContent.MediaType)
            {
                Name = dataContent.Name
            },
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
        AgentRuntimeExecutionOptions runtimeOptions)
    {
        var chatOptions = CreateModelCompatibleChatOptions(
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

    internal static void ApplyResponseFormat(
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

    internal static bool ShouldApplyStructuredOutputResponseFormat(AgentRuntimeExecutionOptions runtimeOptions)
    {
        ArgumentNullException.ThrowIfNull(runtimeOptions);

        return runtimeOptions.StructuredOutput is not null &&
               runtimeOptions.FinalizerMode != AgentFinalizerMode.Required;
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
        ChatSessionRecord session,
        AgentRuntimeExecutionOptions runtimeOptions)
    {
        if (runtimeOptions.ContextIntent?.IsGovernedProcessStep == true)
        {
            return "Creating an isolated Microsoft Agent Framework session for this governed process step.";
        }

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

    internal static bool ShouldUseFrameworkManagedHistory(
        AgentDefinition agent,
        ProviderProfile provider,
        AgentRuntimeExecutionOptions runtimeOptions)
    {
        ArgumentNullException.ThrowIfNull(runtimeOptions);

        return runtimeOptions.ContextIntent?.IsGovernedProcessStep == true ||
               ShouldUseFrameworkManagedHistory(agent, provider);
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
