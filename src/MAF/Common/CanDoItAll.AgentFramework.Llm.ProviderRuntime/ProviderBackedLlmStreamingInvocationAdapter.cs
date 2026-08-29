using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Llm.ProviderRuntime;

public sealed class ProviderBackedLlmStreamingInvocationAdapter(
    IProviderRuntimeDescriptorStore descriptorStore,
    IProviderRuntimePool runtimePool,
    TimeProvider timeProvider,
    ILogger<ProviderBackedLlmStreamingInvocationAdapter> logger) : ILlmStreamingInvocationPort
{
    public const int MaximumAttempts = ProviderBackedLlmInvocationAdapter.MaximumEmptyResponseAttempts;
    private const int ProviderUpdateBufferCapacity = 32;

    public async IAsyncEnumerable<LlmStreamingUpdate> StreamAsync(
        LlmInvocationRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var provider = request.Provider;
        var model = ProviderBackedLlmInvocationAdapter.ResolveModel(provider, request.Model);
        var payload = ProviderBackedLlmInvocationAdapter.CreateProviderRequest(request, provider, model);
        using var deadlineCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (request.Timeout is { } timeout)
        {
            deadlineCancellation.CancelAfter(timeout);
        }

        var aggregateUsage = LlmUsage.Zero;
        var acceptedCharacterCount = 0;
        for (var attemptOrdinal = 1; attemptOrdinal <= MaximumAttempts; attemptOrdinal++)
        {
            IProviderRuntimeHandle handle;
            StreamingDispatchPlan? dispatchPlan;
            try
            {
                handle = await GetRuntimeHandleAsync(provider, deadlineCancellation.Token).ConfigureAwait(false);
                dispatchPlan = ResolveDispatchPlan(handle, payload);
            }
            catch (OperationCanceledException) when (
                !cancellationToken.IsCancellationRequested && deadlineCancellation.IsCancellationRequested)
            {
                throw new LlmInvocationException(
                    LlmInvocationFailureKind.DeadlineExceeded,
                    provider.Name,
                    model,
                    request.CorrelationId,
                    usage: aggregateUsage);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                logger.LogWarning(
                    "LLM streaming runtime preparation failed. ProviderId={ProviderId} ProviderKind={ProviderKind} Model={Model} CorrelationId={CorrelationId}",
                    provider.Id,
                    provider.Kind,
                    model,
                    request.CorrelationId);
                throw new LlmInvocationException(
                    LlmInvocationFailureKind.ProviderFailure,
                    provider.Name,
                    model,
                    request.CorrelationId,
                    usage: aggregateUsage);
            }

            if (dispatchPlan is null)
            {
                throw new LlmInvocationException(
                    LlmInvocationFailureKind.ProviderFailure,
                    provider.Name,
                    model,
                    request.CorrelationId,
                    usage: aggregateUsage);
            }

            var startedAtUtc = timeProvider.GetUtcNow();
            yield return new LlmStreamingAttemptStarted(
                attemptOrdinal,
                provider.Id,
                provider.Kind,
                model,
                dispatchPlan.DeliveryMode,
                startedAtUtc);

            var emittedDelta = false;
            ProviderChatCompleted? providerCompletion = null;
            var attemptUsage = LlmUsage.Zero;
            Exception? dispatchFailure = null;
            var providerSequence = 0L;
            await using (var enumerator = DispatchAttemptAsync(
                handle,
                payload,
                dispatchPlan,
                deadlineCancellation.Token).GetAsyncEnumerator(deadlineCancellation.Token))
            {
                while (true)
                {
                    bool moved;
                    try
                    {
                        moved = await enumerator.MoveNextAsync().ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        dispatchFailure = exception;
                        break;
                    }

                    if (!moved)
                    {
                        break;
                    }

                    switch (enumerator.Current)
                    {
                        case ProviderChatTextDelta { Text.Length: > 0 } delta when providerCompletion is null:
                            if (acceptedCharacterCount > LlmMessage.MaximumTextLength - delta.Text.Length)
                            {
                                dispatchFailure = new InvalidOperationException(
                                    "Provider stream exceeded the assistant response size limit.");
                                break;
                            }

                            emittedDelta = true;
                            acceptedCharacterCount += delta.Text.Length;
                            yield return new LlmStreamingTextDelta(
                                attemptOrdinal,
                                delta.Text,
                                ++providerSequence);
                            break;
                        case ProviderChatCorrelationObserved:
                        case ProviderChatUsageObserved:
                            break;
                        case ProviderChatCompleted completed when providerCompletion is null:
                            providerCompletion = completed;
                            break;
                        case ProviderChatTextDelta:
                            break;
                        default:
                            dispatchFailure = new InvalidOperationException(
                                "Provider stream contained updates after its terminal completion.");
                            break;
                    }

                    if (dispatchFailure is not null)
                    {
                        break;
                    }
                }
            }

            if (dispatchFailure is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                throw dispatchFailure;
            }

            if (providerCompletion is not null)
            {
                try
                {
                    attemptUsage = new LlmUsage(
                        providerCompletion.InputTokens,
                        providerCompletion.OutputTokens,
                        providerCompletion.CachedInputTokens);
                    aggregateUsage = aggregateUsage.Add(attemptUsage);
                }
                catch (Exception exception) when (exception is ArgumentOutOfRangeException or OverflowException)
                {
                    dispatchFailure = exception;
                }
            }

            if (dispatchFailure is null && providerCompletion is not null && emittedDelta)
            {
                yield return new LlmStreamingCompleted(
                    attemptOrdinal,
                    providerCompletion.Model,
                    providerCompletion.FinishReason,
                    aggregateUsage,
                    dispatchPlan.DeliveryMode,
                    timeProvider.GetUtcNow())
                {
                    AttemptUsage = attemptUsage
                };
                yield break;
            }

            var failureKind = dispatchFailure is OperationCanceledException &&
                              !cancellationToken.IsCancellationRequested &&
                              deadlineCancellation.IsCancellationRequested
                ? LlmInvocationFailureKind.DeadlineExceeded
                : dispatchFailure is null && providerCompletion is not null
                    ? LlmInvocationFailureKind.EmptyResponse
                    : LlmInvocationFailureKind.ProviderFailure;
            if (dispatchFailure is not null)
            {
                logger.LogWarning(
                    "LLM streaming provider attempt failed. ProviderId={ProviderId} ProviderKind={ProviderKind} Model={Model} CorrelationId={CorrelationId} AttemptOrdinal={AttemptOrdinal} FailureKind={FailureKind} PartialOutputVisible={PartialOutputVisible}",
                    provider.Id,
                    provider.Kind,
                    model,
                    request.CorrelationId,
                    attemptOrdinal,
                    failureKind,
                    emittedDelta);
            }

            var retryScheduled = !emittedDelta &&
                                 failureKind != LlmInvocationFailureKind.DeadlineExceeded &&
                                 attemptOrdinal < MaximumAttempts;
            yield return new LlmStreamingFailed(
                attemptOrdinal,
                failureKind,
                aggregateUsage,
                retryScheduled,
                timeProvider.GetUtcNow())
            {
                AttemptUsage = attemptUsage
            };
            if (!retryScheduled)
            {
                yield break;
            }
        }
    }

    private async IAsyncEnumerable<ProviderChatStreamingUpdate> DispatchAttemptAsync(
        IProviderRuntimeHandle handle,
        ProviderChatCompletionRequest payload,
        StreamingDispatchPlan dispatchPlan,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<ProviderChatStreamingUpdate>(new BoundedChannelOptions(
            ProviderUpdateBufferCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });
        using var producerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var producer = ProduceUpdatesAsync(
            handle,
            payload,
            dispatchPlan,
            channel.Writer,
            producerCancellation.Token);
        try
        {
            await foreach (var update in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return update;
            }
        }
        finally
        {
            await producerCancellation.CancelAsync().ConfigureAwait(false);
            await producer.ConfigureAwait(false);
        }
    }

    private static async Task ProduceUpdatesAsync(
        IProviderRuntimeHandle handle,
        ProviderChatCompletionRequest payload,
        StreamingDispatchPlan dispatchPlan,
        ChannelWriter<ProviderChatStreamingUpdate> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = new ProviderDispatchQuery(
                payload.Provider,
                AgentProviderCapabilityKind.ChatCompletion,
                AgentProviderOperationKind.CompleteChat,
                payload.Model);
            await handle.DispatchAsync(
                new ProviderRuntimeDispatchRequest<ProviderChatCompletionRequest>(query, payload),
                async (context, token) =>
                {
                    ProviderBackedLlmInvocationAdapter.EnsureProviderKindMatches(
                        context.Descriptor,
                        context.Query.Provider);
                    if (dispatchPlan.StreamingDriver is not null)
                    {
                        await foreach (var update in dispatchPlan.StreamingDriver.StreamChatAsync(
                            context.Payload,
                            token).ConfigureAwait(false))
                        {
                            await writer.WriteAsync(update, token).ConfigureAwait(false);
                        }

                        return true;
                    }

                    var result = await dispatchPlan.CompletedDriver!.CompleteChatAsync(
                        context.Payload,
                        token).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(result.ResponseText))
                    {
                        await writer.WriteAsync(
                            new ProviderChatTextDelta(result.ResponseText),
                            token).ConfigureAwait(false);
                    }

                    await writer.WriteAsync(new ProviderChatCompleted(
                        result.Model,
                        result.InputTokens,
                        result.OutputTokens,
                        "completed")
                    {
                        ObservedUsage = result.ObservedUsage,
                        CachedInputTokens = result.CachedInputTokens
                    }, token).ConfigureAwait(false);
                    return true;
                },
                cancellationToken).ConfigureAwait(false);
            writer.TryComplete();
        }
        catch (Exception exception)
        {
            writer.TryComplete(exception);
        }
    }

    private async ValueTask<IProviderRuntimeHandle> GetRuntimeHandleAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken)
    {
        descriptorStore.Upsert(provider, secretReferenceIdentity: provider.ApiKeyEnvironmentVariable);
        return await runtimePool.GetRequiredAsync(provider.Id, cancellationToken).ConfigureAwait(false);
    }

    private static StreamingDispatchPlan? ResolveDispatchPlan(
        IProviderRuntimeHandle handle,
        ProviderChatCompletionRequest request)
    {
        if (request.Provider.SupportsStreaming &&
            handle.ProviderFactory.TryResolve<IProviderStreamingChatCompletionDriver>(
                request.Provider.Kind,
                out var streamingDriver) &&
            streamingDriver.ResolveStreamingMode(request) == ProviderChatStreamingMode.Incremental)
        {
            return new StreamingDispatchPlan(
                LlmStreamingDeliveryMode.Incremental,
                streamingDriver,
                null);
        }

        return handle.ProviderFactory.TryResolve<IProviderChatCompletionDriver>(
            request.Provider.Kind,
            out var completedDriver)
            ? new StreamingDispatchPlan(
                LlmStreamingDeliveryMode.CompletedFallback,
                null,
                completedDriver)
            : null;
    }

    private sealed record StreamingDispatchPlan(
        LlmStreamingDeliveryMode DeliveryMode,
        IProviderStreamingChatCompletionDriver? StreamingDriver,
        IProviderChatCompletionDriver? CompletedDriver);
}
