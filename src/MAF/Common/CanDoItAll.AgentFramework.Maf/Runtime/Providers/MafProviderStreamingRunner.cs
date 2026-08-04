using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafProviderStreamingInvocation
{
    public static IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
        ProviderProfile provider,
        string model,
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        IEnumerable<ChatMessage> inputMessages,
        ChatClientAgentRunOptions runOptions,
        CancellationToken cancellationToken)
        => RunStreamingCoreAsync(
            runtimeAgent,
            runtimeSession,
            inputMessages,
            runOptions,
            cancellationToken);

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
}

internal static class MafAgentResponseSnapshotter
{
    public static AgentResponseUpdate SnapshotUpdate(
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
            FunctionCallContent functionCall => SnapshotToolCall(functionCall),
            McpServerToolCallContent mcpToolCall => SnapshotToolCall(mcpToolCall),
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
        var callId = RequireStableToolCallId(toolCall);
        return toolCall switch
        {
            FunctionCallContent functionCall => new FunctionCallContent(
                callId,
                functionCall.Name,
                SnapshotArguments(functionCall.Arguments)),
            McpServerToolCallContent mcpToolCall => new McpServerToolCallContent(
                callId,
                mcpToolCall.Name,
                mcpToolCall.ServerName)
            {
                Arguments = SnapshotArguments(mcpToolCall.Arguments)
            },
            _ => new FunctionCallContent(
                callId,
                ResolveOpaqueToolCallName(toolCall),
                SnapshotNamedValues(ResolveOpaqueToolCallArguments(toolCall)))
        };
    }

    private static string RequireStableToolCallId(ToolCallContent toolCall)
    {
        if (string.IsNullOrWhiteSpace(toolCall.CallId))
        {
            throw new InvalidOperationException(
                $"Cannot snapshot Microsoft Agent Framework tool call type '{toolCall.GetType().Name}' without a stable call identifier.");
        }

        return toolCall.CallId;
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
}
