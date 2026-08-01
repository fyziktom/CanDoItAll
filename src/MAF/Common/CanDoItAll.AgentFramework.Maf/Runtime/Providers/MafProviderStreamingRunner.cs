using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal interface IMafProviderStreamingRunner
{
    IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
        ProviderProfile provider,
        string model,
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        IEnumerable<ChatMessage> inputMessages,
        ChatClientAgentRunOptions runOptions,
        CancellationToken cancellationToken);
}

internal sealed class MafProviderStreamingRunner : IMafProviderStreamingRunner
{
    private readonly IMafProviderStreamingDispatchGate providerStreamingDispatchGate;
    private readonly Func<ProviderProfile, TimeSpan> resolveStreamingIdleTimeout;
    private readonly Func<ProviderProfile, TimeSpan> resolveStreamingAbsoluteTimeout;

    public MafProviderStreamingRunner(
        IMafProviderStreamingDispatchGate providerStreamingDispatchGate)
        : this(
            providerStreamingDispatchGate,
            MafProviderRuntimeSettings.ResolveStreamingIdleTimeout,
            MafProviderRuntimeSettings.ResolveStreamingAbsoluteTimeout)
    {
    }

    internal MafProviderStreamingRunner(
        IMafProviderStreamingDispatchGate providerStreamingDispatchGate,
        Func<ProviderProfile, TimeSpan> resolveStreamingIdleTimeout)
        : this(
            providerStreamingDispatchGate,
            resolveStreamingIdleTimeout,
            provider => ResolveDefaultAbsoluteTimeout(resolveStreamingIdleTimeout(provider)))
    {
    }

    internal MafProviderStreamingRunner(
        IMafProviderStreamingDispatchGate providerStreamingDispatchGate,
        Func<ProviderProfile, TimeSpan> resolveStreamingIdleTimeout,
        Func<ProviderProfile, TimeSpan> resolveStreamingAbsoluteTimeout)
    {
        this.providerStreamingDispatchGate = providerStreamingDispatchGate
            ?? throw new ArgumentNullException(nameof(providerStreamingDispatchGate));
        this.resolveStreamingIdleTimeout = resolveStreamingIdleTimeout
            ?? throw new ArgumentNullException(nameof(resolveStreamingIdleTimeout));
        this.resolveStreamingAbsoluteTimeout = resolveStreamingAbsoluteTimeout
            ?? throw new ArgumentNullException(nameof(resolveStreamingAbsoluteTimeout));
    }

    public async IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
        ProviderProfile provider,
        string model,
        AIAgent runtimeAgent,
        AgentSession runtimeSession,
        IEnumerable<ChatMessage> inputMessages,
        ChatClientAgentRunOptions runOptions,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var dispatchLease = await providerStreamingDispatchGate.EnterAsync(
            provider,
            model,
            cancellationToken).ConfigureAwait(false);

        var idleTimeout = resolveStreamingIdleTimeout(provider);
        if (idleTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("The provider streaming idle timeout must be positive.");
        }

        var absoluteTimeout = resolveStreamingAbsoluteTimeout(provider);
        if (absoluteTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("The provider streaming absolute timeout must be positive.");
        }

