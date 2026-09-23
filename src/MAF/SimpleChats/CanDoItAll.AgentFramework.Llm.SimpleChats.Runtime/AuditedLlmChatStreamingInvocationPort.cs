using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime;

public sealed class AuditedLlmChatStreamingInvocationPort(
    ILlmStreamingInvocationPort inner,
    ILlmChatOperationEvidenceSink evidenceSink,
    IProviderModelCapabilityResolver capabilityResolver,
    ILlmChatOperationScopeAccessor operationScope,
    TimeProvider timeProvider,
    LlmChatStreamingConsumerState consumerState) : ILlmStreamingInvocationPort
{
    public async IAsyncEnumerable<LlmStreamingUpdate> StreamAsync(
        LlmInvocationRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var operationId = operationScope.Current?.OperationId
            ?? throw new InvalidOperationException("An LLM Chat operation scope is required for invocation audit.");
        request = LlmChatHistoryContext.Attach(request, operationId, operationScope.Current?.HistoryCaller);
        var firstHistoryAttempt = request.History.Attempts.Count;
        var requestedEffort = request.Settings?.ThinkingEffort;
        var effectiveEffort = requestedEffort ??
            capabilityResolver.ResolveProviderDefaultThinkingEffort(request.Provider, request.Model);
        consumerState.Reset();
        LlmStreamingAttemptStarted? activeAttempt = null;
        Exception? streamFailure = null;
        try
        {
            await using (var enumerator = inner.StreamAsync(request, cancellationToken)
                .GetAsyncEnumerator(cancellationToken))
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
                        streamFailure = exception;
                        break;
                    }

                    if (!moved)
                    {
                        break;
                    }

                    var update = enumerator.Current;
                    switch (update)
                    {
                        case LlmStreamingAttemptStarted started:
                            activeAttempt = started;
                            firstHistoryAttempt = request.History.Attempts.Count;
                            await evidenceSink.MarkProviderDispatchStartedAsync(
                                operationId,
                                started,
                                cancellationToken).ConfigureAwait(false);
                            break;
                        case LlmStreamingCompleted completed:
                            await RecordAsync(
                                request,
                                firstHistoryAttempt,
                                operationId,
                                requestedEffort,
                                effectiveEffort,
                                completed.AttemptOrdinal,
                                completed.Model,
                                completed.DeliveryMode,
                                completed.FinishReason,
                                completed.AttemptUsage,
                                LlmChatInvocationEvidenceCapture.ResolveReturnedUsage(completed.AttemptUsage),
                                LlmChatInvocationOutcome.Succeeded,
                                string.Empty,
                                RequireStartedAt(activeAttempt, completed.AttemptOrdinal),
                                completed.CompletedAtUtc).ConfigureAwait(false);
                            activeAttempt = null;
                            break;
                        case LlmStreamingFailed failed:
                            await RecordAsync(
                                request,
                                firstHistoryAttempt,
                                operationId,
                                requestedEffort,
                                effectiveEffort,
                                failed.AttemptOrdinal,
                                RequireActiveAttempt(activeAttempt, failed.AttemptOrdinal).Model,
                                RequireActiveAttempt(activeAttempt, failed.AttemptOrdinal).DeliveryMode,
                                string.Empty,
                                failed.AttemptUsage,
                                LlmChatInvocationEvidenceCapture.ResolveReturnedUsage(failed.AttemptUsage),
                                LlmChatInvocationOutcome.Failed,
                                AuditedLlmChatInvocationPort.MapFailureCode(failed.FailureKind),
                                RequireStartedAt(activeAttempt, failed.AttemptOrdinal),
                                failed.CompletedAtUtc).ConfigureAwait(false);
                            activeAttempt = null;
                            break;
                    }

                    yield return update;
                }
            }

            if (streamFailure is OperationCanceledException)
            {
                if (activeAttempt is not null)
                {
                    await RecordAsync(
                        request,
                        firstHistoryAttempt,
                        operationId,
                        requestedEffort,
                        effectiveEffort,
                        activeAttempt.AttemptOrdinal,
                        activeAttempt.Model,
                        activeAttempt.DeliveryMode,
                        string.Empty,
                        LlmUsage.Zero,
                        LlmChatInvocationUsageEvidenceStatus.MissingAfterProviderActivity,
                        LlmChatInvocationOutcome.Cancelled,
                        LlmChatErrorCodes.Cancelled,
                        activeAttempt.StartedAtUtc,
                        timeProvider.GetUtcNow()).ConfigureAwait(false);
                    activeAttempt = null;
                }

                throw streamFailure;
            }

            if (activeAttempt is not null)
            {
                var completedAtUtc = timeProvider.GetUtcNow();
                await RecordAsync(
                    request,
                    firstHistoryAttempt,
                    operationId,
                    requestedEffort,
                    effectiveEffort,
                    activeAttempt.AttemptOrdinal,
                    activeAttempt.Model,
                    activeAttempt.DeliveryMode,
                    string.Empty,
                    LlmUsage.Zero,
                    LlmChatInvocationUsageEvidenceStatus.MissingAfterProviderActivity,
                    LlmChatInvocationOutcome.Failed,
                    LlmChatErrorCodes.ProviderUnavailable,
                    activeAttempt.StartedAtUtc,
                    completedAtUtc).ConfigureAwait(false);
                var failedAttemptOrdinal = activeAttempt.AttemptOrdinal;
                activeAttempt = null;
                yield return new LlmStreamingFailed(
                    failedAttemptOrdinal,
                    LlmInvocationFailureKind.ProviderFailure,
                    LlmUsage.Zero,
                    false,
                    completedAtUtc);
            }
        }
        finally
        {
            if (activeAttempt is not null)
            {
                await RecordAsync(
                    request,
                    firstHistoryAttempt,
                    operationId,
                    requestedEffort,
                    effectiveEffort,
                    activeAttempt.AttemptOrdinal,
                    activeAttempt.Model,
                    activeAttempt.DeliveryMode,
                    string.Empty,
                    LlmUsage.Zero,
                    LlmChatInvocationUsageEvidenceStatus.MissingAfterProviderActivity,
                    cancellationToken.IsCancellationRequested
                        ? LlmChatInvocationOutcome.Cancelled
                        : LlmChatInvocationOutcome.Failed,
                    cancellationToken.IsCancellationRequested
                        ? LlmChatErrorCodes.Cancelled
                        : ResolveConsumerFailureCode(),
                    activeAttempt.StartedAtUtc,
                    timeProvider.GetUtcNow()).ConfigureAwait(false);
            }
        }
    }

    private Task<LlmChatOperation> RecordAsync(
        LlmInvocationRequest request,
        int firstHistoryAttempt,
        LlmChatOperationId operationId,
        AgentReasoningEffortLevel? requestedEffort,
        AgentReasoningEffortLevel? effectiveEffort,
        int ordinal,
        string model,
        LlmStreamingDeliveryMode deliveryMode,
        string finishReason,
        LlmUsage usage,
        LlmChatInvocationUsageEvidenceStatus usageStatus,
        LlmChatInvocationOutcome outcome,
        string failureCode,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc)
    {
        var pricing = LlmChatInvocationEvidenceCapture.CapturePricing(request, model, usage, usageStatus);
        return evidenceSink.RecordInvocationAsync(new LlmChatInvocationRecord(
            operationId,
            request.Provider.Id,
            request.Provider.Kind,
            request.Provider.Name,
            model,
            requestedEffort,
            effectiveEffort,
            ordinal,
            usage,
            outcome,
            failureCode,
            startedAtUtc,
            completedAtUtc,
            request.CorrelationId,
            deliveryMode,
            finishReason,
            usageStatus,
            pricing.Status,
            pricing.ProviderCostUsd,
            pricing.CalculatedCostUsd,
            pricing.PricingProfileHash,
            pricing.PricingVersion) {
                HistoryAttempts = request.History.Attempts.EvidenceSnapshot(firstHistoryAttempt)
            }, CancellationToken.None);
    }

    private static DateTimeOffset RequireStartedAt(
        LlmStreamingAttemptStarted? activeAttempt,
        int terminalAttemptOrdinal)
    {
        if (activeAttempt?.AttemptOrdinal != terminalAttemptOrdinal)
        {
            throw new InvalidOperationException("Streaming invocation terminal update has no matching active attempt.");
        }

        return activeAttempt.StartedAtUtc;
    }

    private static LlmStreamingAttemptStarted RequireActiveAttempt(
        LlmStreamingAttemptStarted? activeAttempt,
        int terminalAttemptOrdinal)
    {
        RequireStartedAt(activeAttempt, terminalAttemptOrdinal);
        return activeAttempt!;
    }

    private string ResolveConsumerFailureCode()
    {
        var failureCode = consumerState.ResolveFailureCode();
        return string.IsNullOrEmpty(failureCode)
            ? LlmChatErrorCodes.ProviderUnavailable
            : failureCode;
    }
}
