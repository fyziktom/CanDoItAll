using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Modules.LlmChats.Persistence;

public sealed class AuditedLlmChatStreamingInvocationPort(
    ILlmStreamingInvocationPort inner,
    ILlmChatOperationEvidenceSink evidenceSink,
    IProviderModelCapabilityResolver capabilityResolver,
    ILlmChatOperationScopeAccessor operationScope,
    TimeProvider timeProvider) : ILlmStreamingInvocationPort
{
    public async IAsyncEnumerable<LlmStreamingUpdate> StreamAsync(
        LlmInvocationRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var operationId = operationScope.Current?.OperationId
            ?? throw new InvalidOperationException("An LLM Chat operation scope is required for invocation audit.");
        var requestedEffort = request.Settings?.ThinkingEffort;
        var effectiveEffort = requestedEffort ??
            capabilityResolver.ResolveProviderDefaultThinkingEffort(request.Provider, request.Model);
        LlmStreamingAttemptStarted? activeAttempt = null;
        Exception? streamFailure = null;
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
                        await evidenceSink.MarkProviderDispatchStartedAsync(
                            operationId,
                            started.StartedAtUtc,
                            cancellationToken).ConfigureAwait(false);
                        break;
                    case LlmStreamingCompleted completed:
                        await RecordAsync(
                            request,
                            operationId,
                            requestedEffort,
                            effectiveEffort,
                            completed.AttemptOrdinal,
                            completed.AttemptUsage,
                            LlmChatInvocationOutcome.Succeeded,
                            string.Empty,
                            RequireStartedAt(activeAttempt, completed.AttemptOrdinal),
                            completed.CompletedAtUtc).ConfigureAwait(false);
                        activeAttempt = null;
                        break;
                    case LlmStreamingFailed failed:
                        await RecordAsync(
                            request,
                            operationId,
                            requestedEffort,
                            effectiveEffort,
                            failed.AttemptOrdinal,
                            failed.AttemptUsage,
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
                    operationId,
                    requestedEffort,
                    effectiveEffort,
                    activeAttempt.AttemptOrdinal,
                    LlmUsage.Zero,
                    LlmChatInvocationOutcome.Cancelled,
                    LlmChatErrorCodes.Cancelled,
                    activeAttempt.StartedAtUtc,
                    timeProvider.GetUtcNow()).ConfigureAwait(false);
            }

            throw streamFailure;
        }

        if (activeAttempt is not null)
        {
            var completedAtUtc = timeProvider.GetUtcNow();
            await RecordAsync(
                request,
                operationId,
                requestedEffort,
                effectiveEffort,
                activeAttempt.AttemptOrdinal,
                LlmUsage.Zero,
                LlmChatInvocationOutcome.Failed,
                LlmChatErrorCodes.ProviderUnavailable,
                activeAttempt.StartedAtUtc,
                completedAtUtc).ConfigureAwait(false);
            yield return new LlmStreamingFailed(
                activeAttempt.AttemptOrdinal,
                LlmInvocationFailureKind.ProviderFailure,
                LlmUsage.Zero,
                false,
                completedAtUtc);
        }
    }

    private Task<LlmChatOperation> RecordAsync(
        LlmInvocationRequest request,
        LlmChatOperationId operationId,
        AgentReasoningEffortLevel? requestedEffort,
        AgentReasoningEffortLevel? effectiveEffort,
        int ordinal,
        LlmUsage usage,
        LlmChatInvocationOutcome outcome,
        string failureCode,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc)
        => evidenceSink.RecordInvocationAsync(new LlmChatInvocationRecord(
            operationId,
            request.Provider.Id,
            request.Provider.Kind,
            request.Provider.Name,
            request.Model,
            requestedEffort,
            effectiveEffort,
            ordinal,
            usage,
            outcome,
            failureCode,
            startedAtUtc,
            completedAtUtc,
            request.CorrelationId), CancellationToken.None);

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
}
