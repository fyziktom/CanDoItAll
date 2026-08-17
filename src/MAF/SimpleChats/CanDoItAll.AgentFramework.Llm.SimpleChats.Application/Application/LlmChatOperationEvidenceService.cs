using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

public sealed class LlmChatOperationEvidenceService(
    ILlmChatOperationRepository operationRepository,
    ILlmChatInvocationRecordRepository invocationRepository,
    ILlmChatUnitOfWork unitOfWork,
    ILlmChatOperationScopeAccessor operationScope,
    TimeProvider timeProvider,
    LlmChatOperationEventJournal eventJournal) : ILlmChatOperationEvidenceSink
{
    private const int MaximumConcurrencyAttempts = 4;

    public Task<LlmChatOperation> MarkTurnAdmittedAsync(
        LlmChatOperationId operationId,
        DateTimeOffset admittedAtUtc,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            operationId,
            operation => LlmChatOperationTransitions.MarkTurnAdmitted(operation, admittedAtUtc),
            null,
            cancellationToken);

    public Task<LlmChatOperation> MarkProviderDispatchStartedAsync(
        LlmChatOperationId operationId,
        LlmStreamingAttemptStarted attempt,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            operationId,
            operation => LlmChatOperationTransitions.MarkProviderDispatchStarted(operation, attempt.StartedAtUtc),
            (operation, token) => eventJournal.AppendAttemptStartedAsync(operation.Id, attempt, token),
            cancellationToken,
            appendWhenUnchanged: true);

    public async Task<LlmChatOperation> RecordInvocationAsync(
        LlmChatInvocationRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        for (var attempt = 1; attempt <= MaximumConcurrencyAttempts; attempt++)
        {
            var updated = await unitOfWork.ExecuteAsync(async transactionCancellationToken =>
            {
                var current = await RequireAsync(record.OperationId, transactionCancellationToken)
                    .ConfigureAwait(false);
                var transitioned = LlmChatOperationTransitions.MarkProviderDispatchReturned(
                    current,
                    record.CompletedAtUtc);
                if (!await TryReplaceAsync(
                        transitioned,
                        current.ConcurrencyToken,
                        transactionCancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                await invocationRepository.AppendAsync(record, transactionCancellationToken).ConfigureAwait(false);
                await eventJournal.AppendAttemptFinishedAsync(record, transactionCancellationToken)
                    .ConfigureAwait(false);
                return await RequireAsync(record.OperationId, transactionCancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
            if (updated is not null)
            {
                return updated;
            }
        }

        throw new InvalidOperationException("The LLM Chat invocation evidence could not win its concurrency update.");
    }

    public Task<LlmChatOperation> CompleteTranscriptAsync(
        LlmChatOperationId operationId,
        DateTimeOffset completedAtUtc,
        long resultingTranscriptRevision,
        Guid assistantEntryId,
        string model,
        LlmUsage usage,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            operationId,
            operation => LlmChatOperationTransitions.CompleteTranscript(
                operation,
                completedAtUtc,
                resultingTranscriptRevision,
                assistantEntryId),
            (operation, token) => eventJournal.AppendStateChangedAsync(operation, model, usage, token),
            cancellationToken);

    public Task<LlmChatOperation> RequestCancellationAsync(
        LlmChatOperationId operationId,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            operationId,
            operation => LlmChatOperationTransitions.RequestCancellation(operation, requestedAtUtc),
            (operation, token) => eventJournal.AppendStateChangedAsync(operation, cancellationToken: token),
            cancellationToken);

    public Task<LlmChatOperation> CompleteCancellationAsync(
        LlmChatOperationId operationId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            operationId,
            operation => LlmChatOperationTransitions.CompleteCancellation(operation, completedAtUtc),
            StateEventAppender(),
            cancellationToken);

    public Task<LlmChatOperation> CompleteFailureAsync(
        LlmChatOperationId operationId,
        DateTimeOffset completedAtUtc,
        string failureCode,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            operationId,
            operation => LlmChatOperationTransitions.CompleteFailure(operation, completedAtUtc, failureCode),
            StateEventAppender(),
            cancellationToken);

    public Task<LlmChatOperation> RequireRecoveryAsync(
        LlmChatOperationId operationId,
        string failureCode,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            operationId,
            operation => LlmChatOperationTransitions.RequireRecovery(operation, failureCode),
            (operation, token) => eventJournal.AppendStateChangedAsync(
                operation,
                cancellationToken: token),
            cancellationToken);

    private Func<LlmChatOperation, CancellationToken, Task<LlmChatOperationEvent>> StateEventAppender()
        => async (operation, token) =>
        {
            var records = await invocationRepository.ListAsync(operation.Id, token).ConfigureAwait(false);
            var usage = records.Aggregate(LlmUsage.Zero, (total, record) => total.Add(record.Usage));
            return await eventJournal.AppendStateChangedAsync(
                operation,
                usage: usage,
                cancellationToken: token).ConfigureAwait(false);
        };

    private async Task<LlmChatOperation> UpdateAsync(
        LlmChatOperationId operationId,
        Func<LlmChatOperation, LlmChatOperation> transition,
        Func<LlmChatOperation, CancellationToken, Task<LlmChatOperationEvent>>? appendEvent,
        CancellationToken cancellationToken,
        bool appendWhenUnchanged = false)
    {
        for (var attempt = 1; attempt <= MaximumConcurrencyAttempts; attempt++)
        {
            var updated = await unitOfWork.ExecuteAsync(async transactionCancellationToken =>
            {
                var current = await RequireAsync(operationId, transactionCancellationToken).ConfigureAwait(false);
                var transitioned = transition(current);
                if (ReferenceEquals(transitioned, current) || transitioned == current)
                {
                    if (appendWhenUnchanged && appendEvent is not null)
                    {
                        await appendEvent(current, transactionCancellationToken).ConfigureAwait(false);
                    }

                    return current;
                }

                if (!await TryReplaceAsync(
                        transitioned,
                        current.ConcurrencyToken,
                        transactionCancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                if (appendEvent is not null)
                {
                    await appendEvent(transitioned, transactionCancellationToken).ConfigureAwait(false);
                }

                return await RequireAsync(operationId, transactionCancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
            if (updated is not null)
            {
                return updated;
            }
        }

        throw new InvalidOperationException("The LLM Chat operation could not win its concurrency update.");
    }

    private async Task<LlmChatOperation> RequireAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken)
        => await operationRepository.TryGetAsync(operationId, cancellationToken).ConfigureAwait(false)
           ?? throw new InvalidOperationException("The LLM Chat operation evidence target does not exist.");

    private Task<bool> TryReplaceAsync(
        LlmChatOperation operation,
        long expectedConcurrencyToken,
        CancellationToken cancellationToken)
        => operationScope.Current?.ExecutionLease is { } executionLease
            ? operationRepository.TryReplaceOwnedAsync(
                operation,
                expectedConcurrencyToken,
                executionLease,
                timeProvider.GetUtcNow(),
                cancellationToken)
            : operationRepository.TryReplaceAsync(
                operation,
                expectedConcurrencyToken,
                cancellationToken);
}