        using var streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var updates = RunStreamingCoreAsync(
            runtimeAgent,
            runtimeSession,
            inputMessages,
            runOptions,
            streamCancellation.Token);
        var enumerator = updates.GetAsyncEnumerator(streamCancellation.Token);
        TimeoutException? idleTimeoutException = null;
        var streamStartedAt = Stopwatch.GetTimestamp();
        var lastSemanticProgressAt = streamStartedAt;
        try
        {
            while (true)
            {
                var watchdog = ResolveWatchdogDeadline(
                    streamStartedAt,
                    lastSemanticProgressAt,
                    idleTimeout,
                    absoluteTimeout);
                if (watchdog.Remaining <= TimeSpan.Zero)
                {
                    idleTimeoutException = await CreateWatchdogTimeoutAsync(
                        streamCancellation,
                        provider.Name,
                        model,
                        watchdog.IsAbsoluteDeadline,
                        watchdog.Timeout,
                        innerException: null).ConfigureAwait(false);
                    break;
                }

                var moveNext = await MoveNextWithWatchdogTimeoutAsync(
                    enumerator,
                    watchdog.Remaining,
                    cancellationToken).ConfigureAwait(false);
                if (moveNext.TimeoutException is not null)
                {
                    idleTimeoutException = await CreateWatchdogTimeoutAsync(
                        streamCancellation,
                        provider.Name,
                        model,
                        watchdog.IsAbsoluteDeadline,
                        watchdog.Timeout,
                        moveNext.TimeoutException).ConfigureAwait(false);
                    break;
                }

                if (!moveNext.HasNext)
                {
                    break;
                }

                var update = enumerator.Current;
                if (HasSemanticProgress(update))
                {
                    lastSemanticProgressAt = Stopwatch.GetTimestamp();
                }

                yield return update;
            }
        }
        finally
        {
            try
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception disposalException) when (idleTimeoutException is not null)
            {
                idleTimeoutException = new TimeoutException(
                    idleTimeoutException.Message,
                    new AggregateException(idleTimeoutException, disposalException));
            }
        }

        if (idleTimeoutException is not null)
        {
            throw idleTimeoutException;
        }
    }

    private static async Task<(bool HasNext, TimeoutException? TimeoutException)> MoveNextWithWatchdogTimeoutAsync(
        IAsyncEnumerator<AgentResponseUpdate> enumerator,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            var hasNext = await enumerator
                .MoveNextAsync()
                .AsTask()
                .WaitAsync(timeout, cancellationToken)
                .ConfigureAwait(false);
            return (hasNext, null);
        }
        catch (TimeoutException exception)
        {
            return (false, exception);
        }
    }

    private static async Task<TimeoutException> CreateWatchdogTimeoutAsync(
        CancellationTokenSource streamCancellation,
        string providerName,
        string model,
        bool isAbsoluteDeadline,
        TimeSpan timeout,
        Exception? innerException)
    {
        Exception? cancellationException = null;
        try
        {
            await streamCancellation.CancelAsync().ConfigureAwait(false);
        }
        catch (Exception cancellationFailure)
        {
            cancellationException = cancellationFailure;
        }

        var reason = isAbsoluteDeadline
            ? $"exceeded the absolute stream deadline of {timeout}"
            : $"made no semantic progress for {timeout}";
        var diagnostic = $"Provider stream '{providerName}' using model '{model}' {reason}. " +
            "The stream was canceled so runtime recovery can proceed.";
        var combinedFailure = innerException switch
        {
            null when cancellationException is null => null,
            null => cancellationException,
            _ when cancellationException is null => innerException,
            _ => new AggregateException(innerException, cancellationException)
        };
        return combinedFailure is null
            ? new TimeoutException(diagnostic)
            : new TimeoutException(diagnostic, combinedFailure);
    }

    private static (TimeSpan Remaining, bool IsAbsoluteDeadline, TimeSpan Timeout) ResolveWatchdogDeadline(
        long streamStartedAt,
        long lastSemanticProgressAt,
        TimeSpan semanticIdleTimeout,
        TimeSpan absoluteTimeout)
    {
        var semanticRemaining = semanticIdleTimeout - Stopwatch.GetElapsedTime(lastSemanticProgressAt);
        var absoluteRemaining = absoluteTimeout - Stopwatch.GetElapsedTime(streamStartedAt);
        return absoluteRemaining <= semanticRemaining
            ? (absoluteRemaining, true, absoluteTimeout)
            : (semanticRemaining, false, semanticIdleTimeout);
    }

    private static TimeSpan ResolveDefaultAbsoluteTimeout(TimeSpan idleTimeout)
    {
        var doubledTicks = checked(idleTimeout.Ticks * 2);
        return TimeSpan.FromTicks(doubledTicks);
    }

    private static bool HasSemanticProgress(AgentResponseUpdate update)
    {
        if (update.FinishReason is not null)
        {
            return true;
        }

        return update.Contents.Any(content => content switch
        {
            TextContent text => !string.IsNullOrWhiteSpace(text.Text),
            _ => true
        });
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
