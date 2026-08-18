using System.Text;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

public sealed class LlmChatStreamingPipeline(
    LlmChatOperationEventJournal eventJournal,
    LlmChatStreamingOptions options,
    TimeProvider timeProvider,
    LlmChatStreamingConsumerState consumerState)
{
    public async Task<LlmInvocationResult> ConsumeAsync(
        LlmChatOperationId operationId,
        IAsyncEnumerable<LlmStreamingUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updates);
        options.Validate();
        var response = new StringBuilder();
        var responseBytes = 0;
        var deltaEventCount = 0;
        var coalescer = new LlmChatTextDeltaCoalescer(options);
        var bufferedAttemptOrdinal = 0;
        LlmStreamingFailed? terminalFailure = null;
        await using var enumerator = updates.GetAsyncEnumerator(cancellationToken);
        Task<bool>? moveNextTask = null;
        try
        {
            while (true)
            {
                moveNextTask ??= enumerator.MoveNextAsync().AsTask();
                if (coalescer.HasBufferedContent)
                {
                    var delay = Task.Delay(
                        coalescer.GetRemainingDelay(timeProvider.GetUtcNow()),
                        timeProvider,
                        cancellationToken);
                    if (await Task.WhenAny(moveNextTask, delay).ConfigureAwait(false) == delay)
                    {
                        foreach (var chunk in coalescer.Flush(timeProvider.GetUtcNow()))
                        {
                            await AppendChunkAsync(bufferedAttemptOrdinal, chunk).ConfigureAwait(false);
                        }

                        continue;
                    }
                }

                if (!await moveNextTask.ConfigureAwait(false))
                {
                    break;
                }

                var update = enumerator.Current;
                moveNextTask = null;
                switch (update)
                {
                    case LlmStreamingAttemptStarted:
                        break;
                    case LlmStreamingTextDelta delta:
                        response.Append(delta.Delta);
                        responseBytes = checked(responseBytes + Encoding.UTF8.GetByteCount(delta.Delta));
                        EnsureResponseWithinBounds(response.Length, responseBytes);
                        bufferedAttemptOrdinal = delta.AttemptOrdinal;
                        foreach (var chunk in coalescer.Append(delta.Delta, timeProvider.GetUtcNow()))
                        {
                            await AppendChunkAsync(delta.AttemptOrdinal, chunk).ConfigureAwait(false);
                        }

                        EnsureDeltaEventsWithinBounds(deltaEventCount, coalescer.BufferedChunkCount);

                        break;
                    case LlmStreamingFailed failed:
                        terminalFailure = failed.RetryScheduled ? null : failed;
                        break;
                    case LlmStreamingCompleted completed:
                        foreach (var chunk in coalescer.Flush(timeProvider.GetUtcNow()))
                        {
                            await AppendChunkAsync(bufferedAttemptOrdinal, chunk).ConfigureAwait(false);
                        }

                        return new LlmInvocationResult(completed.Model, response.ToString(), completed.Usage);
                    default:
                        throw new InvalidOperationException("The streaming provider returned an unknown update.");
                }
            }

            foreach (var chunk in coalescer.Flush(timeProvider.GetUtcNow()))
            {
                await AppendChunkAsync(bufferedAttemptOrdinal, chunk).ConfigureAwait(false);
            }

            throw new LlmChatConversationEngineException(
                terminalFailure is null
                    ? LlmChatErrorCodes.ProviderUnavailable
                    : MapFailureCode(terminalFailure.FailureKind),
                "The streaming LLM invocation did not complete successfully.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (moveNextTask is { IsCompleted: false })
            {
                try
                {
                    await moveNextTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
            }

            throw;
        }

        async Task AppendChunkAsync(int attemptOrdinal, string chunk)
        {
            if (attemptOrdinal < 1)
            {
                throw new InvalidOperationException("A durable text chunk has no provider attempt.");
            }

            deltaEventCount = checked(deltaEventCount + 1);
            if (deltaEventCount > options.MaximumDeltaEvents)
            {
                ThrowStreamLimit("The streaming response exceeded its durable event limit.");
            }

            await eventJournal.AppendTextDeltaAsync(
                operationId,
                attemptOrdinal,
                chunk,
                timeProvider.GetUtcNow(),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private void EnsureDeltaEventsWithinBounds(int durableEvents, int bufferedEvents)
    {
        if (checked(durableEvents + bufferedEvents) > options.MaximumDeltaEvents)
        {
            ThrowStreamLimit("The streaming response exceeded its durable event limit.");
        }
    }

    private void EnsureResponseWithinBounds(int characters, int bytes)
    {
        if (characters > options.MaximumResponseCharacters || bytes > options.MaximumResponseBytes)
        {
            ThrowStreamLimit("The streaming response exceeded its aggregate size limit.");
        }
    }

    private void ThrowStreamLimit(string message)
    {
        consumerState.Abort(LlmChatStreamingConsumerAbortReason.StreamLimitExceeded);
        throw new LlmChatConversationEngineException(LlmChatErrorCodes.StreamLimitExceeded, message);
    }

    private static string MapFailureCode(LlmInvocationFailureKind kind)
        => kind switch
        {
            LlmInvocationFailureKind.InvalidRequest => LlmChatErrorCodes.ModelSettingsInvalid,
            LlmInvocationFailureKind.DeadlineExceeded => LlmChatErrorCodes.DeadlineExceeded,
            _ => LlmChatErrorCodes.ProviderUnavailable
        };
}
